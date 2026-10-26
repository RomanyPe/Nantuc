using Nantuc.Compiler.ASM;
using Nantuc.Compiler.ASM.IR;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Kernel.Common;
using Nantuc.Kernel.Contracts;

namespace Nantuc.VmApplication.Extensions.Optimizators.IRAsmRules;

public sealed class ConstantFoldingRule : IPeepholeRule
{
    private readonly PeepholeLog log = new(PeepholeOptimizerExtension.ConstantFolding);
    public IReadOnlyLogOptimization GetLogs() => log;
    public void Clear() => log.Clear();

    public bool TryOptimize(List<AsmItem> items, int index, int passCount, out int newIndex)
    {
        newIndex = index;
        if (index < 2 || items[index] is not AsmInstruction opInstr)
            return false;

        if (items[index - 2] is not AsmInstruction ldi1 ||
            items[index - 1] is not AsmInstruction ldi2 ||
            ldi1.OpCode != OpCode.LDI ||
            ldi2.OpCode != OpCode.LDI ||
            !ldi1.Immediate.HasValue ||
            !ldi2.Immediate.HasValue)
            return false;

        if (HasLabelBetween(items, index - 2, index))
            return false;

        if (!IsBinaryArithmetic(opInstr.OpCode))
            return false;

        bool regsMatchDirect = ldi1.FirstReg == opInstr.FirstReg && ldi2.FirstReg == opInstr.SecondReg;
        bool regsMatchSwap = ldi1.FirstReg == opInstr.SecondReg && ldi2.FirstReg == opInstr.FirstReg;

        if (!regsMatchDirect && !regsMatchSwap)
            return false;

        ulong left = regsMatchDirect ? ldi1.Immediate.Value : ldi2.Immediate.Value;
        ulong right = regsMatchDirect ? ldi2.Immediate.Value : ldi1.Immediate.Value;

        // Для DIV проверяем деление на ноль
        if (opInstr.OpCode == OpCode.DIV && right == 0)
            return false;

        // Вычисляем результат
        ulong result = ComputeBinary(opInstr.OpCode, left, right);

        // Создаём новую LDI с результатом (регистр назначения = opInstr.FirstReg)
        uint newRaw = InstructionEncoder.Encode(
            OpCode.LDI.ToUint(),
            (uint)opInstr.FirstReg,
            0, // второй регистр не используется
            (uint)opInstr.Size);
        var newLdi = new AsmInstruction(newRaw, result);

        log.Append(passCount, items[index]);
        log.Append(passCount, items[index - 1]);
        log.Append(passCount,items[index - 2]);

        // Заменяем три инструкции одной
        items.RemoveAt(index);
        items.RemoveAt(index - 1);
        items.RemoveAt(index - 2);
        items.Insert(index - 2, newLdi);
        newIndex = index - 2; // продолжаем с новой инструкции
        return true;
    }

    private static bool IsBinaryArithmetic(OpCode op) =>
        op == OpCode.ADD || op == OpCode.SUB || op == OpCode.MULT_INT ||
        op == OpCode.DIV || op == OpCode.AND || op == OpCode.OR ||
        op == OpCode.XOR || op == OpCode.SHR;

    private static ulong ComputeBinary(OpCode op, ulong a, ulong b) => op switch
    {
        OpCode.ADD => a + b,
        OpCode.SUB => a - b,
        OpCode.MULT_INT => a * b,
        OpCode.DIV => a / b,
        OpCode.AND => a & b,
        OpCode.OR => a | b,
        OpCode.XOR => a ^ b,
        OpCode.SHR => a >> (int)b,
        _ => throw new InvalidOperationException($"Unsupported binary opcode: {op}")
    };

    private static bool HasLabelBetween(List<AsmItem> items, int start, int end)
    {
        for (int i = start + 1; i < end; i++)
        {
            if (items[i] is AsmLabel)
                return true;
        }
        return false;
    }
}
