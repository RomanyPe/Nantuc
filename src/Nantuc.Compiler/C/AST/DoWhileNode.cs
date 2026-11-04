namespace Nantuc.Compiler.C.AST;

public class DoWhileNode(ASTNode body, ASTNode condition) : ASTNode
{
    public ASTNode Body { get; set; } = body;   // BlockNode
    public ASTNode Condition { get; set; } = condition;
    public override string NameNode => nameof(DoWhileNode);

}
