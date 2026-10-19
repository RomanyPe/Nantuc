using Nantuc.Kernel.Common;
using Nantuc.Kernel.Contracts;

namespace Nantuc.Kernel.RamSystem;

public class MemoryBus(RamSize size) : IDisposable
{
    private NativeMemoryBuffer _memory = new(size);

    public ulong Length => _memory.Length;
    public INativeReadOnlyBuffer NativeReadOnlyBuffer => _memory.NativeReadOnlyBuffer;

    private ulong _faultData = 0;
    private BiosStatus _lastStatus = BiosStatus.Success;
    public ulong FaultData => _faultData;

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

    public void LoadProgram(ReadOnlySpan<byte> prog, ulong startAdresProg)
    {
        _memory.LoadProgram(prog, startAdresProg);
    }

    /// <summary>
    /// Метод для чтения 8 битового целого числа (Int8)
    /// </summary>
    /// <param name="address"> Адресс в виртуальной памяти </param>
    /// <returns> Успешность операции, при возврате не BiosStatus.Success происходит исключение и остановка работы биоса </returns>
    public byte ReadInt8LE(ulong address)
    {
        return address < _memory.Length
            ? _memory[address]
            : SetErrorStatus(BiosStatus.SegmentationFault, address);
    }

    /// <summary>
    /// Метод для чтения 16 битового целого числа (Int16)
    /// </summary>
    /// <param name="address"> Адресс в виртуальной памяти </param>
    /// <returns> Успешность операции, при возврате не BiosStatus.Success происходит исключение и остановка работы биоса </returns>

    public ushort ReadInt16LE(ulong address)
    {
        if ((address & 0x01) != 0) return SetErrorStatus(BiosStatus.AlignmentFault, address);

        return address <= _memory.Length - 2
            ? _memory.ReadUInt16(address)
            : SetErrorStatus(BiosStatus.SegmentationFault, address);
    }

    /// <summary>
    /// Метод для чтения 32 битового целого числа (Int32)
    /// </summary>
    /// <param name="address"> Адресс в виртуальной памяти </param>
    /// <returns> Успешность операции, при возврате не BiosStatus.Success происходит исключение и остановка работы биоса </returns>
    public uint ReadInt32LE(ulong address)
    {
        if ((address & 0x03) != 0) return SetErrorStatus(BiosStatus.AlignmentFault, address);

        return address <= _memory.Length - 4
            ? _memory.ReadUInt32(address)
            : SetErrorStatus(BiosStatus.SegmentationFault, address);
    }

    /// <summary>
    /// Метод для чтения 64 битового целого числа (Int64)
    /// </summary>
    /// <param name="address"> Адресс в виртуальной памяти </param>
    /// <returns> Успешность операции, при возврате не BiosStatus.Success происходит исключение и остановка работы биоса </returns>

    public ulong ReadInt64LE(ulong address)
    {
        if ((address & 0x07) != 0) return SetErrorStatus(BiosStatus.AlignmentFault, address);

        return address <= _memory.Length - 8
            ? _memory.ReadUInt64(address)
            : SetErrorStatus(BiosStatus.SegmentationFault, address);
    }

    /// <summary>
    /// Метод для записи в память 8 битового целого числа (Int8)
    /// </summary>
    /// <param name="address"> Адрес в виртуальной памяти </param>
    /// <param name="value"> 16 битовое целое число (Int8) </param>
    /// <returns> Успешность операции, при возврате False программа остановится и выведится ошибка </returns>
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
    /// Метод для записи в память 16 битового целого числа (Int16)
    /// </summary>
    /// <param name="address"> Адрес в виртуальной памяти </param>
    /// <param name="value"> 16 битовое целое число (Int16) </param>
    /// <returns> Успешность операции, при возврате False программа остановится и выведится ошибка </returns>
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
    /// Метод для записи в память 32 битового целого числа (Int32)
    /// </summary>
    /// <param name="address"> Адрес в виртуальной памяти </param>
    /// <param name="value"> 32 битовое целое число (Int32) </param>
    /// <returns> Успешность операции, при возврате False программа остановится и выведится ошибка </returns>
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
    /// Метод для записи в память 64 битового целого числа (Int64)
    /// </summary>
    /// <param name="address"> Адрес в виртуальной памяти </param>
    /// <param name="value"> 64 битовое целое число (Int64) </param>
    /// <returns> Успешность операции, при возврате False программа остановится и выведится ошибка </returns>
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

    public void WriteInt8LEUnSafe(ulong address, byte value) => _memory[address] = value;

    public void WriteInt16LEUnSafe(ulong address, ushort value) => _memory.WriteUInt16(address, value);

    public void WriteInt32LEUnSafe(ulong address, uint value) => _memory.WriteUInt32(address, value);

    public void WriteInt64LEUnSafe(ulong address, ulong value) => _memory.WriteUInt64(address, value);

    public byte ReadInt8LEUnSafe(ulong address) => _memory[address];

    public ushort ReadInt16LEUnSafe(ulong address) => _memory.ReadUInt16(address);

    public uint ReadInt32LEUnSafe(ulong address) => _memory.ReadUInt32(address);

    public ulong ReadInt64LEUnSafe(ulong address) => _memory.ReadUInt64(address);

    
    // ==============================
    //              API 
    // ==============================

    /// <summary>
    /// API для очистки памяти
    /// </summary>
    public void ClearMemory() => _memory.Clear();

    private int _disposed;


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