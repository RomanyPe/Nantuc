using Nantuc.Common;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Compiler.C.AST;
using Nantuc.Diagnostics;

namespace Nantuc.Compiler.C.CodeGenerator.Expression;

public partial class ExpressionGenerator
{
    // ============================================================
    // Доступ к полям структур: obj.f, ptr->f, arr[i].f, a.b.c
    // ============================================================

    public void GenerateMemberAddress(MemberAccessNode node, RegType targetReg)
    {
        // 1) база
        switch (node.Object)
        {
            case IdentifierNode identifier:
                if (node.IsArrow) _varAccessor.LoadToRegister(identifier.Name, targetReg);
                else _varAccessor.LoadAddressToRegister(identifier.Name, targetReg);
                break;

            case MemberAccessNode inner: GenerateMemberAddress(inner, targetReg); break;
            case ArrayAccessNode arrAcc: ComputeArrayElementAddress(arrAcc.ArrayName, arrAcc.Index, targetReg); break;
            case DereferenceNode:
                _asm.ErrorReporter.Throw(ErrorCode.CodeGen_UnknownStructType,
                    DiagnosticData.Of("Dereference member access not implemented yet"));
                return;
            default: GenerateExpression(node.Object, targetReg); break;
        }

        // 2) + offset поля
        var (_, field, _) = ResolveMemberAccessType(node);
        if (field.Offset > 0)
        {
            _asm.EmitLdi(CodeGenUtils.ADDR_SCRATCH_REG_EN, (ulong)field.Offset);
            _asm.EmitAdd(targetReg, CodeGenUtils.ADDR_SCRATCH_REG_EN);
        }
    }

    private void GenerateMemberAccess(MemberAccessNode node, RegType targetReg)
    {
        string? structType = null;
        switch (node.Object)
        {
            case IdentifierNode id:
                structType = ResolveIdentifierStructType(id.Name);
                break;

            case MemberAccessNode:
                // Вложенный доступ: сначала вычислим адрес поля, потом загрузим значение
                GenerateMemberAddress(node, targetReg);
                var (_, fieldMem, _) = ResolveMemberAccessType(node);
                OpCodeSize fieldSize = CodeGenUtils.GetSizeForType(fieldMem.Type, _asm.ErrorReporter);
                _asm.EmitLoadInd(targetReg, targetReg, fieldSize);
                _asm.EmitCastToType(targetReg, fieldSize, CodeGenUtils.IsSignedType(fieldMem.Type));
                return;

            case DereferenceNode:
                _asm.ErrorReporter.Throw(ErrorCode.CodeGen_UnknownStructType,
                    DiagnosticData.Of("Dereference member access not implemented yet"));
                return;

            case ArrayAccessNode arrAcc:
                string? arrStructType = ResolveArrayStructType(arrAcc.ArrayName);
                var layoutArr = _structTable[arrStructType!];
                var fieldArr = layoutArr.GetField(node.FieldName)
                    ?? _asm.ErrorReporter.Throw<FieldInfo>(ErrorCode.CodeGen_UnknownField,
                        DiagnosticData.Of(node.FieldName), DiagnosticData.Of(arrStructType!));

                ComputeArrayElementAddress(arrAcc.ArrayName, arrAcc.Index, targetReg);
                if (fieldArr.Offset > 0)
                {
                    _asm.EmitLdi(CodeGenUtils.ADDR_SCRATCH_REG_EN, (ulong)fieldArr.Offset);
                    _asm.EmitAdd(targetReg, CodeGenUtils.ADDR_SCRATCH_REG_EN);
                }
                OpCodeSize fs = CodeGenUtils.GetSizeForType(fieldArr.Type, _asm.ErrorReporter);
                _asm.EmitLoadInd(targetReg, targetReg, fs);
                _asm.EmitCastToType(targetReg, fs, CodeGenUtils.IsSignedType(fieldArr.Type));
                return;

            default:
                if (node.IsArrow)
                {
                    GenerateMemberAddress(node, targetReg);
                    var (_, f, _) = ResolveMemberAccessType(node);
                    OpCodeSize sz = CodeGenUtils.GetSizeForType(f.Type, _asm.ErrorReporter);
                    _asm.EmitLoadInd(targetReg, targetReg, sz);
                    _asm.EmitCastToType(targetReg, sz, CodeGenUtils.IsSignedType(f.Type));
                    return;
                }
                _asm.ErrorReporter.Throw(ErrorCode.NotSupported, DiagnosticData.Of("Unsupported object for member access"));
                break;
        }

        if (structType == null)
            _asm.ErrorReporter.Throw(ErrorCode.CodeGen_UnknownStructType,
                DiagnosticData.Of("Cannot determine struct type for member access"));

        var layout = _structTable[structType];
        var field = layout.GetField(node.FieldName)
            ?? _asm.ErrorReporter.Throw<FieldInfo>(ErrorCode.CodeGen_UnknownField, 
            DiagnosticData.Of(node.FieldName), 
            DiagnosticData.Of(structType));

        if (node.IsArrow) GenerateExpression(node.Object, targetReg);
        else if (node.Object is IdentifierNode ident) _varAccessor.LoadAddressToRegister(ident.Name, targetReg);
        else _asm.ErrorReporter.Throw(ErrorCode.NotSupported,
            DiagnosticData.Of("Dot access only supported for simple variables currently"));

        if (field.Offset > 0)
        {
            _asm.EmitLdi(CodeGenUtils.ADDR_SCRATCH_REG_EN, (ulong)field.Offset);
            _asm.EmitAdd(targetReg, CodeGenUtils.ADDR_SCRATCH_REG_EN);
        }

        OpCodeSize fieldSizeFinal = CodeGenUtils.GetSizeForType(field.Type, _asm.ErrorReporter);
        _asm.EmitLoadInd(targetReg, targetReg, fieldSizeFinal);
        _asm.EmitCastToType(targetReg, fieldSizeFinal, CodeGenUtils.IsSignedType(field.Type));
    }

