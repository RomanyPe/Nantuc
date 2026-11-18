using Nantuc.Common;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Compiler.C.AST;
using Nantuc.Diagnostics;

namespace Nantuc.Compiler.C.CodeGenerator.Expression;

public partial class ExpressionGenerator
{
    // ============================================================
    // ++x / x++ / --x / x-- (идентификатор, arr[i], *p, obj.f)
    // ============================================================
    internal void GenerateIncrement(IncrementNode node, RegType targetReg)
    {
        // Проверка константности
        switch (node.Target)
        {
            case IdentifierNode id when _varAccessor.IsConst(id.Name):
                _asm.ErrorReporter.Throw(ErrorCode.CodeGen_IncrementOfConst, DiagnosticData.Of(id.Name));
                break;
            case ArrayAccessNode arr when _varAccessor.IsPointedConst(arr.ArrayName):
                _asm.ErrorReporter.Throw(ErrorCode.CodeGen_IncrementOfConst, DiagnosticData.Of(arr.ArrayName));
                break;
            case DereferenceNode dr when dr.Operand is IdentifierNode p && _varAccessor.IsPointedConst(p.Name):
                _asm.ErrorReporter.Throw(ErrorCode.CodeGen_IncrementOfConst, DiagnosticData.Of(p.Name));
                break;
        }

        switch (node.Target)
        {
            case IdentifierNode id: GenerateIncrementIdent(id.Name, node.Delta, node.IsPrefix, targetReg); break;
            case ArrayAccessNode arrAcc: GenerateIncrementArray(arrAcc, node.Delta, node.IsPrefix, targetReg); break;
            case DereferenceNode deref: GenerateIncrementDeref(deref, node.Delta, node.IsPrefix, targetReg); break;
            case MemberAccessNode member: GenerateIncrementMember(member, node.Delta, node.IsPrefix, targetReg); break;
            default:
                _asm.ErrorReporter.Throw(ErrorCode.NotSupported,
                    DiagnosticData.Of($"Increment target must be lvalue: {node.Target.NameNode}"));
                break;
        }
    }

    private void GenerateIncrementIdent(string name, int delta, bool isPrefix, RegType targetReg)
    {
        long step = GetIncrementStepBytes(name);

        OpCodeSize typeSize = OpCodeSize.S64;
        bool isSigned = false;
        if (_funcCtx.VarMap.TryGetValue(name, out var loc))
        { typeSize = loc.TypeSize; isSigned = loc.IsSigned; }
        else if (_globalMem.Contains(name))
        {
            var g = _globalMem.GetInfo(name)!.Value;
            if (!g.IsPointer)
            {
                typeSize = CodeGenUtils.GetSizeForType(g.Type, _asm.ErrorReporter);
                isSigned = g.IsSigned;
            }
        }

        // Fast path: переменная живёт в регистре
        if (_varAccessor.TryGetRegister(name, out RegType xReg))
        {
            if (!isPrefix) _asm.EmitMov(targetReg, xReg);
            EmitIncrementStep(xReg, delta, step, avoid1: targetReg);
            _asm.EmitCastToType(xReg, typeSize, isSigned);
            if (isPrefix && targetReg != xReg) _asm.EmitMov(targetReg, xReg);
            return;
        }

        // Slow path: через память
        _varAccessor.LoadToRegister(name, targetReg);
        if (!isPrefix)
        {
            RegType temp = (targetReg != RegType.r0) ? RegType.r0 : RegType.r1;
            _asm.EmitMov(temp, targetReg);
            EmitIncrementStep(temp, delta, step, avoid1: targetReg);
            _varAccessor.StoreFromRegister(name, temp);
        }
        else
        {
            EmitIncrementStep(targetReg, delta, step, avoid1: targetReg);
            _asm.EmitCastToType(targetReg, typeSize, isSigned);
            _varAccessor.StoreFromRegister(name, targetReg);
        }
    }

