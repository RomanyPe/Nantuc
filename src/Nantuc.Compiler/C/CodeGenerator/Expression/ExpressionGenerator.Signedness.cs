using Nantuc.Compiler.C.AST;

namespace Nantuc.Compiler.C.CodeGenerator.Expression;

public partial class ExpressionGenerator
{
    // ============================================================
    // Определение знаковости значения выражения.
    // true  → signed   (JL/JG при сравнении)
    // false → unsigned (JB/JA при сравнении), включая указатели.
    // ============================================================
    internal bool IsExpressionSigned(ASTNode expr) => expr switch
    {
        NumberNode => true,                // литералы — long
        IdentifierNode id => IsIdentifierSigned(id.Name),
        ArrayAccessNode arr => IsArrayElemSigned(arr.ArrayName),
        DereferenceNode deref => IsPointedSigned(deref.Operand),
        MemberAccessNode ma => IsMemberSigned(ma),
        AddressOfNode => false,               // адрес → указатель
        SizeofTypeNode
            or SizeofExprNode => false,               // sizeof → size_t (unsigned)
        FunctionCallNode call => IsFunctionCallSigned(call.Name),
        IndirectCallNode => true,                // консервативно
        UnaryOpNode unop => unop.Operator switch
        {
            "!" => true,                         // bool → int
            "-" or "~" => IsExpressionSigned(unop.Operand),
            _ => true,
        },
        BinaryOpNode bin => IsBinaryResultSigned(bin),
        IncrementNode inc => IsExpressionSigned(inc.Target),
        AssignmentNode assign => IsAssignmentResultSigned(assign),
        CastNode cast => CodeGenUtils.IsSignedType(cast.TypeName),
        _ => true,
    };

    private bool IsIdentifierSigned(string name)
    {
        if (_functionTable.ContainsKey(name)) return false;      // имя функции в выражении → адрес → указатель

        if (_funcCtx.VarMap.TryGetValue(name, out var loc))
        {
            if (loc.IsPointer || loc.IsArray) return false;
            return loc.IsSigned;
        }
        if (_globalMem.Contains(name))
        {
            var g = _globalMem.GetInfo(name)!.Value;
            if (g.IsPointer || g.IsArray) return false;
            return g.IsSigned;
        }
        return true;
    }

    private bool IsFunctionCallSigned(string name)
    {
        if (!_functionTable.TryGetValue(name, out var f)) return true;
        if (f.ReturnIsPointer) return false;
        if (f.ReturnType == "void" || _structTable.ContainsKey(f.ReturnType)) return true;
        return CodeGenUtils.IsSignedType(f.ReturnType);
    }

    private bool IsMemberSigned(MemberAccessNode ma)
    {
        var (_, field, _) = ResolveMemberAccessType(ma);
        return CodeGenUtils.IsSignedType(field.Type);
    }

    private bool IsBinaryResultSigned(BinaryOpNode bin)
    {
        // Сравнения, логика, запятая → int (signed)
        if (CodeGenUtils.IsComparisonOperator(bin.Operator)
            || bin.Operator is "&&" or "||" or ",")
            return true;

        // C usual arithmetic conversions: unsigned если ХОТЯ БЫ ОДИН операнд unsigned
        return IsExpressionSigned(bin.Left) && IsExpressionSigned(bin.Right);
    }

    private bool IsAssignmentResultSigned(AssignmentNode assign)
    {
        if (assign.IndexExpr != null) return IsArrayElemSigned(assign.Name);
        if (assign.LValue is DereferenceNode d) return IsPointedSigned(d.Operand);
        if (assign.LValue is MemberAccessNode m) return IsMemberSigned(m);
        return IsIdentifierSigned(assign.Name);
    }
}