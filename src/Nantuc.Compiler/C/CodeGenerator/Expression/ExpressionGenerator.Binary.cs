using Nantuc.Common;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Compiler.C.AST;
using Nantuc.Diagnostics;

namespace Nantuc.Compiler.C.CodeGenerator.Expression;

public partial class ExpressionGenerator
{
    // ============================================================
    // Бинарные операции: + - * / % & | ^ << >> == != < > <= >= && ||
    // ============================================================
    private void GenerateBinaryOp(BinaryOpNode binop, RegType targetReg)
    {
        if (CodeGenUtils.IsComparisonOperator(binop.Operator))
        {
            GenerateComparisonAsValue(binop, targetReg);
            return;
        }

        if (binop.Operator == ",")
        {
            GenerateExpression(binop.Left);
            GenerateExpression(binop.Right, targetReg);
            return;
        }

        if (binop.Operator == "&&" || binop.Operator == "||")
        {
            GenerateLogicalAndOr(binop);
            if (targetReg != RegType.r0) _asm.EmitMov(targetReg, RegType.r0);
            return;
        }

        if (binop.Left is NumberNode l && binop.Right is NumberNode r)
        {
            long folded = ComputeConstant(l.Value, r.Value, binop.Operator, _asm.ErrorReporter);
            _asm.EmitLdi(targetReg, (ulong)folded);
            return;
        }

        // Оптимизация: targetReg совпадает с регистром левого операнда
        if (binop.Left is IdentifierNode leftId
            && _varAccessor.TryGetRegister(leftId.Name, out var leftReg)
            && leftReg == targetReg)
        {
            RegType temp = (targetReg != RegType.r1) ? RegType.r1 : RegType.r2;
            GenerateExpression(binop.Right, temp);
            ApplyPointerArithmeticScaling(binop.Operator, binop.Left, binop.Right, targetReg, temp);
            EmitBinaryOperation(binop.Operator, targetReg, temp);
            return;
        }

        // Оптимизация: targetReg совпадает с регистром правого операнда (только для коммутативных)
        if (binop.Right is IdentifierNode rightId
            && _varAccessor.TryGetRegister(rightId.Name, out var rightReg)
            && rightReg == targetReg
            && (binop.Operator == "+" || binop.Operator == "*"))
        {
            RegType temp = (targetReg != RegType.r0) ? RegType.r0 : RegType.r1;
            GenerateExpression(binop.Left, temp);
            ApplyPointerArithmeticScaling(binop.Operator, binop.Left, binop.Right, temp, targetReg);
            EmitBinaryOperation(binop.Operator, targetReg, temp);
            return;
        }

        // Общий случай
        GenerateExpression(binop.Left, targetReg);
        RegType rReg = (targetReg != RegType.r1) ? RegType.r1 : RegType.r2;
        RegType addrReg = RegType.r4;

        int saveOffset = _funcCtx.TotalLocalSize + _funcCtx.ExpressionSaveDepth * 8;
        _asm.EmitLdi(addrReg, (ulong)saveOffset);
        _asm.EmitAdd(addrReg, RegType.rSP);
        _asm.EmitStoreInd(targetReg, addrReg, OpCodeSize.S64);

        _funcCtx.ExpressionSaveDepth++;
        GenerateExpression(binop.Right, rReg);
        _funcCtx.ExpressionSaveDepth--;

        _asm.EmitLdi(addrReg, (ulong)saveOffset);
        _asm.EmitAdd(addrReg, RegType.rSP);
        _asm.EmitLoadInd(targetReg, addrReg, OpCodeSize.S64);

        ApplyPointerArithmeticScaling(binop.Operator, binop.Left, binop.Right, targetReg, rReg);
        EmitBinaryOperation(binop.Operator, targetReg, rReg);
    }

    private void GenerateComparisonAsValue(BinaryOpNode binop, RegType targetReg)
    {
        GenerateExpression(binop.Left, RegType.r0);
        int slot = SaveRegToStack(RegType.r0);
        GenerateExpression(binop.Right, RegType.r0);
        RestoreRegFromStack(slot, RegType.r1);
        _asm.EmitCmp(RegType.r1, RegType.r0);

        string trueLabel = _getLabel();
        string endLabel = _getLabel();

        _asm.EmitLdi(targetReg, 0);
        GenerateComparisonJump(binop.Operator, trueLabel, endLabel);
        _asm.MarkLabel(trueLabel);
        _asm.EmitLdi(targetReg, 1);
        _asm.MarkLabel(endLabel);
    }

