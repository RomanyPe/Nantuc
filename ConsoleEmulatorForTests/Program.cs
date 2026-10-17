using ConsoleEmulatorForTests;
using ExtensionsVMApplication.Emulator.IO;
using Kernel.Common;
using VMApplication;
using VMApplication.Emulator;
using VMApplication.Project;

const string source =
    """
    LDI  r1, 0x41          // r1 = данные ('A')
    LDI  r2, 0x10          // r2 = адрес порта Data
    OUT  r1, r2            // OUT regSrc=r1, regPort=r2

    LDI  r1, 1             // данные для Flush
    LDI  r2, 0x12          // адрес порта Flush
    OUT  r1, r2

    END
    """;


using var host = VMHostFactory.CreateDefault();
var lfPolicy = new LoggingFaultPolicy(Console.Out);

var handle = DeviceSpec.Create(
    ramSize: RamSize.MB128,
    processorFaultPolicy: lfPolicy,
    name: "ConsoleVM"
);

var iostream = new QueuedIOStream(Console.Out, PortCharEncoding.Utf8);

var deviceId = host.Emulator.RegisterDevice(handle, sector: 0);
host.Emulator.RegisterDevice(ioDevice: iostream, name: "console", sector: 1);

var deviceCtx = host.Emulator.GetDeviceContext(deviceId)!;
var resCompile = host.Project.Compiler.CompileToIL(
    source: source,
    baseAddress: 0,  
    optimize: true,
    SourceLanguage.Asm).Build();


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