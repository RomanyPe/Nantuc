namespace Nantuc.Compiler.ASM.IR;

public sealed class AsmData(byte[] data, int alignment = 8) : AsmItem
{
    public byte[] Data { get; } = data; public int Alignment { get; set; } = alignment;
}

