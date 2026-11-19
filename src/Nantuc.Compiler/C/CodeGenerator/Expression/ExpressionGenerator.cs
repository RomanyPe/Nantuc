using Nantuc.Common;
using Nantuc.Compiler.ASM.Base;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Compiler.C.AST;
using Nantuc.Compiler.C.CodeGenerator.FunctionCtx;
using Nantuc.Diagnostics;

namespace Nantuc.Compiler.C.CodeGenerator.Expression;

// ============================================================
// ExpressionGenerator — генерация выражений.
// Partial-класс разбит по зонам ответственности:
//   - ExpressionGenerator.cs             — диспетчеризация (этот файл)
//   - ExpressionGenerator.Binary.cs      — бинарные операторы, сравнения, &&/||
//   - ExpressionGenerator.Unary.cs       — унарные операторы
//   - ExpressionGenerator.Assignment.cs  — присваивания (в т.ч. compound)
//   - ExpressionGenerator.Increment.cs   — ++/--
//   - ExpressionGenerator.Arrays.cs      — индексация массивов / указателей
//   - ExpressionGenerator.MemberAccess.cs — доступ к полям структур
//   - ExpressionGenerator.Calls.cs       — вызовы функций (прямые и косвенные)
//   - ExpressionGenerator.Sizeof.cs      — sizeof(type) / sizeof(expr)
//   - ExpressionGenerator.Conditions.cs  — генерация условий переходов
//   - ExpressionGenerator.PointerOps.cs  — & и * , работа с указателями
//   - ExpressionGenerator.Helpers.cs     — общие утилиты (стек, размеры)
// ============================================================
public partial class ExpressionGenerator(AssemblerBase asm,
                                         GlobalMemoryManager globalMem,
                                         FunctionContext funcCtx,
                                         Dictionary<string, FunctionNode> functionTable,
                                         Func<string> getLabel,
                                         Dictionary<string, StructLayout> structTable,
                                         VariableAccessor varAccessor)
{
    private readonly AssemblerBase _asm = asm;
    private readonly GlobalMemoryManager _globalMem = globalMem;
    private readonly FunctionContext _funcCtx = funcCtx;
    private readonly Dictionary<string, FunctionNode> _functionTable = functionTable;
    private readonly Dictionary<string, StructLayout> _structTable = structTable;
    private readonly Func<string> _getLabel = getLabel;
    private readonly VariableAccessor _varAccessor = varAccessor;

    public void GenerateExpression(ASTNode expr, RegType targetReg, bool mayClobberTarget)
    {
        if (mayClobberTarget && expr is IdentifierNode id
            && _varAccessor.TryGetRegister(id.Name, out var idReg)
            && _funcCtx.Allocation.DeadAfterUse.Contains(id)
            && idReg != targetReg)
        {
            // Переменная мертва после этого чтения — можно затереть её home-регистр
            // и использовать его как аккумулятор
            _asm.EmitMov(targetReg, idReg);
            return;
        }
        GenerateExpression(expr, targetReg);
    }
    public void GenerateExpression(ASTNode expr)
    {
        switch (expr)
        {
            case NumberNode num: _asm.EmitLdi(RegType.r0, (ulong)num.Value); break;
            case IdentifierNode id: EmitIdentifierToReg(id, RegType.r0); break;
            case BinaryOpNode binop: GenerateBinaryOp(binop, RegType.r0); break;
            case UnaryOpNode unop: GenerateUnaryOp(unop, RegType.r0); break;
            case FunctionCallNode call: GenerateFunctionCall(call); break;
            case ArrayAccessNode arr: LoadArrayElementToR0(arr.ArrayName, arr.Index, RegType.r0); break;
            case AddressOfNode addrOf: GenerateAddressOf(addrOf, RegType.r0); break;
            case MemberAccessNode member: GenerateMemberAccess(member, RegType.r0); break;
            case DereferenceNode deref: GenerateDereference(deref, RegType.r0); break;
            case AssignmentNode assign: GenerateAssignmentExpr(assign, RegType.r0); break;
            case IncrementNode inc: GenerateIncrement(inc, RegType.r0); break;
            case SizeofTypeNode st: GenerateSizeofType(st, RegType.r0); break;
            case SizeofExprNode se: GenerateSizeofExpr(se, RegType.r0); break;
            case IndirectCallNode ic: GenerateIndirectCall(ic, RegType.r0); break;
            case CastNode ca: GenerateCast(ca, RegType.r0); break;
            default:
                _asm.ErrorReporter.Throw(ErrorCode.NotSupported,
                    DiagnosticData.Of($"Unsupported expression: {expr.NameNode}"));
                break;
        }
    }

    public void GenerateExpression(ASTNode expr, RegType targetReg)
    {
        if (targetReg == RegType.r0) { GenerateExpression(expr); return; }

        switch (expr)
        {
            case NumberNode num: _asm.EmitLdi(targetReg, (ulong)num.Value); break;
            case IdentifierNode id: EmitIdentifierToReg(id, targetReg); break;
            case BinaryOpNode binop: GenerateBinaryOp(binop, targetReg); break;
            case UnaryOpNode unop: GenerateUnaryOp(unop, targetReg); break;
            case ArrayAccessNode arr: LoadArrayElementToR0(arr.ArrayName, arr.Index, targetReg); break;
            case AssignmentNode assign: GenerateAssignmentExpr(assign, targetReg); break;
            case DereferenceNode deref: GenerateDereference(deref, targetReg); break;
            case AddressOfNode addrOf: GenerateAddressOf(addrOf, targetReg); break;
            case MemberAccessNode member: GenerateMemberAccess(member, targetReg); break;
            case IncrementNode inc: GenerateIncrement(inc, targetReg); break;
            case SizeofTypeNode st: GenerateSizeofType(st, targetReg); break;
            case SizeofExprNode se: GenerateSizeofExpr(se, targetReg); break;
            case IndirectCallNode ic: GenerateIndirectCall(ic, targetReg); break;
            case CastNode ca: GenerateCast(ca, targetReg); break;
            default:
                GenerateExpression(expr);
                if (targetReg != RegType.r0) _asm.EmitMov(targetReg, RegType.r0);
                break;
        }
    }

    private void EmitIdentifierToReg(IdentifierNode id, RegType reg)
    {
        if (_functionTable.ContainsKey(id.Name))
            _asm.EmitLdi(reg, $"func_{id.Name}");
        else if (IsArrayIdentifier(id.Name))
            _varAccessor.LoadAddressToRegister(id.Name, reg);
        else
            _varAccessor.LoadToRegister(id.Name, reg);
    }

    private bool IsArrayIdentifier(string name)
    {
        if (_funcCtx.VarMap.TryGetValue(name, out var loc)) return loc.IsArray;
        if (_globalMem.Contains(name)) return _globalMem.GetInfo(name)!.Value.IsArray;
        return false;
    }
}