using Nantuc.Compiler.ASM;
using Nantuc.Compiler.C.AST;

namespace Nantuc.Compiler.C.CodeGenerator.Statement;

public partial class StatementGenerator
{
    // ============================================================
    // Inline asm
    // ============================================================
    private void GenerateInlineAsm(InlineAsmNode asm)
        => new AssemblerParser().Assemble(asm.AsmCode, _asm, allowDataSection: false);
}