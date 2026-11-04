namespace Nantuc.Compiler.C.AST;

public class IdentifierNode(string name) : ASTNode
{
    public string Name { get; set; } = name;
    public override string NameNode => nameof(IdentifierNode);

}
