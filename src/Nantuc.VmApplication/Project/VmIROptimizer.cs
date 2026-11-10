using Nantuc.Compiler.ASM.IR;

namespace Nantuc.VmApplication.Project;

/// <summary>
/// IR оптимизатор
/// </summary>
public sealed class VmIROptimizer
{
    private readonly IROptimizer _opt;

    internal VmIROptimizer(IROptimizer astOptimizer)
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
    public void Add(IOptimizerIRRule rule) => _opt.Add(rule);
    /// <summary>
    /// Вставить в определнный <paramref name="index"/>
    /// </summary>
    public void Insert(int index, IOptimizerIRRule item) => _opt.Insert(index, item);
    /// <summary>
    /// Удалить правило
    /// </summary>
    public void Remove(IOptimizerIRRule item) => _opt.Remove(item);
    /// <summary>
    /// Удалить правило по индексу
    /// </summary>
    public void RemoveAt(int index) => _opt.RemoveAt(index);
}
