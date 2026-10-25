using Nantuc.Kernel.BiosSystem;
using Nantuc.Kernel.Common;
using Nantuc.Kernel.Contracts;
using Nantuc.Kernel.ControllersData;

namespace Nantuc.VmApplication.Emulator;

public sealed class VmEmulator(Kernel.Utilites.Emulator emulator) : IDisposable
{
    private readonly Kernel.Utilites.Emulator _emulator = emulator;
    internal PortBus AsPortBus => _emulator.PortBus;

    public IPortBus GetPortBus() => _emulator.PortBus;
    public DeviceContext? GetDeviceContext(int id)
    {
        return _emulator.TryGetDevice<Device>(id, out var device) 
            ? new DeviceContext(device) 
            : null;
    }

    public bool ChangeDeviceSector(int id, uint newSector) => _emulator.ChangeDeviceSector(id, newSector);

    public IReadOnlyCollection<DeviceInfo> GetAllDevices()
        => [.. _emulator.AllDeviceInfo];


    public int RegisterDevice(IPortController ioDevice,
        string? name,
        uint sector)
    {
        return _emulator.RegisterDevice(ioDevice, name, sector);
    }
    
    public int RegisterDevice(DeviceSpec handle, uint sector)
    {
        var device = _emulator.CreateDevice(handle.RamSize, handle.ProcessorFaultPolicy);
        return RegisterDevice(device, handle.Name, sector);
    }

    public bool UnregisterDevice(int id) => _emulator.UnregisterDevice(id);

    public void Dispose() => _emulator.Dispose();
}

public readonly record struct DeviceSpec(RamSize RamSize, string? Name, IProcessorFaultPolicy ProcessorFaultPolicy)
{
    public static DeviceSpec Create(RamSize? ramSize = null,
                                      string? name = null,
                                      IProcessorFaultPolicy? processorFaultPolicy = null)
    {
        ramSize ??= RamSize.MB1;
        processorFaultPolicy ??= IProcessorFaultPolicy.Default;
        return new(ramSize.Value, name, processorFaultPolicy);

    }
}