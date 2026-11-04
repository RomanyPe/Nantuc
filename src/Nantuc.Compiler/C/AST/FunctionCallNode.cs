namespace Nantuc.Compiler.C.AST;

public class FunctionCallNode(string name) : ASTNode
{
    public string Name { get; set; } = name;
    public List<ASTNode> Arguments { get; set; } = [];
    public override string NameNode => nameof(FunctionCallNode);

}
