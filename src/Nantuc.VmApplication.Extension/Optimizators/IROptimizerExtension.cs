using Nantuc.Compiler.ASM;
using Nantuc.VmApplication.Extensions.Optimizators.IRAsmRules;
using Nantuc.VmApplication.Project;
using Nantuc.Contracts;

namespace Nantuc.VmApplication.Extensions.Optimizators;

public static class IROptimizerExtension
{
    extension(VmIROptimizer IR)
    {
        public VmIROptimizer AddStdRules()
        {
            IR.Add(new RemoveNoopMovRule());
            IR.Add(new RemovePushPopPairRule());
            IR.Add(new RemoveSwapMovPairRule());
            IR.Add(new RemoveJumpToNextLabelRule());
            IR.Add(new ConstantFoldingRule());
            IR.Add(new RemoveMultiEndPairRule());
            return IR;
        }
    }
    extension(OptimizationId id)
    {
        public OptimizationId WithIRPrefix()
        {
            return new OptimizationId("ir." + id.Value);
        }
        public OptimizationId WithRule()
        {
            return new OptimizationId(id.Value + "-rule");
        }
    }

    private static OptimizationId IR(string value) => new OptimizationId(value).WithIRPrefix();

    public static readonly OptimizationId ConstantFolding = IR("constant-folding");
    public static readonly OptimizationId RemoveJumpToNextLabel = IR("remove-jump-to-next-label");
    public static readonly OptimizationId RemoveMultiEnd = IR("remove-multi-end");
    public static readonly OptimizationId RemoveNoopMove = IR("remove-noop-mov");
    public static readonly OptimizationId RemovePushPop = IR("remove-push-pop");
    public static readonly OptimizationId RemoveSwapMov = IR("remove-swap-mov");
    public static readonly OptimizationId ReplaceSafeAccess = IR("replace-safe-mem-acces-with-unsafe");
}
