using Nantuc.Common;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Compiler.C.AST;
using Nantuc.Diagnostics;

namespace Nantuc.Compiler.C.CodeGenerator.Expression;

public partial class ExpressionGenerator
{
    // ============================================================
    // sizeof(type) / sizeof(expr)
    // ============================================================
    private void GenerateSizeofType(SizeofTypeNode node, RegType targetReg)
    {
        long size;
        if (node.IsPointer) size = 8;
        else if (_structTable.TryGetValue(node.TypeName, out var layout)) size = layout.Size;
        else if (CodeGenUtils.IsPrimitiveType(node.TypeName))
            size = CodeGenUtils.GetSizeInBytes(
                CodeGenUtils.GetSizeForType(node.TypeName, _asm.ErrorReporter), _asm.ErrorReporter);
        else
        {
            _asm.ErrorReporter.Throw(ErrorCode.CodeGen_UnknownTypeSize, node.TypeName);
            return;
        }
        _asm.EmitLdi(targetReg, (ulong)size);
    }

    private void GenerateSizeofExpr(SizeofExprNode node, RegType targetReg)
        => _asm.EmitLdi(targetReg, (ulong)InferExprTypeSize(node.Operand));

    private long InferExprTypeSize(ASTNode expr) => expr switch
    {
        IdentifierNode id => GetVariableTypeSize(id.Name),
        DereferenceNode deref => GetPointedTypeSize(deref.Operand),
        ArrayAccessNode arr => GetIndexedElemSizeBytes(arr.ArrayName),
        MemberAccessNode mem => GetMemberTypeSize(mem),
        NumberNode => 4,   // int
        _ => _asm.ErrorReporter.Throw<long>(ErrorCode.NotSupported,
                 $"sizeof of {expr.NameNode} not supported"),
    };

    private long GetVariableTypeSize(string name)
    {
        if (_funcCtx.VarMap.TryGetValue(name, out var loc))
        {
            long elem = loc.IsPointer ? 8
                      : loc.StructTypeName != null && _structTable.TryGetValue(loc.StructTypeName, out var l)
                          ? l.Size
                          : CodeGenUtils.GetSizeInBytes(loc.TypeSize, _asm.ErrorReporter);
            return loc.IsArray ? elem * loc.ArraySize : elem;
        }

        if (_globalMem.Contains(name))
        {
            var g = _globalMem.GetInfo(name)!.Value;
            long elem = g.IsPointer ? 8
                      : _structTable.TryGetValue(g.Type, out var l) ? l.Size
                      : CodeGenUtils.GetSizeInBytes(CodeGenUtils.GetSizeForType(g.Type, _asm.ErrorReporter), _asm.ErrorReporter);
            return g.IsArray ? elem * g.ArraySize : elem;
        }

        _asm.ErrorReporter.Throw(ErrorCode.CodeGen_UndefinedVariable, name);
        return 0;
    }

    private long GetPointedTypeSize(ASTNode operand)
    {
        if (operand is IdentifierNode id)
        {
            if (_funcCtx.VarMap.TryGetValue(id.Name, out var loc)
                && loc.IsPointer && loc.PointedType != null)
                return GetTypeSizeBytes(loc.PointedType);

            if (_globalMem.Contains(id.Name))
            {
                var g = _globalMem.GetInfo(id.Name)!.Value;
                if (g.IsPointer && g.PointedType != null) return GetTypeSizeBytes(g.PointedType);
            }
        }
        _asm.ErrorReporter.Throw(ErrorCode.CodeGen_UnknownPointedType);
        return 0;
    }

    private long GetIndexedElemSizeBytes(string arrayName)
    {
        if (_funcCtx.VarMap.TryGetValue(arrayName, out var loc))
        {
            if (loc.IsArray)
                return loc.StructTypeName != null && _structTable.TryGetValue(loc.StructTypeName, out var l)
                    ? l.Size
                    : CodeGenUtils.GetSizeInBytes(loc.TypeSize, _asm.ErrorReporter);
            if (loc.IsPointer && loc.PointedType != null) return GetTypeSizeBytes(loc.PointedType);
        }
        else if (_globalMem.Contains(arrayName))
        {
            var g = _globalMem.GetInfo(arrayName)!.Value;
            if (g.IsArray)
                return _structTable.TryGetValue(g.Type, out var l) ? l.Size
                    : CodeGenUtils.GetSizeInBytes(
                        CodeGenUtils.GetSizeForType(g.Type, _asm.ErrorReporter), _asm.ErrorReporter);
            if (g.IsPointer && g.PointedType != null) return GetTypeSizeBytes(g.PointedType);
        }
        _asm.ErrorReporter.Throw(ErrorCode.CodeGen_NonIndexableType, arrayName);
        return 0;
    }
}