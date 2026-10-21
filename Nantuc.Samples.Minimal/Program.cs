using Nantuc.Compiler.ASM;
using Nantuc.Kernel.Common;
using Nantuc.Samples.Minimal;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Extensions.Emulator.IO;
using Nantuc.VmApplication.Extensions.Host;

const string source1 =
    """
    int y[] = { 1, 2, 4, 8, 16 };
    int main() { return y[0] + (y[1] + (y[2] + (y[3] + y[4]))); }
    """;

const string ioSource = """

    func_In:
        IN r1, r0;
        MOV r0, r1;
        RET;

    func_Out:
        OUT r0, r1;
        RET;
    """;

using var host = VMHostFactory.CreateDefault();
var lfPolicy = new LoggingFaultPolicy(Console.Out);

var fs = host.Project.FileService;
var paths = host.Project.Paths;

host.Project.SaveFileProject("main.c", source1);
host.Project.SaveFileInclude("io.asm",  ioSource);

var handle = DeviceSpec.Create(
    ramSize: RamSize.MB128,
    processorFaultPolicy: lfPolicy,
    name: "ConsoleVM"
);

var iostream = new NumericIOStream(Console.Out, PortCharEncoding.Utf8);

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
Console.WriteLine(Disassembler.Disassemble(resCompile.Program, out _, out _));

if (!deviceCtx.TryFastLoadProgram(program: resCompile.Program, loadAddress: 0, out string? error))
{
    Console.WriteLine(error);
    return;
}
deviceCtx.Run();
var regs = deviceCtx.GetRegistersSnapshot();
Console.WriteLine($"{RegType.r0.RegName()} = {regs[RegType.r0]}");
Console.WriteLine($"{RegType.r1.RegName()} = {regs[RegType.r1]}");
Console.WriteLine($"{RegType.r2.RegName()} = {regs[RegType.r2]}");
Console.WriteLine($"{RegType.r3.RegName()} = {regs[RegType.r3]}");
