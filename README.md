# Nantuc — README

> Platform for executing bytecode with its own MiniC language (almost full C), NANASM assembler, compiler, virtual machine, and public API for embedding.
> **Project version:** current project version is specified in `CHANGELOG.ru.md`, semver guaranteed.
> Written in C# (.NET 10). Compiler and runtime are separated by layers, unsafe code is isolated in the kernel, public API is a thin facade with safe `Try*` methods and `IdState` diagnostics.

Nantuc is a mini-framework that has everything needed to run a program in a C-like language:
lexer, parser, AST optimizer, code generator to bytecode, assembler (text and IL),
virtual machine with registers, memory and port bus, disassembler, debugger,
I/O devices and API for embedding into an application.
The repository contains a Hello world output in MiniC, a playable snake game in MiniC, an allocator test and two VM benchmarks.

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

- **Two languages**: MiniC (C-like: structures, pointers, arrays, `const`, `sizeof`, inline `asm`, `extern`, `#include`) and NANASM (assembler with labels and size suffixes).
- **Compiler from scratch**: lexer, recursive parser, AST, greedy register allocation, code generation to 32-bit bytecode, AST and peephole optimizations, `const`-correctness.
- **VM with isolated `unsafe`**: native RAM via `NativeMemory`, registers in `InlineArray` (no allocations per step), port bus with `address → (device, offset)` routing.
- **Safe API**: `TryRegisterDevice`/`TryGetDeviceContext` instead of `RegisterDevice(id < 0)` + `!`, `IdState` instead of magic `-1..-4`, `ProcessorFault` from `Run`/`DebugRun` instead of manual `IProcessorFaultPolicy`, `TryLoadProgram` instead of exceptions, `Pause()`/`Resume()` for control from another thread.
- **Devices as first-class**: VM itself is a device (`IPortController`) — you can nest VMs inside each other, connect UART, monitors, keyboards.
- **Public API without hacks**: `VmHost` / `DeviceContext` / `VmHostProject` — all unsafe and native buffers are hidden in `Nantuc.Kernel`.
- **Disassembler** with code and data sections, text and structural.
- **Debugger** (`Debugger`) on top of `DeviceContext`: `Step`/`StepOver`/`StepOut`/`Continue`/`ContinueTo`/`RunToEnd`, breakpoints, `RequestStop` from another thread, best-effort call stack, reading RAM (`ReadUInt16/32/64`, `ReadCString`), disassembling current instruction via `StringDisassembler`. Without `AsRawDevice` and `unsafe` — only public API of `DeviceContext`.
- **Standard optimizations** (AST and peephole) and infrastructure for your own rules.
- **Two benchmarks**: manual `Nantuc.Benchmarks` (MIPS, stability, percentiles, CV, JIT-impact) and `Nantuc.Benchmarks.BenchmarkDotNet` (BenchmarkDotNet, allocations, statistics, artifacts) on **14 MiniC cases**: calls, ALU/ILP, memory, bit operations, multiplication, branches, division, recursion, pointers, structures, sorting.

## Safe API

Public API of `Nantuc.VmApplication` follows the "Try + IdState + ProcessorFault" pattern:

| Scenario | Old (raw) path | Safe path |
|---|---|---|
| Device registration | `int id = RegisterDevice(...)` + check `id < 0` | `TryRegisterDevice(..., out int id)` → `IdState` |
| Getting `DeviceContext` | `GetDeviceContext(id)!` | `TryGetDeviceContext(id, out ctx)` |
| Program loading | `LoadProgram(...)` (throws) | `TryLoadProgram(..., out error)` |
| VM start | `Run()` → `void` | `Run(opt)` → `ProcessorFault` |
| Stop by limit | `RunWithLimit(...)` | `Run(new LaunchOptions(StepLimit: N))` |
| Pause/resume | — (only `Stop` existed) | `Pause()` / `Resume()` — queue-based, safe from another thread |
| Step-by-step execution | — | `Debugger` (in `VmApplication.Extensions`) on top of `RunStepMode`/`SingleStep` |

