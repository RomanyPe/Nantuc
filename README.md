# Nantuc — README

> A platform for executing bytecode with its own MiniC language (almost full C), NANASM assembler, compiler, virtual machine, and a public API for embedding.
> **Project version:** the current project version is specified in `CHANGELOG.ru.md`; semver is guaranteed.
> Written in C# (.NET 10). The compiler and runtime are separated into layers, unsafe code is isolated in the kernel, and the public API is a thin facade with safe `Try*` methods and `IdState` diagnostics.

Nantuc is a mini-framework that contains everything needed to run a program in a C-like language:
lexer, parser, AST optimizer, bytecode code generator, assembler (text and IL),
virtual machine with registers, memory, and a port bus, disassembler, debugger,
I/O devices, and an API for embedding into an application.
The repository contains a Hello world example in MiniC, a playable Snake game in MiniC, an allocator test, and two VM benchmarks.

## Code quality
- ~21,000 lines, 12 projects
- ~17,600 lines, 7 projects (core and API, excluding two samples, tests, and two benchmarks)
- Maintainability index: 80–97 (main modules)
- Full metrics table: [docs/METRICS.md](docs/METRICS.md)


## Contents
- [Key features](#key-features)
- [Safe API](#safe-api)
- [Quick start](#quick-start)
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
- **Compiler from scratch**: lexer, recursive parser, AST, greedy register allocation, code generation into 32-bit bytecode, AST and peephole optimizations, `const` correctness.
- **VM with isolated `unsafe`**: native RAM via `NativeMemory`, registers in `InlineArray` (no allocations per step), port bus with `address → (device, offset)` routing.
- **Safe API**: `TryRegisterDevice`/`TryGetDeviceContext` instead of `RegisterDevice(id < 0)` + `!`, `IdState` instead of magic `-1..-4`, `ProcessorFault` from `Run`/`DebugRun` instead of a manual `IProcessorFaultPolicy`, `TryLoadProgram` instead of exceptions, `Pause()`/`Resume()` for control from another thread.
- **Devices as first-class**: the VM itself is a device (`IPortController`) — VMs can be nested inside each other, and UARTs, monitors, and keyboards can be connected.
- **Public API without hacks**: `VmHost` / `DeviceContext` / `VmHostProject` — all unsafe and native buffers are hidden in `Nantuc.Kernel`.
- **Disassembler** with code and data sections, text and structured.
- **Debugger** (`Debugger`) on top of `DeviceContext`: `Step`/`StepOver`/`StepOut`/`Continue`/`ContinueTo`/`RunToEnd`, breakpoints, `RequestStop` from another thread, best-effort call stack, RAM reads (`ReadUInt16/32/64`, `ReadCString`), disassembly of the current instruction via `StringDisassembler`. No `AsRawDevice` or `unsafe` — only the public `DeviceContext` API.
- **Standard optimizations** (AST and peephole) and infrastructure for custom rules.
- **Two benchmarks**: manual `Nantuc.Benchmarks` (MIPS, stability, percentiles, CV, JIT impact) and `Nantuc.Benchmarks.BenchmarkDotNet` (BenchmarkDotNet, allocations, statistics, artifacts) on **14 MiniC cases**: calls, ALU/ILP, memory, bit operations, multiplication, branches, division, recursion, pointers, structs, sorting.

## Safe API

The public `Nantuc.VmApplication` API follows the “Try + IdState + ProcessorFault” pattern:

| Scenario | Old (raw) path | Safe path |
|---|---|---|
| Device registration | `int id = RegisterDevice(...)` + check `id < 0` | `TryRegisterDevice(..., out int id)` → `IdState` |
| Getting `DeviceContext` | `GetDeviceContext(id)!` | `TryGetDeviceContext(id, out ctx)` |
| Program loading | `LoadProgram(...)` (throws) | `TryLoadProgram(..., out error)` |
| Running the VM | `Run()` → `void` | `Run(opt)` → `ProcessorFault` |
| Stopping by limit | `RunWithLimit(...)` | `Run(new LaunchOptions(StepLimit: N))` |
| Pause/resume | — (only `Stop` existed) | `Pause()` / `Resume()` — queue-based, safe from another thread |
| Step-by-step execution | — | `Debugger` (in `VmApplication.Extensions`) on top of `RunStepMode`/`SingleStep` |

- **`IdState`** (`Nantuc.Common`) — `Unknown` (0, default) / `Valid` (1) / `NotAssigned` (-1) / `OutOfRange` (-2) / `NoFreeSlot` (-3) / `Rejected` (-4). Returned from `ManagerDevices.TryAddDevice` (and from `Emulator.RegisterDevice`/`Emulator.TryRegisterDevice`). `default(IdState) == Unknown` so that an uninitialized status is not perceived as success; **do not rely on `default(IdState) == Valid`** — this has changed.
- **`ProcessorFault`** (`Nantuc.Common`) — `(BiosStatus Status, ulong FaultData, ulong InstructionPointer, OpCode OpCode)`. On success `Status == EndProgramm`; on failure — `DivOnZero`/`SegmentationFault`/… with the exact `IP` and `OpCode`.
- **`IProcessorFaultPolicy`** — used **inside** the VM to decide “whether to continue”. The return from `Run`/`DebugRun` is already a `ProcessorFault`; a separate policy is not needed.
- **`Debugger`** (`Nantuc.VmApplication.Extensions.Debugging`) — does not add new contracts to `Kernel`/`VmApplication`; it works through `DeviceContext.RunStepMode`/`SingleStep`/`GetRegistersSnapshot`/`NativeReadOnlyBuffer`. It ships with `DebugStop`, `DebugStopReason`, and `CallStackFrame`.

## Quick start

Minimal example — compile and run `Hello world` in MiniC with output via UART, with safe checks at every step:

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

// 1) Register the VM — via TryRegisterDevice, IdState instead of int < 0
var vmState = host.Emulator.TryRegisterDevice(
    DeviceSpec.Create(RamSize.MB128, name: "ConsoleVM"),
    sector: 0,
    out int vmId);
if (vmState != IdState.Valid) { Console.Error.WriteLine($"register vm: {vmState}"); return; }

// 2) Register the UART — also via TryRegisterDevice
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

// 6) Running — Run returns ProcessorFault, so the status can be checked
ProcessorFault fault = vm.Run(new LaunchOptions(ShowTimer: true));
if (fault.Status != BiosStatus.EndProgramm)
    Console.Error.WriteLine($"VM fault: {fault.Status} at IP=0x{fault.InstructionPointer:X}");
```

Detailed walkthrough — in [`Nantuc.Samples.Minimal`](Nantuc.Samples.Minimal/README.ru.md).

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
- **`Diagnostics` does not depend on any Nantuc project.** A compiler author does not pull in the unsafe kernel just for error codes.
- **The compiler does not depend on the runtime.** `Nantuc.Compiler` knows only about `Common` (ISA), `Contracts` (optimization types), and `Diagnostics` (errors). It sees neither `Processor` nor `MemoryBus`.
- **`IdState` lives in `Common`**, not in `VmApplication`: `Kernel` (where `ManagerDevices` lives) cannot depend on `VmApplication`, and `Common` is the only layer visible to `Kernel`, `VmApplication`, and `Extensions`.
- **VmApplication is the only module that knows about both the compiler and the runtime.** It glues “compile” and “run” together.
- **The debugger lives in `VmApplication.Extensions`**, not in `VmApplication`/`Kernel`: it uses only the public API (`RunStepMode`/`SingleStep`/`GetRegistersSnapshot`/`NativeReadOnlyBuffer`) and does not require `AsRawDevice`/`unsafe`. This preserves the separation “the kernel knows nothing about the debugger UI”.
- **Benchmarks live at the very top layer.** The manual `Nantuc.Benchmarks` and `Nantuc.Benchmarks.BenchmarkDotNet` use the public `VmApplication`/`Extensions` API and do not touch `Kernel` directly.

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
├── Nantuc.Samples.Scratch/            # allocator test + Snake game
├── Nantuc.Benchmarks/                 # manual VM benchmark (14 MiniC cases, MIPS/statistics)
└── Nantuc.Benchmarks.BenchmarkDotNet/ # BenchmarkDotNet benchmark (14 MiniC cases, artifacts)
```

## Modules

| Module | Purpose | For whom |
|---|---|---|
| [`Nantuc.Common`](Nantuc.Common/README.ru.md) | Opcodes, registers, RAM/port sizes, `BiosStatus`, `ProcessorFault`, `IdState` (`Unknown` = 0, default-safe), instruction decoder | Compiler, extension, and disassembler authors |
| [`Nantuc.Contracts`](Nantuc.Contracts/README.ru.md) | `IPortBus`, `IPortUse`, `IPortController`, `ProcessorSnapshot`, `IProcessorFaultPolicy`, `OptimizationId` | Device and extension authors |
| [`Nantuc.Diagnostics`](Nantuc.Diagnostics/README.ru.md) | `ErrorCode`, `DiagnosticData`, `IErrorMessageProvider`, `ThrowCompiler`, `CodeException` | Compiler, IDE, and CI authors |
| [`Nantuc.Kernel`](Nantuc.Kernel/README.ru.md) | `Processor`, `MemoryBus`, `PortBus`, `ManagerDevices` (`TryAddDevice` → `IdState`), `Emulator`, `Device` (`Run`/`DebugRun` → `ProcessorFault`) | Embedding code, debugger authors |
| [`Nantuc.Compiler`](Nantuc.Compiler/README.ru.md) | `Lexer`, `Parser`, AST, `FunctionGenerator`, `Assembler`, `ILAssembler`, `AssemblerParser`, `PeepholeOptimizer`, `AstOptimizer`, `const` correctness | IDE authors, optimization rule authors |
| [`Nantuc.VmApplication`](Nantuc.VmApplication/README.ru.md) | `VmHost`, `DeviceContext`, `VmEmulator` (`TryRegisterDevice`/`TryGetDeviceContext`), `VmHostProject`, `CompilationResult`, `DeviceSpec` | End users of the platform |
| [`Nantuc.VmApplication.Extensions`](Nantuc.VmApplication.Extensions/README.ru.md) | Disassembler, `Debugger` (step, breakpoints, stack, RAM), `SynchronousIOStream`/`QueuedIOStream`, `AddStdRules`, `Libraries`, `RunAndKeep`/`RunOnce` | IDE authors, game authors, custom optimization authors |
| [`Nantuc.Samples.Minimal`](Nantuc.Samples.Minimal/README.ru.md) | Minimal Hello world | New users |
| [`Nantuc.Samples.Scratch`](Nantuc.Samples.Scratch/README.ru.md) | Allocator test + Snake game with a monitor | Example of a game loop and I/O |
| [`Nantuc.Benchmarks`](Nantuc.Benchmarks/README.ru.md) | Manual benchmark of 14 MiniC cases: MIPS, stability, percentiles, CV, JIT impact | Kernel/compiler authors |
| [`Nantuc.Benchmarks.BenchmarkDotNet`](Nantuc.Benchmarks.BenchmarkDotNet/README.ru.md) | BenchmarkDotNet benchmark of 14 MiniC cases: time, allocations, statistics, artifacts | Kernel/compiler authors |

## Languages and ISA

### MiniC

A C-like language. Supports:

- Types: `int`, `char`, `byte`, `ushort`, `short`, `ulong`, `long`, `uint`, `void`, `struct`.
- Pointers (`*`, `&`, `->`), arrays (including initializers `{...}` and string literals for `char[]`).
- `const` qualifiers: `const int`, `int * const`, `const int *`, `const int * const`, `const int arr[]`, `const` parameters (see the “Constness” section in the compiler README).
- `if`/`else`, `while`, `for`, `do-while`, `break`, `continue`, `return`.
- `sizeof(type)` and `sizeof(expr)`.
- Inline assembler `asm { ... }`.
- `extern` for external functions (implemented in NANASM).
- `#include "file"` for C and ASM.
- `+=`, `-=`, `*=`, `/=`, `%=`, `&=`, `|=`, `^=`, `<<=`, `>>=`.
- Prefix/postfix `++`/`--`.

### NANASM

An assembler with labels and size suffixes for memory instructions:

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

- 8 bits per opcode, 5 bits per register (32 registers: `r0..r27`, `rFL`, `rSP`, `rHP`, `rIP`), 2 bits for size (`S8`/`S16`/`S32`/`S64`).
- Opcodes with an immediate are followed by an 8-byte operand aligned to 8.
- Full specification — in [`Nantuc.Common`](Nantuc.Common/README.ru.md) and [`docs/bytecode.ru.md`](docs/bytecode.ru.md).

## Requirements

- **.NET 10 SDK**. All projects target `net10.0`.
- **C# 14**: `extension` blocks, `[InlineArray]`, `params ReadOnlySpan`, `field` keyword, `FrozenDictionary`/`FrozenSet`, primary constructors.
- **Windows / Linux / macOS / AoT** (other platforms have not been tested, but Nantuc is not tied to a specific platform). In WASM there are module-specific limitations:
  - `Common` / `Contracts` / `Diagnostics` — fully WASM-safe (managed, no `unsafe`, no reflection). `DiagnosticData` and `TokenTypeExtension.AsText` are specifically AOT-safe.
  - `Nantuc.Compiler` — `[GeneratedRegex]` in `AssemblerParser.GetRexExtensionAsm()` is AOT-safe; `MiniCLanguageDefinition.GetKeywordRegex()` is dead code with `RegexOptions.Compiled` and may be an AOT blocker on browser-wasm. For strict AOT/WASM this path must be removed or replaced.
  - `Nantuc.Kernel` — `unsafe` and `NativeMemory.AllocZeroed`/`Free`/`Clear` are supported in WASM (emscripten linear memory), but:
    - `Device.Run`/`DebugRun` are synchronous and block the current thread (`ManualResetEventSlim.Wait(15)` when `IsSleeping`, `Thread.Sleep(delay)` in `DebugRun`) — in single-threaded browser this blocks the UI; a Web Worker or async loop is needed.
    - `NativeMemory.AllocZeroed` for large `RamSize` (≥ `MB512`, especially `GB1`/`GB2`) risks `OutOfMemoryException` due to the WASM linear memory limit (~4 GiB per process, with fragmentation). The practical ceiling in the browser is ~256 MiB.
  - `Nantuc.VmApplication.Extensions` — `Debugger` works, but `Continue`/`StepOver`/`StepOut`/`GetCallStack` are synchronous loops and block the current thread; for browser-WASM use a Web Worker or `Task.Run` with `RequestStop`. `QueuedIOStream` in WASM requires an explicit `Dispose`/`await`; otherwise the background drain may not complete.
  - `Nantuc.Benchmarks` — `Process`/P/Invoke, outside WASM; the manual benchmark also blocks on `Console.ReadLine()` at the end of `Main`, so it is not suitable for CI/headless.
  - `Nantuc.Benchmarks.BenchmarkDotNet` — a desktop/CI tool, not for browser-WASM. In CI use `--filter` and a time limit; otherwise a full run of 14 cases may take tens of minutes.

## Build and run

**Build everything:**

```bash
dotnet build Nantuc.sln -c Release
```

**Run Hello world:**

```bash
dotnet run --project Nantuc.Samples.Minimal -c Release
```

**Run Snake** (`Nantuc.Samples.Scratch` — `-snake <period>`):

```bash
dotnet run --project Nantuc.Samples.Scratch -c Release -snake 150
```

Or via the IDE profile `Game` from `launchSettings.json`. Controls: arrows — movement, `Q`/`Escape` — exit.

**Run the allocator test** (default in `Nantuc.Samples.Scratch`):

```bash
dotnet run --project Nantuc.Samples.Scratch -c Release
```

**Run the manual benchmark** (Linux: `sudo` is required for `PriorityClass.High`):

```bash
dotnet run --project Nantuc.Benchmarks -c Release
```

Results are saved to `Nantuc.Benchmarks/bin/.../logs/result_Benchmark_Logs_{timestamp}.txt`.

**Run the BenchmarkDotNet benchmark:**

```bash
# Full run (may take a long time)
dotnet run --project Nantuc.Benchmarks.BenchmarkDotNet -c Release

# Only selected cases
dotnet run --project Nantuc.Benchmarks.BenchmarkDotNet -c Release -- --filter *Call* *Memory*
```

Artifacts are saved to `BenchmarkDotNet.Artifacts/`: Markdown/CSV/HTML reports and logs. For accurate numbers, replace `[ShortRunJob]` with `[MediumRunJob]`/`[LongRunJob]`.

## Examples

### Hello world (safe path)

See [`Nantuc.Samples.Minimal/README.ru.md`](Nantuc.Samples.Minimal/README.ru.md) — a full walkthrough with `TryRegisterDevice` + `TryGetDeviceContext` + `ProcessorFault`, and an explanation of ports `0x10`/`0x12` and `QueuedIOStream`.

### Snake

See [`Nantuc.Samples.Scratch/README.ru.md`](Nantuc.Samples.Scratch/README.ru.md) — `SnakeGameOnNantuc`, `MonochromeMonitor` (32×16), HALT/WAKE loop, ticker, ANSI rendering. Run via `dotnet run -- -snake 150`.

### Allocator test

See [`Nantuc.Samples.Scratch/README.ru.md`](Nantuc.Samples.Scratch/README.ru.md), `Program.cs` — `Libraries.alloc_minic` (first-fit + split + coalescing), `return 10` check, register dump, and RAM disassembler.

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

// Continue to the end, ignoring all breakpoints
var final = dbg.RunToEnd();

// Inspect RAM and registers
var regs = dbg.Registers;
Console.WriteLine($"r0={regs[RegType.r0]}, sp=0x{regs[RegType.rSP]:X}");
ulong counter = dbg.ReadUInt64(0x100);

foreach (var frame in dbg.GetCallStack())
    Console.WriteLine($"#{frame.Index} return=0x{frame.ReturnAddress:X8}");
```

**Abort a long run from another thread** (`RequestStop` is the only thread-safe `Debugger` method):

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
vm.Pause();     // OpCode.HALT via the external command queue
Thread.Sleep(100);
vm.Resume();    // OpCode.WAKE
runner.Wait();
```

## Metrics and quality

**Done:**
- MiniC (lexer, parser, AST, code generation, structs, pointers, arrays, `const` correctness, inline asm, `#include`, `extern`).
- NANASM (text assembler, size suffixes, labels, `.data`/`.text` sections).
- VM (registers, RAM, stack, heap, ports, HALT/WAKE, external commands, fault policy).
- Devices: UART (sync/async/numeric), monitor (32×16), keyboard.
- Disassembler (text and structured; `Disassembler` is public).
- **Debugger** (`Debugger` in `VmApplication.Extensions`): `Step`/`StepOver`/`StepOut`/`Continue`/`ContinueTo`/`RunToEnd`, breakpoints, `RequestStop` from another thread, `Wake` from `HALT`, best-effort call stack, RAM reads (`ReadUInt16/32/64`, `ReadCString`), disassembly of the current instruction. No `AsRawDevice`/`unsafe`.
- Standard AST and peephole optimizations.
- Safe public API: `TryRegisterDevice`, `TryGetDeviceContext`, `TryLoadProgram`, `Run`/`DebugRun` → `ProcessorFault`, `Pause()`/`Resume()`, `IdState` (`default(IdState) == Unknown`, not `Valid`).
- Public API (`VmHost`, `DeviceContext`, `VmHostProject`, `DeviceSpec`).
- Examples: Hello world, Snake, allocator test.
- **Two benchmarks**: manual `Nantuc.Benchmarks` (14 MiniC cases, MIPS, percentiles, CV, JIT impact) and `Nantuc.Benchmarks.BenchmarkDotNet` (14 MiniC cases, BenchmarkDotNet, allocations, artifacts).
- AOT-friendly diagnostics: `DiagnosticData` (no boxing, no `Enum.ToString()`), `TokenTypeExtension.AsText` (manual switch), `[GeneratedRegex]` in `AssemblerParser.GetRexExtensionAsm()`.
- A sandbox is partially implemented: out of the box, the Nantuc VM runs only in a fully isolated sandbox — it can send data and receive it on request via `IN`/`OUT`, without direct access to the host’s memory.
> The `IN`/`OUT` restriction must be enforced by the calling code, preferably through small, short facades.

> The VM is a minimal environment with fixed RAM, a single address space, and a safe instruction set (bounds/alignment checks, stopping with a reason instead of exceptions). UNSAFE LOAD/STORE is an explicit escape hatch without checks; responsibility lies with the host. External effects only through IO functions provided by the host. Stopping is cooperative and thread-safe; timeouts and budgets are the host’s policy.
- Tests: unit tests for lexer/parser/code generation, golden execution tests, parser fuzzing, `const` correctness tests, diagnostics tests (`DefaultErrorMessageProvider`).
- Error code reference — [`docs/error-codes.ru.md`](docs/error-codes.ru.md).
- Full bytecode specification — [`docs/bytecode.ru.md`](docs/bytecode.ru.md).
- Built-in libraries reference — [`Nantuc.VmApplication.Extensions/STDLIBRARIES.ru.md`](Nantuc.VmApplication.Extensions/STDLIBRARIES.ru.md).
- **Documentation:** full translation of all project documentation into English.
- Baseline/CI profile for `Nantuc.Benchmarks.BenchmarkDotNet` (filters, time limits, version comparison).
- NuGet packages for `Common`, `Contracts`, `Diagnostics`, `Compiler`, `VmApplication`.


**In progress / plans:**
- Writing a JIT compiler in `C` and `C++`, but that will be a separate project.
- Migrating `Libraries.alloc_minic` and `LibCode.snake_minic` from globals to locals (the code generator bug has been fixed, the workaround is no longer required; see [STDLIBRARIES.ru.md](Nantuc.VmApplication.Extensions/STDLIBRARIES.ru.md)).
- **Debugger:** a frame pointer in `Processor` for an accurate `GetCallStack` (currently a best-effort heuristic over `[rSP, RamSize)`), `Debugger.Reset()`/`Reload()` for reusing a single instance, and a UI panel for IDEs (via the `Stopped` event).

## Contributing

The project is open to issues and pull requests.

- **Found a bug** — open an issue with a minimal example (MiniC/NANASM + expected/actual behavior).
- **Want a feature** — open an issue describing the scenario (what you are doing, what is blocking you).
- **Fix** — fork → branch → PR. For large changes, open an issue first (discuss the design).
- **Style**: follow the style of neighboring files; `Nullable=enable`, `ImplicitUsings=enable`; `unsafe` — only in `Nantuc.Kernel`.

Before a PR:
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
- Debugger — [`Nantuc.VmApplication.Extensions/README.ru.md`](Nantuc.VmApplication.Extensions/README.ru.md#debugger).
- Examples — [`Nantuc.Samples.Minimal`](Nantuc.Samples.Minimal/README.ru.md), [`Nantuc.Samples.Scratch`](Nantuc.Samples.Scratch/README.ru.md).
- Benchmarks — [`Nantuc.Benchmarks`](Nantuc.Benchmarks/README.ru.md), [`Nantuc.Benchmarks.BenchmarkDotNet`](Nantuc.Benchmarks.BenchmarkDotNet/README.ru.md).