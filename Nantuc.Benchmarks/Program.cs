using Nantuc.Kernel.Common;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Extensions.Disassembler;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Nantuc.Benchmarks;

internal class Program
{
    const string text1 =
    """
        LDI r0, 0;
        LDI r1, 10000000;

    loop:
        CALL Sum;
        DEC r1;
        TEST r1, r1;
        JNZ loop;

        END;

    Sum:
        ADD r0, r1;
        RET;
    """;

    const string text2 =
    """
        LDI r0, 0;
        LDI r1, 10000000;

    loop:
        ADD r0, r1;
        DEC r1;
        TEST r1, r1;
        JNZ loop;

        END;
    """;

    const string text3 =
    """
        LDI r0, 0;
        LDI r7, 1;
        LDI r6, 2500000;

    loop:
        ADD r0, r7;
        ADD r0, r7;
        ADD r0, r7;
        ADD r0, r7;
        DEC r6;
        TEST r6, r6;
        JNZ loop;

        END;
    """;

    const string text3a =
    """
        LDI r0, 0;
        LDI r1, 0;
        LDI r2, 0;
        LDI r3, 0;
        LDI r7, 1;
        LDI r6, 2500000;

    loop:
        ADD r0, r7;
        ADD r1, r7;
        ADD r2, r7;
        ADD r3, r7;
        DEC r6;
        TEST r6, r6;
        JNZ loop;

        END;
    """;

    const string text4 =
    """
        LDI r8, 100;
        LDI r0, 200000;
        MOV r7, rHP;
        ADD rHP, r0;
        MOV r1, r7;
        LDI r2, 0;
        LDI r4, 4;

    outer:
        MOV r1, r7;
        LDI r2, 0;
        LDI r6, 50000;

    fill:
        STORE_IND.S32 r2, r1;
        ADD r1, r4;
        INC r2;
        DEC r6;
        TEST r6, r6;
        JNZ fill;

        MOV r1, r7;
        LDI r0, 0;
        LDI r6, 50000;

    sum:
        LOAD_IND.S32 r5, r1;
        ADD r0, r5;
        ADD r1, r4;
        DEC r6;
        TEST r6, r6;
        JNZ sum;

        DEC r8;
        TEST r8, r8;
        JNZ outer;

    END;
    """;

    const string text4u =
    """
        LDI r8, 100;
        LDI r0, 200000;
        MOV r7, rHP;        // <-- замена ALLOC r7
        ADD rHP, r0;        // <-- сдвигаем heap pointer за блоком
        MOV r1, r7;
        LDI r2, 0;
        LDI r4, 4;

    outer:
        MOV r1, r7;
        LDI r2, 0;
        LDI r6, 50000;

    fill:
        STORE_IND_UNSAFE.S32 r2, r1;
        ADD r1, r4;
        INC r2;
        DEC r6;
        TEST r6, r6;
        JNZ fill;

        MOV r1, r7;
        LDI r0, 0;
        LDI r6, 50000;

    sum:
        LOAD_IND_UNSAFE.S32 r5, r1;
        ADD r0, r5;
        ADD r1, r4;
        DEC r6;
        TEST r6, r6;
        JNZ sum;

        DEC r8;
        TEST r8, r8;
        JNZ outer;

    END;
    """;

    const string text5 =
    """
        LDI r0, 0;
        LDI r6, 10000000;

    loop:
        PUSH r0;
        POP  r0;
        DEC  r6;
        TEST r6, r6;
        JNZ  loop;

        END;
    """;

    const string text6 =
    """
        LDI r0, 1;
        LDI r1, 1;
        LDI r6, 10000000;

    loop:
        SHR r0, r1;
        AND r2, r0;
        OR  r3, r0;
        XOR r4, r0;
        DEC r6;
        TEST r6, r6;
        JNZ loop;

        END;
    """;

    const string text7 =
    """
        LDI r0, 1;
        LDI r1, 3;
        LDI r6, 10000000;

    loop:
        MULT_INT r0, r1;
        DEC r6;
        TEST r6, r6;
        JNZ loop;

        END;
    """;

    const string text8 =
    """
        LDI r5, 1000000;
        LDI r6, 0;
        LDI r10, 1;

    loop:
        INC r6;
        TEST r6, r10;       // Z = (r6 & 1) == 0
        JZ  even;
    odd:
        INC r2;
        JMP next;
    even:
        INC r3;
    next:
        DEC r5;
        TEST r5, r5;
        JNZ loop;

    END;
    """;

