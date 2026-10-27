using Nantuc.Kernel.BiosSystem;
using Nantuc.Kernel.Common;
using Nantuc.Kernel.Contracts;
using Nantuc.VmApplication.Project;
using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Nantuc.VmApplication.Emulator;

/// <summary>
/// Безопасный контекст устройства
/// </summary>
public class DeviceContext
{
    private enum Mode
    {
        Step,
        Continuous, 
        None
    }
    private readonly Device _device;
    private volatile Mode _mode = Mode.None;

    internal Device UnsafeGetDevice => _device;
    internal DeviceContext(Device device) => _device = device;

    /// <summary>
    /// Запущено ли устройство
    /// </summary>
    public bool IsRunning => _device.IsRunning;

    /// <summary>
    /// Количество шагов устройство
    /// </summary>
    public long StepCount => _device.StepCount;
    /// <summary>
    /// Размер озу
    /// </summary>
    public ulong RamSize => _device.MaxRamSize;
    /// <summary>
    /// Безопасная обертка над нативным буфером, позволяет читать озу устройства, возвращает объект <see cref="INativeReadOnlyBuffer"/>
    /// </summary>
    public INativeReadOnlyBuffer NativeReadOnlyBuffer => _device.NativeReadOnlyBuffer;

    /// <summary>
    /// Останавливет устройство
    /// </summary>
    public void Stop()
    {
        _device.Stop();
        SetNoneMode();
    }

    /// <summary>
    /// Загрузка программы в озу устройства
    /// </summary>
    /// <param name="program">программа</param>
    /// <param name="loadAddress">с какого адреса загрузить</param>
    /// <exception cref="InvalidOperationException"></exception>
    public void LoadProgram(ReadOnlySpan<byte> program, ulong loadAddress = ProjectBuilder.BaseAddressProgram)
    {
        ThrowIfRunning("Cannot load program while device is running. Call Stop() first.");
        _device.LoadProgram(program, loadAddress);
    }

    /// <summary>
    /// Try- форма загрузки устройства
    /// </summary>
    /// <param name="program">программа</param>
    /// <param name="loadAddress">с какого адреса загрузить</param>
    /// <param name="error">строк ошибки</param>
    /// <returns>успешность загрузки</returns>
    public bool TryLoadProgram(ReadOnlySpan<byte> program, ulong loadAddress,[NotNullWhen(false)] out string? error)
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
    /// Запуска цикла устройства, метод расчитанный на скорость выполнения, Release
    /// </summary>
    /// <param name="opt">настройки запуска</param>
    /// <returns><see cref="ProcessorFault"/> результат работы устройства, приичина остановки</returns>
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
    /// Запуска цикла устройства, метод расчитанный на отладку, Debug
    /// </summary>
    /// <param name="opt">настройки запуска</param>
    /// <returns><see cref="ProcessorFault"/> результат работы устройства, приичина остановки</returns>

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
    /// Запуск устройства в пошаговом режиме
    /// </summary>
    /// <param name="startAddress">адрес с которого запускается устройство</param>
    /// <param name="onLaunch">колбэк функция при запуске</param>
    public void RunStepMode(ulong startAddress = ProjectBuilder.BaseAddressProgram,
        Action? onLaunch = null)
    {
        ThrowIfRunning();
        ThrowByMode(Mode.Step);
        _device.BreakPointerLaunchDevice(startAddress, onLaunch);
    }
    /// <summary>
    /// Единичный шаг устройства
    /// </summary>
    /// <param name="onStep">колбэк для анализа состояния процесса</param>
    public void SingleStep(Action<IProcessorReader>? onStep = null)
    {
        ThrowIfNotStepMode();
        _device.NextStepProcessor(onStep);
    }
    /// <summary>
    /// Множественный шаг устройства
    /// </summary>
    /// <param name="count">количество шагов</param>
    /// <param name="onStep">колбэк для анализа состояния процесса</param>
    public void MultiStep(int count = 5, Action<IProcessorReader>? onStep = null)
    {
        ThrowIfNotStepMode();
        _device.NextStepProcessorCount(count, onStep);
    }
    
    /// <summary>
    /// Окончание работы устройства из пошагового режима
    /// </summary>
    public void EndStepMode()
    {
        ThrowIfNotStepMode();
        _device.Stop();
        _device.NextStepProcessor(null);
        SetNoneMode();
    }

