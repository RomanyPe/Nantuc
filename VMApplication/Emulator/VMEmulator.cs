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
    
    public int RegisterDevice(DeviceSpec handle, uint sector)
    {
        var device = _emulator.CreateDevice(handle.RamSize, handle.DeviceCtx, handle.ProcessorFaultPolicy);
        return RegisterDevice(device, handle.Name, sector);
    }

    public bool RemoveDevice(int id) => _emulator.RemoveDevice(id);

    public void Dispose() => _emulator.Dispose();
}

public readonly record struct DeviceSpec(RamSize RamSize, IDeviceLoggerContext DeviceCtx, string? Name, IProcessorFaultPolicy ProcessorFaultPolicy)
{
    public static DeviceSpec Create(RamSize? ramSize = null,
                                      string? name = null,
                                      IDeviceLoggerContext? deviceCtx = null,
                                      IProcessorFaultPolicy? processorFaultPolicy = null)
    {
        ramSize ??= RamSize.MB1;
        deviceCtx ??= IDeviceLoggerContext.NullContext;
        processorFaultPolicy ??= IProcessorFaultPolicy.Default;
        return new(ramSize.Value, deviceCtx, name, processorFaultPolicy);

    }
}