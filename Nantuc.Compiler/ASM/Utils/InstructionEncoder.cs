using Nantuc.Compiler.ASM.Base;
using Nantuc.Kernel.Common;
using static Nantuc.Kernel.Common.InstructionDecoder;

namespace Nantuc.Compiler.ASM.Utils;

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
            OpCode.SHL or
            OpCode.MOD or
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
            RegType.r0 => "r0",
            RegType.r1 => "r1",
            RegType.r2 => "r2",
            RegType.r3 => "r3",
            RegType.r4 => "r4",
            RegType.r5 => "r5",
            RegType.r6 => "r6",
            RegType.r7 => "r7",
            RegType.r8 => "r8",
            RegType.r9 => "r9",
            RegType.r10 => "r10",
            RegType.r11 => "r11",
            RegType.r12 => "r12",
            RegType.r13 => "r13",
            RegType.r14 => "r14",
            RegType.r15 => "r15",
            RegType.r16 => "r16",
            RegType.r17 => "r17",
            RegType.r18 => "r18",
            RegType.r19 => "r19",
            RegType.r20 => "r20",
            RegType.r21 => "r21",
            RegType.r22 => "r22",
            RegType.r23 => "r23",
            RegType.r24 => "r24",
            RegType.r25 => "r25",
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
            OpCode.MOD => "MOD",
            OpCode.SHL => "SHL",
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

