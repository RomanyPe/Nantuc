using Nantuc.Compiler.ASM.IR;
using Nantuc.Diagnostics;

namespace Nantuc.VmApplication.Project;

/// <summary>
/// Промежуточное IR представление результатов компиляции
/// </summary>
/// <param name="Errors"> Список ошибок при не успешной компиляции </param>
/// <param name="OptimizationResultLog"></param>
public readonly record struct CompilationToIRResult
{
    internal readonly IRAssembler? Assembler;
    /// <summary> Список ошибок </summary>
    public IReadOnlyList<DiagnosticResult>? Errors { get; }
    /// <summary>
    /// Логи оптимизации
    /// </summary>
    public OptimizationResultLog? OptimizationResultLog { get; }

    /// <summary>
    /// Конструктор ошибок
    /// </summary>
    /// <param name="errors">список ошибок</param>
    public CompilationToIRResult(IReadOnlyList<DiagnosticResult>? errors)
    {
        Assembler = null;
        Errors = errors;
        OptimizationResultLog = null;
    }
    /// <summary>
    /// Конструктор успешной компиляции
    /// </summary>
    /// <param name="assembler">IR ассмеблер</param>
    /// <param name="optimizationResultLog">логи оптимизации</param>
    public CompilationToIRResult(IRAssembler? assembler, OptimizationResultLog? optimizationResultLog)
    {
        Assembler = assembler;
        OptimizationResultLog = optimizationResultLog;
    }
    /// <summary>
    /// Дефолтный конструктор со всеми параметрами
    /// </summary>
    /// <param name="assembler">IR ассмеблер</param>
    /// <param name="optimizationResultLog">логи оптимизации</param>
    /// <param name="errors">список ошибок</param>
    public CompilationToIRResult(IRAssembler? assembler, OptimizationResultLog? optimizationResultLog, IReadOnlyList<DiagnosticResult>? errors)
    {
        Assembler = assembler;
        Errors = errors;
        OptimizationResultLog = optimizationResultLog;
    }
    /// <summary>
    /// Собрать IR ассемблер в <see cref="CompilationResult"/>
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
            return new CompilationResult([new DiagnosticResult(ex.Message, ErrorCode.NotImplemented)]);
        }
    }
}
