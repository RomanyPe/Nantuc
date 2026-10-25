using Nantuc.Compiler.ASM.Utils;
using Nantuc.Kernel.Common;
using Nantuc.Samples.Scratch;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Extensions.DeviceCtx;
using Nantuc.VmApplication.Extensions.Disassembler;
using Nantuc.VmApplication.Extensions.Emulator.IO;
using Nantuc.VmApplication.Extensions.Host;
using Nantuc.VmApplication.Extensions.LibraresIncludes;

const string source1 =
    """
#include "alloc.minic"

int main() {
    // 1. Базовое выделение
    ulong a = alloc(16);
    ulong b = alloc(16);
    if (a == 0 || b == 0) return 1;
    if (a == b)           return 2;

    // 2. Store / Load
    Store64(a, 111);
    Store64(b, 222);
    if (Load64(a) != 111) return 3;
    if (Load64(b) != 222) return 4;

    // 3. free + переиспользование (LIFO — вернётся тот же адрес)
    free(a);
    ulong c = alloc(16);
    if (c != a) return 5;
    Store64(c, 333);
    if (Load64(c) != 333) return 6;

    // 4. Соседний блок b не тронут
    if (Load64(b) != 222) return 7;

    free(b);
    free(c);

    // 5. Несколько итераций alloc/Load без free — сумма = 1+2+3+4
    ulong s = 0;
    ulong i = 0;
    while (i < 4) {
        ulong p = alloc(8);
        Store64(p, i + 1);
        s = s + Load64(p);
        i = i + 1;
    }
    return s;   // 10
}
""";

using var host = VmHostFactory.CreateDefault();
var lfPolicy = new LoggingFaultPolicyMinimal(Console.Out);

host.Project.SaveFileProject("main.minic", source1);
host.Project.SaveFileInclude("heap.nanasm", Librare.heap_nanasm);
host.Project.SaveFileInclude("alloc.minic", Librare.alloc_minic);

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
    optimize: true).Build();

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
var regs = deviceCtx.GetRegistersSnapshot();

Console.WriteLine($"{RegType.r0.RegName()}: {regs[RegType.r0]}");
Console.WriteLine($"{RegType.r1.RegName()}: {regs[RegType.r1]}");
Console.WriteLine($"{RegType.r2.RegName()}: {regs[RegType.r2]}");
Console.WriteLine($"{RegType.r3.RegName()}: {regs[RegType.r3]}");
Console.WriteLine($"{RegType.r4.RegName()}: {regs[RegType.r4]}");
Console.WriteLine($"{RegType.r5.RegName()}: {regs[RegType.r5]}");
Console.WriteLine($"{RegType.rSP.RegName()}: {regs[RegType.rSP]}");
Console.WriteLine($"{RegType.rHP.RegName()}: {regs[RegType.rHP]}");

var hpPrt = regs[RegType.rHP];
var ramSnap = deviceCtx.GetRamAsNewArray(0, (nuint)hpPrt);
Console.WriteLine(StringDisassembler.Disassemble(ramSnap, resCompile.CodeSize));
