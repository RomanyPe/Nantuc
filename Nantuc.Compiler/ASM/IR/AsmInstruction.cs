using Nantuc.Kernel.Common;

namespace Nantuc.Compiler.ASM.IR;

public abstract class AsmItem;
public sealed class AsmLabel(string name) : AsmItem
{
    public string Name { get; } = name;
}

public sealed class AsmData(byte[] data, int alignment = 8) : AsmItem
{
    public byte[] Data { get; } = data; public int Alignment { get; set; } = alignment;
}
public sealed class AsmInstruction(uint rawInstruction) : AsmItem
{
    public OpCode OpCode { get; } = InstructionDecoder.GetOpCode(rawInstruction);
    public OpCodeSize Size { get; } = InstructionDecoder.GetDataSizeCode(rawInstruction);
    public RegType FirstReg { get; } = InstructionDecoder.GetReg1(rawInstruction);
    public RegType SecondReg { get; } = InstructionDecoder.GetReg2(rawInstruction);

    public ulong? Immediate { get; } = null;

    public string? TargetLabel { get; } = null;

    public AsmInstruction(uint rawInstruction, ulong immediate)
        : this(rawInstruction)
    {
        Immediate = immediate;
    }

    public AsmInstruction(uint rawInstruction, string targetLabel)
        : this(rawInstruction)
    {
        TargetLabel = targetLabel;
    }

    public AsmInstruction(uint rawInstruction, ulong absoluteTarget, bool isAbsolute)
        : this(rawInstruction)
    {
        if (isAbsolute)
            Immediate = absoluteTarget;
        else
            TargetLabel = absoluteTarget.ToString(); // некрасиво, но как fallback
    }

    public bool HasImmediate => Immediate.HasValue;
    public bool IsJump => TargetLabel != null;
}

