using Nantuc.Common;
using Nantuc.Diagnostics;

namespace Nantuc.Compiler.ASM.IR.Emitеer;

public partial class IRAssembler
{
    // ============================================================
    // Первый проход: вычисление адресов меток и размеров секций.
    // Offset-логика (выравнивание инструкций/данных) вынесена в
    // Align/AdvanceInstructionOffset — общие с EmitItem.
    // ============================================================
    private Dictionary<string, ulong> ComputeLabelAddresses()
    {
        var labels = new Dictionary<string, ulong>();
        ulong offset = 0;
        int dataStart = -1;

        foreach (var item in _items)
        {
            switch (item)
            {
                case AsmLabel label:
                    labels[label.Name] = BaseAddress + offset;
                    break;

                case AsmData data:
                    offset = Align(offset, (ulong)data.Alignment);
                    if (dataStart < 0 && data.Data.Length > 0)
                        dataStart = (int)offset;
                    offset += (ulong)data.Data.Length;
                    break;

                case AsmInstruction instr:
                    offset = AdvanceInstructionOffset(instr, offset);
                    break;
            }
        }

        int totalSize = (int)offset;
        DataSectionStart = dataStart >= 0 ? dataStart : totalSize;
        DataSectionSize = totalSize - DataSectionStart;
        CodeSectionSize = DataSectionStart;

        return labels;
    }

    private static ulong AdvanceInstructionOffset(AsmInstruction instr, ulong offset)
    {
        offset += 4;
        if (instr.HasImmediate || instr.IsJump)
            offset = Align(offset, 8) + 8;
        return offset;
    }

    private static ulong Align(ulong offset, ulong alignment)
        => offset.AlignUp(alignment);
}