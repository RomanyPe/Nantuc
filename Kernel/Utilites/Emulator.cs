using Kernel.BiosSystem;
using Kernel.Common;
using Kernel.Contracts;
using Kernel.ControllersData;
using System.Diagnostics.CodeAnalysis;

namespace Kernel.Utilites;

/// <summary>
/// Эмулятор устройства: управляет загрузкой программ, запуском, пошаговым выполнением.
/// Теперь поддерживает несколько устройств через ManagerDevices.
/// </summary>
public class Emulator : IDisposable
{
    private readonly PortBus _portBus;
    private readonly ManagerDevices _manager;


    public Emulator(PortSize ports, DevicePortSize portsOnDev)
    {
        _portBus = new PortBus(ports, portsOnDev);
        _manager = new ManagerDevices(_portBus);
    }

    public IEnumerable<DeviceInfo> AllDeviceInfo => _manager.GetAllDevices();

    public int RegisterDevice(
        IPortController device,
        string? name,
        uint sector)
    {
        return _manager.AddDevice(device, name, sector);
    }

    public Device CreateDevice(
        RamSize size,
        IDeviceLoggerContext ctx,
        IProcessorFaultPolicy processorFaultPolicy)
    {
        return new Device(_portBus, size, ctx, processorFaultPolicy);
    }


    public IPortController? GetDevice(int id) => _manager.GetDevice(id);

    public bool TryGetDevice<T>(int id, [NotNullWhen(true)] out T? device)
        where T : class, IPortController => _manager.TryGetDevice(id, out device);


    public bool UnregisterDevice(int id) => _manager.RemoveDevice(id);

    public bool ChangeDeviceSector(int id, uint newSector) => _manager.ChangeSector(id, newSector);


    public void Dispose()
    {
        _manager.Dispose();
        GC.SuppressFinalize(this);
    }
}