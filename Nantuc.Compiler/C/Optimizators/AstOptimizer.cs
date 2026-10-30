using Nantuc.Compiler.C.AST;
using Nantuc.Kernel.Contracts;

namespace Nantuc.Compiler.C.Optimizators;


public class OptimizationResult(OptimizationId type) : ILogOptimization
{
    private readonly List<OptimizationLogEntry> _entries = [];
    public OptimizationId Id => type;

    public void Append(int pass, string message) =>
            _entries.Add(new OptimizationLogEntry(pass, message));
    public void Clear() => _entries.Clear();

    public IReadOnlyList<OptimizationLogEntry> GetLogs() => _entries;
}


public class AstOptimizer
{
    private readonly List<IAstOptimizationRule> _rules = [];
    public void Add(IAstOptimizationRule rule) => _rules.Add(rule);
    public void Insert(int index, IAstOptimizationRule item) => _rules.Insert(index, item);
    public void Remove(IAstOptimizationRule item) => _rules.Remove(item);
    public void RemoveAt(int index) => _rules.RemoveAt(index);
    public int Count => _rules.Count;

    public OptimizationId GetType(int i) => _rules[i].Type;

    public IEnumerable<IReadOnlyLogOptimization> Optimize(ProgramNode program)
        => _rules.Select(rule => rule.Optimize(program));
}