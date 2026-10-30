using Nantuc.Kernel.Common;

namespace Nantuc.Compiler.C.CodeGenerator;

// ============================================================
// 4. ГЕНЕРАТОР КОДА
// ============================================================

public class VarLocation
{
    public bool IsRegister;
    public RegType Register;
    public int StackOffset;
    public OpCodeSize TypeSize;
    public bool IsArray;
    public int ArraySize;
    public bool IsPointer;
    public string? PointedType;
    public string? StructTypeName;
    public bool IsConst;
    public bool IsPointedConst;
}