public static class AsmExtensions
{
    extension(AssemblerBase asm)
    {
        // Формат R (два регистра)
        public void EmitMov(RegType dst, RegType src)
            => asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.MOV.ToUint(), dst.ToUint(), src.ToUint()));

        public void EmitMult(RegType dst, RegType src) =>
            asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.MULT_INT.ToUint(), dst.ToUint(), src.ToUint()));
        public void EmitAdd(RegType dst, RegType src)
            => asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.ADD.ToUint(), dst.ToUint(), src.ToUint()));
        public void EmitSub(RegType dst, RegType src)
            => asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.SUB.ToUint(), dst.ToUint(), src.ToUint()));
        public void EmitMultInt(RegType dst, RegType src)
            => asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.MULT_INT.ToUint(), dst.ToUint(), src.ToUint()));
        public void EmitDiv(RegType dst, RegType src)
            => asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.DIV.ToUint(), dst.ToUint(), src.ToUint()));
        public void EmitAnd(RegType dst, RegType src)
            => asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.AND.ToUint(), dst.ToUint(), src.ToUint()));
        public void EmitOr(RegType dst, RegType src)
            => asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.OR.ToUint(), dst.ToUint(), src.ToUint()));
        public void EmitXOr(RegType dst, RegType src)
            => asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.XOR.ToUint(), dst.ToUint(), src.ToUint()));
        public void EmitShl(RegType dst, RegType src)
            => asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.SHL.ToUint(), dst.ToUint(), src.ToUint()));
        public void EmitShr(RegType dst, RegType src)
            => asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.SHR.ToUint(), dst.ToUint(), src.ToUint()));
        public void EmitMod(RegType dst, RegType src)
            => asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.MOD.ToUint(), dst.ToUint(), src.ToUint()));

        public void EmitCmp(RegType dst, RegType src)
            => asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.CMP.ToUint(), dst.ToUint(), src.ToUint()));

        // Формат U (один регистр)
        public void EmitPush(RegType reg)
            => asm.EmitInstruction(InstructionEncoder.EncodeU(OpCode.PUSH.ToUint(), reg.ToUint()));

        public void EmitPop(RegType reg)
            => asm.EmitInstruction(InstructionEncoder.EncodeU(OpCode.POP.ToUint(), reg.ToUint()));
        public void EmitNot(RegType reg)
            => asm.EmitInstruction(InstructionEncoder.EncodeU(OpCode.NOT.ToUint(), reg.ToUint()));
        public void EmitInc(RegType reg)
            => asm.EmitInstruction(InstructionEncoder.EncodeU(OpCode.INC.ToUint(), reg.ToUint()));
        public void EmitDec(RegType reg)
            => asm.EmitInstruction(InstructionEncoder.EncodeU(OpCode.DEC.ToUint(), reg.ToUint()));

        // Специальные форматы
        public void EmitLdi(RegType dst, ulong value)
            => asm.EmitInstruction64(InstructionEncoder.EncodeLDI(dst.ToUint()), value);
        public void EmitLdi(RegType dst, string label)
            => asm.EmitInstruction64WithLabel(InstructionEncoder.EncodeLDI(dst.ToUint()), label);

        public void EmitLoadInd(RegType dst, RegType addr, OpCodeSize size)
            => asm.EmitInstruction(InstructionEncoder.EncodeLOAD_IND(dst.ToUint(), addr.ToUint(), size.ToUint()));

        public void EmitStoreInd(RegType src, RegType addr, OpCodeSize size)
            => asm.EmitInstruction(InstructionEncoder.EncodeSTORE_IND(src.ToUint(), addr.ToUint(), size.ToUint()));

        public void EmitJmp(string label)
            => asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JMP.ToUint()), label);
        public void EmitJz(string label)
            => asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JZ.ToUint()), label);
        public void EmitJnz(string label)
            => asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JNZ.ToUint()), label);
        public void EmitJl(string label)
            => asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JL.ToUint()), label);
        public void EmitJg(string label)
            => asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JG.ToUint()), label);
        public void EmitStore(RegType dst, OpCodeSize size, string label)
            => asm.EmitInstruction64WithLabel(InstructionEncoder.EncodeSTORE(dst.ToUint(), size.ToUint()), label);
        public void EmitStore(RegType dst, OpCodeSize size, ulong adrr)
            => asm.EmitInstruction64(InstructionEncoder.EncodeSTORE(dst.ToUint(), size.ToUint()), adrr);
        public void EmitLoad(RegType dst, OpCodeSize size, string label)
            => asm.EmitInstruction64WithLabel(InstructionEncoder.EncodeLOAD(dst.ToUint(), size.ToUint()), label);
        public void EmitLoad(RegType dst, OpCodeSize size, ulong adrr)
            => asm.EmitInstruction64(InstructionEncoder.EncodeLOAD(dst.ToUint(), size.ToUint()), adrr);
        public void EmitCall(string label)
            => asm.EmitJump(InstructionEncoder.EncodeCALL(), label);
        public void EmitEnd() => asm.EmitInstruction(InstructionEncoder.EncodeEND());
        public void EmitRet() => asm.EmitInstruction(InstructionEncoder.EncodeRET());
        public void EmitNop() => asm.EmitInstruction(InstructionEncoder.EncodeNOP());
        public void EmitHalt() => asm.EmitInstruction(InstructionEncoder.EncodeHALT());
        public void EmitWake() => asm.EmitInstruction(InstructionEncoder.EncodeWAKE());

        public void EmitInst(OpCode op, RegType r1, RegType r2, OpCodeSize size)
            => asm.EmitInstruction(InstructionEncoder.Encode(op.ToUint(), r1.ToUint(), r2.ToUint(), size.ToUint()));
        public void EmitInstU(OpCode op, RegType r1)
            => asm.EmitInstruction(InstructionEncoder.EncodeU(op.ToUint(), r1.ToUint()));
        public void EmitInstR(OpCode op, RegType r1, RegType r2)
            => asm.EmitInstruction(InstructionEncoder.EncodeR(op.ToUint(), r1.ToUint(), r2.ToUint()));
        public void EmitInstI(OpCode op, RegType r1, OpCodeSize size)
            => asm.EmitInstruction(InstructionEncoder.EncodeI(op.ToUint(), r1.ToUint(), size.ToUint()));
        public void EmitInstJ(OpCode op, string label)
            => asm.EmitJump(InstructionEncoder.EncodeJ(op.ToUint()), label);
        public void EmitInstJAbsolute(OpCode op, ulong adrr)
            => asm.EmitJumpToAbsolute(InstructionEncoder.EncodeJ(op.ToUint()), adrr);
    }
}