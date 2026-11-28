using Nantuc.Common;

namespace Nantuc.Kernel.ProcessorSystem;

public sealed partial class Processor
{
    // ============================================================
    // Управление потоком: переходы, CALL/RET, END/HALT.
    // ============================================================

    private BiosStatus InstructionJMP()
    {
        BiosStatus status = CanRead64IntDataInst();
        if (status != BiosStatus.Success) return status;

        ulong t = Get64IntData();

        if (t >= _ram.Length)
            return RecordFaultContext(BiosStatus.SegmentationFault, t);

        _registers[RegType.rIP.Int()] = t;
        return BiosStatus.Success;
    }

    private BiosStatus InstructionJZ()
    {
        BiosStatus status = CanRead64IntDataInst();
        if (status != BiosStatus.Success) return status;

        ulong t = Get64IntData();

        if (t >= _ram.Length)
            return RecordFaultContext(BiosStatus.SegmentationFault, t);

        if (ZeroFlagSet) _registers[RegType.rIP.Int()] = t;
        return BiosStatus.Success;
    }

    private BiosStatus InstructionJNZ()
    {
        BiosStatus status = CanRead64IntDataInst();
        if (status != BiosStatus.Success) return status;

        ulong t = Get64IntData();

        if (t >= _ram.Length)
            return RecordFaultContext(BiosStatus.SegmentationFault, t);

        if (!ZeroFlagSet) _registers[RegType.rIP.Int()] = t;
        return BiosStatus.Success;
    }

    private BiosStatus InstructionJL()
    {
        BiosStatus status = CanRead64IntDataInst();
        if (status != BiosStatus.Success) return status;

        ulong t = Get64IntData();

        if (t >= _ram.Length)
            return RecordFaultContext(BiosStatus.SegmentationFault, t);

        if (NegativeFlagSet) _registers[RegType.rIP.Int()] = t;
        return BiosStatus.Success;
    }

    private BiosStatus InstructionJG()
    {
        BiosStatus status = CanRead64IntDataInst();
        if (status != BiosStatus.Success) return status;

        ulong t = Get64IntData();

        if (t >= _ram.Length)
            return RecordFaultContext(BiosStatus.SegmentationFault, t);

        // Использование битового & здесь нужно для производительности:
        // - отсутсвие короткого пути из за которого происходят проблемы с предсказателем перехода
        // - вычисление обоих флагов очень легкая операция и короткий путь здесь не сильно влияет
        if (!NegativeFlagSet & !ZeroFlagSet) _registers[RegType.rIP.Int()] = t;
        return BiosStatus.Success;
    }
    private BiosStatus InstructionJB()
    {
        BiosStatus status = CanRead64IntDataInst();
        if (status != BiosStatus.Success) return status;

        ulong t = Get64IntData();

        if (t >= _ram.Length)
            return RecordFaultContext(BiosStatus.SegmentationFault, t);

        if (CarryFlagSet) _registers[RegType.rIP.Int()] = t;
        return BiosStatus.Success;
    }

    private BiosStatus InstructionJA()
    {
        BiosStatus status = CanRead64IntDataInst();
        if (status != BiosStatus.Success) return status;

        ulong t = Get64IntData();

        if (t >= _ram.Length)
            return RecordFaultContext(BiosStatus.SegmentationFault, t);

        // Использование битового & здесь нужно для производительности:
        // - отсутсвие короткого пути из за которого происходят проблемы с предсказателем перехода
        // - вычисление обоих флагов очень легкая операция и короткий путь здесь не сильно влияет

        // эквевалент !CarryFlagSet & !ZeroFlagSet
        if ((_registers[RegType.rFL.Int()] & 5UL) == 0UL) _registers[RegType.rIP.Int()] = t;
        return BiosStatus.Success;
    }

    private BiosStatus InstructionJMP_IND(RegType r)
    {
        var nextIp = _registers[r.Int()];
        if (nextIp >= _ram.Length)
            return RecordFaultContext(BiosStatus.SegmentationFault, nextIp);

        _registers[RegType.rIP.Int()] = nextIp;
        return BiosStatus.Success;
    }

    private BiosStatus InstructionCALL_IND(RegType r)
    {
        var nextIp = _registers[r.Int()];
        return InstructionBodyCALL(nextIp);
    }

    private BiosStatus InstructionCALL()
    {
        BiosStatus status = CanRead64IntDataInst();
        if (status != BiosStatus.Success) return status;
        return InstructionBodyCALL(Get64IntData());
    }

    private BiosStatus InstructionBodyCALL(ulong targetAddress)
    {
        if (targetAddress >= _ram.Length)
            return RecordFaultContext(BiosStatus.SegmentationFault, targetAddress);

        ulong oldSp = _registers[RegType.rSP.Int()];

        if (oldSp < 8)
            return RecordFaultContext(BiosStatus.SegmentationFault, oldSp);

        ulong sp = oldSp - 8;

        if (_registers[RegType.rHP.Int()] >= sp)
            return RecordFaultContext(BiosStatus.SegmentationFault, sp);

        _ram.WriteInt64LE(sp, _registers[RegType.rIP.Int()]);
        var status = _ram.UpdateAndReadStatus();
        if (status != BiosStatus.Success)
            return RecordFaultContext(status, _ram.FaultData);

        _registers[RegType.rSP.Int()] = sp;
        _registers[RegType.rIP.Int()] = targetAddress;

        return BiosStatus.Success;
    }

    private BiosStatus InstructionRET()
    {
        ulong sp = _registers[RegType.rSP.Int()];

        if (sp >= _stackBase)
            return RecordFaultContext(BiosStatus.StackUnderflow, _registers[RegType.rIP.Int()]);

        ulong returnAddress = _ram.ReadInt64LE(sp);
        var status = _ram.UpdateAndReadStatus();
        if (status != BiosStatus.Success)
            return RecordFaultContext(status, _ram.FaultData);
        if (returnAddress >= _ram.Length)
            return RecordFaultContext(BiosStatus.SegmentationFault, returnAddress);

        _registers[RegType.rSP.Int()] = sp + 8;
        _registers[RegType.rIP.Int()] = returnAddress;

        return BiosStatus.Success;
    }

    private BiosStatus EndProgram()
    {
        Volatile.Write(ref _isRunning, false);
        return BiosStatus.EndProgram;
    }

    private BiosStatus InstructionHALT()
    {
        Volatile.Write(ref _isSleeping, true);
        Volatile.Write(ref _hasCommand, true);
        return BiosStatus.Success;
    }
}