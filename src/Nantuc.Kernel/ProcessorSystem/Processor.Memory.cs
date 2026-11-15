using Nantuc.Common;

namespace Nantuc.Kernel.ProcessorSystem;

public sealed partial class Processor
{
    // ============================================================
    // Память: LOAD / STORE (+unsafe), непосредственные 64-битные
    // операнды, низкоуровневые Body*.
    // ============================================================

    private BiosStatus CanRead64IntDataInst()
    {
        ulong ip = _registers[RegType.rIP.Int()].AlignUp(8UL);
        return ip > _ram.Length - 8
            ? RecordFaultContext(BiosStatus.SegmentationFault, ip)
            : BiosStatus.Success;
    }

    private ulong Get64IntData()
    {
        ulong ip = _registers[RegType.rIP.Int()];
        ip = ip.AlignUp(8UL);
        ulong data = _ram.ReadInt64LEUnSafe(ip);
        ip += 8;
        _registers[RegType.rIP.Int()] = ip;
        return data;
    }

    private BiosStatus InstructionLOAD(OpCodeSize sizeT, RegType reg)
    {
        BiosStatus status = CanRead64IntDataInst();
        if (status != BiosStatus.Success) return status;

        ulong adress = Get64IntData();
        return InstructionBodyLOAD(sizeT, reg, adress);
    }

    private BiosStatus InstructionLOAD_IND(OpCodeSize sizeT, RegType reg1, RegType reg2)
    {
        return InstructionBodyLOAD(sizeT, reg1, _registers[reg2.Int()]);
    }

    private BiosStatus InstructionSTORE(OpCodeSize sizeT, RegType reg)
    {
        BiosStatus status = CanRead64IntDataInst();
        if (status != BiosStatus.Success) return status;

        ulong adress = Get64IntData();
        return InstructionBodySTORE(sizeT, reg, adress);
    }

    private BiosStatus InstructionSTORE_IND(OpCodeSize sizeT, RegType reg1, RegType reg2)
    {
        return InstructionBodySTORE(sizeT, reg1, _registers[reg2.Int()]);
    }

    private BiosStatus InstructionLOAD_UNSAFE(OpCodeSize sizeT, RegType reg)
    {
        if (!_processorPolicy.AllowUnsafeOpCodes) return BiosStatus.UnsafeOpCodeForbidden;
        BiosStatus status = CanRead64IntDataInst();
        if (status != BiosStatus.Success) return status;

        ulong adress = Get64IntData();
        return InstructionBodyLOAD_UNSAFE(sizeT, reg, adress);
    }

    private BiosStatus InstructionSTORE_UNSAFE(OpCodeSize sizeT, RegType reg)
    {
        if (!_processorPolicy.AllowUnsafeOpCodes) return BiosStatus.UnsafeOpCodeForbidden;
        BiosStatus status = CanRead64IntDataInst();
        if (status != BiosStatus.Success) return status;

        ulong adress = Get64IntData();
        return InstructionBodySTORE_UNSAFE(sizeT, reg, adress);
    }

    private BiosStatus InstructionLOAD_IND_UNSAFE(OpCodeSize sizeT, RegType reg1, RegType reg2)
    {
        if (!_processorPolicy.AllowUnsafeOpCodes) return BiosStatus.UnsafeOpCodeForbidden;
        return InstructionBodyLOAD_UNSAFE(sizeT, reg1, _registers[reg2.Int()]);
    }

    private BiosStatus InstructionSTORE_IND_UNSAFE(OpCodeSize sizeT, RegType reg1, RegType reg2)
    {
        if (!_processorPolicy.AllowUnsafeOpCodes) return BiosStatus.UnsafeOpCodeForbidden;
        return InstructionBodySTORE_UNSAFE(sizeT, reg1, _registers[reg2.Int()]);
    }

