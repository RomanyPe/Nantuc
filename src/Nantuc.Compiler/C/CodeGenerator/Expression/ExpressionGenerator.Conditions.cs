using Nantuc.Common;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Compiler.C.AST;

namespace Nantuc.Compiler.C.CodeGenerator.Expression;

public partial class ExpressionGenerator
{
    // ============================================================
    // Генерация условий (для if/while/for/тернарного оператора)
    // ============================================================
    public void GenerateCondition(ASTNode condition, string? trueLabel, string? falseLabel)
    {
        if (condition is BinaryOpNode logical && (logical.Operator == "&&" || logical.Operator == "||"))
        {
            if (logical.Operator == "&&")
            {
                GenerateCondition(logical.Left, null, falseLabel);
                GenerateCondition(logical.Right, trueLabel, falseLabel);
            }
            else
            {
                GenerateCondition(logical.Left, trueLabel, null);
                GenerateCondition(logical.Right, trueLabel, falseLabel);
            }
            return;
        }

        if (condition is BinaryOpNode binop && CodeGenUtils.IsComparisonOperator(binop.Operator))
        {
            GenerateExpression(binop.Left, RegType.r0);
            int slot = SaveRegToStack(RegType.r0);
            GenerateExpression(binop.Right, RegType.r0);
            RestoreRegFromStack(slot, RegType.r1);
            _asm.EmitCmp(RegType.r1, RegType.r0);
            GenerateComparisonJump(binop.Operator, trueLabel, falseLabel);
            return;
        }

        // Общий случай: сравнение с нулём
        GenerateExpression(condition);
        _asm.EmitLdi(RegType.r1, 0);
        _asm.EmitCmp(RegType.r0, RegType.r1);

        if (trueLabel != null && falseLabel != null)
        {
            _asm.EmitJnz(trueLabel);
            _asm.EmitJmp(falseLabel);
        }
        else if (trueLabel != null) _asm.EmitJnz(trueLabel);
        else if (falseLabel != null) _asm.EmitJz(falseLabel);
    }

    private void GenerateComparisonJump(string op, string? trueLabel, string? falseLabel)
    {
        if (trueLabel != null && falseLabel == null)
        {
            string skip = _getLabel();
            GenerateComparisonJump(op, trueLabel, skip);
            _asm.MarkLabel(skip);
            return;
        }
        if (falseLabel != null && trueLabel == null)
        {
            string skip = _getLabel();
            GenerateComparisonJump(op, skip, falseLabel);
            _asm.MarkLabel(skip);
            return;
        }
        if (trueLabel == null && falseLabel == null) return;

        switch (op)
        {
            case "==": _asm.EmitJz(trueLabel!); _asm.EmitJmp(falseLabel!); break;
            case "!=": _asm.EmitJnz(trueLabel!); _asm.EmitJmp(falseLabel!); break;
            case "<": _asm.EmitJl(trueLabel!); _asm.EmitJmp(falseLabel!); break;
            case ">":
                _asm.EmitJl(falseLabel!);
                _asm.EmitJz(falseLabel!);
                _asm.EmitJmp(trueLabel!);
                break;
            case "<=":
                _asm.EmitJl(trueLabel!);
                _asm.EmitJz(trueLabel!);
                _asm.EmitJmp(falseLabel!);
                break;
            case ">=":
                _asm.EmitJl(falseLabel!);
                _asm.EmitJmp(trueLabel!);
                break;
        }
    }
}