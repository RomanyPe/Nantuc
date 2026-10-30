namespace Nantuc.Compiler.C.AST;

public class IncrementNode(ASTNode target, int delta, bool isPrefix) : ASTNode
{
    public ASTNode Target { get; set; } = target;   // IdentifierNode / ArrayAccessNode / DereferenceNode / MemberAccessNode
    public int Delta { get; set; } = delta;    // +1 для ++, -1 для --
    public bool IsPrefix { get; set; } = isPrefix; // ++x = true, x++ = false
    public override string NameNode => nameof(IncrementNode);

}
