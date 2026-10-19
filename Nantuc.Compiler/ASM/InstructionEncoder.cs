using Nantuc.Kernel.Common;
using static Nantuc.Kernel.Common.InstructionDecoder;

namespace Nantuc.Compiler.ASM;

/// <summary>
/// Энкодер инструкций для процессора.
/// Формат инструкции (32 бита):
///   Bits 0-7:   OpCode
///   Bits 8-12:  Reg1 (целевой/основной регистр)
///   Bits 13-17: Reg2 (второй регистр-источник)
///   Bits 18-19: OpCodeSize (размер данных: 0=S8, 1=S16, 2=S32, 3=S64)
///   Bits 20-31: Не используется
/// </summary>
public static class InstructionEncoder
{
    #region Методы кодирования

    /// <summary>
    /// Кодирует инструкцию формата R (регистр-регистр): ADD, SUB, MOV, AND, OR, XOR
    /// </summary>
    public static uint Encode(uint opcode, uint regDst, uint regSrc, uint sizeCode)
    {
        return opcode | (regDst << 8) | (regSrc << 13) | (sizeCode << 18);
    }

    /// <summary>
    /// Кодирует инструкцию формата R (регистр-регистр): ADD, SUB, MOV, AND, OR, XOR
    /// </summary>
    public static uint EncodeR(uint opcode, uint regDst, uint regSrc)
    {
        return opcode | (regDst << 8) | (regSrc << 13);
    }

    /// <summary>
    /// Кодирует инструкцию формата I (регистр-константа/адрес): LDI, LOAD, STORE
    /// sizeCode - размер операнда (S8, S16, S32, S64)
    /// Для LDI: sizeCode определяет, сколько байт значащие
    /// Для LOAD/STORE: sizeCode определяет размер загружаемых/сохраняемых данных
    /// </summary>
    public static uint EncodeI(uint opcode, uint reg, uint sizeCode)
    {
        return opcode | (reg << 8) | (sizeCode << 18);
    }

    /// <summary>
    /// Кодирует инструкцию LDI (загрузка константы) с автоматическим S64
    /// </summary>
    public static uint EncodeLDI(uint reg)
    {
        return EncodeI(OpCode.LDI.ToUint(), reg, (uint)OpCodeSize.S64);
    }

    /// <summary>
    /// Кодирует инструкцию LOAD с указанием размера данных
    /// </summary>
    public static uint EncodeLOAD(uint regDst, uint sizeCode)
    {
        return EncodeI(OpCode.LOAD.ToUint(), regDst, sizeCode);
    }

    /// <summary>
    /// Кодирует инструкцию STORE с указанием размера данных
    /// </summary>
    public static uint EncodeSTORE(uint regSrc, uint sizeCode)
    {
        return EncodeI(OpCode.STORE.ToUint(), regSrc, sizeCode);
    }

    /// <summary>
    /// Кодирует инструкцию формата U (один регистр): INC, DEC, NOT, PUSH, POP
    /// </summary>
    public static uint EncodeU(uint opcode, uint reg)
    {
        return opcode | (reg << 8);
    }

    /// <summary>
    /// Кодирует инструкцию перехода (JMP, JZ, JNZ, JG, JL)
    /// Адрес всегда 64-битный (S64)
    /// </summary>
    public static uint EncodeJ(uint opcode)
    {
        return opcode | (OpCodeSize.S64.ToUint() << 18);
    }

    /// <summary>
    /// Кодирует инструкцию CALL (вызов подпрограммы)
    /// </summary>
    public static uint EncodeCALL() => EncodeJ(OpCode.CALL.ToUint());

    /// <summary>
    /// Кодирует инструкцию RET (возврат из подпрограммы)
    /// </summary>
    public static uint EncodeRET() => OpCode.RET.ToUint();

    /// <summary>
    /// Кодирует инструкцию HALT (вызов сна)
    /// </summary>
    public static uint EncodeHALT() => OpCode.HALT.ToUint();

    /// <summary>
    /// Кодирует инструкцию WAKE (возврат из сна)
    /// </summary>
    public static uint EncodeWAKE() => OpCode.WAKE.ToUint();
    /// <summary>
    /// Кодирует END
    /// </summary>
    public static uint EncodeEND() => OpCode.END.ToUint();

