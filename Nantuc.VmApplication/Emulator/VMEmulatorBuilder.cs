using Nantuc.Kernel.Common;

namespace Nantuc.VmApplication.Emulator;

public class VmEmulatorBuilder
{
    private PortSize _portBusSize = PortSize.KB64;
    private DevicePortSize _portsPerDevice = DevicePortSize.B16;

    public VmEmulatorBuilder WithPortBusSize(PortSize ports)
    {
        _portBusSize = ports;
        return this;
    }

    public VmEmulatorBuilder WithPortsPerDevice(DevicePortSize portsOnDev)
    {
        _portsPerDevice = portsOnDev;
        return this;
    }

    public VmEmulator Build()
    {
        return new VmEmulator(new(_portBusSize, _portsPerDevice));
    }
}
