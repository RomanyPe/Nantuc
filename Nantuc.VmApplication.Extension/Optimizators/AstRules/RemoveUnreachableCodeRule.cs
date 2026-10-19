using Nantuc.Compiler.C.Optimizators;
using Nantuc.Compiler.C;
using Nantuc.VmApplication.Extensions.Optimizators;
using System.Text;
using Nantuc.Kernel.Contracts;

namespace Nantuc.VmApplication.Extensions.Optimizators.AstRules;

public class RemoveUnreachableCodeRule : IAstOptimizationRule
{
    public OptimizationId Type => AstOptimizerExtension.NodeRemoved;

    public IReadOnlyLogOptimization Optimize(ProgramNode program)
    {
        var log = new OptimizationResult(Type);
        RemoveUnreachableCode(program, log);
        return log;
    }

    public static void RemoveUnreachableCode(ProgramNode program, ILogOptimization removedNodesLog)
    {
        foreach (var func in program.FunctionNodes)
        {
            if (func.Body != null)
                OptimizeBlock(func.Body, removedNodesLog);
        }

        foreach (var global in program.GlobalVarNodes)
        {
            if (global.Initializer is BlockNode block)
                OptimizeBlock(block, removedNodesLog);
        }
    }

    private static void OptimizeBlock(BlockNode block, ILogOptimization removedNodesLog)
    {
        var newStatements = new List<ASTNode>();
        bool unreachable = false;

        foreach (var stmt in block.Statements)
        {
            if (unreachable)
            {
                for (int i = block.Statements.IndexOf(stmt); i < block.Statements.Count; i++)
                {
                    removedNodesLog.Append(0, $"Unreachable node removed: {block.Statements[i].GetType().Name}");
                }
                break;
            }

            switch (stmt)
            {
                case BlockNode nested:
                    OptimizeBlock(nested, removedNodesLog);
                    newStatements.Add(nested);
                    break;

                case IfNode ifNode:
                    OptimizeBlock(ifNode.ThenBlock, removedNodesLog);
                    if (ifNode.ElseBlock != null) OptimizeBlock(ifNode.ElseBlock, removedNodesLog);
                    newStatements.Add(ifNode);
                    break;

                case WhileNode whileNode:
                    OptimizeBlock(whileNode.Body, removedNodesLog);
                    newStatements.Add(whileNode);
                    break;

                case ForNode forNode:
                    if (forNode.Body != null) OptimizeBlock(forNode.Body, removedNodesLog);
                    newStatements.Add(forNode);
                    break;

                case ReturnNode:
                    newStatements.Add(stmt);
                    unreachable = true;
                    break;

                default:
                    newStatements.Add(stmt);
                    break;
            }
        }

        block.Statements.Clear();
        block.Statements.AddRange(newStatements);

        SimplifyControlFlow(block, removedNodesLog);
    }

    private static void SimplifyControlFlow(BlockNode block, ILogOptimization removedNodesLog)
    {
        var simplified = new List<ASTNode>();
        foreach (var stmt in block.Statements)
        {
            if (stmt is IfNode ifNode)
            {
                if (IsConstantFalse(ifNode.Condition))
                {
                    if (ifNode.ElseBlock != null)
                    {
                        removedNodesLog.Append(0, $"IfNode removed (then-block discarded, else-block kept): {ifNode.GetType().Name}");
                        simplified.AddRange(ifNode.ElseBlock.Statements);
                    }
                    else
                    {
                        removedNodesLog.Append(0, $"IfNode removed (no else): {ifNode.GetType().Name}");
                    }
                }
                else if (IsConstantTrue(ifNode.Condition))
                {
                    removedNodesLog.Append(0, $"IfNode removed (condition always true, else-block discarded): {ifNode.GetType().Name}");
                    simplified.AddRange(ifNode.ThenBlock.Statements);
                }
                else
                {
                    simplified.Add(ifNode);
                }
            }
            else if (stmt is WhileNode whileNode && IsConstantFalse(whileNode.Condition))
            {
                removedNodesLog.Append(0, $"WhileNode removed (condition false): {whileNode.GetType().Name}");
            }
            else if (stmt is ForNode forNode && forNode.Condition != null && IsConstantFalse(forNode.Condition))
            {
                removedNodesLog.Append(0, $"ForNode removed (condition false): {forNode.GetType().Name}");
            }
            else
            {
                simplified.Add(stmt);
            }
        }
        block.Statements.Clear();
        block.Statements.AddRange(simplified);
    }

    private static bool IsConstantFalse(ASTNode node) =>
        node is NumberNode num && num.Value == 0;

    private static bool IsConstantTrue(ASTNode node) =>
        node is NumberNode num && num.Value != 0;
}