    /// <summary>
    /// Кодирует NOP
    /// </summary>
    public static uint EncodeNOP() => OpCode.NOP.ToUint();


    extension(BinaryWriter writer)
    {
    #endregion

        #region Вспомогательные методы для работы с буфером

        /// <summary>
        /// Записывает инструкцию и выравнивает поток под 8 байт (для последующего 64-битного данного)
        /// </summary>
        public void WriteInstruction(uint instruction)
        {
            writer.Write(instruction);
        }

        /// <summary>
        /// Выравнивает поток до границы 8 байт (для 64-битных операндов)
        /// </summary>
        public void Align8()
        {
            long pos = writer.BaseStream.Position;
            long pad = ((pos + 7) & ~7) - pos;
            if (pad > 0)
                writer.Write(new byte[pad]);
        }

        /// <summary>
        /// Записывает инструкцию + 64-битный операнд (с выравниванием)
        /// </summary>
        public void WriteInstructionWithData64(uint instruction, ulong data)
        {
            writer.Write(instruction);
            Align8(writer);
            writer.Write(data);
        }
    }

    public static uint EncodeLOAD_IND(uint regDst, uint regAddr, uint sizeCode)
    {
        return Encode(OpCode.LOAD_IND.ToUint(), regDst, regAddr, sizeCode);
    }

    public static uint EncodeSTORE_IND(uint regSrc, uint regAddr, uint sizeCode)
    {
        return Encode(OpCode.STORE_IND.ToUint(), regSrc, regAddr, sizeCode);
    }

    public static uint EncodeSHR(uint regDst, uint regSrc) => EncodeR(OpCode.SHR.ToUint(), regDst, regSrc);
    public static uint EncodeMULT_INT(uint regDst, uint regSrc) => EncodeR(OpCode.MULT_INT.ToUint(), regDst, regSrc);
    #endregion

    #region Декодирование (для отладки)

    private static string R1(uint i) => GetReg1(i).RegName();
    private static string R2(uint i) => GetReg2(i).RegName();
    private static string Size(uint i) => GetDataSizeCode(i).SizeName();

    /// <summary>
    /// Декодирует инструкцию в читаемый вид (для отладки)
    /// </summary>
    public static string Decode(uint instruction)
    {
        var opcode = GetOpCode(instruction);
        var op = opcode.OpName();

        return opcode switch
        {
            OpCode.NOP or 
            OpCode.END or 
            OpCode.HALT or 
            OpCode.WAKE or 
            OpCode.RET
                => op,

            OpCode.WAKE_INT or
            OpCode.INC or 
            OpCode.DEC or 
            OpCode.NOT or 
            OpCode.PUSH or 
            OpCode.POP or 
            OpCode.CALL_IND or
            OpCode.JMP_IND
                => $"{op} {R1(instruction)}",

            OpCode.MOV or 
            OpCode.ADD or 
            OpCode.SUB or 
            OpCode.AND or 
            OpCode.OR or 
            OpCode.XOR or 
            OpCode.IN or 
            OpCode.OUT or 
            OpCode.MULT_INT or 
            OpCode.SHR or 
            OpCode.DIV or
            OpCode.TEST or
            OpCode.CMP
                => $"{op} {R1(instruction)}, {R2(instruction)}",

            OpCode.JMP or 
            OpCode.JZ or 
            OpCode.JNZ or
            OpCode.JG or 
            OpCode.JL or 
            OpCode.CALL
                => $"{op} [data64]",

            OpCode.LOAD => $"{op}.{Size(instruction)} {R1(instruction)}, [data64]",
            OpCode.STORE => $"{op}.{Size(instruction)} [data64], {R1(instruction)}",
            OpCode.LOAD_IND => $"{op}.{Size(instruction)} {R1(instruction)}, {R2(instruction)}",
            OpCode.STORE_IND => $"{op}.{Size(instruction)} {R1(instruction)}, {R2(instruction)}",
            OpCode.LOAD_UNSAFE => $"{op}.{Size(instruction)} {R1(instruction)}, [data64]",
            OpCode.STORE_UNSAFE => $"{op}.{Size(instruction)} [data64], {R1(instruction)}",
            OpCode.LOAD_IND_UNSAFE => $"{op}.{Size(instruction)} {R1(instruction)}, {R2(instruction)}",
            OpCode.STORE_IND_UNSAFE => $"{op}.{Size(instruction)} {R1(instruction)}, {R2(instruction)}",
            OpCode.LDI => $"{op} {R1(instruction)}, [data64]",

            _ => $"UNKNOWN 0x{opcode.ToUint():X2}"
        };
    }


