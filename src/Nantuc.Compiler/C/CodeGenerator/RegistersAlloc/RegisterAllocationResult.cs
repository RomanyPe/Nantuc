using Nantuc.Common;
using Nantuc.Compiler.C.AST;

namespace Nantuc.Compiler.C.CodeGenerator.RegistersAlloc;

public class RegisterAllocationResult
{
    public Dictionary<string, RegType?> VarToRegister { get; } = [];
    public HashSet<RegType> UsedRegisters { get; } = [];

    // AssignmentNode «x = y», где обе переменные в одном регистре и одного типа:
    // mov не нужен, cast — no-op. Кодогенератор может пропустить всю запись.
    public HashSet<ASTNode> ElideCopies { get; } = [];

    // Чтение, после которого переменная мертва → её home-регистр можно
    // использовать как рабочий прямо в выражении (фаза 2).
    public HashSet<IdentifierNode> DeadAfterUse { get; } = [];

    public int SpillCount { get; internal set; }
    public int CoalescedCount { get; internal set; }
}