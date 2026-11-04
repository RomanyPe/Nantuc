using Nantuc.Common;
using Nantuc.Contracts;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Extensions.Disassembler;
using System.Buffers.Binary;
using System.Text;

namespace Nantuc.VmApplication.Extensions.Debugging;

/// <summary>
/// Причина, по которой отладчик вернул управление вызывающему.
/// </summary>
public enum DebugStopReason
{
    /// <summary>Отладчик ещё не останавливался.</summary>
    None = 0,

    /// <summary>Выполнена одна инструкция (<c>Step</c>/<c>StepOver</c>/<c>StepOut</c>).</summary>
    StepComplete,

    /// <summary>Сработала точка останова на текущем <c>rIP</c>.</summary>
    Breakpoint,

    /// <summary>Программа выполнила <c>END</c> и завершилась штатно.</summary>
    Completed,

    /// <summary>Программа упала (<c>BiosStatus</c> — не <c>EndProgramm</c> и не <c>Success</c>).</summary>
    Faulted,

    /// <summary>Программа выполнила <c>HALT</c> и ждёт пробуждения.</summary>
    Sleeping,

    /// <summary>Достигнут лимит шагов, переданный в <c>Continue</c>.</summary>
    StepLimit,

    /// <summary>Пользователь вызвал <c>RequestStop</c>.</summary>
    UserStop,

    /// <summary>Внутренняя ошибка отладчика.</summary>
    Error,
}

/// <summary>
/// Пошаговый отладчик поверх <see cref="DeviceContext"/>:
/// точки останова, шаг, шаг с обходом <c>CALL</c>, шаг из функции,
/// продолжение до точки останова/завершения/фолта, чтение RAM и дизассемблирование.
/// </summary>
/// <remarks>
/// Отладчик не потокобезопасен (кроме <see cref="RequestStop"/>), что совпадает
/// с моделью <see cref="DeviceContext"/>. Все методы, кроме <see cref="RequestStop"/>,
/// должны вызываться из одного потока-координатора.
/// </remarks>
public sealed class Debugger : IDisposable
{
    private const int DefaultStepLimit = int.MaxValue;
    private const int MaxDisassembleBytes = 4096;

    private readonly DeviceContext _context;
    private readonly HashSet<ulong> _breakpoints = [];
    private readonly Action<ProcessorSnapshot> _onStepCallback;

    private volatile bool _stopRequested;
    private ProcessorSnapshot? _stepSnapshot;
    private DebugStop? _lastStop;
    private ulong? _skipBreakpointOnce;
    private bool _isActive;
    private bool _disposed;
    private ulong _programStart;
    private ulong _programEnd;

    /// <summary>Создаёт отладчик над уже существующим <see cref="DeviceContext"/>.</summary>
    public Debugger(DeviceContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _onStepCallback = OnStep;
    }

    // ─── Состояние ────────────────────────────────────────────────────────────

    /// <summary>Обёрнутый <see cref="DeviceContext"/> (для <c>Resume</c>/<c>Pause</c> и пр.).</summary>
    public DeviceContext Context => _context;

    /// <summary>Отладчик загрузил программу и находится в step-режиме.</summary>
    public bool IsActive => _isActive;

    /// <summary>Последний останов (или <c>null</c>, если шагов ещё не было).</summary>
    public DebugStop? LastStop => _lastStop;

    /// <summary>Множество адресов, на которых сработает breakpoint.</summary>
    public IReadOnlyCollection<ulong> Breakpoints => _breakpoints;

    /// <summary>Срабатывает при каждом <c>Step</c>/<c>Continue</c>/... сразу после останова.</summary>
    public event Action<DebugStop>? Stopped;

