using Nantuc.Common;
using Nantuc.Kernel.ControllersData;
using System.Runtime.CompilerServices;
using static Nantuc.Common.InstructionDecoder;

namespace Nantuc.Kernel.ProcessorSystem;

public sealed partial class Processor
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void Step()
    {
        if (Volatile.Read(ref _hasCommand))
        {
            Volatile.Write(ref _hasCommand, false);      // ← до обработки

            if (Volatile.Read(ref _hasExternalCommand))
                ProcessExternalCommandsSlow();
            if (Volatile.Read(ref _hasSimulationStatus))
                ProcessSimulationStatusSlow();
        }

        if (!Volatile.Read(ref _isRunning) | Volatile.Read(ref _isSleeping)) return;

        OpCode opCode = OpCode.NOP;
        BiosStatus dat;

        ulong ip = _registers[RegType.rIP.Int()];

        if (ip > _ram.Length - 8)
        {
            dat = RecordFaultContext(BiosStatus.SegmentationFault, ip);
        }
        else
        {
            uint rawInst = _ram.ReadInt32LEUnSafe(ip);
            opCode = GetOpCode(rawInst);

            ip += 4;
            _registers[RegType.rIP.Int()] = ip;

            dat = opCode switch
            {
                OpCode.NOP => BiosStatus.Success,
                OpCode.END => EndProgram(),
                OpCode.MOV => InstructionMOV(GetReg1(rawInst), GetReg2(rawInst)),
                OpCode.CASTZ => InstructionCASTZ(GetReg1(rawInst), GetDataSizeCode(rawInst)),
                OpCode.CASTS => InstructionCASTS(GetReg1(rawInst), GetDataSizeCode(rawInst)),

                OpCode.LOAD => InstructionLOAD(GetDataSizeCode(rawInst), GetReg1(rawInst)),
                OpCode.STORE => InstructionSTORE(GetDataSizeCode(rawInst), GetReg1(rawInst)),
                OpCode.STORE_IND => InstructionSTORE_IND(GetDataSizeCode(rawInst), GetReg1(rawInst), GetReg2(rawInst)),
                OpCode.LOAD_IND => InstructionLOAD_IND(GetDataSizeCode(rawInst), GetReg1(rawInst), GetReg2(rawInst)),

                OpCode.LOAD_UNSAFE => InstructionLOAD_UNSAFE(GetDataSizeCode(rawInst), GetReg1(rawInst)),
                OpCode.STORE_UNSAFE => InstructionSTORE_UNSAFE(GetDataSizeCode(rawInst), GetReg1(rawInst)),
                OpCode.STORE_IND_UNSAFE => InstructionSTORE_IND_UNSAFE(GetDataSizeCode(rawInst), GetReg1(rawInst), GetReg2(rawInst)),
                OpCode.LOAD_IND_UNSAFE => InstructionLOAD_IND_UNSAFE(GetDataSizeCode(rawInst), GetReg1(rawInst), GetReg2(rawInst)),

                OpCode.LDI => InstructionLDI(GetReg1(rawInst)),

                OpCode.ADD => InstructionADD(GetReg1(rawInst), GetReg2(rawInst)),
                OpCode.SUB => InstructionSUB(GetReg1(rawInst), GetReg2(rawInst)),
                OpCode.INC => InstructionINC(GetReg1(rawInst)),
                OpCode.DEC => InstructionDEC(GetReg1(rawInst)),
                OpCode.MULT_INT => InstructionMULT_INT(GetReg1(rawInst), GetReg2(rawInst)),
                OpCode.SHR => InstructionSHR(GetReg1(rawInst), GetReg2(rawInst)),
                OpCode.SHL => InstructionSHL(GetReg1(rawInst), GetReg2(rawInst)),
                OpCode.DIV => InstructionDIV(GetReg1(rawInst), GetReg2(rawInst)),
                OpCode.MOD => InstructionMOD(GetReg1(rawInst), GetReg2(rawInst)),

                OpCode.AND => InstructionAND(GetReg1(rawInst), GetReg2(rawInst)),
                OpCode.OR => InstructionOR(GetReg1(rawInst), GetReg2(rawInst)),
                OpCode.XOR => InstructionXOR(GetReg1(rawInst), GetReg2(rawInst)),
                OpCode.NOT => InstructionNOT(GetReg1(rawInst)),

                OpCode.JMP => InstructionJMP(),
                OpCode.JZ => InstructionJZ(),
                OpCode.JNZ => InstructionJNZ(),
                OpCode.JG => InstructionJG(),
                OpCode.JL => InstructionJL(),
                OpCode.JA => InstructionJA(),
                OpCode.JB => InstructionJB(),

                OpCode.PUSH => InstructionPUSH(GetReg1(rawInst)),
                OpCode.POP => InstructionPOP(GetReg1(rawInst)),
                OpCode.CALL => InstructionCALL(),
                OpCode.RET => InstructionRET(),

                OpCode.IN => InstructionIN(GetReg1(rawInst), GetReg2(rawInst)),
                OpCode.OUT => InstructionOUT(GetReg1(rawInst), GetReg2(rawInst)),
                OpCode.CALL_IND => InstructionCALL_IND(GetReg1(rawInst)),
                OpCode.JMP_IND => InstructionJMP_IND(GetReg1(rawInst)),

                OpCode.WAKE_INT => InstructionWAKE_INT(GetReg1(rawInst)),
                OpCode.HALT => InstructionHALT(),
                OpCode.CMP => InstructionCMP(GetReg1(rawInst), GetReg2(rawInst)),
                OpCode.TEST => InstructionTEST(GetReg1(rawInst), GetReg2(rawInst)),
                OpCode.WAKE => BiosStatus.Success,

                _ => SetStatusNotImplementedOpCode(opCode),
            };
        }

        if (dat != BiosStatus.Success)
        {
            _faultData = new(dat, _lastFaultData, ip, opCode);
            Volatile.Write(ref _isRunning, !IsTerminal(dat) && _processorPolicy.ShouldContinue(_faultData));
        }
    }
}