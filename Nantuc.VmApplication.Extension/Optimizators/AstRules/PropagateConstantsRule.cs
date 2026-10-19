using Nantuc.Compiler.C;
using Nantuc.Compiler.C.Optimizators;
using Nantuc.Kernel.Contracts;

namespace Nantuc.VmApplication.Extensions.Optimizators.AstRules;

public class PropagateConstantsRule : IAstOptimizationRule
{
    public OptimizationId Type => AstOptimizerExtension.ConstantPropagated;

    public IReadOnlyLogOptimization Optimize(ProgramNode program)
    {
        var log = new OptimizationResult(Type);
        PropagateConstants(program, log);
        return log;
    }

    private static void PropagateConstants(ProgramNode program, OptimizationResult log)
    {
        foreach (var func in program.FunctionNodes)
        {
            if (func.IsExternal || func.Body == null) continue;

            var constantVars = new Dictionary<string, long>();
            var assignedVars = new HashSet<string>();
            CollectAssignedVariables(func.Body, assignedVars);
            CollectConstantsFromBlock(func.Body, constantVars, assignedVars);

            if (constantVars.Count == 0) continue;

            foreach (var kv in constantVars)
            {
                log.Append(0, $"Propagated constant: {kv.Key} -> {kv.Value}");
            }

            ReplaceVariablesWithConstants(func.Body, constantVars);
        }
    }

    private static void CollectAssignedVariables(BlockNode block, HashSet<string> assigned)
    {
        foreach (var stmt in block.Statements)
        {
            switch (stmt)
            {
                case AssignmentNode assign:
                    assigned.Add(assign.Name);
                    break;
                case IfNode ifNode:
                    CollectAssignedVariables(ifNode.ThenBlock, assigned);
                    if (ifNode.ElseBlock != null)
                        CollectAssignedVariables(ifNode.ElseBlock, assigned);
                    break;
                case WhileNode whileNode:
                    CollectAssignedVariables(whileNode.Body, assigned);
                    break;
                case ForNode forNode:
                    if (forNode.Init is AssignmentNode initAssign)
                        assigned.Add(initAssign.Name);
                    if (forNode.Increment is AssignmentNode incAssign)
                        assigned.Add(incAssign.Name);
                    CollectAssignedVariables(forNode.Body, assigned);
                    break;
            }
        }
    }

    private static void CollectConstantsFromBlock(BlockNode block, Dictionary<string, long> constants, HashSet<string> assignedVars)
    {
        foreach (var stmt in block.Statements)
        {
            switch (stmt)
            {
                case VariableNode varNode when varNode.Initializer is NumberNode num &&
                                                !assignedVars.Contains(varNode.Name):
                    constants[varNode.Name] = num.Value;
                    break;
                case IfNode ifNode:
                    CollectConstantsFromBlock(ifNode.ThenBlock, constants, assignedVars);
                    if (ifNode.ElseBlock != null)
                        CollectConstantsFromBlock(ifNode.ElseBlock, constants, assignedVars);
                    break;
                case WhileNode whileNode:
                    CollectConstantsFromBlock(whileNode.Body, constants, assignedVars);
                    break;
                case ForNode forNode:
                    if (forNode.Body != null)
                        CollectConstantsFromBlock(forNode.Body, constants, assignedVars);
                    break;
            }
        }
    }

    private static void ReplaceVariablesWithConstants(BlockNode block, Dictionary<string, long> constants)
    {
        foreach (var stmt in block.Statements)
        {
            switch (stmt)
            {
                case VariableNode varNode:
                    varNode.Initializer = ReplaceConstantsInExpr(varNode.Initializer, constants);
                    break;
                case AssignmentNode assign:
                    assign.Value = ReplaceConstantsInExpr(assign.Value, constants)!;
                    assign.IndexExpr = ReplaceConstantsInExpr(assign.IndexExpr, constants);
                    break;
                case IfNode ifNode:
                    ifNode.Condition = ReplaceConstantsInExpr(ifNode.Condition, constants)!;
                    ReplaceVariablesWithConstants(ifNode.ThenBlock, constants);
                    if (ifNode.ElseBlock != null)
                        ReplaceVariablesWithConstants(ifNode.ElseBlock, constants);
                    break;
                case WhileNode whileNode:
                    whileNode.Condition = ReplaceConstantsInExpr(whileNode.Condition, constants)!;
                    ReplaceVariablesWithConstants(whileNode.Body, constants);
                    break;
                case ForNode forNode:
                    forNode.Init = ReplaceConstantsInExpr(forNode.Init, constants);
                    forNode.Condition = ReplaceConstantsInExpr(forNode.Condition, constants);
                    forNode.Increment = ReplaceConstantsInExpr(forNode.Increment, constants);
                    ReplaceVariablesWithConstants(forNode.Body, constants);
                    break;
                case ReturnNode ret:
                    ret.Value = ReplaceConstantsInExpr(ret.Value, constants);
                    break;
                case FunctionCallNode call:
                    for (int i = 0; i < call.Arguments.Count; i++)
                        call.Arguments[i] = ReplaceConstantsInExpr(call.Arguments[i], constants)!;
                    break;
            }
        }
    }

    private static ASTNode? ReplaceConstantsInExpr(ASTNode? node, Dictionary<string, long> constants)
    {
        if (node == null) return null;

        switch (node)
        {
            case IdentifierNode id when constants.TryGetValue(id.Name, out var val):
                return new NumberNode(val);
            case BinaryOpNode binop:
                binop.Left = ReplaceConstantsInExpr(binop.Left, constants)!;
                binop.Right = ReplaceConstantsInExpr(binop.Right, constants)!;
                return binop;
            case UnaryOpNode unop:
                unop.Operand = ReplaceConstantsInExpr(unop.Operand, constants)!;
                return unop;
            case ArrayAccessNode arr:
                arr.Index = ReplaceConstantsInExpr(arr.Index, constants)!;
                return arr;
            case MemberAccessNode member:
                member.Object = ReplaceConstantsInExpr(member.Object, constants)!;
                return member;
            case AddressOfNode addr:
                addr.Operand = ReplaceConstantsInExpr(addr.Operand, constants)!;
                return addr;
            case DereferenceNode deref:
                deref.Operand = ReplaceConstantsInExpr(deref.Operand, constants)!;
                return deref;
            case FunctionCallNode call:
                for (int i = 0; i < call.Arguments.Count; i++)
                    call.Arguments[i] = ReplaceConstantsInExpr(call.Arguments[i], constants)!;
                return call;
            default:
                return node;
        }
    }
}
