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
    /// <summary>Размер порта 64 байт (2^6).</summary>
    public static DevicePortSize B64 => new(6);
    /// <summary>Размер порта 128 байт (2^7).</summary>
    public static DevicePortSize B128 => new(7);
    /// <summary>Размер порта 256 байт (2^8).</summary>
    public static DevicePortSize B256 => new(8);

    /// <summary>
    /// Определяет равенство двух <see cref="DevicePortSize"/> по полю <see cref="Log2"/>.
    /// </summary>
    /// <param name="other">Значение для сравнения.</param>
    /// <returns>
    /// <see langword="true"/>, если <see cref="Log2"/> обоих значений совпадают;
    /// иначе <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Сравниваются сырые значения <see cref="Log2"/>, а не результаты
    /// <see cref="IsValid"/>. Два невалидных значения с одинаковым
    /// <see cref="Log2"/> (например, два <c>default</c>) считаются равными.
    /// </remarks>
    public bool Equals(DevicePortSize other) => Log2 == other.Log2;

    /// <summary>
    /// Определяет равенство текущего значения с произвольным объектом.
    /// </summary>
    /// <param name="obj">Объект для сравнения.</param>
    /// <returns>
    /// <see langword="true"/>, если <paramref name="obj"/> является
    /// <see cref="DevicePortSize"/> с тем же <see cref="Log2"/>;
    /// иначе <see langword="false"/>.
    /// </returns>
    public override bool Equals(object? obj) => obj is DevicePortSize p && Equals(p);

    /// <summary>
    /// Возвращает хеш-код, построенный по значению <see cref="Log2"/>.
    /// </summary>
    /// <returns>Значение <see cref="Log2"/> в виде <see cref="int"/>.</returns>
    public override int GetHashCode() => Log2;

    /// <summary>
    /// Сравнивает текущее значение с другим по полю <see cref="Log2"/>.
    /// </summary>
    /// <param name="other">Значение для сравнения.</param>
    /// <returns>
    /// Отрицательное число, если <see cref="Log2"/> текущего значения меньше;
    /// ноль, если равны; положительное число, если больше.
    /// </returns>
    /// <remarks>
    /// Сравнение идёт по сырому <see cref="Log2"/>, поэтому невалидное
    /// значение по умолчанию (<see cref="Log2"/> = 0) меньше любого валидного
    /// (у которого <see cref="Log2"/> ≥ <see cref="MinLog2"/>).
    /// </remarks>
    public int CompareTo(DevicePortSize other) => Log2.CompareTo(other.Log2);

    /// <summary>
    /// Возвращает строковое представление размера порта.
    /// </summary>
    /// <returns>
    /// Для валидного значения — строка вида <c>"128 B (2^7)"</c>;
    /// для невалидного — <c>"DevicePortSize(invalid)"</c>.
    /// </returns>
    public override string ToString() => IsValid ? $"{Bytes} B (2^{Log2})" : "DevicePortSize(invalid)";

    /// <summary>
    /// Определяет равенство двух <see cref="DevicePortSize"/>.
    /// </summary>
    /// <param name="a">Левое значение.</param>
    /// <param name="b">Правое значение.</param>
    /// <returns>
    /// <see langword="true"/>, если <see cref="Log2"/> значений совпадают;
    /// иначе <see langword="false"/>.
    /// </returns>
    public static bool operator ==(DevicePortSize a, DevicePortSize b) => a.Equals(b);

    /// <summary>
    /// Определяет неравенство двух <see cref="DevicePortSize"/>.
    /// </summary>
    /// <param name="a">Левое значение.</param>
    /// <param name="b">Правое значение.</param>
    /// <returns>
    /// <see langword="true"/>, если <see cref="Log2"/> значений различаются;
    /// иначе <see langword="false"/>.
    /// </returns>
    public static bool operator !=(DevicePortSize a, DevicePortSize b) => !a.Equals(b);

    /// <summary>
    /// Оператор «меньше»: сравнивает <see cref="Log2"/> левого и правого значений.
    /// </summary>
    /// <param name="a">Левое значение.</param>
    /// <param name="b">Правое значение.</param>
    /// <returns>
    /// <see langword="true"/>, если <c>a.Log2 &lt; b.Log2</c>;
    /// иначе <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Сравниваются сырые <see cref="Log2"/>, поэтому невалидное значение
    /// по умолчанию (<see cref="Log2"/> = 0) меньше любого валидного.
    /// </remarks>
    public static bool operator <(DevicePortSize a, DevicePortSize b) => a.Log2 < b.Log2;

    /// <summary>
    /// Оператор «больше»: сравнивает <see cref="Log2"/> левого и правого значений.
    /// </summary>
    /// <param name="a">Левое значение.</param>
    /// <param name="b">Правое значение.</param>
    /// <returns>
    /// <see langword="true"/>, если <c>a.Log2 &gt; b.Log2</c>;
    /// иначе <see langword="false"/>.
    /// </returns>
    public static bool operator >(DevicePortSize a, DevicePortSize b) => a.Log2 > b.Log2;

    /// <summary>
    /// Оператор «меньше или равно»: сравнивает <see cref="Log2"/> левого и правого значений.
    /// </summary>
    /// <param name="a">Левое значение.</param>
    /// <param name="b">Правое значение.</param>
    /// <returns>
    /// <see langword="true"/>, если <c>a.Log2 &lt;= b.Log2</c>;
    /// иначе <see langword="false"/>.
    /// </returns>
    public static bool operator <=(DevicePortSize a, DevicePortSize b) => a.Log2 <= b.Log2;

    /// <summary>
    /// Оператор «больше или равно»: сравнивает <see cref="Log2"/> левого и правого значений.
    /// </summary>
    /// <param name="a">Левое значение.</param>
    /// <param name="b">Правое значение.</param>
    /// <returns>
    /// <see langword="true"/>, если <c>a.Log2 &gt;= b.Log2</c>;
    /// иначе <see langword="false"/>.
    /// </returns>
    public static bool operator >=(DevicePortSize a, DevicePortSize b) => a.Log2 >= b.Log2;
}
