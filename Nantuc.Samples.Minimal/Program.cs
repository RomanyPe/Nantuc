using Nantuc.Kernel.Common;
using Nantuc.Samples.Minimal;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Default;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Extensions.Emulator.IO;
using Nantuc.VmApplication.Extensions.Host;

using var host = VMHostFactory.CreateDefault(fileService: new DefaultFileService());
var lfPolicy = new LoggingFaultPolicy(Console.Out);

var handle = DeviceSpec.Create(
    ramSize: RamSize.MB128,
    processorFaultPolicy: lfPolicy,
    name: "ConsoleVM"
);

var iostream = new QueuedIOStream(Console.Out, PortCharEncoding.Utf8);

int deviceId = host.Emulator.RegisterDevice(handle: handle, sector: 0);
host.Emulator.RegisterDevice(ioDevice: iostream, name: "console", sector: 1);

var deviceCtx = host.Emulator.GetDeviceContext(deviceId)!;

var resCompile = host.Project.CompileToIL(
    baseAddress: 0,  
    optimize: true ).Build();

if (!resCompile.Success)
{
    foreach (var err in resCompile.Errors!)
        Console.WriteLine(err);
    return;
}

if (!deviceCtx.TryFastLoadProgram(program: resCompile.Program, loadAddress: 0, out string? error))
{
    Console.WriteLine(error);
    return;
}
deviceCtx.Run();