    private void ApplyPointerArithmeticScaling(string op, ASTNode left, ASTNode right,
                                               RegType leftReg, RegType rightReg)
    {
        if (op != "+" && op != "-") return;

        bool leftIsPtr = TryGetPointerElemSize(left, out int leftSize);
        bool rightIsPtr = TryGetPointerElemSize(right, out int rightSize);

        if (leftIsPtr && !rightIsPtr)
        {
            if (leftSize > 1) _asm.EmitMultiplyByConstant(rightReg, leftSize);
        }
        else if (rightIsPtr && !leftIsPtr && op == "+")
        {
            if (rightSize > 1) _asm.EmitMultiplyByConstant(leftReg, rightSize);
        }
    }

    private bool TryGetPointerElemSize(ASTNode node, out int size)
    {
        size = 0;
        if (node is not IdentifierNode id) return false;

        if (_funcCtx.VarMap.TryGetValue(id.Name, out var loc))
        {
            if (loc.IsArray)
            {
                size = CodeGenUtils.GetSizeInBytes(loc.TypeSize, _asm.ErrorReporter);
                return true;
            }
            if (loc.IsPointer && loc.PointedType != null)
            {
                size = (int)GetTypeSizeBytes(loc.PointedType);
                return true;
            }
            return false;
        }

        if (_globalMem.Contains(id.Name))
        {
            var g = _globalMem.GetInfo(id.Name)!.Value;
            if (g.IsArray)
            {
                size = CodeGenUtils.GetSizeInBytes(
                    CodeGenUtils.GetSizeForType(g.Type, _asm.ErrorReporter), _asm.ErrorReporter);
                return true;
            }
            if (g.IsPointer && g.PointedType != null)
            {
                size = (int)GetTypeSizeBytes(g.PointedType);
                return true;
            }
        }
        return false;
    }

    internal void EmitBinaryOperation(string op, RegType dest, RegType src)
    {
        switch (op)
        {
            case "+": _asm.EmitAdd(dest, src); break;
            case "-": _asm.EmitSub(dest, src); break;
            case "*": _asm.EmitMultInt(dest, src); break;
            case "/": _asm.EmitDiv(dest, src); break;
            case "&": _asm.EmitAnd(dest, src); break;
            case "|": _asm.EmitOr(dest, src); break;
            case "^": _asm.EmitXOr(dest, src); break;
            case "<<": _asm.EmitShl(dest, src); break;
            case ">>": _asm.EmitShr(dest, src); break;
            case "%": _asm.EmitMod(dest, src); break;
            default:
                _asm.ErrorReporter.Throw(ErrorCode.NotSupported,
                    $"Unsupported operator in EmitBinaryOperation: {op}");
                break;
        }
    }

    private void GenerateLogicalAndOr(BinaryOpNode binop)
    {
        string endLabel = _getLabel();
        string trueLabel = _getLabel();
        string falseLabel = _getLabel();

        GenerateExpression(binop.Left);
        _asm.EmitInstR(OpCode.TEST, RegType.r0, RegType.r0);
        if (binop.Operator == "&&") _asm.EmitJz(falseLabel); else _asm.EmitJnz(trueLabel);

        GenerateExpression(binop.Right);
        _asm.EmitInstR(OpCode.TEST, RegType.r0, RegType.r0);
        if (binop.Operator == "&&") _asm.EmitJz(falseLabel); else _asm.EmitJnz(trueLabel);

        _asm.EmitLdi(RegType.r0, binop.Operator == "&&" ? 1UL : 0UL);
        _asm.EmitJmp(endLabel);

        _asm.MarkLabel(trueLabel);
        _asm.EmitLdi(RegType.r0, 1);
        _asm.EmitJmp(endLabel);

        _asm.MarkLabel(falseLabel);
        _asm.EmitLdi(RegType.r0, 0);

        _asm.MarkLabel(endLabel);
    }

    public static long ComputeConstant(long value1, long value2, string op, ErrorReporter errorReporter)
    {
        return op switch
        {
            "+" => value1 + value2,
            "-" => value1 - value2,
            "*" => value1 * value2,
            "/" => ComputeDiv(value1, value2, errorReporter),
            "%" => ComputeMod(value1, value2, errorReporter),
            "&" => value1 & value2,
            "|" => value1 | value2,
            "^" => value1 ^ value2,
            "<<" => value1 << (int)value2,
            ">>" => value1 >> (int)value2,
            _ => errorReporter.Throw<long>(ErrorCode.NotSupported, $"Unsupported operator: {op}"),
        };
    }

    private static long ComputeDiv(long a, long b, ErrorReporter er) =>
        b != 0 ? a / b : er.Throw<long>(ErrorCode.NotSupported, "Div on zero");

    private static long ComputeMod(long a, long b, ErrorReporter er) =>
        b != 0 ? a % b : er.Throw<long>(ErrorCode.NotSupported, "Mod on zero");
}