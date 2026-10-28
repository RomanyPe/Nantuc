using Nantuc.Kernel.Common;
using Nantuc.Kernel.Contracts;
using Nantuc.Kernel.ControllersData;
using Nantuc.Kernel.ProcessorSystem;
using Nantuc.Kernel.RamSystem;
using System.Diagnostics;

namespace Nantuc.Kernel.BiosSystem;

public sealed class Device : IPortController
{
    
    private sealed class SimulationResult(TimeSpan elapsed, long steps) : ITimerSimulationResult
    {
        public TimeSpan Elapsed => elapsed;
        public long Steps => steps;
    }

    private readonly byte[] _ioMemory;
    private readonly ManualResetEventSlim _wakeSignal = new(false);
    private readonly MemoryBus _ram;
    private readonly Processor _processor;
    private readonly PortBus _portBus;
    private readonly IProcessorFaultPolicy _processorFaultPolicy;
    private long stepCounter = 0;
    private int _disposed;

    public INativeReadOnlyBuffer NativeReadOnlyBuffer => _ram.NativeReadOnlyBuffer;
    public bool IsRunning => _processor.IsRunning;
    public ulong MaxRamSize => _ram.Length;
    public long StepCount => stepCounter;


    public Device(PortBus portBus, RamSize size, IProcessorFaultPolicy processorFaultPolicy)
    {
        _processorFaultPolicy = processorFaultPolicy;
        _portBus = portBus;
        _ram = new MemoryBus(size);
        _processor = new Processor(_ram, _portBus, _processorFaultPolicy);
        _ioMemory = new byte[_portBus.PortsOnDevice];
    }

    public void CopyRegisters(Span<ulong> destination)
    {
        _processor.CopyRegisters(destination);   
    }
    public ulong[] GetRegistersSnapshot()
    {
        return _processor.GetRegistersSnapshot();
    }

    public void LoadProgram(ReadOnlySpan<byte> program, ulong loadAddress)
    {
        ulong ramLength = _ram.Length;
        ulong programLength = (ulong)program.Length;

        if (loadAddress + programLength < loadAddress || loadAddress + programLength > ramLength)
        {
            throw new InvalidOperationException(
                $"Failed to write the program to the address 0x{_ram.FaultData:X}");
        }
        _ram.LoadProgram(program, loadAddress);
        InitHeap(loadAddress, (ulong)program.Length);
    }

    public bool TryLoadProgram(ReadOnlySpan<byte> program, ulong loadAddress)
    {
        ulong ramLength = _ram.Length;
        ulong programLength = (ulong)program.Length;

        if (loadAddress + programLength < loadAddress || loadAddress + programLength > ramLength)
        {
            return false;
        }
        _ram.LoadProgram(program, loadAddress);
        InitHeap(loadAddress, (ulong)program.Length);
        return true;
    }

    public ProcessorFault Run(ulong start,
                    long? limit,
                    bool snowTimer,
                    Action? onLaunch,
                    Action<ITimerSimulationResult?>? onEnd)
    {

        onLaunch?.Invoke();

        ulong startIndex = start;
        stepCounter = 0;

        _processor.LaunchProgramm(startIndex);

        Stopwatch? sw = snowTimer ? Stopwatch.StartNew() : null;
        if (limit.HasValue)
            RunSimulationLoopWithLimit(limit.Value);
        else
            RunSimulationLoop();

        SimulationResult? timeSpan = null;
        if (sw != null)
        {
            sw.Stop();
            timeSpan = new(sw.Elapsed, stepCounter);
        }

        onEnd?.Invoke(timeSpan);
        return _processor.GetDiagnosticData();
    }

    public ProcessorFault DebugRun(ulong start,
                        long stepLimit,
                        int delay,
                        Action? onLaunch,
                        Action<IProcessorReader>? onStep,
                        Action? onEnd)
    {
        bool useSleepMode = delay != 0;

        onLaunch?.Invoke();
        stepCounter = 0;

        _processor.LaunchProgramm(start);
        RunSimulationLoopDebug(delay, useSleepMode, stepLimit, onStep);

        onEnd?.Invoke();
        return _processor.GetDiagnosticData();
    }


