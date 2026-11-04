namespace Nantuc.Compiler.C.AST;

public class SizeofExprNode(ASTNode operand) : ASTNode
{
    public ASTNode Operand { get; set; } = operand;
    public override string NameNode => nameof(SizeofExprNode);

}
