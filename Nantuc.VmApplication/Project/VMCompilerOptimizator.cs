using Nantuc.Compiler.ASM.IR;
using Nantuc.Compiler.C.Optimizators;

namespace Nantuc.VmApplication.Project;

/// <summary>
/// Оптимизаторы компилятора
/// </summary>
public sealed class VmCompilerOptimizator
{
    internal AstOptimizer AstOptimizer { get; }
    internal PeepholeOptimizer PeepholeOptimizer { get; }
    public VmPeepholeOptimizer VmPeepholeOptimizer { get; }
    public VmAstOptimizer VmAstOptimizer { get; }

    public VmCompilerOptimizator()
    {
        AstOptimizer = new();
        PeepholeOptimizer = new();
        VmAstOptimizer = new(AstOptimizer);
        VmPeepholeOptimizer = new(PeepholeOptimizer);
    }
}
