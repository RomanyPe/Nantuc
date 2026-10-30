namespace Nantuc.Compiler.C.AST;

public class StructDeclNode(string name, List<(string type, string fieldName)> fields) : ASTNode
{
    public string Name { get; set; } = name;
    public List<(string type, string fieldName)> Fields { get; set; } = fields;
    public override string NameNode => nameof(StructDeclNode);

}
