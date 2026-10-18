using Kernel.Common;

namespace Kernel.Contracts;

public interface IProcessorFaultPolicy
{
    public static IProcessorFaultPolicy Default => new DefaultProcessorFaultPolicy();
    private class DefaultProcessorFaultPolicy : IProcessorFaultPolicy
    {
        public bool ShouldContinue(in ProcessorFault fault) => fault.Status switch
        {
            BiosStatus.Success => true,
            BiosStatus.NullDeviceOutput => true,
            _ => false
        };
    }

    bool ShouldContinue(in ProcessorFault fault);
}
