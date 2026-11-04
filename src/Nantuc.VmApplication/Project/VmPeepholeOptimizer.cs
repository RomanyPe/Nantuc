using Nantuc.Compiler.ASM.IR;

namespace Nantuc.VmApplication.Project;

/// <summary>
/// Peephole оптимизатор
/// </summary>
public sealed class VmPeepholeOptimizer
{
    private readonly PeepholeOptimizer _opt;

    internal VmPeepholeOptimizer(PeepholeOptimizer astOptimizer)
    {
        _opt = astOptimizer;
    }
    /// <summary>
    /// Количество правил
    /// </summary>
    public int Count => _opt.Count;
    /// <summary>
    /// Добавить новое правило
    /// </summary>
    public void Add(IPeepholeRule rule) => _opt.Add(rule);
    /// <summary>
    /// Вставить в определнный <paramref name="index"/>
    /// </summary>
    public void Insert(int index, IPeepholeRule item) => _opt.Insert(index, item);
    /// <summary>
    /// Удалить правило
    /// </summary>
    public void Remove(IPeepholeRule item) => _opt.Remove(item);
    /// <summary>
    /// Удалить правило по индексу
    /// </summary>
    public void RemoveAt(int index) => _opt.RemoveAt(index);
}
