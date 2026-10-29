using Nantuc.Kernel.BiosSystem;
using Nantuc.VmApplication.Emulator;

namespace Nantuc.VmApplication.Unsafe;

/// <summary>
/// Небезопасные расширения для <see cref="DeviceContext"/>.
/// Позволяют получить доступ к внутреннему <see cref="Device"/> в обход штатной инкапсуляции.
/// </summary>
/// <remarks>
/// Использование этого класса нарушает инкапсуляцию <see cref="DeviceContext"/>.
/// Полученный <see cref="Device"/> практически не имеет ограничений, поэтому его некорректное
/// использование может привести к повреждению состояния виртуальной машины, порче памяти
/// или неопределённому поведению. Применяйте только в отладочных, диагностических
/// или исследовательских сценариях.
/// </remarks>
public static class DeviceContextUnsafeExtensions
{
    /// <param name="context">
    /// Контекст устройства, из которого извлекается «сырой» <see cref="Device"/>.
    /// Не может быть <see langword="null"/>.
    /// </param>
    extension(DeviceContext context)
    {
        /// <summary>
        /// Возвращает внутренний экземпляр <see cref="Device"/> из <see cref="DeviceContext"/> без использования рефлексии.
        /// </summary>
        /// <returns>Внутренний экземпляр <see cref="Device"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Если <paramref name="context"/> равен <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// <see cref="Device"/> предоставляется «как есть» и почти не имеет ограничений.
        /// Любые некорректные операции с ним могут сломать состояние эмулятора.
        /// </remarks>
        public Device AsRawDevice()
        {
            ArgumentNullException.ThrowIfNull(context);
            return context.UnsafeGetDevice;
        }
    }
}
