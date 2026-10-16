using System.Collections.Immutable;

namespace VMApplication.Project.Disassembler;

public sealed class DisassemblyResult(
    ImmutableArray<DecodedInstruction> instructions,
    ulong baseAddress,
    int totalSize,
    int bytesConsumed)
{
    public ImmutableArray<DecodedInstruction> Instructions { get; } = instructions;
    public ulong BaseAddress { get; } = baseAddress;
    public int TotalSize { get; } = totalSize;
    public int BytesConsumed { get; } = bytesConsumed;

    public override string ToString() => InstructionFormatter.Format(this);
}
