using Nantuc.Compiler.ASM.Utils;
using Nantuc.Kernel.BiosSystem;
using Nantuc.Kernel.Common;
using Nantuc.Kernel.Contracts;
using Nantuc.VmApplication.Project;
using System.Diagnostics.CodeAnalysis;

namespace Nantuc.VmApplication.Emulator;

/// <summary>
/// Безопасный контекст устройства, предоставляющий управляемый доступ к устройству эмулятора.
/// </summary>
/// <remarks>
/// Контекст гарантирует, что над устройством не выполняются одновременно несовместимые операции,
/// и предоставляет методы для загрузки программ, запуска, пошагового выполнения и чтения состояния.
/// </remarks>
public class DeviceContext
{
    /// <summary>
    /// Режим работы контекста устройства.
    /// </summary>
    private enum Mode
    {
        /// <summary>
        /// Пошаговый режим выполнения.
        /// </summary>
        Step,

        /// <summary>
        /// Непрерывный режим выполнения.
        /// </summary>
        Continuous,

        /// <summary>
        /// Режим не установлен (устройство свободно).
        /// </summary>
        None
    }

    /// <summary>
    /// Ссылка на устройство эмулятора.
    /// </summary>
    private readonly Device _device;

    /// <summary>
    /// Текущий режим работы контекста.
    /// </summary>
    private volatile Mode _mode = Mode.None;

    /// <summary>
    /// Небезопасный доступ к внутреннему устройству.
    /// </summary>
    /// <remarks>Использовать только внутри сборки и только при полной уверенности в отсутствии гонок.</remarks>
    internal Device UnsafeGetDevice => _device;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="DeviceContext"/> для указанного устройства.
    /// </summary>
    /// <param name="device">Устройство эмулятора.</param>
    internal DeviceContext(Device device) => _device = device;

    /// <summary>
    /// Запущено ли устройство.
    /// </summary>
    public bool IsRunning => _device.IsRunning;

    /// <summary>
    /// Количество выполненных шагов устройства.
    /// </summary>
    public long StepCount => _device.StepCount;

    /// <summary>
    /// Размер ОЗУ устройства.
    /// </summary>
    public ulong RamSize => _device.MaxRamSize;

    /// <summary>
    /// Безопасная обёртка над нативным буфером, позволяет читать ОЗУ устройства.
    /// </summary>
    /// <returns>Объект <see cref="INativeReadOnlyBuffer"/>, предоставляющий доступ только для чтения.</returns>
    public INativeReadOnlyBuffer NativeReadOnlyBuffer => _device.NativeReadOnlyBuffer;

    /// <summary>
    /// Останавливает устройство и сбрасывает режим контекста.
    /// </summary>
    public void Stop()
    {
        _device.Stop();
        SetNoneMode();
    }

    /// <summary>
    /// Загружает программу в ОЗУ устройства.
    /// </summary>
    /// <param name="program">Программа для загрузки.</param>
    /// <param name="loadAddress">Адрес, начиная с которого загружается программа.</param>
    /// <exception cref="InvalidOperationException">Устройство запущено; перед загрузкой необходимо вызвать <see cref="Stop"/>.</exception>
    public void LoadProgram(ReadOnlySpan<byte> program, ulong loadAddress = ProjectBuilder.BaseAddressProgram)
    {
        ThrowIfRunning("Cannot load program while device is running. Call Stop() first.");
        _device.LoadProgram(program, loadAddress);
    }

    /// <summary>
    /// Пытается загрузить программу в ОЗУ устройства (безопасная форма загрузки).
    /// </summary>
    /// <param name="program">Программа для загрузки.</param>
    /// <param name="loadAddress">Адрес, начиная с которого загружается программа.</param>
    /// <param name="error">Строка с описанием ошибки, если загрузка не удалась; иначе <see langword="null"/>.</param>
    /// <returns><see langword="true"/>, если программа успешно загружена; иначе <see langword="false"/>.</returns>
    public bool TryLoadProgram(ReadOnlySpan<byte> program, ulong loadAddress, [NotNullWhen(false)] out string? error)
    {
        if (IsRunning)
        {
            error = "Cannot overwrite the program while the device is running.";
            return false;
        }

        if (_device.TryLoadProgram(program, loadAddress))
        {
            error = null;
            return true;
        }
        error = $"Failed to load the program: memory out of bounds (loadAddress + {program.Length} > RAM) or other unexpected error.";
        return false;
    }

