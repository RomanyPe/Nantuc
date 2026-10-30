namespace Nantuc.Compiler.C.AST;

public class ParameterNode(string type, string name) : ASTNode
{
    public string Type { get; set; } = type;
    public string Name { get; set; } = name;
    public bool IsPointer { get; set; }
    public string? PointedType { get; set; }
    public bool IsConst { get; set; }
    public bool IsPointedConst { get; set; }

    public override string NameNode => nameof(ParameterNode);

}
