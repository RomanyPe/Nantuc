# Nantuc — README

[Русская версия](README.ru/md)

> A platform for executing bytecode with its own MiniC language (almost full C), NANASM assembler, compiler, virtual machine, and public embedding API.
> **Project version:** specified in `CHANGELOG.md`, semver guaranteed.
> Written in C# (.NET 10). The compiler and runtime are separated into layers, `unsafe` code is isolated in the core, and the public API is a thin facade with safe `Try*` methods and `IdState` diagnostics.

Here is the combined description:

**Nantuc** is a mini-framework and safe low-level scripting engine for .NET, designed for cases where C-level control is needed but the code cannot be trusted. It includes a C-like MiniC language and the NANASM assembler (text and IR) with a disassembler and step-by-step debugging, as well as a full pipeline: lexer, parser, AST optimizer, bytecode generator, assembler, and a register VM with memory and a port bus. It includes I/O devices, explicit In/Out, an embedding API, a thin CLI (`nantuc`), and isolation so that a script crash does not crash the host.

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

Minimal example — compiling and running `Hello world` in MiniC with output via UART:

```csharp
using Nantuc.Common;
using Nantuc.VmApplication.Extensions.Host;

const string source =
    """
    #include "print.minic"
    char str[] = "Hello world";
    void main() { print16(str); }
    """;

using var host = VmHostFactory.CreateDefault();

host.Project.SaveFileInclude("io.nanasm", Libraries.io_nanasm);
host.Project.SaveFileInclude("print.minic", Libraries.print_minic);

var result = host.RunAndKeep(source, SourceLanguage.MiniC, output: Console.Out);

if (!result.Success)
    Console.Error.WriteLine(string.Join("\n", result.Diagnostics));
```

Detailed example:

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

var compilation = host.Project.CompileToIR(baseAddress: 0, optimize: true).Build();
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
if (fault.Status != BiosStatus.EndProgram)
    Console.Error.WriteLine($"VM fault: {fault.Status} at IP=0x{fault.InstructionPointer:X}");
```

A detailed walkthrough (explanation of ports `0x10`/`0x12`, `QueuedIOStream`, diagnostics) — in [`Nantuc.Samples.Minimal/README.md`](Nantuc.Samples.Minimal/README.md).

**The same via CLI (`nantuc`)** — the package is installed as a .NET global tool and covers the whole pipeline: `compile` → `run` → `disasm` + include management via `lib`.

```bash
# Install the CLI as a .NET global tool
dotnet tool install --global Nantuc.CLI

# Compile the project from the current directory into ./bin (with optimizations)
nantuc compile -s ./bin -o

# Run with a step limit and timer
nantuc run -p ./bin/program.bin -ram 8MB -l 500000 -t

# Disassemble the first 128 bytes into ./out/result-decompile.txt
nantuc disasm -f ./bin/program.bin -cs 128 -s ./out

# Add an include from a file and check that it exists
nantuc lib add -p ./print.minic
nantuc lib exist print.minic
```

Help for all commands — `nantuc help`. Details — in [`src/Nantuc.CLI/README.md`](src/Nantuc.CLI/README.md).

## Key features

- **Two languages**: MiniC (C-like: structs, pointers, arrays, `const`, `sizeof`, inline `asm`, `extern`, `#include`) and NANASM (assembler with labels and size suffixes). Specifications — in [`Nantuc.Compiler/README.md`](Nantuc.Compiler/README.md).
- **Compiler from scratch**: lexer, recursive parser, AST, greedy register allocation, code generation to 32-bit bytecode, AST and IR optimizations, `const`-correctness.
- **VM with isolated `unsafe`**: native RAM via `NativeMemory`, registers in `InlineArray` (no per-step allocations), port bus with routing `address → (device, offset)`.
- **Safe API**: `TryRegisterDevice`/`TryGetDeviceContext` instead of `id < 0` + `!`, `IdState` instead of magic `-1..-4`, `ProcessorFault` from `Run`/`DebugRun`, `TryLoadProgram` instead of exceptions, `Pause()`/`Resume()` for control from another thread.
- **Devices as first-class**: the VM itself is a device (`IPortController`) — VMs can be nested, UARTs, monitors, and keyboards can be connected.
- **Public API without hacks**: `VmHost` / `DeviceContext` / `VmHostProject` — all unsafe and native buffers are hidden in `Nantuc.Kernel`.
- **Disassembler and debugger**: code/data sections, text and structured output; `Debugger` on top of `DeviceContext` with `Step`/`StepOver`/`StepOut`/`Continue`/`RunToEnd`, breakpoints, `RequestStop` from another thread, best-effort call stack, RAM reading.
- **CLI `nantuc`**: a thin wrapper over the public API (`compile`, `run`, `disasm`, `lib`, `help`), without its own compilation/execution logic; installed as a .NET global tool (`Nantuc.CLI`).
- **Standard optimizations** (AST and IR) and infrastructure for custom rules.
- **Two benchmarks**: manual `Nantuc.Benchmarks` (MIPS, stability, percentiles, CV, JIT impact) and `Nantuc.Benchmarks.BenchmarkDotNet` on **14 MiniC cases**. Next to them — `OtherLangs`: the same 14 cases in Python, Lua, LuaJIT, Ruby, JS, C, C++, C#, Java, and Rust (separate runner, [`benchmarks/OtherLangs/README.md`](benchmarks/OtherLangs/README.md)).

