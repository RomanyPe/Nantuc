# Nantuc.Benchmarks — README

> Console benchmark for the Nantuc virtual machine: measures MIPS (million instructions per second), stability, JIT/warmup impact, and statistics over a series of runs on typical workloads (memory, ALU, calls, stack, bit operations, multiplication, branches, recursion, pointers, structs, sorting) using **MiniC** sources.  
> Depends on `[Nantuc.VmApplication.Extensions](../../src/Nantuc.VmApplication.Extensions/README.md)` (transitively on `VmApplication`, `Compiler`, `Kernel`).  
> `[← Root](../README.md) · [VmApplication](../../src/Nantuc.VmApplication/README.md) · [Extensions](../../src/Nantuc.VmApplication.Extensions/README.md) · [Kernel](../../src/Nantuc.Kernel/README.md) · [Compiler](../../src/Nantuc.Compiler/README.md)`

## Contents
- [About this module](#about-this-module)
- [Local project tree](#local-project-tree)
- [API](#api)
- [Examples for the API](#examples-for-the-api)
- [Closeness to the user](#closeness-to-the-user)
- [Special notes](#special-notes)
- [What to fix](#what-to-fix)

## About this module

`Nantuc.Benchmarks` is not a library but a **console application** (`<OutputType>Exe</OutputType>`) that:

1. Prints a progress bar (`PrintProgress`) and writes a full log to `logs/result_Benchmark_Logs_YYYY-MM-DD_HH-mm-ss.txt`.
2. Creates a `VmHost` via `VmHostFactory.CreateDefault` and registers one `Device` (RAM 64 MiB) at `sector 0`.
3. Runs **14 fixed MiniC programs** (`internal static class SourceCasesMiniC` in `SourceCasesMiniC.cs`), each `WarmupRuns=5` + `Runs=20` times.
4. Collects MIPS, elapsed, and steps for each run, and computes statistics (`Stats`/`StatsResult`): mean, median, trimmed mean, percentiles, CV, skewness, JIT impact.

The module is an example of **how to use `VmApplication` for measurements**: it does not touch `Kernel` directly, only through `VmHost`/`DeviceContext`, and shows that the public API is sufficient for benchmarking without hacks.

Key roles:
- **`Program.cs`** — entry point + case array + output formatting.
- **`SourceCasesMiniC.cs`** — all 14 MiniC programs as `public const string ...Mc` (`callMc`, `mathMc`, `aluTput1Mc`, `aluTput4Mc`, `aluTputMixedMc`, `memoryMc`, `bitOpsMc`, `mulMc`, `branchMc`, `divModMc`, `recursionMc`, `pointerMc`, `structMc`, `sortMc`).
- **`BenchRunner`** — runner for one `BenchCase`: preparation (`TryPrepare`), warmup, `RunOnce`, log accumulation, and computation of `PeakMips`/`StepsOnce`/`StepsVariance`.
- **`BenchCase`** — `record` with name, program text, number of runs, and (unused) `ExpectedInstructions`.
- **`Stats`** / **`StatsResult`** — statistics: mean, median, percentiles, IQR, CV, trimmed mean, geometric mean, skewness, half-means.

## Local project tree

Namespace — `Nantuc.Benchmarks` (all files in one folder):

```
Nantuc.Benchmarks/
├── Nantuc.Benchmarks.csproj            # net10.0, Exe, ref → VmApplication.Extensions
├── Program.cs                          # Main + BenchCase[] array + output/progress
├── SourceCasesMiniC.cs                 # all 14 MiniC sources (callMc…sortMc)
├── BenchRunner.cs                      # BenchRunner + ListExtensions.ComputeVariance
├── BenchCase.cs                        # record BenchCase(Name, ProgramText, Runs, WarmupRuns, ExpectedInstructions?)
├── Stats.cs                            # static class Stats
└── StatsResult.cs                      # record struct StatsResult + From(...)
```

Organization notes:
- `BenchRunner.cs` contains both `BenchRunner` and a file-local `ListExtensions` (the `ComputeVariance` extension). This is inconvenient for navigation (grepping `ComputeVariance` leads to a file with a different name).
- `Program.cs` is no longer a “monolith with sources”: all MiniC programs have been moved to `SourceCasesMiniC.cs`. What remains is `Main` + `PrintTestHeader` / `PrintProgress` / `AppendStatsExtended` / `AppendIterations`. Further case expansion should be done via `SourceCasesMiniC` + an entry in the `cases` array.

## API

The project is an `Exe` and has no public API. Below is the internal structure useful for edits and extension.

### Entry point

`Program.Main()` (private static):

1. `VmHostFactory.CreateDefault()` → `host`.
2. `host.Emulator.RegisterDevice(DeviceSpec.Create(RamSize.MB64, "ConsoleVM"), sector: 0)` → `deviceId`.
3. `BenchCase[]` array (14 cases, `_runs=20`, `_runsWarmup=5`).
4. For each case: `new BenchRunner(host, deviceId, case)`, `TryPrepare()`, warmup, runs, statistics collection.
5. `File.WriteAllText` to `logs/`, `Console.WriteLine`, `Console.ReadLine()`.

Test programs (`public const string` in `SourceCasesMiniC.cs`):

| Constant | Case | What it measures |
|---|---|---|
| `callMc` | `Call` | Function call `sum(r0, r1)` in a loop, 3M iterations |
| `mathMc` | `Math` | `ADD` + `DEC` + loop, 3M iterations |
| `aluTput1Mc` | `AluTput1` | 4× `ADD` into one register (r0), 800K iterations |
| `aluTput4Mc` | `AluTput4` | 4× `ADD` into different registers (IRP), 800K iterations |
| `aluTputMixedMc` | `AluTputMixed` | `ADD`/`SUB`/`AND`/`OR` into different registers, 800K iterations |
| `memoryMc` | `Memory` | Filling `arr[25000]` + summation, 20× |
| `bitOpsMc` | `BitOps` | `SHR`/`AND`/`OR`/`XOR`, 3M iterations |
| `mulMc` | `Mul` | `MUL`, 3M iterations |
| `branchMc` | `Branch` | `if ((r6 & 1) != 0)`, 500K iterations |
| `divModMc` | `DivMod` | `DIV` + `MOD`, 500K iterations |
| `recursionMc` | `Recursion` | Recursion `down(500)` × 500 times |
| `pointerMc` | `Pointer` | Pointer arithmetic, read/write via `*(p + i)`, 1000 × 2000 |
| `structMc` | `Struct` | Accessing `struct Point` fields in an array, 1000 × 200 |
| `sortMc` | `Sort` | Bubble sort of `arr[200]`, 50× |

### `BenchRunner`

```csharp
public class BenchRunner(VmHost host, int deviceId, BenchCase benchCase)
{
    public BenchCase Case { get; }
    public IReadOnlyList<double> MipsLogs { get; }
    public IReadOnlyList<double> ElapsedSecondsLog { get; }
    public IReadOnlyList<long> StepsLogs { get; }
    public bool IsPrepared { get; private set; }
    public double PeakMips { get; }        // StepsOnce / Min(Elapsed) / 1e6
    public long   StepsOnce { get; }        // StepsLogs[0]
    public double StepsVariance { get; }    // variance of StepsLogs (extension)
    public CompilationResult? Program { get; private set; }

    public bool TryPrepare();        // CompileToIR(SourceLanguage.C, optimize: true) → TryLoadProgram
    public void RunOnce();           // Launch(MipsLogs, ElapsedLog, StepsLogs, ctx)
    public void Warmup();            // Launch([], [], [], ctx) — log discarded
    public static void Launch(List<double> mips, List<double> elapsed,
                              List<long> steps, DeviceContext? ctx);
}
```

`TryPrepare` uses `_host.Project.CompileToIR(_case.ProgramText, ProjectBuilder.BaseAddressProgram, optimize: true, SourceLanguage.C).Build()`. Loading into the device is done via `ctx.TryLoadProgram(res.Program, 0, out string? errorMath)`. The IR optimizer is not applied (see [Special notes](#special-notes)).

`Launch` runs `ctx.Run(new LaunchOptions(ShowTimer: true, OnEnd: ...))`. In `OnEnd`, `Mips = Steps / Elapsed.TotalSeconds / 1e6` is computed and added to three lists.

### `BenchCase`

```csharp
public record BenchCase(
    string Name,
    string ProgramText,
    int Runs,
    int WarmupRuns,
    long? ExpectedInstructions = null);
```

`ExpectedInstructions` is **not used** in either `BenchRunner` or `Program.cs`. It is a placeholder for the future (checking actual steps against expected ones). See [What to fix, item 5](#what-to-fix).

### Statistics

```csharp
public static class Stats
{
    public static double Mean(IReadOnlyList<double> v);
    public static double StdDev(IReadOnlyList<double> v);
    public static double Percentile(IReadOnlyList<double> v, double p);
    public static double Median(IReadOnlyList<double> v);
    public static double Min(Max/Sum/Range/IQR)(IReadOnlyList<double> v);
    public static double CV(IReadOnlyList<double> v);                   // StdDev / Mean
    public static double TrimmedMean(IReadOnlyList<double> v, double trimPercent);
    public static double GeometricMean(IReadOnlyList<double> v);
    public static double Skewness(IReadOnlyList<double> v);
    public static double FirstHalfMean(SecondHalfMean)(IReadOnlyList<double> v);
}

public record struct StatsResult(
    double Mean, double Median, double Min, double Max,
    double P1, double P5, double P25, double P75, double P95, double P99,
    double StdDev, double CV, double IQR, double Range,
    double TrimmedMean5, double TrimmedMean10,
    double GeometricMean, double Skewness,
    double FirstHalfMean, double SecondHalfMean, int N)
{
    public static StatsResult From(IReadOnlyList<double> v);
}
```

- `Percentile` — linear interpolation (rank-based, `rank = p/100 * (n-1)`), works for n=1 too (returns the single element).
- `TrimmedMean` — discards `floor(n * trim/100)` from each end; returns `Median` if nothing remains after trimming.
- `GeometricMean` — ignores `x <= 0` (protection against `log(0) = -inf`).
- `Skewness` — sample skewness with the `n / ((n-1)(n-2))` correction.
- `FirstHalfMean`/`SecondHalfMean` — for JIT impact: the difference between the first and second halves of runs.

## Examples for the API

**Add your own case via `SourceCasesMiniC`:**

```csharp
// SourceCasesMiniC.cs
public const string myNewMc =
"""
int main() {
    int r0 = 0;
    int r1 = 1000000;
    while (r1 != 0) {
        r0 = r0 ^ r0;
        r1 = r1 - 1;
    }
    return r0;
}
""";

// Program.cs, in the cases array:
new BenchCase("XorSelf", SourceCasesMiniC.myNewMc, Runs: _runs, WarmupRuns: _runsWarmup)
```

**Use `BenchRunner` in your own code (not through `Program.Main`):**

```csharp
using var host = VmHostFactory.CreateDefault();
int deviceId = host.Emulator.RegisterDevice(
    DeviceSpec.Create(RamSize.MB64, "Bench"), sector: 0);

var runner = new BenchRunner(host, deviceId,
    new BenchCase("MyCase", SourceCasesMiniC.myNewMc, Runs: 10, WarmupRuns: 3));

if (!runner.TryPrepare()) return;

for (int i = 0; i < 3; i++) runner.Warmup();
for (int i = 0; i < 10; i++) runner.RunOnce();

var mips = StatsResult.From(runner.MipsLogs);
Console.WriteLine($"Peak MIPS: {runner.PeakMips:F2}");
Console.WriteLine($"Median MIPS: {mips.Median:F2}");
Console.WriteLine($"Steps once: {runner.StepsOnce:N0}");
```

**Compute statistics without the VM:**

```csharp
var samples = new[] { 100.0, 102.0, 99.0, 101.0, 103.0, 98.0, 500.0 };
var s = StatsResult.From(samples);
Console.WriteLine($"Median={s.Median:F2} Trim10={s.TrimmedMean10:F2} CV={s.CV*100:F2}%");
// Median=101.00 Trim10=101.00 CV=95.11% — the 500 outlier does not affect median/trim
```

**Disassemble the case program:**

```csharp
if (runner.Program is { } comp)
    Console.WriteLine(comp.DisassembleToString());
```

## Closeness to the user

**Medium.** This is not a library but a “product” — an example of usage and a tool for evaluating VM performance. End users do not need a MiniC benchmark; core/compiler authors find it useful for regressions.

**What you should do:**
- Run it on an **isolated machine** (or at least without load) — otherwise affinity/priority will not help.
- On Linux, `PriorityClass.High` requires `sudo`; otherwise you will get a `[Warning]` and normal priority.
- Read logs from `logs/` — the console may truncate output (`PrintProgress` writes to a single line with `\r`).
- Extend the case set via `BenchCase` + `SourceCasesMiniC`, not by forking `BenchRunner`.
- To compare “with optimizations vs without”, add an `optimize` flag to `BenchCase` and pass it through to `BenchRunner.TryPrepare` instead of the hardcoded `true`.

**What you should not do:**
- Run the benchmark in CI/headless — `Console.ReadLine()` at the end of `Main` will block the process, and `Console.Clear()` may throw.
- Compare MIPS across machines — core affinity, frequency, JIT, GC, and thermal throttling have a strong impact. Compare only within one session.
- Expect `optimize: true` in `BenchRunner.TryPrepare` to have the same effect as AST optimizations + IR for ASM: for MiniC, `optimize: true` enables AST optimizations, but IR (`VmIROptimizer.AddStdRules`) is not called automatically.
- Use `PeakMips` as the only metric — it takes `Min(Elapsed)`, which is sensitive to outliers. `TrimmedMean10` from `MipsLogs` is more reliable.

## Special notes

### `optimize: true` for MiniC

`BenchRunner.TryPrepare` calls `_host.Project.CompileToIR(_case.ProgramText, ProjectBuilder.BaseAddressProgram, optimize: true, SourceLanguage.C).Build()`. For MiniC sources, `optimize: true` enables compiler AST optimizations but **does not** enable IR: nobody calls `VmIROptimizer.AddStdRules`, and `OptimizationIR(..., IROptimizationCount)` is not used in `BenchRunner`/`Program`. Therefore, all 14 cases are AST-optimized but not IR-optimized code.

If you want to compare “with IR vs without”, you need to do it explicitly:

```csharp
host.Project.Optimizator.VMIROptimizer.AddStdRules();
var il = host.Project.CompileToIR(text, ProjectBuilder.BaseAddressProgram, optimize: false, SourceLanguage.C);
il = host.Project.OptimizationIR(il, IROptimizationCount: 5);
var comp = il.Build();
```

### `Stats` — why several metrics

- `Mean` — sensitive to outliers (GC pauses, thermal throttling).
- `Median` — robust, but does not show the “average” scenario.
- `TrimmedMean10` — a compromise: discards 10% from the top and bottom, ignores outliers, and shows a typical value. **The primary metric** for reporting.
- `CV` — coefficient of variation; `CV < 5%` is stable, `CV > 15%` is noisy.
- `Skewness > 0` — right tail (outliers are “slow runs”); `< 0` — left tail.
- `FirstHalfMean`/`SecondHalfMean` — JIT/warmup: if `firstHalf > secondHalf` by 20%+, JIT has not warmed up by the end of warmup and `WarmupRuns` should be increased.

### Output and log format

`Program.cs` uses:
- `PrintProgress` with `\r` and ANSI characters `▓░✓✗•` — in CI/redirected output, `Console.IsOutputRedirected` skips the progress bar, but `Console.Clear()` at the end of `Main` does **not** skip it (see [What to fix, item 4](#what-to-fix)).
- `AppendStatsExtended` — all 20 fields of `StatsResult` in the format `\t    name: value`.
- `AppendIterations` — one value per line, format `F4`.

Log file: `logs/result_Benchmark_Logs_{yyyy-MM-dd_HH-mm-ss}.txt` in `AppDomain.CurrentDomain.BaseDirectory`. It contains **everything**: from progress to the full disassembly of each program.

### Platform limitations

- **`Console.ReadLine()`** at the end of `Main` blocks. In CI/headless, the process will hang. See [What to fix, item 4](#what-to-fix).

### What this module does not do

- It is not a library — it is an `Exe`.
- It does not publish results to external storage (BenchmarkDotNet-style baseline) — only `logs/*.txt`.
- It does not compare Nantuc versions with each other — it only measures the current one.
- It does not use `BenchmarkDotNet` — everything is manual so that `LaunchOptions(ShowTimer: true)` can be controlled and there is no dependency on `VmHost` inside a third-party framework.
- It does not test multithreading — all cases are single-threaded, with one `Device` for everything.

## What to fix

In descending order of severity. File/method references.

1. **`BenchRunner.TryPrepare` — `Console.ReadLine()` in failure paths blocks.**  
   File: `BenchRunner.cs`, `TryPrepare`, two `Console.ReadLine()` calls after `Console.WriteLine`.  
   Same risk as in `Main`. In `Program.Main`, failure is handled via `PrintProgress(..., failed: true)` and `continue`, while `BenchRunner` blocks on its own — inconsistent.  
   Fix: remove `Console.ReadLine()` from `TryPrepare`, return `false`, and let the caller decide (handling already exists in `Main`).

2. **`BenchRunner.PeakMips`/`StepsOnce` — uses `StepsLogs[0]`, not the minimum/median.**  
   File: `BenchRunner.cs`, properties `PeakMips` and `StepsOnce`.  
   `StepsOnce = _stepsLogs[0]` — if the first run behaved differently (for example, GC between `TryPrepare` and the first `RunOnce`), the step count may differ from later runs. `PeakMips = StepsOnce / Min(Elapsed)` mixes steps from one run with time from another.  
   Fix: `StepsOnce` → `_stepsLogs.Min()` (or `Median`), `PeakMips` → `_stepsLogs.Zip(_elapsedSecondsLogs).Max(p => p.First / p.Second / 1e6)`.

3. **`BenchRunner.Warmup`/`RunOnce` — warmup writes to empty lists.**  
   File: `BenchRunner.cs`, methods `Warmup` and `RunOnce`.  
   `Warmup` calls `Launch([], [], [], ctx)` — `Metric` adds an entry to empty lists that are immediately discarded. Allocation per warmup run (3 lists) is minor but ugly.  
   Fix: `Launch` with `onMetric: null` — do not add if the lists are empty, or provide a separate overload without lists.

4. **`Program.Main` — `Console.Clear()` without `try/catch`.**  
   File: `Program.cs`, `Main`: `Console.Clear();` after `totalSw.Stop()`.  
   On headless (redirected output, CI, some terminals) it throws `IOException`. The benchmark has already been computed, but the log will not be saved.  
   Fix: `try { Console.Clear(); } catch (IOException) { /* ignore */ }`.

5. **`BenchCase.ExpectedInstructions` is not used.**  
   File: `BenchCase.cs` (field) + `Program.cs` (all `new BenchCase(...)` calls omit it) + `BenchRunner` (does not read it).  
   The field exists, but `BenchRunner` does not compare actual `StepsLogs` against expected values. If the VM produces an anomalous step count (for example, due to a bug in `Processor.Step`), the benchmark will not notice — MIPS will be “correct”, but steps will not.  
   Fix: in `BenchRunner.RunOnce` after a run: `if (_case.ExpectedInstructions is { } expected && steps[^1] != expected) Console.Error.WriteLine($"Steps mismatch: expected {expected}, got {steps[^1]}");`. Fill in `ExpectedInstructions` for all cases (values can be taken from a reference run).

6. **`Program.cs` — `PrintProgress` for `fail` passes `done=0, phaseTotal=0`.**  
   File: `Program.cs`, `Main`, branch `if (!runner.TryPrepare())`:
   ```csharp
   PrintProgress(testIndex, testTotal, name, "fail", 0, 0,
                 phaseTotal, phaseTotal, globalStep, totalSteps, forceNewLine: true, failed: true);
   ```
   `done=0, phaseTotal=0` — not used in `phaseStr` (`phase == "fail"` → `$"{phase,-4}"`), but in `testPct = phaseStepsTotal <= 0 ? 1.0 : ...` — `phaseStepsTotal = phaseTotal > 0`, so the percentage is computed as `phaseStep/phaseTotal = 1.0`. Not a bug, but the “0, 0” parameters are misleading — either pass `phaseTotal, phaseTotal` or add a comment.

7. **`Stats.Percentile`/`Median`/`TrimmedMean` — `OrderBy().ToArray()` on every call.**  
   File: `Stats.cs`, methods `Percentile`, `Median`, `TrimmedMean`.  
   `StatsResult.From` calls `Percentile` 10 times, `Median` once, and `TrimmedMean` twice — 13 sorts of the same array in total. For `n=20` this is unnoticeable, but for `n=1000` (if you increase `Runs`) it becomes noticeable.  
   Fix: sort once in `StatsResult.From` and pass a `ReadOnlySpan<double>`/sorted array. Or cache it in a local variable inside `From`.

8. **`BenchRunner.cs` — `ListExtensions` in the same file.**  
   File: `BenchRunner.cs`, `file static class ListExtensions`.  
   `ComputeVariance` is the only extension in the project and sits next to `BenchRunner`. This is inconvenient for navigation.  
   Fix: move it to `Stats.cs` as `public static double Variance(IReadOnlyList<double> v)` and do not make it an extension (an extension on `IReadOnlyList<double>` is questionable practice and conflicts with `System.Linq`).

9. **`Program.cs` — `Console.WriteLine($"File results...")` with Cyrillic and `Console.ReadLine()`.**  
   File: `Program.cs`, end of `Main`.  
   In a Windows console with code page 866/1251, Cyrillic may appear as garbled characters if `Console.OutputEncoding = Encoding.UTF8` is not set.  
   Fix: set `Console.OutputEncoding = System.Text.Encoding.UTF8;` at the beginning of `Main` (or `Console.OutputEncoding = new UTF8Encoding(false)` for CI).

10. **`BenchRunner.TryPrepare` — `Program = res` before `TryLoadProgram`.**  
    File: `BenchRunner.cs`, `TryPrepare`:
    ```csharp
    var ctx = _host.Emulator.GetDeviceContext(_deviceId)!;
    Program = res;
    if (!ctx.TryLoadProgram(res.Program, 0, out string? errorMath)) { ... return false; }
    ```
    If loading fails, `Program` is already set while `IsPrepared = false`. If the calling code looks at `Program`, it will see a “successful” compilation even though the program was not loaded.  
    Fix: set `Program = res` after successful loading. Or document that `Program` is the compilation result, not the load result.

11. **`BenchRunner.TryPrepare` — hardcoded `optimize: true` and no IR.**  
    File: `BenchRunner.cs`, `TryPrepare` + `BenchCase.cs`.  
    `optimize: true` is hardcoded, and `BenchCase` does not allow comparison with `optimize: false`. In addition, IR is not called (see [Special notes](#special-notes)).  
    Fix: add an `OptimizeAst` flag to `BenchCase` (default `true`) and, optionally, `RunIR` — so that the effect of optimizations can be measured, not just a “raw” run.

---