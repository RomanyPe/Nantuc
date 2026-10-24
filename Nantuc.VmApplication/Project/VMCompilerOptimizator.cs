using Nantuc.Compiler.ASM.IR;
using Nantuc.Compiler.C.Optimizators;

namespace Nantuc.VmApplication.Project;

public sealed class VmCompilerOptimizator
{
    internal AstOptimizer AstOptimizer { get; }
    internal PeepholeOptimizer PeepholeOptimizer { get; }
    public VmPeepholeOptimizer VMPeepholeOptimizer { get; }
    public VmAstOptimizer VMAstOptimizer { get; }

    public VmCompilerOptimizator()
    {
        AstOptimizer = new();
        PeepholeOptimizer = new();
        VMAstOptimizer = new(AstOptimizer);
        VMPeepholeOptimizer = new(PeepholeOptimizer);
    }
}
