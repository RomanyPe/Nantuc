using Nantuc.Common;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Compiler.C.AST;
using Nantuc.Diagnostics;

namespace Nantuc.Compiler.C.CodeGenerator.Expression;

public partial class ExpressionGenerator
{
    // ============================================================
    // Унарные операторы: -x, !x, ~x
    // ============================================================
    private void GenerateUnaryOp(UnaryOpNode unop, RegType targetReg)
    {
        switch (unop.Operator)
        {
            case "-":
                GenerateExpression(unop.Operand, targetReg);
                _asm.EmitNot(targetReg);
                _asm.EmitInc(targetReg);
                break;

            case "!":
                GenerateExpression(unop.Operand, targetReg);
                RegType scratch = targetReg == RegType.r1 ? RegType.r2 : RegType.r1;
                _asm.EmitLdi(scratch, 0);
                _asm.EmitCmp(targetReg, scratch);

                string endLabel = _getLabel();
                _asm.EmitLdi(targetReg, 0);
                _asm.EmitJnz(endLabel);
                _asm.EmitLdi(targetReg, 1);
                _asm.MarkLabel(endLabel);
                break;

            case "~":
                GenerateExpression(unop.Operand, targetReg);
                _asm.EmitNot(targetReg);
                break;

            default:
                _asm.ErrorReporter.Throw(ErrorCode.NotSupported,
                    $"Unsupported unary operator: {unop.Operator}");
                break;
        }
    }
}