using Nantuc.Kernel.Common;
using Nantuc.Kernel.Contracts;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nantuc.Kernel.RamSystem;

/// <summary>
/// Нативный буфер ОЗУ: выделяет неуправляемую память и предоставляет типизированный
/// доступ к ней в порядке little-endian.
/// </summary>
/// <remarks>
/// <para>
/// Реализует шаблон <see cref="IDisposable"/>; после <see cref="Dispose"/> доступ
/// к буферу (включая индексатор и <c>Read*</c>/<c>Write*</c>) приводит к
/// неопределённому поведению — <see cref="IsDisposed"/> позволяет проверить состояние.
/// </para>
/// <para>
/// Методы <c>Read*</c>/<c>Write*</c> и индексатор не выполняют проверок границ.
/// Проверки вынесены на уровень <see cref="MemoryBus"/>.
/// </para>
/// </remarks>
public unsafe sealed class NativeMemoryBuffer : IDisposable
{

    private readonly NativeReadOnlyView _view;

    private byte* _ptrRam;
    private readonly nuint _sizeRam;
    private bool _disposed = false;

    /// <summary>
    /// Признак того, что буфер уже освобождён.
    /// </summary>
    public bool IsDisposed => Volatile.Read(ref _disposed);

    /// <summary>
    /// Размер буфера в байтах.
    /// </summary>
    public nuint Length => _sizeRam;

    /// <summary>
    /// Прямой доступ к содержимому буфера только для чтения.
    /// </summary>
    public INativeReadOnlyBuffer NativeReadOnlyBuffer => _view;

    /// <summary>
    /// Выделяет занулённый нативный буфер заданного размера.
    /// </summary>
    /// <param name="sizeRam">Размер буфера.</param>
    /// <exception cref="OutOfMemoryException">Если выделить память не удалось.</exception>
    public NativeMemoryBuffer(RamSize sizeRam)
    {
        nuint ram = (nuint)sizeRam.Bytes;


        byte* ptr = (byte*)NativeMemory.AllocZeroed(ram);
        if (ptr == null)
            throw new OutOfMemoryException(
                $"Failed to allocate {ram} bytes for RAM.");


        _sizeRam = ram;
        _ptrRam = ptr;

        _view = new NativeReadOnlyView(this);
    }

    /// <summary>
    /// Читает 16-битное значение (little-endian на любой платформе).
    /// </summary>
    /// <param name="address">Смещение в буфере.</param>
    /// <returns>Значение.</returns>
    public ushort ReadUInt16(ulong address)
    {
        var value = Unsafe.ReadUnaligned<ushort>(_ptrRam + address);
        return BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
    }

    /// <summary>
    /// Читает 32-битное значение (little-endian на любой платформе).
    /// </summary>
    /// <param name="address">Смещение в буфере.</param>
    /// <returns>Значение.</returns>
    public uint ReadUInt32(ulong address)
    {
        var value = Unsafe.ReadUnaligned<uint>(_ptrRam + address);
        return BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
    }

    /// <summary>
    /// Читает 64-битное значение (little-endian на любой платформе).
    /// </summary>
    /// <param name="address">Смещение в буфере.</param>
    /// <returns>Значение.</returns>
    public ulong ReadUInt64(ulong address)
    {
        var value = Unsafe.ReadUnaligned<ulong>(_ptrRam + address);
        return BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
    }


    /// <summary>
    /// Записывает 16-битное значение (little-endian на любой платформе).
    /// </summary>
    /// <param name="address">Смещение в буфере.</param>
    /// <param name="value">Значение.</param>
    public void WriteUInt16(ulong address, ushort value)
    {
        if (!BitConverter.IsLittleEndian)
            value = BinaryPrimitives.ReverseEndianness(value);
        Unsafe.WriteUnaligned(_ptrRam + address, value);
    }

    /// <summary>
    /// Записывает 32-битное значение (little-endian на любой платформе).
    /// </summary>
    /// <param name="address">Смещение в буфере.</param>
    /// <param name="value">Значение.</param>
    public void WriteUInt32(ulong address, uint value)
    {
        if (!BitConverter.IsLittleEndian)
            value = BinaryPrimitives.ReverseEndianness(value);
        Unsafe.WriteUnaligned(_ptrRam + address, value);
    }

    /// <summary>
    /// Записывает 64-битное значение (little-endian на любой платформе).
    /// </summary>
    /// <param name="address">Смещение в буфере.</param>
    /// <param name="value">Значение.</param>
    public void WriteUInt64(ulong address, ulong value)
    {
        if (!BitConverter.IsLittleEndian)
            value = BinaryPrimitives.ReverseEndianness(value);
        Unsafe.WriteUnaligned(_ptrRam + address, value);
    }

    /// <summary>
    /// Индексированный доступ к байту буфера без проверки границ.
    /// </summary>
    /// <param name="index">Смещение в байтах.</param>
    /// <returns>Значение байта.</returns>
    public byte this[ulong index]
    {
        get => *(_ptrRam + index);
        set => *(_ptrRam + index) = value;
    }

    /// <summary>
    /// Заполняет буфер нулями.
    /// </summary>
    public void Clear() => NativeMemory.Clear(_ptrRam, _sizeRam);

    /// <summary>
    /// Освобождает нативную память.
    /// </summary>
    /// <remarks>Повторный вызов безопасен.</remarks>
    public void Dispose()
    {
        if (!Volatile.Read(ref _disposed))
        {
            Volatile.Write(ref _disposed, true);
            NativeMemory.Free(_ptrRam);
            _ptrRam = null;
        }
    }

    /// <summary>
    /// Копирует байты программы в буфер по указанному смещению.
    /// </summary>
    /// <param name="prog">Байты программы.</param>
    /// <param name="startProg">Смещение в буфере.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Если диапазон выходит за пределы буфера.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LoadProgram(ReadOnlySpan<byte> prog, ulong startProg)
    {
        if (prog.Length == 0) return;

        if (startProg > _sizeRam || (ulong)prog.Length > _sizeRam - startProg)
            throw new ArgumentOutOfRangeException(nameof(startProg));

        fixed (byte* src = prog)
            NativeMemory.Copy(src, _ptrRam + startProg, (nuint)prog.Length);
    }
}