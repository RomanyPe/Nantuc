using Nantuc.Kernel.Common;
using Nantuc.Kernel.Contracts;

namespace Nantuc.Kernel.RamSystem;

/// <summary>
/// Шина ОЗУ устройства: типизированные проверяемые и непроверяемые операции чтения/записи
/// поверх <see cref="NativeMemoryBuffer"/>.
/// </summary>
/// <remarks>
/// <para>
/// Проверяемые методы (<c>ReadInt*LE</c>, <c>WriteInt*LE</c>) не бросают исключений:
/// при ошибке они возвращают <c>0</c> и сохраняют статус последней ошибки, который
/// можно получить через <see cref="UpdateAndReadStatus"/>. Ошибка сбрасывается при
/// следующем чтении статуса.
/// </para>
/// <para>
/// UnSafe-варианты не выполняют проверок границ и выравнивания — используются
/// на горячих путях процессора; корректность адреса целиком на совести вызывающего.
/// </para>
/// </remarks>
public class MemoryBus(RamSize size) : IDisposable
{
    private NativeMemoryBuffer _memory = new(size);

    /// <summary>
    /// Размер ОЗУ в байтах.
    /// </summary>
    public ulong Length => _memory.Length;

    /// <summary>
    /// Прямой доступ к ОЗУ только для чтения через нативное представление.
    /// </summary>
    public INativeReadOnlyBuffer NativeReadOnlyBuffer => _memory.NativeReadOnlyBuffer;

    private ulong _faultData = 0;
    private BiosStatus _lastStatus = BiosStatus.Success;

    /// <summary>
    /// Данные, связанные с последней ошибкой доступа к памяти (обычно проблемный адрес).
    /// </summary>
    /// <remarks>Актуально только сразу после неудачной проверяемой операции.</remarks>
    public ulong FaultData => _faultData;

    /// <summary>
    /// Возвращает статус последней проверяемой операции и сбрасывает его в <see cref="BiosStatus.Success"/>.
    /// </summary>
    /// <returns>Статус предыдущей операции.</returns>
    public BiosStatus UpdateAndReadStatus()
    {
        var lastStatus = _lastStatus;
        _lastStatus = BiosStatus.Success;
        return lastStatus;
    }

    private byte SetErrorStatus(BiosStatus status, ulong data)
    {
        _lastStatus = status;
        _faultData = data;
        return 0;
    }

    /// <summary>
    /// Загружает байты программы в ОЗУ по указанному адресу.
    /// </summary>
    /// <param name="prog">Байты программы.</param>
    /// <param name="startAdresProg">Адрес размещения.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Если диапазон выходит за границы ОЗУ.
    /// </exception>
    public void LoadProgram(ReadOnlySpan<byte> prog, ulong startAdresProg)
    {
        _memory.LoadProgram(prog, startAdresProg);
    }

    /// <summary>
    /// Проверяемое чтение 8-битного значения (Int8).
    /// </summary>
    /// <param name="address">Адрес в виртуальной памяти.</param>
    /// <returns>Значение или <c>0</c> при ошибке (см. <see cref="UpdateAndReadStatus"/>).</returns>
    public byte ReadInt8LE(ulong address)
    {
        return address < _memory.Length
            ? _memory[address]
            : SetErrorStatus(BiosStatus.SegmentationFault, address);
    }

    /// <summary>
    /// Проверяемое чтение 16-битного значения (Int16).
    /// </summary>
    /// <param name="address">Адрес в виртуальной памяти.</param>
    /// <returns>Значение или <c>0</c> при ошибке (см. <see cref="UpdateAndReadStatus"/>).</returns>
    /// <remarks>Требует выравнивания по 2 байта; иначе — <see cref="BiosStatus.AlignmentFault"/>.</remarks>
    public ushort ReadInt16LE(ulong address)
    {
        if ((address & 0x01) != 0) return SetErrorStatus(BiosStatus.AlignmentFault, address);

        return address <= _memory.Length - 2
            ? _memory.ReadUInt16(address)
            : SetErrorStatus(BiosStatus.SegmentationFault, address);
    }

    /// <summary>
    /// Проверяемое чтение 32-битного значения (Int32).
    /// </summary>
    /// <param name="address">Адрес в виртуальной памяти.</param>
    /// <returns>Значение или <c>0</c> при ошибке (см. <see cref="UpdateAndReadStatus"/>).</returns>
    /// <remarks>Требует выравнивания по 4 байта.</remarks>
    public uint ReadInt32LE(ulong address)
    {
        if ((address & 0x03) != 0) return SetErrorStatus(BiosStatus.AlignmentFault, address);

        return address <= _memory.Length - 4
            ? _memory.ReadUInt32(address)
            : SetErrorStatus(BiosStatus.SegmentationFault, address);
    }

    /// <summary>
    /// Проверяемое чтение 64-битного значения (Int64).
    /// </summary>
    /// <param name="address">Адрес в виртуальной памяти.</param>
    /// <returns>Значение или <c>0</c> при ошибке (см. <see cref="UpdateAndReadStatus"/>).</returns>
    /// <remarks>Требует выравнивания по 8 байт.</remarks>
    public ulong ReadInt64LE(ulong address)
    {
        if ((address & 0x07) != 0) return SetErrorStatus(BiosStatus.AlignmentFault, address);

        return address <= _memory.Length - 8
            ? _memory.ReadUInt64(address)
            : SetErrorStatus(BiosStatus.SegmentationFault, address);
    }

