using Nantuc.Compiler.ASM;
using Nantuc.Compiler.ASM.Optimizators;
using Nantuc.Kernel.Common;
using Nantuc.Kernel.Diagnostics;

namespace Nantuc.Compiler.C.CodeGenerator;

// ============================================================
// CodeGenUtils – статические утилиты
// ============================================================

public static class CodeGenUtils
{
    public const uint TMP_REG = (uint)RegType.r3;
    public const RegType TMP_REG_EN = RegType.r3;

    public static OpCodeSize GetSizeForType(string? type) => type switch
    {
        "byte" => OpCodeSize.S8,
        "ushort" or "char"=> OpCodeSize.S16,
        "ulong" => OpCodeSize.S64,
        "int" => OpCodeSize.S32,
        _ => ThrowHelper.ThrowMiniC<OpCodeSize>(ErrorCode.CodeGen_UnknownTypeSize, type!)
    };

    public static int GetSizeInBytes(OpCodeSize size) => size switch
    {
        OpCodeSize.S8 => 1,
        OpCodeSize.S16 => 2,
        OpCodeSize.S32 => 4,
        OpCodeSize.S64 => 8,
        _ => ThrowHelper.ThrowMiniC<int>(ErrorCode.CodeGen_UnknownOpCodeSize, (int)size)
    };

    public static void EmitMultiplyByConstant(this AssemblerBase asm, RegType reg, int multiplier)
    {
        for (int i = 1; i < multiplier; i *= 2)
        {
            asm.EmitAdd(reg, reg);
        }
    }

    public static bool IsComparisonOperator(string op) => op is "==" or "!=" or "<" or ">" or "<=" or ">=";

    public static int GetAlignment(string type) => type switch
    {
        "byte" => 1,
        "ushort" or "char" => 2,
        "int" => 4,
        "ulong" => 8,
        _ => 8  // структуры/указатели выравниваются на 8
    };

    public static bool IsStructType(string type, Dictionary<string, StructLayout> structTable)
        => structTable.ContainsKey(type);

    public static StructLayout GetStructLayout(string type, Dictionary<string, StructLayout> structTable)
        => structTable.TryGetValue(type, out var layout) 
        ? layout 
        : ThrowHelper.ThrowMiniC<StructLayout>(ErrorCode.CodeGen_UnknownStructType, type);

    public static bool IsPrimitiveType(string type) => type switch
    {
        "int" or "char" or "void" or "byte" or "ushort" or "ulong" => true,
        _ => false
    };

    public static int GetTypeSize(string type, Dictionary<string, StructLayout> structTable)
    {
        if (structTable.TryGetValue(type, out var layout))
            return layout.Size;
        return GetSizeInBytes(GetSizeForType(type));
    }

    public static int GetAlignment(string type, Dictionary<string, StructLayout> structTable)
    {
        if (structTable.ContainsKey(type)) // структура
            return 8; // наибольшее выравнивание, можно брать максимальное из полей, но 8 ок
        return type switch
        {
            "byte" or "char" => 1,
            "ushort" => 2,
            "int" => 4,
            "ulong" => 8,
            _ => 8
        };
    }
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