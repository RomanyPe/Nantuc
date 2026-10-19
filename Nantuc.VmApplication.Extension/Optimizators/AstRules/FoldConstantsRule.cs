using Nantuc.Compiler.C.CodeGenerator;
using Nantuc.Compiler.C.Optimizators;
using Nantuc.Compiler.C;
using Nantuc.VmApplication.Extensions.Optimizators;
using System.Text;
using Nantuc.Kernel.Contracts;

namespace Nantuc.VmApplication.Extensions.Optimizators.AstRules;

public class FoldConstantsRule : IAstOptimizationRule
{
    public OptimizationId Type => AstOptimizerExtension.ConstantFolded;

    public IReadOnlyLogOptimization Optimize(ProgramNode program)
    {
        var log = new OptimizationResult(Type);
        FoldConstants(program, log);
        return log;
    }

    private static void FoldConstants(ProgramNode program, ILogOptimization log)
    {
        foreach (var func in program.FunctionNodes)
        {
            if (func.IsExternal || func.Body == null) continue;
            FoldConstantsInBlock(func.Body, log);
        }
    }

    private static void FoldConstantsInBlock(BlockNode block, ILogOptimization log)
    {
        foreach (var stmt in block.Statements)
        {
            switch (stmt)
            {
                case VariableNode varNode:
                    varNode.Initializer = FoldConstantsInExpr(varNode.Initializer, log);
                    break;
                case AssignmentNode assign:
                    assign.Value = FoldConstantsInExpr(assign.Value, log)!;
                    assign.IndexExpr = FoldConstantsInExpr(assign.IndexExpr, log);
                    if (assign.LValue != null) assign.LValue = FoldConstantsInExpr(assign.LValue, log);
                    break;
                case IfNode ifNode:
                    ifNode.Condition = FoldConstantsInExpr(ifNode.Condition, log)!;
                    FoldConstantsInBlock(ifNode.ThenBlock, log);
                    if (ifNode.ElseBlock != null) FoldConstantsInBlock(ifNode.ElseBlock, log);
                    break;
                case WhileNode whileNode:
                    whileNode.Condition = FoldConstantsInExpr(whileNode.Condition, log)!;
                    FoldConstantsInBlock(whileNode.Body, log);
                    break;
                case ForNode forNode:
                    forNode.Init = FoldConstantsInExpr(forNode.Init, log);
                    forNode.Condition = FoldConstantsInExpr(forNode.Condition, log);
                    forNode.Increment = FoldConstantsInExpr(forNode.Increment, log);
                    FoldConstantsInBlock(forNode.Body, log);
                    break;
                case ReturnNode ret:
                    ret.Value = FoldConstantsInExpr(ret.Value, log);
                    break;
                case FunctionCallNode call:
                    for (int i = 0; i < call.Arguments.Count; i++)
                        call.Arguments[i] = FoldConstantsInExpr(call.Arguments[i], log)!;
                    break;
            }
        }
    }

    private static ASTNode? FoldConstantsInExpr(ASTNode? node, ILogOptimization log)
    {
        if (node == null) return null;

        switch (node)
        {
            case NumberNode:
                return node;

            case BinaryOpNode binop:
                binop.Left = FoldConstantsInExpr(binop.Left, log)!;
                binop.Right = FoldConstantsInExpr(binop.Right, log)!;

                if (binop.Left is NumberNode left && binop.Right is NumberNode right)
                {
                    long oldLeft = left.Value;
                    long oldRight = right.Value;
                    long result = ExpressionGenerator.ComputeConstant(left.Value, right.Value, binop.Operator);
                    log.Append(0, $"Folded constant: {oldLeft} {binop.Operator} {oldRight} = {result}");
                    return new NumberNode(result);
                }
                return binop;

            case UnaryOpNode unop:
                unop.Operand = FoldConstantsInExpr(unop.Operand, log)!;
                if (unop.Operand is NumberNode num)
                {
                    long oldValue = num.Value;
                    long result = unop.Operator switch
                    {
                        "-" => -num.Value,
                        "!" => num.Value == 0 ? 1 : 0,
                        "~" => ~num.Value,
                        _ => num.Value
                    };
                    log.Append(0, $"Folded unary: {unop.Operator} {oldValue} = {result}");
                    return new NumberNode(result);
                }
                return unop;

            case FunctionCallNode call:
                for (int i = 0; i < call.Arguments.Count; i++)
                    call.Arguments[i] = FoldConstantsInExpr(call.Arguments[i], log)!;
                return call;

            case ArrayAccessNode arr:
                arr.Index = FoldConstantsInExpr(arr.Index, log)!;
                return arr;

            case MemberAccessNode member:
                member.Object = FoldConstantsInExpr(member.Object, log)!;
                return member;

            case AddressOfNode addr:
                addr.Operand = FoldConstantsInExpr(addr.Operand, log)!;
                return addr;

            case DereferenceNode deref:
                deref.Operand = FoldConstantsInExpr(deref.Operand, log)!;
                return deref;

            default:
                return node;
        }
    }
}
