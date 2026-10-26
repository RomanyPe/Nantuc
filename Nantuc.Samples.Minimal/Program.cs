using Nantuc.Kernel.Common;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Extensions.Emulator.IO;
using Nantuc.VmApplication.Extensions.Host;
using Nantuc.VmApplication.Extensions.LibraresIncludes;

const string source =
    """
    #include "print.minic"
    char str[] = "Hello world";
    void main() { print16(str); }
    """;

using var host = VmHostFactory.CreateDefault();
host.Project.SaveFileProject("main.minic", source);
host.Project.SaveFileInclude("io.nanasm", Librare.io_nanasm);
host.Project.SaveFileInclude("print.minic", Librare.print_minic);

// 1) Регистрация VM — через TryRegisterDevice, IdState вместо int < 0
var vmState = host.Emulator.TryRegisterDevice(
    DeviceSpec.Create(RamSize.MB128, name: "ConsoleVM"),
    sector: 0,
    out int vmId);
if (vmState != IdState.Valid) { Console.Error.WriteLine($"register vm: {vmState}"); return; }

// 2) Регистрация UART — тоже через TryRegisterDevice
var uartState = host.Emulator.TryRegisterDevice(
    new QueuedIOStream(Console.Out, PortCharEncoding.Utf16),
    name: "console",
    sector: 1,
    out _);
if (uartState != IdState.Valid) { Console.Error.WriteLine($"register uart: {uartState}"); return; }

// 3) DeviceContext — через TryGetDeviceContext (без !)
if (!host.Emulator.TryGetDeviceContext(vmId, out var vm)) { Console.Error.WriteLine("no ctx"); return; }

// 4) Компиляция
var compilation = host.Project.CompileToIL(baseAddress: 0, optimize: true).Build();
if (!compilation.Success)
{
    foreach (var e in compilation.Errors!) Console.Error.WriteLine(e);
    return;
}

// 5) Загрузка — через TryFastLoadProgram
if (!vm.TryFastLoadProgram(compilation.Program!, loadAddress: 0, out var error))
{
    Console.Error.WriteLine(error);
    return;
}

// 6) Запуск — Run возвращает ProcessorFault, статус можно проверить
ProcessorFault fault = vm.Run();
if (fault.Status != BiosStatus.EndProgramm)
    Console.Error.WriteLine($"VM fault: {fault.Status} at IP=0x{fault.InstructionPointer:X}");