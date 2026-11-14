using Nantuc.Compiler.C.AST;
using Nantuc.Contracts;

namespace Nantuc.Compiler.C.Optimizations;

public interface IAstOptimizationRule
{
    OptimizationId Type { get; }
    /// <summary>
    /// Применяет оптимизацию к программе.
    /// Возвращает true, если были внесены изменения.
    /// </summary>
    IReadOnlyLogOptimization Optimize(ProgramNode program);
}
