using Nantuc.Common;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Compiler.C.AST;
using Nantuc.Diagnostics;

namespace Nantuc.Compiler.C.CodeGenerator.Expression;

public partial class ExpressionGenerator
{
    // ============================================================
    // Присваивание в контексте выражения: результат (новое значение)
    // остаётся в targetReg. Покрывает все lvalue-формы и compound-op.
    // ============================================================
    internal void CheckConstTarget(AssignmentNode assign)
    {
        if (assign.IndexExpr != null)
        {
            if (_varAccessor.IsPointedConst(assign.Name))
                _asm.ErrorReporter.Throw(ErrorCode.CodeGen_AssignmentToConst, DiagnosticData.Of(assign.Name));
            return;
        }

        if (assign.LValue is DereferenceNode deref && deref.Operand is IdentifierNode ptrId)
        {
            if (_varAccessor.IsPointedConst(ptrId.Name))
                _asm.ErrorReporter.Throw(ErrorCode.CodeGen_AssignmentToConst, DiagnosticData.Of(ptrId.Name));
            return;
        }

        if (assign.LValue is MemberAccessNode) return;   // TODO: const-поля

        if (_varAccessor.IsConst(assign.Name))
            _asm.ErrorReporter.Throw(ErrorCode.CodeGen_AssignmentToConst, DiagnosticData.Of(assign.Name));
    }

    private void GenerateAssignmentExpr(AssignmentNode assign, RegType targetReg)
    {
        CheckConstTarget(assign);

        if (assign.CompoundOp != null && assign.CompoundOp != "=")
        {
            GenerateCompoundAssignmentExpr(assign, targetReg);
            return;
        }

        // arr[i] = value
        if (assign.IndexExpr != null)
        {
            GenerateExpression(assign.Value, targetReg);
            int slot = SaveRegToStack(targetReg);
            if (targetReg != RegType.r0) _asm.EmitMov(RegType.r0, targetReg);
            StoreR0ToArrayElement(assign.Name, assign.IndexExpr);
            RestoreRegFromStack(slot, targetReg);

            OpCodeSize size = GetIndexedElemSize(assign.Name);
            _asm.EmitCastToType(targetReg, size, IsArrayElemSigned(assign.Name));
            return;
        }

        // *ptr = value
        if (assign.LValue is DereferenceNode deref)
        {
            GenerateExpression(assign.Value, targetReg);
            int slot = SaveRegToStack(targetReg);
            GenerateExpression(deref.Operand, RegType.r0);

            OpCodeSize size = GetPointedSize(deref.Operand);
            bool signed = IsPointedSigned(deref.Operand);

            RestoreRegFromStack(slot, RegType.r1);
            _asm.EmitCastToType(RegType.r1, size, signed);
            _asm.EmitStoreInd(RegType.r1, RegType.r0, size);
            if (targetReg != RegType.r1) _asm.EmitMov(targetReg, RegType.r1);
            return;
        }

        // obj.field = value
        if (assign.LValue is MemberAccessNode member)
        {
            GenerateExpression(assign.Value, targetReg);
            int slot = SaveRegToStack(targetReg);
            GenerateMemberAddress(member, RegType.r0);

            var (_, field, _) = ResolveMemberAccessType(member);
            OpCodeSize size = CodeGenUtils.GetSizeForType(field.Type, _asm.ErrorReporter);
            bool signed = CodeGenUtils.IsSignedType(field.Type);

            RestoreRegFromStack(slot, RegType.r1);
            _asm.EmitCastToType(RegType.r1, size, signed);
            _asm.EmitStoreInd(RegType.r1, RegType.r0, size);
            if (targetReg != RegType.r1) _asm.EmitMov(targetReg, RegType.r1);
            return;
        }

        // x = value
        GenerateExpression(assign.Value, targetReg);
        _varAccessor.StoreFromRegister(assign.Name, targetReg);

        OpCodeSize resSize = OpCodeSize.S64;
        bool resSigned = false;
        if (_funcCtx.VarMap.TryGetValue(assign.Name, out var vloc) && !vloc.IsPointer)
        { resSize = vloc.TypeSize; resSigned = vloc.IsSigned; }
        else if (_globalMem.Contains(assign.Name))
        {
            var g = _globalMem.GetInfo(assign.Name)!.Value;
            if (!g.IsPointer)
            {
                resSize = CodeGenUtils.GetSizeForType(g.Type, _asm.ErrorReporter);
                resSigned = g.IsSigned;
            }
        }
        _asm.EmitCastToType(targetReg, resSize, resSigned);
    }

