namespace Nantuc.Compiler.C.AST;

public class FunctionNode(string name, string returnType, BlockNode body) : ASTNode
{
    public string Name { get; set; } = name;
    public string ReturnType { get; set; } = returnType;
    public List<ParameterNode> Parameters { get; set; } = [];
    public BlockNode Body { get; set; } = body;
    public int LocalSize { get; set; }
    public bool IsExternal { get; set; }
    public override string NameNode => nameof(FunctionNode);

}
