using Kernel.Common;
using Kernel.Contracts;
using System.Xml.Linq;
using VMApplication;
using VMApplication.Emulator;
using VMApplication.Project;

namespace ExtensionsVMApplication.Host;

public sealed record RunOutcome(
    DeviceContext? Device,
    int? DeviceId,
    CompilationResult Compilation,
    long StepCount,
    bool Success,
    IReadOnlyList<string> Diagnostics);



public static class VMHostRunExtensions
{

    extension(VMHost host)
    {
        /// Запускает и оставляет device живым — можно читать память, регистры, порты.
        public RunOutcome RunAndKeep(
            string source,
            SourceLanguage lang,
            RamSize? ram = null,
            bool optimize = false,
            string deviceName = "run")
        {
            ram ??= RamSize.MB16;
            var comp = host.Project.CompileToIL(source, 0, optimize, lang).Build();

            if (!comp.Success)
                return new(null, null, comp, 0, false, comp.Errors ?? []);
            // device пока не создавали — возвращать нечего

            var spec = DeviceSpec.Create(ram, name: deviceName);
            int id = host.Emulator.RegisterDevice(spec, 0);
            var ctx = host.Emulator.GetDeviceContext(id)!;

            if (!ctx.TryFastLoadProgram(comp.Program!, 0, out var err))
            {
                host.Emulator.UnregisterDevice(id);
                return new(null, null, comp, 0, false, [err]);
            }

            ctx.Run(new LaunchOptions(StartAddress: 0));
            return new(ctx, id, comp, ctx.StepCount, true, []);
        }

        /// Запускает и убирает за собой. Ничего не возвращает — только факт успеха.
        public bool RunAndCleanup(
            string source,
            SourceLanguage lang,
            RamSize? ram = null,
            bool optimize = false)
        {
            ram ??= RamSize.MB16;

            var res = host.RunAndKeep(source, lang, ram, optimize);
            if (res.Device != null && res.DeviceId.HasValue) host.Emulator.UnregisterDevice(res.DeviceId.Value);
            return res.Success;
        }


        public RunOutcome RunOnce(string source, SourceLanguage lang, RamSize? ram = null, bool optimize = false)
        {
            var res = host.RunAndKeep(source, lang, ram, optimize);
            if (res.Device is not null) host.Emulator.UnregisterDevice(res.DeviceId!.Value);
            return res with { Device = null, DeviceId = null };
        }
    }
}
