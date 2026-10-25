using Nantuc.Kernel.Common;
using Nantuc.Kernel.Contracts;

namespace Nantuc.Kernel.ControllersData;


public sealed class PortBus : IPortBus
{
    private readonly ulong _totalPorts;
    private readonly uint _portsOnDevice;

    private readonly uint _sectorCount;
    private readonly IPortUse[] _devices;

    private readonly byte _deviceShift;
    private readonly ulong _offsetMask;

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

    public uint SectorCount => _sectorCount;
    public uint PortsOnDevice => _portsOnDevice;
    public ulong TotalPorts => _totalPorts;

    public bool IsFreeSector(uint sector) => sector < _sectorCount && _devices[sector] == null;

    public bool RegisterDevice(IPortUse device, uint sector)
    {
        if (!IsFreeSector(sector)) return false;

        _devices[sector] = device;
        return true;
    }

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


    public ReadPortResult ReadPort(ulong address)
    {
        var (device, offset) = ResolveAddress(address);
        return device != null
            ? new(device.ReadPort(offset))
            : new((byte)address, BiosStatus.NullDeviceInput);
    }

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

