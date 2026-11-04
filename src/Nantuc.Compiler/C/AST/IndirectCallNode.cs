namespace Nantuc.Compiler.C.AST;

public class IndirectCallNode(ASTNode callee, List<ASTNode> args) : ASTNode
{
    public ASTNode Callee { get; } = callee;
    public List<ASTNode> Arguments { get; } = args;
    public override string NameNode => nameof(IndirectCallNode);
}