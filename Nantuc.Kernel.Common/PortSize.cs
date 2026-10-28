using System.Diagnostics;

namespace Nantuc.Kernel.Common;

/// <summary>
/// Размер порта как степень двойки: от 64 байт (2^6) до 512 КиБ (2^19).
/// </summary>
/// <remarks>
/// <para>
/// Публичного конструктора с произвольным <c>Log2</c> нет — используйте
/// фабричные свойства (<see cref="B64"/>, <see cref="KB1"/>, <see cref="KB4"/>
/// и т.д.).
/// </para>
/// <para>
/// Значение по умолчанию (<see langword="default"/> или <c>new PortSize()</c>)
/// невалидно: <see cref="Log2"/> = 0, <see cref="IsValid"/> = false,
/// <see cref="Bytes"/> возвращает 1.
/// </para>
/// <para>
/// Операторы сравнения и <see cref="CompareTo"/> сравнивают сырой
/// <see cref="Log2"/>, поэтому невалидное значение по умолчанию меньше
/// любого валидного.
/// </para>
/// <para>
/// Структура полностью управляемая и совместима с WebAssembly, включая AOT.
/// </para>
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
public readonly struct PortSize : IEquatable<PortSize>, IComparable<PortSize>
{
    /// <summary>Минимальная степень двойки: 6 (64 байта).</summary>
    public const byte MinLog2 = 6;

    /// <summary>Максимальная степень двойки: 19 (512 КиБ).</summary>
    public const byte MaxLog2 = 19;

    /// <summary>Степень двойки, задающая размер в байтах.</summary>
    public byte Log2 { get; }

    /// <summary>
    /// Размер в байтах: <c>1UL &lt;&lt; Log2</c>.
    /// Для невалидного значения по умолчанию возвращает 1.
    /// </summary>
    /// <remarks>
    /// Для <c>Log2 &gt;= 64</c> сдвиг в C# маскируется по модулю 64,
    /// поэтому результат может быть неожиданным. Проверяйте
    /// <see cref="IsValid"/> перед использованием.
    /// </remarks>
    public ulong Bytes => 1UL << Log2;

    /// <summary>
    /// Показывает, попадает ли <see cref="Log2"/> в диапазон
    /// <see cref="MinLog2"/>…<see cref="MaxLog2"/>.
    /// Для <see langword="default"/> возвращает <see langword="false"/>.
    /// </summary>
    public bool IsValid => Log2 is >= MinLog2 and <= MaxLog2;

    /// <summary>
    /// Создаёт значение с указанной степенью двойки без проверки диапазона.
    /// Используется только фабричными свойствами.
    /// </summary>
    private PortSize(byte log2) => Log2 = log2;

    /// <summary>Размер 64 байта (2^6).</summary>
    public static PortSize B64 => new(6);

    /// <summary>Размер 128 байт (2^7).</summary>
    public static PortSize B128 => new(7);

    /// <summary>Размер 256 байт (2^8).</summary>
    public static PortSize B256 => new(8);

    /// <summary>Размер 512 байт (2^9).</summary>
    public static PortSize B512 => new(9);

    /// <summary>Размер 1 КиБ (2^10).</summary>
    public static PortSize KB1 => new(10);
    /// <summary>Размер 2 КиБ (2^11).</summary>
    public static PortSize KB2 => new(11);

    /// <summary>Размер 4 КиБ (2^12).</summary>
    public static PortSize KB4 => new(12);

    /// <summary>Размер 8 КиБ (2^13).</summary>
    public static PortSize KB8 => new(13);

    /// <summary>Размер 16 КиБ (2^14).</summary>
    public static PortSize KB16 => new(14);
    /// <summary>Размер 32 КиБ (2^15).</summary>
    public static PortSize KB32 => new(15);

    /// <summary>Размер 64 КиБ (2^16).</summary>
    public static PortSize KB64 => new(16);

    /// <summary>Размер 128 КиБ (2^17).</summary>
    public static PortSize KB128 => new(17);

    /// <summary>Размер 256 КиБ (2^18).</summary>
    public static PortSize KB256 => new(18);

    /// <summary>Размер 512 КиБ (2^19).</summary>
    public static PortSize KB512 => new(19);

    /// <summary>
    /// Сравнивает два значения по полю <see cref="Log2"/>.
    /// Два невалидных значения с одинаковым <see cref="Log2"/> равны.
    /// </summary>
    public bool Equals(PortSize other) => Log2 == other.Log2;

    /// <summary>
    /// Сравнивает с другим объектом; возвращает <see langword="true"/>,
    /// если <paramref name="obj"/> — <see cref="PortSize"/> с тем же
    /// <see cref="Log2"/>.
    /// </summary>
    public override bool Equals(object? obj) => obj is PortSize p && Equals(p);

    /// <summary>Возвращает хеш-код на основе <see cref="Log2"/>.</summary>
    public override int GetHashCode() => Log2;

    /// <summary>
    /// Сравнивает по сырому <see cref="Log2"/>.
    /// Невалидное значение по умолчанию (Log2 = 0) меньше любого валидного.
    /// </summary>
    public int CompareTo(PortSize other) => Log2.CompareTo(other.Log2);

    /// <summary>
    /// Возвращает строку вида <c>"64 B (2^6)"</c> для валидного значения
    /// или <c>"PortSize(invalid)"</c> для невалидного.
    /// </summary>
    public override string ToString() =>
        IsValid ? $"{Bytes} B (2^{Log2})" : "PortSize(invalid)";

    /// <summary>Проверяет равенство по <see cref="Log2"/>.</summary>
    public static bool operator ==(PortSize a, PortSize b) => a.Equals(b);

    /// <summary>Проверяет неравенство по <see cref="Log2"/>.</summary>
    public static bool operator !=(PortSize a, PortSize b) => !a.Equals(b);

    /// <summary>
    /// Сравнивает сырые <see cref="Log2"/>: <see langword="true"/>,
    /// если <paramref name="a"/>.Log2 &lt; <paramref name="b"/>.Log2.
    /// Невалидное значение по умолчанию меньше любого валидного.
    /// </summary>
    public static bool operator <(PortSize a, PortSize b) => a.Log2 < b.Log2;

    /// <summary>
    /// Сравнивает сырые <see cref="Log2"/>: <see langword="true"/>,
    /// если <paramref name="a"/>.Log2 &gt; <paramref name="b"/>.Log2.
    /// </summary>
    public static bool operator >(PortSize a, PortSize b) => a.Log2 > b.Log2;

    /// <summary>
    /// Сравнивает сырые <see cref="Log2"/>: <see langword="true"/>,
    /// если <paramref name="a"/>.Log2 &lt;= <paramref name="b"/>.Log2.
    /// </summary>
    public static bool operator <=(PortSize a, PortSize b) => a.Log2 <= b.Log2;

    /// <summary>
    /// Сравнивает сырые <see cref="Log2"/>: <see langword="true"/>,
    /// если <paramref name="a"/>.Log2 &gt;= <paramref name="b"/>.Log2.
    /// </summary>
    public static bool operator >=(PortSize a, PortSize b) => a.Log2 >= b.Log2;
}