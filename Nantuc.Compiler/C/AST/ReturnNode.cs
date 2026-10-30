namespace Nantuc.Compiler.C.AST;

public class ReturnNode(ASTNode? value = null) : ASTNode
{
    public ASTNode? Value { get; set; } = value;
    public override string NameNode => nameof(ReturnNode);

}
