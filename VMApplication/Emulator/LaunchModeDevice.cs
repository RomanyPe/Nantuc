using Kernel.Contracts;
using VMApplication.Project;

namespace VMApplication.Emulator;

public record class LaunchOptions(ulong StartAddress,
                             bool ShowTimer,
                             Action<IDeviceLoggerContext>? OnLaunch,
                             Action<IDeviceLoggerContext, ISimulationResult?>? OnEnd)
{ 
    public static LaunchOptions Default { get; } = new(ProjectBuilder.BaseAddressProgram, false, null, null);
}


public record class LaunchOptionsDebug(ulong StartAddress,
                             int DelayMs,
                             long StepLimit,
                             Action<IDeviceLoggerContext>? OnLaunch,
                             Action<IProcessorReader>? OnStep,
                             Action<IDeviceLoggerContext>? OnEnd)
{ 
    public static LaunchOptionsDebug Default { get; } = new(ProjectBuilder.BaseAddressProgram, 0, long.MaxValue, null, null, null);
}