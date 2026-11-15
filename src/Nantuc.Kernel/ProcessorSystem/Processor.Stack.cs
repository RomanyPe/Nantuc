using Nantuc.Common;

namespace Nantuc.Kernel.ProcessorSystem;

public sealed partial class Processor
{
    // ============================================================
    // Стек: PUSH / POP.
    // ============================================================

    private BiosStatus InstructionPUSH(RegType reg)
    {
        ulong value = _registers[reg.Int()];
        ulong sp = _registers[RegType.rSP.Int()];

        if (sp < 8) return RecordFaultContext(BiosStatus.SegmentationFault, sp);

        sp -= 8;

        if (_registers[RegType.rHP.Int()] >= sp)
            return RecordFaultContext(BiosStatus.SegmentationFault, sp);

        _ram.WriteInt64LE(sp, value);
        var status = _ram.UpdateAndReadStatus();
        if (status != BiosStatus.Success)
            return RecordFaultContext(status, _ram.FaultData);

        _registers[RegType.rSP.Int()] = sp;
        return BiosStatus.Success;
    }

    private BiosStatus InstructionPOP(RegType reg)
    {
        ulong sp = _registers[RegType.rSP.Int()];
        if (sp >= _stackBase)
            return RecordFaultContext(BiosStatus.StackUnderflow, _registers[RegType.rIP.Int()]);

        var readResult = _ram.ReadInt64LE(sp);

        var status = _ram.UpdateAndReadStatus();
        if (status != BiosStatus.Success)
            return RecordFaultContext(status, _ram.FaultData);

        sp += 8;

        _registers[reg.Int()] = readResult;
        _registers[RegType.rSP.Int()] = sp;

        return BiosStatus.Success;
    }
}