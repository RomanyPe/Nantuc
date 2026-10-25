using Nantuc.Kernel.Common;
using Nantuc.VmApplication.Project;
using System.Collections.Immutable;
using System.Text;

namespace Nantuc.VmApplication.Extensions.Disassembler
{
    internal static class Disassembler
    {
        extension(CompilationResult res)
        {
            public DisassemblyResult Disassemble(int? codeSize = null)
            {
                ThrowIfNotSucces(res);

                ReadOnlySpan<byte> program = res.Program;
                ulong baseAddress = res.StartAdress;
                int size = program.Length;

                // Если codeSize не задан — весь поток считается кодом (обратная совместимость)
                int actualCodeSize = codeSize ?? size;
                if (actualCodeSize < 0 || actualCodeSize > size)
                    throw new ArgumentOutOfRangeException(nameof(codeSize),
                        $"codeSize ({actualCodeSize}) must be in [0, {size}]");

                var instrBuilder = ImmutableArray.CreateBuilder<DecodedInstruction>();
                var dataBuilder = ImmutableArray.CreateBuilder<DecodedDataChunk>();

                int pos = 0;
                ulong currentIp = baseAddress;

                // ---------- Секция кода ----------
                while (pos < actualCodeSize && pos + 4 <= size)
                {
                    uint raw = BitConverter.ToUInt32(program.Slice(pos, 4));
                    var decoded = InstructionDecoder.DecodeRawInstToEnums(raw);

                    ulong nextIp = currentIp + 4;
                    int nextPos = pos + 4;
                    int byteLength = 4;
                    ulong? data64 = null;

                    if (InstructionDecoder.HasNeed64IntData(decoded.OpCode))
                    {
                        // Выравнивание строго по абсолютному IP, как в CPU
                        ulong alignedIp = nextIp.AlignUp(8UL);
                        int pad = (int)(alignedIp - nextIp);
                        nextPos += pad;
                        nextIp = alignedIp;

                        if (nextPos + 8 <= size)
                        {
                            data64 = BitConverter.ToUInt64(program.Slice(nextPos, 8));
                            nextPos += 8;
                            nextIp += 8;
                            byteLength += pad + 8;
                        }
                        else
                        {
                            // Данные обрезаны — фиксируем инструкцию как есть и выходим
                            instrBuilder.Add(new DecodedInstruction(
                                currentIp, raw,
                                decoded.OpCode, decoded.Reg1, decoded.Reg2, decoded.DataSizeCode,
                                Data64: null,
                                ByteLength: byteLength + pad));
                            pos = size;
                            break;
                        }
                    }

                    instrBuilder.Add(new DecodedInstruction(
                        currentIp, raw,
                        decoded.OpCode, decoded.Reg1, decoded.Reg2, decoded.DataSizeCode,
                        data64, byteLength));

                    currentIp = nextIp;
                    pos = nextPos;
                }

                // ---------- Секция данных ----------
                // Входим, только если реально дошли до codeSize (или прошли его из-за data64).
                // Если код оборвался на "неполной" инструкции — оставшиеся байты не трактуем как данные
                // (полное соответствие строковой версии).
                if (pos >= actualCodeSize)
                {
                    while (pos < size)
                    {
                        int chunkSize = Math.Min(8, size - pos);

                        // Собираем 8 байт в 64-битное число (Little-Endian)
                        ulong chunkVal = 0;
                        byte[] bytes = new byte[chunkSize];
                        for (int i = 0; i < chunkSize; i++)
                        {
                            bytes[i] = program[pos + i];
                            chunkVal |= (ulong)program[pos + i] << (i * 8);
                        }

                        // Пытаемся декодировать как строку (char = UTF-16LE, 2 байта на символ)
                        string? decodedStr = null;
                        var strBuf = new StringBuilder();
                        for (int i = 0; i + 1 < chunkSize; i += 2)
                        {
                            ushort c = (ushort)(program[pos + i] | (program[pos + i + 1] << 8));
                            if (c == 0) break; // Конец строки
                            strBuf.Append(c >= 32 && c < 127 ? (char)c : '.');
                        }
                        if (strBuf.Length > 0)
                            decodedStr = strBuf.ToString();

                        dataBuilder.Add(new DecodedDataChunk(
                            currentIp, chunkVal, bytes, decodedStr, chunkSize));

                        pos += chunkSize;
                        currentIp += (ulong)chunkSize;
                    }
                }

                return new DisassemblyResult(
                    instrBuilder.ToImmutable(),
                    dataBuilder.ToImmutable(),
                    baseAddress,
                    actualCodeSize,
                    totalSize: size,
                    bytesConsumed: pos);
            }
        }
        private static void ThrowIfNotSucces(CompilationResult res)
        {
            if (!res.Success)
                throw new InvalidOperationException();
        }
    }
}