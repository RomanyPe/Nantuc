using System.Collections.Immutable;

namespace Nantuc.VmApplication.Extensions.Disassembler;

public sealed class DisassemblyResult(
    ImmutableArray<DecodedInstruction> instructions,
    ImmutableArray<DecodedDataChunk> dataChunks,
    ulong baseAddress,
    int codeSize,
    int totalSize,
    int bytesConsumed)
{
    public ImmutableArray<DecodedInstruction> Instructions { get; } = instructions;
    public ImmutableArray<DecodedDataChunk> DataChunks { get; } = dataChunks;
    public ulong BaseAddress { get; } = baseAddress;
    public int CodeSize { get; } = codeSize;
    public int TotalSize { get; } = totalSize;
    public int BytesConsumed { get; } = bytesConsumed;

    public bool HasDataSection => DataChunks.Length > 0;

    public override string ToString() => InstructionFormatter.Format(this);
}