using Nantuc.Common;
using Nantuc.Compiler.ASM;
using Nantuc.Compiler.ASM.Base;
using Nantuc.Compiler.ASM.IR;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Diagnostics;

namespace Nantuc.Compiler.C.CodeGenerator;

// ============================================================
// CodeGenUtils – статические утилиты
// ============================================================

public static class CodeGenUtils
{
    public const RegType ADDR_SCRATCH_REG_EN = RegType.r27;

    public static bool IsSignedType(string type) => type switch
    {
        "char" or "short" or "int" or "long" => true,
        _ => false   // byte, ushort, uint, ulong, указатели, структуры
    };
    extension(AssemblerBase asm)
    {
        public void EmitCastToType(RegType reg, OpCodeSize size, bool isSigned)
        {
            if (size == OpCodeSize.S64) return;
            if (isSigned)
                asm.EmitInstI(OpCode.CASTS, reg, size);
            else
                asm.EmitInstI(OpCode.CASTZ, reg, size);
        }

        public void EmitMultiplyByConstant(RegType reg, int multiplier)
        {
            if (multiplier <= 0 || multiplier == 1) return;
            if (multiplier == 2) { asm.EmitAdd(reg, reg); return; }

            bool isPow2 = (multiplier & (multiplier - 1)) == 0;
            if (isPow2)
            {
                for (int i = 1; i < multiplier; i *= 2)
                    asm.EmitAdd(reg, reg);
                return;
            }

            // Общий случай: scratch * multiplier через MULT_INT
            asm.EmitLdi(ADDR_SCRATCH_REG_EN, (ulong)multiplier);
            asm.EmitMultInt(reg, ADDR_SCRATCH_REG_EN);
        }
    }

    public static OpCodeSize GetSizeForType(string? type, ErrorReporter ErrorReporter) => type switch
    {
        "byte" => OpCodeSize.S8,
        "ushort" or "char" or "short" => OpCodeSize.S16,
        "ulong" or "long" => OpCodeSize.S64,
        "int" or "uint" => OpCodeSize.S32,
        _ => ErrorReporter.Throw<OpCodeSize>(ErrorCode.CodeGen_UnknownTypeSize, type!)
    };

    public static int GetSizeInBytes(OpCodeSize size, ErrorReporter ErrorReporter) => size switch
    {
        OpCodeSize.S8 => 1,
        OpCodeSize.S16 => 2,
        OpCodeSize.S32 => 4,
        OpCodeSize.S64 => 8,
        _ => ErrorReporter.Throw<int>(ErrorCode.CodeGen_UnknownOpCodeSize, (int)size)
    };

    public static bool IsComparisonOperator(string op) => op is "==" or "!=" or "<" or ">" or "<=" or ">=";

    public static int GetAlignment(string type) => type switch
    {
        "byte" => 1,
        "ushort" or "char" or "short" => 2,
        "int" or "uint" => 4,
        "ulong" or "long" => 8,
        _ => 8  // структуры/указатели выравниваются на 8
    };

    public static bool IsStructType(string type, Dictionary<string, StructLayout> structTable)
        => structTable.ContainsKey(type);

    public static bool IsPrimitiveType(string type) => type switch
    {
        "int" or "char" or "void" or "byte" or "ushort" or "ulong" or "uint" or "long" or "short"=> true,
        _ => false
    };

    public static int GetTypeSize(string type, Dictionary<string, StructLayout> structTable, ErrorReporter ErrorReporter)
    {
        if (structTable.TryGetValue(type, out var layout))
            return layout.Size;
        return GetSizeInBytes(GetSizeForType(type, ErrorReporter), ErrorReporter);
    }

    public static int GetAlignment(string type, Dictionary<string, StructLayout> structTable)
    {
        if (structTable.ContainsKey(type)) // структура
            return 8; // наибольшее выравнивание, можно брать максимальное из полей, но 8 ок
        return type switch
        {
            "byte" => 1,
            "ushort" or "char" or "short" => 2,
            "int" or "uint" => 4,
            "ulong" or "long" => 8,
            _ => 8
        };
    }
}