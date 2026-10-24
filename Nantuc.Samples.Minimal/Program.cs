using Nantuc.Compiler.ASM.Utils;
using Nantuc.Kernel.Common;
using Nantuc.Samples.Minimal;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Extensions.Disassembler;
using Nantuc.VmApplication.Extensions.Emulator.IO;
using Nantuc.VmApplication.Extensions.Host;

const string source1 =
    """
    #include "print.minic"

    char str[] = "Hello world";

    void main(){
        print(&str);
    }
    
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

const string printSource =
    """
    #include "io.nanasm"

    extern void Out(byte value, int port);

    int portWrite = 0x10;
    int portFlush = 0x12;

    void print(char* str){
        int i = 0;

        while (str[i] != '\0') { 
            write(str[i]);
            i = i + 1;
        }
        flush();
    }

    void write(byte c){
        Out(c, portWrite);
    }

    void flush(){
        Out(1, portFlush);
    }
    """;

using var host = VmHostFactory.CreateDefault();
var lfPolicy = new LoggingFaultPolicy(Console.Out);

host.Project.SaveFileProject("main.minic", source1);
host.Project.SaveFileInclude("io.nanasm", ioSource);
host.Project.SaveFileInclude("print.minic",  printSource);

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
Console.WriteLine(resCompile.DisassembleToString());

if (!deviceCtx.TryFastLoadProgram(program: resCompile.Program, loadAddress: 0, out string? error))
{
    Console.WriteLine(error);
    return;
}
deviceCtx.Run();
var regs = deviceCtx.GetRegistersSnapshot();
Console.WriteLine($"{RegType.r0.RegName()}: {regs[RegType.r0]}");
Console.WriteLine($"{RegType.r0.RegName()}: {regs[RegType.r0]}");
Console.WriteLine($"{RegType.r1.RegName()}: {regs[RegType.r1]}");
Console.WriteLine($"{RegType.r2.RegName()}: {regs[RegType.r2]}");
//for (RegType i = 0; i < RegType.rIP + 1; i++)
//{
//    Console.WriteLine(regs[i]);
//}
//var ram = deviceCtx.NativeReadOnlyBuffer;
//var buffer = new byte[resCompile.Program!.Length];

//for (nuint i = 0; i < (nuint)resCompile.Program!.Length; i++)
//{
//    buffer[i] = ram[i];
//}
//Console.WriteLine(StringDisassembler.Disassemble(buffer, resCompile.CodeSize!.Value));