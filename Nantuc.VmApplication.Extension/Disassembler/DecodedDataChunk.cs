namespace Nantuc.VmApplication.Extensions.Disassembler;

public readonly record struct DecodedDataChunk(
    ulong Address,        // IP чанка
    ulong Value,          // собранное 64-битное значение (Little-Endian)
    byte[] Bytes,         // реальные байты чанка (1..8)
    string? DecodedString,// декодированная строка (если получилось)
    int ByteLength);