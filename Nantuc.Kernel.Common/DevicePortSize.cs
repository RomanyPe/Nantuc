using System.Diagnostics;

namespace Nantuc.Kernel.Common;

/// <summary>
/// Размер порта устройства, заданный как степень двойки: от 8 до 256 байт.
/// </summary>
/// <remarks>
/// Публичного конструктора с произвольным <c>Log2</c> нет.
/// Значения по умолчанию (<see langword="default"/> или <c>new DevicePortSize()</c>)
/// невалидны: <see cref="Log2"/> = 0, <see cref="IsValid"/> = false.
/// При этом <see cref="Bytes"/> для такого значения возвращает 1.
/// Операторы сравнения сравнивают сырой <see cref="Log2"/>, поэтому невалидное
/// значение по умолчанию меньше любого валидного.
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
public readonly struct DevicePortSize : IEquatable<DevicePortSize>, IComparable<DevicePortSize>
{
    /// <summary>Минимальная степень двойки: 3 (8 байт).</summary>
    public const byte MinLog2 = 3;

    /// <summary>Максимальная степень двойки: 8 (256 байт).</summary>
    public const byte MaxLog2 = 8;

    /// <summary>Степень двойки, задающая размер в байтах.</summary>
    public byte Log2 { get; }

    /// <summary>
    /// Размер в байтах: <c>1UL &lt;&lt; Log2</c>.
    /// Для невалидного значения по умолчанию возвращает 1.
    /// </summary>
    public ulong Bytes => 1UL << Log2;

    /// <summary>Показывает, попадает ли <see cref="Log2"/> в диапазон 3…8.</summary>
    public bool IsValid => Log2 is >= MinLog2 and <= MaxLog2;

    // Приватный конструктор — деталь реализации, XML не нужен.
    // Достаточно обычного комментария для сопровождающих.
    private DevicePortSize(byte log2) => Log2 = log2;

    /// <summary>Размер порта 8 байт (2^3).</summary>
    public static DevicePortSize B8 => new(3);

    /// <summary>Размер порта 16 байт (2^4).</summary>
    public static DevicePortSize B16 => new(4);
    /// <summary>Размер порта 32 байт (2^5).</summary>
    public static DevicePortSize B32 => new(5);
    /// <summary>Размер порта 64 байт (2^6).</summary
    public static DevicePortSize B64 => new(6);
    /// <summary>Размер порта 128 байт (2^7).</summary
    public static DevicePortSize B128 => new(7);
    /// <summary>Размер порта 256 байт (2^8).</summary
    public static DevicePortSize B256 => new(8);

    public bool Equals(DevicePortSize other) => Log2 == other.Log2;
    public override bool Equals(object? obj) => obj is DevicePortSize p && Equals(p);
    public override int GetHashCode() => Log2;
    public int CompareTo(DevicePortSize other) => Log2.CompareTo(other.Log2);
    public override string ToString() => IsValid ? $"{Bytes} B (2^{Log2})" : "DevicePortSize(invalid)";

    public static bool operator ==(DevicePortSize a, DevicePortSize b) => a.Equals(b);
    public static bool operator !=(DevicePortSize a, DevicePortSize b) => !a.Equals(b);
    public static bool operator <(DevicePortSize a, DevicePortSize b) => a.Log2 < b.Log2;
    public static bool operator >(DevicePortSize a, DevicePortSize b) => a.Log2 > b.Log2;
    public static bool operator <=(DevicePortSize a, DevicePortSize b) => a.Log2 <= b.Log2;
    public static bool operator >=(DevicePortSize a, DevicePortSize b) => a.Log2 >= b.Log2;
}
