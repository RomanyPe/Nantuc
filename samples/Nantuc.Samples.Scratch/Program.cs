using Nantuc.Common;
using Nantuc.Samples.Scratch.SnakeGame;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Extensions.DeviceCtx;
using Nantuc.VmApplication.Extensions.Disassembler;
using Nantuc.VmApplication.Extensions.Emulator.IO;
using Nantuc.VmApplication.Extensions.Host;
using Nantuc.VmApplication.Extensions.StdLibraries;
using Nantuc.VmApplication.Project;

const string source1 =
    """
    #include "alloc.minic"

    int main() {
        ulong a = alloc(16);
        ulong b = alloc(16);
        if (a == 0 || b == 0) return 1;
        if (a == b)           return 2;

        Store64(a, 111);
        Store64(b, 222);
        if (Load64(a) != 111) return 3;
        if (Load64(b) != 222) return 4;

        free(a);
        ulong c = alloc(16);
        if (c != a) return 5;          // free-list вернул тот же блок
        Store64(c, 333);
        if (Load64(c) != 333) return 6;
        if (Load64(b) != 222) return 7; // сосед не тронут

        free(b);
        free(c);

        // bump после освобождений + split
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

if (args.Length >= 2 && args[0] == "-snake")
{
    int period = int.TryParse(args[1], out int res) && res > 0
        ? res
        : 200;

    var game = new SnakeGameOnNantuc(period);
    game.Run();
    return;
}

using var host = VmHostFactory.CreateDefault();

host.Project.SaveFileInclude("heap.nanasm", Libraries.heap_nanasm);
host.Project.SaveFileInclude("alloc.minic", Libraries.alloc_minic);

var outcome = host.RunAndKeep(
    source: source1,
    lang: SourceLanguage.MiniC, // при необходимости уточните имя константы в enum
    ram: RamSize.KB128,
    optimize: true,
    deviceName: "ConsoleVM",
    sector: 0,
    output: Console.Out,
    encoding: PortCharEncoding.Utf8
);

// Если устройство не создалось — значит, упала компиляция или регистрация
if (outcome.Device is null)
{
    foreach (var diag in outcome.Diagnostics)
        Console.WriteLine(diag);
    return;
}

if (!outcome.Success)
{
    foreach (var diag in outcome.Diagnostics)
        Console.WriteLine(diag);
}

var deviceCtx = outcome.Device;
var regs = deviceCtx.GetRegistersSnapshot();

Console.WriteLine($"{RegType.r0.AsText}: {regs[RegType.r0]}");
Console.WriteLine($"{RegType.r1.AsText}: {regs[RegType.r1]}");
Console.WriteLine($"{RegType.r2.AsText}: {regs[RegType.r2]}");
Console.WriteLine($"{RegType.r3.AsText}: {regs[RegType.r3]}");
Console.WriteLine($"{RegType.r4.AsText}: {regs[RegType.r4]}");
Console.WriteLine($"{RegType.r5.AsText}: {regs[RegType.r5]}");
Console.WriteLine($"{RegType.rSP.AsText}: {regs[RegType.rSP]}");
Console.WriteLine($"{RegType.rHP.AsText}: {regs[RegType.rHP]}");

var hpPrt = regs[RegType.rHP];
var ramSnap = deviceCtx.GetRamAsNewArray(0, (nuint)hpPrt);
Console.WriteLine(StringDisassembler.Disassemble(ramSnap, outcome.Compilation.CodeSize));
