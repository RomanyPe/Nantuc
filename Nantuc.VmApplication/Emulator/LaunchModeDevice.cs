using Nantuc.Kernel.Contracts;
using Nantuc.VmApplication.Project;

namespace Nantuc.VmApplication.Emulator;

public record class LaunchOptions(ulong StartAddress = ProjectBuilder.BaseAddressProgram,
                             bool ShowTimer = false,
                             Action? OnLaunch = null,
                             Action<ISimulationResult?>? OnEnd = null)
{ 
    public static LaunchOptions Default { get; } = new();
}


public record class LaunchOptionsDebug(ulong StartAddress = ProjectBuilder.BaseAddressProgram,
                             int DelayMs = 0,
                             long StepLimit = long.MaxValue,
                             Action? OnLaunch = null,
                             Action<IProcessorReader>? OnStep = null,
                             Action? OnEnd = null)
{ 
    public static LaunchOptionsDebug Default { get; } = new();
}