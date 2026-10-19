using Nantuc.Compiler.C.Optimizators;
using Nantuc.Compiler.C;
using Nantuc.VmApplication.Extensions.Optimizators;
using System.Text;
using Nantuc.Kernel.Contracts;

namespace Nantuc.VmApplication.Extensions.Optimizators.AstRules;

public class InlineSmallVoidFunctionsRule : IAstOptimizationRule
{
    private const int MaxWeightInlineSize = 10;

    public OptimizationId Type => AstOptimizerExtension.FunctionInlined;

    public IReadOnlyLogOptimization Optimize(ProgramNode program)
    {
        var log = new OptimizationResult(Type);
        InlineSmallVoidFunctions(program, MaxWeightInlineSize, log);
        return log;
    }

    private static void InlineSmallVoidFunctions(ProgramNode program, int maxBodySize, ILogOptimization inlinedFunc)
    {
        var candidates = program.FunctionNodes
            .Where(f => f.ReturnType == "void" &&
                        !f.IsExternal &&
                        f.Name != "main" &&
                        f.Body != null &&
                        CountStatements(f.Body) <= maxBodySize)
            .ToList();

        if (candidates.Count == 0) return;

        candidates = [.. candidates.Where(f => !ContainsCallTo(f.Body, f.Name))];

        var inlined = new HashSet<string>();

        foreach (var func in program.FunctionNodes)
        {
            if (func.Body != null && func.Name != "main")
            {
                InlineCallsInBlock(func.Body, candidates.ToDictionary(f => f.Name), inlined, inlinedFunc);
            }
        }
    }

    private static void InlineCallsInBlock(BlockNode block,
                                            Dictionary<string, FunctionNode> candidateMap,
                                            HashSet<string> inlined,
                                            ILogOptimization inlinedFunc)
    {
        var newStatements = new List<ASTNode>();

        foreach (var stmt in block.Statements)
        {
            switch (stmt)
            {
                case FunctionCallNode call when candidateMap.TryGetValue(call.Name, out var target) &&
                                                target.ReturnType == "void" &&
                                                target.Parameters.Count == call.Arguments.Count:
                    var inlineBlock = CreateInlineBlock(call, target);
                    newStatements.AddRange(inlineBlock.Statements);
                    inlined.Add(target.Name);
                    inlinedFunc.Append(0, $"Inlined call to '{call.Name}' (replaced with inline block)");
                    break;

                case IfNode ifNode:
                    InlineCallsInBlock(ifNode.ThenBlock, candidateMap, inlined, inlinedFunc);
                    if (ifNode.ElseBlock != null) InlineCallsInBlock(ifNode.ElseBlock, candidateMap, inlined, inlinedFunc);
                    newStatements.Add(ifNode);
                    break;

                case WhileNode whileNode:
                    InlineCallsInBlock(whileNode.Body, candidateMap, inlined, inlinedFunc);
                    newStatements.Add(whileNode);
                    break;

                case ForNode forNode:
                    if (forNode.Init is BlockNode initBlock) InlineCallsInBlock(initBlock, candidateMap, inlined, inlinedFunc);
                    if (forNode.Increment is BlockNode incBlock) InlineCallsInBlock(incBlock, candidateMap, inlined, inlinedFunc);
                    InlineCallsInBlock(forNode.Body, candidateMap, inlined, inlinedFunc);
                    newStatements.Add(forNode);
                    break;

                default:
                    newStatements.Add(stmt);
                    break;
            }
        }

        block.Statements.Clear();
        block.Statements.AddRange(newStatements);
    }

    private static BlockNode CreateInlineBlock(FunctionCallNode call, FunctionNode targetFunc)
    {
        var inlineBlock = new BlockNode();
        var renameMap = new Dictionary<string, string>();
        int uniqueId = Guid.NewGuid().GetHashCode() & 0xFFFF;

        for (int i = 0; i < targetFunc.Parameters.Count; i++)
        {
            var param = targetFunc.Parameters[i];
            var arg = call.Arguments[i];
            string newParamName = $"{param.Name}_inl_{uniqueId}";
            renameMap[param.Name] = newParamName;

            var paramVar = new VariableNode(param.Type, newParamName, null)
            {
                IsPointer = param.IsPointer,
                PointedType = param.PointedType
            };
            inlineBlock.Statements.Add(paramVar);

            var assign = new AssignmentNode(newParamName, arg) { LValue = new IdentifierNode(newParamName) };
            inlineBlock.Statements.Add(assign);
        }

        foreach (var stmt in targetFunc.Body.Statements)
        {
            var cloned = CloneStatementWithRename(stmt, renameMap);
            inlineBlock.Statements.Add(cloned);
        }

        return inlineBlock;
    }

