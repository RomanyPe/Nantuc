using Kernel.Common;
using VMApplication.Default;
using VMApplication.Emulator;
using VMApplication.Logger;
using VMApplication.Project;

namespace VMApplication;

public static class VMHostFactory
{
    public static VMHost CreateDefault(
        IOutputView? outputView = null,
        IProjectFilesConfig? projectConfig = null,
        IFileService? fileService = null,
        PortSize? portBusSize = null,
        DevicePortSize? portsPerDevice = null)
    {
        // Создаём логгер
        var loggerBuilder = new LoggerBuilder()
            .WithOutPut(outputView ?? new ConsoleOutputView());
        var logger = loggerBuilder.Build();

        // Создаём проект
        var res = projectConfig ?? IProjectFilesConfig.Default;

        var projectBuilder = new VMHostProjectBuilder()
            .WithLogger(logger)
            .WithPaths(res)
            .WithFileService(fileService ?? new DefaultFileService(res.ProjectPath));
        var project = projectBuilder.Build();

        // Создаём эмулятор
        var emulatorBuilder = new VMEmulatorBuilder()
            .WithLogger(logger)
            .WithPortBusSize(portBusSize ?? PortSize.KB64)
            .WithPortsPerDevice(portsPerDevice ?? DevicePortSize.B16);
        var emulator = emulatorBuilder.Build();

        return new VMHost(project, emulator, logger);
    }
}
