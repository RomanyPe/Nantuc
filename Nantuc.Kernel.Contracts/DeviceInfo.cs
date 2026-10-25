using Nantuc.Kernel.Common;

namespace Nantuc.Kernel.Contracts;

public readonly record struct DeviceInfo(int Id, uint Sector, string? Name);

public interface IPortBus
{
    ReadPortResult ReadPort(ulong address);
    BiosStatus WritePort(ulong address, byte value);
    bool WakeProcessor(ulong address);
}

public record struct ReadPortResult(byte Data, BiosStatus Status = BiosStatus.Success);
