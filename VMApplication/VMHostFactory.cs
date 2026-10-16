using Kernel.Common;
using VMApplication.Default;
using VMApplication.Emulator;
using VMApplication.Project;

namespace VMApplication;

public static class VMHostFactory
{
    public static VMHost CreateDefault(
        IProjectFilesConfig? projectConfig = null,
        IFileService? fileService = null,
        PortSize? portBusSize = null,
        DevicePortSize? portsPerDevice = null)
    {

        // Создаём проект
        projectConfig ??= IProjectFilesConfig.Default;

        var projectBuilder = new VMHostProjectBuilder()
            .WithPaths(projectConfig)
            .WithFileService(fileService ?? new DefaultFileService(projectConfig.ProjectPath));
        var project = projectBuilder.Build();

        // Создаём эмулятор
        var emulatorBuilder = new VMEmulatorBuilder()
            .WithPortBusSize(portBusSize ?? PortSize.KB64)
            .WithPortsPerDevice(portsPerDevice ?? DevicePortSize.B16);
        var emulator = emulatorBuilder.Build();

        return new VMHost(project, emulator);
    }
}
