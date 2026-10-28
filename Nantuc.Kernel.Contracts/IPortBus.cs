using Nantuc.Kernel.Common;

namespace Nantuc.Kernel.Contracts;

/// <summary>
/// Интерфейс шины портов только для работы IO, без регистраций и валидаторов
/// </summary>
public interface IPortBus
{
    /// <summary>
    /// Чтение порта устройства
    /// </summary>
    /// <param name="address">адрес порта</param>
    /// <returns>Результат чтения, сначала проверяйте <see cref="ReadPortResult.Status"/> на предмет ошибок потом читайте <see cref="ReadPortResult.Data"/></returns>
    ReadPortResult ReadPort(ulong address);
    /// <summary>
    /// Метод записи <paramref name="value"/> в порт <paramref name="address"/>
    /// </summary>
    /// <param name="address">адрес порта</param>
    /// <param name="value">значение</param>
    /// <returns>статус записи, если ошибка </returns>
    BiosStatus WritePort(ulong address, byte value);
    /// <summary>
    /// Разбудить устройство по порту <paramref name="address"/>
    /// </summary>
    /// <param name="address">адрес порта</param>
    /// <returns>пробуждено ли устройство</returns>
    bool WakeProcessor(ulong address);
}
