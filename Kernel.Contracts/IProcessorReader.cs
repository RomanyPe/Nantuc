using Kernel.Common;

namespace Kernel.Contracts;

public interface IProcessorReader
{
    bool IsRunning { get; }
    bool IsSleeping { get; }
    ulong GetRegister(RegType regType);
}