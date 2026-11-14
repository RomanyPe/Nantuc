using Nantuc.Common;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Compiler.C.AST;

namespace Nantuc.Compiler.C.CodeGenerator.Statement;

public partial class StatementGenerator
{
    // ============================================================
    // Простые присваивания (lhs = rhs).
    // ============================================================
    private void GenerateAssignment(AssignmentNode assign)
    {
        _exprGen.CheckConstTarget(assign);

        if (assign.CompoundOp != null && assign.CompoundOp != "=")
        {
            GenerateCompoundAssignment(assign);
            return;
        }

        // Порядок важен: сначала специализированные LValue-формы,
        // потом — оптимизации для простого имени.
        if (assign.IndexExpr != null) { GenerateArrayAssignment(assign); return; }
        if (assign.LValue is DereferenceNode d) { GenerateDerefAssignment(assign, d); return; }
        if (assign.LValue is MemberAccessNode m) { GenerateMemberAssignment(assign, m); return; }
        if (TryGenerateRegisterFastPath(assign)) return;
        if (TryGenerateRegisterBinaryFastPath(assign)) return;

        GenerateSimpleAssignment(assign);
    }

    private void GenerateArrayAssignment(AssignmentNode assign)
    {
        _exprGen.GenerateExpression(assign.Value);                      // value -> r0
        _exprGen.StoreR0ToArrayElement(assign.Name, assign.IndexExpr!);
    }

    private void GenerateDerefAssignment(AssignmentNode assign, DereferenceNode deref)
    {
        _exprGen.GenerateExpression(assign.Value);
        int slot = _exprGen.SaveRegToStack(RegType.r0);

        _exprGen.GenerateExpression(deref.Operand, RegType.r0);
        OpCodeSize size = _types.GetPointedSize(deref.Operand);

        _exprGen.RestoreRegFromStack(slot, RegType.r1);
        _asm.EmitStoreInd(RegType.r1, RegType.r0, size);
    }

    private void GenerateMemberAssignment(AssignmentNode assign, MemberAccessNode member)
    {
        _exprGen.GenerateExpression(assign.Value);
        int slot = _exprGen.SaveRegToStack(RegType.r0);

        _exprGen.GenerateMemberAddress(member, RegType.r0);
        OpCodeSize size = _types.GetFieldSize(member);

        _exprGen.RestoreRegFromStack(slot, RegType.r1);
        _asm.EmitStoreInd(RegType.r1, RegType.r0, size);
    }

    /// <summary>x = &lt;простое значение&gt; где x — в регистре.</summary>
    private bool TryGenerateRegisterFastPath(AssignmentNode assign)
    {
        if (assign.LValue != null) return false;
        if (!_funcCtx.VarMap.TryGetValue(assign.Name, out var regLoc) || !regLoc.IsRegister) return false;
        if (assign.Value is not (NumberNode or IdentifierNode)) return false;

        _exprGen.GenerateExpression(assign.Value, regLoc.Register);
        _asm.EmitCastToType(regLoc.Register, regLoc.TypeSize, regLoc.IsSigned);
        return true;
    }

    /// <summary>x = x &lt;op&gt; expr где x — в регистре и op ∈ {+, -, *, /}.</summary>
    private bool TryGenerateRegisterBinaryFastPath(AssignmentNode assign)
    {
        if (!_funcCtx.VarMap.TryGetValue(assign.Name, out var loc) || !loc.IsRegister) return false;

        if (assign.Value is not BinaryOpNode binop
            || binop.Left is not IdentifierNode id
            || id.Name != assign.Name
            || binop.Operator is not ("+" or "-" or "*" or "/"))
        {
            _exprGen.GenerateExpression(assign.Value, loc.Register);
            _asm.EmitCastToType(loc.Register, loc.TypeSize, loc.IsSigned);
            return true;
        }

        RegType xReg = loc.Register;
        RegType temp = (xReg != RegType.r0) ? RegType.r0 : RegType.r1;
        _exprGen.GenerateExpression(binop.Right, temp);
        _exprGen.EmitBinaryOperation(binop.Operator, xReg, temp);
        _asm.EmitCastToType(xReg, loc.TypeSize, loc.IsSigned);
        return true;
    }

    private void GenerateSimpleAssignment(AssignmentNode assign)
    {
        _exprGen.GenerateExpression(assign.Value);
        _varAccessor.StoreFromRegister(assign.Name, RegType.r0);
    }
}