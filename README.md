# Nantuc — README

[Русская версия](README.ru.md)

> A platform for executing bytecode with its own MiniC language (almost full C), NANASM assembler, compiler, virtual machine, and a public API for embedding.
> **Project version:** the current version is listed in `CHANGELOG.md`; semver is guaranteed.
> Written in C# (.NET 10). The compiler and runtime are separated into layers, unsafe code is isolated in the kernel, and the public API is a thin facade with safe `Try*` methods and `IdState` diagnostics.

Nantuc is a mini-framework that contains everything needed to run a program in a C-like language: lexer, parser, AST optimizer, bytecode code generator, assembler (text and IL), virtual machine with registers, memory, and a port bus, disassembler, debugger, I/O devices, and an API for embedding into an application.
The repository includes a Hello world in MiniC, a playable snake game in MiniC, an allocator test, and two VM benchmarks.

## Contents
- [Key features](#key-features)
- [Safe API](#safe-api)
- [Quick start](#quick-start)
- [NuGet packages](#nuget-packages)
- [Architecture](#architecture)
- [Repository structure](#repository-structure)
- [Modules](#modules)
- [Languages and ISA](#languages-and-isa)
- [Requirements](#requirements)
- [Build and run](#build-and-run)
- [Examples](#examples)
- [Metrics and quality](#metrics-and-quality)
- [Contributing](#contributing)
- [License](#license)
- [Links](#links)

## Key features

- **Two languages**: MiniC (C-like: structs, pointers, arrays, `const`, `sizeof`, inline `asm`, `extern`, `#include`) and NANASM (assembler with labels and size suffixes).
- **Compiler from scratch**: lexer, recursive parser, AST, greedy register allocation, code generation to 32-bit bytecode, AST and peephole optimizations, `const`-correctness.
- **VM with isolated `unsafe`**: native RAM via `NativeMemory`, registers in `InlineArray` (no allocations per step), port bus with `address → (device, offset)` routing.
- **Safe API**: `TryRegisterDevice`/`TryGetDeviceContext` instead of `RegisterDevice(id < 0)` + `!`, `IdState` instead of magic `-1..-4`, `ProcessorFault` from `Run`/`DebugRun` instead of manual `IProcessorFaultPolicy`, `TryLoadProgram` instead of exceptions, `Pause()`/`Resume()` for control from another thread.
- **Devices as first-class**: the VM itself is a device (`IPortController`) — VMs can be nested, UART, monitors, and keyboards can be connected.
- **Public API without hacks**: `VmHost` / `DeviceContext` / `VmHostProject` — all unsafe and native buffers are hidden in `Nantuc.Kernel`.
- **Disassembler** with code and data sections, textual and structural.
- **Debugger** (`Debugger`) on top of `DeviceContext`: `Step`/`StepOver`/`StepOut`/`Continue`/`ContinueTo`/`RunToEnd`, breakpoints, `RequestStop` from another thread, best-effort call stack, RAM reads (`ReadUInt16/32/64`, `ReadCString`), disassembling the current instruction via `StringDisassembler`. No `AsRawDevice` and no `unsafe` — only the public `DeviceContext` API.
- **Standard optimizations** (AST and peephole) and infrastructure for custom rules.
- **Two benchmarks**: manual `Nantuc.Benchmarks` (MIPS, stability, percentiles, CV, JIT impact) and `Nantuc.Benchmarks.BenchmarkDotNet` (BenchmarkDotNet, allocations, statistics, artifacts) on **14 MiniC cases**: calls, ALU/ILP, memory, bit operations, multiplication, branches, division, recursion, pointers, structs, sorting.

## Safe API

The public `Nantuc.VmApplication` API follows the "Try + IdState + ProcessorFault" pattern:

| Scenario | Old (raw) path | Safe path |
|---|---|---|
| Register a device | `int id = RegisterDevice(...)` + check `id < 0` | `TryRegisterDevice(..., out int id)` → `IdState` |
| Get `DeviceContext` | `GetDeviceContext(id)!` | `TryGetDeviceContext(id, out ctx)` |
| Load a program | `LoadProgram(...)` (throws) | `TryLoadProgram(..., out error)` |
| Run VM | `Run()` → `void` | `Run(opt)` → `ProcessorFault` |
| Stop by limit | `RunWithLimit(...)` | `Run(new LaunchOptions(StepLimit: N))` |
| Pause/resume | — (only `Stop` existed) | `Pause()` / `Resume()` — queue-based, safe from another thread |
| Single-step execution | — | `Debugger` (in `VmApplication.Extensions`) on top of `RunStepMode`/`SingleStep` |

- **`IdState`** (`Nantuc.Common`) — `Unknown` (0, default) / `Valid` (1) / `NotAssigned` (-1) / `OutOfRange` (-2) / `NoFreeSlot` (-3) / `Rejected` (-4). Returned from `ManagerDevices.TryAddDevice` (and from `Emulator.RegisterDevice`/`Emulator.TryRegisterDevice`). `default(IdState) == Unknown`, so an uninitialized status is not treated as success; **do not rely on `default(IdState) == Valid`** — this has changed.
- **`ProcessorFault`** (`Nantuc.Common`) — `(BiosStatus Status, ulong FaultData, ulong InstructionPointer, OpCode OpCode)`. On success `Status == EndProgramm`; on failure — `DivOnZero`/`SegmentationFault`/… with exact `IP` and `OpCode`.
- **`IProcessorFaultPolicy`** — used **inside** the VM to decide "continue or not". The return from `Run`/`DebugRun` is already `ProcessorFault`; a separate policy is not needed.
- **`Debugger`** (`Nantuc.VmApplication.Extensions.Debugging`) — does not add new contracts to `Kernel`/`VmApplication`, works through `DeviceContext.RunStepMode`/`SingleStep`/`GetRegistersSnapshot`/`NativeReadOnlyBuffer`. Ships with `DebugStop`, `DebugStopReason`, `CallStackFrame`.

## Quick start

Minimal example — compile and run `Hello world` in MiniC with output through UART, checking every step safely:

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

// 1) Register VM — via TryRegisterDevice, IdState instead of int < 0
var vmState = host.Emulator.TryRegisterDevice(
    DeviceSpec.Create(RamSize.MB128, name: "ConsoleVM"),
    sector: 0,
    out int vmId);
if (vmState != IdState.Valid) { Console.Error.WriteLine($"register vm: {vmState}"); return; }

// 2) Register UART — also via TryRegisterDevice
var uartState = host.Emulator.TryRegisterDevice(
    new QueuedIOStream(Console.Out, PortCharEncoding.Utf16),
    name: "console",
    sector: 1,
    out _);
if (uartState != IdState.Valid) { Console.Error.WriteLine($"register uart: {uartState}"); return; }

// 3) DeviceContext — via TryGetDeviceContext (no !)
if (!host.Emulator.TryGetDeviceContext(vmId, out var vm)) { Console.Error.WriteLine("no ctx"); return; }

// 4) Compilation
var compilation = host.Project.CompileToIL(baseAddress: 0, optimize: true).Build();
if (!compilation.Success)
{
    foreach (var e in compilation.Errors!) Console.Error.WriteLine(e);
    return;
}

// 5) Loading — via TryLoadProgram
if (!vm.TryLoadProgram(compilation.Program!, loadAddress: 0, out var error))
{
    Console.Error.WriteLine(error);
    return;
}

// 6) Run — Run returns ProcessorFault, status can be checked
ProcessorFault fault = vm.Run(new LaunchOptions(ShowTimer: true));
if (fault.Status != BiosStatus.EndProgramm)
    Console.Error.WriteLine($"VM fault: {fault.Status} at IP=0x{fault.InstructionPointer:X}");
```

Detailed walkthrough — in [`Nantuc.Samples.Minimal`](Nantuc.Samples.Minimal/README.md).

## NuGet packages

All platform modules are published as NuGet packages (GitHub Packages, author — `RomanyPe`). Install only what your scenario needs; transitive dependencies are pulled automatically.

| Package | For whom | What's inside |
|---|---|---|
| `Nantuc.Common` | Compiler, extension, disassembler authors | Opcodes, registers, RAM/port sizes, `BiosStatus`, `ProcessorFault`, `IdState` (default-safe), instruction decoder |
| `Nantuc.Contracts` | Device and extension authors | `IPortBus`, `IPortUse`, `IPortController`, `ProcessorSnapshot`, `IProcessorFaultPolicy`, `OptimizationId` |
| `Nantuc.Diagnostics` | Compiler, IDE, CI authors | `ErrorCode`, `DiagnosticData` (AOT-safe), `IErrorMessageProvider`, `ThrowCompiler`, `CodeException` |
| `Nantuc.Kernel` | Embedding code, debugger author | `Processor`, `MemoryBus`, `PortBus`, `ManagerDevices`, `Emulator`, `Device` (`Run`/`DebugRun` → `ProcessorFault`) |
| `Nantuc.Compiler` | IDE author, optimization rule author | `Lexer`, `Parser`, AST, code generator, `Assembler`/`ILAssembler`, peephole/AST optimizations, `const`-correctness |
| `Nantuc.VmApplication` | End user of the platform | `VmHost`, `DeviceContext`, `VmEmulator` (`TryRegisterDevice`/`TryGetDeviceContext`), `VmHostProject`, `DeviceSpec` |
| `Nantuc.VmApplication.Extensions` | IDE author, game author, custom optimization author | Disassembler, `Debugger` (step, breakpoints, stack, RAM), `SynchronousIOStream`/`QueuedIOStream`, `AddStdRules`, `Libraries`, `RunAndKeep`/`RunOnce` |

### Installation

Nantuc packages are published to **GitHub Packages**, not nuget.org. Add the source once; otherwise `dotnet add package` fails with `NU1101`.

```bash
dotnet nuget add source "https://nuget.pkg.github.com/RomanyPe/index.json" \
  --name github-nantuc \
  --username YOUR_GITHUB_USERNAME \
  --password YOUR_GITHUB_TOKEN \
  --store-password-in-clear-text
```

The token needs the `read:packages` scope.

Minimal set to run a MiniC program:

```bash
dotnet add package Nantuc.VmApplication
dotnet add package Nantuc.VmApplication.Extensions
```

Compiler only, no runtime (no `unsafe`/`NativeMemory`):

```bash
dotnet add package Nantuc.Compiler
dotnet add package Nantuc.Diagnostics
```

Own device (implement `IPortController`):

```bash
dotnet add package Nantuc.Contracts
dotnet add package Nantuc.Kernel
```

The latest stable version is pulled by default; pin a specific one with `--version 0.8.2`.
The full list and versions are on the package page in GitHub Packages.

**Dependencies (bottom-up):**
`Common` → `Contracts` → `Diagnostics` → `Kernel` → `Compiler` → `VmApplication` → `VmApplication.Extensions`.
`Contracts` depends only on `Common`; `Compiler` — on `Common`+`Contracts`+`Diagnostics`; `VmApplication` is the only module that knows both the compiler and the runtime.

**What to install for your scenario:**

- **Run a MiniC program** — `Nantuc.VmApplication` + `Nantuc.VmApplication.Extensions` is enough (the rest is pulled transitively).
- **Own device (UART, monitor, keyboard)** — `Nantuc.Contracts` (implement `IPortController`) + `Nantuc.Kernel` (if you need `Emulator`/`PortBus`), but preferably `Nantuc.VmApplication` + `Nantuc.VmApplication.Extensions`.
- **Own IDE / debugger** — `Nantuc.VmApplication` + `Nantuc.VmApplication.Extensions` (`Debugger`, `Disassembler`).
- **Own compiler/assembler without runtime** — `Nantuc.Common` + `Nantuc.Diagnostics` (no `unsafe`/`NativeMemory`).
- **Own optimization rule** — `Nantuc.Compiler` + `Nantuc.Contracts` (`IAstOptimizationRule`/`IPeepholeRule`, `OptimizationId`).

> `Nantuc.Common`, `Nantuc.Contracts`, `Nantuc.Diagnostics` do not pull `unsafe`/`NativeMemory`/`Regex` — they can be used in WASM/AOT projects and in environments where `unsafe` is prohibited.

## Architecture

The layers go bottom-up. Each next layer depends only on the previous ones; there are no reverse dependencies.

```
┌──────────────────────────────────────────────────────────────────────────────────┐
│  Nantuc.Samples.Minimal   Nantuc.Samples.Scratch                                 │
│  Nantuc.Benchmarks        Nantuc.Benchmarks.BenchmarkDotNet                      │
│  (samples, end products, and benchmarks)                                         │
└──────────────────────────────────────────────────────────────────────────────────┘
                              │
┌──────────────────────────────────────────────────────────────────────────────────┐
│  Nantuc.VmApplication.Extensions                                                 │
│  (disassembler, debugger, I/O, standard optimizations,                           │
│   libraries, compilation and run utilities)                                      │
└──────────────────────────────────────────────────────────────────────────────────┘
                              │
┌──────────────────────────────────────────────────────────────────────────────────┐
│  Nantuc.VmApplication                                                            │
│ (VmHost, DeviceContext, VmEmulator, VmHostProject, CompilationResult)            │
│  TryRegisterDevice / TryGetDeviceContext / Run → ProcessorFault                  │
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
│  (ISA, sizes, BiosStatus,      (ErrorCode, DiagnosticData,                       │
│   ProcessorFault, IdState)      ThrowCompiler)                                   │
└──────────────────────────────────────────────────────────────────────────────────┘
```

Note:
- **`Diagnostics` does not depend on any Nantuc project.** A compiler author does not pull the unsafe kernel just for error codes.
- **The compiler does not depend on the runtime.** `Nantuc.Compiler` knows only about `Common` (ISA), `Contracts` (optimization types), and `Diagnostics` (errors). It does not see `Processor` or `MemoryBus`.
- **`IdState` lives in `Common`**, not in `VmApplication`: `Kernel` (where `ManagerDevices` is) cannot depend on `VmApplication`, and `Common` is the only layer visible to `Kernel`, `VmApplication`, and `Extensions`.
- **VMApplication is the only module that knows both the compiler and the runtime.** It glues "compile" and "run" together.
- **The debugger lives in `VmApplication.Extensions`**, not in `VmApplication`/`Kernel`: it uses only the public API (`RunStepMode`/`SingleStep`/`GetRegistersSnapshot`/`NativeReadOnlyBuffer`) and does not require `AsRawDevice`/`unsafe`. This preserves the separation "the kernel knows nothing about the debugger UI".
- **Benchmarks live at the very top layer.** Manual `Nantuc.Benchmarks` and `Nantuc.Benchmarks.BenchmarkDotNet` use the public `VmApplication`/`Extensions` API and do not touch `Kernel` directly.

## Repository structure

```
Nantuc/
├── .github/
├── .gitignore
├── Directory.Build.props
├── README.md
├── README.ru.md
├── CHANGELOG.md
├── CHANGELOG.ru.md
├── LICENSE
├── Nantuc.slnx
│
├── src/
│   ├── Nantuc.Common/                 # ISA, sizes, BiosStatus, ProcessorFault, IdState
│   ├── Nantuc.Contracts/              # public contracts (ports, processor)
│   ├── Nantuc.Diagnostics/            # ErrorCode, DiagnosticData, ThrowCompiler, CodeException
│   ├── Nantuc.Kernel/                 # CPU, RAM, PortBus, Device implementation
│   ├── Nantuc.Compiler/               # MiniC + NANASM (frontend, backend, ASM, optimizations)
│   ├── Nantuc.VmApplication/          # main API (TryRegisterDevice, TryGetDeviceContext, Run → ProcessorFault)
│   └── Nantuc.VmApplication.Extensions/   # disassembler, debugger, I/O, optimizations, libraries
│
├── tests/
│   └── Nantuc.Tests/                  # ← placeholder name, replace with the actual one
│
├── samples/
│   ├── Nantuc.Samples.Minimal/        # Hello world
│   └── Nantuc.Samples.Scratch/        # allocator test + snake
│
└── benchmarks/
    ├── Nantuc.Benchmarks/                     # manual VM benchmark (14 MiniC cases, MIPS/statistics)
    └── Nantuc.Benchmarks.BenchmarkDotNet/     # BenchmarkDotNet benchmark (14 MiniC cases, artifacts)
```

## Modules

| Module | Purpose | For whom |
|---|---|---|
| [`Nantuc.Common`](Nantuc.Common/README.md) | Opcodes, registers, RAM/port sizes, `BiosStatus`, `ProcessorFault`, `IdState` (`Unknown` = 0, default-safe), instruction decoder | Compiler, extension, disassembler authors |
| [`Nantuc.Contracts`](Nantuc.Contracts/README.md) | `IPortBus`, `IPortUse`, `IPortController`, `ProcessorSnapshot`, `IProcessorFaultPolicy`, `OptimizationId` | Device and extension authors |
| [`Nantuc.Diagnostics`](Nantuc.Diagnostics/README.md) | `ErrorCode`, `DiagnosticData`, `IErrorMessageProvider`, `ThrowCompiler`, `CodeException` | Compiler, IDE, CI authors |
| [`Nantuc.Kernel`](Nantuc.Kernel/README.md) | `Processor`, `MemoryBus`, `PortBus`, `ManagerDevices` (`TryAddDevice` → `IdState`), `Emulator`, `Device` (`Run`/`DebugRun` → `ProcessorFault`) | Embedding code, debugger author |
| [`Nantuc.Compiler`](Nantuc.Compiler/README.md) | `Lexer`, `Parser`, AST, `FunctionGenerator`, `Assembler`, `ILAssembler`, `AssemblerParser`, `PeepholeOptimizer`, `AstOptimizer`, `const`-correctness | IDE author, optimization rule author |
| [`Nantuc.VmApplication`](Nantuc.VmApplication/README.md) | `VmHost`, `DeviceContext`, `VmEmulator` (`TryRegisterDevice`/`TryGetDeviceContext`), `VmHostProject`, `CompilationResult`, `DeviceSpec` | End user of the platform |
| [`Nantuc.VmApplication.Extensions`](Nantuc.VmApplication.Extensions/README.md) | Disassembler, `Debugger` (step, breakpoints, stack, RAM), `SynchronousIOStream`/`QueuedIOStream`, `AddStdRules`, `Libraries`, `RunAndKeep`/`RunOnce` | IDE author, game author, custom optimization author |
| [`Nantuc.Samples.Minimal`](Nantuc.Samples.Minimal/README.md) | Minimal Hello world | New user |
| [`Nantuc.Samples.Scratch`](Nantuc.Samples.Scratch/README.md) | Allocator test + snake with a monitor | Example of a game loop and I/O |
| [`Nantuc.Benchmarks`](Nantuc.Benchmarks/README.md) | Manual benchmark of 14 MiniC cases: MIPS, stability, percentiles, CV, JIT impact | Kernel/compiler author |
| [`Nantuc.Benchmarks.BenchmarkDotNet`](Nantuc.Benchmarks.BenchmarkDotNet/README.md) | BenchmarkDotNet benchmark of 14 MiniC cases: time, allocations, statistics, artifacts | Kernel/compiler author |

## Languages and ISA

### MiniC

A C-like language. Supports:

- Types: `int`, `char`, `byte`, `ushort`, `short`, `ulong`, `long`, `uint`, `void`, `struct`.
- Pointers (`*`, `&`, `->`), arrays (including initializers `{...}` and string literals for `char[]`).
- `const` qualifiers: `const int`, `int * const`, `const int *`, `const int * const`, `const int arr[]`, `const` parameters (see the "Constness" section in the compiler README).
- `if`/`else`, `while`, `for`, `do-while`, `break`, `continue`, `return`.
- `sizeof(type)` and `sizeof(expr)`.
- Inline assembler `asm { ... }`.
- `extern` for external functions (implemented in NANASM).
- `#include "file"` for C and ASM.
- `+=`, `-=`, `*=`, `/=`, `%=`, `&=`, `|=`, `^=`, `<<=`, `>>=`.
- Prefix/postfix `++`/`--`.

### NANASM

Assembler with labels and size suffixes for memory instructions:

```asm
start:
    LDI r0, 0x10
    LOAD.S64 r1, [r0]
    ADD r1, r1
    STORE.S64 [r0], r1
    END
```

Supported: `MOV`, `ADD`, `SUB`, `MULT_INT`, `DIV`, `MOD`, `SHR`, `SHL`, `AND`, `OR`, `XOR`, `NOT`, `INC`, `DEC`, `CMP`, `TEST`, `JMP`/`JZ`/`JNZ`/`JG`/`JL`, `CALL`/`RET`, `PUSH`/`POP`, `LOAD`/`STORE` and `*_IND` variants, `IN`/`OUT`, `HALT`/`WAKE`/`WAKE_INT`, `NOP`, `END`. Plus `.data`/`.text` sections and directives `.byte`, `.word`, `.dword`, `.quad`, `.ascii`, `.asciz`, `.zero`.

### ISA (brief)

32-bit instruction, little-endian:

```
bit:  31…20    | 19…18     | 17…13  | 12…8   | 7…0
      spare    | OpCodeSize| Reg2   | Reg1   | OpCode
```

- 8 bits for opcode, 5 bits per register (32 registers: `r0..r27`, `rFL`, `rSP`, `rHP`, `rIP`), 2 bits for size (`S8`/`S16`/`S32`/`S64`).
- Opcodes with immediate are followed by an 8-byte operand aligned to 8.
- Full specification — in [`Nantuc.Common`](Nantuc.Common/README.md) and [`docs/bytecode.md`](docs/bytecode.md).

## Requirements

- **.NET 10 SDK**. All projects target `net10.0`.
- **C# 14**: `extension` blocks, `[InlineArray]`, `params ReadOnlySpan`, `field` keyword, `FrozenDictionary`/`FrozenSet`, primary constructors.
- **Windows / Linux / macOS / AoT** (other platforms have not been tested, but Nantuc is not tied to a specific platform). In WASM there are module limitations:
  - `Common` / `Contracts` / `Diagnostics` — fully WASM-safe (managed, no `unsafe`, no reflection). `DiagnosticData` and `TokenTypeExtension.AsText` are specifically AOT-safe.
  - `Nantuc.Compiler` — `[GeneratedRegex]` in `AssemblerParser.GetRexExtensionAsm()` is AOT-safe; `MiniCLanguageDefinition.GetKeywordRegex()` — dead code with `RegexOptions.Compiled`, may be an AOT blocker on browser-wasm. For strict AOT/WASM this path must be removed or replaced.
  - `Nantuc.Kernel` — `unsafe` and `NativeMemory.AllocZeroed`/`Free`/`Clear` are supported by WASM (emscripten linear memory), but:
    - `Device.Run`/`DebugRun` are synchronous and block the current thread (`ManualResetEventSlim.Wait(15)` when `IsSleeping`, `Thread.Sleep(delay)` in `DebugRun`) — in single-threaded browser this blocks the UI; a Web Worker or async loop is needed.
    - `NativeMemory.AllocZeroed` for large `RamSize` (≥ `MB512`, especially `GB1`/`GB2`) risks `OutOfMemoryException` due to the WASM linear memory limit (~4 GiB per process, with fragmentation). The practical ceiling in the browser is ~256 MiB.
  - `Nantuc.VmApplication.Extensions` — `Debugger` works, but `Continue`/`StepOver`/`StepOut`/`GetCallStack` are synchronous loops and block the current thread; for browser-WASM use a Web Worker or `Task.Run` with `RequestStop`. `QueuedIOStream` in WASM requires explicit `Dispose`/`await`, otherwise the background drain may not finish.
  - `Nantuc.Benchmarks` — the manual benchmark also blocks on `Console.ReadLine()` at the end of `Main`, so it is not suitable for CI/headless.
  - `Nantuc.Benchmarks.BenchmarkDotNet` — a desktop/CI tool, not for browser-WASM. In CI use `--filter` and a time limit, otherwise a full run of 14 cases can take tens of minutes.

## Build and run

**Build everything:**

```bash
dotnet build Nantuc.sln -c Release
```

**Run Hello world:**

```bash
dotnet run --project Nantuc.Samples.Minimal -c Release
```

**Run snake** (`Nantuc.Samples.Scratch` — `-snake <period>`):

```bash
dotnet run --project Nantuc.Samples.Scratch -c Release -snake 150
```

Or via the IDE profile `Game` from `launchSettings.json`. Controls: arrows — movement, `Q`/`Escape` — exit.

**Run the allocator test** (default in `Nantuc.Samples.Scratch`):

```bash
dotnet run --project Nantuc.Samples.Scratch -c Release
```

**Run the manual benchmark**:

```bash
dotnet run --project Nantuc.Benchmarks -c Release
```

Results are saved to `Nantuc.Benchmarks/bin/.../logs/result_Benchmark_Logs_{timestamp}.txt`.

**Run the BenchmarkDotNet benchmark:**

```bash
# Full run (may be long)
dotnet run --project Nantuc.Benchmarks.BenchmarkDotNet -c Release

# Only selected cases
dotnet run --project Nantuc.Benchmarks.BenchmarkDotNet -c Release -- --filter *Call* *Memory*
```

Artifacts are saved to `BenchmarkDotNet.Artifacts/`: Markdown/CSV/HTML reports and logs. For accurate numbers, replace `[ShortRunJob]` with `[MediumRunJob]`/`[LongRunJob]`.

## Examples

### Hello world (safe path)

See [`Nantuc.Samples.Minimal/README.md`](Nantuc.Samples.Minimal/README.md) — a full walkthrough with `TryRegisterDevice` + `TryGetDeviceContext` + `ProcessorFault`, explanation of ports `0x10`/`0x12` and `QueuedIOStream`.

### Snake

See [`Nantuc.Samples.Scratch/README.md`](Nantuc.Samples.Scratch/README.md) — `SnakeGameOnNantuc`, `MonochromeMonitor` (32×16), HALT/WAKE loop, ticker, ANSI render. Run via `dotnet run -- -snake 150`.

### Allocator test

See [`Nantuc.Samples.Scratch/README.md`](Nantuc.Samples.Scratch/README.md), `Program.cs` — `Libraries.alloc_minic` (first-fit + split + coalescing), check `return 10`, register dump, and RAM disassembly.

### Full cycle with diagnostics

```csharp
using Nantuc.Common;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Extensions.Host;
using Nantuc.VmApplication.Project;

using var host = VmHostFactory.CreateDefault();

var sources = host.Project.GetSourceFiles();
var comp = host.Project.Compile(sources, baseAddress: 0, optimize: true);
if (!comp.Success) { foreach (var e in comp.Errors!) Console.Error.WriteLine(e); return; }

var state = host.Emulator.TryRegisterDevice(
    DeviceSpec.Create(RamSize.MB16, name: "main"), sector: 0, out int vmId);
if (state != IdState.Valid) { Console.Error.WriteLine($"register: {state}"); return; }

if (!host.Emulator.TryGetDeviceContext(vmId, out var vm)) return;
if (!vm.TryLoadProgram(comp.Program!, 0, out var error)) { Console.Error.WriteLine(error); return; }

ProcessorFault fault = vm.Run(new LaunchOptions(ShowTimer: true,
    OnEnd: r => Console.WriteLine($"steps={r?.Steps} elapsed={r?.Elapsed}")));

if (fault.Status != BiosStatus.EndProgramm)
    Console.Error.WriteLine($"fault {fault.Status} at IP=0x{fault.InstructionPointer:X} op={fault.OpCode}");
```

### Step-by-step debugging

```csharp
using Nantuc.Common;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Extensions.Debugging;
using Nantuc.VmApplication.Project;

using var host = VmHostFactory.CreateDefault();

var sources = host.Project.GetSourceFiles();
var comp = host.Project.Compile(sources, baseAddress: 0, optimize: false);
if (!comp.Success) { foreach (var e in comp.Errors!) Console.Error.WriteLine(e); return; }

var state = host.Emulator.TryRegisterDevice(
    DeviceSpec.Create(RamSize.MB16, name: "dbg"), sector: 0, out int vmId);
if (state != IdState.Valid) return;
if (!host.Emulator.TryGetDeviceContext(vmId, out var vm)) return;

using var dbg = new Debugger(vm);
dbg.Load(comp.Program!, loadAddress: 0, startAddress: 0);

dbg.AddBreakpoint(0x00);           // stop at entry to main
dbg.Stopped += s => Console.WriteLine($"STOP: {s}");

// Step with stepping over CALL
var stop = dbg.StepOver();
Console.WriteLine($"  at: {dbg.CurrentInstruction}");

// Continue to breakpoint, but no more than 100k steps
stop = dbg.Continue(stepLimit: 100_000);
if (stop.Reason == DebugStopReason.Breakpoint)
    Console.WriteLine($"  hit bp at 0x{stop.InstructionPointer:X}");

// Continue to the end, ignoring all breakpoints
var final = dbg.RunToEnd();

// Inspect RAM and registers
var regs = dbg.Registers;
Console.WriteLine($"r0={regs[RegType.r0]}, sp=0x{regs[RegType.rSP]:X}");
ulong counter = dbg.ReadUInt64(0x100);

foreach (var frame in dbg.GetCallStack())
    Console.WriteLine($"#{frame.Index} return=0x{frame.ReturnAddress:X8}");
```

**Abort a long run from another thread** (`RequestStop` — the only thread-safe method of `Debugger`):

```csharp
var runner = Task.Run(() => dbg.Continue());
Thread.Sleep(200);
dbg.RequestStop();                 // volatile bool
var stop = runner.Result;          // Reason == UserStop
```

### Custom optimization

```csharp
using Nantuc.VmApplication.Extensions.Optimizators;

host.Project.Optimizator.VmAstOptimizer.AddStdRules();
host.Project.Optimizator.VmPeepholeOptimizer.AddStdRules();
host.Project.Optimizator.VmAstOptimizer.Add(new MyCustomRule());
```

### Pause/Resume from another thread

```csharp
var runner = Task.Run(() => vm.Run());
Thread.Sleep(200);
vm.Pause();     // OpCode.HALT via external command queue
Thread.Sleep(100);
vm.Resume();    // OpCode.WAKE
runner.Wait();
```

## Metrics and quality

- ~21,000 lines, 12 projects
- ~17,600 lines, 7 projects (core and API, excluding two samples, tests, and two benchmarks)
- Maintainability index: 80–97 (main modules)
- Full metrics table: [docs/METRICS.md](docs/METRICS.md)

> **Module status** — in each module README (section "What was fixed in this version" / "Module proximity to the user"). **Change history** — in [`CHANGELOG.md`](CHANGELOG.md). **Plans** — below.

**In progress / plans:**
- Writing a JIT compiler in `C` and `C++`, but this will be a separate project.
- Migrating `Libraries.alloc_minic` and `LibCode.snake_minic` from globals to locals (the code generator bug is fixed, the workaround is no longer required; see [STDLIBRARIES.md](Nantuc.VmApplication.Extensions/STDLIBRARIES.md)).
- **Debugger:** frame pointer in `Processor` for accurate `GetCallStack` (currently a best-effort heuristic over `[rSP, RamSize)`), `Debugger.Reset()`/`Reload()` to reuse one instance, a UI panel for the IDE (via the `Stopped` event).

## Contributing

The project is open to issues and pull requests.

- **Found a bug** — issue with a minimal example (MiniC/NANASM + expected/actual behavior).
- **Want a feature** — issue with a scenario description (what you are doing, what blocks you).
- **Fix** — fork → branch → PR. For large changes, open an issue first (discuss the design).
- **Style**: follow the style of neighboring files; `Nullable=enable`, `ImplicitUsings=enable`; `unsafe` — only in `Nantuc.Kernel`.

Before a PR:
```bash
dotnet build Nantuc.slnx -c Release
dotnet test -c Release
```

## License

See the [`LICENSE`](LICENSE) file in the repository root.

## Links

- GitHub: [RomanyPe/Nantuc](https://github.com/RomanyPe/Nantuc)
- Module documentation — README in each folder (links in the [Modules](#modules) section).
- Bytecode specification — [`docs/bytecode.md`](docs/bytecode.md).
- Error code reference — [`docs/error-codes.md`](docs/error-codes.md).
- Code metrics — [`docs/METRICS.md`](docs/METRICS.md).
- Built-in MiniC/NANASM libraries — [`src/Nantuc.VmApplication.Extensions/STDLIBRARIES.md`](src/Nantuc.VmApplication.Extensions/STDLIBRARIES.md).
- Debugger — [`src/Nantuc.VmApplication.Extensions/README.md`](src/Nantuc.VmApplication.Extensions/README.md#debugger).
- Examples — [`samples/Nantuc.Samples.Minimal`](samples/Nantuc.Samples.Minimal/README.md), [`samples/Nantuc.Samples.Scratch`](samples/Nantuc.Samples.Scratch/README.md).
- Benchmarks — [`samples/Nantuc.Benchmarks`](samples/Nantuc.Benchmarks/README.md), [`samples/Nantuc.Benchmarks.BenchmarkDotNet`](samples/Nantuc.Benchmarks.BenchmarkDotNet/README.md).