- **`IdState`** (`Nantuc.Common`) — `Unknown` (0, default) / `Valid` (1) / `NotAssigned` (-1) / `OutOfRange` (-2) / `NoFreeSlot` (-3) / `Rejected` (-4). Returned from `ManagerDevices.TryAddDevice` (and from `Emulator.RegisterDevice`/`Emulator.TryRegisterDevice`). `default(IdState) == Unknown`, so an uninitialized status is not perceived as successful; **do not rely on `default(IdState) == Valid`** — this has changed.
- **`ProcessorFault`** (`Nantuc.Common`) — `(BiosStatus Status, ulong FaultData, ulong InstructionPointer, OpCode OpCode)`. On success `Status == EndProgramm`; on failure — `DivOnZero`/`SegmentationFault`/… with exact `IP` and `OpCode`.
- **`IProcessorFaultPolicy`** — used **inside** VM to decide "whether to continue". Return from `Run`/`DebugRun` is already a `ProcessorFault`, separate policy is not needed.
- **`Debugger`** (`Nantuc.VmApplication.Extensions.Debugging`) — does not add new contracts to `Kernel`/`VmApplication`, works through `DeviceContext.RunStepMode`/`SingleStep`/`GetRegistersSnapshot`/`NativeReadOnlyBuffer`. Ships with `DebugStop`, `DebugStopReason`, `CallStackFrame`.

## Quick start

Minimal example — compiling and running `Hello world` in MiniC with output via UART, with safe checking of each step:

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

// 3) DeviceContext — via TryGetDeviceContext (without !)
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

// 6) Start — Run returns ProcessorFault, status can be checked
ProcessorFault fault = vm.Run(new LaunchOptions(ShowTimer: true));
if (fault.Status != BiosStatus.EndProgramm)
    Console.Error.WriteLine($"VM fault: {fault.Status} at IP=0x{fault.InstructionPointer:X}");
