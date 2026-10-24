using Nantuc.Compiler.ASM.IR;

namespace Nantuc.VmApplication.Project;

public readonly record struct CompilationToILResult(IReadOnlyList<string>? Errors, OptimizationResultLog? OptimizationResultLog)
{
    internal readonly IRAssembler? Assembler;
    internal CompilationToILResult(IRAssembler? assembler, OptimizationResultLog? optimizationResultLog, IReadOnlyList<string>? errors = null) : this(errors, optimizationResultLog)
    {
        Assembler = assembler;
    }

    public CompilationResult Build()
    {
        try
        {
            var asm = Assembler;
            return asm != null
                ? new CompilationResult(asm, OptimizationResultLog)
                : new CompilationResult(Errors);
        }
        catch (Exception ex)
        {
            return new CompilationResult([ex.Message]);
        }
    }
}
