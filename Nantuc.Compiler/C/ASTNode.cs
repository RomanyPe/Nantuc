namespace Nantuc.Compiler.C;

// ============================================================
// БАЗОВЫЙ КЛАСС И РЕАЛИЗАЦИИ УЗЛОВ
// ============================================================

public abstract class ASTNode;

public class ProgramNode : ASTNode
{
    public List<StructDeclNode> StructNodes { get; set; } = [];
    public List<FunctionNode> FunctionNodes { get; set; } = [];
    public List<VariableNode> GlobalVarNodes { get; set; } = [];
    public List<string> IncludesList { get; set; } = [];
}

public class FunctionNode(string name, string returnType, BlockNode body) : ASTNode
{
    public string Name { get; set; } = name;
    public string ReturnType { get; set; } = returnType;
    public List<ParameterNode> Parameters { get; set; } = [];
    public BlockNode Body { get; set; } = body;
    public int LocalSize { get; set; }
    public bool IsExternal { get; set; }
}

public class ParameterNode(string type, string name) : ASTNode
{
    public string Type { get; set; } = type;
    public string Name { get; set; } = name;
    public bool IsPointer { get; set; }
    public string? PointedType { get; set; }
}

public class BlockNode : ASTNode
{
    public List<ASTNode> Statements { get; set; } = [];
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
}

public class AssignmentNode(string name, ASTNode value, ASTNode? indexExpr = null) : ASTNode
{
    public string Name { get; set; } = name;
    public ASTNode Value { get; set; } = value;
    public ASTNode? IndexExpr { get; set; } = indexExpr;
    public ASTNode? LValue { get; set; }

}

public class BinaryOpNode(string op, ASTNode left, ASTNode right) : ASTNode
{
    public string Operator { get; set; } = op;
    public ASTNode Left { get; set; } = left;
    public ASTNode Right { get; set; } = right;

}

public class UnaryOpNode(string op, ASTNode operand) : ASTNode
{
    public string Operator { get; set; } = op;
    public ASTNode Operand { get; set; } = operand;
}

public class NumberNode(long value) : ASTNode
{
    public long Value { get; set; } = value;
}

public class IdentifierNode(string name) : ASTNode
{
    public string Name { get; set; } = name;
}

public class IfNode(ASTNode condition, BlockNode thenBlock, BlockNode? elseBlock = null) : ASTNode
{
    public ASTNode Condition { get; set; } = condition;
    public BlockNode ThenBlock { get; set; } = thenBlock;
    public BlockNode? ElseBlock { get; set; } = elseBlock;
}

public class WhileNode(ASTNode condition, BlockNode body) : ASTNode
{
    public ASTNode Condition { get; set; } = condition;
    public BlockNode Body { get; set; } = body;
}

public class ForNode(ASTNode? init, ASTNode? condition, ASTNode? increment, BlockNode body) : ASTNode
{
    public ASTNode? Init { get; set; } = init;
    public ASTNode? Condition { get; set; } = condition;
    public ASTNode? Increment { get; set; } = increment;
    public BlockNode Body { get; set; } = body;

}

public class ReturnNode(ASTNode? value = null) : ASTNode
{
    public ASTNode? Value { get; set; } = value;
}

public class FunctionCallNode(string name) : ASTNode
{
    public string Name { get; set; } = name;
    public List<ASTNode> Arguments { get; set; } = [];
}

public class ArrayAccessNode(string name, ASTNode index) : ASTNode
{
    public string ArrayName { get; set; } = name;
    public ASTNode Index { get; set; } = index;
}

public class AddressOfNode(ASTNode operand) : ASTNode
{
    public ASTNode Operand { get; set; } = operand;
}

public class DereferenceNode(ASTNode operand) : ASTNode
{
    public ASTNode Operand { get; set; } = operand;
}

public class InlineAsmNode(string asmCode) : ASTNode
{
    public string AsmCode { get; set; } = asmCode;
}

public class StructDeclNode(string name, List<(string type, string fieldName)> fields) : ASTNode
{
    public string Name { get; set; } = name;
    public List<(string type, string fieldName)> Fields { get; set; } = fields;
}

public class MemberAccessNode(ASTNode obj, string fieldName, bool isArrow) : ASTNode
{
    public ASTNode Object { get; set; } = obj;
    public string FieldName { get; set; } = fieldName;
    public bool IsArrow { get; set; } = isArrow;
}