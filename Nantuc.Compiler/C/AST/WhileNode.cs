namespace Nantuc.Compiler.C.AST;

public class WhileNode(ASTNode condition, BlockNode body) : ASTNode
{
    public ASTNode Condition { get; set; } = condition;
    public BlockNode Body { get; set; } = body;
    public override string NameNode => nameof(WhileNode);

}
