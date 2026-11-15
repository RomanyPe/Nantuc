using Nantuc.Common;

namespace Nantuc.Kernel.ProcessorSystem;

public sealed partial class Processor
{
    // ============================================================
    // Флаги: CMP/TEST, предикаты Zero/Negative, SetFlags.
    // ============================================================

    private bool ZeroFlagSet => (_registers[RegType.rFL.Int()] & 1UL) != 0UL;
    private bool NegativeFlagSet => (_registers[RegType.rFL.Int()] & 2UL) != 0UL;

    private void SetFlags(ulong res)
    {
        ulong zeroFlag = 1UL ^ ((res | (0UL - res)) >> 63);
        ulong negativeFlag = res >> 63;

        _registers[RegType.rFL.Int()] = zeroFlag | (negativeFlag << 1);
    }

    private BiosStatus InstructionCMP(RegType r1, RegType r2)
    {
        ulong res = _registers[r1.Int()] - _registers[r2.Int()];
        SetFlags(res);
        return BiosStatus.Success;
    }

    private BiosStatus InstructionTEST(RegType r1, RegType r2)
    {
        ulong res = _registers[r1.Int()] & _registers[r2.Int()];
        SetFlags(res);
        return BiosStatus.Success;
    }
}