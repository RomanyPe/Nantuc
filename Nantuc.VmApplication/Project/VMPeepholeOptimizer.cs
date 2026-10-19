using Nantuc.Compiler.ASM.Optimizators;

namespace Nantuc.VmApplication.Project;

public sealed class VMPeepholeOptimizer
{
    private readonly PeepholeOptimizer _opt;

    internal VMPeepholeOptimizer(PeepholeOptimizer astOptimizer)
    {
        _opt = astOptimizer;
    }
    public int Count => _opt.Count;
    public void Add(IPeepholeRule rule) => _opt.Add(rule);
    public void Insert(int index, IPeepholeRule item) => _opt.Insert(index, item);
    public void Remove(IPeepholeRule item) => _opt.Remove(item);
    public void RemoveAt(int index) => _opt.RemoveAt(index);
}
