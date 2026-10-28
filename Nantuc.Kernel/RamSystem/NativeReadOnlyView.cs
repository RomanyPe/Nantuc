using Nantuc.Kernel.Contracts;

namespace Nantuc.Kernel.RamSystem;

/// <summary>
/// Публичное read-only представление над <see cref="NativeMemoryBuffer"/>:
/// проверяет границы и состояние освобождения при каждом обращении.
/// </summary>
public class NativeReadOnlyView(NativeMemoryBuffer buffer) : INativeReadOnlyBuffer
{
    /// <summary>
    /// Размер буфера в байтах.
    /// </summary>
    public nuint Length => buffer.Length;

    /// <summary>
    /// Индексированный доступ к байту буфера.
    /// </summary>
    /// <param name="index">Смещение в байтах.</param>
    /// <returns>Значение байта.</returns>
    /// <exception cref="ObjectDisposedException">Если буфер уже освобождён.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Если <paramref name="index"/> вне диапазона.</exception>
    public byte this[nuint index]
    {
        get
        {
            ObjectDisposedException.ThrowIf(buffer.IsDisposed, buffer);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, buffer.Length);
            return buffer[index];
        }
    }
}