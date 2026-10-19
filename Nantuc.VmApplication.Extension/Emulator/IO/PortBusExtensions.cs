using Nantuc.Kernel.ControllersData;

namespace Nantuc.VmApplication.Extensions.Emulator.IO;

public static class PortBusExtensions
{
    extension(PortBus bus)
    {
        public ulong GetBaseAddress(uint sector)
            => (ulong)sector * bus.PortsOnDevice;
    }
}