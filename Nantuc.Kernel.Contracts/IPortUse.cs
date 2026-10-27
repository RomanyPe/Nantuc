namespace Nantuc.Kernel.Contracts;

/// <summary>
/// Контракт для регистрации и работы устройства в щине портов
/// </summary>
public interface IPortUse
{
    /// <summary>
    /// Метод чтения порта этого устройства
    /// </summary>
    /// <param name="offset">смещение относительно начала сектора</param>
    /// <returns>1 <see cref="byte"/> информации</returns>
    byte ReadPort(ulong offset);
    /// <summary>
    /// Метод пробуждения этого устройства порта
    /// </summary>
    void WakeProcessor();
    /// <summary>
    /// Метод записи байта в порт этого устройства 
    /// </summary>
    /// <param name="offset">смещение относительно начала сектора</param>
    /// <param name="value">значение которое будет записано на это устройство</param>
    void WritePort(ulong offset, byte value);
}