    public void BreakPointerLaunchDevice(ulong? start, Action? titleAct)
    {
        titleAct?.Invoke();
        stepCounter = 0;
        ulong startIndex = start ?? _ram.Length;

        _processor.LaunchProgramm(startIndex);
    }


    public void Stop()
    {
        _processor.EnqueueBiosStatus(BiosStatus.EndProgramm);
        _processor.EnqueueExternalCommand((uint)OpCode.WAKE);
        _wakeSignal.Set();
    }

    private void InitHeap(ulong loadAddress, ulong length)
    {
        ulong programEnd = loadAddress + length;
        ulong heapStart = programEnd.AlignUp(8UL);
        _processor.InitRegHP(heapStart);
    }

    private void RunSimulationLoop()
    {
        while (_processor.IsRunning)
        {
            _processor.Step();

            if (_processor.IsSleeping)
            {
                _wakeSignal.Wait(15);
                _wakeSignal.Reset();
            }
            else
            {
                stepCounter++;
            }
        }
    }
    private void RunSimulationLoopWithLimit(long limit)
    {
        while (_processor.IsRunning)
        {
            if (limit <= stepCounter) _processor.EnqueueBiosStatus(BiosStatus.InfinityLoop);
            _processor.Step();

            if (_processor.IsSleeping)
            {
                _wakeSignal.Wait(15);
                _wakeSignal.Reset();
            }
            else
            {
                stepCounter++;
            }
        }
    }

    private void RunSimulationLoopDebug(int delay, bool useSleepMode, long stepLimit, Action<IProcessorReader>? onStep)
    {
        while (_processor.IsRunning)
        {
            if (stepCounter >= stepLimit) _processor.EnqueueBiosStatus(BiosStatus.InfinityLoop);
            _processor.Step();

            if (_processor.IsSleeping)
            {
                _wakeSignal.Wait(15);
                _wakeSignal.Reset();
            }
            else
            {
                stepCounter++;
            }
            
            onStep?.Invoke(_processor.GetCurrentProcessorReader());
            if (useSleepMode) Thread.Sleep(delay);
        }
    }



    public byte ReadPort(ulong offset)
    {
        if (offset >= (ulong)_ioMemory.Length)
            return 0; // или ошибка
        return _ioMemory[offset];
    }

    public void WritePort(ulong offset, byte value)
    {
        if (offset < (ulong)_ioMemory.Length)
            _ioMemory[offset] = value;
    }

    public void WakeProcessor()
    {
        _processor.EnqueueExternalCommand((uint)OpCode.WAKE);
        _wakeSignal.Set();
    }

    public void EnqueueExternalCommand(uint instruction)
    {
        _processor.EnqueueExternalCommand(instruction);
        _wakeSignal.Set();
    }
    public void NextStepProcessor(Action<IProcessorReader>? OnStep)
    {
        if (!_processor.IsRunning) return;

        _processor.Step();
        OnStep?.Invoke(_processor.GetCurrentProcessorReader());
        if (!_processor.IsSleeping)
        {
            stepCounter++;
        }
    }

    public void NextStepProcessorCount(int count, Action<IProcessorReader>? OnStep)
    {
        if (!_processor.IsRunning) return;
        bool needLog = OnStep != null;
        for (int i = 0; i < count; i++)
        {
            _processor.Step();
            if (needLog)
            {
                OnStep!.Invoke(_processor.GetCurrentProcessorReader());
            }
            if (!_processor.IsSleeping)
            {
                stepCounter++;
            }
        }
    }


    public void ResetMemoryRam() => _ram.ClearMemory();

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        Stop();
        _ram?.Dispose();
        GC.SuppressFinalize(this);
    }

    public void ClearRegisters()
    {
        _processor.Reset();
    }

    public ProcessorFault GetLastFault() => _processor.GetDiagnosticData();
}