    private BiosStatus InstructionBodyLOAD(OpCodeSize sizeT, RegType reg, ulong adress)
    {
        BiosStatus status;
        switch (sizeT)
        {
            case OpCodeSize.S8:
                var data8 = _ram.ReadInt8LE(adress);
                status = _ram.UpdateAndReadStatus();
                if (status != BiosStatus.Success)
                    return RecordFaultContext(status, _ram.FaultData);

                _registers[reg.Int()] = data8;
                return status;

            case OpCodeSize.S16:
                var data16 = _ram.ReadInt16LE(adress);
                status = _ram.UpdateAndReadStatus();
                if (status != BiosStatus.Success)
                    return RecordFaultContext(status, _ram.FaultData);

                _registers[reg.Int()] = data16;
                return BiosStatus.Success;

            case OpCodeSize.S32:
                var data32 = _ram.ReadInt32LE(adress);
                status = _ram.UpdateAndReadStatus();
                if (status != BiosStatus.Success)
                    return RecordFaultContext(status, _ram.FaultData);

                _registers[reg.Int()] = data32;
                return BiosStatus.Success;

            case OpCodeSize.S64:
                var data64 = _ram.ReadInt64LE(adress);
                status = _ram.UpdateAndReadStatus();
                if (status != BiosStatus.Success)
                    return RecordFaultContext(status, _ram.FaultData);

                _registers[reg.Int()] = data64;
                return BiosStatus.Success;

            default:
                _lastFaultData = adress;
                return BiosStatus.SegmentationFault;
        }
    }

    private BiosStatus InstructionBodySTORE(OpCodeSize sizeT, RegType reg, ulong adress)
    {
        BiosStatus status;
        switch (sizeT)
        {
            case OpCodeSize.S8:
                _ram.WriteInt8LE(adress, (byte)_registers[reg.Int()]);
                status = _ram.UpdateAndReadStatus();
                if (status != BiosStatus.Success)
                    return RecordFaultContext(status, _ram.FaultData);
                return status;

            case OpCodeSize.S16:
                _ram.WriteInt16LE(adress, (ushort)_registers[reg.Int()]);
                status = _ram.UpdateAndReadStatus();
                if (status != BiosStatus.Success)
                    return RecordFaultContext(status, _ram.FaultData);
                return status;

            case OpCodeSize.S32:
                _ram.WriteInt32LE(adress, (uint)_registers[reg.Int()]);
                status = _ram.UpdateAndReadStatus();
                if (status != BiosStatus.Success)
                    return RecordFaultContext(status, _ram.FaultData);
                return status;

            case OpCodeSize.S64:
                _ram.WriteInt64LE(adress, _registers[reg.Int()]);
                status = _ram.UpdateAndReadStatus();
                if (status != BiosStatus.Success)
                    return RecordFaultContext(status, _ram.FaultData);
                return status;

            default:
                _lastFaultData = adress;
                return BiosStatus.AlignmentFault;
        }
    }

    private BiosStatus InstructionBodySTORE_UNSAFE(OpCodeSize sizeT, RegType reg, ulong adress)
    {
        switch (sizeT)
        {
            case OpCodeSize.S8:
                var data8 = (byte)_registers[reg.Int()];
                _ram.WriteInt8LEUnSafe(adress, data8);
                return BiosStatus.Success;

            case OpCodeSize.S16:
                var data16 = (ushort)_registers[reg.Int()];
                _ram.WriteInt16LEUnSafe(adress, data16);
                return BiosStatus.Success;

            case OpCodeSize.S32:
                var data32 = (uint)_registers[reg.Int()];
                _ram.WriteInt32LEUnSafe(adress, data32);
                return BiosStatus.Success;

            case OpCodeSize.S64:
                var data64 = _registers[reg.Int()];
                _ram.WriteInt64LEUnSafe(adress, data64);
                return BiosStatus.Success;
        }
        _lastFaultData = adress;
        return BiosStatus.AlignmentFault;
    }

    private BiosStatus InstructionBodyLOAD_UNSAFE(OpCodeSize sizeT, RegType reg, ulong adress)
    {
        switch (sizeT)
        {
            case OpCodeSize.S8:
                var data8 = _ram.ReadInt8LEUnSafe(adress);
                _registers[reg.Int()] = data8;
                return BiosStatus.Success;

            case OpCodeSize.S16:
                var data16 = _ram.ReadInt16LEUnSafe(adress);
                _registers[reg.Int()] = data16;
                return BiosStatus.Success;

            case OpCodeSize.S32:
                var data32 = _ram.ReadInt32LEUnSafe(adress);
                _registers[reg.Int()] = data32;
                return BiosStatus.Success;

            case OpCodeSize.S64:
                var data64 = _ram.ReadInt64LEUnSafe(adress);
                _registers[reg.Int()] = data64;
                return BiosStatus.Success;
        }
        _lastFaultData = adress;
        return BiosStatus.SegmentationFault;
    }
}