    private string? ResolveIdentifierStructType(string name)
    {
        if (_funcCtx.VarMap.TryGetValue(name, out var loc))
        {
            if (loc.StructTypeName != null) return loc.StructTypeName;
            if (loc.IsPointer && loc.PointedType != null && _structTable.ContainsKey(loc.PointedType))
                return loc.PointedType;
        }
        else if (_globalMem.Contains(name))
        {
            var g = _globalMem.GetInfo(name)!.Value;
            if (_structTable.ContainsKey(g.Type)) return g.Type;
            if (g.IsPointer && g.PointedType != null && _structTable.ContainsKey(g.PointedType))
                return g.PointedType;
        }
        _asm.ErrorReporter.Throw(ErrorCode.CodeGen_NonStructAccess, DiagnosticData.Of(name));
        return null;
    }

    private (StructLayout layout, FieldInfo field, string fieldType) ResolveMemberAccessType(MemberAccessNode node)
    {
        string? structType = node.Object switch
        {
            IdentifierNode id => ResolveIdentifierStructType(id.Name),
            MemberAccessNode inner => ResolveMemberAccessType(inner).fieldType,
            ArrayAccessNode arrAcc => ResolveArrayStructType(arrAcc.ArrayName),
            DereferenceNode => _asm.ErrorReporter.Throw<string>(
                ErrorCode.CodeGen_UnknownStructType, DiagnosticData.Of("Dereference in member access not yet supported")),
            BinaryOpNode bin when bin.Operator == "+" || bin.Operator == "-" =>
                TryInferStructTypeFromPointerExpr(bin) ?? _asm.ErrorReporter.Throw<string>(
                    ErrorCode.CodeGen_UnknownStructType, DiagnosticData.Of("Cannot determine struct type from pointer arithmetic")),
            _ => null,
        };

        if (structType == null)
            _asm.ErrorReporter.Throw(ErrorCode.CodeGen_UnknownStructType,
                DiagnosticData.Of("Cannot determine struct type for member access"));

        if (!_structTable.TryGetValue(structType, out var layout))
            _asm.ErrorReporter.Throw(ErrorCode.NotImplemented, DiagnosticData.Of(structType));

        var field = layout.GetField(node.FieldName)
            ?? _asm.ErrorReporter.Throw<FieldInfo>(ErrorCode.CodeGen_UnknownField, 
            DiagnosticData.Of(node.FieldName), 
            DiagnosticData.Of(structType));

        return (layout, field, field.Type);
    }

    public string? TryInferStructTypeFromPointerExpr(ASTNode expr)
    {
        switch (expr)
        {
            case IdentifierNode id:
                if (_funcCtx.VarMap.TryGetValue(id.Name, out var loc))
                {
                    if (loc.StructTypeName != null) return loc.StructTypeName;
                    if (loc.IsPointer && loc.PointedType != null && _structTable.ContainsKey(loc.PointedType))
                        return loc.PointedType;
                }
                if (_globalMem.Contains(id.Name))
                {
                    var g = _globalMem.GetInfo(id.Name)!.Value;
                    if (_structTable.ContainsKey(g.Type)) return g.Type;
                    if (g.IsPointer && g.PointedType != null && _structTable.ContainsKey(g.PointedType))
                        return g.PointedType;
                }
                return null;
            case BinaryOpNode bin when bin.Operator == "+" || bin.Operator == "-":
                return TryInferStructTypeFromPointerExpr(bin.Left)
                    ?? TryInferStructTypeFromPointerExpr(bin.Right);
            case ArrayAccessNode arrAcc:
                return ResolveArrayStructType(arrAcc.ArrayName);
            default:
                return null;
        }
    }

    public string? ResolveArrayStructType(string arrayName)
    {
        if (_funcCtx.VarMap.TryGetValue(arrayName, out var loc))
        {
            if (loc.IsPointer && loc.PointedType != null && _structTable.ContainsKey(loc.PointedType))
                return loc.PointedType;
            if (loc.StructTypeName != null) return loc.StructTypeName;
        }
        else if (_globalMem.Contains(arrayName))
        {
            var g = _globalMem.GetInfo(arrayName)!.Value;
            if (g.IsPointer && g.PointedType != null && _structTable.ContainsKey(g.PointedType))
                return g.PointedType;
            if (_structTable.ContainsKey(g.Type)) return g.Type;
        }
        _asm.ErrorReporter.Throw(ErrorCode.CodeGen_NonIndexableType, DiagnosticData.Of(arrayName));
        return null;
    }

    private long GetMemberTypeSize(MemberAccessNode mem)
    {
        var (_, field, _) = ResolveMemberAccessType(mem);
        return GetTypeSizeBytes(field.Type);
    }
}