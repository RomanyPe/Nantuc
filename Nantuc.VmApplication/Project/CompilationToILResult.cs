using Nantuc.Compiler.ASM.IR;

namespace Nantuc.VmApplication.Project;

/// <summary>
/// Промежуточное IL представление результатов компиляции
/// </summary>
/// <param name="Errors"> Список ошибок при не успешной компиляции </param>
/// <param name="OptimizationResultLog"></param>
public readonly record struct CompilationToILResult
{
    internal readonly ILAssembler? Assembler;
    public IReadOnlyList<string>? Errors { get; }
    public OptimizationResultLog? OptimizationResultLog { get; }

    public CompilationToILResult(IReadOnlyList<string>? errors)
    {
        Assembler = null;
        Errors = errors;
        OptimizationResultLog = null;
    }

    internal CompilationToILResult(ILAssembler? assembler, OptimizationResultLog? optimizationResultLog)
    {
        Assembler = assembler;
        OptimizationResultLog = optimizationResultLog;
    }

    public CompilationToILResult(ILAssembler? assembler, OptimizationResultLog? optimizationResultLog, IReadOnlyList<string>? errors)
    {
        Assembler = assembler;
        Errors = errors;
        OptimizationResultLog = optimizationResultLog;
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
