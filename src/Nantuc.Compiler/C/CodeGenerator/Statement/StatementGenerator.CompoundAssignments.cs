using Nantuc.Common;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Compiler.C.AST;

namespace Nantuc.Compiler.C.CodeGenerator.Statement;

public partial class StatementGenerator
{
    // ============================================================
    // Compound-присваивания: arr[i] op= rv, *p op= rv, obj.f op= rv, x op= rv.
    // ============================================================
    private void GenerateCompoundAssignment(AssignmentNode assign)
    {
        string baseOp = assign.CompoundOp!.TrimEnd('=');

        if (assign.IndexExpr != null) { GenerateCompoundArrayElement(assign, baseOp); return; }
        if (assign.LValue is DereferenceNode d) { GenerateCompoundDeref(d, baseOp, assign.Value); return; }
        if (assign.LValue is MemberAccessNode m) { GenerateCompoundMember(m, baseOp, assign.Value); return; }
        GenerateCompoundIdent(assign.Name, baseOp, assign.Value);
    }

    private void GenerateCompoundArrayElement(AssignmentNode assign, string baseOp)
    {
        var (size, signed) = _types.GetArrayElem(assign.Name);
        GenerateCompoundIndirect(
            reg => _exprGen.ComputeArrayElementAddress(assign.Name, assign.IndexExpr!, reg),
            size, signed, baseOp, assign.Value);
    }

    private void GenerateCompoundDeref(DereferenceNode deref, string baseOp, ASTNode rvalue)
    {
        var (size, signed) = _types.GetPointed(deref.Operand);
        GenerateCompoundIndirect(
            reg => _exprGen.GenerateExpression(deref.Operand, reg),
            size, signed, baseOp, rvalue);
    }

    private void GenerateCompoundMember(MemberAccessNode member, string baseOp, ASTNode rvalue)
    {
        var (size, signed) = _types.GetField(member);
        GenerateCompoundIndirect(
            reg => _exprGen.GenerateMemberAddress(member, reg),
            size, signed, baseOp, rvalue);
    }

    private void GenerateCompoundIdent(string name, string baseOp, ASTNode rvalue)
    {
        // Fast path: переменная уже в регистре
        if (_varAccessor.TryGetRegister(name, out RegType xReg))
        {
            RegType temp = (xReg != RegType.r0) ? RegType.r0 : RegType.r1;
            _exprGen.GenerateExpression(rvalue, temp);
            _exprGen.EmitBinaryOperation(baseOp, xReg, temp);
            if (_funcCtx.VarMap.TryGetValue(name, out var loc))
                _asm.EmitCastToType(xReg, loc.TypeSize, loc.IsSigned);
            return;
        }

        // Slow path: через память
        _varAccessor.LoadToRegister(name, RegType.r0);
        int slot = _exprGen.SaveRegToStack(RegType.r0);
        _exprGen.GenerateExpression(rvalue, RegType.r0);
        _exprGen.RestoreRegFromStack(slot, RegType.r1);
        _exprGen.EmitBinaryOperation(baseOp, RegType.r1, RegType.r0);
        _varAccessor.StoreFromRegister(name, RegType.r1);
    }

    /// <summary>
    /// Универсальная compound-операция для *ptr, obj.field, arr[i].
    /// computeAddress должен записать адрес LValue в переданный регистр (≠ r0).
    /// </summary>
    private void GenerateCompoundIndirect(
        Action<RegType> computeAddress, OpCodeSize size, bool isSigned,
        string baseOp, ASTNode rvalue)
    {
        computeAddress(RegType.r1);
        int addrSlot = _exprGen.SaveRegToStack(RegType.r1);

        _asm.EmitLoadInd(RegType.r0, RegType.r1, size);
        _asm.EmitCastToType(RegType.r0, size, isSigned);
        int valSlot = _exprGen.SaveRegToStack(RegType.r0);

        _exprGen.GenerateExpression(rvalue, RegType.r0);

        _exprGen.RestoreRegFromStack(valSlot, RegType.r1);
        _exprGen.EmitBinaryOperation(baseOp, RegType.r1, RegType.r0);
        _asm.EmitCastToType(RegType.r1, size, isSigned);

        _exprGen.RestoreRegFromStack(addrSlot, RegType.r2);
        _asm.EmitStoreInd(RegType.r1, RegType.r2, size);
    }
}