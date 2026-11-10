using Nantuc.Compiler.ASM.IR;
using Nantuc.Compiler.C.Optimizators;
using Nantuc.VmApplication.Project;

namespace Nantuc.VmApplication.Unsafe;

public static class VmCompilerOptimizatorExtension
{
    extension(VmCompilerOptimizator opt)
    {
        public IROptimizer RawIROptimizer => opt.IROptimizer;

        public AstOptimizer RawAstOptimizer => opt.AstOptimizer;
    }
}