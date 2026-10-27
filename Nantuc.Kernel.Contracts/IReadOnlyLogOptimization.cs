namespace Nantuc.Kernel.Contracts;

public interface IReadOnlyLogOptimization
{
    OptimizationId Id { get; }
    IReadOnlyList<OptimizationLogEntry> GetLogs();  // снимок

}
