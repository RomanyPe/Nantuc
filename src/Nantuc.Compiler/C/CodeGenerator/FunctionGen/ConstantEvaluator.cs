using Nantuc.Common;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Compiler.C.AST;
using Nantuc.Compiler.C.CodeGenerator.Expression;
using Nantuc.Diagnostics;

namespace Nantuc.Compiler.C.CodeGenerator.FunctionGen;

// ============================================================
// Свёртка константных выражений для инициализаторов данных.
// Чистые функции — не хранят состояние, зависят только от таблицы
// структур и ErrorReporter.
// ============================================================
internal static class ConstantEvaluator
{
    public static long Evaluate(
        ASTNode node,
        Dictionary<string, StructLayout> structTable,
        ErrorReporter er) => node switch
        {
            NumberNode num => num.Value,
            UnaryOpNode { Operator: "-" } u => -Evaluate(u.Operand, structTable, er),
            UnaryOpNode { Operator: "+" } u => Evaluate(u.Operand, structTable, er),
            UnaryOpNode { Operator: "~" } u => ~Evaluate(u.Operand, structTable, er),
            BinaryOpNode bin => ExpressionGenerator.ComputeConstant(
                                                         Evaluate(bin.Left, structTable, er),
                                                         Evaluate(bin.Right, structTable, er),
                                                         bin.Operator, er),
            SizeofTypeNode st => EvaluateSizeof(st, structTable, er),
            _ => er.Throw<long>(ErrorCode.NotSupported,
                     $"Non-constant initializer in array: {node.NameNode}"),
        };

    public static long EvaluateSizeof(
        SizeofTypeNode st,
        Dictionary<string, StructLayout> structTable,
        ErrorReporter er)
    {
        if (st.IsPointer) return 8;
        if (structTable.TryGetValue(st.TypeName, out var layout)) return layout.Size;
        if (CodeGenUtils.IsPrimitiveType(st.TypeName))
            return CodeGenUtils.GetSizeInBytes(
                CodeGenUtils.GetSizeForType(st.TypeName, er), er);

        er.Throw(ErrorCode.CodeGen_UnknownTypeSize, st.TypeName);
        return 0;
    }
}