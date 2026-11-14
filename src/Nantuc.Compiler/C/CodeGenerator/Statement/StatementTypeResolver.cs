using Nantuc.Common;
using Nantuc.Compiler.ASM.Base;
using Nantuc.Compiler.C.AST;
using Nantuc.Compiler.C.CodeGenerator.Expression;
using Nantuc.Diagnostics;

namespace Nantuc.Compiler.C.CodeGenerator.Statement;

// ============================================================
// Type resolution для LValue-форм: arr[i], *p, obj.f / p->f / a.b.c.
// Собирает в одном месте логику, ранее размазанную по
// StatementGenerator (и частично дублировавшую ExpressionGenerator).
// ============================================================
public sealed class StatementTypeResolver(
    AssemblerBase asm,
    FunctionContext funcCtx,
    GlobalMemoryManager globalMem,
    Dictionary<string, StructLayout> structTable,
    ExpressionGenerator exprGen)
{
    private readonly AssemblerBase _asm = asm;
    private readonly FunctionContext _funcCtx = funcCtx;
    private readonly GlobalMemoryManager _globalMem = globalMem;
    private readonly Dictionary<string, StructLayout> _structTable = structTable;
    private readonly ExpressionGenerator _exprGen = exprGen;

    // ---------- Массив ----------
    public OpCodeSize GetArrayElemSize(string arrayName) => GetArrayElem(arrayName).size;

    public bool IsArrayElemSigned(string arrayName) => GetArrayElem(arrayName).isSigned;

    public (OpCodeSize size, bool isSigned) GetArrayElem(string arrayName)
    {
        if (_funcCtx.VarMap.TryGetValue(arrayName, out var loc))
        {
            if (loc.IsArray) return (loc.TypeSize, loc.IsSigned);
            if (loc.IsPointer && loc.PointedType != null)
                return (CodeGenUtils.GetSizeForType(loc.PointedType, _asm.ErrorReporter),
                        CodeGenUtils.IsSignedType(loc.PointedType));
        }
        else if (_globalMem.Contains(arrayName))
        {
            var g = _globalMem.GetInfo(arrayName)!.Value;
            if (g.IsArray)
            {
                var sz = g.IsPointer ? OpCodeSize.S64
                                     : CodeGenUtils.GetSizeForType(g.Type, _asm.ErrorReporter);
                return (sz, !g.IsPointer && CodeGenUtils.IsSignedType(g.Type));
            }
            if (g.IsPointer && g.PointedType != null)
                return (CodeGenUtils.GetSizeForType(g.PointedType, _asm.ErrorReporter),
                        CodeGenUtils.IsSignedType(g.PointedType));
        }
        _asm.ErrorReporter.Throw(ErrorCode.CodeGen_NonIndexableType, arrayName);
        return default;
    }

    // ---------- Указатель ----------
    public OpCodeSize GetPointedSize(ASTNode expr) => GetPointed(expr).size;

    public bool IsPointedSigned(ASTNode expr) => GetPointed(expr).isSigned;

    public (OpCodeSize size, bool isSigned) GetPointed(ASTNode expr)
    {
        if (expr is IdentifierNode id)
        {
            if (_funcCtx.VarMap.TryGetValue(id.Name, out var loc) && loc.IsPointer)
                return (CodeGenUtils.GetSizeForType(loc.PointedType!, _asm.ErrorReporter),
                        CodeGenUtils.IsSignedType(loc.PointedType!));
            if (_globalMem.Contains(id.Name))
            {
                var g = _globalMem.GetInfo(id.Name)!.Value;
                if (g.IsPointer)
                    return (CodeGenUtils.GetSizeForType(g.PointedType!, _asm.ErrorReporter),
                            CodeGenUtils.IsSignedType(g.PointedType!));
            }
        }
        _asm.ErrorReporter.Throw(ErrorCode.CodeGen_UnknownPointedType);
        return default;
    }

    // ---------- Поле структуры ----------
    public OpCodeSize GetFieldSize(MemberAccessNode node) => GetField(node).size;

    public bool IsFieldSigned(MemberAccessNode node) => GetField(node).isSigned;

    public (OpCodeSize size, bool isSigned) GetField(MemberAccessNode node)
    {
        var (_, field, _) = ResolveFieldInfo(node);
        return (CodeGenUtils.GetSizeForType(field.Type, _asm.ErrorReporter),
                CodeGenUtils.IsSignedType(field.Type));
    }

    /// <summary>
    /// Полное разрешение цепочки <c>a.b.c</c> / <c>p->f</c> / <c>arr[i].f</c> / <c>(ptr+n)->f</c>.
    /// Возвращает layout родителя, FieldInfo и имя типа поля.
    /// </summary>
    public (StructLayout layout, FieldInfo field, string fieldType) ResolveFieldInfo(MemberAccessNode node)
    {
        var chain = new List<string>();
        MemberAccessNode? current = node;
        ASTNode? rootObject = null;

        while (current != null)
        {
            chain.Add(current.FieldName);
            if (current.Object is MemberAccessNode inner) { current = inner; continue; }
            rootObject = current.Object;
            break;
        }
        chain.Reverse();

        string? rootStructType = ResolveRootStructType(rootObject);
        if (rootStructType == null)
            _asm.ErrorReporter.Throw(ErrorCode.CodeGen_UnknownStructType,
                "Cannot determine struct type for member access");

        StructLayout? currentLayout = null;
        FieldInfo? currentField = null;
        string currentType = rootStructType;

        foreach (var fieldName in chain)
        {
            if (!_structTable.TryGetValue(currentType, out var layout))
                _asm.ErrorReporter.Throw(ErrorCode.CodeGen_UnknownStructType, currentType);
            currentLayout = layout;
            currentField = layout.GetField(fieldName)
                ?? _asm.ErrorReporter.Throw<FieldInfo>(ErrorCode.CodeGen_UnknownField, fieldName, currentType);
            currentType = currentField.Type;
        }

        return (currentLayout!, currentField!, currentType);
    }

    private string? ResolveRootStructType(ASTNode? rootObject)
    {
        switch (rootObject)
        {
            case IdentifierNode id:
                if (_funcCtx.VarMap.TryGetValue(id.Name, out var loc))
                {
                    if (loc.StructTypeName != null) return loc.StructTypeName;
                    if (loc.IsPointer && loc.PointedType != null && _structTable.ContainsKey(loc.PointedType))
                        return loc.PointedType;
                }
                else if (_globalMem.Contains(id.Name))
                {
                    var g = _globalMem.GetInfo(id.Name)!.Value;
                    if (_structTable.ContainsKey(g.Type)) return g.Type;
                    if (g.IsPointer && g.PointedType != null && _structTable.ContainsKey(g.PointedType))
                        return g.PointedType;
                }
                return null;

            case ArrayAccessNode arrAcc:
                return _exprGen.ResolveArrayStructType(arrAcc.ArrayName);

            case BinaryOpNode bin when bin.Operator == "+" || bin.Operator == "-":
                var fromPtr = _exprGen.TryInferStructTypeFromPointerExpr(bin);
                if (fromPtr == null)
                    _asm.ErrorReporter.Throw(ErrorCode.CodeGen_UnknownStructType,
                        "Cannot determine struct type from pointer arithmetic");
                return fromPtr;

            case DereferenceNode:
                _asm.ErrorReporter.Throw(ErrorCode.CodeGen_UnknownStructType,
                    "Dereference member access not implemented yet");
                return null;

            default:
                return null;
        }
    }
}