using Nantuc.Common;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Diagnostics;

namespace Nantuc.Compiler.ASM.IR.Emitеer;

public partial class IRAssembler
{
    // ============================================================
    // Второй проход: эмиссия байтов. Offset пересчитывается той же
    // Align/AdvanceInstructionOffset-логикой, что и в проходе 1.
    //
    // ВНИМАНИЕ: padding имеет размер 7 — рассчитан на alignment ≤ 8
    // (все текущие вызовы EmitData используют 1 или 8).
    // ============================================================
    private ulong EmitItem(BinaryWriter writer, AsmItem item, ulong offset,
                           Dictionary<string, ulong> labelAddresses,
                           ReadOnlySpan<byte> padding)
    {
        return item switch
        {
            AsmLabel => offset,
            AsmInstruction instr => EmitInstructionItem(writer, instr, offset, labelAddresses, padding),
            AsmData data => EmitDataItem(writer, data, offset, padding),
            _ => offset,
        };
    }

    private ulong EmitInstructionItem(BinaryWriter writer, AsmInstruction instr, ulong offset,
                                      Dictionary<string, ulong> labelAddresses,
                                      ReadOnlySpan<byte> padding)
    {
        writer.Write(InstructionEncoder.Encode(
            instr.OpCode.ToUint(),
            (uint)instr.FirstReg,
            (uint)instr.SecondReg,
            (uint)instr.Size));
        offset += 4;

        if (!instr.HasImmediate && !instr.IsJump)
            return offset;

        offset = PadTo(writer, offset, 8, padding);

        ulong data = instr.IsJump
            ? ResolveJumpTarget(instr, labelAddresses)
            : instr.Immediate!.Value;

        writer.Write(data);
        return offset + 8;
    }

    private ulong ResolveJumpTarget(AsmInstruction instr, Dictionary<string, ulong> labelAddresses)
    {
        if (!labelAddresses.TryGetValue(instr.TargetLabel!, out var targetAddr))
            ErrorReporter.Throw(ErrorCode.Asm_UndefinedLabel, 
                DiagnosticData.Of(instr.TargetLabel!), 
                DiagnosticData.Of(0));
        return targetAddr;
    }

    private static ulong EmitDataItem(BinaryWriter writer, AsmData data, ulong offset,
                                      ReadOnlySpan<byte> padding)
    {
        offset = PadTo(writer, offset, (ulong)data.Alignment, padding);
        writer.Write(data.Data);
        return offset + (ulong)data.Data.Length;
    }

    private static ulong PadTo(BinaryWriter writer, ulong offset, ulong alignment,
                               ReadOnlySpan<byte> padding)
    {
        ulong aligned = Align(offset, alignment);
        int pad = (int)(aligned - offset);
        if (pad > 0)
            writer.Write(padding[..pad]);
        return aligned;
    }
}