    const string text3m =
    """
        LDI r0, 0;
        LDI r1, 0;
        LDI r2, 0;
        LDI r3, 0;
        LDI r7, 1;
        LDI r6, 2500000;

    loop:
        ADD r0, r7;
        SUB r1, r7;      // SUB с вычитанием 1 — уйдёт в минус, но нам важен тайминг
        AND r2, r7;      // r2 = r2 & 1 (остаётся 0 или 1)
        OR  r3, r7;      // r3 = r3 | 1 (всегда 1 после первого)
        DEC r6;
        TEST r6, r6;
        JNZ loop;

        END;
    """;

    const string text3m2 =
    """
        LDI r0, 0;
        LDI r1, 0;
        LDI r2, 0;
        LDI r3, 0;
        LDI r7, 1;
        LDI r6, 2500000;

    loop:
        ADD r0, r7;
        ADD r1, r7;
        ADD r2, r7;
        ADD r3, r7;
        DEC r6;
        TEST r6, r6;
        JNZ loop;

        END;
    """;


    private static readonly int _runs = 20;
    private static readonly int _runsWarmup = 5;

    private static void Main()
    {
        BenchmarkOptimizer.OptimizeCurrentProcess();

        using var host = VmHostFactory.CreateDefault();

        DeviceSpec deviceHandle = DeviceSpec.Create(ramSize: RamSize.MB64, name: "ConsoleVM");
        int deviceId = host.Emulator.RegisterDevice(
            spec: deviceHandle,
            sector: 0
        );

        var cases = new[]
        {
            new BenchCase("Memory",       text4,  Runs: _runs, WarmupRuns: _runsWarmup),
            new BenchCase("MemoryUnsafe", text4u, Runs: _runs, WarmupRuns: _runsWarmup),
            new BenchCase("Math",         text2,  Runs: _runs, WarmupRuns: _runsWarmup),
            new BenchCase("AluTput1",     text3,  Runs: _runs, WarmupRuns: _runsWarmup),
            new BenchCase("AluTput4",     text3a, Runs: _runs, WarmupRuns: _runsWarmup),
            new BenchCase("AluTputM1",    text3m, Runs: _runs, WarmupRuns: _runsWarmup),
            new BenchCase("AluTputM2",    text3m2,Runs: _runs, WarmupRuns: _runsWarmup),
            new BenchCase("Call",         text1,  Runs: _runs, WarmupRuns: _runsWarmup),
            new BenchCase("PushPop",      text5,  Runs: _runs, WarmupRuns: _runsWarmup),
            new BenchCase("BitOps",       text6,  Runs: _runs, WarmupRuns: _runsWarmup),
            new BenchCase("Mul",          text7,  Runs: _runs, WarmupRuns: _runsWarmup),
            new BenchCase("Branch",       text8,  Runs: _runs, WarmupRuns: _runsWarmup),
        };

        var sb = new StringBuilder();
        var runners = cases.Select(c => new BenchRunner(host, deviceId, c)).ToArray();

        // Общий план: сколько всего шагов (warm + run) по всем тестам
        int totalSteps = runners.Sum(r => r.Case.WarmupRuns + r.Case.Runs);
        int globalStep = 0;
        var totalSw = Stopwatch.StartNew();

        for (int ti = 0; ti < runners.Length; ti++)
        {
            var runner = runners[ti];
            string name = runner.Case.Name;
            int testIndex = ti + 1;
            int testTotal = runners.Length;
            int phaseTotal = runner.Case.WarmupRuns + runner.Case.Runs;
            int phaseStep = 0;

            PrintTestHeader(testIndex, testTotal, name, runner.Case.WarmupRuns, runner.Case.Runs, totalSw);

            if (!runner.TryPrepare())
            {
                sb.AppendLine($"Test '{name}': prepare failed");
                globalStep += phaseTotal;
                PrintProgress(testIndex, testTotal, name, "fail", 0, 0,
                              phaseTotal, phaseTotal, globalStep, totalSteps, forceNewLine: true, failed: true);
                continue;
            }

            // Warmup
            for (int i = 0; i < runner.Case.WarmupRuns; i++)
            {
                runner.Warmup();
                phaseStep++; globalStep++;
                PrintProgress(testIndex, testTotal, name, "warm", i + 1, runner.Case.WarmupRuns,
                              phaseStep, phaseTotal, globalStep, totalSteps, totalSw);
            }

            // Runs
            for (int i = 0; i < runner.Case.Runs; i++)
            {
                runner.RunOnce();
                phaseStep++; globalStep++;
                PrintProgress(testIndex, testTotal, name, "run", i + 1, runner.Case.Runs,
                              phaseStep, phaseTotal, globalStep, totalSteps, totalSw);
            }

            // Отмечаем тест как завершённый
            PrintProgress(testIndex, testTotal, name, "done", phaseTotal, phaseTotal,
                          phaseStep, phaseTotal, globalStep, totalSteps, totalSw, forceNewLine: true);

            sb.AppendLine($"Test '{name}':");
            sb.AppendLine();

            sb.AppendLine("  Elapsed (s) per iteration:");
            AppendIterations(sb, runner.ElapsedSecondsLog, "F4");
            sb.AppendLine();

            var elapsedStats = StatsResult.From(runner.ElapsedSecondsLog);
            var mipsStats = StatsResult.From(runner.MipsLogs);
            long totalInstr = runner.StepsOnce;

            sb.AppendLine("  Performance summary:");
            sb.AppendLine($"\t    Instructions / run:  {totalInstr:N0}");
            sb.AppendLine($"\t    Peak MIPS:           {runner.PeakMips,10:F2}   (TotalInstr / MinElapsed)");
            sb.AppendLine($"\t    Stable MIPS (trim10):{mipsStats.TrimmedMean10,10:F2}   (устойчиво к JIT/GC)");
            sb.AppendLine($"\t    Median MIPS:         {mipsStats.Median,10:F2}");
            sb.AppendLine($"\t    ns / instruction:    {(elapsedStats.Min * 1e9 / Math.Max(1, totalInstr)),10:F2}");
            sb.AppendLine($"\t    CV (MIPS):           {mipsStats.CV * 100,10:F2}%   (чем меньше — тем стабильнее)");
            sb.AppendLine();

            sb.AppendLine("  Elapsed stats (s):");
            AppendStatsExtended(sb, elapsedStats);
            sb.AppendLine();

            sb.AppendLine("  MIPS stats:");
            AppendStatsExtended(sb, mipsStats);
            sb.AppendLine();

            double firstHalf = elapsedStats.FirstHalfMean;
            double secondHalf = elapsedStats.SecondHalfMean;
            double jitImpact = firstHalf == 0 ? 0 : (firstHalf - secondHalf) / secondHalf * 100.0;
            sb.AppendLine($"  JIT/warmup impact (elapsed): {jitImpact:+0.0;-0.0;0}%  " +
                          $"(firstHalf={firstHalf:F4}, secondHalf={secondHalf:F4})");
            sb.AppendLine();

            sb.AppendLine("  Disassembly:");
            sb.AppendLine(runner.Program!.Value.DisassembleToString());
            sb.AppendLine();
        }

        totalSw.Stop();

        Console.Clear();
        var time = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var pathToDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        Directory.CreateDirectory(pathToDir);
        var path = Path.Combine(pathToDir, $"result_Benchmark_Logs_{time}.txt");
        var logRes = sb.ToString();
        File.WriteAllText(path, logRes);
        Console.WriteLine(logRes);
        Console.WriteLine($"Файл результатов логов сохранен по пути: \"{path}\"");
        Console.ReadLine();
    }

