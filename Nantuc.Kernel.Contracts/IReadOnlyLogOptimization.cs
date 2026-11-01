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
    /// <remarks>Returns a snapshot; the returned list is a new allocation on each call and is not updated as new entries are appended.</remarks>
    IReadOnlyList<OptimizationLogEntry> GetLogs();  // снимок

}
