using Nantuc.Compiler.ASM.IR;
using Nantuc.Kernel.Common;
using Nantuc.Kernel.Contracts;

namespace Nantuc.VmApplication.Extensions.Optimizators.IRAsmRules;

public sealed class RemoveJumpToNextLabelRule : IPeepholeRule
{
    private readonly PeepholeLog log = new(PeepholeOptimizerExtension.RemoveJumpToNextLabel);

    public IReadOnlyLogOptimization GetLogs() => log;
    public void Clear() => log.Clear();

    public bool TryOptimize(List<AsmItem> items, int index, int passCount, out int newIndex)
    {
        newIndex = index;
        if (items[index] is not AsmInstruction instr)
            return false;

        if (instr.OpCode == OpCode.JMP &&
            instr.TargetLabel != null &&
            index + 1 < items.Count &&
            items[index + 1] is AsmLabel nextLabel &&
            nextLabel.Name == instr.TargetLabel)
        {
            log.Append(passCount, items[index]);

            items.RemoveAt(index);
            newIndex = index - 1;
            return true;
        }

        return false;
    }
}