    // ─── Загрузка ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Загружает программу и переводит отладчик в step-режим.
    /// </summary>
    /// <param name="program">Байты программы.</param>
    /// <param name="loadAddress">Куда копировать байты (по умолчанию 0).</param>
    /// <param name="startAddress">С какого адреса начать исполнение (по умолчанию 0).</param>
    /// <remarks>
    /// После вызова <see cref="IsActive"/> становится <c>true</c>, но ни одна инструкция
    /// ещё не выполнена. Точки останова можно добавлять как до, так и после <see cref="Load"/>.
    /// </remarks>
    public void Load(ReadOnlySpan<byte> program, ulong loadAddress = 0, ulong startAddress = 0)
    {
        ThrowIfDisposed();
        if (_isActive)
            throw new InvalidOperationException("Debugger is already active. Dispose or create a new instance.");

        _context.LoadProgram(program, loadAddress);
        _context.RunStepMode(startAddress);

        _programStart = startAddress;
        _programEnd = loadAddress + (ulong)program.Length;
        _isActive = true;
        _lastStop = null;
        _skipBreakpointOnce = null;
    }

    // ─── Управление исполнением ───────────────────────────────────────────────

    /// <summary>Выполняет одну инструкцию.</summary>
    public DebugStop Step()
    {
        EnsureActive();
        if (TryReturnTerminalStop(out var terminal)) return terminal;

        return StepOneOrFinish();
    }

    /// <summary>
    /// Выполняет одну инструкцию. Если текущая — <c>CALL</c>/<c>CALL_IND</c>,
    /// исполняет её целиком, до возврата в вызывающую функцию.
    /// </summary>
    public DebugStop StepOver()
    {
        EnsureActive();
        if (TryReturnTerminalStop(out var terminal)) return terminal;

        ulong ip = _context.GetRegistersSnapshot()[RegType.rIP];
        if (!TryPeekInstruction(ip, out var op))
            return StepOneOrFinish();

        if (op is not (OpCode.CALL or OpCode.CALL_IND))
            return StepOneOrFinish();

        ulong returnAddress = op == OpCode.CALL_IND
            ? ip + 4UL
            : Align8(ip + 4UL) + 8UL;

        _stopRequested = false;
        while (true)
        {
            if (_stopRequested)
                return MakeStop(DebugStopReason.UserStop, BuildCurrentSnapshot());

            var stop = StepOneOrFinish();
            if (stop.IsTerminal || stop.Reason is DebugStopReason.Breakpoint or DebugStopReason.Sleeping)
                return stop;

            ulong newIp = stop.InstructionPointer;
            if (newIp == returnAddress || newIp == ip)
                return stop;
        }
    }

    /// <summary>
    /// Выполняет инструкции, пока не вернётся из текущей функции
    /// (<c>rSP</c> станет больше значения на момент вызова).
    /// </summary>
    public DebugStop StepOut()
    {
        EnsureActive();
        if (TryReturnTerminalStop(out var terminal)) return terminal;

        ulong initialSp = _context.GetRegistersSnapshot()[RegType.rSP];
        _stopRequested = false;

        while (true)
        {
            if (_stopRequested)
                return MakeStop(DebugStopReason.UserStop, BuildCurrentSnapshot());

            var stop = StepOneOrFinish();
            if (stop.IsTerminal || stop.Reason is DebugStopReason.Breakpoint or DebugStopReason.Sleeping)
                return stop;

            ulong newSp = stop.Snapshot.GetRegister(RegType.rSP);
            if (newSp > initialSp)
                return stop;
        }
    }

