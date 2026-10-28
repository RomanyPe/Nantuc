using Nantuc.Kernel.Common;
using Nantuc.Kernel.Diagnostics;
using Nantuc.VmApplication.Default;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Project;

namespace Nantuc.VmApplication;

public static class VmHostFactory
{
    /// <summary>
    /// Создает хоста с дефолтными объектами
    /// </summary>
    /// <param name="projectConfig"> Своя файловая конфигурация </param>
    /// <param name="fileService"> Свой файловый сервис </param>
    /// <param name="portBusSize"> Размер шины портов </param>
    /// <param name="optimizator">Готовые правила оптимизаций компиляции</param>
    /// <param name="portsPerDevice">Размер сектора на устройство</param>
    /// <param name="provider">Политика исключений компилятора</param>
    /// <returns>Объект <see cref="VmHost"/></returns>
    public static VmHost CreateDefault(
        IProjectFilesConfig? projectConfig = null,
        IFileService? fileService = null,
        VmCompilerOptimizator? optimizator = null,
        IErrorMessageProvider? provider = null,
        PortSize? portBusSize = null,
        DevicePortSize? portsPerDevice = null)
    {
        var projectBuilder = new VmHostProjectBuilder()
            .WithPaths(projectConfig ?? IProjectFilesConfig.Default)
            .WithFileService(fileService ?? new RamFileService())
            .WithOptimizator(optimizator)
            .WithThrowProvider(provider);
        var project = projectBuilder.Build();

        var emulatorBuilder = new VmEmulatorBuilder()
            .WithPortBusSize(portBusSize ?? PortSize.KB64)
            .WithPortsPerDevice(portsPerDevice ?? DevicePortSize.B16);
        var emulator = emulatorBuilder.Build();

        return new VmHost(project, emulator);
    }
}
