using Nantuc.Kernel.Common;
using Nantuc.VmApplication.Default;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Project;

namespace Nantuc.VmApplication;

public static class VMHostFactory
{
    public static VMHost CreateDefault(
        IProjectFilesConfig? projectConfig = null,
        IFileService? fileService = null,
        PortSize? portBusSize = null,
        DevicePortSize? portsPerDevice = null)
    {
        projectConfig ??= IProjectFilesConfig.Default;
        var projectBuilder = new VMHostProjectBuilder()
            .WithPaths(projectConfig)
            .WithFileService(fileService ?? new RamFileService(projectConfig));
        var project = projectBuilder.Build();

        var emulatorBuilder = new VMEmulatorBuilder()
            .WithPortBusSize(portBusSize ?? PortSize.KB64)
            .WithPortsPerDevice(portsPerDevice ?? DevicePortSize.B16);
        var emulator = emulatorBuilder.Build();

        return new VMHost(project, emulator);
    }
}
