using Nantuc.Kernel.BiosSystem;
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