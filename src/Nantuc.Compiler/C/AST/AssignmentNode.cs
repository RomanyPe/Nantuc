namespace Nantuc.Compiler.C.AST;

public class AssignmentNode(string name, ASTNode value, ASTNode? indexExpr = null) : ASTNode
{
    public string Name { get; set; } = name;
    public ASTNode Value { get; set; } = value;
    public ASTNode? IndexExpr { get; set; } = indexExpr;
    public ASTNode? LValue { get; set; }
    public string? CompoundOp { get; set; }
    public override string NameNode => nameof(AssignmentNode);

}
