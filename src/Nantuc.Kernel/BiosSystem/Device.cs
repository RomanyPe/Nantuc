using Nantuc.Common;
using Nantuc.Contracts;
using Nantuc.Kernel.ControllersData;
using Nantuc.Kernel.ProcessorSystem;
using Nantuc.Kernel.RamSystem;
using System.Diagnostics;

namespace Nantuc.Kernel.BiosSystem;

/// <summary>
/// Вычислительное устройство: собственные ОЗУ и процессор, плюс блок портов ввода-вывода.
/// Реализует <see cref="IPortController"/>, поэтому может быть зарегистрировано на <see cref="PortBus"/>.
/// </summary>
/// <remarks>
/// <para>
/// Устройство не является потокобезопасным: одновременно выполнять <see cref="Run"/>,
/// <see cref="DebugRun"/> или пошаговые методы из разных потоков нельзя. <see cref="Stop"/>
/// и <see cref="WakeProcessor"/> можно вызывать из другого потока — они сигналят
/// внутренний <see cref="ManualResetEventSlim"/> и очередь команд процессора.
/// </para>
/// </remarks>
public sealed class Device : IPortController
{
    private readonly byte[] _ioMemory;
    private readonly ManualResetEventSlim _wakeSignal = new(false);
    private readonly MemoryBus _ram;
    private readonly Processor _processor;
    private readonly IProcessorPolicy _processorPolicy;
    private long _stepCounter = 0;
    private int _disposed = 0;

    /// <summary>
    /// Прямой доступ к ОЗУ устройства только для чтения (нативное представление).
    /// </summary>
    public IReadOnlyNativeBuffer NativeReadOnlyBuffer => _ram.NativeReadOnlyBuffer;

    /// <summary>
    /// Признак того, что процессор устройства в данный момент исполняет программу.
    /// </summary>
    public bool IsRunning => _processor.IsRunning;

    /// <summary>
    /// Размер ОЗУ устройства в байтах.
    /// </summary>
    public ulong MaxRamSize => _ram.Length;

    /// <summary>
    /// Количество выполненных инструкций с момента последнего запуска.
    /// </summary>
    public long StepCount => _stepCounter;


    /// <summary>
    /// Создаёт устройство с собственной ОЗУ и процессором.
    /// </summary>
    /// <param name="portBus">Общая шина портов ввода-вывода.</param>
    /// <param name="size">Размер ОЗУ.</param>
    /// <param name="processorFaultPolicy">Политика обработки сбоев процессора.</param>
    public Device(PortBus portBus, RamSize size, IProcessorPolicy processorFaultPolicy)
    {
        _processorPolicy = processorFaultPolicy;
        _ram = new MemoryBus(size);
        _processor = new Processor(_ram, portBus, _processorPolicy);
        _ioMemory = new byte[portBus.PortsOnDevice];
    }

    /// <summary>
    /// Копирует текущие значения регистров процессора в указанный буфер.
    /// </summary>
    /// <param name="destination">Буфер не менее чем из 32 элементов.</param>
    /// <exception cref="ArgumentException">Если <paramref name="destination"/> короче 32 элементов.</exception>
    public void CopyRegisters(Span<ulong> destination)
    {
        _processor.CopyRegisters(destination);
    }

    /// <summary>
    /// Загружает программу в ОЗУ устройства и инициализирует указатель кучи.
    /// </summary>
    /// <param name="program">Байты программы.</param>
    /// <param name="loadAddress">Адрес, по которому размещается программа.</param>
    /// <exception cref="InvalidOperationException">
    /// Если программа не помещается в ОЗУ (переполнение адресного пространства или выход за границы).
    /// </exception>
    public void LoadProgram(ReadOnlySpan<byte> program, ulong loadAddress)
    {
        ThrowIfDisposed();
        ulong ramLength = _ram.Length;
        ulong programLength = (ulong)program.Length;

        if (loadAddress > ramLength || programLength > ramLength - loadAddress)
            throw new InvalidOperationException(
                $"Failed to write the program to the address 0x{loadAddress:X}");
        
        _ram.LoadProgram(program, loadAddress);
        InitHeap(loadAddress, (ulong)program.Length);
    }

    /// <summary>
    /// Пытается загрузить программу в ОЗУ устройства и инициализировать указатель кучи.
    /// </summary>
    /// <param name="program">Байты программы.</param>
    /// <param name="loadAddress">Адрес, по которому размещается программа.</param>
    /// <returns><see langword="false"/>, если программа не помещается в ОЗУ; иначе <see langword="true"/>.</returns>
    public bool TryLoadProgram(ReadOnlySpan<byte> program, ulong loadAddress)
    {
        if (Volatile.Read(ref _disposed) != 0) return false;

        ulong ramLength = _ram.Length;
        ulong programLength = (ulong)program.Length;

        if (loadAddress > ramLength || programLength > ramLength - loadAddress)
            return false;
        
        _ram.LoadProgram(program, loadAddress);
        InitHeap(loadAddress, (ulong)program.Length);
        return true;
    }

