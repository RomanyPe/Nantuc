using Nantuc.Compiler.C.AST;

namespace Nantuc.Compiler.C.CodeGenerator.RegistersAlloc;

internal sealed class IrInstruction
{
    public ASTNode? Source;
    public readonly List<string> Defs = [];
    public readonly List<string> Uses = [];
    public readonly List<IdentifierNode> UseSites = [];
    public bool IsCopy;
    public string? CopyDst;    // для coalescing: имя переменной-приёмника
    public string? CopySrc;    // для coalescing: имя переменной-источника
    public bool HasCall;
    public int LoopDepth;
    public HashSet<string> LiveOut = new(StringComparer.Ordinal);
}

internal sealed class BasicBlock
{
    public int Id;
    public readonly List<IrInstruction> Code = [];
    public readonly List<BasicBlock> Succs = [];
    public readonly List<BasicBlock> Preds = [];
    public HashSet<string> LiveIn = new(StringComparer.Ordinal);
    public HashSet<string> LiveOut = new(StringComparer.Ordinal);
    public HashSet<string> Use = new(StringComparer.Ordinal);
    public HashSet<string> Def = new(StringComparer.Ordinal);
}

internal sealed record CfgResult(List<BasicBlock> Rpo, BasicBlock Entry, List<IrInstruction> Instructions);

internal sealed class CfgBuilder
{
    private readonly List<BasicBlock> _blocks = [];
    private readonly List<IrInstruction> _all = [];
    private readonly Stack<(BasicBlock Break, BasicBlock Continue)> _loops = new();
    private BasicBlock _cur = null!, _entry = null!, _exit = null!;
    private int _depth;

    public static CfgResult Build(FunctionNode func)
    {
        var b = new CfgBuilder();
        b._entry = b.NewBlock();
        b._exit = b.NewBlock();
        b._cur = b._entry;

        foreach (var p in func.Parameters)
            b.Emit(null).Defs.Add(p.Name);

        b.Stmt(func.Body);
        Link(b._cur, b._exit);
        return new CfgResult(b.Rpo(), b._entry, b._all);
    }

    private BasicBlock NewBlock()
    {
        var b = new BasicBlock { Id = _blocks.Count };
        _blocks.Add(b);
        return b;
    }

    private static void Link(BasicBlock a, BasicBlock b)
    {
        if (!a.Succs.Contains(b)) a.Succs.Add(b);
    }

    private IrInstruction Emit(ASTNode? src)
    {
        var i = new IrInstruction { Source = src, LoopDepth = _depth };
        _cur.Code.Add(i);
        _all.Add(i);
        return i;
    }

    private void Stmt(ASTNode? n)
    {
        switch (n)
        {
            case null: return;

            case BlockNode blk:
                foreach (var s in blk.Statements) Stmt(s);
                return;

            case VariableNode v:
                {
                    var ins = Emit(v);
                    Expr(v.Initializer, ins);
                    if (v.ArrayInitializer is not null)
                        foreach (var x in v.ArrayInitializer) Expr(x, ins);
                    ins.Defs.Add(v.Name);
                    return;
                }

            case AssignmentNode a:
                {
                    var ins = Emit(a);
                    Expr(a.Value, ins);
                    bool compound = a.CompoundOp is not null && a.CompoundOp != "=";

                    if (a.LValue is null)
                    {
                        ins.Defs.Add(a.Name);
                        if (compound)
                        {
                            // x += y читает x
                            ins.Uses.Add(a.Name);
                        }
                        else if (a.Value is IdentifierNode src && !ins.HasCall && ins.Defs.Count == 1)
                        {
                            // чистая копия x = y — кандидат на coalescing
                            ins.IsCopy = true;
                            ins.CopyDst = a.Name;
                            ins.CopySrc = src.Name;
                            ins.Uses.Clear();
                            ins.UseSites.Clear();
                            ins.Uses.Add(src.Name);
                            ins.UseSites.Add(src);
                        }
                    }
                    else
                    {
                        // arr[i] = v, *p = v, obj.f = v
                        Expr(a.LValue, ins);
                        Expr(a.IndexExpr, ins);
                        if (a.LValue is IdentifierNode lid) ins.Defs.Add(lid.Name);
                    }
                    return;
                }

            case IncrementNode inc:
                {
                    var ins = Emit(inc);
                    Expr(inc.Target, ins);
                    if (inc.Target is IdentifierNode id)
                    {
                        ins.Defs.Add(id.Name);
                        ins.Uses.Add(id.Name);
                    }
                    return;
                }

            case ReturnNode r:
                Expr(r.Value, Emit(r));
                Link(_cur, _exit);
                _cur = NewBlock();
                return;

            case IfNode i:
                {
                    Expr(i.Condition, Emit(i));
                    var thenB = NewBlock();
                    var joinB = NewBlock();
                    var elseB = i.ElseBlock is null ? joinB : NewBlock();
                    Link(_cur, thenB);
                    Link(_cur, elseB);
                    _cur = thenB;
                    Stmt(i.ThenBlock);
                    Link(_cur, joinB);
                    if (i.ElseBlock is not null)
                    {
                        _cur = elseB;
                        Stmt(i.ElseBlock);
                        Link(_cur, joinB);
                    }
                    _cur = joinB;
                    return;
                }

            case WhileNode w:
                {
                    var head = NewBlock();
                    var body = NewBlock();
                    var exit = NewBlock();
                    Link(_cur, head);
                    _loops.Push((exit, head));
                    _depth++;
                    _cur = head;
                    Expr(w.Condition, Emit(w));
                    Link(head, body);
                    Link(head, exit);
                    _cur = body;
                    Stmt(w.Body);
                    Link(_cur, head);
                    _depth--;
                    _loops.Pop();
                    _cur = exit;
                    return;
                }

            case DoWhileNode dw:
                {
                    var body = NewBlock();
                    var cond = NewBlock();
                    var exit = NewBlock();
                    Link(_cur, body);
                    _loops.Push((exit, cond));
                    _depth++;
                    _cur = body;
                    Stmt(dw.Body);
                    Link(_cur, cond);
                    _cur = cond;
                    Expr(dw.Condition, Emit(dw));
                    Link(cond, body);
                    Link(cond, exit);
                    _depth--;
                    _loops.Pop();
                    _cur = exit;
                    return;
                }

            case ForNode f:
                {
                    Stmt(f.Init);
                    var head = NewBlock();
                    var body = NewBlock();
                    var step = NewBlock();
                    var exit = NewBlock();
                    Link(_cur, head);
                    _loops.Push((exit, step));
                    _depth++;
                    _cur = head;
                    if (f.Condition is not null) Expr(f.Condition, Emit(f));
                    Link(head, body);
                    Link(head, exit);
                    _cur = body;
                    Stmt(f.Body);
                    Link(_cur, step);
                    _cur = step;
                    Stmt(f.Increment);
                    Link(step, head);
                    _depth--;
                    _loops.Pop();
                    _cur = exit;
                    return;
                }

            case BreakNode:
                Link(_cur, _loops.Peek().Break);
                _cur = NewBlock();
                return;

            case ContinueNode:
                Link(_cur, _loops.Peek().Continue);
                _cur = NewBlock();
                return;

            default:
                Expr(n, Emit(n));
                return;
        }
    }

