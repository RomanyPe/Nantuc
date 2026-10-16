using Compiller.ASM;
using System.Text;

namespace VMApplication.Project.Disassembler;

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
}