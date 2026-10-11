using Kernel.BiosSystem;
using Kernel.Contracts;
using System.Diagnostics.CodeAnalysis;
using VMApplication.Project;

namespace VMApplication.Emulator;

public class DeviceContext
{
    private readonly Device _device;

    internal Device UnsafeGetDevice => _device;
    internal DeviceContext(Device device)
    {
        _device = device;
    }

    public bool IsRunning => _device.IsRunning;
    public long StepCount => _device.StepCount;
    public ulong MaxRamSize => _device.MaxRamSize;

    public INativeReadOnlyBuffer NativeReadOnlyBuffer => _device.NativeReadOnlyBuffer;

    public void Stop() => _device.Stop();

    public void LoadProgram(byte[] program, ulong loadAddress = ProjectBuilder.ZeroAdressProgram)
    {
        ThrowIfRunning("Cannot load program while device is running. Call Stop() first.");
        _device.LoadProgram(program, loadAddress);
    }

    public bool TryFastLoadProgram(ReadOnlySpan<byte> program, ulong loadAddress,[NotNullWhen(false)] out string? error)
    {
        if (IsRunning)
        {
            error = "запрещено перезаписывать программу пока устройство работает";
            return false;
        }

        if (_device.TryLoadProgramFast(program, loadAddress))
        {
            error = null;
            return true;
        }
        error = $"Не удалось загрузить программу: выход за границы памяти (loadAddress + {program.Length} > RAM) или другая не инициализированная причина";
        return false;
    }
    public void Run(LaunchOptions? opt = null)
    {
        ThrowIfRunning();

        opt ??= LaunchOptions.Default;
        _device.Run(opt.StartAddress,
            opt.ShowTimer,
            opt.OnLaunch,
            opt.OnEnd);
    }

    public void DebugRun(LaunchOptionsDebug? opt = null)
    {
        ThrowIfRunning();
        opt ??= LaunchOptionsDebug.Default;
        _device.DebugRun(opt.StartAddress,
                         opt.StepLimit,
                         opt.DelayMs,
                         opt.OnLaunch,
                         opt.OnStep,
                         opt.OnEnd);

    }


    public void SetStepMode(ulong startAddress = ProjectBuilder.ZeroAdressProgram,
        Action<IDeviceLoggerContext>? titleAct = null)
    {
        ThrowIfRunning();
        _device.BreakPointerLaunchDevice(startAddress, titleAct);
    }

    private void ThrowIfRunning(
        string msg = "Cannot launch program while device is running. Call Stop() first.")
    {
        if (IsRunning)
            throw new InvalidOperationException(msg);
    }
    public void SingleStep() => _device.NextStepProcessor();
    public void MultiStep(int count = 5) => _device.NextStepProcessorCount(count);

    public void ResetMemoryRam() => _device.ResetMemoryRam();
    public byte ReadPort(ulong offset) => _device.ReadPort(offset);
    public void ResetRegisters() => _device.ClearRegisters();
    public void CopyRegisters(Span<ulong> destination) => _device.CopyRegisters(destination);
    public ulong[]? GetRegistersSnapshot() => _device.GetRegistersSnapshot();
}
