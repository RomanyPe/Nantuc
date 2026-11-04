namespace Nantuc.Contracts;

/// <summary>
/// Интерфейс для чтение нативного буфера
/// </summary>
public interface IReadOnlyNativeBuffer
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
