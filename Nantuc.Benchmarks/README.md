# Nantuc.Benchmarks — README

> Console benchmark for the Nantuc virtual machine: measures MIPS (million instructions per second), stability, JIT/warmup impact, and statistics over a series of runs on typical workloads (memory, ALU, calls, stack, bitwise operations, multiplication, branches).  
> Depends on `[Nantuc.VmApplication.Extensions](../Nantuc.VmApplication.Extensions/README.md)` (transitively — on `VmApplication`, `Compiler`, `Kernel`).  
> `[← Root](../README.md) · [VmApplication](../Nantuc.VmApplication/README.md) · [Extensions](../Nantuc.VmApplication.Extensions/README.md) · [Kernel](../Nantuc.Kernel/README.md) · [Compiler](../Nantuc.Compiler/README.md)`

## Table of contents
- [About this module](#about-this-module)
- [Local project tree](#local-project-tree)
- [API](#api)
- [API examples](#api-examples)
- [How close the module is to the user](#how-close-the-module-is-to-the-user)
- [Special information](#special-information)
- [What to fix](#what-to-fix)

## About this module

`Nantuc.Benchmarks` is not a library but a **console application** (`<OutputType>Exe</OutputType>`) that:

1. Prints a progress bar (`PrintProgress`) and writes a full log to `logs/result_Benchmark_Logs_YYYY-MM-DD_HH-mm-ss.txt`.
2. Creates a `VmHost` via `VmHostFactory.CreateDefault`, registers one `Device` (RAM 64 MiB) on `sector 0`.
3. Runs 12 fixed NANASM programs (text constants `text1`…`text8` in `Program.cs`), each `WarmupRuns=5` + `Runs=20` times.
4. Collects MIPS, elapsed, steps per run, computes statistics (`Stats`/`StatsResult`): mean, median, trimmed mean, percentiles, CV, skewness, JIT-impact.

The module is an example of **how to use `VmApplication` for measurements**: it does not touch `Kernel` directly, only through `VmHost`/`DeviceContext`, and shows that the public API is sufficient for a benchmark without hacks.

Key roles:
- **`Program.cs`** — entry point + all test programs (`text*`) + output formatting.
- **`BenchRunner`** — runner for a single `BenchCase`: preparation (`TryPrepare`), warmup, `RunOnce`, log accumulation, and computation of `PeakMips`/`StepsOnce`/`StepsVariance`.
- **`BenchCase`** — `record` with name, program text, run counts, and (unused) `ExpectedInstructions`.
- **`Stats`** / **`StatsResult`** — statistics: mean, median, percentiles, IQR, CV, trimmed mean, geometric mean, skewness, half-means.

## Local project tree

Namespace — `Nantuc.Benchmarks` (all files in one folder):

```
Nantuc.Benchmarks/
├── Nantuc.Benchmarks.csproj            # net10.0, Exe, ref → VmApplication.Extensions
├── Program.cs                          # Main + test programs text1..text8 + output
├── BenchRunner.cs                      # BenchRunner + ListExtensions.ComputeVariance
├── BenchCase.cs                        # record BenchCase(Name, ProgramText, Runs, WarmupRuns, ExpectedInstructions?)
├── Stats.cs                            # static class Stats
└── StatsResult.cs                      # record struct StatsResult + From(...)
```

Organization notes:
- `BenchRunner.cs` contains both `BenchRunner` and a file-local `ListExtensions` (the `ComputeVariance` extension). Inconvenient for navigation (grepping `ComputeVariance` leads to a file with a different name).
- `Program.cs` is a monolith: 350+ lines with `text*` constants, `Main`, `PrintTestHeader`, `PrintProgress`, `AppendStatsExtended`, `AppendIterations`. If you plan to expand the case set, consider moving test programs to `Cases/` or a separate `BenchPrograms.cs`.

## API

The project is an `Exe`; there is no public API. Below is the internal structure useful for edits and extensions.

### Entry point

`Program.Main()` (private static):

1. `VmHostFactory.CreateDefault()` → `host`.
2. `host.Emulator.RegisterDevice(DeviceSpec.Create(RamSize.MB64, "ConsoleVM"), sector: 0)` → `deviceId`.
3. `BenchCase[]` array (12 cases).
4. For each case: `new BenchRunner(host, deviceId, case)`, `TryPrepare()`, warmup, runs, statistics collection.
5. `File.WriteAllText` to `logs/`, `Console.WriteLine`, `Console.ReadLine()`.

Test programs (`const string` in `Program.cs`):

| Constant | Case | What it measures |
|---|---|---|
| `text1` | `Call` | `CALL`/`RET` in a loop (10M iterations, `CALL Sum; ADD; RET`) |
| `text2` | `Math` | `ADD` + `DEC` + `TEST` + `JNZ` (10M iterations) |
| `text3` | `AluTput1` | 4× `ADD` into one register + loop (2.5M iterations) |
| `text3a` | `AluTput4` | 4× `ADD` into different registers (ILP) + loop |
| `text3m` | `AluTputM1` | `ADD`/`SUB`/`AND`/`OR` into one register |
| `text3m2` | `AluTputM2` | 4× `ADD` into different registers (shape duplicate of `text3a`) |
| `text4` | `Memory` | `STORE_IND.S32` + `LOAD_IND.S32` (safe), 100×50000 |
| `text4u` | `MemoryUnsafe` | `STORE_IND_UNSAFE.S32` + `LOAD_IND_UNSAFE.S32`, 100×50000 |
| `text5` | `PushPop` | `PUSH r0; POP r0` in a loop (10M) |
| `text6` | `BitOps` | `SHR`/`AND`/`OR`/`XOR` (10M) |
| `text7` | `Mul` | `MULT_INT` (10M) |
| `text8` | `Branch` | `TEST`/`JZ`/`JMP` (branches, 1M iterations, ~6 instructions per iteration) |

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

    public bool TryPrepare();        // CompileToIL(ASM, optimize:true) → TryFastLoadProgram
    public void RunOnce();           // Launch(MipsLogs, ElapsedLog, StepsLogs, ctx)
    public void Warmup();            // Launch([], [], [], ctx) — log discarded
    public static void Launch(List<double> mips, List<double> elapsed,
                              List<long> steps, DeviceContext? ctx);
}
```

`TryPrepare` uses `_host.Project.CompileToIL(text, 0, optimize: true, SourceLanguage.Asm).Build()`. For ASM, `optimize: true` does nothing — `ProjectBuilder.Build` assembles asm files directly (`AsmParser.Assemble`), without AST/peephole. See [Special information](#special-information).

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

`ExpectedInstructions` is **not used** in either `BenchRunner` or `Program.cs`. It is a placeholder for the future (comparing actual steps with expected). See [What to fix, item 9](#what-to-fix).

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

- `Percentile` — linear interpolation (rank-based, `rank = p/100 * (n-1)`), works for n=1 (returns the single element).
- `TrimmedMean` — discards `floor(n * trim/100)` from each end, returns `Median` if empty after discarding.
- `GeometricMean` — ignores `x <= 0` (protection against `log(0) = -inf`).
- `Skewness` — sample skewness with correction `n / ((n-1)(n-2))`.
- `FirstHalfMean`/`SecondHalfMean` — for JIT-impact: the difference between the first and second half of the runs.

## API examples

**Add your own case to `Program.Main`:**

```csharp
const string textNew =
    """
    LDI r0, 0;
    LDI r6, 1000000;
loop:
    XOR r0, r0;
    DEC r6;
    TEST r6, r6;
    JNZ loop;
    END;
    """;

// in the cases array:
new BenchCase("XorSelf", textNew, Runs: 20, WarmupRuns: 5)
```

**Use `BenchRunner` in your own code (not via `Program.Main`):**

```csharp
using var host = VmHostFactory.CreateDefault();
int deviceId = host.Emulator.RegisterDevice(
    DeviceSpec.Create(RamSize.MB64, "Bench"), sector: 0);

var runner = new BenchRunner(host, deviceId,
    new BenchCase("MyCase", textNew, Runs: 10, WarmupRuns: 3));

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

## How close the module is to the user

**Medium.** It is not a library but a "product" — an example of usage and a tool for evaluating VM performance. The end user of MiniC/NANASM does not need the benchmark; the kernel/compiler author finds it useful for regressions.

**What you should do:**
- Run on an **isolated machine** (or at least without load) — otherwise affinity/priority will not help.
- On Linux, `sudo` is needed for `PriorityClass.High`, otherwise you will get a `[Warning]` and normal priority.
- Read logs from `logs/` — the console may truncate output (`PrintProgress` writes to a single line with `\r`).
- Expand the case set via `BenchCase` + `text*` in `Program.cs`, not by forking `BenchRunner`.
- To compare "safe vs unsafe" memory — `Memory` vs `MemoryUnsafe` already exists. To compare "with optimizations vs without" — add `optimize: false` in `BenchRunner.TryPrepare` or a separate flag in `BenchCase`.

**What you should not do:**
- Run the benchmark in CI/headless — `Console.ReadLine()` at the end of `Main` will block the process, and `Console.Clear()` may throw.
- Compare MIPS between machines — core binding, frequency, JIT, GC, thermal throttling have a strong influence. Compare only within one session.
- Expect `optimize: true` in `BenchRunner.TryPrepare` to do anything for ASM programs — it won't (see below).
- Use `PeakMips` as the only metric — it takes `Min(Elapsed)`, which is sensitive to outliers. `TrimmedMean10` from `MipsLogs` is more reliable.

## Special information

### `optimize: true` — no-op for ASM

`BenchRunner.TryPrepare` calls `_host.Project.CompileToIL(_case.ProgramText, ..., optimize: true, SourceLanguage.Asm).Build()`. In `ProjectBuilder.Build`, ASM files are processed as follows:

```csharp
foreach (SourceFile file in files.Where(file => file.Language == SourceLanguage.Asm))
{
    asmParser.Assemble(file.Content, assembler);   // no AST, no peephole
}
```

`optimize: true` enables only AST optimizations (which do not apply to ASM) and **does not** enable peephole (`VmPeepholeOptimizer.AddStdRules` is never called). So all 12 cases are exactly what you wrote in `text*`, without optimizations.

If you want to compare "with optimizations vs without", you need to do it explicitly:

```csharp
host.Project.Optimizator.VMPeepholeOptimizer.AddStdRules();
var il = host.Project.CompileToIL(text, 0, optimize: false, SourceLanguage.Asm);
il = host.Project.OptimizationIL(il, peepholeOptimizationCount: 5);
var comp = il.Build();
```

### `text4` vs `text4u` — safe vs unsafe

Both cases use the same pattern `MOV r7, rHP; ADD rHP, r0;` (manual bump allocator), but:

- `text4` — `STORE_IND.S32` / `LOAD_IND.S32` (with bounds and alignment checks in `Processor.Step`).
- `text4u` — `STORE_IND_UNSAFE.S32` / `LOAD_IND_UNSAFE.S32` (no checks).

The comment in `text4u` (`// <-- replacement for ALLOC r7`) is historical: there used to be an `ALLOC` opcode in the language, now there isn't, and allocation is done manually. This does not affect the current benchmark.

Expected difference: `MemoryUnsafe` should be 20-40% faster because it skips `AlignmentFault` checks and `_ram.UpdateAndReadStatus()`. If the difference is < 10%, then either `Processor` is not on the hot path, or JIT does not inline `_ram.ReadInt32LEUnsafe` (see `[Nantuc.Kernel](../Nantuc.Kernel/README.md)`, `[MethodImpl(AggressiveOptimization)]` on `Step`).

### `Stats` — why several metrics

- `Mean` — sensitive to outliers (GC pauses, thermal throttling).
- `Median` — robust, but does not show the "average" scenario.
- `TrimmedMean10` — a compromise: discards 10% from the top and bottom, ignores outliers, shows the typical value. **The primary metric** for the report.
- `CV` — coefficient of variation; `CV < 5%` — stable, `CV > 15%` — noisy.
- `Skewness > 0` — right tail (outliers are "slow runs"); `< 0` — left tail.
- `FirstHalfMean`/`SecondHalfMean` — JIT/warmup: if `firstHalf > secondHalf` by 20%+, JIT has not warmed up by the end of warmup; increase `WarmupRuns`.

### Output and log format

`Program.cs` uses:
- `PrintProgress` with `\r` and ANSI characters `▓░✓✗•` — in CI/redirected output `Console.IsOutputRedirected` skips the progress bar, but `Console.Clear()` at the end of `Main` — **does not** (see [What to fix, item 3](#what-to-fix)).
- `AppendStatsExtended` — all 20 fields of `StatsResult` in the format `\t    name: value`.
- `AppendIterations` — one value per line, format `F4`.

Log file: `logs/result_Benchmark_Logs_{yyyy-MM-dd_HH-mm-ss}.txt` in `AppDomain.CurrentDomain.BaseDirectory`. Contains **everything**: from progress to the full disassembly of each program.

### Platform limitations

- **`Console.ReadLine()`** at the end of `Main` — blocks. In CI/headless the process will hang. See [What to fix, item 3](#what-to-fix).

### What this module does not do

- It is not a library — it is an `Exe`.
- It does not publish results to external storage (BenchmarkDotNet-style baseline) — only `logs/*.txt`.
- It does not compare Nantuc versions with each other — it only measures the current one.
- It does not use `BenchmarkDotNet` — everything is manual to control `LaunchOptions(ShowTimer: true)` and not depend on `VmHost` in someone else's framework.
- It does not test multithreading — all cases are single-threaded, one `Device` for everything.

## What to fix

In descending order of severity. References to files/methods.

3. **`Program.Main` — `Console.ReadLine()` at the end blocks CI/headless.**  
   File: `Program.cs`, end of `Main` (`Console.WriteLine($"Results file..."); Console.ReadLine();`).  
   Plus `Console.Clear()` before saving — on headless it may throw `IOException`.  
   Fix: `if (!Console.IsInputRedirected && !Console.IsOutputRedirected) Console.ReadLine();` and `try { Console.Clear(); } catch (IOException) { }`. Similarly for `Console.Clear()` at the beginning.

4. **`BenchRunner.TryPrepare` — `Console.WriteLine(res.Errors)` prints the type name, not the errors.**  
   File: `BenchRunner.cs`, `TryPrepare`: `Console.WriteLine(res.Errors);`.  
   `IReadOnlyList<string>` in `Console.WriteLine` outputs `System.Collections.Generic.List...`. The errors are not visible.  
   Fix: `foreach (var e in res.Errors ?? []) Console.WriteLine(e);` or `Console.WriteLine(string.Join(Environment.NewLine, res.Errors ?? []))`.

5. **`BenchRunner.TryPrepare` — `Console.ReadLine()` in failure paths blocks.**  
   File: `BenchRunner.cs`, `TryPrepare`, two `Console.ReadLine()` after `Console.WriteLine`.  
   The same risk as in `Main`. In `Program.Main`, failure is handled via `PrintProgress(..., failed: true)` and `continue`, but `BenchRunner` blocks itself — inconsistent.  
   Fix: remove `Console.ReadLine()` from `TryPrepare`, return `false`, and let the caller decide (in `Main` handling already exists).

6. **`BenchRunner.PeakMips`/`StepsOnce` — takes `StepsLogs[0]` instead of min/median.**  
   File: `BenchRunner.cs`, properties `PeakMips` and `StepsOnce`.  
   `StepsOnce = _stepsLogs[0]` — if the first run had different behavior (e.g., GC between `TryPrepare` and the first `RunOnce`), the step count may differ from subsequent runs. `PeakMips = StepsOnce / Min(Elapsed)` mixes steps from one run with time from another.  
   Fix: `StepsOnce` → `_stepsLogs.Min()` (or `Median`), `PeakMips` → `_stepsLogs.Zip(_elapsedSecondsLogs).Max(p => p.First / p.Second / 1e6)`.

7. **`BenchRunner.Warmup`/`RunOnce` — warmup writes to empty lists.**  
   File: `BenchRunner.cs`, methods `Warmup` and `RunOnce`.  
   `Warmup` calls `Launch([], [], [], ctx)` — `Metric` adds a record to empty lists that are immediately discarded. Allocation per warmup run (3 lists) is a minor issue but ugly.  
   Fix: `Launch` with `onMetric: null` — do not add if the lists are empty, or a separate overload without lists.

8. **`Program.Main` — `Console.Clear()` without `try/catch`.**  
   File: `Program.cs`, `Main`: `Console.Clear();` after `totalSw.Stop()`.  
   On headless (redirected output, CI, some terminals) it throws `IOException`. The whole benchmark has already been computed, but the log will not be saved.  
   Fix: `try { Console.Clear(); } catch (IOException) { /* ignore */ }`.

9. **`BenchCase.ExpectedInstructions` is not used.**  
   File: `BenchCase.cs` (field) + `Program.cs` (all `new BenchCase(...)` without it) + `BenchRunner` (does not read it).  
   The field exists, but `BenchRunner` does not compare actual `StepsLogs` with expected. If the VM produces an anomalous number of steps (e.g., due to a bug in `Processor.Step`), the benchmark will not notice — MIPS will be "correct", but the steps will not.  
   Fix: in `BenchRunner.RunOnce` after the run: `if (_case.ExpectedInstructions is { } expected && steps[^1] != expected) Console.Error.WriteLine($"Steps mismatch: expected {expected}, got {steps[^1]}");`. Fill in `ExpectedInstructions` for all cases (values can be taken from a reference run).

10. **`Program.cs` — test programs `text*` in the same file as `Main`.**  
    File: `Program.cs`, `const string text1`…`text8` (350+ lines).  
    A monolith, hard to navigate, hard to extend.  
    Fix: move to `Cases/BenchPrograms.cs` (`internal static class BenchPrograms { public const string Call = """..."""; ... }`) or a `BenchCase` factory with a `switch`. Then `Program.Main` becomes shorter and cleaner.

11. **`Program.cs` — `text3a` and `text3m2` are semantically equivalent.**  
    File: `Program.cs`, `const string text3a` and `const string text3m2`.  
    Both are 4× `ADD` into different registers (r0/r1/r2/r3), r7=1, loop with `DEC r6; TEST r6, r6; JNZ loop`. The only difference is the order of `LDI`. The `AluTput4` and `AluTputM2` cases will show identical MIPS.  
    Fix: either replace `text3m2` with something different (e.g., `MULT_INT` into different registers), or remove the `AluTputM2` case.

12. **`Program.cs` — `PrintProgress` for `fail` passes `done=0, phaseTotal=0`.**  
    File: `Program.cs`, `Main`, branch `if (!runner.TryPrepare())`:
    ```csharp
    PrintProgress(testIndex, testTotal, name, "fail", 0, 0,
                  phaseTotal, phaseTotal, globalStep, totalSteps, forceNewLine: true, failed: true);
    ```
    `done=0, phaseTotal=0` — not used in `phaseStr` (`phase == "fail"` → `$"{phase,-4}"`), but in `testPct = phaseStepsTotal <= 0 ? 1.0 : ...` — `phaseStepsTotal = phaseTotal > 0`, so the percentage is computed as `phaseStep/phaseTotal = 1.0`. Not a bug, but the parameters "0, 0" are misleading — either `phaseTotal, phaseTotal`, or add a comment.

13. **`Stats.Percentile`/`Median`/`TrimmedMean` — `OrderBy().ToArray()` on every call.**  
    File: `Stats.cs`, methods `Percentile`, `Median`, `TrimmedMean`.  
    `StatsResult.From` calls `Percentile` 10 times, `Median` 1 time, `TrimmedMean` 2 times — a total of 13 sorts of the same array. For `n=20` it is unnoticeable, but for `n=1000` (if you expand `Runs`) it is noticeable.  
    Fix: sort once in `StatsResult.From` and pass `ReadOnlySpan<double>`/a sorted array. Or cache in a local variable inside `From`.

14. **`BenchRunner.cs` — `ListExtensions` in the same file.**  
    File: `BenchRunner.cs`, `file static class ListExtensions`.  
    `ComputeVariance` is the only extension in the project, located next to `BenchRunner`. Inconvenient for navigation.  
    Fix: move to `Stats.cs` as `public static double Variance(IReadOnlyList<double> v)` and do not make it an extension (an extension on `IReadOnlyList<double>` is a dubious practice, conflicts with `System.Linq`).

15. **`Benchmarks.csproj` — `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` without `unsafe` code.**  
    File: `Nantuc.Benchmarks.csproj`. There is no `unsafe` in the project code.  
    Fix: remove it (or add a comment `<!-- needed for ... -->` if it is really needed for transitive dependencies).

16. **`Program.cs` — `case text4` does not use a heap allocator.**  
    File: `Program.cs`, `const string text4`. Comment in `text4u`: `// <-- replacement for ALLOC r7`. In `text4` — the same replacement (`MOV r7, rHP; ADD rHP, r0;`), but without a comment. The reader may think that `text4` is the "original with ALLOC", which is not true.  
    Fix: add the same comment to `text4`, or move the bump alloc into a separate function (via `#include` or inline asm) so that it is clear this is manual allocation, not an opcode.

17. **`Program.cs` — `Console.WriteLine($"Results file...")` with Cyrillic and `Console.ReadLine()`.**  
    File: `Program.cs`, end of `Main`.  
    In a Windows console with code page 866/1251, Cyrillic may display as mojibake if `Console.OutputEncoding = Encoding.UTF8` is not set.  
    Fix: `Console.OutputEncoding = System.Text.Encoding.UTF8;` at the beginning of `Main` (or `Console.OutputEncoding = new UTF8Encoding(false)` for CI).

18. **`BenchRunner.TryPrepare` — `Program = res` before `TryFastLoadProgram`.**  
    File: `BenchRunner.cs`, `TryPrepare`:
    ```csharp
    var ctx = _host.Emulator.GetDeviceContext(_deviceId)!;
    Program = res;
    if (!ctx.TryFastLoadProgram(res.Program, 0, out string? errorMath)) { ... return false; }
    ```
    If loading fails, `Program` is already set, but `IsPrepared = false`. If the calling code looks at `Program`, it will see a "successful" compilation, but the program is not loaded.  
    Fix: `Program = res` after successful loading. Or document that `Program` is the compilation result, not the loading result.