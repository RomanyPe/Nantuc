using Nantuc.Compiler.ASM.Utils;
using System.Collections.Immutable;
using System.Text;

namespace Nantuc.VmApplication.Extensions.Disassembler;

public static class InstructionFormatter
{
    // -------- helpers ----------
    public static string Format(DisassemblyResult result)
    {
        var sb = new StringBuilder();
        Format(sb, result);
        return sb.ToString();
    }

    public static void Format(StringBuilder sb, DisassemblyResult result)
    {
        foreach (var inst in result.Instructions)
            Format(sb, inst);

        if (result.HasDataSection)
        {
            sb.AppendLine($"--- data section ({result.TotalSize - result.CodeSize} bytes) ---");
            foreach (var chunk in result.DataChunks)
                Format(sb, chunk);
        }
    }

    public static string Format(DecodedInstruction inst)
    {
        var sb = new StringBuilder();
        Format(sb, inst);
        return sb.ToString();
    }

    public static void Format(StringBuilder sb, DecodedInstruction inst)
    {
        string mnemonic = InstructionEncoder.Decode(inst.RawInstruction);

        sb.Append($"{inst.Address:X8}: {inst.RawInstruction:X8}   {mnemonic}");

        if (inst.HasData64)
        {
            ulong data = inst.Data64!.Value;
            sb.Append($"  data = 0x{data:X16} ({data})");
        }

        sb.AppendLine();
    }

    public static void Format(StringBuilder sb, DecodedDataChunk chunk)
    {
        // Формируем hex-строку байт с выравниванием (как в строковой версии)
        var hexBytes = new StringBuilder(8 * 3);
        for (int i = 0; i < 8; i++)
        {
            if (i < chunk.ByteLength) hexBytes.Append($"{chunk.Bytes[i]:X2} ");
            else hexBytes.Append("   ");
        }

        sb.Append($"{chunk.Address:X8}: {hexBytes} .data 0x{chunk.Value:X16}");

        if (!string.IsNullOrEmpty(chunk.DecodedString))
            sb.Append($"  \"{chunk.DecodedString}\"");

        sb.AppendLine();
    }
}
