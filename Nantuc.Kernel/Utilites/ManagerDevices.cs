using Nantuc.Kernel.Contracts;
using Nantuc.Kernel.ControllersData;
using System.Diagnostics.CodeAnalysis;

namespace Nantuc.Kernel.Utilites;

public class ManagerDevices(PortBus portBus) : IDisposable
{
    private sealed class DeviceEntry
    {
        public int Id;
        public string? Name;
        public uint Sector;
        public IPortController? Device;
        public bool IsFreed;

        public bool IsValid => !IsFreed && Device != null;
    }

    private readonly List<DeviceEntry> _devices = new((int)portBus.SectorCount);
    private readonly int _count = (int)portBus.SectorCount;
    private int _nextId = 0;
    private bool _disposed;
    private readonly PortBus _portBus = portBus;

    // Валидатор: считаем только живые записи.
    public int Count
    {
        get
        {
            int n = 0;
            for (int i = 0; i < _devices.Count; i++)
                if (_devices[i].IsValid) n++;
            return n;
        }
    }

    private DeviceEntry? FindEntry(int id)
    {
        if (id < 0 || id >= _count) return null;
        var list = _devices;
        for (int i = 0; i < list.Count; i++)
            if (list[i].Id == id) return list[i];
        return null;
    }

    private DeviceEntry? FindValid(int id)
    {
        var e = FindEntry(id);
        return e != null && e.IsValid ? e : null;
    }

    private DeviceEntry? FindFreeSlot()
    {
        for (int i = 0; i < _devices.Count; i++)
            if (_devices[i].IsFreed) return _devices[i];
        return null;
    }

    public IEnumerable<DeviceInfo> GetAllDevices()
    {
        for (int i = 0; i < _devices.Count; i++)
        {
            var e = _devices[i];
            if (!e.IsValid) continue;

            yield return new(e.Id, e.Sector, e.Name);
        }
    }

    public IPortController? GetDevice(int id) => FindValid(id)?.Device;

    public bool TryGetDevice<T>(int id, [NotNullWhen(true)] out T? device)
    where T : class, IPortController
    {
        if (FindValid(id)?.Device is T t)
        {
            device = t;
            return true;
        }
        device = null;
        return false;
    }

    public int AddDevice(
        IPortController device,
        string? name,
        uint sector)
    {
        if (Count >= _count) return -1;
        if (!_portBus.IsFreeSector(sector)) return -2;

        var slot = FindFreeSlot();
        if (slot == null && _nextId >= _count)
            return -3;

        if (!_portBus.RegisterDevice(device, sector))
        {
            device.Dispose();
            return -4;
        }

        if (slot != null)
        {
            // Переиспользуем освобождённый слот вместе с его ID.
            slot.Name = name;
            slot.Sector = sector;
            slot.Device = device;
            slot.IsFreed = false;
            return slot.Id;
        }

        int newId = _nextId++;
        _devices.Add(new DeviceEntry
        {
            Id = newId,
            Name = name,
            Sector = sector,
            Device = device,
            IsFreed = false,
        });
        return newId;
    }

    public bool RemoveDevice(int id)
    {
        var e = FindValid(id);
        if (e == null) return false;

        _portBus.UnregisterDevice(e.Sector);
        e.Device?.Dispose();
        e.Device = null;
        e.Name = null;
        e.IsFreed = true;
        return true;
    }

    public bool ChangeSector(int id, uint newSector)
    {
        var e = FindValid(id);
        if (e == null) return false;
        if (newSector >= _portBus.SectorCount) return false;

        if (!_portBus.RegisterDevice(e.Device!, newSector)) return false;

        _portBus.UnregisterDevice(e.Sector);
        e.Sector = newSector;
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        for (int i = 0; i < _devices.Count; i++)
        {
            _devices[i].Device?.Dispose();
            _devices[i].Device = null;
            _devices[i].Name = null;
            _devices[i].IsFreed = true;
        }
        _devices.Clear();
        _nextId = 0;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