    /// <summary>
    /// Запускает программу в блокирующем режиме до её завершения или сбоя.
    /// </summary>
    /// <param name="start">Адрес первой инструкции.</param>
    /// <param name="limit">Лимит шагов; <see langword="null"/> — без ограничения. При превышении генерируется <see cref="BiosStatus.InfinityLoop"/>.</param>
    /// <param name="showTimer"><see langword="true"/>, чтобы замерить время выполнения через <see cref="Stopwatch"/>.</param>
    /// <param name="onLaunch">Необязательный колбэк, вызываемый перед запуском.</param>
    /// <param name="onEnd">Необязательный колбэк, получающий результат замера времени (или <see langword="null"/>, если таймер не включён).</param>
    /// <returns>Диагностические данные о последнем сбое/остановке.</returns>
    public ProcessorFault Run(ulong startIndex,
                    long? limit,
                    bool showTimer,
                    Action? onLaunch,
                    Action<TimeSimulationResult>? onEnd)
    {
        GlobalInit(onLaunch, startIndex);

        Stopwatch? sw = showTimer ? Stopwatch.StartNew() : null;
        if (limit.HasValue) RunSimulationLoopWithLimit(limit.Value);
        else RunSimulationLoop();

        TimeSimulationResult timeSpan = CreateTimeSimulationResult(sw);

        onEnd?.Invoke(timeSpan);
        return _processor.GetDiagnosticData();
    }

    private TimeSimulationResult CreateTimeSimulationResult(Stopwatch? sw)
    {
        TimeSimulationResult timeSpan;
        if (sw != null)
        {
            sw.Stop();
            timeSpan = new(sw.Elapsed, Interlocked.Read(ref _stepCounter));
        }
        else
        {
            timeSpan = new(TimeSpan.Zero, Interlocked.Read(ref _stepCounter));
        }

        return timeSpan;
    }
    /// <summary>
    /// Запускает программу в режиме отладки с задержкой между шагами и колбэком на каждый шаг.
    /// </summary>
    /// <param name="start">Адрес первой инструкции.</param>
    /// <param name="stepLimit">Максимальное число шагов; по достижении генерируется <see cref="BiosStatus.InfinityLoop"/>.</param>
    /// <param name="delay">Задержка между шагами в миллисекундах; <c>0</c> — без задержки.</param>
    /// <param name="onLaunch">Необязательный колбэк, вызываемый перед запуском.</param>
    /// <param name="onStep">Необязательный колбэк, получающий снимок состояния процессора после каждого шага.</param>
    /// <param name="onEnd">Необязательный колбэк, вызываемый после остановки.</param>
    /// <returns>Диагностические данные о последнем сбое/остановке.</returns>
    public ProcessorFault DebugRun(ulong start,
                        long stepLimit,
                        int delay,
                        Action? onLaunch,
                        Action<ProcessorSnapshot>? onStep,
                        Action? onEnd)
    {
        GlobalInit(onLaunch, start);
        bool useSleepMode = delay != 0;
        RunSimulationLoopDebug(delay, useSleepMode, stepLimit, onStep);

        onEnd?.Invoke();
        return _processor.GetDiagnosticData();
    }


    /// <summary>
    /// Инициализирует процессор на указанный адрес без запуска цикла исполнения —
    /// удобно для ручного пошагового выполнения.
    /// </summary>
    /// <param name="start">Адрес первой инструкции; <see langword="null"/> — старт с конца ОЗУ.</param>
    /// <param name="onLaunch">Необязательный колбэк, вызываемый перед инициализацией.</param>
    public void BreakPointerLaunchDevice(ulong? start, Action? onLaunch)
    {
        GlobalInit(onLaunch, start ?? 0);
    }


    /// <summary>
    /// Останавливает исполнение: ставит в очередь процессора статус завершения и команду пробуждения.
    /// </summary>
    /// <remarks>Безопасно вызывать из другого потока.</remarks>
    public void Stop()
    {
        _processor.EnqueueBiosStatus(BiosStatus.HostStopped);
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
            Step();
    }

    public void Step()
    {
        _processor.Step();

        if (_processor.IsSleeping)
        {
            ProcessorSleepWait();
        }
        else if (_processor.IsRunning)
        {
            _stepCounter++;
        }
    }

    public void BatchStep(int count)
    {
        for (int i = 0; _processor.IsRunning && i < count; i++)
            Step();
        
    }
    private void RunSimulationLoopWithLimit(long limit)
    {
        bool isEndLimit = false;

        while (_processor.IsRunning)
        {
            if (isEndLimit) break;
            if (_stepCounter >= limit)
            {
                _processor.EnqueueBiosStatus(BiosStatus.InfinityLoop);
                isEndLimit = true;
            }

            Step();
        }
    }

