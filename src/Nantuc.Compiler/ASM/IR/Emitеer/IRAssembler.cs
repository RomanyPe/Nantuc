using Nantuc.Common;
using Nantuc.Compiler.ASM.Base;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Diagnostics;

namespace Nantuc.Compiler.ASM.IR.Emitеer;

public partial class IRAssembler(ulong baseAddress, ErrorReporter ErrorReporter) : AssemblerBase(baseAddress, ErrorReporter)
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
            ErrorReporter.Throw(ErrorCode.Asm_DuplicateLabel, name, existingPos);
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
            ErrorReporter.Throw(ErrorCode.Asm_InvalidInstruction, op.OpName());
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
        _items.Add(new AsmData([], alignment: 8));
        if (!string.IsNullOrEmpty(label))
            MarkLabel(label);
        _items.Add(new AsmData(data, alignment: 1));
    }
    public List<AsmItem> GetItems() => _items;
    public override byte[] Build()
    {
        var labelAddresses = ComputeLabelAddresses();

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        Span<byte> padding = stackalloc byte[7];

        ulong offset = 0;
        foreach (var item in _items)
            offset = EmitItem(writer, item, offset, labelAddresses, padding);

        return ms.ToArray();
    }
}