```

Detailed breakdown — in [`Nantuc.Samples.Minimal`](Nantuc.Samples.Minimal/README.ru.md).
## NuGet packages

All platform modules are published as NuGet packages (GitHub Packages, author — `RomanyPe`). Install only what the scenario needs; transitive dependencies are pulled automatically.

| Package | For whom | What's inside |
|---|---|---|
| `Nantuc.Common` | Compiler, extension, disassembler authors | Opcodes, registers, RAM/port sizes, `BiosStatus`, `ProcessorFault`, `IdState` (default-safe), instruction decoder |
| `Nantuc.Contracts` | Device and extension authors | `IPortBus`, `IPortUse`, `IPortController`, `ProcessorSnapshot`, `IProcessorFaultPolicy`, `OptimizationId` |
| `Nantuc.Diagnostics` | Compiler, IDE, CI authors | `ErrorCode`, `DiagnosticData` (AOT-safe), `IErrorMessageProvider`, `ThrowCompiler`, `CodeException` |
| `Nantuc.Kernel` | Embedding code, debugger author | `Processor`, `MemoryBus`, `PortBus`, `ManagerDevices`, `Emulator`, `Device` (`Run`/`DebugRun` → `ProcessorFault`) |
| `Nantuc.Compiler` | IDE author, optimization rule author | `Lexer`, `Parser`, AST, code generator, `Assembler`/`ILAssembler`, peephole/AST optimizations, `const`-correctness |
| `Nantuc.VmApplication` | End user of the platform | `VmHost`, `DeviceContext`, `VmEmulator` (`TryRegisterDevice`/`TryGetDeviceContext`), `VmHostProject`, `DeviceSpec` |
| `Nantuc.VmApplication.Extensions` | IDE author, game author, custom optimization author | Disassembler, `Debugger` (step, breakpoints, stack, RAM), `SynchronousIOStream`/`QueuedIOStream`, `AddStdRules`, `Libraries`, `RunAndKeep`/`RunOnce` |

**Dependencies (bottom-up):**
`Common` → `Contracts` → `Diagnostics` → `Kernel` → `Compiler` → `VmApplication` → `VmApplication.Extensions`.
`Contracts` depends only on `Common`; `Compiler` — on `Common`+`Contracts`+`Diagnostics`; `VmApplication` — the only module that knows about both compiler and runtime.

**What to install for your scenario:**

- **Run a MiniC program** — `Nantuc.VmApplication` + `Nantuc.VmApplication.Extensions` is enough (the rest will be pulled transitively).
- **Custom device (UART, monitor, keyboard)** — `Nantuc.Contracts` (implement `IPortController`) + `Nantuc.Kernel` (if `Emulator`/`PortBus` is needed), but better `Nantuc.VmApplication` + `Nantuc.VmApplication.Extensions`.
- **Custom IDE / debugger** — `Nantuc.VmApplication` + `Nantuc.VmApplication.Extensions` (`Debugger`, `Disassembler`).
- **Custom compiler/assembler without runtime** — `Nantuc.Common` + `Nantuc.Diagnostics` (no `unsafe`/`NativeMemory`).
- **Custom optimization rule** — `Nantuc.Compiler` + `Nantuc.Contracts` (`IAstOptimizationRule`/`IPeepholeRule`, `OptimizationId`).

> `Nantuc.Common`, `Nantuc.Contracts`, `Nantuc.Diagnostics` do not pull `unsafe`/`NativeMemory`/`Regex` — they can be used in WASM/AOT projects and in environments where `unsafe` is prohibited.

## Architecture

Layers go bottom-up. Each next one depends only on the previous ones, no reverse dependencies.

```
┌──────────────────────────────────────────────────────────────────────────────────┐
│  Nantuc.Samples.Minimal   Nantuc.Samples.Scratch                                 │
│  Nantuc.Benchmarks        Nantuc.Benchmarks.BenchmarkDotNet                      │
│  (examples, end products and benchmarks)                                         │
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
│   ProcessorFault, IdState)        ThrowCompiler)                                 │
└──────────────────────────────────────────────────────────────────────────────────┘
```

Note:
- **`Diagnostics` does not depend on any Nantuc project.** The compiler author does not pull the unsafe kernel for error codes.
- **The compiler does not depend on the runtime.** `Nantuc.Compiler` knows only about `Common` (ISA), `Contracts` (optimization types) and `Diagnostics` (errors). It does not see `Processor` or `MemoryBus`.
- **`IdState` lives in `Common`**, not in `VmApplication`: `Kernel` (where `ManagerDevices` is) cannot depend on `VmApplication`, and `Common` is the only layer visible to `Kernel`, `VmApplication`, and `Extensions`.
- **VMApplication is the only module that knows about both compiler and runtime.** It glues "compile" and "run" together.
- **The debugger lives in `VmApplication.Extensions`**, not in `VmApplication`/`Kernel`: it uses only the public API (`RunStepMode`/`SingleStep`/`GetRegistersSnapshot`/`NativeReadOnlyBuffer`) and does not require `AsRawDevice`/`unsafe`. This preserves the separation "the kernel knows nothing about the debugger UI".
- **Benchmarks live at the topmost layer.** Manual `Nantuc.Benchmarks` and `Nantuc.Benchmarks.BenchmarkDotNet` use the public API of `VmApplication`/`Extensions` and do not touch `Kernel` directly.

## Repository structure

```
Nantuc/
├── Nantuc.Common/                     # ISA, sizes, BiosStatus, ProcessorFault, IdState
├── Nantuc.Contracts/                  # public contracts (ports, processor)
├── Nantuc.Diagnostics/                # ErrorCode, DiagnosticData, ThrowCompiler, CodeException
├── Nantuc.Kernel/                     # CPU, RAM, PortBus, Device implementation
├── Nantuc.Compiler/                   # MiniC + NANASM (frontend, backend, ASM, optimizations)
├── Nantuc.VmApplication/              # main API (TryRegisterDevice, TryGetDeviceContext, Run → ProcessorFault)
├── Nantuc.VmApplication.Extensions/   # disassembler, debugger, I/O, optimizations, libraries
├── Nantuc.Samples.Minimal/            # Hello world
├── Nantuc.Samples.Scratch/            # allocator test + snake
├── Nantuc.Benchmarks/                 # manual VM benchmark (14 MiniC cases, MIPS/statistics)
└── Nantuc.Benchmarks.BenchmarkDotNet/ # BenchmarkDotNet benchmark (14 MiniC cases, artifacts)
```

## Modules

| Module | Purpose | For whom |
|---|---|---|
| [`Nantuc.Common`](Nantuc.Common/README.ru.md) | Opcodes, registers, RAM/port sizes, `BiosStatus`, `ProcessorFault`, `IdState` (`Unknown` = 0, default-safe), instruction decoder | Compiler, extension, disassembler authors |
| [`Nantuc.Contracts`](Nantuc.Contracts/README.ru.md) | `IPortBus`, `IPortUse`, `IPortController`, `ProcessorSnapshot`, `IProcessorFaultPolicy`, `OptimizationId` | Device and extension authors |
| [`Nantuc.Diagnostics`](Nantuc.Diagnostics/README.ru.md) | `ErrorCode`, `DiagnosticData`, `IErrorMessageProvider`, `ThrowCompiler`, `CodeException` | Compiler, IDE, CI authors |
| [`Nantuc.Kernel`](Nantuc.Kernel/README.ru.md) | `Processor`, `MemoryBus`, `PortBus`, `ManagerDevices` (`TryAddDevice` → `IdState`), `Emulator`, `Device` (`Run`/`DebugRun` → `ProcessorFault`) | Embedding code, debugger author |
| [`Nantuc.Compiler`](Nantuc.Compiler/README.ru.md) | `Lexer`, `Parser`, AST, `FunctionGenerator`, `Assembler`, `ILAssembler`, `AssemblerParser`, `PeepholeOptimizer`, `AstOptimizer`, `const`-correctness | IDE author, optimization rule author |
| [`Nantuc.VmApplication`](Nantuc.VmApplication/README.ru.md) | `VmHost`, `DeviceContext`, `VmEmulator` (`TryRegisterDevice`/`TryGetDeviceContext`), `VmHostProject`, `CompilationResult`, `DeviceSpec` | End user of the platform |
| [`Nantuc.VmApplication.Extensions`](Nantuc.VmApplication.Extensions/README.ru.md) | Disassembler, `Debugger` (step, breakpoints, stack, RAM), `SynchronousIOStream`/`QueuedIOStream`, `AddStdRules`, `Libraries`, `RunAndKeep`/`RunOnce` | IDE author, game author, custom optimization author |
| [`Nantuc.Samples.Minimal`](Nantuc.Samples.Minimal/README.ru.md) | Minimal Hello world | New user |
| [`Nantuc.Samples.Scratch`](Nantuc.Samples.Scratch/README.ru.md) | Allocator test + snake with monitor | Example of game loop and I/O |
| [`Nantuc.Benchmarks`](Nantuc.Benchmarks/README.ru.md) | Manual benchmark of 14 MiniC cases: MIPS, stability, percentiles, CV, JIT-impact | Core/compiler author |
| [`Nantuc.Benchmarks.BenchmarkDotNet`](Nantuc.Benchmarks.BenchmarkDotNet/README.ru.md) | BenchmarkDotNet benchmark of 14 MiniC cases: time, allocations, statistics, artifacts | Core/compiler author |

## Languages and ISA

### MiniC

C-like language. Supports:

- Types: `int`, `char`, `byte`, `ushort`, `short`, `ulong`, `long`, `uint`, `void`, `struct`.
- Pointers (`*`, `&`, `->`), arrays (including initializers `{...}` and string literals for `char[]`).
- `const` qualifiers: `const int`, `int * const`, `const int *`, `const int * const`, `const int arr[]`, `const` parameters (see "Constness" section in the compiler README).
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

Supported: `MOV`, `ADD`, `SUB`, `MULT_INT`, `DIV`, `MOD`, `SHR`, `SHL`, `AND`, `OR`, `XOR`, `NOT`, `INC`, `DEC`, `CMP`, `TEST`, `JMP`/`JZ`/`JNZ`/`JG`/`JL`, `CALL`/`RET`, `PUSH`/`POP`, `LOAD`/`STORE` and `*_IND` variants, `IN`/`OUT`, `HALT`/`WAKE`/`WAKE_INT`, `NOP`, `END`. Plus `.data`/`.text` sections and `.byte`, `.word`, `.dword`, `.quad`, `.ascii`, `.asciz`, `.zero` directives.

### ISA (brief)

32-bit instruction, little-endian:

```
bit:  31…20    | 19…18     | 17…13  | 12…8   | 7…0
      spare    | OpCodeSize| Reg2   | Reg1   | OpCode
