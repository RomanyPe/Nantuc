using Nantuc.Compiler.C.AST;

namespace Nantuc.Compiler.C.CodeGenerator.FunctionCtx;

// ============================================================
// Оценка максимальной глубины ExpressionSaveDepth по AST.
// Чистая функция: состояние не хранит, зависимостей нет.
// Легко тестируется отдельно (см. SaveDepthEstimatorTests).
//
// Каждый SaveSlot в switch соответствует одному одновременно
// занятому save-слоту (см. ExpressionGenerator.SaveRegToStack).
// ============================================================
public static class SaveDepthEstimator
{
    private const int SaveSlot = 1;

    public static int Estimate(ASTNode? node) => node switch
    {
        null => 0,

        // ----- Выражения -----
        BinaryOpNode b => SaveSlot + Max(Depth(b.Left), Depth(b.Right)),
        UnaryOpNode u => Depth(u.Operand),
        AssignmentNode a => AssignmentDepth(a),
        FunctionCallNode fc => FunctionCallDepth(fc),
        IndirectCallNode ic => IndirectCallDepth(ic),
        ArrayAccessNode aa => Depth(aa.Index),
        MemberAccessNode ma => Depth(ma.Object),
        DereferenceNode d => Depth(d.Operand),
        AddressOfNode ao => Depth(ao.Operand),
        IncrementNode inc => SaveSlot + Depth(inc.Target),

        // ----- Инструкции -----
        IfNode i => Max(SaveSlot + Depth(i.Condition), Depth(i.ThenBlock), Depth(i.ElseBlock)),
        WhileNode w => Max(SaveSlot + Depth(w.Condition), Depth(w.Body)),
        ForNode f => Max(Depth(f.Init), SaveSlot + Depth(f.Condition), Depth(f.Increment), Depth(f.Body)),
        DoWhileNode d => Max(Depth(d.Body), SaveSlot + Depth(d.Condition)),
        ReturnNode r => Depth(r.Value),
        VariableNode v => Max(Depth(v.Initializer), MaxDepth(v.ArrayInitializer)),
        BlockNode blk => MaxDepth(blk.Statements),

        _ => 0,
    };

    private static int Depth(ASTNode? n) => Estimate(n);
    private static int MaxDepth(IReadOnlyList<ASTNode>? nodes)
        => nodes is null or { Count: 0 } ? 0 : nodes.Max(Depth);

    private static int Max(params int[] values)
        => values.Length == 0 ? 0 : values.Max();

    private static int AssignmentDepth(AssignmentNode a)
    {
        if (a.IndexExpr != null)
            return Max(Depth(a.Value), SaveSlot + Depth(a.IndexExpr));

        bool savesAddress = a.LValue is MemberAccessNode or DereferenceNode;
        return Depth(a.Value) + (savesAddress ? SaveSlot : 0);
    }

    private static int FunctionCallDepth(FunctionCallNode fc)
        => Math.Min(fc.Arguments.Count, 4) + MaxDepth(fc.Arguments);

    private static int IndirectCallDepth(IndirectCallNode ic)
        => Math.Min(ic.Arguments.Count, 4)
           + SaveSlot
           + Max(MaxDepth(ic.Arguments), Depth(ic.Callee));
}