    // ───────── Прогресс ─────────

    private const int ProgressBarWidth = 24;

    private static void PrintTestHeader(int index, int total, string name, int warm, int runs,
                                        Stopwatch? totalSw = null)
    {
        if (Console.IsOutputRedirected) return;
        //try { Console.Title = $"[{index}/{total}] {name}"; } catch { /* ignore */ }

        string elapsed = totalSw is null ? "" : $" elapsed {FormatTime(totalSw.Elapsed)}";
        Console.WriteLine();
        Console.WriteLine($"=== [{index}/{total}] {name}  (warmup={warm}, runs={runs}){elapsed} ===");
    }

    private static void PrintProgress(
        int testIndex, int testTotal, string test,
        string phase, int done, int phaseTotal,
        int phaseStep, int phaseStepsTotal,
        int globalStep, int globalTotal,
        Stopwatch? totalSw = null,
        bool forceNewLine = false,
        bool failed = false)
    {
        if (Console.IsOutputRedirected) return;

        double globalPct = globalTotal <= 0 ? 1.0 : (double)globalStep / globalTotal;
        double testPct = phaseStepsTotal <= 0 ? 1.0 : (double)phaseStep / phaseStepsTotal;

        int filled = Math.Clamp((int)Math.Round(globalPct * ProgressBarWidth), 0, ProgressBarWidth);
        string bar = new string('▓', filled) + new string('░', ProgressBarWidth - filled);

        string marker = failed ? "✗" : (phase == "done" ? "✓" : "•");
        string phaseStr = phase == "done" || phase == "fail"
            ? $"{phase,-4}"
            : $"{phase,-4} {done,3}/{phaseTotal,-3}";

        string eta = "";
        if (totalSw is not null && globalStep > 0 && globalStep < globalTotal)
        {
            double avgPerStep = totalSw.Elapsed.TotalSeconds / globalStep;
            var remain = TimeSpan.FromSeconds(avgPerStep * (globalTotal - globalStep));
            eta = $" | ETA {FormatTime(remain)}";
        }

        string line =
            $"{marker} [{testIndex,2}/{testTotal,-2}] {test,-13} | {phaseStr} " +
            $"| test {testPct,6:P1} | total {globalStep,4}/{globalTotal,-4} {globalPct,6:P1} " +
            $"{bar}{eta}";

        int width = SafeWindowWidth();
        if (line.Length < width) line = line.PadRight(width);
        else if (line.Length > width) line = line[..width];

        Console.Write('\r');
        Console.Write(line);

        if (forceNewLine) Console.WriteLine();
    }

