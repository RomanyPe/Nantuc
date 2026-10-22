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

    public static OpCodeSize GetSizeForType(string? type) => type switch
    {
        "byte" => OpCodeSize.S8,
        "ushort" or "char" => OpCodeSize.S16,
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

    public static void EmitMultiplyByConstant(AssemblerBase asm, uint reg, int multiplier)
    {
        for (int i = 1; i < multiplier; i *= 2)
        {
            asm.EmitInstruction(InstructionEncoder.EncodeR((uint)OpCode.ADD, reg, reg));
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
        public void Mov(RegType dst, RegType src)
            => asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.MOV.ToUint(), dst.ToUint(), src.ToUint()));

        public void Add(RegType dst, RegType src)
            => asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.ADD.ToUint(), dst.ToUint(), src.ToUint()));

        public void Sub(RegType dst, RegType src)
            => asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.SUB.ToUint(), dst.ToUint(), src.ToUint()));

        public void Cmp(RegType dst, RegType src)
            => asm.EmitInstruction(InstructionEncoder.EncodeR(OpCode.CMP.ToUint(), dst.ToUint(), src.ToUint()));

        // Формат U (один регистр)
        public void Push(RegType reg)
            => asm.EmitInstruction(InstructionEncoder.EncodeU(OpCode.PUSH.ToUint(), reg.ToUint()));

        public void Pop(RegType reg)
            => asm.EmitInstruction(InstructionEncoder.EncodeU(OpCode.POP.ToUint(), reg.ToUint()));

        // Специальные форматы
        public void Ldi(RegType dst, ulong value)
            => asm.EmitInstruction64(InstructionEncoder.EncodeLDI(dst.ToUint()), value);

        public void LoadInd(RegType dst, RegType addr, OpCodeSize size)
            => asm.EmitInstruction(InstructionEncoder.EncodeLOAD_IND(dst.ToUint(), addr.ToUint(), size.ToUint()));

        public void StoreInd(RegType src, RegType addr, OpCodeSize size)
            => asm.EmitInstruction(InstructionEncoder.EncodeSTORE_IND(src.ToUint(), addr.ToUint(), size.ToUint()));

        public void Jmp(string label)
            => asm.EmitJump(InstructionEncoder.EncodeJ(OpCode.JMP.ToUint()), label);
    }
}