```

- 8 bits per opcode, 5 bits per register (32 registers: `r0..r27`, `rFL`, `rSP`, `rHP`, `rIP`), 2 bits per size (`S8`/`S16`/`S32`/`S64`).
- Opcodes with immediate are padded with an 8-byte operand aligned to 8.
- Full specification — in [`Nantuc.Common`](Nantuc.Common/README.ru.md) and [`docs/bytecode.ru.md`](docs/bytecode.ru.md).

## Requirements

- **.NET 10 SDK**. All projects target `net10.0`.
- **C# 14**: `extension` blocks, `[InlineArray]`, `params ReadOnlySpan`, `field` keyword, `FrozenDictionary`/`FrozenSet`, primary constructors.
- **Windows / Linux / macOS / AoT** (other platforms have not been tested, but Nantuc is not tied to a specific platform). In WASM there are module limitations:
  - `Common` / `Contracts` / `Diagnostics` — fully WASM-safe (managed, no `unsafe`, no reflection). `DiagnosticData` and `TokenTypeExtension.AsText` are specifically made AOT-safe.
  - `Nantuc.Compiler` — `[GeneratedRegex]` in `AssemblerParser.GetRexExtensionAsm()` is AOT-safe; `MiniCLanguageDefinition.GetKeywordRegex()` — dead code with `RegexOptions.Compiled`, may be an AOT-blocker on browser-wasm. For strict AOT/WASM this path needs to be removed or replaced.
  - `Nantuc.Kernel` — `unsafe` and `NativeMemory.AllocZeroed`/`Free`/`Clear` are supported by WASM (emscripten linear memory), but:
    - `Device.Run`/`DebugRun` are synchronous and block the current thread (`ManualResetEventSlim.Wait(15)` when `IsSleeping`, `Thread.Sleep(delay)` in `DebugRun`) — in single-threaded browser this blocks the UI; a Web Worker or async loop is needed.
    - `NativeMemory.AllocZeroed` for large `RamSize` (≥ `MB512`, especially `GB1`/`GB2`) risks `OutOfMemoryException` due to WASM linear memory limit (~4 GiB per process, with fragmentation). Practical ceiling in browser is ~256 MiB.
  - `Nantuc.VmApplication.Extensions` — `Debugger` works, but `Continue`/`StepOver`/`StepOut`/`GetCallStack` are synchronous loops, block the current thread; for browser-WASM — Web Worker or `Task.Run` with `RequestStop`. `QueuedIOStream` in WASM requires explicit `Dispose`/`await`, otherwise the background drain may not finish.
  - `Nantuc.Benchmarks` — `Process`/P/Invoke, outside WASM; the manual benchmark also blocks on `Console.ReadLine()` at the end of `Main`, so it is not suitable for CI/headless.
  - `Nantuc.Benchmarks.BenchmarkDotNet` — desktop/CI tool, not for browser-WASM. In CI use `--filter` and time limit, otherwise a full run of 14 cases may take tens of minutes.

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

Or via IDE profile `Game` from `launchSettings.json`. Controls: arrows — movement, `Q`/`Escape` — exit.

**Run allocator test** (default in `Nantuc.Samples.Scratch`):

```bash
dotnet run --project Nantuc.Samples.Scratch -c Release
```

**Run manual benchmark** (Linux: `sudo` is needed for `PriorityClass.High`):

```bash
dotnet run --project Nantuc.Benchmarks -c Release
```

Results are saved to `Nantuc.Benchmarks/bin/.../logs/result_Benchmark_Logs_{timestamp}.txt`.

**Run BenchmarkDotNet benchmark:**

```bash
# Full run (may be long)
dotnet run --project Nantuc.Benchmarks.BenchmarkDotNet -c Release

