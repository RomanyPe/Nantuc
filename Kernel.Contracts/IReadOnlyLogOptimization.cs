namespace Kernel.Contracts;

public interface IReadOnlyLogOptimization
{
    OptimizationId Id { get; }
    IReadOnlyList<OptimizationLogEntry> GetLogs();  // снимок
}
public readonly record struct OptimizationLogEntry(
    int Pass,
    string Message);