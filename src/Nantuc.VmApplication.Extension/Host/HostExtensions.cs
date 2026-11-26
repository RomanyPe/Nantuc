using Nantuc.Common;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Project;
using Nantuc.VmApplication.Extensions.Emulator.IO;
using Nantuc.VmApplication.Extensions.AsyncDeviceExtensions;

namespace Nantuc.VmApplication.Extensions.Host;

public sealed record class RunOutcome(
    DeviceContext? Device,
    int? DeviceId,
    CompilationResult Compilation,
    long StepCount,
    bool Success,
    IReadOnlyList<string> Diagnostics,
    ProcessorFault FaultInfo);

public sealed record class AsyncRunOutcome(
    AsyncDeviceContext? Device,
    int? DeviceId,
    CompilationResult Compilation,
    long StepCount,
    bool Success,
    IReadOnlyList<string> Diagnostics,
    ProcessorFault FaultInfo);

public static class VMHostRunExtensions
{
    extension(VmHost host)
    {
        /// Запускает и оставляет device живым — можно читать память, регистры, порты.
        public RunOutcome RunAndKeep(
            string source,
            SourceLanguage lang,
            RamSize? ram = null,
            bool optimize = false,
            string deviceName = "run",
            uint sector = 0,
            TextWriter? output = null,
            PortCharEncoding encoding = PortCharEncoding.Utf16)
        {
            ram ??= RamSize.MB16;

            // 1) Компиляция
            var comp = host.Project.CompileToIR(source, 0, optimize, lang).Build();
            if (!comp.Success)
                return new(null, null, comp, 0, false, [.. (comp.Errors ?? []).Select(e => e.Message)], default);

            // 2) Регистрация VM — безопасно, через TryRegisterDevice
            var spec = DeviceSpec.Create(ram, name: deviceName);
            var vmState = host.Emulator.TryRegisterDevice(spec, sector, out int vmId);
            if (vmState != IdState.Valid)
                return new(null, null, comp, 0, false, [$"register vm: {vmState.AsText}"], default);

            // 3) Регистрация UART (если нужен вывод)
            int? uartId = null;
            if (output is not null)
            {
                var uartState = host.Emulator.TryRegisterDevice(
                    new QueuedIOStream(output, encoding),
                    name: $"{deviceName}-console",
                    sector: sector + 1,
                    out int uart);
                if (uartState != IdState.Valid)
                {
                    host.Emulator.UnregisterDevice(vmId);
                    return new(null, null, comp, 0, false, [$"register uart: {uartState.AsText}"], default);
                }
                uartId = uart;
            }

            // 4) Контекст — без '!'
            if (!host.Emulator.TryGetDeviceContext(vmId, out var ctx))
            {
                host.Emulator.UnregisterDevice(vmId);
                if (uartId is int u) host.Emulator.UnregisterDevice(u);
                return new(null, null, comp, 0, false, ["no device context"], default);
            }

            // 5) Загрузка
            if (!ctx.TryLoadProgram(comp.Program!, 0, out var err))
            {
                host.Emulator.UnregisterDevice(vmId);
                if (uartId is int u) host.Emulator.UnregisterDevice(u);
                return new(null, null, comp, 0, false, [err], default);
            }

            // 6) Запуск — Success считается по fault.Status, а не по факту запуска
            var fault = ctx.Run(new LaunchOptions(StartAddress: 0));
            bool ok = fault.Status == BiosStatus.EndProgram;
            IReadOnlyList<string> diags = ok
                ? []
                : [$"VM fault: {fault.Status.AsText} at IP=0x{fault.InstructionPointer:X}"];

            if (uartId is int uartIdValue) host.Emulator.UnregisterDevice(uartIdValue);
            return new(ctx, vmId, comp, ctx.StepCount, ok, diags, fault);
        }

        public bool RunAndCleanup(
                    string source,
                    SourceLanguage lang,
                    RamSize? ram = null,
                    bool optimize = false,
                    string deviceName = "run",
                    uint sector = 0,
                    TextWriter? output = null,
                    PortCharEncoding encoding = PortCharEncoding.Utf16)
        {
            var res = host.RunAndKeep(source, lang, ram, optimize, deviceName, sector, output, encoding);
            if (res.DeviceId is int id) host.Emulator.UnregisterDevice(id);
            return res.Success;
        }