    private void GenerateIncrementArray(ArrayAccessNode arrAcc, int delta, bool isPrefix, RegType targetReg)
        => GenerateIncrementIndirect(
            reg => ComputeArrayElementAddress(arrAcc.ArrayName, arrAcc.Index, reg),
            GetIndexedElemSize(arrAcc.ArrayName),
            IsArrayElemSigned(arrAcc.ArrayName),
            delta, isPrefix, targetReg);

    private void GenerateIncrementDeref(DereferenceNode deref, int delta, bool isPrefix, RegType targetReg)
        => GenerateIncrementIndirect(
            reg => GenerateExpression(deref.Operand, reg),
            GetPointedSize(deref.Operand),
            IsPointedSigned(deref.Operand),
            delta, isPrefix, targetReg);

    private void GenerateIncrementMember(MemberAccessNode member, int delta, bool isPrefix, RegType targetReg)
    {
        var (_, field, _) = ResolveMemberAccessType(member);
        GenerateIncrementIndirect(
            reg => GenerateMemberAddress(member, reg),
            CodeGenUtils.GetSizeForType(field.Type, _asm.ErrorReporter),
            CodeGenUtils.IsSignedType(field.Type),
            delta, isPrefix, targetReg);
    }

    /// <summary>
    /// Универсальный эмиттер ++/-- через адрес:
    /// computeAddress(addrReg); old = [addr]; new = old ± step; [addr] = new; targetReg = old/new.
    /// </summary>
    private void GenerateIncrementIndirect(
        Action<RegType> computeAddress, OpCodeSize size, bool isSigned,
        int delta, bool isPrefix, RegType targetReg)
    {
        RegType addrReg = (targetReg != RegType.r2) ? RegType.r2 : RegType.r3;

        computeAddress(addrReg);
        int addrSlot = SaveRegToStack(addrReg);
        _asm.EmitLoadInd(targetReg, addrReg, size);
        _asm.EmitCastToType(targetReg, size, isSigned);

        if (!isPrefix)
        {
            RegType temp = (targetReg != RegType.r0 && addrReg != RegType.r0) ? RegType.r0 : RegType.r1;
            _asm.EmitMov(temp, targetReg);
            if (delta > 0) _asm.EmitInc(temp); else _asm.EmitDec(temp);
            _asm.EmitCastToType(temp, size, isSigned);

            RestoreRegFromStack(addrSlot, addrReg);
            _asm.EmitStoreInd(temp, addrReg, size);
        }
        else
        {
            if (delta > 0) _asm.EmitInc(targetReg); else _asm.EmitDec(targetReg);
            _asm.EmitCastToType(targetReg, size, isSigned);
            RestoreRegFromStack(addrSlot, addrReg);
            _asm.EmitStoreInd(targetReg, addrReg, size);
        }
    }

    private void EmitIncrementStep(RegType reg, int delta, long step, RegType avoid1)
    {
        if (step == 1)
        {
            if (delta > 0) _asm.EmitInc(reg); else _asm.EmitDec(reg);
            return;
        }

        RegType scratch =
            reg != RegType.r0 && avoid1 != RegType.r0 ? RegType.r0 :
            reg != RegType.r1 && avoid1 != RegType.r1 ? RegType.r1 : RegType.r2;

        _asm.EmitLdi(scratch, (ulong)step);
        if (delta > 0) _asm.EmitAdd(reg, scratch); else _asm.EmitSub(reg, scratch);
    }

    private long GetIncrementStepBytes(string name)
    {
        if (_funcCtx.VarMap.TryGetValue(name, out var loc))
            return loc.IsPointer && loc.PointedType != null ? GetTypeSizeBytes(loc.PointedType) : 1;

        if (_globalMem.Contains(name))
        {
            var g = _globalMem.GetInfo(name)!.Value;
            if (g.IsPointer && g.PointedType != null) return GetTypeSizeBytes(g.PointedType);
        }
        return 1;
    }
}