using Nantuc.Compiler.C.AST;
using Nantuc.Contracts;

namespace Nantuc.Compiler.C.Optimizations;

public interface IAstOptimizationRule
{
    OptimizationId Type { get; }
    IReadOnlyLogOptimization Optimize(ProgramNode program);
}
