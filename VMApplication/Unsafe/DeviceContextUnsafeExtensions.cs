using Kernel.BiosSystem;
using VMApplication.Emulator;

namespace VMApplication.Unsafe;

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