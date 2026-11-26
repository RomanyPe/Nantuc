# Nantuc.Kernel — README

> The Nantuc virtual machine kernel: processor, RAM, port bus, device registry, and emulator.
> The only platform layer that uses `unsafe`, `NativeMemory`, `[InlineArray]`, and `ConcurrentQueue`.
> [← Root](../README.ru.md), [Common](../../src/Nantuc.Common/README.ru.md), [Contracts](../../src/Nantuc.Contracts/README.ru.md)

## Contents
- [About this module](#about-this-module)
- [Local project tree](#local-project-tree)
- [API](#api)
- [API examples](#api-examples)
- [Proximity of the module to the user](#proximity-of-the-module-to-the-user)
- [Special information](#special-information)
- [What to fix](#what-to-fix)

## About this module

`Nantuc.Kernel` is the execution layer. It contains: the processor (`Processor` + `ProcessorSnapshot`), the managed wrapper over native RAM (`MemoryBus` + `NativeMemoryBuffer` + `NativeReadOnlyView`), the port bus (`PortBus`), the device registry (`ManagerDevices`), as well as the "computing device" (`Device`), which owns RAM and the processor and implements `IPortController` from `Contracts`.

This module is the only one in the platform where `unsafe` is allowed (`<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` in `Nantuc.csproj`). All native code is in `NativeMemoryBuffer`: `NativeMemory.AllocZeroed`, `NativeMemory.Free`, `byte*`, `Unsafe.ReadUnaligned` / `Unsafe.WriteUnaligned`. The rest of the code works with managed wrappers.

Key roles of the module:
- RAM: `MemoryBus` (checked/unchecked typed operations), `NativeMemoryBuffer` (the native buffer itself), `NativeReadOnlyView` (read-only view with checks).
- Processor: `Processor` — `Step()`, decoding via `InstructionDecoder` from `Common`, `[InlineArray(32)]` registers, `rFL` flags, external command/status queues. `ProcessorSnapshot` — a state snapshot for external observers.
- Bus and devices: `PortBus`, `ManagerDevices`, `Device`.
- Integration: implements `IPortBus`, `IPortController`, `IReadOnlyNativeBuffer`, `ProcessorSnapshot` from `Contracts`.

The module depends on `Common` (ISA types) and `Contracts` (interfaces). It does not depend on `VmApplication`/`Extensions` — the dependency arrow always points inward.

> **XML documentation is complete on the public surface.** All public types, methods, and parameters are documented. The remaining defects are not XML, but behavior: the order of `Volatile.Write` in `Processor.EnqueueBiosStatus` (`_hasCommand` is written before `_hasSimulationStatus` — the status may "stick" in the queue until the next `Enqueue`/`Reset`), the absence of `ObjectDisposedException` on `MemoryBus.ReadInt*LE` after `Dispose`, the stack check order in `InstructionPUSH`, `CurrentProcessorSnapshot` as a property creating a new object on every access, the blocking `_wakeSignal.Wait(...)` on single-threaded WASM. Details — in the [What to fix](#what-to-fix) section.

## Local project tree

Namespaces by layer, files are flat within them:

```
Nantuc.Kernel/
├── Nantuc.csproj               # net10.0, AllowUnsafeBlocks=true, refs Common+Contracts
├── RamSystem/
│   ├── MemoryBus.cs                   # checked/unchecked RAM bus
│   ├── NativeMemoryBuffer.cs          # unsafe, NativeMemory, byte*, ReadUnaligned
│   └── NativeReadOnlyView.cs          # IReadOnlyNativeBuffer, bounds/Dispose checks
├── ProcessorSystem/
│   ├── Processor.cs                       (~150) — fields, ctor, public API
│   ├── Processor.Step.cs                  (~110) — main switch
│   ├── Processor.Arithmetic.cs            (~110) — ADD/SUB/INC/DEC/MULT/SHR/SHL/DIV/MOD/AND/OR/XOR/NOT/CASTZ/CASTS
│   ├── Processor.ControlFlow.cs           (~130) — JMP/JZ/JNZ/JG/JL/CALL/RET/JMP_IND/CALL_IND/END/HALT
│   ├── Processor.Stack.cs                 (~70)  — PUSH/POP
│   ├── Processor.Memory.cs                (~220) — LOAD/STORE (+unsafe) + Body + CanRead64/Get64
│   ├── Processor.Ports.cs                 (~50)  — IN/OUT/WAKE_INT/WAKE
│   ├── Processor.Flags.cs                 (~70)  — CMP/TEST/SetFlags/Zero/Negative
│   ├── Processor.Interrupts.cs            (~70)  — ProcessExternal/ProcessSimulation/ExecuteExternal
│   ├── Processor.Faults.cs                (~30)  — RecordFaultContext/IsTerminal
│   ├── Processor.Snapshot.cs              (~50)  — CurrentProcessorSnapshot
│   └── InstructionHelpers.cs              (~30)  — MaskOf/SignExtend (new static class)
├── ControllersData/
│   └── PortBus.cs                     # sector port bus
├── BiosSystem/
│   └── Device.cs                      # IPortController: MemoryBus + Processor + ManualResetEventSlim
└── Utilities/
    └── ManagerDevices.cs              # device registry, slot reuse
```

Notes on organization:
- The `Utilities` namespace (with a typo — should be `Utilities`) contains `ManagerDevices`.
- `Processor` is `sealed`, uses `[InlineArray(CountReg)]` (32 × `ulong`) instead of an array: more compact for GC, AOT-friendly.
- `NativeMemoryBuffer` is an `unsafe sealed class`; all native code is only here. Other classes work through `MemoryBus`/`IReadOnlyNativeBuffer`.
- `Device` implements `IPortController`, not just `IPortUse`: it has `_ioMemory` and `_wakeSignal`, which need to be released.
- `ManagerDevices` — the entry point for the consumer
- `Processor.CurrentProcessorSnapshot` — a **property** (not a method), creates a **new** `ProcessorSnapshot` on every access (not a reused instance).

## API

### General picture

Public surface by namespace:

| Type | Namespace | Kind | Purpose |
|---|---|---|---|
| `MemoryBus` | `RamSystem` | `class : IDisposable` | Checked `ReadInt*LE`/`WriteInt*LE`, `*UnSafe` variants, `UpdateAndReadStatus`, `FaultData`, `NativeReadOnlyBuffer` |
| `NativeMemoryBuffer` | `RamSystem` | `unsafe sealed class : IDisposable` | `NativeMemory.AllocZeroed`, `ReadUInt*`/`WriteUInt*`, indexer, `Clear`, `LoadProgram`, `IsDisposed` |
| `NativeReadOnlyView` | `RamSystem` | `class : IReadOnlyNativeBuffer` | `ObjectDisposedException` + `ArgumentOutOfRangeException` checks on indexing |
| `Processor` | `ProcessorSystem` | `sealed class` | `Step()`, `LaunchProgramm`, `Reset`, queues `EnqueueBiosStatus`/`EnqueueExternalCommand`, `GetDiagnosticData`, `CopyRegisters`, `CurrentProcessorSnapshot` (property) |
| `ProcessorSnapshot` | `ProcessorSystem` | `readonly struct` | State snapshot: `IsRunning`, `IsSleeping`, copy of 32 registers, `GetRegister(RegType)`, current instruction and `imm64` |
| `PortBus` | `ControllersData` | `sealed class : IPortBus` | Sector bus: `RegisterDevice`, `ReadPort`, `WritePort`, `WakeProcessor`, `IsFreeSector` |
| `Device` | `BiosSystem` | `sealed class : IPortController` | `Run`, `DebugRun`, `StepDebug`,`Step`, `BatchStep`, `BatchStepDebug`, `LoadProgram`, `TryLoadProgram`, `Stop`, `WakeProcessor`, `EnqueueExternalCommand`, `GetLastFault` |
| `ManagerDevices` | `Utilities` | `class : IDisposable` | `TryAddDevice`, `RemoveDevice`, `ChangeSector`, `GetDevice`, `TryGetDevice`, `GetAllDevices`, `Count`, `CreateDevice`|

### `MemoryBus` — RAM

Checked methods **do not throw**: on error they return `0` and save the status, which is read via `UpdateAndReadStatus()` (and resets to `Success`). The `*UnSafe` variants — without bounds and alignment checks.

| Method | What it does | Checks |
|---|---|---|
| `ReadInt8LE(ulong)` | reads a byte | upper bound only |
| `ReadInt16LE`/`ReadInt32LE`/`ReadInt64LE` | reads an LE value | alignment by 2/4/8 + bound |
| `WriteInt8LE`/`WriteInt16LE`/`WriteInt32LE`/`WriteInt64LE` | writes an LE value | same |
| `ReadInt8LEUnSafe`/`WriteInt8LEUnSafe`… | no checks | none |
| `UpdateAndReadStatus()` | returns and resets `_lastStatus` | — |
| `LoadProgram(ReadOnlySpan<byte>, ulong)` | copies the program into RAM | `ArgumentOutOfRangeException` on out-of-bounds |
| `ClearMemory()` | zeroes the buffer | — |

**Status race:** `_lastStatus` and `_faultData` are not `volatile` and are not protected by a lock. `UpdateAndReadStatus` resets the status — the consumer is obliged to call it after every checked operation, otherwise the error will "stick".

### `NativeMemoryBuffer` — native buffer

The only `unsafe` class. After `Dispose`, all `Read*`/`Write*`/indexer are **UB**: `_ptrRam` becomes `null`, and `*(_ptrRam + index)` — NRE or AV. Protection exists only in `NativeReadOnlyView` (`ObjectDisposedException.ThrowIf(buffer.IsDisposed, buffer)`).

- `NativeMemory.AllocZeroed(nuint)` — allocates zeroed memory; on failure — `OutOfMemoryException` (already a non-`NativeMemory`-level error).
- `ReadUInt16/32/64` — `Unsafe.ReadUnaligned<T>` + `BitConverter.IsLittleEndian` (on WASM `IsLittleEndian == true`, so `ReverseEndianness` never executes).
- `this[ulong index]` — `*(_ptrRam + index)`, no checks.
- `LoadProgram(ReadOnlySpan<byte>, ulong)` — `NativeMemory.Copy(src, _ptrRam + startProg, (nuint)prog.Length)`; checks only `prog.Length > 0` and bounds.
- `Dispose` — `Volatile.Read/Write` on `bool`; **not thread-safe with respect to repeated calls** (see [What to fix](#what-to-fix)).

### `NativeReadOnlyView` — read-only view

`IReadOnlyNativeBuffer` from `Contracts`. On indexing, checks `IsDisposed` and `index < Length` — the **only** place where `ObjectDisposedException` is thrown after `Dispose` instead of UB.

### `Processor` — processor

| Method / property | Purpose |
|---|---|
| `Step()` | processes queues (if `Volatile.Read(_hasCommand)`), reads a 32-bit word at `rIP`, increments `rIP` by 4, calls the opcode handler |
| `LaunchProgramm(ulong)` | resets `rFL`, sets `rIP`, `rSP` (top of RAM, aligned to 8), `IsRunning = true` |
| `Reset()` | zeroes registers, clears queues, resets flags (`_faultData` is **not** reset) |
| `EnqueueBiosStatus(BiosStatus)` | puts the status into `_statusFromSimulation`; **first** `Volatile.Write(_hasCommand, true)`, **then** `Volatile.Write(_hasSimulationStatus, true)` — potential signal loss (see [What to fix](#what-to-fix)) |
| `EnqueueExternalCommand(uint)` | puts a raw word into `_externalCommands`; **first** `Volatile.Write(_hasExternalCommand, true)`, **then** `Volatile.Write(_hasCommand, true)` |
| `GetDiagnosticData()` | snapshot of `ProcessorFault` |
| `GetRegValue(RegType)` | a single value |
| `CopyRegisters(Span<ulong>)` | copies 32 registers; throws `ArgumentException` if the buffer < 32 |
| `CurrentProcessorSnapshot` (property) | a **new** `ProcessorSnapshot` on every access: copy of registers, `IsRunning`, `IsSleeping`, current instruction and `imm64` (if needed) |
| `InitRegHP(ulong)` | sets `rHP` |
| `IsRunning`/`IsSleeping` | flags |

Registers — `[InlineArray(CountReg)]` of `ulong` (32 × 8 bytes = 256 bytes per instance). `rFL` — bitfield: bit 0 = Zero, bit 1 = Negative; set **only** by `CMP`/`TEST` (see `<remarks>` `OpCode`).

`Step()` — a `switch` expression over all ~60 opcodes, with `[MethodImpl(AggressiveOptimization)]`. The `Instruction*` handler returns `BiosStatus`; if `!= Success` — fills `_faultData`, calls `_processorPolicy.ShouldContinue` and `Volatile.Write(_isRunning, ...)`.

**Immediate 64-bit operand:** `Get64IntData()` reads `rIP`, aligns up by 8 (`AlignUp(8)`), reads `ulong` via `_ram.ReadInt64LEUnSafe`, then **assigns `rIP = aligned_ip + 8`** (not `rIP += 8`). That is, after a 4-byte instruction there are up to 4 bytes of padding, and the immediate starts at the nearest 8-byte boundary; the new `rIP` = `AlignUp(old_rIP, 8) + 8`.

### `ProcessorSnapshot` — state snapshot

Created by the **property** `Processor.CurrentProcessorSnapshot`. Contains `IsRunning`, `IsSleeping`, and a copy of 32 registers. Has `GetRegister(RegType)`. Since the snapshot is an independent copy, it can be safely stored; it won't change on the next step.

### `PortBus` — sector port bus

- The address is decomposed into a "sector" (`address >> _deviceShift`) and an "offset" (`address & _offsetMask`).
- `RegisterDevice(IPortUse, uint)` — `IPortUse`, not `IPortController` (the latter extends `IPortUse`).
- `ReadPort` returns `ReadPortResult(Data, Status)`; `Status = NullDeviceInput` if there is no device at the address.
- `WritePort` returns `BiosStatus`: `Success` or `NullDeviceOutput`.
- `WakeProcessor` — `bool` (whether a device was found).
- `RegisterDevice`/`UnregisterDevice` — direct control of the `_devices` array; **no thread safety** (see `<remarks>` of the type).

### `Device` — computing device

Own `MemoryBus` + `Processor` + `byte[] _ioMemory` + `ManualResetEventSlim _wakeSignal`. Implements `IPortController` — can be registered on `PortBus` via `ManagerDevices.TryAddDevice`.

| Method | Purpose |
|---|---|
| `Run(ulong, long?, bool, Action?, Action<TimeSimulationResult>?)` | blocking run until completion/failure; `limit` — step limit -> `InfinityLoop` |
| `DebugRun(ulong, long, int, Action?, Action<ProcessorSnapshot>?, Action?)` | run with delay and per-step callback |
| `BreakPointerLaunchDevice(ulong?, Action?)` | initialization without starting the loop; `start ?? 0` — if `null`, `rIP` is set to `0` (not "from the end of RAM" — the end of RAM is only used for `rSP` in `LaunchProgramm`) |
| `StepDebug(Action<ProcessorSnapshot>?)` / `BatchStepDebug(int, Action<ProcessorSnapshot>?)` | manual steps; read `Processor.CurrentProcessorSnapshot` (property) |
| `Step()` / `BatchStep(int)` | manual steps |
| `LoadProgram(ReadOnlySpan<byte>, ulong)` / `TryLoadProgram` | load + initialize `rHP` (`(loadAddress + programLength).AlignUp(8)`) |
| `Stop()` / `WakeProcessor()` / `EnqueueExternalCommand(uint)` | from another thread: `Enqueue*` + `_wakeSignal.Set()` |
| `CopyRegisters` / `GetLastFault` / `ResetMemoryRam` / `ClearRegisters` | snapshots |
| `ReadPort` / `WritePort` | **no bounds checking** — return `0`/ignore on out-of-bounds of `_ioMemory.Length` (in the type's `<remarks>` — "I/O ports", the implementation does not throw) |

`Stop`/`WakeProcessor`/`EnqueueExternalCommand` — the only methods safe from another thread (they use `Enqueue*` + `_wakeSignal.Set()`). `Run`/`DebugRun`/`NextStep*` — are not.

### `ManagerDevices` — registry
- `CreateDevice(RamSize, IProcessorPolicy)` — a new `Device`.
- `TryAddDevice` — `IdState.Valid` on success; otherwise `NotAssigned` (registry full), `OutOfRange` (sector occupied), `NoFreeSlot` (no slot and no ID), `Rejected` (`PortBus.RegisterDevice` returned `false`, the device is **disposed**). `out int id` on failure receives `-1`/`-2`/`-3`/`-4`.
- Slots (`DeviceEntry`) are reused: `IsFreed = true` preserves `Id` so that on re-registration `Id` stays the same.
- `Count` — O(n) every time (not cached); called in `TryAddDevice` on every add.
- `Dispose` **does not remove** devices from `PortBus` — `_portBus._devices[sector]` continues to reference the disposed controller (see [What to fix](#what-to-fix)).
- `GetDevice(int)` / `TryGetDevice<T>(int, out T?)` — by `Id`.
- Constructor — `MangerDevices(PortSize ports, DevicePortSize portsOnDev)` creates `PortBus`.

## API examples

**Minimal launch:**

```csharp
using Nantuc.Common;
using Nantuc.Contracts;
using Nantuc.Kernel.Utilities;

using var manager = new ManagerDevices(PortSize.KB64, DevicePortSize.B64);

using Device device = manager.CreateDevice(RamSize.MB16, IProcessorPolicy.Default);

byte[] program = [/* bytes: 32-bit instruction words */];
device.LoadProgram(program, loadAddress: 0x0);

ProcessorFault fault = device.Run(
    start: 0,
    limit: 10_000,
    showTimer: true,
    onLaunch: () => Console.WriteLine("start"),
    onEnd: r => Console.WriteLine($"steps={r.Steps}, elapsed={r.Elapsed}"));

Console.WriteLine($"{fault.Status} @ 0x{fault.InstructionPointer:X}");
```

**Step-by-step execution in debug mode:**

```csharp
device.BreakPointerLaunchDevice(start: 0, onLaunch: null);

device.StepDebug(step => Console.WriteLine($"IP=0x{step.GetRegister(RegType.rIP):X}"));
device.BatchStepDebug(3, onStep: null);
```
**Quick step-by-step execution:**

```csharp
device.BreakPointerLaunchDevice(start: 0, onLaunch: null);

device.Step();
device.BatchStep(3);
```

**Registering an external device:**

```csharp
public sealed class MyDevice : IPortController
{
    public byte ReadPort(ulong offset) => 0;
    public void WritePort(ulong offset, byte value) { /* ... */ }
    public void WakeProcessor() { /* ... */ }
    public void Dispose() { /* ... */ }
}

IdState state = emulator.RegisterDevice(new MyDevice(), "my-dev", sector: 0, out int id);
if (state != IdState.Valid)
    Console.Error.WriteLine($"register failed: {state} (id={id})");
```

**Direct RAM read:**

```csharp
IReadOnlyNativeBuffer ram = device.NativeReadOnlyBuffer;
nuint n = (nuint)Math.Min((ulong)ram.Length, 64UL);
for (nuint i = 0; i < n; i++)
    Console.Write($"{ram[i]:X2} ");

// !! after device.Dispose() indexing will throw ObjectDisposedException
```

**Checked read via MemoryBus (inside your own handler):**

```csharp
ulong addr = 0x10;
uint value = memoryBus.ReadInt32LE(addr);
BiosStatus st = memoryBus.UpdateAndReadStatus();
if (st != BiosStatus.Success)
    Console.Error.WriteLine($"{st} @ 0x{memoryBus.FaultData:X}");
```

## Proximity of the module to the user

Medium (higher than `Common`/`Contracts`, lower than `VmApplication`).

- The author of device extensions works with `ManagerDevices`/`Device`/`PortBus` — this is their main API.
- The `VmApplication` user does not import this layer directly, but it is `Device.Run`/`DebugRun` that are called from `VmApplication`.
- The compiler author does not work with this layer — only with `Common`/`Contracts`.

**What you should do:**
- Use `ManagerDevices.CreateDevice` + `Device.Run`, rather than creating `Device`/`Processor`/`MemoryBus` manually.
- After every checked `MemoryBus` operation — call `UpdateAndReadStatus()`; the status is reset.
- Remember that `ReadPort`/`WritePort` in `Device` **do not throw** — out-of-bounds yields `0`/ignore, not an exception.
- Call `Stop`/`WakeProcessor`/`EnqueueExternalCommand` from another thread, and `Run`/`NextStep*` — only from a single coordinator thread.
- After `device.Dispose()`, read RAM only through `NativeReadOnlyBuffer` — there it will be `ObjectDisposedException`, not UB.
- `Processor.CurrentProcessorSnapshot` (property) returns a new `ProcessorSnapshot`; it can be stored and used after the next step.

**What you should not do:**
- Rely on `_lastStatus` between operations without `UpdateAndReadStatus` — the status will "stick".
- Read/write `_ioMemory`/`NativeMemoryBuffer` directly from another thread during `Run`.
- Register/unregister devices (`RegisterDevice`/`UnregisterDevice`/`ChangeDeviceSector`) during `Device.Run` — there is no synchronization.
- Use `NativeMemoryBuffer` directly after `Dispose` — UB, not an exception (only `NativeReadOnlyView` is protected).
- Store the `IReadOnlyNativeBuffer` obtained from `Device.NativeReadOnlyBuffer` after the device's `Dispose()` — `ObjectDisposedException` on the next indexing.
- Expect `GetDiagnosticData()` to be reset by `Processor.Reset()`: `_faultData` is **not reset** in `Reset()` — after a reset, `GetDiagnosticData()` returns the last fault of the previous run.

## Special information

### WASM / cross-platform

| Aspect | Status | Comment |
|---|---|---|
| Building to WASM | !!️ | Builds for `browser-wasm` without csproj changes, but `Device.Run`/`DebugRun` are synchronous blocking methods. On single-threaded WASM they block the UI thread. Requires moving to a Web Worker or replacing with an async loop. |
| `NativeMemory` | !!️ | `NativeMemory.AllocZeroed`/`Free`/`Clear` work on WASM (emscripten linear memory). But allocating 2 GiB (`RamSize.GB2`) in a browser will almost certainly throw `OutOfMemoryException` — the WASM linear memory limit is ~4 GiB for the whole process, with fragmentation. `MemoryBus`/`Device` do not check this limit. |
| `unsafe` / `byte*` | - | `NativeMemoryBuffer` — `unsafe`; WASM supports pointers. `Unsafe.ReadUnaligned`/`WriteUnaligned` work. |
| `stackalloc` / `InlineArray` | - | `stackalloc` is not used in hot paths, except `Processor.CurrentProcessorSnapshot` (32 × 8 bytes per access). `[InlineArray(32)]` in `Processor` — compile-time, AOT-friendly. |
| `Interlocked` / `Volatile` | !!️ | `Interlocked.Exchange` in `MemoryBus.Dispose` and `Device.Dispose`, `Volatile.Read/Write` in `NativeMemoryBuffer.Dispose` and `Processor` — on single-threaded WASM (without `SharedArrayBuffer`) these are no-ops, but the code works. With threaded WASM — atomic. |
| `ConcurrentQueue<T>` | - | Two queues in `Processor` (`_externalCommands`, `_statusFromSimulation`). On single-threaded WASM — just a queue; on threaded — lock-free. |
| `ManualResetEventSlim.Wait(...)` | !!️!!️ | In `Device.RunSimulationLoop`/`RunSimulationLoopWithLimit`/`RunSimulationLoopDebug` — `_wakeSignal.Wait(_processorPolicy.SleepWaitTimeout)` blocks the current thread when `IsSleeping`. In the browser — UI blocking. Replacement: `await Task.Delay` + async loop, or a Web Worker. |
| `Thread.Sleep(delay)` | !!️!!️ | `RunSimulationLoopDebug` — `Thread.Sleep(delay)`. In the browser — UI blocking. |
| `Stopwatch` | - | `Run` with `showTimer: true` — `Stopwatch.StartNew()`; on WASM works via a `performance.now`-like timer. |
| `IFileService` | n/a | There is no interface in this batch of files. If it appears in other layers, the WASM implementation is separate. |
| `Regex` / trimming | - | Regex is not used; there is no reflection, `Type.GetType`, `Activator.CreateInstance`, `dynamic`. Trimming-compatible. |
| AOT | - | No `Expression.Compile`, `MakeGenericMethod`/`MakeGenericType`. The `switch` over opcodes in `Step` is AOT-friendly. Generic code — only `TryGetDevice<T>` (monomorphized for the used `T`s). |

**Summary:** the code builds for WASM and AOT, but the **synchronous blocking execution model** (`Run`, `Thread.Sleep`, `ManualResetEventSlim.Wait`) is not suitable for browser-WASM in single-threaded mode. `NativeMemory.AllocZeroed` works, but large `RamSize` (≥ `MB512`–`GB1`) risk `OutOfMemoryException`. For production in the browser — Web Worker + async loop + RAM ≤ ~256 MiB.

### `Processor.Step` — processing order

1. `Volatile.Read(ref _hasCommand)` — if `true`, **first** `Volatile.Write(ref _hasCommand, false)` (reset before processing, so that new commands arriving during processing are not lost), then `Volatile.Read(ref _hasExternalCommand)` -> `ProcessExternalCommandsSlow`, `Volatile.Read(ref _hasSimulationStatus)` -> `ProcessSimulationStatusSlow`.
2. If `!_isRunning | _isSleeping` — exit (note: `|`, not `||`).
3. `ip = rIP`; `rawInst = _ram.ReadInt32LEUnSafe(ip)`; `rIP += 4` (before calling the handler — the handler sees the next word already).
4. `switch` over the opcode -> `BiosStatus`.
5. If `dat != Success` — `_faultData = (dat, _lastFaultData, ip, opCode)`, `Volatile.Write(ref _isRunning, !IsTerminal(dat) && _processorPolicy.ShouldContinue(_faultData))`.

**Immediate operands:** `Get64IntData()` reads `rIP`, aligns up by 8 (`AlignUp(8)`), reads `ulong` via `_ram.ReadInt64LEUnSafe`, then **assigns `rIP = aligned_ip + 8`** — called by the `LOAD`/`STORE`/`LDI`/`JMP`/`JZ`/`JNZ`/`JG`/`JL`/`CALL` handlers and their `*_UNSAFE` variants. `LOAD_IND`/`STORE_IND`/`CALL_IND`/`JMP_IND` and their `*_UNSAFE` — indirect via a register, do not call `Get64IntData`.

`CanRead64IntDataInst()` checks `AlignUp(rIP, 8) > _ram.Length - 8`; if `_ram.Length < 8`, underflow is possible (the check formally passes, the read will go out of bounds — there will be no actual trigger only if `_ram.Length ≥ 8`).

### `rFL` flags and jumps

`ZeroFlagSet` — `(rFL & 1) != 0`; `NegativeFlagSet` — `(rFL & 2) != 0`. `CMP`/`TEST` set `zeroFlag | (negativeFlag << 1)` via bit hacks. `JG` checks `!Negative & !Zero`, `JL` — only `Negative`, `JZ`/`JNZ` — `Zero`/`!Zero`. `rFL` is reset to `0` on `LaunchProgramm`.

### Stack and heap

`rSP` is initialized in `LaunchProgramm` as `_ram.Length & ~0x7UL`; `rHP` — in `Device.InitHeap` as `(loadAddress + programLength).AlignUp(8)`. `PUSH`/`CALL` decrease `rSP` by 8 and check the collision `rHP >= sp`; `POP`/`RET` increase it by 8. `RET` checks `sp >= _stackBase` (empty stack -> `StackUnderflow`). With `rHP = 0` (e.g., `BreakPointerLaunchDevice` without `LoadProgram`), the check `rHP >= sp` lets everything through except `sp == 0`.

### Thread safety

- `Processor.Step`/`LaunchProgramm`/`Reset` — not thread-safe.
- `EnqueueBiosStatus`/`EnqueueExternalCommand` — safe from another thread (queue + `Volatile.Write`), but `EnqueueBiosStatus` has an inverted flag write order (see [What to fix](#what-to-fix)).
- `Device.Stop`/`WakeProcessor`/`EnqueueExternalCommand` — safe (delegate to `Processor` + `_wakeSignal.Set()`).
- `PortBus.RegisterDevice`/`UnregisterDevice` — not thread-safe.
- `ManagerDevices` — not thread-safe (see `<remarks>`).
- `MemoryBus` — not thread-safe (`_lastStatus`/`_faultData` without synchronization).
- `NativeMemoryBuffer.Dispose` — `Volatile.Read/Write` on `bool`, but check-then-act is not atomic; with simultaneous `Dispose` from two threads, a double-free is possible.
- `Processor.CurrentProcessorSnapshot` — not thread-safe (reads registers without synchronization with `Step`); `_faultData` itself is also read without `Volatile`, so the snapshot may be slightly "smeared" relative to the last step.

### Platform limitations

- Target framework: `net10.0`; `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`.
- `unsafe` — only in `NativeMemoryBuffer`; `MemoryBus`/`PortBus`/`Processor`/`Device` — managed.
- `[MethodImpl(AggressiveInlining)]`/`[AggressiveOptimization]` — optimization for JIT; in AOT/WASM the inliner behaves differently.
- `Nullable=enable`, `ImplicitUsings=enable`.

### What this module does not do

- Does not contain an `IFileService` implementation — only works with RAM and ports.
- Does not provide an async API — everything is synchronous (`Run`, `NextStep*`).
- Does not manage the process lifecycle — that is `VmApplication`.
- Does not parse `.bin`/`.nanas` bytecode — only executes 32-bit words via `Processor.Step`.