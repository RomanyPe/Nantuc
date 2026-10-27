using Nantuc.Kernel.Common;
using System.Runtime.CompilerServices;

namespace Nantuc.VmApplication.Emulator;

/// <summary>
/// Zero-alloc структура, вместо массива <see cref="Array"/> имеет заинлайненный массив
/// </summary>
public readonly struct SnapshotRegisters : IEquatable<SnapshotRegisters>
{
    [InlineArray(32)]
    private struct RegisterArray { public ulong _v0; }

    private readonly RegisterArray _values;
    /// <summary>
    /// Конструктор
    /// </summary>
    /// <param name="values"><see cref="Span{T}"/> регистров</param>
    public SnapshotRegisters(Span<ulong> values)
    {
        _values = new();
        for (int i = 0; i < values.Length; i++)
            _values[i] = values[i];
    }

    /// <summary>
    /// Метод для создания нового массива из Zero-alloc структуры
    /// </summary>
    /// <returns></returns>
    public ulong[] AsArray()
    {
        var arr = new ulong[32];
        for (int i = 0; i < 32; i++) arr[i] = _values[i];
        return arr;
    }

    /// <summary>
    /// Индексатор
    /// </summary>
    /// <param name="r"><see cref="RegType"/>, чтобы нельзя было ошибиться с индексацией регистров в массиве</param>
    /// <returns></returns>
    public ulong this[RegType r]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _values![r.Int()];
    }
    /// <summary>
    /// Количество регистров, <![CDATA[static]]> так как количество регистров константно
    /// </summary>
    public const int Count = 32;

    /// <summary>
    /// Метод для создания <see cref="IEnumerable{T}"/> из регистров
    /// </summary>
    /// <returns><see cref="IEnumerable{T}"/> из регистров где <![CDATA[Type]]> это <see cref="RegType"/> а  <![CDATA[Value]]> это <see cref="ulong"/></returns>
    public IEnumerable<(RegType Type, ulong Value)> Enumerate()
    {
        for (int i = 0; i < 32; i++)
            yield return ((RegType)i, _values[i]);
    }

    public bool Equals(SnapshotRegisters other)
    {
        for(RegType i = 0; i < RegType.rIP + 1; i++)
        {
            if (this[i] != other[i])
                return false;
        }
        return true;
    }

    public override int GetHashCode() 
    { 
        var h = new HashCode(); 
        for (int i = 0; i < 32; i++) 
            h.Add(_values[i]); 
        return h.ToHashCode(); 
    }
    public override string ToString() =>
        string.Join(", ", Enumerate().Select(e => $"{e.Type}={e.Value:X}"));

    public static bool operator ==(SnapshotRegisters left, SnapshotRegisters right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(SnapshotRegisters left, SnapshotRegisters right)
    {
        return !(left == right);
    }

    public override bool Equals(object? obj)
    {
        return obj is SnapshotRegisters registers && Equals(registers);
    }
}