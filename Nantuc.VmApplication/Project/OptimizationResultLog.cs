using Nantuc.Kernel.Contracts;

namespace Nantuc.VmApplication.Project;
/// <summary>
/// Класс логов оптимизаций
/// </summary>
public sealed class OptimizationResultLog
{
    private readonly Dictionary<OptimizationId, IReadOnlyLogOptimization> _logs = [];

    /// <summary>
    /// Не мутабельный словарь оптимизаций, только класс может изменять
    /// </summary>
    public IReadOnlyDictionary<OptimizationId, IReadOnlyLogOptimization> Logs => _logs;

    /// <summary>
    /// [ЭКСПЕРЕМЕНТАЛЬНАЯ ФУНКЦИЯ] Заменить лог оптимизации на другой, позже будет изменена
    /// </summary>
    /// <param name="log">Лог оптимизации</param>
    public void Upsert(IReadOnlyLogOptimization log)
    {
        // если лог уже есть — мерджим записи; иначе кладём снимок
        _logs[log.Id] = log;
    }
    /// <summary>
    /// Ленивое добавление логов оптимизации в словарь
    /// </summary>
    /// <param name="logs"></param>
    public void AddRange(IEnumerable<IReadOnlyLogOptimization> logs)
    {
        ArgumentNullException.ThrowIfNull(logs);

        foreach (var log in logs)
        {
            _logs[log.Id] = log;
        }
    }
}
