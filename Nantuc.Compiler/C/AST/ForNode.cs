namespace Nantuc.Compiler.C.AST;

public class ForNode(ASTNode? init, ASTNode? condition, ASTNode? increment, BlockNode body) : ASTNode
{
    public ASTNode? Init { get; set; } = init;
    public ASTNode? Condition { get; set; } = condition;
    public ASTNode? Increment { get; set; } = increment;
    public BlockNode Body { get; set; } = body;
    public override string NameNode => nameof(ForNode);


}
