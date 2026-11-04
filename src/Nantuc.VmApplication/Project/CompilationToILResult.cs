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
    /// <summary> Список ошибок </summary>
    public IReadOnlyList<string>? Errors { get; }
    /// <summary>
    /// Логи оптимизации
    /// </summary>
    public OptimizationResultLog? OptimizationResultLog { get; }

    /// <summary>
    /// Конструктор ошибок
    /// </summary>
    /// <param name="errors">список ошибок</param>
    public CompilationToILResult(IReadOnlyList<string>? errors)
    {
        Assembler = null;
        Errors = errors;
        OptimizationResultLog = null;
    }
    /// <summary>
    /// Конструктор успешной компиляции
    /// </summary>
    /// <param name="assembler">IL ассмеблер</param>
    /// <param name="optimizationResultLog">логи оптимизации</param>
    public CompilationToILResult(ILAssembler? assembler, OptimizationResultLog? optimizationResultLog)
    {
        Assembler = assembler;
        OptimizationResultLog = optimizationResultLog;
    }
    /// <summary>
    /// Дефолтный конструктор со всеми параметрами
    /// </summary>
    /// <param name="assembler">IL ассмеблер</param>
    /// <param name="optimizationResultLog">логи оптимизации</param>
    /// <param name="errors">список ошибок</param>
    public CompilationToILResult(ILAssembler? assembler, OptimizationResultLog? optimizationResultLog, IReadOnlyList<string>? errors)
    {
        Assembler = assembler;
        Errors = errors;
        OptimizationResultLog = optimizationResultLog;
    }
    /// <summary>
    /// Собрать IL ассемблер в <see cref="CompilationResult"/>
    /// </summary>
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