# Only selected cases
dotnet run --project Nantuc.Benchmarks.BenchmarkDotNet -c Release -- --filter *Call* *Memory*
```

Artifacts are saved to `BenchmarkDotNet.Artifacts/`: Markdown/CSV/HTML reports and logs. For accurate numbers, replace `[ShortRunJob]` with `[MediumRunJob]`/`[LongRunJob]`.

## Examples

### Hello world (safe path)

See [`Nantuc.Samples.Minimal/README.ru.md`](Nantuc.Samples.Minimal/README.ru.md) — full breakdown with `TryRegisterDevice` + `TryGetDeviceContext` + `ProcessorFault`, explanation of ports `0x10`/`0x12` and `QueuedIOStream`.

### Snake

See [`Nantuc.Samples.Scratch/README.ru.md`](Nantuc.Samples.Scratch/README.ru.md) — `SnakeGameOnNantuc`, `MonochromeMonitor` (32×16), HALT/WAKE loop, ticker, ANSI render. Run via `dotnet run -- -snake 150`.

### Allocator test

See [`Nantuc.Samples.Scratch/README.ru.md`](Nantuc.Samples.Scratch/README.ru.md), `Program.cs` — `Libraries.alloc_minic` (first-fit + split + coalescing), check `return 10`, register dump and RAM disassembler.

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

dbg.AddBreakpoint(0x00);           // stop at main entry
dbg.Stopped += s => Console.WriteLine($"STOP: {s}");

// Step over CALL
var stop = dbg.StepOver();
Console.WriteLine($"  at: {dbg.CurrentInstruction}");

// Continue to breakpoint, but no more than 100k steps
stop = dbg.Continue(stepLimit: 100_000);
if (stop.Reason == DebugStopReason.Breakpoint)
    Console.WriteLine($"  hit bp at 0x{stop.InstructionPointer:X}");

// Continue to end, ignoring all breakpoints
var final = dbg.RunToEnd();

// Inspect RAM and registers
var regs = dbg.Registers;
Console.WriteLine($"r0={regs[RegType.r0]}, sp=0x{regs[RegType.rSP]:X}");
ulong counter = dbg.ReadUInt64(0x100);

foreach (var frame in dbg.GetCallStack())
    Console.WriteLine($"#{frame.Index} return=0x{frame.ReturnAddress:X8}");
```

