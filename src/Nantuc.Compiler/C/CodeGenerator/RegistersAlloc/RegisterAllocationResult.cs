using Nantuc.Common;
using Nantuc.Compiler.C.AST;

namespace Nantuc.Compiler.C.CodeGenerator.RegistersAlloc;

public class RegisterAllocationResult
{
    public Dictionary<string, RegType?> VarToRegister { get; } = [];
    public HashSet<RegType> UsedRegisters { get; } = [];
    public HashSet<ASTNode> ElideCopies { get; } = [];
    public HashSet<IdentifierNode> DeadAfterUse { get; } = [];
    public int SpillCount { get; internal set; }
    public int CoalescedCount { get; internal set; }
}