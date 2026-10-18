using Kernel.Contracts;

namespace Compiller.C.Optimizators;

public interface IAstOptimizationRule
{
    OptimizationId Type { get; }
    /// <summary>
    /// Применяет оптимизацию к программе.
    /// Возвращает true, если были внесены изменения.
    /// </summary>
    IReadOnlyLogOptimization Optimize(ProgramNode program);
}
