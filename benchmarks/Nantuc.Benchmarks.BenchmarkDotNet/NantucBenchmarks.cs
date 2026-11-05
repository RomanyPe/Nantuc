using BenchmarkDotNet.Attributes;
using Microsoft.VSDiagnostics;
using Nantuc.Common;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Project;
using Nantuc.VmApplication.Extensions.Host;
using System;
using System.Collections.Generic;

namespace Nantuc.Benchmarks.BenchmarkDotNet;

[ShortRunJob]                     // быстрый прогон; для точных цифр замените на [MediumRunJob]
[MemoryDiagnoser]                 // опционально: покажет аллокации
public class NantucBenchmarks
{
    private VmHost _host = null!;
    private int _deviceId;
    private BenchCase _case = null!;

    /// <summary>
    /// BDN переберёт все кейсы из SourceCasesMiniC.AllCases.
    /// </summary>
    [ParamsSource(nameof(Cases))]
    public BenchCase Case { get; set; } = null!;

    public static IEnumerable<BenchCase> Cases => SourceCasesMiniC.AllCases;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _host = VmHostFactory.CreateDefault();

        var spec = DeviceSpec.Create(ramSize: RamSize.MB64, name: "ConsoleVM");
        _deviceId = _host.Emulator.RegisterDevice(spec, sector: 0);

        _case = Case;

        // Компиляция и загрузка — вне замера
        var res = _host.Project
            .CompileToIL(_case.ProgramText, ProjectBuilder.BaseAddressProgram, true, SourceLanguage.C)
            .Build();

        if (!res.Success)
        {
            var errors = string.Join("\n", res.Errors ?? []);
            throw new InvalidOperationException($"Compilation failed for '{_case.Name}':\n{errors}");
        }

        var ctx = _host.Emulator.GetDeviceContext(_deviceId)!;
        if (!ctx.TryLoadProgram(res.Program, 0, out var error))
            throw new InvalidOperationException($"Failed to load program for '{_case.Name}': {error}");
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _host?.Dispose();
    }

    /// <summary>
    /// Один прогон виртуальной машины. Возвращаем количество шагов, чтобы JIT не выбросил вызов.
    /// </summary>
    [Benchmark]
    public long RunVm()
    {
        var ctx = _host.Emulator.GetDeviceContext(_deviceId)!;
        long steps = 0;

        var opt = new LaunchOptions(
            ShowTimer: true,
            OnEnd: res => steps = res.Steps);

        ctx.Run(opt);
        return steps;
    }
}
