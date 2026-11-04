namespace Nantuc.Compiler.C.AST;

public class ProgramNode : ASTNode
{
    public List<StructDeclNode> StructNodes { get; set; } = [];
    public List<FunctionNode> FunctionNodes { get; set; } = [];
    public List<VariableNode> GlobalVarNodes { get; set; } = [];
    public List<string> IncludesList { get; set; } = [];

    public override string NameNode => nameof(ProgramNode);
}
