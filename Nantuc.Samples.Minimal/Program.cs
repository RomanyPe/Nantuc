using Nantuc.Kernel.Common;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Extensions.Emulator.IO;
using Nantuc.VmApplication.Extensions.Host;
using Nantuc.VmApplication.Extensions.LibraresIncludes;

const string source1 =
    """
    #include "print.minic"

    char str[] = "Hello world";

    void main() {
        print16(str);
    }    
    """;


using var host = VmHostFactory.CreateDefault();
var lfPolicy = new LoggingFaultPolicyMinimal(Console.Out);

host.Project.SaveFileProject("main.minic", source1);
host.Project.SaveFileInclude("io.nanasm", Librare.io_nanasm);
host.Project.SaveFileInclude("print.minic",  Librare.print_minic);

var handle = DeviceSpec.Create(
    ramSize: RamSize.MB128,
    processorFaultPolicy: lfPolicy,
    name: "ConsoleVM"
);

var iostream = new QueuedIOStream(Console.Out, PortCharEncoding.Utf16);

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