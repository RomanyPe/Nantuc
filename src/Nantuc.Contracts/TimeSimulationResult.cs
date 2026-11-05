namespace Nantuc.Contracts;

/// <summary>
/// Результат замеров времени симуляции
/// </summary>
public readonly record struct TimeSimulationResult(TimeSpan Elapsed, long Steps);