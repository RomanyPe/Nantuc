using Nantuc.Compiler.C.AST;

namespace Nantuc.Compiler.C.CodeGenerator.RegistersAlloc;

// ============================================================
// AST-анализ переменных. Два независимых прохода:
//   • CollectAddressTakenVars — имена x, к которым применён &x
//   • CollectVarEvents        — позиции def/use в линейном обходе
//
// ВАЖНО: проходы НЕ объединены, потому что по-разному обходят
// AssignmentNode (CollectVarEvents не заходит в LValue/IndexExpr).
// Это влияет на liveness — сохраняем текущее поведение бит-в-бит.
// ============================================================
internal static class VariableAccessAnalyzer
{
    // ------------------------------------------------------------
    // Проход 1: &x
    // ------------------------------------------------------------
    public static HashSet<string> CollectAddressTakenVars(BlockNode body)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        Walk(body);
        return result;

        void Walk(ASTNode? node)
        {
            switch (node)
            {
                case null: return;

                case AddressOfNode { Operand: IdentifierNode id }:
                    result.Add(id.Name);
                    return;
                case AddressOfNode ao:
                    Walk(ao.Operand);
                    return;

                case BinaryOpNode b: Walk(b.Left); Walk(b.Right); return;
                case UnaryOpNode u: Walk(u.Operand); return;

                case AssignmentNode a:
                    if (a.LValue is not null) Walk(a.LValue);
                    if (a.IndexExpr is not null) Walk(a.IndexExpr);
                    Walk(a.Value);
                    return;

                case IfNode i:
                    Walk(i.Condition); Walk(i.ThenBlock); Walk(i.ElseBlock);
                    return;
                case WhileNode w:
                    Walk(w.Condition); Walk(w.Body);
                    return;
                case ForNode f:
                    Walk(f.Init); Walk(f.Condition); Walk(f.Increment); Walk(f.Body);
                    return;
                case ReturnNode r:
                    Walk(r.Value);
                    return;

                case FunctionCallNode c:
                    foreach (var arg in c.Arguments) Walk(arg);
                    return;

                case ArrayAccessNode aa: Walk(aa.Index); return;
                case MemberAccessNode ma: Walk(ma.Object); return;
                case DereferenceNode d: Walk(d.Operand); return;

                case BlockNode blk:
                    foreach (var s in blk.Statements) Walk(s);
                    return;
                case DoWhileNode dw:
                    Walk(dw.Body); Walk(dw.Condition);
                    return;
                case IncrementNode inc:
                    Walk(inc.Target);
                    return;

                case VariableNode v:
                    Walk(v.Initializer);
                    if (v.ArrayInitializer is not null)
                        foreach (var x in v.ArrayInitializer) Walk(x);
                    return;

                case IndirectCallNode ic:
                    Walk(ic.Callee);
                    foreach (var arg in ic.Arguments) Walk(arg);
                    return;
                case CastNode cast:          // или TypeCastNode, CastExpressionNode
                    Walk(cast.Operand);   // или cast.Operand, в зависимости от имени свойства
                    break;
                default: return;
            }
        }
    }

    // ------------------------------------------------------------
    // Проход 2: def/use events
    //
    // ВНИМАНИЕ: AssignmentNode обрабатывается УПРОЩЁННО —
    // для arr[i] = v, *p = v, obj.f = v не обходятся LValue/IndexExpr.
    // Это баг liveness (i / p / obj не помечаются как used),
    // сохранённый для совместимости с текущей раскраской.
    // ------------------------------------------------------------
    public static (Dictionary<string, List<int>> Defs, Dictionary<string, List<int>> Uses)
        CollectVarEvents(BlockNode body)
    {
        var defs = new Dictionary<string, List<int>>();
        var uses = new Dictionary<string, List<int>>();
        int counter = 0;

        Walk(body);
        return (defs, uses);

        void Walk(ASTNode node)
        {
            if (node is null) return;
            counter++;

            switch (node)
            {
                case VariableNode v:
                    AddEvent(defs, v.Name, counter);
                    if (v.Initializer is not null) Walk(v.Initializer);
                    break;

                case AssignmentNode a:
                    if (a.LValue is null)
                        AddEvent(defs, a.Name, counter);
                    Walk(a.Value);
                    break;

                case IdentifierNode id:
                    AddEvent(uses, id.Name, counter);
                    break;

                case BinaryOpNode b:
                    Walk(b.Left); Walk(b.Right);
                    break;

                case UnaryOpNode u:
                    Walk(u.Operand);
                    break;

                case IfNode i:
                    Walk(i.Condition); Walk(i.ThenBlock);
                    if (i.ElseBlock is not null) Walk(i.ElseBlock);
                    break;

                case WhileNode w:
                    Walk(w.Condition); Walk(w.Body);
                    break;

                case ForNode f:
                    if (f.Init is not null) Walk(f.Init);
                    if (f.Condition is not null) Walk(f.Condition);
                    Walk(f.Body);
                    if (f.Increment is not null) Walk(f.Increment);
                    break;

                case ReturnNode r:
                    if (r.Value is not null) Walk(r.Value);
                    break;

                case FunctionCallNode c:
                    foreach (var arg in c.Arguments) Walk(arg);
                    break;

                case ArrayAccessNode arr:
                    Walk(arr.Index);
                    AddEvent(uses, arr.ArrayName, counter);
                    break;

                case MemberAccessNode ma:
                    Walk(ma.Object);
                    break;

                case AddressOfNode ao:
                    Walk(ao.Operand);
                    break;

                case DereferenceNode d:
                    Walk(d.Operand);
                    break;

                case BlockNode blk:
                    foreach (var s in blk.Statements) Walk(s);
                    break;

                case DoWhileNode dw:
                    Walk(dw.Body); Walk(dw.Condition);
                    break;

                case IncrementNode inc:
                    Walk(inc.Target);
                    if (inc.Target is IdentifierNode idNode)
                    {
                        AddEvent(defs, idNode.Name, counter);
                        AddEvent(uses, idNode.Name, counter);
                    }
                    break;

                case SizeofTypeNode:
                case SizeofExprNode:
                    break;

                case IndirectCallNode ic:
                    Walk(ic.Callee);
                    foreach (var arg in ic.Arguments) Walk(arg);
                    break;
                case CastNode cast:          // или TypeCastNode, CastExpressionNode
                    Walk(cast.Operand);   // или cast.Operand, в зависимости от имени свойства
                    break;

                default:
                    break;
            }
        }
    }

    public static void AddEvent(Dictionary<string, List<int>> dict, string name, int pos)
    {
        if (!dict.TryGetValue(name, out var list))
            dict[name] = list = [];
        list.Add(pos);
    }
}