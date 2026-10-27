using Nantuc.Kernel.Contracts;

namespace Nantuc.VmApplication.Emulator.Abstraction;

/// <summary>
/// Асбстрактный класс который обезопасен от двойного <see cref="Dispose"/>
/// </summary>
public abstract class PortControlBase : IPortController
{
    private int _disposed;

    /// <summary>
    /// Проверка был ли вызван <see cref="Dispose"/> у объекта ранее
    /// </summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        OnDispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Метод чтения порта этого устройства для контракта <see cref="IPortController"/> 
    /// </summary>
    /// <param name="offset">смещение относительно начала сектора</param>
    /// <returns>1 <see cref="byte"/> информации</returns>
    public abstract byte ReadPort(ulong offset);
    /// <summary>
    /// Метод пробуждения этого устройства порта для контракта <see cref="IPortController"/> 
    /// </summary>
    public abstract void WakeProcessor();
    /// <summary>
    /// Метод записи байта в порт этого устройства для контракта <see cref="IPortController"/> 
    /// </summary>
    /// <param name="offset">смещение относительно начала сектора</param>
    /// <param name="value">значение которое будет записано на это устройство</param>
    public abstract void WritePort(ulong offset, byte value);

    /// <summary>
    /// Безопасный метод для 
    /// </summary>
    protected virtual void OnDispose() { }
}