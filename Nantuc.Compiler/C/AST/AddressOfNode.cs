namespace Nantuc.Compiler.C.AST;

public class AddressOfNode(ASTNode operand) : ASTNode
{
    public ASTNode Operand { get; set; } = operand;
    public override string NameNode => nameof(AddressOfNode);

}
