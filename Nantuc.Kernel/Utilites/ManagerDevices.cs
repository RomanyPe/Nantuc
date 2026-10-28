using Nantuc.Kernel.Common;
using Nantuc.Kernel.Contracts;
using Nantuc.Kernel.ControllersData;
using System.Diagnostics.CodeAnalysis;

namespace Nantuc.Kernel.Utilites;

/// <summary>
/// Реестр устройств эмулятора: выделяет секторы портов, присваивает идентификаторы,
/// поддерживает переиспользование освобождённых слотов.
/// </summary>
/// <remarks>
/// <para>
/// Ёмкость реестра ограничена <see cref="PortBus.SectorCount"/>: каждому устройству
/// требуется собственный сектор портов. Идентификаторы выдаются монотонно и
/// переиспользуются вместе со слотами (освобождённый слот хранит свой <c>Id</c>).
/// </para>
/// <para>
/// Потокобезопасность не гарантируется; предполагается использование из одного потока-координатора.
/// </para>
/// </remarks>
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

    /// <summary>
    /// Количество живых (не освобождённых) устройств в реестре.
    /// </summary>
    /// <remarks>
    /// Вычисляется перебором всех записей — O(n); не кэшируется.
    /// </remarks>
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

    /// <summary>
    /// Возвращает снимок информации обо всех живых устройствах.
    /// </summary>
    /// <returns>Новый список <see cref="DeviceInfo"/>; пустой, если устройств нет.</returns>
    public IReadOnlyList<DeviceInfo> GetAllDevices()
    {
        List<DeviceInfo> res = [];
        for (int i = 0; i < _devices.Count; i++)
        {
            var e = _devices[i];
            if (!e.IsValid) continue;

            res.Add(new(e.Id, e.Sector, e.Name));
        }
        return res;
    }

    /// <summary>
    /// Возвращает контроллер живого устройства по идентификатору.
    /// </summary>
    /// <param name="id">Идентификатор устройства.</param>
    /// <returns>Контроллер или <see langword="null"/>, если устройство не найдено/освобождено.</returns>
    public IPortController? GetDevice(int id) => FindValid(id)?.Device;

    /// <summary>
    /// Пытается получить контроллер живого устройства, приведённый к типу <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Ожидаемый тип контроллера.</typeparam>
    /// <param name="id">Идентификатор устройства.</param>
    /// <param name="device">Контроллер при успехе; иначе <see langword="null"/>.</param>
    /// <returns><see langword="true"/>, если устройство найдено и имеет тип <typeparamref name="T"/>.</returns>
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

    /// <summary>
    /// Пытается добавить устройство в реестр, закрепив за ним указанный сектор портов.
    /// </summary>
    /// <param name="device">Контроллер портов устройства.</param>
    /// <param name="name">Необязательное имя для диагностики; может быть <see langword="null"/>.</param>
    /// <param name="sector">Сектор портов, который нужно закрепить за устройством.</param>
    /// <param name="id">
    /// Присвоенный идентификатор при успехе; <c>-1…-4</c> при отказе
    /// (см. <returns>). При отказе регистрации переданный <paramref name="device"/>
    /// освобождается вызовом <c>Dispose()</c>.
    /// </param>
    /// <returns>
    /// <see cref="IdState.Valid"/> при успехе; иначе:
    /// <see cref="IdState.NotAssigned"/> — реестр заполнен;
    /// <see cref="IdState.OutOfRange"/> — сектор занят или вне диапазона;
    /// <see cref="IdState.NoFreeSlot"/> — нет свободного слота и исчерпаны идентификаторы;
    /// <see cref="IdState.Rejected"/> — <see cref="PortBus"/> отказал в регистрации.
    /// </returns>
    public IdState TryAddDevice(
        IPortController device,
        string? name,
        uint sector, out int id)
    {
        if (Count >= _count)
        {
            id = -1;
            return IdState.NotAssigned;
        }

        if (!_portBus.IsFreeSector(sector))
        {
            id = -2;
            return IdState.OutOfRange;
        }

        var slot = FindFreeSlot();
        if (slot == null && _nextId >= _count)
        {
            id = -3;
            return IdState.NoFreeSlot;
        }

        if (!_portBus.RegisterDevice(device, sector))
        {
            device.Dispose();
            id = -4;
            return IdState.Rejected;
        }

        if (slot != null)
        {
            // Переиспользуем освобождённый слот вместе с его ID.
            slot.Name = name;
            slot.Sector = sector;
            slot.Device = device;
            slot.IsFreed = false;
            id = slot.Id;
            return IdState.Valid;
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
        id = newId;
        return IdState.Valid;
    }

    /// <summary>
    /// Снимает устройство с регистрации: освобождает его сектор портов,
    /// освобождает контроллер и помечает слот как свободный (с сохранением <c>Id</c>).
    /// </summary>
    /// <param name="id">Идентификатор устройства.</param>
    /// <returns><see langword="true"/>, если устройство было найдено и снято; иначе <see langword="false"/>.</returns>
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

    /// <summary>
    /// Переводит живое устройство на новый сектор портов.
    /// </summary>
    /// <param name="id">Идентификатор устройства.</param>
    /// <param name="newSector">Новый сектор портов.</param>
    /// <returns>
    /// <see langword="true"/> при успехе; <see langword="false"/>, если устройство не найдено,
    /// сектор вне диапазона или уже занят.
    /// </returns>
    /// <remarks>
    /// Сначала регистрирует устройство на новом секторе, затем освобождает старый.
    /// При отказе регистрации состояние не меняется.
    /// </remarks>
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

    /// <summary>
    /// Освобождает все зарегистрированные контроллеры и очищает реестр.
    /// </summary>
    /// <remarks>Повторный вызов безопасен.</remarks>
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