    /// <summary>
    /// Проверяемая запись 8-битного значения (Int8).
    /// </summary>
    /// <param name="address">Адрес в виртуальной памяти.</param>
    /// <param name="value">Записываемое значение.</param>
    public void WriteInt8LE(ulong address, byte value)
    {
        if (address >= _memory.Length)
        {
            SetErrorStatus(BiosStatus.SegmentationFault, address);
            return;
        }
        _memory[address] = value;
    }

    /// <summary>
    /// Проверяемая запись 16-битного значения (Int16).
    /// </summary>
    /// <param name="address">Адрес в виртуальной памяти.</param>
    /// <param name="value">Записываемое значение.</param>
    /// <remarks>Требует выравнивания по 2 байта; иначе — <see cref="BiosStatus.AlignmentFault"/>.</remarks>
    public void WriteInt16LE(ulong address, ushort value)
    {
        if ((address & 0x01) != 0)
        {
            SetErrorStatus(BiosStatus.AlignmentFault, address);
            return;
        }

        if (address > _memory.Length - 2)
        {
            SetErrorStatus(BiosStatus.SegmentationFault, address);
            return;
        }

        _memory.WriteUInt16(address, value);
    }


    /// <summary>
    /// Проверяемая запись 32-битного значения (Int32).
    /// </summary>
    /// <param name="address">Адрес в виртуальной памяти.</param>
    /// <param name="value">Записываемое значение.</param>
    /// <remarks>Требует выравнивания по 4 байта.</remarks>
    public void WriteInt32LE(ulong address, uint value)
    {
        if ((address & 0x03) != 0)
        {
            SetErrorStatus(BiosStatus.AlignmentFault, address);
            return;
        }
        if (address > _memory.Length - 4)
        {
            SetErrorStatus(BiosStatus.SegmentationFault, address);
            return;
        }

        _memory.WriteUInt32(address, value);
    }

    /// <summary>
    /// Проверяемая запись 64-битного значения (Int64).
    /// </summary>
    /// <param name="address">Адрес в виртуальной памяти.</param>
    /// <param name="value">Записываемое значение.</param>
    /// <remarks>Требует выравнивания по 8 байт.</remarks>
    public void WriteInt64LE(ulong address, ulong value)
    {
        if ((address & 0x07) != 0)
        {
            SetErrorStatus(BiosStatus.AlignmentFault, address);
            return;
        }
        if (address > _memory.Length - 8)
        {
            SetErrorStatus(BiosStatus.SegmentationFault, address);
            return;
        }

        _memory.WriteUInt64(address, value);
    }

    /// <summary>
    /// Непроверяемая запись 8-битного значения (без контроля границ и выравнивания).
    /// </summary>
    /// <param name="address">Адрес в ОЗУ.</param>
    /// <param name="value">Записываемое значение.</param>
    public void WriteInt8LEUnSafe(ulong address, byte value) => _memory[address] = value;

    /// <summary>
    /// Непроверяемая запись 16-битного значения.
    /// </summary>
    /// <param name="address">Адрес в ОЗУ.</param>
    /// <param name="value">Записываемое значение.</param>
    public void WriteInt16LEUnSafe(ulong address, ushort value) => _memory.WriteUInt16(address, value);

    /// <summary>
    /// Непроверяемая запись 32-битного значения.
    /// </summary>
    /// <param name="address">Адрес в ОЗУ.</param>
    /// <param name="value">Записываемое значение.</param>
    public void WriteInt32LEUnSafe(ulong address, uint value) => _memory.WriteUInt32(address, value);

    /// <summary>
    /// Непроверяемая запись 64-битного значения.
    /// </summary>
    /// <param name="address">Адрес в ОЗУ.</param>
    /// <param name="value">Записываемое значение.</param>
    public void WriteInt64LEUnSafe(ulong address, ulong value) => _memory.WriteUInt64(address, value);

    /// <summary>
    /// Непроверяемое чтение 8-битного значения.
    /// </summary>
    /// <param name="address">Адрес в ОЗУ.</param>
    /// <returns>Значение.</returns>
    public byte ReadInt8LEUnSafe(ulong address) => _memory[address];

    /// <summary>
    /// Непроверяемое чтение 16-битного значения.
    /// </summary>
    /// <param name="address">Адрес в ОЗУ.</param>
    /// <returns>Значение.</returns>
    public ushort ReadInt16LEUnSafe(ulong address) => _memory.ReadUInt16(address);

    /// <summary>
    /// Непроверяемое чтение 32-битного значения.
    /// </summary>
    /// <param name="address">Адрес в ОЗУ.</param>
    /// <returns>Значение.</returns>
    public uint ReadInt32LEUnSafe(ulong address) => _memory.ReadUInt32(address);

    /// <summary>
    /// Непроверяемое чтение 64-битного значения.
    /// </summary>
    /// <param name="address">Адрес в ОЗУ.</param>
    /// <returns>Значение.</returns>
    public ulong ReadInt64LEUnSafe(ulong address) => _memory.ReadUInt64(address);


    // ==============================
    //              API 
    // ==============================

    /// <summary>
    /// Очищает ОЗУ (заполняет нулями).
    /// </summary>
    public void ClearMemory() => _memory.Clear();

    private int _disposed;


    /// <summary>
    /// Освобождает нативный буфер ОЗУ.
    /// </summary>
    /// <remarks>Повторный вызов безопасен; потокобезопасен.</remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        if (_memory != null)
        {
            _memory.Dispose();
            _memory = null!;
        }

        GC.SuppressFinalize(this);
    }
}