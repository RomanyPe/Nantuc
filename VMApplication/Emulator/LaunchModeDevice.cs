using Kernel.BiosSystem;
using Kernel.Contracts;
using VMApplication.Project;

namespace VMApplication.Emulator;

public record class LaunchOptions(ulong StartAddress = ProjectBuilder.ZeroAdressProgram,
                             bool ShowTimer = false,
                             Action<IDeviceLoggerContext>? OnLaunch = null,
                             Action<IDeviceLoggerContext, ISimulationResult?>? OnEnd = null)
{ 
    public static LaunchOptions Default { get; } = new();
}
public record class LaunchOptionsDebug(ulong StartAddress = ProjectBuilder.ZeroAdressProgram,
                             int DelayMs = 0,
                             long StepLimit = long.MaxValue,
                             Action<IDeviceLoggerContext>? OnLaunch = null,
                             Action<IProcessorReader>? OnStep = null,
                             Action<IDeviceLoggerContext>? OnEnd = null)
{ 
    public static LaunchOptionsDebug Default { get; } = new();
}