using Compiller.ASM.Optimizators;
using Kernel.Common;
using Kernel.Contracts;

namespace ExtensionsVMApplication.Optimizators.IRAsmRules;

public sealed class RemoveSwapMovPairRule : IPeepholeRule
{
    private readonly PeepholeLog log = new(PeepholeOptimizerExtension.RemoveSwapMov);

    public IReadOnlyLogOptimization GetLogs()
    {
        log.Clear();
        return log;
    }
    public void Clear() => log.Clear();

    public bool TryOptimize(List<AsmItem> items, int index, int passCount, out int newIndex)
    {
        newIndex = index;
        if (index + 1 >= items.Count ||
            items[index] is not AsmInstruction first ||
            items[index + 1] is not AsmInstruction second)
            return false;

        if (first.OpCode == OpCode.MOV &&
            second.OpCode == OpCode.MOV &&
            first.FirstReg == second.SecondReg &&
            first.SecondReg == second.FirstReg &&
            first.FirstReg != first.SecondReg &&
            !IsImmediatelyAfterLabel(items, index) &&
            !IsImmediatelyAfterLabel(items, index + 1))
        {
            log.Append(passCount, items[index + 1]);
            log.Append(passCount, items[index]);

            items.RemoveAt(index + 1);
            items.RemoveAt(index);
            newIndex = index - 1;
            return true;
        }

        return false;
    }

    private static bool IsImmediatelyAfterLabel(List<AsmItem> items, int index)
        => index > 0 && items[index - 1] is AsmLabel;
}
