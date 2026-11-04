namespace Nantuc.Compiler.C.AST;

public class ArrayAccessNode(string name, ASTNode index) : ASTNode
{
    public string ArrayName { get; set; } = name;
    public ASTNode Index { get; set; } = index;
    public override string NameNode => nameof(ArrayAccessNode);

}
