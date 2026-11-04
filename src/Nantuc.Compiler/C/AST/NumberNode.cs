namespace Nantuc.Compiler.C.AST;

public class NumberNode(long value) : ASTNode
{
    public long Value { get; set; } = value;
    public override string NameNode => nameof(NumberNode);

}