    extension(OpCodeSize size)
    {
        public uint ToUint() => (uint)size;
        public string SizeName() => size switch
        {
            OpCodeSize.S8 => "S8",
            OpCodeSize.S16 => "S16",
            OpCodeSize.S32 => "S32",
            OpCodeSize.S64 => "S64",
            _ => "???"
        };
    }

    extension(RegType r)
    {
        public uint ToUint() => (uint)r;

        public string RegName() => r switch
        {
            RegType.r0 => "rZ",
            RegType.r1 => "r0",
            RegType.r2 => "r1",
            RegType.r3 => "r2",
            RegType.r4 => "r3",
            RegType.r5 => "r4",
            RegType.r6 => "r5",
            RegType.r7 => "r6",
            RegType.r8 => "r7",
            RegType.r9 => "r8",
            RegType.r10 => "r9",
            RegType.r11 => "r10",
            RegType.r12 => "r11",
            RegType.r13 => "r12",
            RegType.r14 => "r13",
            RegType.r15 => "r14",
            RegType.r16 => "r15",
            RegType.r17 => "r16",
            RegType.r18 => "r17",
            RegType.r19 => "r18",
            RegType.r20 => "r19",
            RegType.r21 => "r20",
            RegType.r22 => "r21",
            RegType.r23 => "r22",
            RegType.r24 => "r23",
            RegType.r25 => "r24",
            RegType.r26 => "r26",
            RegType.rFL => "rFL",
            RegType.r27 => "r27",
            RegType.rSP => "rSP",
            RegType.rHP => "rHP",
            RegType.rIP => "rIP",
            _ => "r?"
        };
    }

    extension(OpCode op)
    {
        public uint ToUint() => (uint)op;

        public string OpName() => op switch
        {
            OpCode.NOP => "NOP",
            OpCode.END => "END",
            OpCode.MOV => "MOV",
            OpCode.LOAD => "LOAD",
            OpCode.STORE => "STORE",
            OpCode.LDI => "LDI",
            OpCode.LOAD_IND => "LOAD_IND",
            OpCode.STORE_IND => "STORE_IND",
            OpCode.ADD => "ADD",
            OpCode.SUB => "SUB",
            OpCode.MULT_INT => "MULT_INT",
            OpCode.SHR => "SHR",
            OpCode.INC => "INC",
            OpCode.DEC => "DEC",
            OpCode.DIV => "DIV",
            OpCode.AND => "AND",
            OpCode.OR => "OR",
            OpCode.XOR => "XOR",
            OpCode.NOT => "NOT",
            OpCode.JMP => "JMP",
            OpCode.JZ => "JZ",
            OpCode.JNZ => "JNZ",
            OpCode.JG => "JG",
            OpCode.JL => "JL",
            OpCode.PUSH => "PUSH",
            OpCode.POP => "POP",
            OpCode.CALL => "CALL",
            OpCode.RET => "RET",
            OpCode.IN => "IN",
            OpCode.OUT => "OUT",
            OpCode.CALL_IND => "CALL_IND",
            OpCode.JMP_IND => "JMP_INT",
            OpCode.HALT => "HALT",
            OpCode.WAKE => "WAKE",
            OpCode.WAKE_INT => "WAKE_INT",
            OpCode.LOAD_UNSAFE => "LOAD_UNSAFE",
            OpCode.STORE_UNSAFE => "STORE_UNSAFE",
            OpCode.STORE_IND_UNSAFE => "STORE_IND_UNSAFE",
            OpCode.LOAD_IND_UNSAFE => "LOAD_IND_UNSAFE",
            OpCode.CMP => "CMP",
            OpCode.TEST => "TEST",
            _ => op.ToUint().ToString()
        };
    }

    #endregion

}
