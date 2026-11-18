# Nantuc.Benchmarks.BenchmarkDotNet — README

> A benchmark for the Nantuc virtual machine based on **BenchmarkDotNet**: measures MIPS, stability, and allocations across 14 typical MiniC programs (calls, ALU, memory, bitwise operations, multiplication, branching, division, recursion, pointers, structures, sorting).  
> Depends on `[Nantuc.VmApplication.Extensions](../../src/Nantuc.VmApplication.Extensions/README.md)` (transitively on `VmApplication`, `Compiler`, `Kernel`).  
> [← Root](../README.md), [VmApplication](../../src/Nantuc.VmApplication/README.md), [Extensions](../../src/Nantuc.VmApplication.Extensions/README.md), [Kernel](../../src/Nantuc.Kernel/README.md), [Compiler](../../src/Nantuc.Compiler/README.md)

## Table of Contents
- [About this module](#about-this-module)
- [Local project tree](#local-project-tree)
- [API](#api)
- [API examples](#api-examples)
- [User proximity](#user-proximity)
- [Special information](#special-information)
- [What to fix](#what-to-fix)

## About this module

`Nantuc.Benchmarks.BenchmarkDotNet` is not a library, but a **console application** (`<OutputType>Exe</OutputType>`) that uses **BenchmarkDotNet** to measure the performance of the Nantuc virtual machine. It is an alternative to the manual benchmark `[Nantuc.Benchmarks](../Nantuc.Benchmarks/README.md)`: all statistics, warmup, outlier handling, and report generation are delegated to BenchmarkDotNet.

The module:

1. Creates a `VmHost` via `VmHostFactory.CreateDefault` and registers one `Device` (64 MiB RAM) on `sector 0`.
2. Compiles and loads a MiniC program in `GlobalSetup` — **outside the measurement**.
3. For each of the 14 cases (`SourceCasesMiniC.AllCases`), executes the `[Benchmark]` method `RunVm`, which runs the VM once (`ctx.Run`) and returns the number of executed steps.
4. Outputs the standard BenchmarkDotNet report (time, allocations, statistics) and saves artifacts to `BenchmarkDotNet.Artifacts/`.

Key roles:
- **`NantucBenchmarks.cs`** — the benchmark class with `[ShortRunJob]`, `[MemoryDiagnoser]`, `[ParamsSource]`, `GlobalSetup`/`GlobalCleanup`, and `[Benchmark] RunVm`. Also defines the `record BenchCase`.
- **`SourceCasesMiniC.cs`** — texts of 14 MiniC programs and the static property `AllCases`, returning `IEnumerable<BenchCase>`.
- **`Program.cs`** — entry point: `BenchmarkRunner.Run<NantucBenchmarks>()`.

This module serves as an example of **how to use `VmApplication` together with BenchmarkDotNet**: it does not touch `Kernel` directly, only through `VmHost`/`DeviceContext`, and demonstrates that the public API is sufficient for benchmarking without hacks.

## Local project tree

Namespace — `Nantuc.Benchmarks.BenchmarkDotNet`:

```
Nantuc.Benchmarks.BenchmarkDotNet/
├── Nantuc.Benchmarks.BenchmarkDotNet.csproj  # net10.0, Exe, BenchmarkDotNet 0.15.2, VSDiagnostics
├── Program.cs                                # BenchmarkRunner.Run<NantucBenchmarks>()
├── NantucBenchmarks.cs                       # benchmark class + record BenchCase
└── SourceCasesMiniC.cs                       # 14 MiniC programs + AllCases
```

Organization notes:
- `BenchCase` is defined in `NantucBenchmarks.cs`, although it would be more logical to move it to a separate file (as in the manual benchmark `Nantuc.Benchmarks`).
- `SourceCasesMiniC.AllCases` creates a new list on every access; for BenchmarkDotNet this is not critical (it is called once during parameter construction), but it could be cached.
- The `Runs` and `WarmupRuns` fields in `BenchCase` are not used by BenchmarkDotNet — this is legacy from the manual benchmark (see [What to fix, item 1](#what-to-fix)).

## API

The project is an `Exe` and has no public API. Below is the internal structure, useful for modifications and extensions.

### Entry point

`Program.cs` (top-level statements):

```csharp
using BenchmarkDotNet.Running;

BenchmarkRunner.Run<NantucBenchmarks>();
```

### `NantucBenchmarks`

```csharp
[ShortRunJob]
[MemoryDiagnoser]
public class NantucBenchmarks
{
    [ParamsSource(nameof(Cases))]
    public BenchCase Case { get; set; }

    public static IEnumerable<BenchCase> Cases => SourceCasesMiniC.AllCases;

    [GlobalSetup]    public void GlobalSetup();
    [GlobalCleanup]  public void GlobalCleanup();

    [Benchmark]
    public long RunVm();
}
```

- `[ShortRunJob]` — fast mode (few iterations). For precise numbers, replace with `[MediumRunJob]` or `[LongRunJob]`.
- `[MemoryDiagnoser]` — shows managed memory allocations.
- `[ParamsSource]` — BenchmarkDotNet will iterate through all cases from `SourceCasesMiniC.AllCases`.
- `GlobalSetup`:
  - creates a `VmHost` and registers a device;
  - compiles `Case.ProgramText` via `CompileToIR(..., optimize: true, SourceLanguage.C)`;
  - throws an `InvalidOperationException` with a list of errors if compilation fails;
  - loads the program via `ctx.TryLoadProgram(res.Program, 0, out error)`;
  - calls `GC.Collect(2, GCCollectionMode.Forced, true)` and `GC.WaitForPendingFinalizers()`.
- `GlobalCleanup` — disposes the `VmHost`.
- `RunVm`:
  - gets the `DeviceContext`;
  - creates `LaunchOptions(ShowTimer: true, OnEnd: res => steps = res.Steps)`;
  - calls `ctx.Run(opt)`;
  - returns `steps` (to prevent JIT from eliminating the call).

### `BenchCase`

```csharp
public record BenchCase(
    string Name,
    string ProgramText,
    int Runs = 1,
    int WarmupRuns = 1)
{
    public override string ToString() => Name;
}
```

`Runs` and `WarmupRuns` are **not used** by BenchmarkDotNet (it manages iterations itself). They are kept for compatibility with the manual benchmark. `ToString()` returns `Name` — this is what BenchmarkDotNet will display in the report as the parameter name.

### `SourceCasesMiniC`

```csharp
internal static class SourceCasesMiniC
{
    public const string callMc = """...""";
    // ... mathMc, aluTput1Mc, aluTput4Mc, aluTputMixedMc,
    //     memoryMc, bitOpsMc, mulMc, branchMc, divModMc,
    //     recursionMc, pointerMc, structMc, sortMc

    public static IEnumerable<BenchCase> AllCases =>
    [
        new BenchCase("Call",         callMc),
        new BenchCase("Math",         mathMc),
        new BenchCase("AluTput1",     aluTput1Mc),
        new BenchCase("AluTput4",     aluTput4Mc),
        new BenchCase("AluTputMixed", aluTputMixedMc),
        new BenchCase("Memory",       memoryMc),
        new BenchCase("BitOps",       bitOpsMc),
        new BenchCase("Mul",          mulMc),
        new BenchCase("Branch",       branchMc),
        new BenchCase("DivMod",       divModMc),
        new BenchCase("Recursion",    recursionMc),
        new BenchCase("Pointer",      pointerMc),
        new BenchCase("Struct",       structMc),
        new BenchCase("Sort",         sortMc),
    ];
}
```

| Case | What it measures |
|---|---|
| `Call` | Function call `sum(r0, r1)` in a loop (3M iterations) |
| `Math` | `ADD` + `DEC` + `TEST` + `JNZ` (3M iterations) |
| `AluTput1` | 4× `ADD` into one register + loop (800K iterations) |
| `AluTput4` | 4× `ADD` into different registers (IRP) + loop (800K iterations) |
| `AluTputMixed` | `ADD`/`SUB`/`AND`/`OR` into different registers (800K iterations) |
| `Memory` | Filling an `int[25000]` array and summing, 20 repeats |
| `BitOps` | `SHR`/`AND`/`OR`/`XOR` (3M iterations) |
| `Mul` | `MULT_INT` (3M iterations) |
| `Branch` | `if ((r6 & 1) != 0)` — even/odd check (500K iterations) |
| `DivMod` | `DIV` + `MOD` (500K iterations) |
| `Recursion` | Recursive `down(500)` × 500 calls |
| `Pointer` | Pointer arithmetic `*(p + i)` (2000 repeats) |
| `Struct` | Access to `struct Point` fields in an array (200 repeats) |
| `Sort` | Bubble sort of `int[200]` (50 repeats) |

## API examples

**Add your own case:**

```csharp
// in SourceCasesMiniC.cs
public const string myCaseMc =
"""
int main() {
    int r0 = 0;
    int r6 = 1000000;
    while (r6 != 0) {
        r0 = r0 ^ r0;
        r6 = r6 - 1;
    }
    return r0;
}
""";

// in AllCases:
new BenchCase("XorSelf", myCaseMc),
```

**Run only selected cases (via command-line arguments):**

```bash
dotnet run -c Release -- --filter *Call* *Memory*
```

**Replace Job with a more precise one:**

```csharp
// instead of [ShortRunJob]
[SimpleJob(RunStrategy.Throughput, launchCount: 3, warmupCount: 5, iterationCount: 10)]
// or
[MediumRunJob]
```

**Run from code (not via `Program.Main`):**

```csharp
using BenchmarkDotNet.Running;
using Nantuc.Benchmarks.BenchmarkDotNet;

var summary = BenchmarkRunner.Run<NantucBenchmarks>();
```

**Get the number of steps in the report:**

`RunVm` returns `long steps`. BenchmarkDotNet will show this as `Result` (not as the primary metric, but this prevents dead-code elimination). The primary metric is execution time.

## User proximity

**Medium.** This is a tool for VM kernel/compiler authors, not for end users of MiniC. BenchmarkDotNet provides ready-made statistics and reports, but requires understanding its model (Job, Warmup, Iteration, Params).

**What you should do:**
- Run in **Release** configuration: `dotnet run -c Release`.
- For precise numbers, replace `[ShortRunJob]` with `[MediumRunJob]` or `[LongRunJob]` — `ShortRunJob` gives noisy results.
- Use `--filter` to select specific cases so you don't have to run all 14.
- Check artifacts in `BenchmarkDotNet.Artifacts/` (HTML report, CSV, log).
- Remember that `GlobalSetup` is executed **once** per case, while `RunVm` is executed many times; device state is preserved between iterations (see [Special information](#special-information)).

**What you should not do:**
- Compare MIPS between machines — BenchmarkDotNet warns about instability, but core affinity, frequency, JIT, GC, and thermal throttling still affect results.
- Expect `Runs`/`WarmupRuns` from `BenchCase` to change anything — BenchmarkDotNet ignores them.
- Run in CI without `--filter` and without time limits — a full run of 14 cases with `MediumRunJob` can take tens of minutes.

## Special information

### `ShortRunJob` vs `MediumRunJob`/`LongRunJob`

`[ShortRunJob]` is `Job.ShortRun`: few iterations, fast run, but high variance. Suitable for a quick check that the benchmark works. For comparing VM versions, use `[MediumRunJob]` (recommended) or `[LongRunJob]`.

### `GlobalSetup` and device state

`GlobalSetup` compiles and loads the program **once** per case. Then `RunVm` calls `ctx.Run(opt)` repeatedly on the same `DeviceContext`. This means device memory is **not reset** between iterations. For cases that modify memory (`Memory`, `Pointer`, `Struct`, `Sort`), results may depend on previous runs (e.g., the array is already filled). This corresponds to the behavior of the manual benchmark `Nantuc.Benchmarks` (there too, `RunOnce` was called repeatedly on the same context). If isolation is needed, add state reset at the beginning of the program or use a separate `DeviceContext` per iteration (but this is beyond the scope of `[Benchmark]`).

### `optimize: true` for MiniC

Unlike the ASM benchmark, here `CompileToIR(..., optimize: true, SourceLanguage.C)` actually enables AST optimizations, since the language is C (MiniC). This is correct.

### `GC.Collect` in `GlobalSetup`

The call to `GC.Collect(2, GCCollectionMode.Forced, true)` and `GC.WaitForPendingFinalizers()` inherits from the manual benchmark. BenchmarkDotNet itself performs garbage collection between iterations, so this call is more "for peace of mind". It can be removed without loss of accuracy.

### `LaunchOptions(ShowTimer: true)`

`ShowTimer: true` enables the internal VM timer and fills `res.Elapsed`/`res.Steps` in `OnEnd`. BenchmarkDotNet measures the time of `RunVm` externally; the internal VM timer is needed only to obtain `Steps`. If you remove `ShowTimer`, `OnEnd` may not receive correct data.

### Artifacts

BenchmarkDotNet saves results to `BenchmarkDotNet.Artifacts/`:
- `results/*.md` — Markdown report;
- `results/*.csv` — CSV with metrics;
- `log/*.log` — execution log;
- `*.html` — HTML report (if exporter is enabled).

### Package `Microsoft.VisualStudio.DiagnosticsHub.BenchmarkDotNetDiagnosers`

Adds diagnostics for Visual Studio. Not required for CI and regular runs; can be removed if you do not use VS diagnostics.

### What this module does not do

- It is not a library — it is an `Exe`.
- It does not publish baseline results to external storage.
- It does not compare Nantuc versions — it only measures the current one.
- It does not test multithreading — all cases are single-threaded, using one `Device` for everything.

## What to fix

In descending order of severity.

1. **`BenchCase.Runs`/`WarmupRuns` are not used by BenchmarkDotNet.**  
   File: `NantucBenchmarks.cs`, `record BenchCase`.  
   The fields exist, but BenchmarkDotNet manages iterations itself. A reader might think they affect the measurement.  
   Fix: remove `Runs`/`WarmupRuns` from `BenchCase` (leave only `Name` and `ProgramText`) or add a comment `// ignored by BenchmarkDotNet`.

2. **`_case = Case;` is redundant assignment.**  
   File: `NantucBenchmarks.cs`, `GlobalSetup`: the field `_case` is initialized with the value of the property `Case`, which is accessible anyway.  
   Fix: use `Case` directly in `GlobalSetup` and `RunVm` (or keep the field, but remove the assignment — it adds nothing).

3. **`using Microsoft.VSDiagnostics;` is unused.**  
   File: `NantucBenchmarks.cs`.  
   Fix: remove the `using` if no attributes/classes from this namespace are used.

4. **`[ShortRunJob]` may give noisy results.**  
   File: `NantucBenchmarks.cs`, class attribute.  
   For comparing VM versions, `ShortRunJob` is too imprecise.  
   Fix: replace with `[MediumRunJob]` (or `[LongRunJob]`) or leave `ShortRunJob` with a comment "for quick checks, for precise numbers use MediumRunJob".

5. **`SourceCasesMiniC.AllCases` creates a new list on every access.**  
   File: `SourceCasesMiniC.cs`.  
   BenchmarkDotNet calls `ParamsSource` once, but if someone uses `AllCases` in a loop, it causes extra allocations.  
   Fix: cache it in `static readonly IReadOnlyList<BenchCase>`.

6. **`Program.cs` — top-level statements without `using System;`.**  
   File: `Program.cs`.  
   It works due to implicit usings (if enabled). The csproj does not specify `<ImplicitUsings>enable</ImplicitUsings>`, but the SDK for net10.0 might enable it by default. If disabled, `using System;` will be needed.  
   Fix: explicitly enable `<ImplicitUsings>enable</ImplicitUsings>` in csproj or add `using System;`.

7. **`<Nullable>` is not enabled.**  
   File: `Nantuc.Benchmarks.BenchmarkDotNet.csproj`.  
   The code contains `null!` and nullable annotations, but without `<Nullable>enable</Nullable>` they are not checked.  
   Fix: add `<Nullable>enable</Nullable>` (and possibly `<ImplicitUsings>enable</ImplicitUsings>`).

8. **`RunVm` returns `long steps`, but BenchmarkDotNet does not use this as a metric.**  
   File: `NantucBenchmarks.cs`, `[Benchmark] RunVm`.  
   The return value prevents dead-code elimination, but `steps` does not appear in the report as a separate metric (only as `Result`). If steps are needed in the report, you can add a `[Benchmark]` method returning `steps` and configure `[BenchmarkCategory]`, or use a `Consumer`.  
   Fix: document that `steps` is only protection against the optimizer; for step analysis, use the manual benchmark or a `[Benchmark]` with return value `steps` and a separate category.

9. **`Microsoft.VisualStudio.DiagnosticsHub.BenchmarkDotNetDiagnosers` is an unnecessary dependency for CI.**  
   File: `Nantuc.Benchmarks.BenchmarkDotNet.csproj`.  
   The package is only needed for diagnostics in Visual Studio.  
   Fix: remove the `PackageReference` if you do not use VS diagnostics (or keep it with a comment).