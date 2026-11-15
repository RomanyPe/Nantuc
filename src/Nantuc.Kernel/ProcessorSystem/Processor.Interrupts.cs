using Nantuc.Common;
using Nantuc.Kernel.ControllersData;
using System.Runtime.CompilerServices;
using static Nantuc.Common.InstructionDecoder;

namespace Nantuc.Kernel.ProcessorSystem;

public sealed partial class Processor
{
    // ============================================================
    // Внешние команды и статусы BIOS из очередей.
    // ============================================================

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
                _faultData = new(result, _lastFaultData, _registers[RegType.rIP.Int()], 0);
                Volatile.Write(ref _isRunning, !IsTerminal(result) && _processorPolicy.ShouldContinue(_faultData));
                return;
            }
        }
        Volatile.Write(ref _hasSimulationStatus, false);
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
}