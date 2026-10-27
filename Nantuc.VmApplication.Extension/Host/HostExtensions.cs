using Nantuc.Kernel.Common;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Project;

namespace Nantuc.VmApplication.Extensions.Host;

public sealed record RunOutcome(
    DeviceContext? Device,
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
            uint sector = 0)
        {
            ram ??= RamSize.MB16;
            var comp = host.Project.CompileToIL(source, 0, optimize, lang).Build();

            if (!comp.Success)
                return new(null, null, comp, 0, false, comp.Errors ?? [], default);
            // device пока не создавали — возвращать нечего

            var spec = DeviceSpec.Create(ram, name: deviceName);
            int id = host.Emulator.RegisterDevice(spec, sector);
            var ctx = host.Emulator.GetDeviceContext(id)!;

            if (!ctx.TryLoadProgram(comp.Program!, 0, out var err))
            {
                host.Emulator.UnregisterDevice(id);
                return new(null, null, comp, 0, false, [err], default);
            }

            var fault = ctx.Run(new LaunchOptions(StartAddress: 0));
            return new(ctx, id, comp, ctx.StepCount, true, [], fault);
        }

        /// Запускает и убирает за собой. Ничего не возвращает — только факт успеха.
        public bool RunAndCleanup(
            string source,
            SourceLanguage lang,
            RamSize? ram = null,
            bool optimize = false, 
            uint sector = 0)
        {
            ram ??= RamSize.MB16;

            var res = host.RunAndKeep(source, lang, ram, optimize, sector: sector);
            if (res.Device != null && res.DeviceId.HasValue) host.Emulator.UnregisterDevice(res.DeviceId.Value);
            return res.Success;
        }


        public RunOutcome RunOnce(string source, SourceLanguage lang, RamSize? ram = null, bool optimize = false, uint sector = 0)
        {
            var res = host.RunAndKeep(source, lang, ram, optimize, sector: sector);
            if (res.Device is not null) host.Emulator.UnregisterDevice(res.DeviceId!.Value);
            return res with { Device = null, DeviceId = null };
        }
    }
}
