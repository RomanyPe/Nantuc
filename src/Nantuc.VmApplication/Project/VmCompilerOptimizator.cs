using Nantuc.Compiler.ASM.IR;
using Nantuc.Compiler.C.Optimizators;

namespace Nantuc.VmApplication.Project;

/// <summary>
/// Оптимизаторы компилятора
/// </summary>
public sealed class VmCompilerOptimizator
{
    internal AstOptimizer AstOptimizer { get; }
    internal IROptimizer IROptimizer { get; }
    public VmIROptimizer VmIROptimizer { get; }
    public VmAstOptimizer VmAstOptimizer { get; }

    public VmCompilerOptimizator()
    {
        AstOptimizer = new();
        IROptimizer = new();
        VmAstOptimizer = new(AstOptimizer);
        VmIROptimizer = new(IROptimizer);
    }
}