    /// <summary>
    /// Запускает программу, пока не сработает breakpoint, пока не завершится/упадёт
    /// или пока не будет достигнут <paramref name="stepLimit"/>.
    /// </summary>
    /// <param name="stepLimit">Максимум шагов; <c>null</c> — без ограничения.</param>
    public DebugStop Continue(long? stepLimit = null)
    {
        EnsureActive();
        if (TryReturnTerminalStop(out var terminal)) return terminal;

        long limit = stepLimit ?? DefaultStepLimit;
        long startCount = _context.StepCount;
        _stopRequested = false;

        // Breakpoint на текущем IP (актуально сразу после Load и между Continue).
        ulong initialIp = _context.GetRegistersSnapshot()[RegType.rIP];
        if (_skipBreakpointOnce == initialIp)
        {
            _skipBreakpointOnce = null;
        }
        else if (HasBreakpoint(initialIp))
        {
            return MakeBreakpointStop(BuildCurrentSnapshot());
        }

        while (true)
        {
            if (_stopRequested)
                return MakeStop(DebugStopReason.UserStop, BuildCurrentSnapshot());

            if (_context.StepCount - startCount >= limit)
                return MakeStop(DebugStopReason.StepLimit, BuildCurrentSnapshot());

            var stop = StepOneOrFinish();
            if (stop.IsTerminal
                || stop.Reason is DebugStopReason.Sleeping
                || stop.Reason is DebugStopReason.StepComplete && HasBreakpoint(stop.InstructionPointer))
            {
                if (stop.Reason == DebugStopReason.StepComplete)
                    return MakeBreakpointStop(stop.Snapshot);
                return stop;
            }
        }
    }

    /// <summary>Продолжает исполнение до конкретного адреса (или до завершения/фолта/лимита).</summary>
    public DebugStop ContinueTo(ulong address, long? stepLimit = null)
    {
        EnsureActive();
        if (TryReturnTerminalStop(out var terminal)) return terminal;

        _breakpoints.Add(address);
        try
        {
            return Continue(stepLimit);
        }
        finally
        {
            _breakpoints.Remove(address);
        }
    }

    /// <summary>Продолжает исполнение до завершения, игнорируя точки останова.</summary>
    public DebugStop RunToEnd(long? stepLimit = null)
    {
        var saved = _breakpoints.ToArray();
        _breakpoints.Clear();
        try
        {
            return Continue(stepLimit);
        }
        finally
        {
            foreach (var bp in saved) _breakpoints.Add(bp);
        }
    }

    /// <summary>
    /// Асинхронно просит отладчик остановиться на следующей итерации цикла
    /// (безопасно из другого потока — например, по кнопке «Stop»).
    /// </summary>
    public void RequestStop() => _stopRequested = true;

    /// <summary>Снимает процессор со сна <c>HALT</c> (кладёт <c>WAKE</c> в очередь).</summary>
    public void Wake()
    {
        ThrowIfDisposed();
        _context.Resume();
    }

    // ─── Точки останова ───────────────────────────────────────────────────────

    /// <summary>Добавляет точку останова; повторное добавление идемпотентно.</summary>
    public void AddBreakpoint(ulong address) => _breakpoints.Add(address);

    /// <summary>Удаляет точку останова; возвращает <c>true</c>, если она была.</summary>
    public bool RemoveBreakpoint(ulong address) => _breakpoints.Remove(address);

    /// <summary>Убирает все точки останова.</summary>
    public void ClearBreakpoints() => _breakpoints.Clear();

    /// <summary>Проверяет наличие точки останова.</summary>
    public bool HasBreakpoint(ulong address) => _breakpoints.Contains(address);

    // ─── Инспекция ────────────────────────────────────────────────────────────

    /// <summary>Снимок всех 32 регистров.</summary>
    public SnapshotRegisters Registers
    {
        get
        {
            ThrowIfDisposed();
            return _context.GetRegistersSnapshot();
        }
    }

    /// <summary>Текущий <c>rIP</c>.</summary>
    public ulong InstructionPointer
    {
        get
        {
            ThrowIfDisposed();
            return _context.GetRegistersSnapshot()[RegType.rIP];
        }
    }

