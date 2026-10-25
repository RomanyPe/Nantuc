using Nantuc.Kernel.Common;
using Nantuc.Kernel.Contracts;
using Nantuc.Kernel.ControllersData;
using Nantuc.Kernel.RamSystem;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using static Nantuc.Kernel.Common.InstructionDecoder;

namespace Nantuc.Kernel.ProcessorSystem;

public sealed class Processor(MemoryBus ram,
                              PortBus portBus,
                              IProcessorFaultPolicy processorFaultPolicy)
{

    [InlineArray(CountReg)]
    private struct ArrayRegisters
    {
        public ulong _element0;
    }

    private const int CountReg = 32;

    private ArrayRegisters _registers;

    private readonly ConcurrentQueue<uint> _externalCommands = new();
    private readonly ConcurrentQueue<BiosStatus> _statusFromSimulation = [];
    private readonly IProcessorFaultPolicy _processorFaultPolicy = processorFaultPolicy;
    private readonly MemoryBus _ram = ram;
    private readonly PortBus _portBus = portBus;

    private ulong _stackBase;
    private bool _isRunning = false;
    private bool _hasSimulationStatus = false;
    private bool _hasExternalCommand = false;
    private bool _isSleeping = false;
    public bool IsSleeping => _isSleeping;
    public bool IsRunning => _isRunning;
    private bool _hasCommand = false;
    private ulong _lastFaultData = 0;


    private bool ZeroFlagSet => (_registers[RegType.rFL.Int()] & 1UL) != 0UL;
    private bool NegativeFlagSet => (_registers[RegType.rFL.Int()] & 2UL) != 0UL;


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong GetRegValue(RegType reg) => _registers[reg.Int()];

    public void EnqueueBiosStatus(BiosStatus status)
    {
        Volatile.Write(ref _hasCommand, true);
        Volatile.Write(ref _hasSimulationStatus, true);
        _statusFromSimulation.Enqueue(status);
    }

    public void CopyRegisters(Span<ulong> destination)
    {
        if (destination.Length < CountReg)
            throw new ArgumentException($"At least {CountReg} elements are required", nameof(destination));
        for (int i = 0; i < CountReg; i++)
            destination[i] = _registers[i];
    }

    public ulong[] GetRegistersSnapshot()
    {
        var arr = new ulong[CountReg];
        CopyRegisters(arr);
        return arr;
    }
   

    private ulong Get64IntData()
    {
        ulong ip = _registers[RegType.rIP.Int()];
        ip = (ip + 7) & ~7UL;
        ulong data = _ram.ReadInt64LEUnSafe(ip);
        ip += 8;
        _registers[RegType.rIP.Int()] = ip;
        return data;
    }

    public void LaunchProgramm(ulong startAddress)
    {
        _isSleeping = false;
        _registers[RegType.rIP.Int()] = startAddress;
        _isRunning = true;

        _registers[RegType.rFL.Int()] = 0;

        ulong stackTop = _ram.Length;
        stackTop &= ~0x7UL;

        _stackBase = stackTop;
        _registers[RegType.rSP.Int()] = stackTop;
    }


    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ProcessExternalCommandsSlow()
    {
        while (_externalCommands.TryDequeue(out uint cmd))
        {
            ExecuteExternalCommand(cmd);
            if (!IsRunning) return;
        }
        Volatile.Write(ref _hasExternalCommand, false);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ProcessSimulationStatusSlow()
    {
        while (_statusFromSimulation.TryDequeue(out BiosStatus result))
        {
            if (result != BiosStatus.Success)
            {
                ProcessorFault fault = new(result, _lastFaultData, _registers[RegType.rIP.Int()], 0);
                Volatile.Write(ref _isRunning, _processorFaultPolicy.ShouldContinue(fault));
                return;
            }
        }
        Volatile.Write(ref _hasSimulationStatus, false);
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void Step()
    {

        if (_hasCommand)
        {
            if (_hasExternalCommand)
                ProcessExternalCommandsSlow();

            if (_hasSimulationStatus)
                ProcessSimulationStatusSlow();

            _hasCommand = false;
        }

        if (!_isRunning | _isSleeping) return;

        ulong ip = _registers[RegType.rIP.Int()];        

        uint rawInst = _ram.ReadInt32LEUnSafe(ip);
        OpCode opCode = GetOpCode(rawInst);

        ip += 4;

        _registers[RegType.rIP.Int()] = ip;

        BiosStatus dat = opCode switch
        {
            // === 1. Системные команды ===
            OpCode.NOP => BiosStatus.Success,
            OpCode.END => EndProgramm(),
            // === 2. Работа с памятью (Указатели и регистры) ===
            OpCode.MOV => InstructionMOV(GetReg1(rawInst), GetReg2(rawInst)),

            OpCode.LOAD => InstructionLOAD(GetDataSizeCode(rawInst), GetReg1(rawInst), Get64IntData()),
            OpCode.STORE => InstructionSTORE(GetDataSizeCode(rawInst), GetReg1(rawInst), Get64IntData()),
            OpCode.STORE_IND => InstructionSTORE_IND(GetDataSizeCode(rawInst), GetReg1(rawInst), GetReg2(rawInst)),
            OpCode.LOAD_IND => InstructionLOAD_IND(GetDataSizeCode(rawInst), GetReg1(rawInst), GetReg2(rawInst)),

            OpCode.LOAD_UNSAFE => InstructionLOAD_UNSAFE(GetDataSizeCode(rawInst), GetReg1(rawInst), Get64IntData()),
            OpCode.STORE_UNSAFE => InstructionSTORE_UNSAFE(GetDataSizeCode(rawInst), GetReg1(rawInst), Get64IntData()),
            OpCode.STORE_IND_UNSAFE => InstructionSTORE_IND_UNSAFE(GetDataSizeCode(rawInst), GetReg1(rawInst), GetReg2(rawInst)),
            OpCode.LOAD_IND_UNSAFE => InstructionLOAD_IND_UNSAFE(GetDataSizeCode(rawInst), GetReg1(rawInst), GetReg2(rawInst)),

            OpCode.LDI => InstructionLDI(GetReg1(rawInst), Get64IntData()),

            // === 3. Арифметика и Логика (Тьюринг-базис) ===
            OpCode.ADD => InstructionADD(GetReg1(rawInst), GetReg2(rawInst)),
            OpCode.SUB => InstructionSUB(GetReg1(rawInst), GetReg2(rawInst)),
            OpCode.INC => InstructionINC(GetReg1(rawInst)),
            OpCode.DEC => InstructionDEC(GetReg1(rawInst)),
            OpCode.MULT_INT => InstructionMULT_INT(GetReg1(rawInst), GetReg2(rawInst)),
            OpCode.SHR => InstructionSHR(GetReg1(rawInst), GetReg2(rawInst)),
            OpCode.SHL => InstructionSHL(GetReg1(rawInst), GetReg2(rawInst)),
            OpCode.DIV => InstructionDIV(GetReg1(rawInst), GetReg2(rawInst)),
            OpCode.MOD => InstructionMOD(GetReg1(rawInst), GetReg2(rawInst)),

            // === 4. Логика ===
            OpCode.AND => InstructionAND(GetReg1(rawInst), GetReg2(rawInst)),
            OpCode.OR => InstructionOR(GetReg1(rawInst), GetReg2(rawInst)),
            OpCode.XOR => InstructionXOR(GetReg1(rawInst), GetReg2(rawInst)),
            OpCode.NOT => InstructionNOT(GetReg1(rawInst)),

            // === 5. Управление потоком ===
            OpCode.JMP => InstructionJMP(Get64IntData()),
            OpCode.JZ => InstructionJZ(Get64IntData()),
            OpCode.JNZ => InstructionJNZ(Get64IntData()),
            OpCode.JG => InstructionJG(Get64IntData()),
            OpCode.JL => InstructionJL(Get64IntData()),

            // === 6. Работа со Стеком ===
            OpCode.PUSH => InstructionPUSH(GetReg1(rawInst)),
            OpCode.POP => InstructionPOP(GetReg1(rawInst)),
            OpCode.CALL => InstructionCALL(Get64IntData()),
            OpCode.RET => InstructionRET(),

            // === 7. Ввод-вывод ===
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

        if (dat != BiosStatus.Success)
        {
            ProcessorFault fault = new(dat, _lastFaultData, ip, opCode);
            Volatile.Write(ref _isRunning, _processorFaultPolicy.ShouldContinue(fault));
        }
    }

    private BiosStatus InstructionMOD(RegType reg1, RegType reg2)
    {
        ulong divisor = _registers[reg2.Int()];
        if (divisor == 0)
        {
            ulong ip = _registers[RegType.rIP.Int()];
            _lastFaultData = ip;
            return BiosStatus.DivOnZero;
        }
        ulong dividend = _registers[reg1.Int()];
        ulong res = dividend % divisor;
        _registers[reg1.Int()] = res;
        return BiosStatus.Success;
    }

    private BiosStatus InstructionJMP_IND(RegType r)
    {
        var nextIp = _registers[r.Int()];
        if (nextIp >= _ram.Length) 
        {
            _lastFaultData = nextIp;
            return BiosStatus.SegmentationFault;
        }
        _registers[RegType.rIP.Int()] = nextIp;
        return BiosStatus.Success;
    }

    private BiosStatus InstructionCALL_IND(RegType r)
    {
        var nextIp = _registers[r.Int()];
        if (nextIp >= _ram.Length)
        {
            _lastFaultData = nextIp;
            return BiosStatus.SegmentationFault;
        }
        return InstructionCALL(nextIp);
    }

    private BiosStatus InstructionCMP(RegType r1, RegType r2)
    {
        ulong res = _registers[r1.Int()] - _registers[r2.Int()];

        ulong zeroFlag = 1UL ^ ((res | (0UL - res)) >> 63);
        ulong negativeFlag = res >> 63;

        _registers[RegType.rFL.Int()] = zeroFlag | (negativeFlag << 1);
        return BiosStatus.Success;
    }

    private BiosStatus InstructionTEST(RegType r1, RegType r2)
    {
        ulong res = _registers[r1.Int()] & _registers[r2.Int()];
        ulong zeroFlag = 1UL ^ ((res | (0UL - res)) >> 63);
        ulong negativeFlag = res >> 63;

        _registers[RegType.rFL.Int()] = zeroFlag | (negativeFlag << 1);
        return BiosStatus.Success;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private BiosStatus SetStatusNotImplementedOpCode(OpCode code)
    {
        _lastFaultData = (ulong)code;
        return BiosStatus.NotImplementedOpCode;
    }


    [MethodImpl(MethodImplOptions.NoInlining)]
    private BiosStatus SetStatusNotSupportedOpCode(OpCode code)
    {
        _lastFaultData = (ulong)code;
        return BiosStatus.NotSupportedOpCode;
    }


    private BiosStatus InstructionHALT()
    {
        Volatile.Write(ref _isSleeping, true);
        Volatile.Write(ref _hasCommand, true);
        return BiosStatus.Success;
    }

    private BiosStatus InstructionWAKE_INT(RegType reg1)
    {
        ulong regV = _registers[reg1.Int()];
        if (!_portBus.WakeProcessor(regV))
        {
            ulong ip = _registers[RegType.rIP.Int()];
            _lastFaultData = ip;
            return BiosStatus.NullDeviceOutput;
        }

        return BiosStatus.Success;
    }

    private BiosStatus InstructionDIV(RegType reg1, RegType reg2)
    {
        ulong divisor = _registers[reg2.Int()];
        if (divisor == 0)
        {
            ulong ip = _registers[RegType.rIP.Int()];
            _lastFaultData = ip;
            return BiosStatus.DivOnZero;
        }
        ulong dividend = _registers[reg1.Int()];
        ulong res = dividend / divisor;
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

    private BiosStatus InstructionMULT_INT(RegType reg1, RegType reg2)
    {
        ulong regv1 = _registers[reg1.Int()];
        ulong regv2 = _registers[reg2.Int()];
        ulong res = regv1 * regv2;
        _registers[reg1.Int()] = res;
        return BiosStatus.Success;
    }

    private BiosStatus EndProgramm()
    {
        Volatile.Write(ref _isRunning, false);
        return BiosStatus.EndProgramm;
    }


    private BiosStatus InstructionMOV(RegType reg1, RegType reg2)
    {
        _registers[reg1.Int()] = _registers[reg2.Int()];
        return BiosStatus.Success;
    }
    private BiosStatus InstructionLOAD(OpCodeSize sizeT, RegType reg, ulong adress)
    {
        BiosStatus status;
        switch (sizeT)
        {
            case OpCodeSize.S8:
                var data8 = _ram.ReadInt8LE(adress);
                status = _ram.UpdateAndReadStatus();
                if (status != BiosStatus.Success)
                {
                    _lastFaultData = _ram.FaultData;
                    return status;
                }
                _registers[reg.Int()] = data8;
                return status;


            case OpCodeSize.S16:
                var data16 = _ram.ReadInt16LE(adress);
                status = _ram.UpdateAndReadStatus();
                if (status != BiosStatus.Success)
                {
                    _lastFaultData = _ram.FaultData;
                    return status;
                }
                _registers[reg.Int()] = data16;

                return BiosStatus.Success;

            case OpCodeSize.S32:
                var data32 = _ram.ReadInt32LE(adress);
                status = _ram.UpdateAndReadStatus();
                if (status != BiosStatus.Success)
                {
                    _lastFaultData = _ram.FaultData;
                    return status;
                }
                _registers[reg.Int()] = data32;
                return BiosStatus.Success;

            case OpCodeSize.S64:
                var data64 = _ram.ReadInt64LE(adress);
                status = _ram.UpdateAndReadStatus();
                if (status != BiosStatus.Success)
                {
                    _lastFaultData = _ram.FaultData;
                    return status;
                }
                _registers[reg.Int()] = data64;
                return BiosStatus.Success;

            default:
                // Защита на случай передачи некорректного или нереализованного OpCodeSize
                _lastFaultData = adress;
                return BiosStatus.SegmentationFault;
        }


    }

    private BiosStatus InstructionSTORE(OpCodeSize sizeT, RegType reg, ulong adress)
    {
        BiosStatus status;
        switch (sizeT)
        {
            case OpCodeSize.S8:
                _ram.WriteInt8LE(adress, (byte)_registers[reg.Int()]);
                status = _ram.UpdateAndReadStatus();
                if (status != BiosStatus.Success)
                {
                    _lastFaultData = _ram.FaultData;
                    return status;
                }
                return status;

            case OpCodeSize.S16:
                _ram.WriteInt16LE(adress, (ushort)_registers[reg.Int()]);
                status = _ram.UpdateAndReadStatus();
                if (status != BiosStatus.Success)
                {
                    _lastFaultData = _ram.FaultData;
                    return status;
                }
                return status;


            case OpCodeSize.S32:
                _ram.WriteInt32LE(adress, (uint)_registers[reg.Int()]);
                status = _ram.UpdateAndReadStatus();
                if (status != BiosStatus.Success)
                {
                    _lastFaultData = _ram.FaultData;
                    return status;
                }
                return status;


            case OpCodeSize.S64:
                _ram.WriteInt64LE(adress, _registers[reg.Int()]);
                status = _ram.UpdateAndReadStatus();
                if (status != BiosStatus.Success)
                {
                    _lastFaultData = _ram.FaultData;
                    return status;
                }
                return status;


            default:
                _lastFaultData = adress;
                return BiosStatus.AlignmentFault;
        }
    }

    private BiosStatus InstructionLOAD_IND(OpCodeSize sizeT, RegType reg1, RegType reg2)
    {
        return InstructionLOAD(sizeT, reg1, _registers[reg2.Int()]);
    }

    private BiosStatus InstructionSTORE_IND(OpCodeSize sizeT, RegType reg1, RegType reg2)
    {
        return InstructionSTORE(sizeT, reg1, _registers[reg2.Int()]);
    }
    private BiosStatus InstructionLOAD_UNSAFE(OpCodeSize sizeT, RegType reg, ulong adress)
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

    private BiosStatus InstructionSTORE_UNSAFE(OpCodeSize sizeT, RegType reg, ulong adress)
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

    private BiosStatus InstructionLOAD_IND_UNSAFE(OpCodeSize sizeT, RegType reg1, RegType reg2)
    {
        return InstructionLOAD_UNSAFE(sizeT, reg1, _registers[reg2.Int()]);
    }

    private BiosStatus InstructionSTORE_IND_UNSAFE(OpCodeSize sizeT, RegType reg1, RegType reg2)
    {
        return InstructionSTORE_UNSAFE(sizeT, reg1, _registers[reg2.Int()]);
    }

    private BiosStatus InstructionLDI(RegType reg, ulong value)
    {
        _registers[reg.Int()] = value;
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
    private BiosStatus InstructionJMP(ulong targetAddress)
    {
        _registers[RegType.rIP.Int()] = targetAddress;
        return BiosStatus.Success;
    }

    private BiosStatus InstructionJZ(ulong t) 
    { 
        if (ZeroFlagSet) _registers[RegType.rIP.Int()] = t; 
        return BiosStatus.Success; 
    }
    private BiosStatus InstructionJNZ(ulong t) 
    { 
        if (!ZeroFlagSet) _registers[RegType.rIP.Int()] = t;
        return BiosStatus.Success; 
    }
    private BiosStatus InstructionJL(ulong t) 
    { 
        if (NegativeFlagSet) _registers[RegType.rIP.Int()] = t;
        return BiosStatus.Success; 
    }
    private BiosStatus InstructionJG(ulong t) 
    { 
        if (!NegativeFlagSet & !ZeroFlagSet) _registers[RegType.rIP.Int()] = t;
        return BiosStatus.Success; 
    }

    private BiosStatus InstructionPUSH(RegType reg)
    {
        ulong value = _registers[reg.Int()];
        ulong sp = _registers[RegType.rSP.Int()];

        sp -= 8;

        _ram.WriteInt64LE(sp, value);
        var status = _ram.UpdateAndReadStatus();
        if (status != BiosStatus.Success)
        {
            _lastFaultData = _ram.FaultData;
            return status;
        }

        if (_registers[RegType.rHP.Int()] >= sp)   // коллизия с стеком
        {
            _lastFaultData = sp;
            return BiosStatus.SegmentationFault;
        }

        _registers[RegType.rSP.Int()] = sp;
        return BiosStatus.Success;
    }

    private BiosStatus InstructionPOP(RegType reg)
    {
        ulong sp = _registers[RegType.rSP.Int()];

        var readResult = _ram.ReadInt64LE(sp);

        var status = _ram.UpdateAndReadStatus();
        if (status != BiosStatus.Success)
        {
            _lastFaultData = _ram.FaultData;
            return status;
        }

        sp += 8;

        _registers[reg.Int()] = readResult;
        _registers[RegType.rSP.Int()] = sp;

        return BiosStatus.Success;
    }


    private BiosStatus InstructionCALL(ulong targetAddress)
    {
        ulong oldSp = _registers[RegType.rSP.Int()];

        if (oldSp < 8)
            return BiosStatus.SegmentationFault;

        ulong sp = oldSp - 8;

        if (_registers[RegType.rHP.Int()] >= sp) // коллизия с heap
            return BiosStatus.SegmentationFault;

        _ram.WriteInt64LEUnSafe(sp, _registers[RegType.rIP.Int()]);

        _registers[RegType.rSP.Int()] = sp;
        _registers[RegType.rIP.Int()] = targetAddress;

        return BiosStatus.Success;
    }
    

    private BiosStatus InstructionRET()
    {
        ulong sp = _registers[RegType.rSP.Int()];

        if (sp >= _stackBase)
        {
            _lastFaultData = _registers[RegType.rIP.Int()];
            return BiosStatus.StackUnderflow;
        }

        ulong returnAddress = _ram.ReadInt64LEUnSafe(sp);

        _registers[RegType.rSP.Int()] = sp + 8;
        _registers[RegType.rIP.Int()] = returnAddress;

        return BiosStatus.Success;
    }


    private BiosStatus InstructionIN(RegType regDest, RegType regPort)
    {
        ulong portAddress = _registers[regPort.Int()];
        var result = _portBus.ReadPort(portAddress);
        if (result.Status != BiosStatus.Success)
        {
            _lastFaultData = _ram.FaultData;
            return result.Status;
        }

        _registers[regDest.Int()] = result.Data;
        return BiosStatus.Success;
    }

    private BiosStatus InstructionOUT(RegType regSrc, RegType regPort)
    {
        ulong portAddress = _registers[regPort.Int()];
        byte value = (byte)(_registers[regSrc.Int()] & 0xFF);
        var status = _portBus.WritePort(portAddress, value);
        if (status != BiosStatus.Success)
        {
            _lastFaultData = _ram.FaultData;
            return status;
        }
        // Флаги обычно не меняются при выводе, но можно обновить по желанию
        return BiosStatus.Success;
    }

    public void Reset()
    {
        _lastFaultData = 0;
        _isRunning = false;
        _isSleeping = false;
        ClearRegs();
        while (_externalCommands.TryDequeue(out _));
        while (_statusFromSimulation.TryDequeue(out _));
        _hasCommand = false;
        _hasExternalCommand = false;
        _hasSimulationStatus = false;
    }

    private void ClearRegs()
    {
        for (int i = 0; i < CountReg; i++) _registers[i] = 0UL;
    }

    public void InitRegHP(ulong hpInit) => _registers[RegType.rHP.Int()] = hpInit;

    public void EnqueueExternalCommand(uint instruction)
    {
        _externalCommands.Enqueue(instruction);
        Volatile.Write(ref _hasExternalCommand, true);
        Volatile.Write(ref _hasCommand, true);
    }

    private void ExecuteExternalCommand(uint rawInst)
    {
        _hasCommand = true;
        OpCode opCode = GetOpCode(rawInst);
        switch (opCode)
        {
            case OpCode.END: _isRunning = false; return;
            case OpCode.HALT: _isSleeping = true; return;
            case OpCode.WAKE: _isSleeping = false; return;

            default: return;
        }
    }

    private readonly ProcessorReader _processorReader = new();
    public IProcessorReader GetCurrentProcessorReader()
    {
        _processorReader.Update(_isRunning, _isSleeping, _registers);
        return _processorReader;
    }

    private sealed class ProcessorReader : IProcessorReader
    {
        private bool _isRunning;
        private bool _isSleeping;
        private ArrayRegisters _registers;

        public bool IsRunning => _isRunning;
        public bool IsSleeping => _isSleeping;

        public void Update(bool running, bool sleeping, ArrayRegisters regs)
        {
            _isRunning = running;
            _isSleeping = sleeping;
            _registers = regs;
        }

        public ulong GetRegister(RegType regType)
        {
            if ((uint)regType >= CountReg)
                throw new ArgumentOutOfRangeException(nameof(regType));
            return _registers[regType.Int()];
        }
    }
}

