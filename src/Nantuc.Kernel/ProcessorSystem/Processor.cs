using Nantuc.Common;
using Nantuc.Contracts;
using Nantuc.Kernel.ControllersData;
using Nantuc.Kernel.RamSystem;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using static Nantuc.Common.InstructionDecoder;

namespace Nantuc.Kernel.ProcessorSystem;

/// <summary>
/// Процессор виртуальной машины Nantuc: исполняет инструкции <see cref="OpCode"/>,
/// взаимодействует с ОЗУ через <see cref="MemoryBus"/> и с устройствами через <see cref="PortBus"/>.
/// </summary>
/// <remarks>
/// <para>
/// Состояние: 32 регистра по 64 бита (см. <see cref="RegType"/>), флаги в <see cref="RegType.rFL"/>,
/// очереди внешних команд и статусов BIOS. Основной цикл исполнения — <see cref="Step"/>.
/// </para>
/// <para>
/// <see cref="Step"/> и метод <see cref="LaunchProgramm"/> не являются потокобезопасными
/// относительно друг друга; <see cref="EnqueueExternalCommand"/> и <see cref="EnqueueBiosStatus"/>
/// можно вызывать из другого потока — они используют lock-free очередь и <c>Volatile</c>-запись флагов.
/// </para>
/// </remarks>
/// <param name="ram">Шина ОЗУ.</param>
/// <param name="portBus">Шина портов ввода-вывода.</param>
/// <param name="processorPolicy">Политика, определяющая, продолжать ли исполнение после сбоя.</param>
public sealed partial class Processor(MemoryBus ram, IPortBus portBus, IProcessorPolicy processorPolicy)
{
    [InlineArray(CountReg)]
    private struct ArrayRegisters { public ulong _element0; }

    private const int CountReg = 32;

    private ArrayRegisters _registers;

    private readonly ConcurrentQueue<uint> _externalCommands = new();
    private readonly ConcurrentQueue<BiosStatus> _statusFromSimulation = [];
    private readonly IProcessorPolicy _processorPolicy = processorPolicy;
    private readonly MemoryBus _ram = ram;
    private readonly IPortBus _portBus = portBus;

    private ulong _stackBase;
    private bool _isRunning;
    private bool _hasSimulationStatus;
    private bool _hasExternalCommand;
    private bool _isSleeping;
    private bool _hasCommand;
    private ulong _lastFaultData;

    private ProcessorFault _faultData = new(BiosStatus.Success, 0, 0, OpCode.NOP);

    public bool IsSleeping => _isSleeping;
    public bool IsRunning => _isRunning;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong GetRegValue(RegType reg) => _registers[reg.Int()];

    public ProcessorFault GetDiagnosticData() => _faultData;

    public void EnqueueBiosStatus(BiosStatus status)
    {
        _statusFromSimulation.Enqueue(status);
        Volatile.Write(ref _hasSimulationStatus, true);
        Volatile.Write(ref _hasCommand, true);
    }

    public void EnqueueExternalCommand(uint instruction)
    {
        _externalCommands.Enqueue(instruction);
        Volatile.Write(ref _hasExternalCommand, true);
        Volatile.Write(ref _hasCommand, true);
    }

    public void CopyRegisters(Span<ulong> destination)
    {
        if (destination.Length < CountReg)
            throw new ArgumentException($"At least {CountReg} elements are required", nameof(destination));
        for (int i = 0; i < CountReg; i++)
            destination[i] = _registers[i];
    }

    public void LaunchProgramm(ulong startAddress)
    {
        _isSleeping = false;
        _registers[RegType.rIP.Int()] = startAddress;
        _isRunning = true;
        _registers[RegType.rFL.Int()] = 0;

        ulong stackTop = _ram.Length & ~0x7UL;
        _stackBase = stackTop;
        _registers[RegType.rSP.Int()] = stackTop;
    }

    public void Reset()
    {
        _faultData = new ProcessorFault(BiosStatus.Success, 0, 0, OpCode.NOP);
        _lastFaultData = 0;
        _isRunning = false;
        _isSleeping = false;
        ClearRegs();
        while (_externalCommands.TryDequeue(out _)) ;
        while (_statusFromSimulation.TryDequeue(out _)) ;
        _hasCommand = false;
        _hasExternalCommand = false;
        _hasSimulationStatus = false;
    }

    public void InitRegHP(ulong hpInit) => _registers[RegType.rHP.Int()] = hpInit;

    private void ClearRegs()
    {
        for (int i = 0; i < CountReg; i++) _registers[i] = 0UL;
    }
}