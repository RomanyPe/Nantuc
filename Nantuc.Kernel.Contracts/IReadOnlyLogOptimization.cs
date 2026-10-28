namespace Nantuc.Kernel.Contracts;

/// <summary>
/// Интерфейс для чтения логов оптимизации 
/// </summary>
public interface IReadOnlyLogOptimization
{
    /// <summary>
    /// <see cref="OptimizationId"/> итентификатор логов оптимизации
    /// </summary>
    OptimizationId Id { get; }
    /// <summary>
    /// Получение снимка логов оптимизаций
    /// </summary>
    IReadOnlyList<OptimizationLogEntry> GetLogs();  // снимок

}