    private static ASTNode CloneStatementWithRename(ASTNode node, Dictionary<string, string> renameMap)
    {
        switch (node)
        {
            case BlockNode block:
                var newBlock = new BlockNode();
                foreach (var s in block.Statements)
                    newBlock.Statements.Add(CloneStatementWithRename(s, renameMap));
                return newBlock;

            case VariableNode varNode:
                string newName = varNode.Name;
                if (renameMap.TryGetValue(varNode.Name, out var mapped)) newName = mapped;
                return new VariableNode(varNode.Type, newName, CloneExpression(varNode.Initializer, renameMap))
                {
                    IsArray = varNode.IsArray,
                    ArraySize = varNode.ArraySize,
                    IsPointer = varNode.IsPointer,
                    PointedType = varNode.PointedType
                };

            case AssignmentNode assign:
                string assignName = assign.Name;
                if (renameMap.TryGetValue(assign.Name, out var mappedAssign)) assignName = mappedAssign;
                return new AssignmentNode(assignName, CloneExpression(assign.Value, renameMap)!, CloneExpression(assign.IndexExpr, renameMap))
                {
                    LValue = CloneExpression(assign.LValue, renameMap)
                };

            case ReturnNode ret:
                return new ReturnNode(CloneExpression(ret.Value, renameMap));

            case IfNode ifNode:
                var newIf = new IfNode(CloneExpression(ifNode.Condition, renameMap)!,
                                       (BlockNode)CloneStatementWithRename(ifNode.ThenBlock, renameMap),
                                       ifNode.ElseBlock != null ? (BlockNode)CloneStatementWithRename(ifNode.ElseBlock, renameMap) : null);
                return newIf;

            case WhileNode whileNode:
                return new WhileNode(CloneExpression(whileNode.Condition, renameMap)!,
                                     (BlockNode)CloneStatementWithRename(whileNode.Body, renameMap));

            case ForNode forNode:
                var newFor = new ForNode(CloneExpression(forNode.Init, renameMap),
                                         CloneExpression(forNode.Condition, renameMap),
                                         CloneExpression(forNode.Increment, renameMap),
                                         (BlockNode)CloneStatementWithRename(forNode.Body, renameMap));
                return newFor;

            case FunctionCallNode call:
                var newCall = new FunctionCallNode(call.Name);
                foreach (var arg in call.Arguments)
                    newCall.Arguments.Add(CloneExpression(arg, renameMap)!);
                return newCall;

            case BinaryOpNode bin:
                return new BinaryOpNode(bin.Operator,
                                        CloneExpression(bin.Left, renameMap)!,
                                        CloneExpression(bin.Right, renameMap)!);

            case UnaryOpNode un:
                return new UnaryOpNode(un.Operator, CloneExpression(un.Operand, renameMap)!);

            case IdentifierNode id:
                if (renameMap.TryGetValue(id.Name, out var idMapped))
                    return new IdentifierNode(idMapped);
                return new IdentifierNode(id.Name);

            case ArrayAccessNode arr:
                return new ArrayAccessNode(
                    renameMap.TryGetValue(arr.ArrayName, out var arrMapped) ? arrMapped : arr.ArrayName,
                    CloneExpression(arr.Index, renameMap)!);

            case MemberAccessNode member:
                return new MemberAccessNode(CloneExpression(member.Object, renameMap)!,
                                            member.FieldName,
                                            member.IsArrow);

            case AddressOfNode addrOf:
                return new AddressOfNode(CloneExpression(addrOf.Operand, renameMap)!);

            case DereferenceNode deref:
                return new DereferenceNode(CloneExpression(deref.Operand, renameMap)!);

            default:
                return node;
        }
    }

    private static ASTNode? CloneExpression(ASTNode? node, Dictionary<string, string> renameMap)
    {
        if (node == null) return null;
        return CloneStatementWithRename(node, renameMap);
    }

    private static int CountStatements(BlockNode block)
    {
        int count = 0;
        foreach (var stmt in block.Statements)
        {
            count++;
            switch (stmt)
            {
                case IfNode ifNode:
                    count += CountStatements(ifNode.ThenBlock) + (ifNode.ElseBlock != null ? CountStatements(ifNode.ElseBlock) : 0);
                    break;
                case WhileNode whileNode:
                    count += CountStatements(whileNode.Body);
                    break;
                case ForNode forNode:
                    count += CountStatements(forNode.Body);
                    break;
            }
        }
        return count;
    }

    private static bool ContainsCallTo(BlockNode block, string funcName)
    {
        foreach (var stmt in block.Statements)
        {
            if (ContainsCallToStatement(stmt, funcName))
                return true;
        }
        return false;
    }

    private static bool ContainsCallToStatement(ASTNode node, string funcName) => node switch
    {
        FunctionCallNode call => FunkCallContains(funcName, call),
        BinaryOpNode bin => ContainsCallToStatement(bin.Left, funcName) || ContainsCallToStatement(bin.Right, funcName),
        UnaryOpNode un => ContainsCallToStatement(un.Operand, funcName),
        IfNode ifNode => ContainsCallTo(ifNode.ThenBlock, funcName) ||
                               (ifNode.ElseBlock != null && ContainsCallTo(ifNode.ElseBlock, funcName)),
        WhileNode whileNode => ContainsCallTo(whileNode.Body, funcName),
        ForNode forNode => ContainsCallTo(forNode.Body, funcName),
        BlockNode block => ContainsCallTo(block, funcName),
        _ => false,
    };

    private static bool FunkCallContains(string funcName, FunctionCallNode call)
    {
        if (call.Name == funcName) return true;
        foreach (var arg in call.Arguments)
            if (ContainsCallToStatement(arg, funcName)) return true;
        return false;
    }
}