## Safe API

The public API of `Nantuc.VmApplication` follows the “Try + IdState + ProcessorFault” pattern:

| Scenario | Old (raw) path | Safe path |
|---|---|---|
| Device registration | `int id = RegisterDevice(...)` + check `id < 0` | `TryRegisterDevice(..., out int id)` → `IdState` |
| Getting `DeviceContext` | `GetDeviceContext(id)!` | `TryGetDeviceContext(id, out ctx)` |
| Loading a program | `LoadProgram(...)` (throws) | `TryLoadProgram(..., out error)` |
| Running the VM | `Run()` → `void` | `Run(opt)` → `ProcessorFault` |
| Stop by limit | `RunWithLimit(...)` | `Run(new LaunchOptions(StepLimit: N))` |
| Pause/resume | — (previously only `Stop`) | `Pause()` / `Resume()` — queue-based, safe from another thread |
| Step-by-step execution | — | `Debugger` (in `VmApplication.Extensions`) on top of `RunStepMode`/`SingleStep` |

- **`IdState`** (`Nantuc.Common`) — `Unknown` (0, default) / `Valid` (1) / `NotAssigned` (-1) / `OutOfRange` (-2) / `NoFreeSlot` (-3) / `Rejected` (-4). `default(IdState) == Unknown`; **do not rely on `default(IdState) == Valid`** — this has changed.
- **`ProcessorFault`** (`Nantuc.Common`) — `(BiosStatus Status, ulong FaultData, ulong InstructionPointer, OpCode OpCode)`. On success `Status == EndProgram`; on failure — `DivOnZero`/`SegmentationFault`/… with exact `IP` and `OpCode`.
- **`IProcessorFaultPolicy`** — used **inside** the VM; the return from `Run`/`DebugRun` is already `ProcessorFault`, no separate policy is needed.
- **`Debugger`** (`Nantuc.VmApplication.Extensions.Debugging`) — does not add contracts to `Kernel`/`VmApplication`, works through the public API. Details — in [`Nantuc.VmApplication.Extensions/README.md`](src/Nantuc.VmApplication.Extensions/README.md#отладчик).

## Architecture

Layers go bottom-up. Each subsequent layer depends only on the previous ones; there are no reverse dependencies.

```
┌──────────────────────────────────────────────────────────────────────────────────┐
│  Nantuc.Samples.*   Nantuc.Benchmarks.*   Nantuc.CLI                             │
│  (examples, end products, benchmarks, console interface)                         │
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
│  Assembler / IRAssembler │    │   Device, ManagerDevices)                        │
└──────────────────────────┘    └──────────────────────────────────────────────────┘
              │                                │
┌──────────────────────────────────────────────────────────────────────────────────┐
│  Nantuc.Contracts                                                                │
│  (IPortBus, IPortUse, IPortController, ProcessorSnapshot, ...)                   │
└──────────────────────────────────────────────────────────────────────────────────┘
              │                                │
┌──────────────────────────────────────────────────────────────────────────────────┐
│  Nantuc.Common            Nantuc.Diagnostics                                     │
│  (ISA, sizes, BiosStatus,      (ErrorCode, DiagnosticData,                       │
│   ProcessorFault, IdState)        ThrowCompiler)                                 │
└──────────────────────────────────────────────────────────────────────────────────┘
```

Key invariants:
- **`Diagnostics` does not depend on any Nantuc project** — a compiler author does not pull in the unsafe core just for error codes.
- **The compiler does not depend on the runtime** — `Nantuc.Compiler` knows only `Common` + `Contracts` + `Diagnostics`.
- **`VmApplication` is the only module that knows about both the compiler and the runtime**; it glues together “compile” and “run”. The debugger, disassembler, and benchmarks live higher in the layers.
- **`Nantuc.CLI` is the top layer**: it contains no logic of its own, only parses arguments and calls `VmApplication`/`Extensions`.

## Modules and NuGet

All modules are published as NuGet packages on [nuget.org](https://www.nuget.org/profiles/RomanyPe) (author — `RomanyPe`). Install only what the scenario needs; transitive dependencies are pulled in automatically. Each module has its own README with details and examples.

| Package | Purpose | README |
|---|---|---|
| `Nantuc.Common` | Opcodes, registers, RAM/port sizes, `BiosStatus`, `ProcessorFault`, `IdState` (default-safe), decoder | [README](src/Nantuc.Common/README.md) |
| `Nantuc.Contracts` | `IPortBus`, `IPortUse`, `IPortController`, `ProcessorSnapshot`, `IProcessorFaultPolicy`, `OptimizationId` | [README](src/Nantuc.Contracts/README.md) |
| `Nantuc.Diagnostics` | `ErrorCode`, `DiagnosticData` (AOT-safe), `IErrorMessageProvider`, `ThrowCompiler`, `CodeException` | [README](src/Nantuc.Diagnostics/README.md) |
| `Nantuc.Kernel` | `Processor`, `MemoryBus`, `PortBus`, `ManagerDevices` (`TryAddDevice` → `IdState`), `Emulator`, `Device` (`Run`/`DebugRun` → `ProcessorFault`) | [README](src/Nantuc.Kernel/README.md) |
| `Nantuc.Compiler` | `Lexer`, `Parser`, AST, `FunctionGenerator`, `Assembler`, `IRAssembler`, `AssemblerParser`, `IROptimizer`, `AstOptimizer`, `const`-correctness | [README](src/Nantuc.Compiler/README.md) |
| `Nantuc.VmApplication` | `VmHost`, `DeviceContext`, `VmEmulator` (`TryRegisterDevice`/`TryGetDeviceContext`), `VmHostProject`, `CompilationResult`, `DeviceSpec` | [README](src/Nantuc.VmApplication/README.md) |
| `Nantuc.VmApplication.Extensions` | Disassembler, `Debugger`, `SynchronousIOStream`/`QueuedIOStream`, `AddStdRules`, `Libraries`, `RunAndKeep`/`RunOnce` | [README](src/Nantuc.VmApplication.Extensions/README.md) |
| `Nantuc.CLI` | CLI `nantuc`: `compile`, `run`, `disasm`, `lib`, `help`; installed as a .NET global tool | [README](src/Nantuc.CLI/README.md) |

**Dependencies (bottom-up):** `Common` → `Contracts` → `Diagnostics` → `Kernel` → `Compiler` → `VmApplication` → `VmApplication.Extensions` → `Nantuc.CLI`.

**What to install for a scenario:**

```bash
# Run a MiniC program (the rest will be pulled in transitively)
dotnet add package Nantuc.VmApplication
dotnet add package Nantuc.VmApplication.Extensions

# Compiler only, without the runtime (no unsafe / NativeMemory)
dotnet add package Nantuc.Compiler
dotnet add package Nantuc.Diagnostics

# Custom device (implement IPortController)
dotnet add package Nantuc.Contracts
dotnet add package Nantuc.Kernel

# CLI as a .NET global tool
dotnet tool install --global Nantuc.CLI
nantuc help
```

The latest stable version is pulled by default; to pin a specific one — `--version 0.8.2`.

> `Nantuc.Common`, `Nantuc.Contracts`, `Nantuc.Diagnostics` do not pull in `unsafe`/`NativeMemory`/`Regex` — they can be used in WASM/AOT projects and in environments where `unsafe` is prohibited.

## Requirements

- **.NET 10 SDK** (all projects target `net10.0`).
- **C# 14**: `extension` blocks, `[InlineArray]`, `params ReadOnlySpan`, `field` keyword, `FrozenDictionary`/`FrozenSet`, primary constructors.
- **Windows / Linux / macOS / AoT** (other platforms have not been tested, but Nantuc is not tied to a specific platform).
- **WASM**: `Common`/`Contracts`/`Diagnostics` — fully WASM-safe; `Compiler`, `Kernel`, `Extensions`, `Benchmarks`, `CLI` have limitations (blocking calls, linear memory limits, single-threaded browser). Full analysis — [`docs/wasm.md`](docs/wasm.md).

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

# CLI locally (without installing as a global tool)
dotnet run --project src/Nantuc.CLI -c Release -- compile -s ./bin -o
dotnet run --project src/Nantuc.CLI -c Release -- run -p ./bin/program.bin -l 100000 -t

# Manual benchmark (results in bin/.../logs/result_Benchmark_Logs_{ts}.txt)
dotnet run --project Nantuc.Benchmarks -c Release

# BenchmarkDotNet (full run is long; filter by cases)
dotnet run --project Nantuc.Benchmarks.BenchmarkDotNet -c Release -- --filter *Call* *Memory*
```

BenchmarkDotNet artifacts are in `BenchmarkDotNet.Artifacts/`. The IDE profile `Game` comes from `launchSettings.json`.

## Examples

| Example | What it shows | README |
|---|---|---|
| `Nantuc.Samples.Minimal` | Hello world, `TryRegisterDevice` + `TryGetDeviceContext` + `ProcessorFault`, ports `0x10`/`0x12`, `QueuedIOStream` | [README](samples/Nantuc.Samples.Minimal/README.md) |
| `Nantuc.Samples.Scratch` | `SnakeGameOnNantuc`, `MonochromeMonitor` (32×16), HALT/WAKE loop, ANSI rendering; allocator test (`Libraries.alloc_minic`) | [README](samples/Nantuc.Samples.Scratch/README.md) |
| `Nantuc.Benchmarks` | Manual benchmark of 14 MiniC cases: MIPS, stability, percentiles, CV, JIT impact | [README](benchmarks/Nantuc.Benchmarks/README.md) |
| `Nantuc.Benchmarks.BenchmarkDotNet` | BenchmarkDotNet benchmark of 14 MiniC cases: time, allocations, statistics, artifacts | [README](benchmarks/Nantuc.Benchmarks.BenchmarkDotNet/README.md) |
| `OtherLangs` | Comparison with 10 other languages on the same 14 MiniC cases: own process, own runner, consolidated `results.json`/`results.csv` | [README](benchmarks/OtherLangs/README.md) |
| `Nantuc.CLI` | `nantuc`: `compile`/`run`/`disasm`/`lib`/`help`, installation as a .NET global tool | [README](src/Nantuc.CLI/README.md) |

Additional examples (full cycle with diagnostics, step-by-step debugging, custom optimization, Pause/Resume) — in the READMEs of the corresponding modules (`VmApplication`, `VmApplication.Extensions`, `CLI`).

## Metrics and quality

- ~22,000 lines, 13 projects; core and API (excluding samples, tests, and benchmarks) — ~17,900 lines, 7 projects.
- End product `Nantuc.CLI` (console application): ~865 lines.
- Maintainability index: 80–97 (main modules).
- Full metrics table — [docs/METRICS.md](docs/METRICS.md).

**In progress / plans:**
- JIT compiler in C/C++ (separate project).
- **Debugger:** frame pointer in `Processor` for an accurate `GetCallStack`, `Debugger.Reset()`/`Reload()`, IDE UI panel (via the `Stopped` event).
- **CLI:** interactive REPL mode, project configuration file instead of flags for every command.
- For more details, see [ROADMAP.md](ROADMAP.md)
## Contributing

The project is open to issues and pull requests.

- **Found a bug** — issue with a minimal example (MiniC/NANASM + expected/actual behavior).
- **Want a feature** — issue describing the scenario (what you are doing, what is in the way).
- **Change** — fork → branch → PR. For large changes, open an issue first (to discuss the design).
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
- Author's NuGet profile: [nuget.org/profiles/RomanyPe](https://www.nuget.org/profiles/RomanyPe)
- Changelog: [`CHANGELOG.md`](CHANGELOG.md)
- Bytecode specification: [`docs/bytecode.md`](docs/bytecode.md)
- Error code reference: [`docs/error-codes.md`](docs/error-codes.md)
- Metrics: [`docs/METRICS.md`](docs/METRICS.md)
- WASM limitations: [`docs/wasm.md`](docs/wasm.md)
- Built-in MiniC/NANASM libraries: [`src/Nantuc.VmApplication.Extensions/STDLIBRARIES.md`](src/Nantuc.VmApplication.Extensions/STDLIBRARIES.md)
- CLI: [`src/Nantuc.CLI/README.md`](src/Nantuc.CLI/README.md)
- Comparison with other languages (OtherLangs): [`benchmarks/OtherLangs/README.md`](benchmarks/OtherLangs/README.md)
- Module READMEs: see the “README” column in the [Modules and NuGet](#modules-and-nuget) section.