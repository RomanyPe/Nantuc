using Kernel.Common;

namespace VMApplication.Project.Disassembler;

public readonly record struct DecodedInstruction(
    ulong Address,           // IP инструкции
    uint RawInstruction,    // исходные 4 байта
    OpCode OpCode,
    RegType Reg1,
    RegType Reg2,
    OpCodeSize DataSize,
    ulong? Data64,           // null, если операнда-64 нет
    int ByteLength)          // сколько байт заняла в потоке (4 + pad + 8 или 4)
    {

    public bool HasData64 => Data64.HasValue;

    public override string ToString() => InstructionFormatter.Format(this);
}
