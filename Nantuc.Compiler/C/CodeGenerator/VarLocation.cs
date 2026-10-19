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
}

/// <summary>
/// Результат раскраски: отображение имени переменной на регистр (или null, если переменная в стеке).
/// </summary>
public class RegisterAllocationResult
{
    public Dictionary<string, RegType?> VarToRegister { get; } = [];
    // Регистры, которые заняты (для быстрой проверки)
    public HashSet<RegType> UsedRegisters { get; } = [];
}
