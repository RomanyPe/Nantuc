using Nantuc.Kernel.BiosSystem;
using Nantuc.Kernel.Common;
using Nantuc.Kernel.Contracts;
using Nantuc.VmApplication.Project;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Nantuc.VmApplication.Emulator;

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

    public bool IsRunning => _device.IsRunning;
    public long StepCount => _device.StepCount;
    public ulong RamSize => _device.MaxRamSize;
    public INativeReadOnlyBuffer NativeReadOnlyBuffer => _device.NativeReadOnlyBuffer;

    public void Stop()
    {
        _device.Stop();
        SetNoneMode();
    }

    public void LoadProgram(byte[] program, ulong loadAddress = ProjectBuilder.BaseAddressProgram)
    {
        ThrowIfRunning("Cannot load program while device is running. Call Stop() first.");
        _device.LoadProgram(program, loadAddress);
    }

    public bool TryFastLoadProgram(ReadOnlySpan<byte> program, ulong loadAddress,[NotNullWhen(false)] out string? error)
    {
        if (IsRunning)
        {
            error = "Cannot overwrite the program while the device is running.";
            return false;
        }

        if (_device.TryLoadProgramFast(program, loadAddress))
        {
            error = null;
            return true;
        }
        error = $"Failed to load the program: memory out of bounds (loadAddress + {program.Length} > RAM) or other unexpected error.";
        return false;
    }
    public void Run(LaunchOptions? opt = null)
    {
        ThrowIfRunning();
        ThrowByMode(Mode.Continuous);

        opt ??= LaunchOptions.Default;
        _device.Run(opt.StartAddress,
            opt.ShowTimer,
            opt.OnLaunch,
            opt.OnEnd);

        SetNoneMode();
    }

    public void DebugRun(LaunchOptionsDebug? opt = null)
    {
        ThrowIfRunning();
        ThrowByMode(Mode.Continuous);
        opt ??= LaunchOptionsDebug.Default;
        _device.DebugRun(opt.StartAddress,
                         opt.StepLimit,
                         opt.DelayMs,
                         opt.OnLaunch,
                         opt.OnStep,
                         opt.OnEnd);
        SetNoneMode();
    }


    public void RunStepMode(ulong startAddress = ProjectBuilder.BaseAddressProgram,
        Action? onLaunch = null)
    {
        ThrowIfRunning();
        ThrowByMode(Mode.Step);
        _device.BreakPointerLaunchDevice(startAddress, onLaunch);
    }

    public void SingleStep(Action<IProcessorReader>? onStep = null)
    {
        ThrowIfNotStepMode();
        _device.NextStepProcessor(onStep);
    }

    public void MultiStep(int count = 5, Action<IProcessorReader>? onStep = null)
    {
        ThrowIfNotStepMode();
        _device.NextStepProcessorCount(count, onStep);
    }
    
    public void EndStepMode()
    {
        ThrowIfNotStepMode();
        _device.Stop();
        _device.NextStepProcessor(null);
        SetNoneMode();
    }

    public void ResetMemoryRam()
    {
        ThrowIfRunning();
        _device.ResetMemoryRam();
    }

    public byte ReadPort(ulong offset) => _device.ReadPort(offset);

    public void CopyRegisters(Span<ulong> destination) => _device.CopyRegisters(destination);
    public SnapshotRegisters GetRegistersSnapshot() => new(_device.GetRegistersSnapshot());

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

public readonly struct SnapshotRegisters(ulong[] values) : IEquatable<SnapshotRegisters>
{
    private readonly ulong[]? _values = values;

    public ulong this[RegType r]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _values![r.Int()];
    }

    public ulong Count => (ulong)_values!.Length;

    public IEnumerable<(RegType Type, ulong Value)> Enumerate()
    {
        for (int i = 0; i < _values!.Length; i++)
            yield return ((RegType)i, _values[i]);
    }

    public bool Equals(SnapshotRegisters other) =>
        _values.AsSpan().SequenceEqual(other._values);

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