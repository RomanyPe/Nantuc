# Nantuc — README

[Русская версия](README.ru.md)

> A platform for executing bytecode with its own MiniC language (almost full C), NANASM assembler, compiler, virtual machine, and public embedding API.
> **Project version:** specified in `CHANGELOG.md`; semver is guaranteed.
> Written in C# (.NET 10). The compiler and runtime are separated into layers, `unsafe` code is isolated in the core, and the public API is a thin facade with safe `Try*` methods and `IdState` diagnostics.

**Nantuc** is a mini-framework and safe low-level scripting engine for .NET for cases where C-level control is needed but the code cannot be trusted. It runs programs written in a C-like language (MiniC) and provides a complete pipeline: lexer, parser, AST optimizer, bytecode code generator, assembler (text and IL), a register-based VM with memory and a port bus, a disassembler, a step-by-step debugger, I/O devices, and an embedding API. It also includes the NANASM assembler with disassembly and debugging support, explicit In/Out operations, and isolation so that a script crash does not crash the host.

The repository contains: Hello world in MiniC, a playable Snake game, an allocator test, and two VM benchmarks.

## Contents
- [Quick start](#quick-start)
- [Key features](#key-features)
- [Safe API](#safe-api)
- [Architecture](#architecture)
- [Modules and NuGet](#modules-and-nuget)
- [Requirements](#requirements)
- [Build and run](#build-and-run)
- [Examples](#examples)
- [Metrics and quality](#metrics-and-quality)
- [Contributing](#contributing)
- [License](#license)
- [Links](#links)

## Quick start

Minimal example — compile and run `Hello world` in MiniC with UART output, safely checking each step:

```csharp
using Nantuc.Common;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Extensions.Emulator.IO;
using Nantuc.VmApplication.Extensions.Host;
using Nantuc.VmApplication.Extensions.StdLibraries;

const string source =
    """
    #include "print.minic"
    char str[] = "Hello world";
    void main() { print16(str); }
    """;

using var host = VmHostFactory.CreateDefault();
host.Project.SaveFileProject("main.minic", source);
host.Project.SaveFileInclude("io.nanasm", Libraries.io_nanasm);
host.Project.SaveFileInclude("print.minic", Libraries.print_minic);

var vmState = host.Emulator.TryRegisterDevice(
    DeviceSpec.Create(RamSize.MB128, name: "ConsoleVM"),
    sector: 0, out int vmId);
if (vmState != IdState.Valid) { Console.Error.WriteLine($"register vm: {vmState}"); return; }

var uartState = host.Emulator.TryRegisterDevice(
    new QueuedIOStream(Console.Out, PortCharEncoding.Utf16),
    name: "console", sector: 1, out _);
if (uartState != IdState.Valid) { Console.Error.WriteLine($"register uart: {uartState}"); return; }

if (!host.Emulator.TryGetDeviceContext(vmId, out var vm)) { Console.Error.WriteLine("no ctx"); return; }

var compilation = host.Project.CompileToIL(baseAddress: 0, optimize: true).Build();
if (!compilation.Success)
{
    foreach (var e in compilation.Errors!) Console.Error.WriteLine(e);
    return;
}

if (!vm.TryLoadProgram(compilation.Program!, loadAddress: 0, out var error))
{
    Console.Error.WriteLine(error);
    return;
}

ProcessorFault fault = vm.Run(new LaunchOptions(ShowTimer: true));
if (fault.Status != BiosStatus.EndProgramm)
    Console.Error.WriteLine($"VM fault: {fault.Status} at IP=0x{fault.InstructionPointer:X}");
```

A detailed walkthrough (explanation of ports `0x10`/`0x12`, `QueuedIOStream`, diagnostics) — in [`Nantuc.Samples.Minimal/README.md`](Nantuc.Samples.Minimal/README.md).

## Key features

- **Two languages**: MiniC (C-like: structs, pointers, arrays, `const`, `sizeof`, inline `asm`, `extern`, `#include`) and NANASM (assembler with labels and size suffixes). Specifications are in [`Nantuc.Compiler/README.md`](Nantuc.Compiler/README.md).
- **Compiler from scratch**: lexer, recursive parser, AST, greedy register allocation, code generation to 32-bit bytecode, AST and peephole optimizations, `const`-correctness.
- **VM with isolated `unsafe`**: native RAM via `NativeMemory`, registers in `InlineArray` (no per-step allocations), port bus with `address → (device, offset)` routing.
- **Safe API**: `TryRegisterDevice`/`TryGetDeviceContext` instead of `id < 0` + `!`, `IdState` instead of magic `-1..-4`, `ProcessorFault` from `Run`/`DebugRun`, `TryLoadProgram` instead of exceptions, `Pause()`/`Resume()` for control from another thread.
- **Devices as first-class**: the VM itself is a device (`IPortController`) — VMs can be nested, UART, monitors, and keyboards can be connected.
- **Public API without hacks**: `VmHost` / `DeviceContext` / `VmHostProject` — all unsafe and native buffers are hidden in `Nantuc.Kernel`.
- **Disassembler and debugger**: code/data sections, text and structured output; `Debugger` on top of `DeviceContext` with `Step`/`StepOver`/`StepOut`/`Continue`/`RunToEnd`, breakpoints, `RequestStop` from another thread, best-effort call stack, RAM reading.
- **Standard optimizations** (AST and peephole) and infrastructure for custom rules.
- **Two benchmarks**: manual `Nantuc.Benchmarks` (MIPS, stability, percentiles, CV, JIT impact) and `Nantuc.Benchmarks.BenchmarkDotNet` on **14 MiniC cases**.

## Safe API

The public `Nantuc.VmApplication` API follows the “Try + IdState + ProcessorFault” pattern:

| Scenario | Old (raw) path | Safe path |
|---|---|---|
| Register a device | `int id = RegisterDevice(...)` + check `id < 0` | `TryRegisterDevice(..., out int id)` → `IdState` |
| Get `DeviceContext` | `GetDeviceContext(id)!` | `TryGetDeviceContext(id, out ctx)` |
| Load a program | `LoadProgram(...)` (throws) | `TryLoadProgram(..., out error)` |
| Run the VM | `Run()` → `void` | `Run(opt)` → `ProcessorFault` |
| Stop by limit | `RunWithLimit(...)` | `Run(new LaunchOptions(StepLimit: N))` |
| Pause/resume | — (only `Stop` before) | `Pause()` / `Resume()` — queue-based, safe from another thread |
| Step execution | — | `Debugger` (in `VmApplication.Extensions`) on top of `RunStepMode`/`SingleStep` |

- **`IdState`** (`Nantuc.Common`) — `Unknown` (0, default) / `Valid` (1) / `NotAssigned` (-1) / `OutOfRange` (-2) / `NoFreeSlot` (-3) / `Rejected` (-4). `default(IdState) == Unknown`; **do not rely on `default(IdState) == Valid`** — this has changed.
- **`ProcessorFault`** (`Nantuc.Common`) — `(BiosStatus Status, ulong FaultData, ulong InstructionPointer, OpCode OpCode)`. On success `Status == EndProgramm`; on failure — `DivOnZero`/`SegmentationFault`/… with the exact `IP` and `OpCode`.
- **`IProcessorFaultPolicy`** — used **inside** the VM; the return from `Run`/`DebugRun` is already `ProcessorFault`, so a separate policy is not needed.
- **`Debugger`** (`Nantuc.VmApplication.Extensions.Debugging`) — does not add contracts to `Kernel`/`VmApplication`; it works through the public API. Details — in [`Nantuc.VmApplication.Extensions/README.md`](src/Nantuc.VmApplication.Extensions/README.md#debugger).

## Architecture

Layers go bottom-up. Each next layer depends only on the previous ones; there are no reverse dependencies.

```
┌──────────────────────────────────────────────────────────────────────────────────┐
│  Nantuc.Samples.*   Nantuc.Benchmarks.*                                          │
│  (examples, end products, benchmarks)                                            │
└──────────────────────────────────────────────────────────────────────────────────┘
                              │
┌──────────────────────────────────────────────────────────────────────────────────┐
│  Nantuc.VmApplication.Extensions                                                 │
│  (disassembler, debugger, I/O, standard optimizations, libraries, utilities)     │
└──────────────────────────────────────────────────────────────────────────────────┘
                              │
┌──────────────────────────────────────────────────────────────────────────────────┐
│  Nantuc.VmApplication                                                            │
│  (VmHost, DeviceContext, VmEmulator, VmHostProject, CompilationResult)           │
└──────────────────────────────────────────────────────────────────────────────────┘
              │                                │
┌──────────────────────────┐    ┌──────────────────────────────────────────────────┐
│  Nantuc.Compiler         │    │  Nantuc.Kernel                                   │
│  (MiniC + NANASM)        │    │  (Processor, MemoryBus, PortBus,                 │
│  Assembler / ILAssembler │    │   Device, ManagerDevices)                        │
└──────────────────────────┘    └──────────────────────────────────────────────────┘
              │                                │
┌──────────────────────────────────────────────────────────────────────────────────┐
│  Nantuc.Contracts                                                                │
│  (IPortBus, IPortUse, IPortController, ProcessorSnapshot, ...)                   │
└──────────────────────────────────────────────────────────────────────────────────┘
              │                                │
┌──────────────────────────────────────────────────────────────────────────────────┐
│  Nantuc.Common            Nantuc.Diagnostics                                     │
│  (ISA, sizes, BiosStatus,       (ErrorCode, DiagnosticData,                     │
│   ProcessorFault, IdState)       ThrowCompiler)                                 │
└──────────────────────────────────────────────────────────────────────────────────┘
```

Key invariants:
- **`Diagnostics` does not depend on any Nantuc project** — the compiler author does not pull the unsafe core just for error codes.
- **The compiler does not depend on the runtime** — `Nantuc.Compiler` knows only `Common` + `Contracts` + `Diagnostics`.
- **`VmApplication` is the only module that knows both the compiler and the runtime**; it glues “compile” and “run”. The debugger, disassembler, and benchmarks live higher in the layers.

## Modules and NuGet

All modules are published as NuGet packages on [nuget.org](https://www.nuget.org/profiles/RomanyPe) (author — `RomanyPe`). Install only what the scenario needs; transitive dependencies are pulled automatically. Each module has its own README with details and examples.

| Package | Purpose | README |
|---|---|---|
| `Nantuc.Common` | Opcodes, registers, RAM/port sizes, `BiosStatus`, `ProcessorFault`, `IdState` (default-safe), decoder | [README](src/Nantuc.Common/README.md) |
| `Nantuc.Contracts` | `IPortBus`, `IPortUse`, `IPortController`, `ProcessorSnapshot`, `IProcessorFaultPolicy`, `OptimizationId` | [README](src/Nantuc.Contracts/README.md) |
| `Nantuc.Diagnostics` | `ErrorCode`, `DiagnosticData` (AOT-safe), `IErrorMessageProvider`, `ThrowCompiler`, `CodeException` | [README](src/Nantuc.Diagnostics/README.md) |
| `Nantuc.Kernel` | `Processor`, `MemoryBus`, `PortBus`, `ManagerDevices` (`TryAddDevice` → `IdState`), `Emulator`, `Device` (`Run`/`DebugRun` → `ProcessorFault`) | [README](src/Nantuc.Kernel/README.md) |
| `Nantuc.Compiler` | `Lexer`, `Parser`, AST, `FunctionGenerator`, `Assembler`, `ILAssembler`, `AssemblerParser`, `PeepholeOptimizer`, `AstOptimizer`, `const`-correctness | [README](src/Nantuc.Compiler/README.md) |
| `Nantuc.VmApplication` | `VmHost`, `DeviceContext`, `VmEmulator` (`TryRegisterDevice`/`TryGetDeviceContext`), `VmHostProject`, `CompilationResult`, `DeviceSpec` | [README](src/Nantuc.VmApplication/README.md) |
| `Nantuc.VmApplication.Extensions` | Disassembler, `Debugger`, `SynchronousIOStream`/`QueuedIOStream`, `AddStdRules`, `Libraries`, `RunAndKeep`/`RunOnce` | [README](src/Nantuc.VmApplication.Extensions/README.md) |

**Dependencies (bottom-up):** `Common` → `Contracts` → `Diagnostics` → `Kernel` → `Compiler` → `VmApplication` → `VmApplication.Extensions`.

**What to install by scenario:**

```bash
# Run a MiniC program (the rest is pulled transitively)
dotnet add package Nantuc.VmApplication
dotnet add package Nantuc.VmApplication.Extensions

# Compiler only, without runtime (no unsafe / NativeMemory)
dotnet add package Nantuc.Compiler
dotnet add package Nantuc.Diagnostics

# Custom device (implement IPortController)
dotnet add package Nantuc.Contracts
dotnet add package Nantuc.Kernel
```

The latest stable version is pulled by default; to pin a specific one — `--version 0.8.2`.

> `Nantuc.Common`, `Nantuc.Contracts`, `Nantuc.Diagnostics` do not pull `unsafe`/`NativeMemory`/`Regex` — they can be used in WASM/AOT projects and in environments where `unsafe` is prohibited.

## Requirements

- **.NET 10 SDK** (all projects target `net10.0`).
- **C# 14**: `extension` blocks, `[InlineArray]`, `params ReadOnlySpan`, `field` keyword, `FrozenDictionary`/`FrozenSet`, primary constructors.
- **Windows / Linux / macOS / AOT** (other platforms have not been tested, but Nantuc is not tied to a specific platform).
- **WASM**: `Common`/`Contracts`/`Diagnostics` are fully WASM-safe; `Compiler`, `Kernel`, `Extensions`, `Benchmarks` have limitations (blocking calls, linear memory limits, single-thread browser). Full analysis — [`docs/wasm.md`](docs/wasm.md).

## Build and run

```bash
# Build everything
dotnet build Nantuc.slnx -c Release

# Hello world
dotnet run --project Nantuc.Samples.Minimal -c Release

# Snake (controls: arrows, Q/Escape — exit)
dotnet run --project Nantuc.Samples.Scratch -c Release -snake 150

# Allocator test (default in Samples.Scratch)
dotnet run --project Nantuc.Samples.Scratch -c Release

# Manual benchmark (results in bin/.../logs/result_Benchmark_Logs_{ts}.txt)
dotnet run --project Nantuc.Benchmarks -c Release

# BenchmarkDotNet (a full run is long; filter by cases)
dotnet run --project Nantuc.Benchmarks.BenchmarkDotNet -c Release -- --filter *Call* *Memory*
```

BenchmarkDotNet artifacts — in `BenchmarkDotNet.Artifacts/`. IDE profile `Game` — from `launchSettings.json`.

## Examples

| Example | What it shows | README |
|---|---|---|
| `Nantuc.Samples.Minimal` | Hello world, `TryRegisterDevice` + `TryGetDeviceContext` + `ProcessorFault`, ports `0x10`/`0x12`, `QueuedIOStream` | [README](samples/Nantuc.Samples.Minimal/README.md) |
| `Nantuc.Samples.Scratch` | `SnakeGameOnNantuc`, `MonochromeMonitor` (32×16), HALT/WAKE loop, ANSI render; allocator test (`Libraries.alloc_minic`) | [README](samples/Nantuc.Samples.Scratch/README.md) |
| `Nantuc.Benchmarks` | Manual benchmark of 14 MiniC cases: MIPS, stability, percentiles, CV, JIT impact | [README](samples/Nantuc.Benchmarks/README.md) |
| `Nantuc.Benchmarks.BenchmarkDotNet` | BenchmarkDotNet benchmark of 14 MiniC cases: time, allocations, statistics, artifacts | [README](samples/Nantuc.Benchmarks.BenchmarkDotNet/README.md) |

Additional examples (full cycle with diagnostics, step-by-step debugging, a custom optimization, Pause/Resume) — in the READMEs of the corresponding modules (`VmApplication`, `VmApplication.Extensions`).

## Metrics and quality

- ~21,000 lines, 12 projects; core and API (excluding samples, tests, and benchmarks) — ~17,600 lines, 7 projects.
- Maintainability index: 80–97 (main modules).
- Full metrics table — [docs/METRICS.md](docs/METRICS.md).

**In progress / plans:**
- JIT compiler in C/C++ (separate project).
- **Debugger:** frame pointer in `Processor` for accurate `GetCallStack`, `Debugger.Reset()`/`Reload()`, UI panel for the IDE (via the `Stopped` event).

## Contributing

The project is open to issues and pull requests.

- **Found a bug** — open an issue with a minimal example (MiniC/NANASM + expected/actual behavior).
- **Want a feature** — open an issue describing the scenario (what you are doing, what blocks you).
- **Fix** — fork → branch → PR. For large changes, open an issue first (discuss the design).
- **Style**: follow the style of neighboring files; `Nullable=enable`, `ImplicitUsings=enable`; `unsafe` — only in `Nantuc.Kernel`.

Before a PR:

```bash
dotnet build Nantuc.slnx -c Release
dotnet test -c Release
```

## License

See [`LICENSE`](LICENSE) in the repository root.

## Links

- GitHub: [RomanyPe/Nantuc](https://github.com/RomanyPe/Nantuc)
- Author’s NuGet profile: [nuget.org/profiles/RomanyPe](https://www.nuget.org/profiles/RomanyPe)
- Changelog: [`CHANGELOG.md`](CHANGELOG.md)
- Bytecode specification: [`docs/bytecode.md`](docs/bytecode.md)
- Error code reference: [`docs/error-codes.md`](docs/error-codes.md)
- Metrics: [`docs/METRICS.md`](docs/METRICS.md)
- WASM limitations: [`docs/wasm.md`](docs/wasm.md)
- Built-in MiniC/NANASM libraries: [`src/Nantuc.VmApplication.Extensions/STDLIBRARIES.md`](src/Nantuc.VmApplication.Extensions/STDLIBRARIES.md)
- Module READMEs: see the “README” column in [Modules and NuGet](#modules-and-nuget)