using Nantuc.Common;

namespace Nantuc.Kernel.ProcessorSystem;

public sealed partial class Processor
{
    // ============================================================
    // Арифметика и логика.
    // ============================================================

    private BiosStatus InstructionCASTZ(RegType reg, OpCodeSize size)
    {
        ulong v = _registers[reg.Int()] & InstructionHelpers.MaskOf(size);
        _registers[reg.Int()] = v;
        return BiosStatus.Success;
    }

    private BiosStatus InstructionCASTS(RegType reg, OpCodeSize size)
    {
        _registers[reg.Int()] = InstructionHelpers.SignExtend(_registers[reg.Int()], size);
        return BiosStatus.Success;
    }

    private BiosStatus InstructionMOV(RegType reg1, RegType reg2)
    {
        _registers[reg1.Int()] = _registers[reg2.Int()];
        return BiosStatus.Success;
    }

    private BiosStatus InstructionLDI(RegType reg)
    {
        BiosStatus status = CanRead64IntDataInst();
        if (status != BiosStatus.Success) return status;

        _registers[reg.Int()] = Get64IntData();
        return BiosStatus.Success;
    }

    private BiosStatus InstructionADD(RegType reg1, RegType reg2)
    {
        ulong value1 = _registers[reg1.Int()];
        ulong value2 = _registers[reg2.Int()];
        ulong res = value1 + value2;
        _registers[reg1.Int()] = res;
        return BiosStatus.Success;
    }

    private BiosStatus InstructionSUB(RegType reg1, RegType reg2)
    {
        ulong value1 = _registers[reg1.Int()];
        ulong value2 = _registers[reg2.Int()];
        ulong res = value1 - value2;
        _registers[reg1.Int()] = res;
        return BiosStatus.Success;
    }

    private BiosStatus InstructionINC(RegType reg)
    {
        ulong value1 = _registers[reg.Int()];
        ulong res = value1 + 1;
        _registers[reg.Int()] = res;
        return BiosStatus.Success;
    }

    private BiosStatus InstructionDEC(RegType reg)
    {
        ulong value1 = _registers[reg.Int()];
        ulong res = value1 - 1;
        _registers[reg.Int()] = res;
        return BiosStatus.Success;
    }

    private BiosStatus InstructionMULT_INT(RegType reg1, RegType reg2)
    {
        ulong regv1 = _registers[reg1.Int()];
        ulong regv2 = _registers[reg2.Int()];
        ulong res = regv1 * regv2;
        _registers[reg1.Int()] = res;
        return BiosStatus.Success;
    }

    private BiosStatus InstructionSHR(RegType reg1, RegType reg2)
    {
        ulong regv1 = _registers[reg1.Int()];
        byte regv2 = (byte)_registers[reg2.Int()];
        ulong res = regv1 >> regv2;
        _registers[reg1.Int()] = res;
        return BiosStatus.Success;
    }

    private BiosStatus InstructionSHL(RegType reg1, RegType reg2)
    {
        ulong regv1 = _registers[reg1.Int()];
        byte regv2 = (byte)_registers[reg2.Int()];
        ulong res = regv1 << regv2;
        _registers[reg1.Int()] = res;
        return BiosStatus.Success;
    }

    private BiosStatus InstructionDIV(RegType reg1, RegType reg2)
    {
        ulong divisor = _registers[reg2.Int()];
        if (divisor == 0)
            return RecordFaultContext(BiosStatus.DivOnZero, _registers[RegType.rIP.Int()]);

        ulong dividend = _registers[reg1.Int()];
        ulong res = dividend / divisor;
        _registers[reg1.Int()] = res;
        return BiosStatus.Success;
    }

    private BiosStatus InstructionMOD(RegType reg1, RegType reg2)
    {
        ulong divisor = _registers[reg2.Int()];
        if (divisor == 0)
            return RecordFaultContext(BiosStatus.DivOnZero, _registers[RegType.rIP.Int()]);

        ulong dividend = _registers[reg1.Int()];
        ulong res = dividend % divisor;
        _registers[reg1.Int()] = res;
        return BiosStatus.Success;
    }

    private BiosStatus InstructionAND(RegType reg1, RegType reg2)
    {
        ulong value1 = _registers[reg1.Int()];
        ulong value2 = _registers[reg2.Int()];
        ulong res = value1 & value2;
        _registers[reg1.Int()] = res;
        return BiosStatus.Success;
    }

    private BiosStatus InstructionOR(RegType reg1, RegType reg2)
    {
        ulong value1 = _registers[reg1.Int()];
        ulong value2 = _registers[reg2.Int()];
        ulong res = value1 | value2;
        _registers[reg1.Int()] = res;
        return BiosStatus.Success;
    }

    private BiosStatus InstructionXOR(RegType reg1, RegType reg2)
    {
        ulong value1 = _registers[reg1.Int()];
        ulong value2 = _registers[reg2.Int()];
        ulong res = value1 ^ value2;
        _registers[reg1.Int()] = res;
        return BiosStatus.Success;
    }

    private BiosStatus InstructionNOT(RegType reg)
    {
        ulong value = _registers[reg.Int()];
        ulong res = ~value;
        _registers[reg.Int()] = res;
        return BiosStatus.Success;
    }
}