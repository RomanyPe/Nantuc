namespace Nantuc.Compiler.C.AST;

public class BlockNode : ASTNode
{
    public List<ASTNode> Statements { get; set; } = [];
    public override string NameNode => nameof(BlockNode);
}
