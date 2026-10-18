using Kernel.Contracts;

namespace VMApplication.Project;

public sealed class OptimizationResultLog
{
    private readonly Dictionary<OptimizationId, IReadOnlyLogOptimization> _logs = [];

    public IReadOnlyDictionary<OptimizationId, IReadOnlyLogOptimization> Logs => _logs;

    public void Upsert(IReadOnlyLogOptimization log)
    {
        // если лог уже есть — мерджим записи; иначе кладём снимок
        _logs[log.Id] = log;
    }
    public void AddRange(IEnumerable<IReadOnlyLogOptimization> logs)
    {
        ArgumentNullException.ThrowIfNull(logs);

        foreach (var log in logs)
        {
            _logs[log.Id] = log;
        }
    }
}
