namespace Nantuc.Compiler.C.AST;

public class MemberAccessNode(ASTNode obj, string fieldName, bool isArrow) : ASTNode
{
    public ASTNode Object { get; set; } = obj;
    public string FieldName { get; set; } = fieldName;
    public bool IsArrow { get; set; } = isArrow;
    public override string NameNode => nameof(MemberAccessNode);

}
