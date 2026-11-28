using Nantuc.Common;
using Nantuc.Compiler.ASM.Base;

namespace Nantuc.Compiler.ASM.Utils;

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
        public void EmitJb(string label)
            => asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JB.ToUint()), label);
        public void EmitJa(string label)
            => asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JA.ToUint()), label);
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

        public void EmitInstWithData(OpCode op, RegType reg, OpCodeSize s, ulong data)
            => asm.EmitInstruction64(InstructionEncoder.EncodeI(op.ToUint(), reg.ToUint(), s.ToUint()), data);
    }
}