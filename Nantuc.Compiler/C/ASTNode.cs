using System.Security.Cryptography;

namespace Nantuc.Compiler.C;

// ============================================================
// БАЗОВЫЙ КЛАСС И РЕАЛИЗАЦИИ УЗЛОВ
// ============================================================

public abstract class ASTNode
{
    public abstract string NameNode { get; }
}

public class ProgramNode : ASTNode
{
    public List<StructDeclNode> StructNodes { get; set; } = [];
    public List<FunctionNode> FunctionNodes { get; set; } = [];
    public List<VariableNode> GlobalVarNodes { get; set; } = [];
    public List<string> IncludesList { get; set; } = [];

    public override string NameNode => nameof(ProgramNode);
}

public class FunctionNode(string name, string returnType, BlockNode body) : ASTNode
{
    public string Name { get; set; } = name;
    public string ReturnType { get; set; } = returnType;
    public List<ParameterNode> Parameters { get; set; } = [];
    public BlockNode Body { get; set; } = body;
    public int LocalSize { get; set; }
    public bool IsExternal { get; set; }
    public override string NameNode => nameof(FunctionNode);

}

public class ParameterNode(string type, string name) : ASTNode
{
    public string Type { get; set; } = type;
    public string Name { get; set; } = name;
    public bool IsPointer { get; set; }
    public string? PointedType { get; set; }
    public override string NameNode => nameof(ParameterNode);

}

public class BlockNode : ASTNode
{
    public List<ASTNode> Statements { get; set; } = [];
    public override string NameNode => nameof(BlockNode);
}

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
    public override string NameNode => nameof(VariableNode);

}

public class AssignmentNode(string name, ASTNode value, ASTNode? indexExpr = null) : ASTNode
{
    public string Name { get; set; } = name;
    public ASTNode Value { get; set; } = value;
    public ASTNode? IndexExpr { get; set; } = indexExpr;
    public ASTNode? LValue { get; set; }
    public string? CompoundOp { get; set; }
    public override string NameNode => nameof(AssignmentNode);

}

public class BinaryOpNode(string op, ASTNode left, ASTNode right) : ASTNode
{
    public string Operator { get; set; } = op;
    public ASTNode Left { get; set; } = left;
    public ASTNode Right { get; set; } = right;
    public override string NameNode => nameof(BinaryOpNode);

}

public class UnaryOpNode(string op, ASTNode operand) : ASTNode
{
    public string Operator { get; set; } = op;
    public ASTNode Operand { get; set; } = operand;
    public override string NameNode => nameof(UnaryOpNode);

}

public class NumberNode(long value) : ASTNode
{
    public long Value { get; set; } = value;
    public override string NameNode => nameof(NumberNode);

}

public class IdentifierNode(string name) : ASTNode
{
    public string Name { get; set; } = name;
    public override string NameNode => nameof(IdentifierNode);

}

public class IfNode(ASTNode condition, BlockNode thenBlock, BlockNode? elseBlock = null) : ASTNode
{
    public ASTNode Condition { get; set; } = condition;
    public BlockNode ThenBlock { get; set; } = thenBlock;
    public BlockNode? ElseBlock { get; set; } = elseBlock;
    public override string NameNode => nameof(IfNode);

}

public class WhileNode(ASTNode condition, BlockNode body) : ASTNode
{
    public ASTNode Condition { get; set; } = condition;
    public BlockNode Body { get; set; } = body;
    public override string NameNode => nameof(WhileNode);

}

public class ForNode(ASTNode? init, ASTNode? condition, ASTNode? increment, BlockNode body) : ASTNode
{
    public ASTNode? Init { get; set; } = init;
    public ASTNode? Condition { get; set; } = condition;
    public ASTNode? Increment { get; set; } = increment;
    public BlockNode Body { get; set; } = body;
    public override string NameNode => nameof(ForNode);


}

public class ReturnNode(ASTNode? value = null) : ASTNode
{
    public ASTNode? Value { get; set; } = value;
    public override string NameNode => nameof(ReturnNode);

}

public class FunctionCallNode(string name) : ASTNode
{
    public string Name { get; set; } = name;
    public List<ASTNode> Arguments { get; set; } = [];
    public override string NameNode => nameof(FunctionCallNode);

}

public class ArrayAccessNode(string name, ASTNode index) : ASTNode
{
    public string ArrayName { get; set; } = name;
    public ASTNode Index { get; set; } = index;
    public override string NameNode => nameof(ArrayAccessNode);

}

public class AddressOfNode(ASTNode operand) : ASTNode
{
    public ASTNode Operand { get; set; } = operand;
    public override string NameNode => nameof(AddressOfNode);

}

public class DereferenceNode(ASTNode operand) : ASTNode
{
    public ASTNode Operand { get; set; } = operand;
    public override string NameNode => nameof(DereferenceNode);

}

public class InlineAsmNode(string asmCode) : ASTNode
{
    public string AsmCode { get; set; } = asmCode;
    public override string NameNode => nameof(InlineAsmNode);

}

public class StructDeclNode(string name, List<(string type, string fieldName)> fields) : ASTNode
{
    public string Name { get; set; } = name;
    public List<(string type, string fieldName)> Fields { get; set; } = fields;
    public override string NameNode => nameof(StructDeclNode);

}

public class MemberAccessNode(ASTNode obj, string fieldName, bool isArrow) : ASTNode
{
    public ASTNode Object { get; set; } = obj;
    public string FieldName { get; set; } = fieldName;
    public bool IsArrow { get; set; } = isArrow;
    public override string NameNode => nameof(MemberAccessNode);

}

public class BreakNode : ASTNode
{
    public override string NameNode => nameof(BreakNode);

}
public class ContinueNode : ASTNode
{
    public override string NameNode => nameof(ContinueNode);

}

public class DoWhileNode(ASTNode body, ASTNode condition) : ASTNode
{
    public ASTNode Body { get; set; } = body;   // BlockNode
    public ASTNode Condition { get; set; } = condition;
    public override string NameNode => nameof(DoWhileNode);

}

public class IncrementNode(ASTNode target, int delta, bool isPrefix) : ASTNode
{
    public ASTNode Target { get; set; } = target;   // IdentifierNode / ArrayAccessNode / DereferenceNode / MemberAccessNode
    public int Delta { get; set; } = delta;    // +1 для ++, -1 для --
    public bool IsPrefix { get; set; } = isPrefix; // ++x = true, x++ = false
    public override string NameNode => nameof(IncrementNode);

}

public class SizeofTypeNode(string typeName, bool isPointer) : ASTNode
{
    public string TypeName { get; set; } = typeName;
    public bool IsPointer { get; set; } = isPointer;
    public override string NameNode => nameof(SizeofTypeNode);

}

public class SizeofExprNode(ASTNode operand) : ASTNode
{
    public ASTNode Operand { get; set; } = operand;
    public override string NameNode => nameof(SizeofExprNode);

}
