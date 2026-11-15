using Nantuc.Common;

namespace Nantuc.Kernel.ProcessorSystem;

public sealed partial class Processor
{
    // ============================================================
    // Порты ввода-вывода и пробуждение устройства.
    // ============================================================

    private BiosStatus InstructionIN(RegType regDest, RegType regPort)
    {
        ulong portAddress = _registers[regPort.Int()];
        var result = _portBus.ReadPort(portAddress);

        if (result.Status != BiosStatus.Success)
            return RecordFaultContext(result.Status, portAddress);

        _registers[regDest.Int()] = result.Data;
        return BiosStatus.Success;
    }

    private BiosStatus InstructionOUT(RegType regSrc, RegType regPort)
    {
        ulong portAddress = _registers[regPort.Int()];
        byte value = (byte)(_registers[regSrc.Int()] & 0xFF);
        var status = _portBus.WritePort(portAddress, value);
        if (status != BiosStatus.Success)
            return RecordFaultContext(status, portAddress);

        return BiosStatus.Success;
    }

    private BiosStatus InstructionWAKE_INT(RegType reg1)
    {
        ulong regV = _registers[reg1.Int()];
        if (!_portBus.WakeProcessor(regV))
        {
            return RecordFaultContext(BiosStatus.NullDeviceOutput, _registers[RegType.rIP.Int()]);
        }

        return BiosStatus.Success;
    }
}