using Nantuc.Compiler.ASM.Base;
using Nantuc.Compiler.C.AST;
using Nantuc.Compiler.C.CodeGenerator.Expression;

namespace Nantuc.Compiler.C.CodeGenerator.Statement;

// ============================================================
// StatementGenerator — генерация инструкций.
//
// Partial-файлы по зонам ответственности:
//   - StatementGenerator.cs               — диспетчеризация (этот файл)
//   - StatementGenerator.Declarations.cs  — объявления переменных, инициализация массивов
//   - StatementGenerator.Assignments.cs   — простые присваивания
//   - StatementGenerator.CompoundAssignments.cs — op= формы
//   - StatementGenerator.ControlFlow.cs   — if/while/for/do/return/break/continue
//   - StatementGenerator.InlineAsm.cs     — inline asm
//
// Type resolution вынесен в StatementTypeResolver (см. отдельный файл).
// ============================================================
public partial class StatementGenerator(
    AssemblerBase asm,
    ExpressionGenerator exprGen,
    FunctionContext funcCtx,
    Func<string> getLabel,
    GlobalMemoryManager globalMem,
    Dictionary<string, StructLayout> structTable,
    VariableAccessor varAccessor)
{
    private readonly AssemblerBase _asm = asm;
    private readonly ExpressionGenerator _exprGen = exprGen;
    private readonly FunctionContext _funcCtx = funcCtx;
    private readonly Func<string> _getLabel = getLabel;
    private readonly VariableAccessor _varAccessor = varAccessor;

    private readonly StatementTypeResolver _types =
        new(asm, funcCtx, globalMem, structTable, exprGen);

    private readonly Stack<(string BreakLabel, string ContinueLabel)> _loopStack = [];

    public void GenerateBlock(BlockNode block)
    {
        foreach (var stmt in block.Statements)
        {
            switch (stmt)
            {
                case VariableNode v: GenerateVariableDeclaration(v); break;
                case AssignmentNode a: GenerateAssignment(a); break;
                case IfNode i: GenerateIf(i); break;
                case WhileNode w: GenerateWhile(w); break;
                case ForNode f: GenerateFor(f); break;
                case DoWhileNode dw: GenerateDoWhile(dw); break;
                case ReturnNode r: GenerateReturn(r); break;
                case BreakNode: GenerateBreak(); break;
                case ContinueNode: GenerateContinue(); break;
                case InlineAsmNode ia: GenerateInlineAsm(ia); break;
                case FunctionCallNode c: _exprGen.GenerateExpression(c); break;
                case IncrementNode inc: _exprGen.GenerateExpression(inc); break;
                case BinaryOpNode b: _exprGen.GenerateExpression(b); break;
                case UnaryOpNode u: _exprGen.GenerateExpression(u); break;
                // Прочие выражения как инструкция (результат отбрасывается)
                default: _exprGen.GenerateExpression(stmt); break;
            }
        }
    }
}