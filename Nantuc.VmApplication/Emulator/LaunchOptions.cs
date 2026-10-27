using Nantuc.Kernel.Contracts;
using Nantuc.VmApplication.Project;

namespace Nantuc.VmApplication.Emulator;

/// <summary>
/// Правила запуска устройства, Release
/// </summary>
/// <param name="StartAddress">Стартовый адрес запуска</param>
/// <param name="StepLimit">Лимит шагов</param>
/// <param name="ShowTimer">Запустить таймер</param>
/// <param name="OnLaunch">Колбэк вызывающийся при запуске устройства</param>
/// <param name="OnEnd">Колбэк вызывающийся при остановке устройства</param>
public record class LaunchOptions(ulong StartAddress = ProjectBuilder.BaseAddressProgram,
                             long? StepLimit = null,
                             bool ShowTimer = false,
                             Action? OnLaunch = null,
                             Action<ISimulationResult?>? OnEnd = null)
{ 
    /// <summary>
    /// Дефолтные настройки
    /// </summary>
    public static LaunchOptions Default { get; } = new();
}

/// <summary>
/// Правила запуска устройства, Debug
/// </summary>
/// <param name="StartAddress">Стартовый адрес</param>
/// <param name="DelayMs">Задержка между итерациями, измеряется в миллисекундах</param>
/// <param name="StepLimit">Лимит шагов</param>
/// <param name="OnLaunch">Колбэк вызывающийся при запуске устройства</param>
/// <param name="OnStep">Колбэк вызывающйся на каждой итерации устройства</param>
/// <param name="OnEnd">Колбэк вызывающийся при остановке устройства</param>
public record class LaunchOptionsDebug(ulong StartAddress = ProjectBuilder.BaseAddressProgram,
                             int DelayMs = 0,
                             long StepLimit = long.MaxValue,
                             Action? OnLaunch = null,
                             Action<IProcessorReader>? OnStep = null,
                             Action? OnEnd = null)
{ 
    /// <summary>
    /// Дефолтные настройки
    /// </summary>
    public static LaunchOptionsDebug Default { get; } = new();
}