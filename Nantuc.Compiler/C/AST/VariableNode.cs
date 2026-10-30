namespace Nantuc.Compiler.C.AST;

public class VariableNode(string type, string name, ASTNode? initializer = null) : ASTNode
{
    public string? StructTypeName { get; set; }
    public string Type { get; set; } = type;
    public string Name { get; set; } = name;
    public ASTNode? Initializer { get; set; } = initializer;
    public List<ASTNode>? ArrayInitializer { get; set; }
    public bool IsPointer { get; set; }
    public string? PointedType { get; set; }
    public bool IsArray { get; set; }
    public int ArraySize { get; set; }
    public bool IsConst { get; set; }
    public bool IsPointedConst { get; set; }

    public override string NameNode => nameof(VariableNode);

}
