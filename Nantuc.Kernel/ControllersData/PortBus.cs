using Nantuc.Kernel.Common;
using Nantuc.Kernel.Contracts;

namespace Nantuc.Kernel.ControllersData;


/// <summary>
/// Шина портов ввода-вывода: делит адресное пространство на секторы,
/// каждый из которых может быть закреплён за отдельным устройством.
/// </summary>
/// <remarks>
/// <para>
/// Адрес порта раскладывается на «сектор» (старшие биты) и «смещение внутри устройства»
/// (младшие биты). Размер сектора равен <see cref="PortsOnDevice"/> и всегда является
/// степенью двойки.
/// </para>
/// <para>
/// Потокобезопасность не гарантируется; регистрация/снятие устройств должны
/// выполняться из одного потока-координатора.
/// </para>
/// </remarks>
public sealed class PortBus : IPortBus
{
    private readonly ulong _totalPorts;
    private readonly uint _portsOnDevice;

    private readonly uint _sectorCount;
    private readonly IPortUse[] _devices;

    private readonly byte _deviceShift;
    private readonly ulong _offsetMask;

    /// <summary>
    /// Создаёт шину портов.
    /// </summary>
    /// <param name="ports">Общее количество портов (степень двойки).</param>
    /// <param name="portsOnDev">Количество портов на одно устройство (степень двойки, не больше <paramref name="ports"/>).</param>
    /// <exception cref="ArgumentException">
    /// Если <paramref name="ports"/> или <paramref name="portsOnDev"/> не инициализированы,
    /// либо <paramref name="portsOnDev"/> превышает <paramref name="ports"/>.
    /// </exception>
    public PortBus(PortSize ports, DevicePortSize portsOnDev)
    {
        if (!ports.IsValid)
            throw new ArgumentException("Uninitialized PortSize.", nameof(ports));
        if (!portsOnDev.IsValid)
            throw new ArgumentException("Uninitialized DevicePortSize.", nameof(portsOnDev));
        if (ports.Log2 < portsOnDev.Log2)
            throw new ArgumentException(
                $"{nameof(portsOnDev)} ({portsOnDev}) exceeds {nameof(ports)} ({ports}).",
                nameof(portsOnDev));

        _deviceShift = portsOnDev.Log2;
        _totalPorts = 1UL << ports.Log2;
        _portsOnDevice = 1U << portsOnDev.Log2;
        _sectorCount = 1U << (ports.Log2 - portsOnDev.Log2);
        _offsetMask = (1UL << portsOnDev.Log2) - 1UL;
        _devices = new IPortUse[_sectorCount];
    }

    /// <summary>
    /// Количество секторов (слотов) на шине; равно <see cref="TotalPorts"/> / <see cref="PortsOnDevice"/>.
    /// </summary>
    public uint SectorCount => _sectorCount;

    /// <summary>
    /// Количество портов, выделенных одному устройству.
    /// </summary>
    public uint PortsOnDevice => _portsOnDevice;

    /// <summary>
    /// Общее количество портов на шине.
    /// </summary>
    public ulong TotalPorts => _totalPorts;

    /// <summary>
    /// Проверяет, что указанный сектор существует и не занят устройством.
    /// </summary>
    /// <param name="sector">Номер сектора.</param>
    /// <returns><see langword="true"/>, если сектор свободен.</returns>
    public bool IsFreeSector(uint sector) => sector < _sectorCount && _devices[sector] == null;

    /// <summary>
    /// Регистрирует устройство на указанном секторе портов.
    /// </summary>
    /// <param name="device">Контроллер портов.</param>
    /// <param name="sector">Номер сектора.</param>
    /// <returns><see langword="true"/> при успехе; иначе <see langword="false"/> (сектор занят или вне диапазона).</returns>
    public bool RegisterDevice(IPortUse device, uint sector)
    {
        if (!IsFreeSector(sector)) return false;

        _devices[sector] = device;
        return true;
    }

    /// <summary>
    /// Освобождает указанный сектор портов.
    /// </summary>
    /// <param name="sector">Номер сектора.</param>
    /// <remarks>Если сектор вне диапазона, вызов игнорируется.</remarks>
    public void UnregisterDevice(uint sector)
    {
        if (sector < _sectorCount)
            _devices[sector] = null!;
    }

    private (IPortUse? device, ulong offset) ResolveAddress(ulong address)
    {
        uint sector = (uint)(address >> _deviceShift);
        if (sector >= _sectorCount) return (null, 0);

        var dev = _devices[sector];
        if (dev != null)
        {
            ulong offset = address & _offsetMask;
            return (dev, offset);
        }
        return (null, address);
    }


    /// <summary>
    /// Читает байт из порта по адресу.
    /// </summary>
    /// <param name="address">Полный адрес порта.</param>
    /// <returns>
    /// Результат с данными и статусом: <see cref="BiosStatus.Success"/> при успехе;
    /// <see cref="BiosStatus.NullDeviceInput"/>, если по адресу нет устройства.
    /// </returns>
    public ReadPortResult ReadPort(ulong address)
    {
        var (device, offset) = ResolveAddress(address);
        return device != null
            ? new(device.ReadPort(offset))
            : new(0, BiosStatus.NullDeviceInput);
    }

    /// <summary>
    /// Записывает байт в порт по адресу.
    /// </summary>
    /// <param name="address">Полный адрес порта.</param>
    /// <param name="value">Записываемое значение.</param>
    /// <returns>
    /// <see cref="BiosStatus.Success"/> при успехе;
    /// <see cref="BiosStatus.NullDeviceOutput"/>, если по адресу нет устройства.
    /// </returns>
    public BiosStatus WritePort(ulong address, byte value)
    {
        var (device, offset) = ResolveAddress(address);
        if (device == null)
        {
            return BiosStatus.NullDeviceOutput;
        }

        device.WritePort(offset, value);
        return BiosStatus.Success;
    }

    /// <summary>
    /// Будит устройство по адресу порта.
    /// </summary>
    /// <param name="address">Полный адрес порта.</param>
    /// <returns><see langword="true"/>, если устройство найдено и оповещено; иначе <see langword="false"/>.</returns>
    public bool WakeProcessor(ulong address)
    {
        var (device, _) = ResolveAddress(address);
        if (device != null)
        {
            device.WakeProcessor();
            return true;
        }
        return false;
    }
}