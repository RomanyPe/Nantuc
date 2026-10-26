using Nantuc.Kernel.BiosSystem;
using Nantuc.Kernel.ControllersData;
using Nantuc.VmApplication.Emulator;

namespace Nantuc.VmApplication.Unsafe;

public static class DeviceContextUnsafeExtensions
{
    extension(DeviceContext context)
    {
        /// <summary>
        /// Получения класса <see cref="Device"/> из <see cref="DeviceContext"/> без рефлексии
        /// Использовать <see cref="Device"/> на свой страх и риск, почти не имеет ограничений поэтому легче сломать
        /// </summary>
        /// <returns><see cref="Device"/></returns>
        /// <exception cref="ArgumentNullException"></exception>
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
        /// <summary>
        /// Получения класса <see cref="PortBus"/> из <see cref="VmEmulator"/> без рефлексии и привидений
        /// Использовать <see cref="PortBus"/> на свой страх и риск, почти не имеет ограничений поэтому легче сломать
        /// </summary>
        /// <returns><see cref="PortBus"/></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public PortBus GetRawPortBus() 
        {
            ArgumentNullException.ThrowIfNull(vm);
            return vm.AsPortBus;
        }
    }
}