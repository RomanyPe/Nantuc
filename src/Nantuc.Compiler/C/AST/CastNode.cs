namespace Nantuc.Compiler.C.AST;

public class CastNode(string typeName, bool isPointer, ASTNode operand) : ASTNode
{
    public string TypeName { get; set; } = typeName;
    public bool IsPointer { get; set; } = isPointer;
    public ASTNode Operand { get; set; } = operand;
    public override string NameNode => nameof(CastNode);
}
