using Nantuc.Kernel.Contracts;
using Nantuc.VmApplication.Project;

namespace Nantuc.VmApplication.Emulator;

public record class LaunchOptions(ulong StartAddress = ProjectBuilder.BaseAddressProgram,
                             bool ShowTimer = false,
                             Action<IDeviceLoggerContext>? OnLaunch = null,
                             Action<IDeviceLoggerContext, ISimulationResult?>? OnEnd = null)
{ 
    public static LaunchOptions Default { get; } = new();
}


public record class LaunchOptionsDebug(ulong StartAddress = ProjectBuilder.BaseAddressProgram,
                             int DelayMs = 0,
                             long StepLimit = long.MaxValue,
                             Action<IDeviceLoggerContext>? OnLaunch = null,
                             Action<IProcessorReader>? OnStep = null,
                             Action<IDeviceLoggerContext>? OnEnd = null)
{ 
    public static LaunchOptionsDebug Default { get; } = new();
}