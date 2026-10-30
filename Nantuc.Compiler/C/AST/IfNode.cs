namespace Nantuc.Compiler.C.AST;

public class IfNode(ASTNode condition, BlockNode thenBlock, BlockNode? elseBlock = null) : ASTNode
{
    public ASTNode Condition { get; set; } = condition;
    public BlockNode ThenBlock { get; set; } = thenBlock;
    public BlockNode? ElseBlock { get; set; } = elseBlock;
    public override string NameNode => nameof(IfNode);

}
