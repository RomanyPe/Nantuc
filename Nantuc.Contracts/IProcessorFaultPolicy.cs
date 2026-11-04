using Nantuc.Common;

namespace Nantuc.Contracts;

/// <summary>
/// Политика остановки процессора при возникновение ошибки
/// </summary>
public interface IProcessorFaultPolicy
{
    /// <summary>
    /// Стандартная реализация
    /// </summary>
    public static IProcessorFaultPolicy Default { get; } = new DefaultProcessorFaultPolicy();
    private class DefaultProcessorFaultPolicy : IProcessorFaultPolicy
    {
        public bool ShouldContinue(in ProcessorFault fault) => fault.Status == BiosStatus.Success;
    }

    /// <summary>
    /// Проверка политики, стоит ли продолжать итерации
    /// </summary>
    /// <param name="fault"> отчет об ошибке</param>
    bool ShouldContinue(in ProcessorFault fault);
}
