using ConsoleEmulatorForTests;
using ExtensionsVMApplication.Emulator.IO;
using Kernel.Common;
using VMApplication;
using VMApplication.Project;

const string source =
    """
    main:
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

int deviceId = host.Emulator.CreateAndAddDevice(
    ramSize: RamSize.MB128,
    sector: 0,
    processorFaultPolicy: lfPolicy,
    name: "ConsoleVM"
);

var device = host.Emulator.GetDeviceContext(deviceId)!;
var iostream = new QueuedIOStream(Console.Out, PortCharEncoding.Utf8);
host.Emulator.AddDevice(iostream, "console", 1);

CompilationToILResult resIl = host.Project.Compiler.CompileToIL(source, 0, true, SourceLanguage.Asm);
CompilationResult resCompile = host.Project.Compiler.Compile(resIl);

if (!resCompile.Success)
{
    foreach (var err in resCompile.Errors!)
        Console.WriteLine(err);
    return;
}

if (!device.TryFastLoadProgram(resCompile.Program, 0, out var error))
{
    Console.WriteLine(error!);
    return;
}
device.Run();
