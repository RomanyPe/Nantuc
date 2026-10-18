using Compiller.ASM.Optimizators;
using Kernel.Common;
using Kernel.Contracts;

namespace ExtensionsVMApplication.Optimizators.IRAsmRules;

/// <summary>
/// Удаляет дублирующиеся инструкции END в конце программы,
/// оставляя только одну.
/// </summary>
public sealed class RemoveMultiEndPairRule : IPeepholeRule
{
    private readonly PeepholeLog log = new(PeepholeOptimizerExtension.RemoveMultiEnd);

    public IReadOnlyLogOptimization GetLogs()
    {
        log.Clear();
        return log;
    }
    public void Clear() => log.Clear();

    public bool TryOptimize(List<AsmItem> items, int index, int passCount, out int newIndex)
    {
        newIndex = index;

        // Текущий элемент должен быть END
        if (index >= items.Count || items[index] is not AsmInstruction current ||
            current.OpCode != OpCode.END)
            return false;

        // Проверяем, есть ли следующий элемент и является ли он тоже END
        if (index + 1 >= items.Count)
            return false;

        if (items[index + 1] is not AsmInstruction next || next.OpCode != OpCode.END)
            return false;

        // Если между ними метка (маловероятно, но для безопасности), не удаляем
        if (items[index] is AsmLabel || items[index + 1] is AsmLabel)
            return false;

        // Удаляем второй END
        items.RemoveAt(index + 1);
        log.Append(passCount,$"Removed extra END at position {index + 1}");

        // Возвращаем тот же индекс, чтобы проверить, не остались ли ещё END подряд.
        newIndex = index;
        return true;
    }
}