namespace Nantuc.Kernel.Contracts;

/// <summary>
/// Результат замеров времени симуляции
/// </summary>
public interface ITimerSimulationResult
{
    /// <summary>
    /// Информация о пройденом времени симуляции
    /// </summary>
    TimeSpan Elapsed { get; }
    /// <summary>
    /// Количество шагов симуляции
    /// </summary>
    long Steps { get; }
}
