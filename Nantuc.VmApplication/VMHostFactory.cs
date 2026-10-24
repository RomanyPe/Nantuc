using Nantuc.Kernel.Common;
using Nantuc.VmApplication.Default;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Project;

namespace Nantuc.VmApplication;

public static class VmHostFactory
{
    public static VmHost CreateDefault(
        IProjectFilesConfig? projectConfig = null,
        IFileService? fileService = null,
        PortSize? portBusSize = null,
        DevicePortSize? portsPerDevice = null)
    {
        var projectBuilder = new VmHostProjectBuilder()
            .WithPaths(projectConfig ?? IProjectFilesConfig.Default)
            .WithFileService(fileService ?? new RamFileService());
        var project = projectBuilder.Build();

        var emulatorBuilder = new VmEmulatorBuilder()
            .WithPortBusSize(portBusSize ?? PortSize.KB64)
            .WithPortsPerDevice(portsPerDevice ?? DevicePortSize.B16);
        var emulator = emulatorBuilder.Build();

        return new VmHost(project, emulator);
    }
}
