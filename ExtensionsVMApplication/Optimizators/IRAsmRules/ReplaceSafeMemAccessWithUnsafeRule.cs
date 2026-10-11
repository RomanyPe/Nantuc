using Compiller.ASM;
using Compiller.ASM.Optimizators;
using Kernel.Common;

namespace ExtensionsVMApplication.Optimizators.IRAsmRules;

public sealed class ReplaceSafeMemAccessWithUnsafeRule : IPeepholeRule
{
    public RamSize SizeRam { get; set; }

    public bool TryOptimize(List<AsmItem> items, int index, PeepholeLog log, out int newIndex)
    {
        newIndex = index;

        // Работаем только с инструкциями
        if (items[index] is not AsmInstruction instr)
            return false;

        // Проверяем, является ли instr безопасной операцией доступа к памяти
        if (!IsSafeMemoryAccess(instr.OpCode))
            return false;

        // Получаем информацию о размере данных
        int dataSize = GetDataSize(instr.Size);
        if (dataSize == 0)
            return false; // неизвестный размер – не трогаем

        ulong address = SizeRam.Bytes + 1;
        bool addressKnown = false;

        // Случай 1: LOAD/STORE с непосредственным адресом
        if (instr.OpCode is OpCode.LOAD or OpCode.STORE)
        {
            if (instr.Immediate.HasValue)
            {
                address = instr.Immediate.Value;
                addressKnown = true;
            }
        }
        // Случай 2: LOAD_IND/STORE_IND – ищем предшествующую LDI в адресный регистр
        else if (instr.OpCode is OpCode.LOAD_IND or OpCode.STORE_IND)
        {
            if (index > 0 && items[index - 1] is AsmInstruction prev &&
                prev.OpCode == OpCode.LDI &&
                prev.FirstReg == instr.SecondReg &&  // адресный регистр
                prev.Immediate.HasValue)
            {
                address = prev.Immediate.Value;
                addressKnown = true;
            }
        }

        if (!addressKnown)
            return false;

        // Проверка границ и выравнивания
        if (address + (ulong)dataSize > SizeRam.Bytes)
            return false;

        if (address % (ulong)dataSize != 0)
            return false;

        // Заменяем OpCode на небезопасный
        OpCode unsafeOp = GetUnsafeOpCode(instr.OpCode);
        if (unsafeOp == instr.OpCode)
            return false; // нет небезопасного аналога

        // Создаём новую инструкцию с тем же набором параметров, но другим OpCode
        AsmInstruction? newInstr = CreateUnsafeInstruction(unsafeOp, instr);
        if (newInstr == null)
            return false;

        // Заменяем элемент списка
        items[index] = newInstr;

        log.AppendLine($"Replaced {instr.OpCode} with {unsafeOp} at index {index}, address=0x{address:X}");

        // После замены можно проверить следующую инструкцию (но не откатываемся)
        newIndex = index + 1;
        return true;
    }

    private static bool IsSafeMemoryAccess(OpCode op) =>
        op == OpCode.LOAD || op == OpCode.STORE ||
        op == OpCode.LOAD_IND || op == OpCode.STORE_IND;

    private static OpCode GetUnsafeOpCode(OpCode safeOp) => safeOp switch
    {
        OpCode.LOAD => OpCode.LOAD_UNSAFE,
        OpCode.STORE => OpCode.STORE_UNSAFE,
        OpCode.LOAD_IND => OpCode.LOAD_IND_UNSAFE,
        OpCode.STORE_IND => OpCode.STORE_IND_UNSAFE,
        _ => safeOp
    };

    private static int GetDataSize(OpCodeSize size) => size switch
    {
        OpCodeSize.S8 => 1,
        OpCodeSize.S16 => 2,
        OpCodeSize.S32 => 4,
        OpCodeSize.S64 => 8,
        _ => 0
    };

    private static AsmInstruction? CreateUnsafeInstruction(OpCode unsafeOp, AsmInstruction original)
    {
        uint raw;
        switch (unsafeOp)
        {
            case OpCode.LOAD_UNSAFE:
                raw = InstructionEncoder.EncodeI(
                    OpCode.LOAD_UNSAFE.ToUint(),
                    original.FirstReg.ToUint(),
                    original.Size.ToUint());
                return new AsmInstruction(raw, original.Immediate!.Value);

            case OpCode.STORE_UNSAFE:
                raw = InstructionEncoder.EncodeI(
                    OpCode.STORE_UNSAFE.ToUint(),
                    original.FirstReg.ToUint(),
                    original.Size.ToUint());
                return new AsmInstruction(raw, original.Immediate!.Value);

            case OpCode.LOAD_IND_UNSAFE:
                raw = InstructionEncoder.Encode(
                    OpCode.LOAD_IND_UNSAFE.ToUint(),
                    original.FirstReg.ToUint(),
                    original.SecondReg.ToUint(),
                    original.Size.ToUint());
                return new AsmInstruction(raw);

            case OpCode.STORE_IND_UNSAFE:
                raw = InstructionEncoder.Encode(
                    OpCode.STORE_IND_UNSAFE.ToUint(),
                    original.FirstReg.ToUint(),
                    original.SecondReg.ToUint(),
                    original.Size.ToUint());
                return new AsmInstruction(raw);

            default:
                return null;
        }
    }
}