    /// <summary>
    /// Полная очистка ОЗУ устройства
    /// </summary>
    public void ResetMemoryRam()
    {
        ThrowIfRunning();
        _device.ResetMemoryRam();
    }
    /// <summary>
    /// Метод чтобы прочитать порты этого устройства (читается только его IO буфер)
    /// </summary>
    /// <param name="offset">смещение</param>
    /// <returns>байт данных</returns>
    public byte ReadPort(ulong offset) => _device.ReadPort(offset);

    /// <summary>
    /// Копирование значений регистров в <see cref="Span{T}"/>
    /// </summary>
    /// <param name="destination"><see cref="Span{T}"/> буфера в который будут скопированы регистры</param>
    public void CopyRegisters(Span<ulong> destination) => _device.CopyRegisters(destination);

    /// <summary>
    /// Получение готового объекта <see cref="SnapshotRegisters"/>, 
    /// </summary>
    /// <returns>структура для удобного получения регистров</returns>
    public SnapshotRegisters GetRegistersSnapshot()
    {
        Span<ulong> destination = stackalloc ulong[32];
        _device.CopyRegisters(destination);
        return new(destination);
    }

    private void ThrowIfRunning(
        string msg = "Cannot launch program while device is running. Call Stop() first.")
    {
        if (IsRunning)
            throw new InvalidOperationException(msg);
    }

    private void ThrowByMode(Mode mode,
        string msg = "The device operating mode is already set. Stop the device or wait for the current operation to complete.")
    {
        if (_mode != Mode.None)
            throw new InvalidOperationException(msg);
        _mode = mode;
    }

    private void ThrowIfNotStepMode(
        string msg = "The device operating mode is not set to 'Step'.")
    {
        if (_mode != Mode.Step)
            throw new InvalidOperationException(msg);
    }
    private void SetNoneMode() => _mode = Mode.None;

}
/// <summary>
/// Zero-alloc структура, вместо массива <see cref="Array"/> имеет заинлайненный массив
/// </summary>
public readonly struct SnapshotRegisters : IEquatable<SnapshotRegisters>
{
    [InlineArray(32)]
    private struct RegisterArray { public ulong _v0; }

    private readonly RegisterArray _values;
    /// <summary>
    /// Конструктор
    /// </summary>
    /// <param name="values"><see cref="Span{T}"/> регистров</param>
    public SnapshotRegisters(Span<ulong> values)
    {
        _values = new();
        for (int i = 0; i < values.Length; i++)
            _values[i] = values[i];
    }

    /// <summary>
    /// Метод для создания нового массива из Zero-alloc структуры
    /// </summary>
    /// <returns></returns>
    public ulong[] AsArray()
    {
        var arr = new ulong[32];
        for (int i = 0; i < 32; i++) arr[i] = _values[i];
        return arr;
    }

    /// <summary>
    /// Индексатор
    /// </summary>
    /// <param name="r"><see cref="RegType"/>, чтобы нельзя было ошибиться с индексацией регистров в массиве</param>
    /// <returns></returns>
    public ulong this[RegType r]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _values![r.Int()];
    }
    /// <summary>
    /// Количество регистров, <![CDATA[static]]> так как количество регистров константно
    /// </summary>
    public static ulong Count => 32;

    /// <summary>
    /// Метод для создания <see cref="IEnumerable{T}"/> из регистров
    /// </summary>
    /// <returns><see cref="IEnumerable{T}"/> из регистров где <![CDATA[Type]]> это <see cref="RegType"/> а  <![CDATA[Value]]> это <see cref="ulong"/></returns>
    public IEnumerable<(RegType Type, ulong Value)> Enumerate()
    {
        for (int i = 0; i < 32; i++)
            yield return ((RegType)i, _values[i]);
    }

    public bool Equals(SnapshotRegisters other)
    {
        for(RegType i = 0; i < RegType.rIP + 1; i++)
        {
            if (this[i] != other[i])
                return false;
        }
        return true;
    }

    public override int GetHashCode() => _values!.GetHashCode();

    public override string ToString() =>
        string.Join(", ", Enumerate().Select(e => $"{e.Type}={e.Value:X}"));

    

    public static bool operator ==(SnapshotRegisters left, SnapshotRegisters right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(SnapshotRegisters left, SnapshotRegisters right)
    {
        return !(left == right);
    }

#pragma warning disable CS8765 // Допустимость значений NULL для типа параметра не соответствует переопределенному элементу (возможно, из-за атрибутов допустимости значений NULL).
    public override bool Equals(object obj)
#pragma warning restore CS8765 // Допустимость значений NULL для типа параметра не соответствует переопределенному элементу (возможно, из-за атрибутов допустимости значений NULL).
    {
        return obj is SnapshotRegisters registers && Equals(registers);
    }
}