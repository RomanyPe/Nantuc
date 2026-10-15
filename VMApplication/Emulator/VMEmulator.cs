using Kernel.BiosSystem;
using Kernel.Common;
using Kernel.Contracts;
using VMApplication.Logger;

namespace VMApplication.Emulator;

public sealed class VMEmulator(Kernel.Utilites.Emulator emulator, VMHostLogger outputView) : IDisposable
{
    private readonly Kernel.Utilites.Emulator _emulator = emulator;
    private readonly VMHostLogger _outputView = outputView;

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


    public int AddDevice(IPortController ioDevice,
        string? name,
        uint sector)
    {
        return _emulator.AddDevice(ioDevice, name, sector);
    }
    
    public int CreateAndAddDevice(RamSize ramSize,
                                      uint sector,
                                      IDeviceLoggerContext? deviceCtx = null,
                                      string? name = null,
                                      IProcessorFaultPolicy? processorFaultPolicy = null)
    {
        var ctx = CreateDevice(ramSize, name, deviceCtx, processorFaultPolicy);
        return AddDevice(ctx.UnsafeGetDevice, name, sector);
    }

    private DeviceContext CreateDevice(RamSize? ramSize,
                                      string? name,
                                      IDeviceLoggerContext? deviceCtx,
                                      IProcessorFaultPolicy? processorFaultPolicy)
    {
        ramSize ??= RamSize.MB1;
        deviceCtx ??= _outputView.CreateDefaultLoggerContext(new NameDeviceToken(name));
        processorFaultPolicy ??= IProcessorFaultPolicy.Default;
        var device = _emulator.CreateDevice(ramSize.Value, deviceCtx, processorFaultPolicy);
        return new DeviceContext(device);
    }

    public bool RemoveDevice(int id) => _emulator.RemoveDevice(id);

    public void Dispose() => _emulator.Dispose();
}