    /// <summary>
    /// Запускает цикл устройства в непрерывном режиме; метод оптимизирован для скорости выполнения (Release).
    /// </summary>
    /// <param name="opt">Настройки запуска; при <see langword="null"/> используются <see cref="LaunchOptions.Default"/>.</param>
    /// <returns>Результат работы устройства — причина остановки <see cref="ProcessorFault"/>.</returns>
    /// <exception cref="InvalidOperationException">Устройство уже запущено или установлен другой режим работы.</exception>
    public ProcessorFault Run(LaunchOptions? opt = null)
    {
        ThrowIfRunning();
        ThrowByMode(Mode.Continuous);

        opt ??= LaunchOptions.Default;
        var fault = _device.Run(opt.StartAddress,
            opt.StepLimit,
            opt.ShowTimer,
            opt.OnLaunch,
            opt.OnEnd);

        SetNoneMode();
        return fault;
    }

    /// <summary>
    /// Запускает цикл устройства в отладочном режиме; метод рассчитан на отладку (Debug).
    /// </summary>
    /// <param name="opt">Настройки отладочного запуска; при <see langword="null"/> используются <see cref="LaunchOptionsDebug.Default"/>.</param>
    /// <returns>Результат работы устройства — причина остановки <see cref="ProcessorFault"/>.</returns>
    /// <exception cref="InvalidOperationException">Устройство уже запущено или установлен другой режим работы.</exception>
    public ProcessorFault DebugRun(LaunchOptionsDebug? opt = null)
    {
        ThrowIfRunning();
        ThrowByMode(Mode.Continuous);
        opt ??= LaunchOptionsDebug.Default;
        var fault = _device.DebugRun(opt.StartAddress,
                         opt.StepLimit,
                         opt.DelayMs,
                         opt.OnLaunch,
                         opt.OnStep,
                         opt.OnEnd);
        SetNoneMode();
        return fault;
    }

    /// <summary>
    /// Запускает устройство в пошаговом режиме.
    /// </summary>
    /// <param name="startAddress">Адрес, с которого запускается устройство.</param>
    /// <param name="onLaunch">Колбэк-функция, вызываемая при запуске.</param>
    /// <exception cref="InvalidOperationException">Устройство уже запущено или установлен другой режим работы.</exception>
    public void RunStepMode(ulong startAddress = ProjectBuilder.BaseAddressProgram,
        Action? onLaunch = null)
    {
        ThrowIfRunning();
        ThrowByMode(Mode.Step);
        _device.BreakPointerLaunchDevice(startAddress, onLaunch);
    }

    /// <summary>
    /// Выполняет один шаг устройства.
    /// </summary>
    /// <param name="onStep">Колбэк для анализа состояния процесса.</param>
    /// <exception cref="InvalidOperationException">Контекст не находится в пошаговом режиме.</exception>
    public void SingleStep(Action<ProcessorReader>? onStep = null)
    {
        ThrowIfNotStepMode();
        _device.NextStepProcessor(onStep);
    }

    /// <summary>
    /// Выполняет несколько шагов устройства подряд.
    /// </summary>
    /// <param name="count">Количество шагов; по умолчанию 5.</param>
    /// <param name="onStep">Колбэк для анализа состояния процесса после каждого шага.</param>
    /// <exception cref="InvalidOperationException">Контекст не находится в пошаговом режиме.</exception>
    public void MultiStep(int count = 5, Action<ProcessorReader>? onStep = null)
    {
        ThrowIfNotStepMode();
        _device.NextStepProcessorCount(count, onStep);
    }

    /// <summary>
    /// Завершает работу устройства из пошагового режима и сбрасывает режим контекста.
    /// </summary>
    /// <exception cref="InvalidOperationException">Контекст не находится в пошаговом режиме.</exception>
    public void EndStepMode()
    {
        ThrowIfNotStepMode();
        _device.Stop();
        _device.NextStepProcessor(null);
        SetNoneMode();
    }

