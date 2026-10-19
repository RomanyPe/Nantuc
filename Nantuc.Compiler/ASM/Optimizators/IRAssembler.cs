using Nantuc.Kernel.Common;
using Nantuc.Kernel.Diagnostics;

namespace Nantuc.Compiler.ASM.Optimizators;

public class IRAssembler(ulong baseAddress = 0) : AssemblerBase(baseAddress)
{
    private readonly List<AsmItem> _items = new(64);
    private readonly HashSet<string> _labelNames = new(StringComparer.Ordinal);
    // Добавляет метку в список. Проверяет уникальность имени.
    public override void MarkLabel(string name)
    {
        if (!_labelNames.Add(name))
        {
            // Найдём позицию существующей метки для сообщения об ошибке
            long existingPos = 0;
            long currentPos = 0;
            foreach (var item in _items)
            {
                if (item is AsmLabel lab && lab.Name == name)
                {
                    existingPos = currentPos;
                    break;
                }
                currentPos++;
            }
            ThrowHelper.ThrowMiniC(ErrorCode.Asm_DuplicateLabel, name, existingPos);
            return;
        }
        _items.Add(new AsmLabel(name));
    }

    // Записывает 32-битную инструкцию без 64-битных данных
    public override void EmitInstruction(uint instruction)
    {
        _items.Add(new AsmInstruction(instruction));
    }

    // Записывает инструкцию с последующим 64-битным операндом
    public override void EmitInstruction64(uint instruction, ulong data)
    {
        var op = InstructionDecoder.GetOpCode(instruction);
        if (!InstructionDecoder.HasNeed64IntData(op))
        {
            ThrowHelper.ThrowMiniC(ErrorCode.Asm_InvalidInstruction, op);
        }
        _items.Add(new AsmInstruction(instruction, data));
    }

    public override void EmitJump(uint jmpOpcode, string label)
    {
        _items.Add(new AsmInstruction(jmpOpcode, label));
    }

    public override void EmitJumpToAbsolute(uint jmpOpcode, ulong absoluteTarget)
    {
        _items.Add(new AsmInstruction(jmpOpcode, absoluteTarget, true));
    }

    // Для поддержки LDI с меткой (если используется)
    public override void EmitInstruction64WithLabel(uint instruction, string label)
    {
        _items.Add(new AsmInstruction(instruction, targetLabel: label));
    }
    public override void EmitData(string label, byte[] data)
    {
        MarkLabel(label);
        _items.Add(new AsmData(data));
    }
    public List<AsmItem> GetItems() => _items;
    public override byte[] Build()
    {
        // Первый проход: вычисляем адреса меток в байтовом потоке
        var labelAddresses = new Dictionary<string, ulong>();
        ulong currentOffset = 0;
        foreach (var item in _items)
        {
            switch (item)
            {
                case AsmLabel label:
                    labelAddresses[label.Name] = _baseAddress + currentOffset;
                    break;
                case AsmInstruction instr:
                    currentOffset += 4;
                    if (instr.HasImmediate || instr.IsJump)
                    {
                        currentOffset = (currentOffset + 7) & ~7UL;
                        currentOffset += 8;
                    }
                    break;
                case AsmData data:
                    // выравнивание
                    currentOffset = (currentOffset + (ulong)data.Alignment - 1) & ~((ulong)data.Alignment - 1);
                    currentOffset += (ulong)data.Data.Length;
                    break;
            }
        }

        // Второй проход: пишем байты
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        currentOffset = 0;
        byte[] emptyPadding = new byte[7]; 
        foreach (var item in _items)
        {
            switch (item)
            {
                case AsmLabel:
                    continue; // метки не пишутся
                case AsmInstruction instr:
                    {
                        // Кодируем заголовок обратно в uint
                        uint raw = InstructionEncoder.Encode(
                            instr.OpCode.ToUint(),
                            (uint)instr.FirstReg,
                            (uint)instr.SecondReg,
                            (uint)instr.Size);
                        writer.Write(raw);
                        currentOffset += 4;

                        if (instr.HasImmediate || instr.IsJump)
                        {
                            // выравнивание
                            int pad = (int)((long)currentOffset + 7 & ~7L) - (int)currentOffset;
                            if (pad > 0)
                                writer.Write(emptyPadding.AsSpan(0, pad));


                            currentOffset += (ulong)pad;

                            ulong data;
                            if (instr.IsJump)
                            {
                                if (!labelAddresses.TryGetValue(instr.TargetLabel!, out var targetAddr))
                                    ThrowHelper.ThrowMiniC(ErrorCode.Asm_UndefinedLabel, instr.TargetLabel!, 0);
                                data = targetAddr;
                            }
                            else
                            {
                                data = instr.Immediate!.Value;
                            }
                            writer.Write(data);
                            currentOffset += 8;
                        }

                        break;
                    }

                case AsmData data:
                    {
                        // выравнивание
                        int pad = (int)((currentOffset + (ulong)data.Alignment - 1) & ~((ulong)data.Alignment - 1)) - (int)currentOffset;
                        if (pad > 0) writer.Write(emptyPadding.AsSpan(0, pad));
                        writer.Write(data.Data);
                        currentOffset += (ulong)pad + (ulong)data.Data.Length;
                        break;
                    }
            }
        }

        return ms.ToArray();
    }
}

