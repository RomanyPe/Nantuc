namespace Nantuc.Compiler.C.AST;

public class InlineAsmNode(string asmCode) : ASTNode
{
    public string AsmCode { get; set; } = asmCode;
    public override string NameNode => nameof(InlineAsmNode);

}
