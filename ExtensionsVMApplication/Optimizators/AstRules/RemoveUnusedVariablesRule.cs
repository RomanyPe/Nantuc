using Compiller.C;
using Compiller.C.Optimizators;
using Kernel.Contracts;
using System.Text;

namespace ExtensionsVMApplication.Optimizators.AstRules;

public class RemoveUnusedVariablesRule : IAstOptimizationRule
{
    public OptimizationId Type => AstOptimizerExtension.VariableRemoved;

    public IReadOnlyLogOptimization Optimize(ProgramNode program)
    {
        var log = new OptimizationResult(Type);
        RemoveUnusedVariables(program, log);
        return log;
    }

    private static void RemoveUnusedVariables(ProgramNode program, ILogOptimization log)
    {
        foreach (var func in program.FunctionNodes)
        {
            if (func.IsExternal || func.Body == null) continue;

            var usedVars = new HashSet<string>();
            CollectUsedVariables(func.Body, usedVars);
            RemoveUnusedFromBlock(func.Body, usedVars, log);
        }
    }

    private static void CollectUsedVariables(BlockNode block, HashSet<string> used)
    {
        foreach (var stmt in block.Statements)
        {
            switch (stmt)
            {
                case VariableNode varNode:
                    CollectUsedInExpression(varNode.Initializer, used);
                    break;
                case AssignmentNode assign:
                    used.Add(assign.Name);
                    CollectUsedInExpression(assign.IndexExpr, used);
                    CollectUsedInExpression(assign.Value, used);
                    if (assign.LValue != null) CollectUsedInExpression(assign.LValue, used);
                    break;
                case BinaryOpNode binop:
                    CollectUsedInExpression(binop, used);
                    break;
                case UnaryOpNode unop:
                    CollectUsedInExpression(unop, used);
                    break;
                case IfNode ifNode:
                    CollectUsedInExpression(ifNode.Condition, used);
                    CollectUsedVariables(ifNode.ThenBlock, used);
                    if (ifNode.ElseBlock != null) CollectUsedVariables(ifNode.ElseBlock, used);
                    break;
                case WhileNode whileNode:
                    CollectUsedInExpression(whileNode.Condition, used);
                    CollectUsedVariables(whileNode.Body, used);
                    break;
                case ForNode forNode:
                    CollectUsedInExpression(forNode.Init, used);
                    CollectUsedInExpression(forNode.Condition, used);
                    CollectUsedInExpression(forNode.Increment, used);
                    CollectUsedVariables(forNode.Body, used);
                    break;
                case ReturnNode ret:
                    CollectUsedInExpression(ret.Value, used);
                    break;
                case FunctionCallNode call:
                    foreach (var arg in call.Arguments)
                        CollectUsedInExpression(arg, used);
                    break;
            }
        }
    }

    private static void CollectUsedInExpression(ASTNode? node, HashSet<string> used)
    {
        if (node == null) return;
        switch (node)
        {
            case IdentifierNode id: used.Add(id.Name); break;
            case BinaryOpNode bin:
                CollectUsedInExpression(bin.Left, used);
                CollectUsedInExpression(bin.Right, used);
                break;
            case UnaryOpNode un:
                CollectUsedInExpression(un.Operand, used);
                break;
            case ArrayAccessNode arr:
                used.Add(arr.ArrayName);
                CollectUsedInExpression(arr.Index, used);
                break;
            case MemberAccessNode member:
                CollectUsedInExpression(member.Object, used);
                break;
            case AddressOfNode addr:
                CollectUsedInExpression(addr.Operand, used);
                break;
            case DereferenceNode deref:
                CollectUsedInExpression(deref.Operand, used);
                break;
            case FunctionCallNode call:
                foreach (var arg in call.Arguments) CollectUsedInExpression(arg, used);
                break;
        }
    }

    private static void RemoveUnusedFromBlock(BlockNode block, HashSet<string> used, ILogOptimization log)
    {
        var newStatements = new List<ASTNode>();
        foreach (var stmt in block.Statements)
        {
            if (stmt is VariableNode varNode && !used.Contains(varNode.Name) && IsSideEffectFree(varNode.Initializer))
            {
                log.Append(0, $"Removed unused variable: {varNode.Name}");
                continue;
            }

            switch (stmt)
            {
                case IfNode ifNode:
                    RemoveUnusedFromBlock(ifNode.ThenBlock, used, log);
                    if (ifNode.ElseBlock != null) RemoveUnusedFromBlock(ifNode.ElseBlock, used, log);
                    break;
                case WhileNode whileNode:
                    RemoveUnusedFromBlock(whileNode.Body, used, log);
                    break;
                case ForNode forNode:
                    if (forNode.Body != null) RemoveUnusedFromBlock(forNode.Body, used, log);
                    break;
            }
            newStatements.Add(stmt);
        }
        block.Statements.Clear();
        block.Statements.AddRange(newStatements);
    }

    private static bool IsSideEffectFree(ASTNode? node)
    {
        if (node == null) return true;
        return node is NumberNode or IdentifierNode or BinaryOpNode or UnaryOpNode;
    }
}
