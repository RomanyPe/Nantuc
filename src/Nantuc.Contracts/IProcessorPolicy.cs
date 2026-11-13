using Nantuc.Common;

namespace Nantuc.Contracts;

public interface IProcessorPolicy
{
    /// <summary>
    /// Стандартная реализация
    /// </summary>
    public static IProcessorPolicy Default { get; } = new DefaultProcessorPolicy();
    private class DefaultProcessorPolicy : IProcessorPolicy
    {
        public bool ShouldContinue(in ProcessorFault fault) => fault.Status == BiosStatus.Success;
    }
    TimeSpan SleepWaitTimeout => TimeSpan.FromMilliseconds(15);
    bool AllowUnsafeOpCodes => false;
    bool ShouldContinue(in ProcessorFault fault);
}
