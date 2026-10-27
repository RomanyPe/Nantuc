using Nantuc.Kernel.Common;

namespace Nantuc.VmApplication.Emulator;

/// <summary> 
/// Билдер для <see cref="VmEmulator"/>
/// Дефолтный <see cref="PortSize"/>: <see cref="PortSize.KB64"/>
/// Дефолтный <see cref="DevicePortSize"/>: <see cref="DevicePortSize.B16"/> 
/// </summary>
public class VmEmulatorBuilder
{
    private PortSize _portBusSize = PortSize.KB64;
    private DevicePortSize _portsPerDevice = DevicePortSize.B16;

    /// <summary>
    /// Сменить стандартный <see cref="PortSize"/>, дефолтный <see cref="PortSize.KB64"/>
    /// </summary>
    public VmEmulatorBuilder WithPortBusSize(PortSize ports)
    {
        _portBusSize = ports;
        return this;
    }

    /// <summary>
    /// Сменить стандартный <see cref="DevicePortSize"/>, дефолтный <see cref="DevicePortSize.B16"/>
    /// </summary
    public VmEmulatorBuilder WithPortsPerDevice(DevicePortSize portsOnDev)
    {
        _portsPerDevice = portsOnDev;
        return this;
    }
    /// <summary>
    /// Собрать <see cref="VmEmulator"/> из предыдущих данных
    /// </summary>
    /// <returns></returns>
    public VmEmulator Build()
    {
        return new VmEmulator(new(_portBusSize, _portsPerDevice));
    }
}
