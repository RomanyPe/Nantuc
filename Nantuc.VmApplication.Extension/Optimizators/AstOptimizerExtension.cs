using Nantuc.Kernel.Contracts;
using Nantuc.VmApplication.Extensions.Optimizators.AstRules;
using Nantuc.VmApplication.Project;

namespace Nantuc.VmApplication.Extensions.Optimizators;

public static class AstOptimizerExtension
{
    extension(VmAstOptimizer optimizer)
    {
        public VmAstOptimizer AddStdRules()
        {
            optimizer.Add(new PropagateConstantsRule());
            optimizer.Add(new FoldConstantsRule());
            optimizer.Add(new RemoveUnusedVariablesRule());
            optimizer.Add(new RemoveUnreachableCodeRule());
            optimizer.Add(new InlineSmallVoidFunctionsRule());
            optimizer.Add(new RemoveUnreachableCodeRule());
            return optimizer;
        }
    }

    extension(OptimizationId id)
    {
        public OptimizationId WithASTNodePrefix()
        {
            return new OptimizationId("astnode-opt." + id.Value);
        }
    }
    private static OptimizationId AstNode(string value) => new OptimizationId(value).WithASTNodePrefix();
    public static readonly OptimizationId FunctionInlined = AstNode("inline-func");
    public static readonly OptimizationId ConstantPropagated = AstNode("const-propagate");
    public static readonly OptimizationId ConstantFolded = AstNode("const-fold");
    public static readonly OptimizationId NodeRemoved = AstNode("remove-node");
    public static readonly OptimizationId VariableRemoved = AstNode("remove-variable-unused");

}