**Interrupt a long run from another thread** (`RequestStop` — the only thread-safe method of `Debugger`):

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
- ~17,600 lines, 7 projects (core and API, without two examples, tests and two benchmarks)
- Maintainability index: 80–97 (main modules)
- Full metrics table: [docs/METRICS.md](docs/METRICS.md)
> **Module status** — in each module's README (section "What was fixed in this version" / "Module proximity to the user"). **Change history** — in [`CHANGELOG.ru.md`](CHANGELOG.ru.md). **Plans** — below.

**In progress / plans:**
- Writing a JIT compiler in `C` and `C++`, but this will be a separate project.
- Migrating `Libraries.alloc_minic` and `LibCode.snake_minic` from globals to locals (the code generator bug is fixed, the workaround is no longer required; see [STDLIBRARIES.ru.md](Nantuc.VmApplication.Extensions/STDLIBRARIES.ru.md)).
- **Debugger:** frame pointer in `Processor` for accurate `GetCallStack` (currently — best-effort heuristic over `[rSP, RamSize)`), `Debugger.Reset()`/`Reload()` for reusing one instance, UI panel for IDE (via `Stopped` event).

## Contributing

The project is open for issues and pull requests.

- **Found a bug** — issue with a minimal example (MiniC/NANASM + expected/actual behavior).
- **Want a feature** — issue with a scenario description (what you are doing, what is in the way).
- **Fix** — fork → branch → PR. For major changes, first an issue (discuss the design).
- **Style**: follow the style of neighboring files; `Nullable=enable`, `ImplicitUsings=enable`; `unsafe` — only in `Nantuc.Kernel`.

Before PR:
```bash
dotnet build Nantuc.sln -c Release
dotnet test -c Release
```

## License

See the [`LICENSE`](LICENSE) file in the repository root.

## Links

- GitHub: [RomanyPe/Nantuc](https://github.com/RomanyPe/Nantuc)
- Module documentation — README in each folder (links in the [Modules](#modules) section).
- Bytecode specification — [`docs/bytecode.ru.md`](docs/bytecode.ru.md).
- Error code reference — [`docs/error-codes.ru.md`](docs/error-codes.ru.md).
- Code metrics — [`docs/METRICS.md`](docs/METRICS.md).
- Built-in MiniC/NANASM libraries — [`Nantuc.VmApplication.Extensions/STDLIBRARIES.ru.md`](Nantuc.VmApplication.Extensions/STDLIBRARIES.ru.md).
- Debugger — [`Nantuc.VmApplication.Extensions/README.ru.md`](Nantuc.VmApplication.Extensions/README.ru.md#отладчик).
- Examples — [`Nantuc.Samples.Minimal`](Nantuc.Samples.Minimal/README.ru.md), [`Nantuc.Samples.Scratch`](Nantuc.Samples.Scratch/README.ru.md).
- Benchmarks — [`Nantuc.Benchmarks`](Nantuc.Benchmarks/README.ru.md), [`Nantuc.Benchmarks.BenchmarkDotNet`](Nantuc.Benchmarks.BenchmarkDotNet/README.ru.md).