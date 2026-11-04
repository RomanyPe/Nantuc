namespace Nantuc.Compiler.C.AST;

public class UnaryOpNode(string op, ASTNode operand) : ASTNode
{
    public string Operator { get; set; } = op;
    public ASTNode Operand { get; set; } = operand;
    public override string NameNode => nameof(UnaryOpNode);

}
