using Nantuc.Kernel.Contracts;

namespace Nantuc.Compiler.C.Optimizators;

public interface IAstOptimizationRule
{
    OptimizationId Type { get; }
    /// <summary>
    /// Применяет оптимизацию к программе.
    /// Возвращает true, если были внесены изменения.
    /// </summary>
    IReadOnlyLogOptimization Optimize(ProgramNode program);
}
