using Kernel.Common;

namespace VMApplication.Emulator;

public class VMEmulatorBuilder
{
    private PortSize _portBusSize = PortSize.KB64;
    private DevicePortSize _portsPerDevice = DevicePortSize.B16;

    public VMEmulatorBuilder WithPortBusSize(PortSize ports)
    {
        _portBusSize = ports;
        return this;
    }

    public VMEmulatorBuilder WithPortsPerDevice(DevicePortSize portsOnDev)
    {
        _portsPerDevice = portsOnDev;
        return this;
    }

    public VMEmulator Build()
    {
        return new VMEmulator(new(_portBusSize, _portsPerDevice));
    }
}
