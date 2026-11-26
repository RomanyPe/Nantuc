using Nantuc.Common;

namespace Nantuc.Kernel.ProcessorSystem;

public sealed partial class Processor
{
    // ============================================================
    // Флаги: CMP/TEST, предикаты Zero/Negative, SetFlags.
    // ============================================================

    private bool ZeroFlagSet => (_registers[RegType.rFL.Int()] & 1UL) != 0UL;
    private bool NegativeFlagSet => (_registers[RegType.rFL.Int()] & 2UL) != 0UL;
    private bool CarryFlagSet => (_registers[RegType.rFL.Int()] & 4UL) != 0UL; // флаг заема (Borrow/Carry)

    private void SetFlags(ulong res, ulong carryFlag = 0UL)
    {
        ulong zeroFlag = (res == 0) ? 1UL : 0UL;
        ulong negativeFlag = res >> 63;

        // Формируем регистр флагов: 
        // бит 0 = Zero, бит 1 = Negative, бит 2 = Carry
        _registers[RegType.rFL.Int()] = zeroFlag | (negativeFlag << 1) | (carryFlag << 2);
    }

    private BiosStatus InstructionCMP(RegType r1, RegType r2)
    {
        ulong v1 = _registers[r1.Int()];
        ulong v2 = _registers[r2.Int()];
        ulong res = v1 - v2;

        ulong carryFlag = (v1 < v2) ? 1UL : 0UL;

        SetFlags(res, carryFlag);
        return BiosStatus.Success;
    }

    private BiosStatus InstructionTEST(RegType r1, RegType r2)
    {
        ulong res = _registers[r1.Int()] & _registers[r2.Int()];

        SetFlags(res, 0UL);
        return BiosStatus.Success;
    }
}