    private static string FormatTime(TimeSpan t)
    {
        if (t.TotalHours >= 1) return $"{(int)t.TotalHours}h{t.Minutes:D2}m{t.Seconds:D2}s";
        if (t.TotalMinutes >= 1) return $"{t.Minutes}m{t.Seconds:D2}s";
        return $"{t.TotalSeconds:F1}s";
    }

    private static int SafeWindowWidth()
    {
        try { return Math.Max(60, Console.WindowWidth); }
        catch { return 140; }
    }
    private static void AppendStatsExtended(StringBuilder sb, StatsResult s)
    {
        sb.AppendLine($"\t    mean:        {s.Mean,10:F4}");
        sb.AppendLine($"\t    trim 10%:    {s.TrimmedMean10,10:F4}");
        sb.AppendLine($"\t    trim 5%:     {s.TrimmedMean5,10:F4}");
        sb.AppendLine($"\t    median:      {s.Median,10:F4}");
        sb.AppendLine($"\t    gmean:       {s.GeometricMean,10:F4}");
        sb.AppendLine($"\t    min:         {s.Min,10:F4}");
        sb.AppendLine($"\t    p1:          {s.P1,10:F4}");
        sb.AppendLine($"\t    p5:          {s.P5,10:F4}");
        sb.AppendLine($"\t    p25:         {s.P25,10:F4}");
        sb.AppendLine($"\t    p75:         {s.P75,10:F4}");
        sb.AppendLine($"\t    p95:         {s.P95,10:F4}");
        sb.AppendLine($"\t    p99:         {s.P99,10:F4}");
        sb.AppendLine($"\t    max:         {s.Max,10:F4}");
        sb.AppendLine($"\t    range:       {s.Range,10:F4}");
        sb.AppendLine($"\t    IQR:         {s.IQR,10:F4}");
        sb.AppendLine($"\t    stddev:      {s.StdDev,10:F4}");
        sb.AppendLine($"\t    CV:          {s.CV * 100,10:F2}%");
        sb.AppendLine($"\t    skew:        {s.Skewness,10:F4}");
        sb.AppendLine($"\t    1st half:    {s.FirstHalfMean,10:F4}");
        sb.AppendLine($"\t    2nd half:    {s.SecondHalfMean,10:F4}");
        sb.AppendLine($"\t    N:           {s.N,10}");
    }


    private static void AppendIterations(StringBuilder sb, IReadOnlyList<double> logs, string format)
    {
        sb.AppendLine($"\tIterations:");
        if (logs.Count == 0)
        {
            sb.AppendLine("\t\t<нет данных>");
            return;
        }

        for (int i = 0; i < logs.Count; i++)
        {
            sb.Append("\t\t[")
              .Append((i + 1).ToString("D3"))
              .Append("] ")
              .AppendLine(logs[i].ToString(format, CultureInfo.InvariantCulture));
        }
    }
}
