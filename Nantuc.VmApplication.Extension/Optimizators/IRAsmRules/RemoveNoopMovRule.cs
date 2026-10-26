using Nantuc.Compiler.ASM.IR;
using Nantuc.Kernel.Common;
using Nantuc.Kernel.Contracts;

namespace Nantuc.VmApplication.Extensions.Optimizators.IRAsmRules;

public sealed class RemoveNoopMovRule : IPeepholeRule
{
    private readonly PeepholeLog log = new(PeepholeOptimizerExtension.RemoveNoopMove);

    public IReadOnlyLogOptimization GetLogs() => log;
    public void Clear() => log.Clear();

    public bool TryOptimize(List<AsmItem> items, int index, int passCount, out int newIndex)
    {
        newIndex = index;
        if (items[index] is not AsmInstruction instr)
            return false;

        if (instr.OpCode == OpCode.MOV &&
            instr.FirstReg == instr.SecondReg &&
            instr.Immediate == null && instr.TargetLabel == null &&
            !IsImmediatelyAfterLabel(items, index))
        {
            log.Append(passCount, items[index]);

            items.RemoveAt(index);
            newIndex = index - 1; // откатываемся, чтобы проверить предыдущую инструкцию
            return true;
        }

        return false;
    }

    private static bool IsImmediatelyAfterLabel(List<AsmItem> items, int index)
        => index > 0 && items[index - 1] is AsmLabel;
}
