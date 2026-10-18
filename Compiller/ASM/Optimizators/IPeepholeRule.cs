using Compiller.C.Optimizators;
using Kernel.Contracts;

namespace Compiller.ASM.Optimizators;

public interface IPeepholeRule
{
    /// <summary>
    /// Пытается применить правило, начиная с позиции index в списке items.
    /// Если правило применимо, изменяет список и возвращает true, а также новое значение index
    /// (обычно index или index - количество удалённых инструкций, чтобы повторно проверить окрестность).
    /// </summary>
    bool TryOptimize(List<AsmItem> items, int index, int passCount, out int newIndex);
    IReadOnlyLogOptimization GetLogs();
    void Clear();
}

