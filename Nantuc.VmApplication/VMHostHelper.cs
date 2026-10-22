using Nantuc.Compiler.ASM;
using Nantuc.Kernel.Common;
using Nantuc.Kernel.Contracts;
using Nantuc.VmApplication.Project;
using Nantuc.VmApplication.Project.Disassembler;
using System.Collections.Immutable;

namespace Nantuc.VmApplication;  

public static class VMHostHelper
{
    public static ResultDeCompilation DisassemblCode(ReadOnlySpan<byte> prog, ulong baseAddress = 0UL)
    {
        var text = Disassembler.Disassemble(prog, out int lenght, out int size, baseAddress);
        return new(text, lenght, size);
    }
    
public static DisassemblyResult Disassemble(
    ReadOnlySpan<byte> program,
    ulong baseAddress = 0)
    {
        int size = program.Length;
        var builder = ImmutableArray.CreateBuilder<DecodedInstruction>();

        int pos = 0;
        ulong currentIp = baseAddress;

        while (pos < size)
        {
            // Не хватает байт на саму инструкцию
            if (pos + 4 > size)
                break;

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
                    builder.Add(new DecodedInstruction(
                        currentIp, raw,
                        decoded.OpCode, decoded.Reg1, decoded.Reg2, decoded.DataSizeCode,
                        Data64: null,
                        ByteLength: byteLength + pad));
                    pos = size;
                    break;
                }
            }

            builder.Add(new DecodedInstruction(
                currentIp, raw,
                decoded.OpCode, decoded.Reg1, decoded.Reg2, decoded.DataSizeCode,
                data64, byteLength));

            currentIp = nextIp;
            pos = nextPos;
        }

        return new DisassemblyResult(
            builder.ToImmutable(),
            baseAddress,
            totalSize: size,
            bytesConsumed: pos);
    }
}
