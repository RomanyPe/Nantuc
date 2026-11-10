# Nantuc.VmApplication — README

> The platform's primary API: compiling a project to bytecode, running the VM, managing devices, and step-by-step execution.  
> The only module intended for embedding Nantuc into an application (IDE, REPL, CI, game host).  
> Depends on `[Kernel](../../src/Nantuc.Kernel/README.md)`, `[Contracts](../../src/Nantuc.Contracts/README.md)`, and `[Compiler](../../src/Nantuc.Compiler/README.md)`.  
> `[← Root](../README.md) · [Common](../../src/Nantuc.Common/README.md) · [Contracts](../../src/Nantuc.Contracts/README.md) · [Diagnostics](../Nantuc.Diagnostics/README.md) · [Kernel](../../src/Nantuc.Kernel/README.md) · [Compiler](../../src/Nantuc.Compiler/README.md)`

## Table of Contents
- [About This Module](#about-this-module)
- [Local Project Tree](#local-project-tree)
- [API](#api)
- [API Examples](#api-examples)
- [Module's Proximity to the User](#modules-proximity-to-the-user)
- [What Was Fixed in This Version](#what-was-fixed-in-this-version)
- [Special Information](#special-information)
- [Desync With the Previous README Version](#desync-with-the-previous-readme-version)

## About This Module

`Nantuc.VmApplication` — a facade over two subsystems: the compiler (`Nantuc.Compiler`) and the runtime (`Nantuc.Kernel`). All kernel types that the user "shouldn't" see (`Processor`, `MemoryBus`, `NativeMemoryBuffer`, `PortBus`, `ManagerDevices`, `Assembler`) are hidden behind a thin layer of wrappers. The user works with five main abstractions:

- `VmHost` — "all in one": the project (`VmHostProject`) + the emulator (`VmEmulator`). Created via `VmHostFactory.CreateDefault()`.
- `VmHostProject` — compilation: `GetSourceFiles()` → `Compile()` / `CompileToIR()` → `CompilationResult` / `CompilationToIRResult`. Configured via `CompilerContext` (built by `VmHostProjectBuilder`).
- `CompilerContext` — an immutable snapshot of the compiler configuration: `Optimizator` + `Paths` + `FileService` + `ThrowCompiler`.
- `VmEmulator` — execution: registering devices, including a VM (`DeviceSpec`), providing `DeviceContext`, access to `IPortBus`. Configured via `VmEmulatorBuilder`.
- `DeviceContext` — managing a specific VM: `LoadProgram`, `Run`, `RunStepMode`/`SingleStep`, `Stop`, `Pause`/`Resume`, `ReadPort`, `GetRegistersSnapshot`, `GetLastFault`, modes (Step/Continuous/None).

Key architectural feature: the VM is also a device on the port bus. `VmEmulator.TryRegisterDevice(DeviceSpec, sector)` creates a `Device` from the kernel and registers it as an `IPortController`. A single emulator can hold multiple VMs, and they communicate via ports like ordinary devices (VM composition inside a VM). `DeviceContext` is a convenient window into a specific `Device` without direct access to `Nantuc.Kernel`.

The module targets `net10.0`.

The API follows the "Try + IdState + ProcessorFault" pattern:
- `TryRegisterDevice` / `TryGetDeviceContext` — no `!` and no magic `-1..-4`.
- `IdState` — classification of `TryRegisterDevice` return values.
- `Run` / `DebugRun` → `ProcessorFault` — completion status instead of manual `IProcessorFaultPolicy`.
- `TryLoadProgram` — safe loading without exceptions.
- `GetLastFault` — access to the last `ProcessorFault` without `AsRawDevice`.
- `ThrowCompiler` (in `CompilerContext`) — a unified compiler exception policy via `IErrorMessageProvider`.

## Local Project Tree

Namespaces correspond to folders:

```
Nantuc.VmApplication/
├── Nantuc.VmApplication.csproj        # net10.0, unsafe, ref → Compiler, Contracts, Kernel
├── VmHost.cs                           # record VmHost(Project, Emulator) : IDisposable
├── VmHostFactory.cs                    # CreateDefault(...) with optimizator and provider
├── Default/
│   ├── DefaultFileService.cs           # IFileService over the real FS
│   └── RamFileService.cs               # IFileService in memory (in-memory)
│                                       # + path canonicalization as in the real FS
├── Emulator/                           (namespace Nantuc.VmApplication.Emulator)
│   ├── Abstraction/PortControlBase.cs  # abstract IPortController + Interlocked.Dispose
│   ├── DeviceContext.cs                # window into Device: Run/Step/Load/Ports/Registers/GetLastFault/Pause/Resume
│   ├── LaunchOptions.cs                # LaunchOptions / LaunchOptionsDebug
│   ├── SnapshotRegisters.cs            # readonly struct with InlineArray[32]
│   ├── VmEmulator.cs                   # wrapper over Utilites.Emulator + DeviceSpec
│   └── VmEmulatorBuilder.cs            # fluent: WithPortBusSize / WithPortsPerDevice
├── Project/                            (namespace Nantuc.VmApplication.Project)
│   ├── CompilationResult.cs            # byte[] + section metrics + errors
│   ├── CompilationToIRResult.cs        # IRAssembler + Build() → CompilationResult
│   ├── CompilerContext.cs              # record: Optimizator + Paths + FileService + ThrowCompiler
│   ├── IFileService.cs                 # FS abstraction
│   ├── IProjectFilesConfig.cs          # paths + extensions; static Default
│   ├── OptimizationResultLog.cs        # Dictionary<OptimizationId, IReadOnlyLogOptimization>
│   ├── ProjectBuilder.cs               # orchestration: parse → AST-merge → includes → generate
│   ├── SourceFile.cs                   # record struct (Name, Content, Language)
│   ├── SourceLanguage.cs               # enum { Asm, C, None }
│   ├── VmAstOptimizer.cs               # wrapper over AstOptimizer
│   ├── VmCompilerOptimizator.cs        # AstOptimizer + IROptimizer + wrappers
│   ├── VmHostProject.cs                # GetSourceFiles / Compile / CompileToIR / OptimizationIR
│   ├── VmHostProjectBuilder.cs         # WithPaths / WithFileService / WithOptimizator / WithThrowProvider / Build
│   └── VmIROptimizer.cs          # wrapper over IROptimizer
└── Unsafe/                             (namespace Nantuc.VmApplication.Unsafe)
    ├── CompilationResultUnsafeExtension.cs # Assembler extension prop
    ├── DeviceContextUnsafeExtensions.cs     # AsRawDevice()
    ├── VmEmulatorUnsafeExtension.cs         # GetRawPortBus() GetRawEmulator
    ├── VmCompilerOptimizatorExtension.cs
    └── VmPojectUnsafeExtension.cs          # CustomCompile<TAssembler,TResult>(...) — wrapper over CompileInternal
```
## API

### VmHost and VmHostFactory — entry point

```csharp
public sealed record VmHost(VmHostProject Project, VmEmulator Emulator) : IDisposable;

public static class VmHostFactory
{
    public static VmHost CreateDefault(
        IProjectFilesConfig? projectConfig = null,
        IFileService? fileService = null,
        VmCompilerOptimizator? optimizator = null,
        IErrorMessageProvider? provider = null,      // exception policy → ThrowCompiler
        PortSize? portBusSize = null,
        DevicePortSize? portsPerDevice = null);
}
```

`CreateDefault` — the simplest entry point:

```csharp
using var host = VmHostFactory.CreateDefault();
```

Optional parameters:
- `projectConfig` — paths and extensions (`IProjectFilesConfig.Default`).
- `fileService` — `RamFileService` by default (in-memory, emulates the path semantics of the real FS: both separators, `.`/`..`, case-insensitive by default); `DefaultFileService` — for the real FS.
- `optimizator` — a ready-made `VmCompilerOptimizator` with rules. If `null` — `VmHostProjectBuilder.Build()` will create a new `VmCompilerOptimizator()` (`?? new()`).
- `provider` — `IErrorMessageProvider?`, wrapped into `ThrowCompiler` inside `VmHostProjectBuilder`.
- `portBusSize` — size of the port space (`PortSize.KB64`).
- `portsPerDevice` — size of the port block per device (`DevicePortSize.B16`).

`VmHost.Dispose()` releases only `Emulator` (internally `Utilites.Emulator.Dispose`), but not `Project` — `VmHostProject` is not `IDisposable`.

### VmEmulator and VmEmulatorBuilder — execution

```csharp
public sealed class VmEmulator(Utilites.Emulator emulator) : IDisposable
{
    public IPortBus GetPortBus();
    public bool ChangeDeviceSector(int id, uint newSector);
    public IReadOnlyList<DeviceInfo> GetAllDevices();

    public int      RegisterDevice(IPortController ioDevice, string? name, uint sector);
    public int      RegisterDevice(DeviceSpec spec, uint sector);
    public IdState  TryRegisterDevice(IPortController ioDevice, string? name, uint sector, out int id);
    public IdState  TryRegisterDevice(DeviceSpec spec, uint sector, out int id);

    public DeviceContext? GetDeviceContext(int id);
    public bool           TryGetDeviceContext(int id, [NotNullWhen(true)] out DeviceContext? ctx);

    public bool UnregisterDevice(int id);
    public void Dispose();
}

public readonly record struct DeviceSpec(
    RamSize RamSize,
    string? Name,
    IProcessorFaultPolicy ProcessorFaultPolicy)
{
    public static DeviceSpec Create(
        RamSize? ramSize = null,
        string? name = null,
        IProcessorFaultPolicy? processorFaultPolicy = null);
}

public class VmEmulatorBuilder
{
    public VmEmulatorBuilder WithPortBusSize(PortSize ports);
    public VmEmulatorBuilder WithPortsPerDevice(DevicePortSize portsOnDev);
    public VmEmulator Build();
}
```

Two registration methods:
- `RegisterDevice` — returns `int` (ID on success, `-1..-4` on error). For code that already works with `int`.
- `TryRegisterDevice` — returns `IdState` (`Valid`/`NotAssigned`/`OutOfRange`/`NoFreeSlot`/`Rejected`) and `out int id`. **Recommended method** for new code.

`TryGetDeviceContext` — a safe variant of `GetDeviceContext` without `!`: returns `bool` and `[NotNullWhen(true)] out DeviceContext?`.

### VmHostProject, CompilerContext and VmHostProjectBuilder — compilation

```csharp
public sealed record class CompilerContext(
    VmCompilerOptimizator Optimizator,
    IProjectFilesConfig Paths,
    IFileService FileService,
    ThrowCompiler ThrowCompiler);

public sealed class VmHostProject(CompilerContext ctx)
{
    public static Regex AsmMnemonics { get; }
    public static Regex AsmRegisters { get; }
    public static Regex MiniCMnemonics { get; }

    public VmCompilerOptimizator Optimizator { get; }
    public IProjectFilesConfig Paths { get; }
    public IFileService FileService { get; }
    public ThrowCompiler ThrowCompilerPolicy { get; }

    public IEnumerable<SourceFile> GetSourceFiles();

    public CompilationResult     Compile(IEnumerable<SourceFile> files, ulong baseAddress, bool optimize);
    public CompilationToIRResult CompileToIR(IEnumerable<SourceFile> files, ulong baseAddress, bool optimize);
    public CompilationToIRResult OptimizationIR(CompilationToIRResult res, int IROptimizationCount);
}

public class VmHostProjectBuilder
{
    public VmHostProjectBuilder WithPaths(IProjectFilesConfig paths);
    public VmHostProjectBuilder WithFileService(IFileService proj);
    public VmHostProjectBuilder WithOptimizator(VmCompilerOptimizator? optimizator);
    public VmHostProjectBuilder WithThrowProvider(IErrorMessageProvider? policy);
    public VmHostProject Build();
}
```

- `CompilerContext` — the only constructor argument of `VmHostProject`. Assembled by `VmHostProjectBuilder.Build()`.
- `Compile` — single-stage build: `Assembler` (direct emit) → `byte[]`.
- `CompileToIR` — two-stage: `IRAssembler` (list of `AsmItem`) → `CompilationToIRResult`, then `OptimizationIR(result, maxPasses)` runs the IR and `result.Build()` assembles the bytes.
- `optimize: true` — enables AST optimizations from `Optimizator.VmAstOptimizer`. Logs are collected in `OptimizationResultLog`.
- `GetSourceFiles` — reads all `.c`/`.minic`/`.asm`/`.nanasm` from `FileService.GetNameFiles(Paths)`.
- `WithThrowProvider` — optional exception policy (`IErrorMessageProvider?`), wrapped into `ThrowCompiler`.

`VmHostProject` exposes static regexes for syntax highlighting: `AsmMnemonics`, `AsmRegisters`, `MiniCMnemonics`.

### DeviceContext — VM management

```csharp
public class DeviceContext
{
    public bool IsRunning { get; }
    public long StepCount { get; }
    public ulong RamSize { get; }
    public IReadOnlyNativeBuffer NativeReadOnlyBuffer { get; }

    public void Stop();
    public void Pause();                 // queues OpCode.HALT
    public void Resume();                // queues OpCode.WAKE
    public void LoadProgram(ReadOnlySpan<byte> program, ulong loadAddress = ProjectBuilder.BaseAddressProgram);
    public bool TryLoadProgram(ReadOnlySpan<byte> program, ulong loadAddress, [NotNullWhen(false)] out string? error);
    public void ResetMemoryRam();

    public ProcessorFault Run(LaunchOptions? opt = null);
    public ProcessorFault DebugRun(LaunchOptionsDebug? opt = null);

    public void RunStepMode(ulong startAddress = ProjectBuilder.BaseAddressProgram, Action? onLaunch = null);
    public void SingleStep(Action<ProcessorSnapshot>? onStep = null);
    public void MultiStep(int count = 5, Action<ProcessorSnapshot>? onStep = null);
    public void EndStepMode();

    public byte ReadPort(ulong offset);
    public void CopyRegisters(Span<ulong> destination);
    public SnapshotRegisters GetRegistersSnapshot();
    public ProcessorFault GetLastFault();
}

public record class LaunchOptions(
    ulong StartAddress = ProjectBuilder.BaseAddressProgram,
    long? StepLimit = null,
    bool ShowTimer = false,
    Action? OnLaunch = null,
    Action<TimeSimulationResult>? OnEnd = null)
{ public static LaunchOptions Default { get; } }

public record class LaunchOptionsDebug(
    ulong StartAddress = ProjectBuilder.BaseAddressProgram,
    int DelayMs = 0,
    long StepLimit = long.MaxValue,
    Action? OnLaunch = null,
    Action<ProcessorSnapshot>? OnStep = null,
    Action? OnEnd = null)
{ public static LaunchOptionsDebug Default { get; } }
```

Capabilities:
- `LoadProgram` accepts a `ReadOnlySpan<byte>` (no allocations) and calls the native `_device.LoadProgram`.
- `TryLoadProgram` — a safe form of loading without exceptions.
- `Pause()` / `Resume()` — queue external commands `OpCode.HALT` / `OpCode.WAKE`; can be called from another thread (queue-based).
- `GetLastFault()` — public access to the last `ProcessorFault` without `AsRawDevice`.

`Run` and `DebugRun` return `ProcessorFault` — on success `BiosStatus.EndProgram`, on failure — the status with `InstructionPointer`/`OpCode`.

Modes (`DeviceContext.Mode` — private enum):

| Mode | How to enter | How to exit | What it does |
|---|---|---|---|
| `Continuous` | `Run` / `DebugRun` | automatically after completion | blocking `Step()` loop |
| `Step` | `RunStepMode` | `EndStepMode` | manual `SingleStep`/`MultiStep` |
| `None` | `Stop()` or completion | — | nothing is running |

**`SnapshotRegisters`** — in a separate file, a `readonly struct` with `[InlineArray(32)]`:

```csharp
public readonly struct SnapshotRegisters : IEquatable<SnapshotRegisters>
{
    public SnapshotRegisters(Span<ulong> values);
    public ulong[] AsArray();
    public ulong this[RegType r] { get; }
    public const int Count = 32;
    public IEnumerable<(RegType Type, ulong Value)> Enumerate();
    public bool Equals(SnapshotRegisters other);
    public override int GetHashCode();
    public override string ToString();
    public static bool operator ==(SnapshotRegisters a, SnapshotRegisters b);
    public static bool operator !=(SnapshotRegisters a, SnapshotRegisters b);
}
```

### Compilation results

```csharp
public readonly struct CompilationResult
{
    public CompilationResult(AssemblerBase asm, OptimizationResultLog? log);
    public CompilationResult(IReadOnlyList<string>? errors);

    public byte[]? Program { get; }
    public ulong StartAdress { get; }
    public int CodeSize { get; }
    public int DataStart { get; }
    public int DataSize { get; }
    public OptimizationResultLog? OptimizationResultLog { get; }
    public IReadOnlyList<string>? Errors { get; }
    public bool Success => Program != null && (Errors == null || Errors.Count == 0);
}
```

`Success` requires `Program != null` — otherwise `CompilationResult(new List<string>())` would yield `Success == true` with `Program == null` and an NRE in `TryLoadProgram`.

```csharp
public readonly record struct CompilationToIRResult
{
    internal readonly IRAssembler? Assembler;
    public IReadOnlyList<string>? Errors { get; }
    public OptimizationResultLog? OptimizationResultLog { get; }

    public CompilationToIRResult(IReadOnlyList<string>? errors);
    public CompilationToIRResult(IRAssembler? assembler, OptimizationResultLog? optimizationResultLog);
    public CompilationToIRResult(IRAssembler? assembler, OptimizationResultLog? optimizationResultLog, IReadOnlyList<string>? errors);

    public CompilationResult Build();
}
```

### File services

```csharp
public interface IFileService
{
    IEnumerable<string> GetNameFiles(IProjectFilesConfig path);
    string ReadFile(string fileName);
    bool Exist(string fileName);
    void SaveFile(string fileName, string content);
    string Combine(string path1, string path2);
}
```

- `DefaultFileService` — the real FS.
- `RamFileService` — in-memory, emulating the behavior of the real FS **within the `IFileService` contract** (there are no write/delete/create-directory operations — they aren't in the interface either).
  - `Combine` = `Path.Combine` + canonicalization: both separators (`\` and `/`) are normalized to one, `.` and `..` are resolved, repeated and trailing separators are removed; the absoluteness marker (`/`, `C:`) is preserved.
  - `Exist` / `ReadFile` / `SaveFile` operate on the canonical path: `src\main.c`, `src/main.c`, `./src//main.c`, `src/sub/../main.c` — all the same file. Absolute and relative paths (`/file.c` vs `file.c`) are **different** files.
  - Path comparison is case-insensitive by default (like Windows). Constructor: `new RamFileService(caseSensitive: true)` — for case-sensitive mode; `separator` — to choose the separator character in canonical paths.
  - `GetNameFiles`: `IncludePath` from the config is canonicalized before comparison, `StartsWith` uses the same `StringComparison` as the dictionary. Extensions (`ExtensionsAsm` / `ExtensionsMiniC`) are compared via `StringComparison.Ordinal` (as in the original — `main.MINIC` is not considered `.minic`).
  - Directories exist implicitly: `SaveFile("a/b/c.c", ...)` implies `a/` and `a/b/`. There is no separate `CreateDirectory` — the contract doesn't require it.

`IProjectFilesConfig` — `BaseDirectory`, `ProjectPath`, `IncludePath`, `ExtensionsAsm`, `ExtensionsMiniC`, `static Default`.
### Optimizers

```csharp
public sealed class VmCompilerOptimizator
{
    public VmAstOptimizer VmAstOptimizer { get; }
    public VmIROptimizer VmIROptimizer { get; }
}

public sealed class VmAstOptimizer
{
    public int Count { get; }
    public void Add(IAstOptimizationRule rule);
    public void Insert(int index, IAstOptimizationRule item);
    public void Remove(IAstOptimizationRule item);
    public void RemoveAt(int index);
    public OptimizationId GetType(int i);
}

public sealed class VmIROptimizer
{
    public int Count { get; }
    public void Add(IOptimizerIRRule rule);
    public void Insert(int index, IOptimizerIRRule item);
    public void Remove(IOptimizerIRRule item);
    public void RemoveAt(int index);
}
```

`VmCompilerOptimizator` — a container, created in `VmHostFactory.CreateDefault` (if not passed) and available via `host.Project.Optimizator`. Both rule lists are empty by default.

### Unsafe extensions

```csharp
public static class CompilationResultUnsafeExtension
{
    extension(CompilationToIRResult il)
    {
        public IRAssembler? Assembler => il.Assembler;
    }
}

public static class DeviceContextUnsafeExtensions
{
    extension(DeviceContext context)
    {
        public Device AsRawDevice();
    }
}

public static class VmEmulatorUnsafeExtension
{
    extension(VmEmulator vm)
    {
        public PortBus GetRawPortBus();
        public Kernel.Utilites.Emulator GetRawEmulator();
    }
}

public static class VmPojectUnsafeExtension
{
    extension(VmHostProject proj)
    {
        public TResult CustomCompile<TAssembler, TResult>(
            ulong baseAddress,
            bool optimize,
            IEnumerable<SourceFile> files,
            Func<ulong, TAssembler> createAssembler,
            Func<TAssembler, OptimizationResultLog?, string[]?, TResult> createResult)
            where TAssembler : AssemblerBase;
    }
}

public static class VmCompilerOptimizatorExtension
{
    extension(VmCompilerOptimizator opt)
    {
        public IROptimizer RawIROptimizer;
        public AstOptimizer RawAstOptimizer;
    }
}
```

For cases where a wrapper isn't enough: direct access to `Device`/`PortBus`/`IRAssembler`/`CompileInternal`. Use with caution: after `AsRawDevice()` you hold a reference to `Device`, and its `Dispose` is no longer controlled by `VmEmulator`. The class name `VmPojectUnsafeExtension` contains a typo (preserved for backward compatibility).

## API Examples

**Full cycle with the safe API:**

```csharp
using Nantuc.Common;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Project;

using var host = VmHostFactory.CreateDefault();

var sources = host.Project.GetSourceFiles().ToList();
var compilation = host.Project.Compile(sources, baseAddress: 0, optimize: true);
if (!compilation.Success)
{
    foreach (var err in compilation.Errors!)
        Console.Error.WriteLine(err);
    return;
}

var registerState = host.Emulator.TryRegisterDevice(
    DeviceSpec.Create(ramSize: RamSize.KB64, name: "main"),
    sector: 0,
    out int deviceId);
if (registerState != IdState.Valid)
{
    Console.Error.WriteLine($"RegisterDevice failed: {registerState} (id={deviceId})");
    return;
}

if (!host.Emulator.TryGetDeviceContext(deviceId, out var vm))
{
    Console.Error.WriteLine("DeviceContext not available");
    return;
}

if (!vm.TryLoadProgram(compilation.Program!, loadAddress: 0, out var error))
{
    Console.Error.WriteLine(error);
    return;
}

ProcessorFault fault = vm.Run(new LaunchOptions(ShowTimer: true,
    OnEnd: r => Console.WriteLine($"steps={r?.Steps} elapsed={r?.Elapsed}")));

if (fault.Status != BiosStatus.EndProgram)
    Console.Error.WriteLine($"VM fault: {fault.Status} at IP=0x{fault.InstructionPointer:X}");

// The last fault can also be obtained after Run
ProcessorFault lastFault = vm.GetLastFault();
```

**Pause/Resume from another thread:**

```csharp
using var host = VmHostFactory.CreateDefault();
// ... device registration and program loading ...

var runner = Task.Run(() => vm.Run());
Thread.Sleep(200);
vm.Pause();     // OpCode.HALT — external command queue
Thread.Sleep(100);
vm.Resume();    // OpCode.WAKE
runner.Wait();
```

**Compilation via IR with optimizations:**

```csharp
var sources = host.Project.GetSourceFiles();
var il = host.Project.CompileToIR(sources, baseAddress: 0, optimize: true);

il = host.Project.OptimizationIR(il, IROptimizationCount: 5);

var result = il.Build();
if (result.Success)
    File.WriteAllBytes("out.nbin", result.Program!);

if (result.OptimizationResultLog is { } logs)
    foreach (var (id, log) in logs.Logs)
        foreach (var e in log.GetLogs())
            Console.WriteLine($"[{id}] pass {e.Pass}: {e.Message}");
```

**In-memory scenario:**

```csharp
using Nantuc.VmApplication.Default;

// Case sensitivity is false by default (as on Windows);
// separator — the character that will be used in canonical paths.
var fs = new RamFileService();
fs.SaveFile(@"src\main.c", "int main() { int x = 21; return x * 2; }");

// The same file via a different path — thanks to canonicalization:
fs.Exist("src/main.c");        // true
fs.Exist("./src//main.c");     // true
fs.Exist("src/sub/../main.c"); // true

using var host = VmHostFactory.CreateDefault(fileService: fs);

var result = host.Project.Compile(host.Project.GetSourceFiles(), 0, optimize: false);```

**Step-by-step execution:**

```csharp
if (!host.Emulator.TryGetDeviceContext(deviceId, out var vm)) return;
vm.LoadProgram(compilation.Program!);   // ReadOnlySpan<byte> — works with byte[] too
vm.RunStepMode(startAddress: 0);

for (int i = 0; i < 20 && vm.IsRunning; i++)
    vm.SingleStep(r => Console.WriteLine(
        $"IP=0x{r.GetRegister(RegType.rIP):X} r0={r.GetRegister(RegType.r0)}"));

vm.EndStepMode();
```

**Register snapshot:**

```csharp
SnapshotRegisters before = vm.GetRegistersSnapshot();
vm.MultiStep(count: 10);
SnapshotRegisters after = vm.GetRegistersSnapshot();

if (before != after)
    Console.WriteLine($"IP: 0x{before[RegType.rIP]:X} -> 0x{after[RegType.rIP]:X}");
```

**Registering a custom device (UART):**

```csharp
using Nantuc.VmApplication.Emulator.Abstraction;

public sealed class UartController : PortControlBase
{
    private byte _tx;
    public override byte ReadPort(ulong offset) => offset == 0 ? _tx : (byte)0;
    public override void WritePort(ulong offset, byte value) { if (offset == 0) _tx = value; }
    public override void WakeProcessor() { }
}

using var host = VmHostFactory.CreateDefault();
var uart = new UartController();
var state = host.Emulator.TryRegisterDevice(uart, name: "UART0", sector: 1, out _);
```

**Custom AST optimization rule:**

```csharp
using Nantuc.Compiler.C.Optimizators;
using Nantuc.Contracts;

public sealed class MyRule : IAstOptimizationRule
{
    public OptimizationId Type => new("my.rule");
    private readonly OptimizationResult _log = new(new OptimizationId("my.rule"));

    public IReadOnlyLogOptimization Optimize(ProgramNode program)
    {
        _log.Clear();
        // traverse program, modify AST
        return _log;
    }
}

var optimizator = new VmCompilerOptimizator();
optimizator.VmAstOptimizer.Add(new MyRule());

using var host = VmHostFactory.CreateDefault(optimizator: optimizator);
```

**Custom compiler exception policy:**

```csharp
using Nantuc.Diagnostics;

IErrorMessageProvider myProvider = /* ... */;
using var host = VmHostFactory.CreateDefault(provider: myProvider);

// or via builder
var project = new VmHostProjectBuilder()
    .WithPaths(IProjectFilesConfig.Default)
    .WithFileService(new RamFileService())
    .WithThrowProvider(myProvider)
    .Build();
```

## Module's Proximity to the User

Maximum. This is the platform's primary API. After compilation, a MiniC/NANASM user works with `DeviceContext`/`VmHost`, not directly with the kernel.

**Who uses what:**
- End user of the language — runs a ready-made `.nbin` via `DeviceContext.Run`/`RunStepMode`/`DebugRun`.
- IDE/REPL/CI author — `VmHostProject.Compile`/`CompileToIR`, `GetSourceFiles`, `OptimizationResultLog`, `SnapshotRegisters`, `Pause`/`Resume`.
- Game/application author — `VmHostFactory.CreateDefault`, `DeviceContext`, `PortControlBase`, `LaunchOptions`.
- Compiler extension author — `VmAstOptimizer.Add`/`VmIROptimizer.Add`, `VmCompilerOptimizator`.
- Custom exception policy author — `IErrorMessageProvider` + `WithThrowProvider`/`CreateDefault(provider:)`.

**What you should do:**
- Always `using var host = VmHostFactory.CreateDefault(...)` — `Dispose` releases the native buffers of all VMs.
- Check `compilation.Success` before `TryLoadProgram` — `Success` also requires `Program != null`.
- Use `TryRegisterDevice` (returns `IdState` + `out int id`) instead of `RegisterDevice` — unified diagnostics.
- Use `TryGetDeviceContext` instead of `GetDeviceContext(id)!`.
- For safe loading — `TryLoadProgram` (no exceptions).
- Read `ProcessorFault` from `Run`/`DebugRun` or via `GetLastFault()` — this is the correct way to learn the completion status.
- For WASM/browser — `RamFileService` (or `DefaultFileService`, but it's slower) + `CompileToIR` + `Build`.
- To stop a long run from another thread — `Pause()`/`Resume()`.

**What you should not do:**
- Create multiple `VmHost` instances in one process unnecessarily — each holds its own `PortBus` and `ManagerDevices`.
- Hold a `DeviceContext` after `host.Dispose()` — inside it there's a `Device` that has already been released.
- Call `Run` without stopping the previous run — `ThrowByMode` will throw `InvalidOperationException`.
- Use `DeviceContextUnsafeExtensions.AsRawDevice()` without a pressing need.
- Rely on `GetLastFault()` as the "result of the last step" — `_faultData` is not updated every iteration (for optimization) and is not reset in `LaunchProgramm`. See [Special Information](#special-information).
- Pass `null` to `WithOptimizator` — this is acceptable (a new `VmCompilerOptimizator` will be created), but it's better to control it explicitly.

## Special Information

### Why the VM is a device

`VmEmulator.TryRegisterDevice(DeviceSpec, sector)` creates a `BiosSystem.Device` and registers it as an `IPortController` in `PortBus`. `Device` implements `IPortController` (for the external port bus) and holds its own `PortBus` (the VM's internal bus). An external VM can send `OUT`/`IN` over the sector of the internal VM — and vice versa. `DeviceContext.ReadPort(offset)` reads the VM's own ports.

### `DeviceContext` modes

Inside `DeviceContext` — `private enum Mode { Step, Continuous, None }` and `volatile Mode _mode`:
- `Run`/`DebugRun` → `ThrowByMode(Continuous)` → blocking loop → `SetNoneMode()`.
- `RunStepMode` → `ThrowByMode(Step)` → `BreakPointerLaunchDevice`.
- `SingleStep`/`MultiStep` → `ThrowIfNotStepMode` → `Device.NextStepProcessor`/`NextStepProcessorCount`.
- `EndStepMode` → `ThrowIfNotStepMode` → `Device.Stop()` + one `NextStepProcessor(null)` + `SetNoneMode()`.
- `Stop` → `Device.Stop()` + `SetNoneMode()` — can be called from any mode.

`Stop`, `Pause`, `Resume` can be called from another thread (`Pause`/`Resume` enqueue a command into `EnqueueExternalCommand`). `Run`/`Step` — single-threaded.

### Bytecode: `baseAddress` and `StartAdress`

`CompilationResult.StartAdress` is the `baseAddress` passed to `Compile`/`CompileToIR`. By default `ProjectBuilder.BaseAddressProgram = 0x00`. `Compile` does not set `StartAdress` in `LoadProgram` automatically — `LoadProgram` copies the bytes to `loadAddress`, and `LaunchOptions.StartAddress` is where execution starts.

### Include resolution

`ProjectBuilder.Build` resolves `#include "file"` from two places: `Paths.ProjectPath` and `Paths.IncludePath`. If the file exists in both — `CodeException(ErrorCode.Parser_IncludeAmbiguous, ...)`; `FileNotFoundException` if the file isn't found anywhere. Included `.c`/`.minic` files are parsed into an AST, `.asm`/`.nanasm` are assembled directly (with a `processed` check against cycles).

### Thread-safety

`DeviceContext` is not thread-safe except for `Stop`, `Pause`, `Resume`. `SnapshotRegisters` is a `readonly struct` with a copy of the registers. `VmHostProject.Compile`/`CompileToIR` are not thread-safe (shared `_optimizator.AstOptimizer`). `VmEmulator.GetDeviceContext`/`RegisterDevice`/`UnregisterDevice` are not thread-safe.

### `ProcessorFault` from `Run`/`DebugRun`/`GetLastFault`

`Run` and `DebugRun` return the `ProcessorFault` from `_device.Run(...)`/`_device.DebugRun(...)`. `GetLastFault()` — public access to `_faultData` in `Processor`. Important: `_faultData` is not reset in `LaunchProgramm` (`[Nantuc.Kernel](../../src/Nantuc.Kernel/README.md)`, [What to fix, item 2](../../src/Nantuc.Kernel/README.md#what-to-fix)) — a second `Run` may return the fault from the first. The XML-doc on `GetLastFault` reflects this: "Not updated every iteration for the sake of optimizations".

### `RamFileService` — a mock, not a full FS

`RamFileService` implements **only** the `IFileService` contract. There are no `Delete`/`CreateDirectory`/`Move`/`Copy` operations either in the interface or in the class — adding them without a corresponding contract extension would violate the principle "the implementation doesn't do more than it promises", and tests for "dead" methods wouldn't validate production scenarios. If writing/deletion is needed in the future — first extend `IFileService` (or add `IWritableFileService : IFileService`), then implement it in both classes.

Known limitation of `GetNameFiles`: `IncludePath` is compared via `StartsWith` **without checking the path boundary**, so `IncludePath = /base/include` erroneously excludes `/base/include2/file.c`. The fix is to check the character following the prefix (`/` or `\`).

### Platform limitations

- `net10.0`, `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` — because of `Kernel`, but there is no unsafe code in the module itself.
- `[InlineArray]` in `SnapshotRegisters` — C# 12+, works in AOT/WASM.
- `DefaultFileService` uses `Directory.EnumerateFiles`/`File.ReadAllText` — works in WASM, but slowly. For WASM — `RamFileService` (in-memory, no I/O, with the same path rules: separator unification, `.`/`..`, case-insensitivity by default).
- `Thread.Sleep(delay)` in `DeviceContext.DebugRun` (via `Device.DebugRun`) — in WASM either a no-op or a `PlatformNotSupportedException`.