    /// <summary>Читает <paramref name="length"/> байт RAM, начиная с <paramref name="start"/>.</summary>
    public byte[] ReadRam(ulong start, int length)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        var buffer = _context.NativeReadOnlyBuffer;
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start, buffer.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((ulong)length, buffer.Length - start);
        var result = new byte[length];
        for (int i = 0; i < length; i++)
            result[i] = buffer[(nuint)(start + (ulong)i)];
        return result;
    }

    /// <summary>Копирует байты RAM в готовый <paramref name="destination"/>.</summary>
    public void CopyRamTo(Span<byte> destination, ulong start)
    {
        ThrowIfDisposed();
        var buffer = _context.NativeReadOnlyBuffer;
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start, buffer.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((ulong)destination.Length, buffer.Length - start);

        for (int i = 0; i < destination.Length; i++)
            destination[i] = buffer[(nuint)(start + (ulong)i)];
    }

    /// <summary>Читает <see cref="byte"/>.</summary>
    public byte ReadByte(ulong address) => ReadRam(address, 1)[0];

    /// <summary>Читает little-endian <see cref="ushort"/>.</summary>
    public ushort ReadUInt16(ulong address) => BinaryPrimitives.ReadUInt16LittleEndian(ReadRam(address, 2));

    /// <summary>Читает little-endian <see cref="uint"/>.</summary>
    public uint ReadUInt32(ulong address) => BinaryPrimitives.ReadUInt32LittleEndian(ReadRam(address, 4));

    /// <summary>Читает little-endian <see cref="ulong"/>.</summary>
    /// <summary>Читает little-endian <see cref="ulong"/>.</summary>
    public ulong ReadUInt64(ulong address) => BinaryPrimitives.ReadUInt64LittleEndian(ReadRam(address, 8));
    /// <summary>Читает <c>char</c> (ASCII / single-byte UTF-8).</summary>
    public char ReadChar(ulong address) => (char)ReadByte(address);

    /// <summary>Читает NUL-terminated строку ASCII, начиная с <paramref name="address"/>.</summary>
    public string ReadCString(ulong address, int maxLength = 1024)
    {
        ThrowIfDisposed();
        var sb = new StringBuilder();
        for (int i = 0; i < maxLength; i++)
        {
            byte b = ReadByte(address + (ulong)i);
            if (b == 0) break;
            sb.Append((char)b);
        }
        return sb.ToString();
    }

    // ─── Дизассемблирование ───────────────────────────────────────────────────

    /// <summary>Строковое представление инструкции по текущему <c>rIP</c>.</summary>
    public string CurrentInstruction => DisassembleAt(_context.GetRegistersSnapshot()[RegType.rIP]);

    /// <summary>Дизассемблирует одну инструкцию по адресу <paramref name="address"/>.</summary>
    public string DisassembleAt(ulong address)
    {
        ThrowIfDisposed();
        if (address >= _context.RamSize) return "(out of range)";

        int length = (int)Math.Min(16, _context.RamSize - address);
        byte[] bytes = ReadRam(address, length);
        string text = StringDisassembler.Disassemble(bytes, length, address);

        int nl = text.IndexOf('\n');
        return (nl < 0 ? text : text[..nl]).Trim();
    }

    /// <summary>Дизассемблирует весь загруженный <see cref="Load"/>-ом диапазон.</summary>
    public string DisassembleProgram()
    {
        ThrowIfDisposed();
        if (_programEnd <= _programStart) return "(no program loaded)";

        ulong size = _programEnd - _programStart;
        if (size > int.MaxValue) size = int.MaxValue;
        byte[] bytes = ReadRam(_programStart, (int)size);
        return StringDisassembler.Disassemble(bytes, bytes.Length, _programStart);
    }

    // ─── Стек вызовов (best-effort) ───────────────────────────────────────────

    /// <summary>
    /// Best-effort восстановление стека вызовов: сканирует стек VM на предмет
    /// 8-байтных значений, похожих на адреса возврата.
    /// </summary>
    /// <remarks>
    /// VM не хранит frame pointer, поэтому возможны ложные срабатывания
    /// (значения локальных переменных, попавшие в диапазон кода).
    /// </remarks>
    public IReadOnlyList<CallStackFrame> GetCallStack(int maxDepth = 16)
    {
        ThrowIfDisposed();

        var snapshot = _context.GetRegistersSnapshot();
        ulong sp = snapshot[RegType.rSP];
        ulong stackBase = _context.RamSize & ~7UL;

        if (_programEnd <= _programStart)
            return [];

        var frames = new List<CallStackFrame>();
        ulong frameAddr = sp;

        while (frameAddr < stackBase && frames.Count < maxDepth)
        {
            if (frameAddr + 8 > _context.RamSize) break;

            ulong value;
            try { value = ReadUInt64(frameAddr); }
            catch { break; }

            if (value >= _programStart && value < _programEnd && (value & 3) == 0)
                frames.Add(new CallStackFrame(frames.Count, value, frameAddr));

            frameAddr += 8;
        }

        return frames;
    }

    // ─── Внутреннее ───────────────────────────────────────────────────────────

    private DebugStop StepOneOrFinish()
    {
        _stepSnapshot = null;
        _context.SingleStep(_onStepCallback);

        ProcessorSnapshot snapshot = _stepSnapshot
            ?? BuildSnapshotFromRegisters(_context.GetRegistersSnapshot());

        // Защита от повторного срабатывания того же breakpoint'а на следующем Continue.
        ulong newIp = snapshot.GetRegister(RegType.rIP);
        _skipBreakpointOnce = HasBreakpoint(newIp) ? newIp : null;

        if (snapshot.IsSleeping)
            return MakeStop(DebugStopReason.Sleeping, snapshot);

        if (!snapshot.IsRunning)
        {
            var fault = _context.GetLastFault();
            return fault.Status == BiosStatus.EndProgramm || fault.Status == BiosStatus.Success
                ? MakeStop(DebugStopReason.Completed, snapshot)
                : MakeStop(DebugStopReason.Faulted, snapshot);
        }

        return MakeStop(DebugStopReason.StepComplete, snapshot);
    }


    private void OnStep(ProcessorSnapshot snapshot) => _stepSnapshot = snapshot;

    private bool TryPeekInstruction(ulong ip, out OpCode op)
    {
        try
        {
            uint raw = ReadUInt32(ip);
            op = InstructionDecoder.GetOpCode(raw);
            return true;
        }
        catch
        {
            op = OpCode.NOP;
            return false;
        }
    }

    private ProcessorSnapshot BuildCurrentSnapshot()
    {
        if (_stepSnapshot is { } cached) return cached;
        return BuildSnapshotFromRegisters(_context.GetRegistersSnapshot());
    }

    private ProcessorSnapshot BuildSnapshotFromRegisters(SnapshotRegisters regs)
    {
        Span<ulong> arr = stackalloc ulong[32];
        for (int i = 0; i < 32; i++) arr[i] = regs[(RegType)i];

        uint raw = 0;
        try { raw = ReadUInt32(regs[RegType.rIP]); } catch { }

        return new ProcessorSnapshot(arr, _context.IsRunning, false, raw, 0);
    }

    private DebugStop MakeStop(DebugStopReason reason, ProcessorSnapshot snapshot)
    {
        var fault = _context.GetLastFault();
        var stop = new DebugStop(reason, snapshot.GetRegister(RegType.rIP),
                                 _context.StepCount, snapshot, fault);
        _lastStop = stop;
        Stopped?.Invoke(stop);
        return stop;
    }

    private DebugStop MakeBreakpointStop(ProcessorSnapshot snapshot)
        => MakeStop(DebugStopReason.Breakpoint, snapshot);

    private bool TryReturnTerminalStop(out DebugStop stop)
    {
        if (_lastStop is { IsTerminal: true } last)
        {
            stop = last;
            return true;
        }
        stop = default;
        return false;
    }

    private void EnsureActive()
    {
        ThrowIfDisposed();
        if (!_isActive)
            throw new InvalidOperationException("Call Load() before stepping.");
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private static ulong Align8(ulong v) => (v + 7UL) & ~7UL;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_isActive)
        {
            try { _context.EndStepMode(); }
            catch { /* already stopped or context disposed */ }
            _isActive = false;
        }
    }
}