    private static void Expr(ASTNode? n, IrInstruction ins)
    {
        switch (n)
        {
            case null: return;
            case IdentifierNode id:
                ins.Uses.Add(id.Name);
                ins.UseSites.Add(id);
                return;
            case BinaryOpNode b:
                Expr(b.Left, ins);
                Expr(b.Right, ins);
                return;
            case UnaryOpNode u:
                Expr(u.Operand, ins);
                return;
            case CastNode c:
                Expr(c.Operand, ins);
                return;
            case AddressOfNode ao:
                Expr(ao.Operand, ins);
                return;
            case DereferenceNode d:
                Expr(d.Operand, ins);
                return;
            case ArrayAccessNode aa:
                ins.Uses.Add(aa.ArrayName);
                Expr(aa.Index, ins);
                return;
            case MemberAccessNode ma:
                Expr(ma.Object, ins);
                return;
            case FunctionCallNode c:
                ins.HasCall = true;
                foreach (var a in c.Arguments) Expr(a, ins);
                return;
            case IndirectCallNode ic:
                ins.HasCall = true;
                Expr(ic.Callee, ins);
                foreach (var a in ic.Arguments) Expr(a, ins);
                return;
            case AssignmentNode a:
                Expr(a.Value, ins);
                bool compound = a.CompoundOp is not null && a.CompoundOp != "=";
                if (a.LValue is null)
                {
                    ins.Defs.Add(a.Name);
                    if (compound) ins.Uses.Add(a.Name);
                }
                else
                {
                    Expr(a.LValue, ins);
                    Expr(a.IndexExpr, ins);
                }
                return;
            case IncrementNode i2:
                Expr(i2.Target, ins);
                if (i2.Target is IdentifierNode id2)
                {
                    ins.Defs.Add(id2.Name);
                    ins.Uses.Add(id2.Name);
                }
                return;
            case VariableNode v2:
                Expr(v2.Initializer, ins);
                ins.Defs.Add(v2.Name);
                return;
            case SizeofTypeNode:
            case SizeofExprNode:
                return;
            default:
                return;
        }
    }

    private List<BasicBlock> Rpo()
    {
        var seen = new HashSet<BasicBlock>();
        var post = new List<BasicBlock>();
        Dfs(_entry);
        post.Reverse();
        foreach (var b in post) b.Preds.Clear();
        foreach (var b in post) foreach (var s in b.Succs) s.Preds.Add(b);
        return post;

        void Dfs(BasicBlock b)
        {
            if (!seen.Add(b)) return;
            foreach (var s in b.Succs) Dfs(s);
            post.Add(b);
        }
    }
}