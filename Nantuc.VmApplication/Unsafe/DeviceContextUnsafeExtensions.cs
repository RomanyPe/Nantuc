using Nantuc.Kernel.BiosSystem;
using Nantuc.Kernel.ControllersData;
using Nantuc.VmApplication.Emulator;

namespace Nantuc.VmApplication.Unsafe;

public static class DeviceContextUnsafeExtensions
{
    extension(DeviceContext context)
    {
        // Метод расширения, который «подтягивает» внутреннее свойство
        public Device AsRawDevice()
        {
            ArgumentNullException.ThrowIfNull(context);
            return context.UnsafeGetDevice;
        }
    }
}

public static class VmEmulatorUnsafeExtension
{
    extension(VmEmulator vm)
    {
        public PortBus GetRawPortBus() 
        {
            ArgumentNullException.ThrowIfNull(vm);
            return vm.AsPortBus;
        }
    }
}