    private void RunSimulationLoopDebug(int delay, bool useSleepMode, long stepLimit, Action<ProcessorSnapshot>? onStep)
    {
        bool isEndLimit = false;
        while (_processor.IsRunning)
        {
            onStep?.Invoke(_processor.CurrentProcessorSnapshot);
            if (isEndLimit) break;
            if (_stepCounter >= stepLimit)
            {
                _processor.EnqueueBiosStatus(BiosStatus.InfinityLoop);
                isEndLimit = true;
            }

            Step();

            if (useSleepMode) Thread.Sleep(delay);
        }
    }

    private void GlobalInit(Action? onLaunch, ulong startIndex)
    {
        ThrowIfDisposed();
        ThrowIfRun();
        onLaunch?.Invoke();
        _stepCounter = 0;
        _wakeSignal.Reset();
        _processor.Reset();
        _processor.LaunchProgramm(startIndex);
    }

    private void ThrowIfRun()
    {
        if (_processor.IsRunning)
            throw new InvalidOperationException(
                "A second Run operation was started on this Device " +
                "instance before the previous operation completed.");
    }

    private void ProcessorSleepWait()
    {
        _wakeSignal.Wait(_processorPolicy.SleepWaitTimeout);
        _wakeSignal.Reset();

    }

    /// <summary>
    /// Читает байт из блока портов устройства.
    /// </summary>
    /// <param name="offset">Смещение внутри блока портов устройства.</param>
    /// <returns>Значение порта или <c>0</c>, если <paramref name="offset"/> вне диапазона.</returns>
    public byte ReadPort(ulong offset)
    {
        if (offset >= (ulong)_ioMemory.Length)
            return 0; // или ошибка
        return _ioMemory[offset];
    }

    /// <summary>
    /// Записывает байт в блок портов устройства.
    /// </summary>
    /// <param name="offset">Смещение внутри блока портов устройства.</param>
    /// <param name="value">Записываемое значение.</param>
    /// <remarks>Если <paramref name="offset"/> вне диапазона, запись игнорируется.</remarks>
    public void WritePort(ulong offset, byte value)
    {
        if (offset < (ulong)_ioMemory.Length)
            _ioMemory[offset] = value;
    }

    /// <summary>
    /// Будит процессор устройства: ставит в очередь команду <see cref="OpCode.WAKE"/> и сигналит ожидание.
    /// </summary>
    /// <remarks>Безопасно вызывать из другого потока.</remarks>
    public void WakeProcessor()
    {
        _processor.EnqueueExternalCommand((uint)OpCode.WAKE);
        _wakeSignal.Set();
    }

    /// <summary>
    /// Ставит в очередь процессора внешнюю команду и будит его.
    /// </summary>
    /// <param name="instruction">Сырое 32-битное слово инструкции.</param>
    /// <remarks>Безопасно вызывать из другого потока.</remarks>
    public void EnqueueExternalCommand(uint instruction)
    {
        _processor.EnqueueExternalCommand(instruction);
        _wakeSignal.Set();
    }

    /// <summary>
    /// Выполняет один шаг процессора и вызывает колбэк со снимком его состояния.
    /// </summary>
    /// <param name="OnStep">Необязательный колбэк, получающий снимок после шага.</param>
    /// <remarks>Если процессор не запущен, метод ничего не делает.</remarks>
    public void StepDebug(Action<ProcessorSnapshot>? OnStep)
    {
        if (!_processor.IsRunning) return;

        OnStep?.Invoke(_processor.CurrentProcessorSnapshot);
        Step();
    }

    /// <summary>
    /// Выполняет заданное количество шагов процессора, опционально вызывая колбэк после каждого.
    /// </summary>
    /// <param name="count">Количество шагов.</param>
    /// <param name="OnStep">Необязательный колбэк, получающий снимок после каждого шага.</param>
    /// <remarks>Если процессор не запущен, метод ничего не делает.</remarks>
    public void BatchStepDebug(int count, Action<ProcessorSnapshot>? OnStep)
    {
        if (!_processor.IsRunning) return;
        bool needLog = OnStep != null;
        for (int i = 0; _processor.IsRunning && i < count; i++)
        {
            if (needLog)
            {
                OnStep!.Invoke(_processor.CurrentProcessorSnapshot);
            }
            Step();
        }
    }


    /// <summary>
    /// Очищает ОЗУ устройства.
    /// </summary>
    public void ResetMemoryRam() => _ram.ClearMemory();

    /// <summary>
    /// Останавливает устройство и освобождает ОЗУ.
    /// </summary>
    /// <remarks>Повторный вызов безопасен; потокобезопасен.</remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        Stop();
        _ram?.Dispose();
        _wakeSignal.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Сбрасывает процессор устройства: останавливает исполнение, очищает регистры и очереди команд.
    /// </summary>
    public void ClearRegisters()
    {
        _processor.Reset();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    }
    /// <summary>
    /// Возвращает диагностические данные о последнем сбое/остановке процессора.
    /// </summary>
    /// <returns>Снимок <see cref="ProcessorFault"/>.</returns>
    public ProcessorFault GetLastFault() => _processor.GetDiagnosticData();
}