        public RunOutcome RunOnce(
            string source,
            SourceLanguage lang,
            RamSize? ram = null,
            bool optimize = false,
            string deviceName = "run",
            uint sector = 0,
            TextWriter? output = null,
            PortCharEncoding encoding = PortCharEncoding.Utf16)
        {
            var res = host.RunAndKeep(source, lang, ram, optimize, deviceName, sector, output, encoding);
            if (res.DeviceId is int id) host.Emulator.UnregisterDevice(id);
            return res with { Device = null, DeviceId = null };
        }

        public async ValueTask<AsyncRunOutcome> AsyncRunAndKeep(
            string source,
            SourceLanguage lang,
            RamSize? ram = null,
            bool optimize = false,
            string deviceName = "run",
            uint sector = 0,
            TextWriter? output = null,
            PortCharEncoding encoding = PortCharEncoding.Utf16,
            CancellationToken token = default)
        {
            ram ??= RamSize.MB16;

            // 1) Компиляция
            var comp = host.Project.CompileToIR(source, 0, optimize, lang).Build();
            if (!comp.Success)
                return new(null, null, comp, 0, false, [.. (comp.Errors ?? []).Select(e => e.Message)], default);

            // 2) Регистрация VM — безопасно, через TryRegisterDevice
            var spec = DeviceSpec.Create(ram, name: deviceName);
            var vmState = host.Emulator.TryRegisterAsyncDevice(spec, sector, out int vmId);
            if (vmState != IdState.Valid)
                return new(null, null, comp, 0, false, [$"register vm: {vmState.AsText}"], default);

            // 3) Регистрация UART (если нужен вывод)
            int? uartId = null;
            if (output is not null)
            {
                var uartState = host.Emulator.TryRegisterDevice(
                    new QueuedIOStream(output, encoding),
                    name: $"{deviceName}-console",
                    sector: sector + 1,
                    out int uart);
                if (uartState != IdState.Valid)
                {
                    host.Emulator.UnregisterDevice(vmId);
                    return new(null, null, comp, 0, false, [$"register uart: {uartState.AsText}"], default);
                }
                uartId = uart;
            }

            // 4) Контекст — без '!'
            if (!host.Emulator.TryGetAsyncDeviceContext(vmId, out var ctx))
            {
                host.Emulator.UnregisterDevice(vmId);
                if (uartId is int u) host.Emulator.UnregisterDevice(u);
                return new(null, null, comp, 0, false, ["no device context"], default);
            }

            // 5) Загрузка
            if (!ctx.TryLoadProgram(comp.Program!, 0, out var err))
            {
                host.Emulator.UnregisterDevice(vmId);
                if (uartId is int u) host.Emulator.UnregisterDevice(u);
                return new(null, null, comp, 0, false, [err], default);
            }

            // 6) Запуск — Success считается по fault.Status, а не по факту запуска
            var fault = await ctx.Run(new LaunchOptions(StartAddress: 0), token);
            bool ok = fault.Status == BiosStatus.EndProgram;
            IReadOnlyList<string> diags = ok
                ? []
                : [$"VM fault: {fault.Status.AsText} at IP=0x{fault.InstructionPointer:X}"];

            if (uartId is int uartIdValue) host.Emulator.UnregisterDevice(uartIdValue);
            return new(ctx, vmId, comp, ctx.StepCount, ok, diags, fault);
        }

        public async ValueTask<bool> AsyncRunAndCleanup(
                    string source,
                    SourceLanguage lang,
                    RamSize? ram = null,
                    bool optimize = false,
                    string deviceName = "run",
                    uint sector = 0,
                    TextWriter? output = null,
                    PortCharEncoding encoding = PortCharEncoding.Utf16,
                    CancellationToken token = default)
        {
            var res = await host.AsyncRunAndKeep(source, lang, ram, optimize, deviceName, sector, output, encoding, token);
            if (res.DeviceId is int id) host.Emulator.UnregisterDevice(id);
            return res.Success;
        }

        public async ValueTask<AsyncRunOutcome> AsyncRunOnce(
            string source,
            SourceLanguage lang,
            RamSize? ram = null,
            bool optimize = false,
            string deviceName = "run",
            uint sector = 0,
            TextWriter? output = null,
            PortCharEncoding encoding = PortCharEncoding.Utf16,
            CancellationToken token = default)
        {
            var res = await host.AsyncRunAndKeep(source, lang, ram, optimize, deviceName, sector, output, encoding, token);
            if (res.DeviceId is int id) host.Emulator.UnregisterDevice(id);
            return res with { Device = null, DeviceId = null };
        }
    }
}