    /// <summary>
    /// Полностью очищает ОЗУ устройства.
    /// </summary>
    /// <exception cref="InvalidOperationException">Устройство запущено; перед очисткой необходимо вызвать <see cref="Stop"/>.</exception>
    public void ResetMemoryRam()
    {
        ThrowIfRunning("Cannot clear RAM while device is running. Call Stop() first.");
        _device.ResetMemoryRam();
    }

    /// <summary>
    /// Читает значение из порта ввода-вывода этого устройства (читается только его IO-буфер).
    /// </summary>
    /// <param name="offset">Смещение порта.</param>
    /// <returns>Байт данных, прочитанный из порта.</returns>
    public byte ReadPort(ulong offset) => _device.ReadPort(offset);

    /// <summary>
    /// Копирует значения регистров в предоставленный буфер.
    /// </summary>
    /// <param name="destination">Буфер <see cref="Span{T}"/>, в который будут скопированы регистры.</param>
    public void CopyRegisters(Span<ulong> destination) => _device.CopyRegisters(destination);

    /// <summary>
    /// Возвращает снимок значений регистров устройства.
    /// </summary>
    /// <returns>Структура <see cref="SnapshotRegisters"/> для удобного получения регистров.</returns>
    public SnapshotRegisters GetRegistersSnapshot()
    {
        Span<ulong> destination = stackalloc ulong[32];
        _device.CopyRegisters(destination);
        return new(destination);
    }

    /// <summary>
    /// Возвращает информацию о последней причине падения устройства.
    /// </summary>
    /// <remarks>
    /// Если устройство работает и не останавливалось, возвращаются значения:
    /// <c>BiosStatus.Success</c>, 0, 0, <c>OpCode.NOP</c>.
    /// Значение не обновляется на каждой итерации ради оптимизации.
    /// </remarks>
    /// <returns>Структура <see cref="ProcessorFault"/> с информацией о последней ошибке.</returns>
    public ProcessorFault GetLastFault() => _device.GetLastFault();

    /// <summary>
    /// Приостанавливает выполнение устройства (ставит в очередь команду <c>HALT</c>).
    /// </summary>
    public void Pause() => _device.EnqueueExternalCommand(OpCode.HALT.ToUint());

    /// <summary>
    /// Возобновляет выполнение устройства после паузы (ставит в очередь команду <c>WAKE</c>).
    /// </summary>
    public void Resume() => _device.EnqueueExternalCommand(OpCode.WAKE.ToUint());

    /// <summary>
    /// Выбрасывает исключение, если устройство в данный момент запущено.
    /// </summary>
    /// <param name="msg">Сообщение исключения.</param>
    /// <exception cref="InvalidOperationException">Устройство запущено.</exception>
    private void ThrowIfRunning(
        string msg = "Cannot launch program while device is running. Call Stop() first.")
    {
        if (IsRunning)
            throw new InvalidOperationException(msg);
    }

    /// <summary>
    /// Устанавливает указанный режим работы, если контекст свободен.
    /// </summary>
    /// <param name="mode">Устанавливаемый режим.</param>
    /// <param name="msg">Сообщение исключения.</param>
    /// <exception cref="InvalidOperationException">Контекст уже находится в каком-либо режиме.</exception>
    private void ThrowByMode(Mode mode,
        string msg = "The device operating mode is already set. Stop the device or wait for the current operation to complete.")
    {
        if (_mode != Mode.None)
            throw new InvalidOperationException(msg);
        _mode = mode;
    }

    /// <summary>
    /// Выбрасывает исключение, если контекст не находится в пошаговом режиме.
    /// </summary>
    /// <param name="msg">Сообщение исключения.</param>
    /// <exception cref="InvalidOperationException">Контекст не в пошаговом режиме.</exception>
    private void ThrowIfNotStepMode(
        string msg = "The device operating mode is not set to 'Step'.")
    {
        if (_mode != Mode.Step)
            throw new InvalidOperationException(msg);
    }

    /// <summary>
    /// Сбрасывает режим контекста в <see cref="Mode.None"/>.
    /// </summary>
    private void SetNoneMode() => _mode = Mode.None;
}