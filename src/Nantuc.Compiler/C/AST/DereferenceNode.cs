namespace Nantuc.Compiler.C.AST;

public class DereferenceNode(ASTNode operand) : ASTNode
{
    public ASTNode Operand { get; set; } = operand;
    public override string NameNode => nameof(DereferenceNode);

}
