namespace Nantuc.Kernel.Contracts;

/// <summary>
/// Интерфейс для чтение нативного буфера
/// </summary>
public interface INativeReadOnlyBuffer
{
    /// <summary>
    /// Длина буфера
    /// </summary>
    nuint Length { get; }
    /// <summary>
    /// индексатор буфера
    /// </summary>
    byte this[nuint index] { get; }
}
