using Nantuc.Compiler.C.Optimizators;
using Nantuc.Kernel.Contracts;

namespace Nantuc.VmApplication.Project;

/// <summary>
/// AST оптимизатор
/// </summary>
public sealed class VmAstOptimizer
{
    private readonly AstOptimizer _opt;

    internal VmAstOptimizer(AstOptimizer astOptimizer)
    {
        _opt = astOptimizer;
    }
    /// <summary>
    /// Количество текущих оптимизаторов
    /// </summary>
    public int Count => _opt.Count;
    /// <summary>
    /// Добавление новогой оптимизатора
    /// </summary>
    /// <param name="rule"></param>
    public void Add(IAstOptimizationRule rule) => _opt.Add(rule);
    /// <summary>
    /// Вставка нового правила в определенную позицию
    /// </summary>
    /// <param name="index">индекс куда будет вставлено правило</param>
    /// <param name="item">правило</param>
    public void Insert(int index, IAstOptimizationRule item) => _opt.Insert(index, item);
    /// <summary>
    /// Удаление правила
    /// </summary>
    /// <param name="item">правило</param>
    public void Remove(IAstOptimizationRule item) => _opt.Remove(item);
    /// <summary>
    /// Удалить по индексу
    /// </summary>
    /// <param name="index">индекс правила</param>
    public void RemoveAt(int index) => _opt.RemoveAt(index);
    /// <summary>
    /// Получить <see cref="OptimizationId"/> правила по индексу <paramref name="i"/>
    /// </summary>
    /// <param name="i"></param>
    /// <returns></returns>
    public OptimizationId GetType(int i) => _opt.GetType(i);

}