    // ------------------------------------------------------------
    // Compound: arr[i] op= rv, *p op= rv, obj.f op= rv, x op= rv
    // ------------------------------------------------------------
    private void GenerateCompoundAssignmentExpr(AssignmentNode assign, RegType targetReg)
    {
        string baseOp = assign.CompoundOp!.TrimEnd('=');

        if (assign.IndexExpr != null)
        {
            OpCodeSize size = GetIndexedElemSize(assign.Name);
            bool signed = IsArrayElemSigned(assign.Name);
            GenerateCompoundIndirectExpr(
                reg => ComputeArrayElementAddress(assign.Name, assign.IndexExpr, reg),
                size, signed, baseOp, assign.Value, targetReg);
            return;
        }

        if (assign.LValue is DereferenceNode deref)
        {
            OpCodeSize size = GetPointedSize(deref.Operand);
            bool signed = IsPointedSigned(deref.Operand);
            GenerateCompoundIndirectExpr(
                reg => GenerateExpression(deref.Operand, reg),
                size, signed, baseOp, assign.Value, targetReg);
            return;
        }

        if (assign.LValue is MemberAccessNode member)
        {
            var (_, field, _) = ResolveMemberAccessType(member);
            OpCodeSize size = CodeGenUtils.GetSizeForType(field.Type, _asm.ErrorReporter);
            bool signed = CodeGenUtils.IsSignedType(field.Type);
            GenerateCompoundIndirectExpr(
                reg => GenerateMemberAddress(member, reg),
                size, signed, baseOp, assign.Value, targetReg);
            return;
        }

        GenerateCompoundIdentExpr(assign.Name, baseOp, assign.Value, targetReg);
    }

    /// <summary>
    /// addrReg = computeAddress(); old = [addr]; v = rvalue; [addr] = old op v; targetReg = new.
    /// </summary>
    private void GenerateCompoundIndirectExpr(
        Action<RegType> computeAddress, OpCodeSize size, bool isSigned,
        string baseOp, ASTNode rvalue, RegType targetReg)
    {
        RegType addrReg = (targetReg != RegType.r1) ? RegType.r1 : RegType.r2;
        computeAddress(addrReg);
        int addrSlot = SaveRegToStack(addrReg);

        _asm.EmitLoadInd(RegType.r0, addrReg, size);
        _asm.EmitCastToType(RegType.r0, size, isSigned);
        int oldSlot = SaveRegToStack(RegType.r0);

        GenerateExpression(rvalue, RegType.r0);

        RestoreRegFromStack(oldSlot, RegType.r1);
        EmitBinaryOperation(baseOp, RegType.r1, RegType.r0);
        _asm.EmitCastToType(RegType.r1, size, isSigned);

        RestoreRegFromStack(addrSlot, RegType.r2);
        _asm.EmitStoreInd(RegType.r1, RegType.r2, size);

        if (targetReg != RegType.r1) _asm.EmitMov(targetReg, RegType.r1);
    }

    private void GenerateCompoundIdentExpr(string name, string baseOp, ASTNode rvalue, RegType targetReg)
    {
        OpCodeSize size = OpCodeSize.S64;
        bool signed = false;
        if (_funcCtx.VarMap.TryGetValue(name, out var loc))
        { size = loc.TypeSize; signed = loc.IsSigned; }
        else if (_globalMem.Contains(name))
        {
            var g = _globalMem.GetInfo(name)!.Value;
            if (!g.IsPointer)
            {
                size = CodeGenUtils.GetSizeForType(g.Type, _asm.ErrorReporter);
                signed = g.IsSigned;
            }
        }

        if (_varAccessor.TryGetRegister(name, out RegType xReg))
        {
            RegType temp = (xReg != RegType.r0) ? RegType.r0 : RegType.r1;
            GenerateExpression(rvalue, temp);
            EmitBinaryOperation(baseOp, xReg, temp);
            _asm.EmitCastToType(xReg, size, signed);
            if (targetReg != xReg) _asm.EmitMov(targetReg, xReg);
            return;
        }

        _varAccessor.LoadToRegister(name, targetReg);
        int slot = SaveRegToStack(targetReg);
        GenerateExpression(rvalue, RegType.r0);
        RestoreRegFromStack(slot, RegType.r1);
        EmitBinaryOperation(baseOp, RegType.r1, RegType.r0);
        _asm.EmitCastToType(RegType.r1, size, signed);
        _varAccessor.StoreFromRegister(name, RegType.r1);
        if (targetReg != RegType.r1) _asm.EmitMov(targetReg, RegType.r1);
    }
}