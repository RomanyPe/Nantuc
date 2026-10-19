using Nantuc.Kernel.Contracts;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Extensions.Host;
using Nantuc.VmApplication.Project;

namespace Nantuc.Benchmarks;

public class BenchRunner(VMHost host, int deviceId, BenchCase benchCase)
{
    private readonly VMHost _host = host;
    private readonly int _deviceId = deviceId;
    private readonly BenchCase _case = benchCase;
    private readonly List<double> _mipsLogs = [];
    private readonly List<double> _elapsedSecondsLogs = [];
    private readonly List<long> _stepsLogs = [];

    public BenchCase Case => _case;
    public IReadOnlyList<double> MipsLogs => _mipsLogs;
    public IReadOnlyList<double> ElapsedSecondsLog => _elapsedSecondsLogs;
    public IReadOnlyList<long> StepsLogs => _stepsLogs;
    public bool IsPrepared { get; private set; } = false;

    /// <summary>Пиковая скорость: максимум выполненных инструкций / минимальное время.</summary>
    public double PeakMips => _elapsedSecondsLogs.Count == 0
        ? 0
        : StepsOnce / Stats.Min(_elapsedSecondsLogs) / 1_000_000.0;

    /// <summary>Число инструкций в одном прогоне (должно быть стабильно).</summary>
    public long StepsOnce => _stepsLogs.Count == 0 ? 0 : _stepsLogs[0];

    /// <summary>Средне-квадратичное число инструкций за прогон.</summary>
    public double StepsVariance => _stepsLogs.Count < 2
        ? 0
        : _stepsLogs.Select(s => (double)s).ToList().ComputeVariance();

    public byte[]? Program { get; private set; }

    public bool TryPrepare()
    {
        IsPrepared = false;

        var res = _host.Project.CompileToIL(_case.ProgramText, ProjectBuilder.BaseAddressProgram, true, SourceLanguage.Asm).Build();
        if (!res.Success)
        {
            Console.WriteLine(res.Errors);
            Console.ReadLine();
            return false;
        }
        var ctx = _host.Emulator.GetDeviceContext(_deviceId)!;
        Program = res.Program;

        if (!ctx.TryFastLoadProgram(res.Program, 0, out string? errorMath))
        {
            Console.WriteLine($"Ошибка при загрузке программы для метрики блока {_case.Name}");
            Console.WriteLine(errorMath);
            Console.ReadLine();
            return false;
        }
        GC.Collect(2, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
        IsPrepared = true;
        return true;
    }

    private static void Metric(ISimulationResult res, List<double> elapsed, List<double> mips, List<long> steps)
    {
        mips.Add(res.Steps / res.Elapsed.TotalSeconds / 1_000_000.0);
        elapsed.Add(res.Elapsed.TotalSeconds);
        steps.Add(res.Steps);
    }

    public void RunOnce() => Launch(_mipsLogs, _elapsedSecondsLogs, _stepsLogs, _host.Emulator.GetDeviceContext(_deviceId));
    public void Warmup() => Launch([], [], [], _host.Emulator.GetDeviceContext(_deviceId));

    public static void Launch(
        List<double> log, List<double> logTime, List<long> logSteps, DeviceContext? launchMode)
    {
        var opt = new LaunchOptions(ShowTimer: true,
            OnEnd: (ctx, res) => Metric(res!, logTime, log, logSteps));
        launchMode?.Run(opt);
    }
}

file static class ListExtensions
{
    extension(IReadOnlyList<double> v)
    {
        public double ComputeVariance()
        {
            if (v.Count < 2) return 0;
            double avg = v.Average();
            return v.Sum(x => (x - avg) * (x - avg)) / (v.Count - 1);
        }
    }
}