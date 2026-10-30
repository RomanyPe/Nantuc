namespace Nantuc.Compiler.C.AST;

public class BinaryOpNode(string op, ASTNode left, ASTNode right) : ASTNode
{
    public string Operator { get; set; } = op;
    public ASTNode Left { get; set; } = left;
    public ASTNode Right { get; set; } = right;
    public override string NameNode => nameof(BinaryOpNode);

}
