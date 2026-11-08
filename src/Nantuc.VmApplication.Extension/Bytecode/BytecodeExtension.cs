using Nantuc.Common;
using Nantuc.VmApplication.Project;
using System.Buffers.Binary;

namespace Nantuc.VmApplication.Extensions.Bytecode;

public static class BytecodeExtension
{
    public enum OpCodeSafety { Safe, HasUnsafe, Unknown }

    public readonly record struct UnsafeOpCodeLocation(
        OpCode OpCode,
        int Offset,
        ulong Ip);

    extension(CompilationResult res)
    {
        public OpCodeSafety GetOpCodeSafety()
        {
            if (!res.Success)
                return OpCodeSafety.Unknown;

            ReadOnlySpan<byte> program = res.Program;
            ulong baseAddress = res.StartAdress;
            int size = program.Length;
            int? codeSize = res.CodeSize;
            int actualCodeSize = codeSize ?? size;
            if (actualCodeSize < 0 || actualCodeSize > size)
                return OpCodeSafety.Unknown;

            int pos = 0;
            ulong currentIp = baseAddress;

            while (pos < actualCodeSize)
            {
                if (pos + 4 > size)
                    return OpCodeSafety.Unknown;

                uint raw = BinaryPrimitives.ReadUInt32LittleEndian(program.Slice(pos, 4));
                OpCode opcode = InstructionDecoder.GetOpCode(raw);

                ulong nextIp = currentIp + 4;
                int nextPos = pos + 4;

                if (IsUnsafeOpcode(opcode))
                    return OpCodeSafety.HasUnsafe;

                if (InstructionDecoder.HasNeed64IntData(opcode))
                {
                    ulong alignedIp = nextIp.AlignUp(8UL);
                    int pad = (int)(alignedIp - nextIp);
                    nextPos += pad;
                    nextIp = alignedIp;

                    if (nextPos + 8 > size)
                        return OpCodeSafety.Unknown;

                    nextPos += 8;
                    nextIp += 8;
                }

                currentIp = nextIp;
                pos = nextPos;
            }

            return OpCodeSafety.Safe;
        }

        /// <summary>
        /// Возвращает все небезопасные OpCode и их местоположение.
        /// Offset — смещение относительно начала Program.
        /// Ip — виртуальный адрес инструкции.
        /// </summary>
        public IReadOnlyList<UnsafeOpCodeLocation> GetUnsafeOpCodes()
        {
            return res.GetUnsafeOpCodes(out _);
        }

        /// <summary>
        /// Возвращает все небезопасные OpCode и их местоположение.
        /// fullyScanned == false, если программу не удалось просканировать до конца.
        /// </summary>
        public IReadOnlyList<UnsafeOpCodeLocation> GetUnsafeOpCodes(out bool fullyScanned)
        {
            var result = new List<UnsafeOpCodeLocation>();
            fullyScanned = false;

            if (!res.Success)
                return result;

            ReadOnlySpan<byte> program = res.Program;
            ulong baseAddress = res.StartAdress;
            int size = program.Length;
            int? codeSize = res.CodeSize;
            int actualCodeSize = codeSize ?? size;

            if (actualCodeSize < 0 || actualCodeSize > size)
                return result;

            int pos = 0;
            ulong currentIp = baseAddress;

            while (pos < actualCodeSize)
            {
                if (pos + 4 > size)
                    return result;

                uint raw = BinaryPrimitives.ReadUInt32LittleEndian(program.Slice(pos, 4));
                OpCode opcode = InstructionDecoder.GetOpCode(raw);

                if (IsUnsafeOpcode(opcode))
                {
                    result.Add(new UnsafeOpCodeLocation(
                        OpCode: opcode,
                        Offset: pos,
                        Ip: currentIp));
                }

                ulong nextIp = currentIp + 4;
                int nextPos = pos + 4;

                if (InstructionDecoder.HasNeed64IntData(opcode))
                {
                    ulong alignedIp = nextIp.AlignUp(8UL);
                    int pad = (int)(alignedIp - nextIp);

                    nextPos += pad;
                    nextIp = alignedIp;

                    if (nextPos + 8 > size)
                        return result;

                    nextPos += 8;
                    nextIp += 8;
                }

                currentIp = nextIp;
                pos = nextPos;
            }

            fullyScanned = true;
            return result;
        }
    }

    public static bool IsUnsafeOpcode(OpCode opCode) => opCode is
        OpCode.LOAD_UNSAFE or
        OpCode.STORE_UNSAFE or
        OpCode.STORE_IND_UNSAFE or
        OpCode.LOAD_IND_UNSAFE;
}