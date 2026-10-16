using Kernel.BiosSystem;
using Kernel.Common;
using Kernel.Contracts;

namespace VMApplication.Emulator;

public sealed class VMEmulator(Kernel.Utilites.Emulator emulator) : IDisposable
{
    private readonly Kernel.Utilites.Emulator _emulator = emulator;


    public DeviceContext? GetDeviceContext(int id)
    {
        return _emulator.TryGetDevice<Device>(id, out var device) 
            ? new DeviceContext(device) 
            : null;
    }

    public IPortController? GetDevice(int id) => _emulator.GetDevice(id);


    public bool ChangeDeviceSector(int id, uint newSector) => _emulator.ChangeDeviceSector(id, newSector);

    public IReadOnlyCollection<DeviceInfo> GetAllDevices()
        => [.. _emulator.AllDeviceInfo];


    public int RegisterDevice(IPortController ioDevice,
        string? name,
        uint sector)
    {
        return _emulator.AddDevice(ioDevice, name, sector);
    }
    
    public int RegisterDevice<T>(T handle, uint sector) where T : IDeviceHandle
    {
        return RegisterDevice(handle.Device, handle.Name, sector);
    }
    public int CreateAndAddDevice(RamSize ramSize,
                                      uint sector,
                                      IDeviceLoggerContext? deviceCtx = null,
                                      string? name = null,
                                      IProcessorFaultPolicy? processorFaultPolicy = null)
    {
        var ctx = NewDevice(ramSize, name, deviceCtx, processorFaultPolicy);
        return RegisterDevice(ctx.Device, name, sector);
    }

    public DeviceHandle NewDevice(RamSize? ramSize = null,
                                      string? name = null,
                                      IDeviceLoggerContext? deviceCtx = null,
                                      IProcessorFaultPolicy? processorFaultPolicy = null)
    {
        ramSize ??= RamSize.MB1;
        deviceCtx ??= IDeviceLoggerContext.NullContext;
        processorFaultPolicy ??= IProcessorFaultPolicy.Default;
        var device = _emulator.CreateDevice(ramSize.Value, deviceCtx, processorFaultPolicy);
        return new DeviceHandle(device, name);
    }

    public bool RemoveDevice(int id) => _emulator.RemoveDevice(id);

    public void Dispose() => _emulator.Dispose();
}

public readonly struct DeviceHandle(Device device, string? name) : IDeviceHandle
{
    public IPortController Device => device;
    public string? Name => name;
}

public interface IDeviceHandle
{
    IPortController Device { get; }
    string? Name { get; }
}