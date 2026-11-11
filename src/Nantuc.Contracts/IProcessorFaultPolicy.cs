using Nantuc.Common;

namespace Nantuc.Contracts;

/// <summary>
/// Политика остановки процессора при возникновение ошибки
/// </summary>
public interface IProcessorPolicy
{
    /// <summary>
    /// Стандартная реализация
    /// </summary>
    public static IProcessorPolicy Default { get; } = new DefaultProcessorFaultPolicy();
    private class DefaultProcessorFaultPolicy : IProcessorPolicy
    {
        bool IProcessorPolicy.AllowUnsafeOpCodes => false;
        public bool ShouldContinue(in ProcessorFault fault) => fault.Status == BiosStatus.Success;
    }

    bool AllowUnsafeOpCodes { get; }
    /// <summary>
    /// Проверка политики, стоит ли продолжать итерации
    /// </summary>
    /// <param name="fault"> отчет об ошибке</param>
    bool ShouldContinue(in ProcessorFault fault);
}
