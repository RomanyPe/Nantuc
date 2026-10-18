using ConsoleEmulatorForTests;
using ExtensionsVMApplication.Emulator.IO;
using ExtensionsVMApplication.Host;
using Kernel.Common;
using VMApplication;
using VMApplication.Emulator;

const string source1 =
    """
    void main(){
        log();
    }
    """;

const string source2 =
    """
    void log() {
        asm {
            LDI  r1, 0x41;          // r1 = данные ('A')
            LDI  r2, 0x10;          // r2 = адрес порта Data
            OUT  r1, r2;            // OUT regSrc=r1, regPort=r2

            LDI  r1, 1;             // данные для Flush
            LDI  r2, 0x12;          // адрес порта Flush
            OUT  r1, r2;

            END;
        }
    }
    """;


using var host = VMHostFactory.CreateDefault();
var lfPolicy = new LoggingFaultPolicy(Console.Out);

host.Project.FileService.SafeFile("main.c", source1);
host.Project.FileService.SafeFile("foo.c", source2);

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