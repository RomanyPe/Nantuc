namespace Nantuc.Compiler.C.AST;

public class SizeofTypeNode(string typeName, bool isPointer) : ASTNode
{
    public string TypeName { get; set; } = typeName;
    public bool IsPointer { get; set; } = isPointer;
    public override string NameNode => nameof(SizeofTypeNode);

}
