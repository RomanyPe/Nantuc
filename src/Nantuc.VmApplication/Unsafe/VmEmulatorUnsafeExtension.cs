using Nantuc.Kernel.ControllersData;
using Nantuc.VmApplication.Emulator;

namespace Nantuc.VmApplication.Unsafe;

/// <summary>
/// Небезопасные расширения для <see cref="VmEmulator"/>.
/// Позволяют получить прямой доступ к <see cref="PortBus"/> без рефлексии и приведений типов.
/// </summary>
/// <remarks>
/// Использование этого класса нарушает инкапсуляцию <see cref="VmEmulator"/>.
/// <see cref="PortBus"/> практически не имеет ограничений — его некорректное использование
/// может привести к повреждению шины портов и неопределённому поведению эмулятора.
/// </remarks>
public static class VmEmulatorUnsafeExtension
{
    /// <param name="vm">
    /// Экземпляр <see cref="VmEmulator"/>, из которого извлекается шина портов.
    /// Не может быть <see langword="null"/>.
    /// </param>
    extension(VmEmulator vm)
    {
        /// <summary>
        /// Возвращает внутренний <see cref="PortBus"/> из <see cref="VmEmulator"/> без рефлексии и приведений типов.
        /// </summary>
        /// <returns>Внутренний экземпляр <see cref="PortBus"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Если <paramref name="vm"/> равен <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// <see cref="PortBus"/> предоставляется «как есть» и почти не имеет ограничений.
        /// Некорректные операции с ним могут сломать эмуляцию I/O-устройств.
        /// </remarks>
        public PortBus GetRawPortBus()
        {
            ArgumentNullException.ThrowIfNull(vm);
            return vm.AsPortBus;
        }
    }
}
