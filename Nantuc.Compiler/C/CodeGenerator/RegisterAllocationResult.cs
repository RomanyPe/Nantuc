using Nantuc.Kernel.Common;

namespace Nantuc.Compiler.C.CodeGenerator;

/// <summary>
/// Результат раскраски: отображение имени переменной на регистр (или null, если переменная в стеке).
/// </summary>
public class RegisterAllocationResult
{
    public Dictionary<string, RegType?> VarToRegister { get; } = [];
    // Регистры, которые заняты (для быстрой проверки)
    public HashSet<RegType> UsedRegisters { get; } = [];
}
