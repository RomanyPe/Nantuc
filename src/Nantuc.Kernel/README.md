# Nantuc.Kernel — README

> The Nantuc virtual machine kernel: processor, RAM, port bus, device registry, and emulator.
> The only platform layer that uses `unsafe`, `NativeMemory`, `[InlineArray]`, and `ConcurrentQueue`.
> `[← Root](../README.ru.md) · [Common](../../src/Nantuc.Common/README.ru.md) · [Contracts](../../src/Nantuc.Contracts/README.ru.md)`

## Contents
- [About This Module](#about-this-module)
- [Local Project Tree](#local-project-tree)
- [API](#api)
- [API Examples](#api-examples)
- [Module Proximity to the User](#module-proximity-to-the-user)
- [Special Information](#special-information)
- [What to Fix](#what-to-fix)

## About This Module

`Nantuc.Kernel` is the execution layer. It contains: the processor (`Processor` + `ProcessorSnapshot`), the managed wrapper over native RAM (`MemoryBus` + `NativeMemoryBuffer` + `NativeReadOnlyView`), the port bus (`PortBus`), the device registry (`ManagerDevices`), and the emulator (`Emulator`), as well as the "computing device" (`Device`), which owns the RAM and processor and implements `IPortController` from `Contracts`.

This module is the only one in the platform where `unsafe` is permitted (`<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` in `Nantuc.csproj`). Everything native lives in `NativeMemoryBuffer`: `NativeMemory.AllocZeroed`, `NativeMemory.Free`, `byte*`, `Unsafe.ReadUnaligned` / `Unsafe.WriteUnaligned`. The rest of the code works with managed wrappers.

Key roles of the module:
- RAM: `MemoryBus` (checked/unchecked typed operations), `NativeMemoryBuffer` (the native buffer itself), `NativeReadOnlyView` (read-only view with checks).
- Processor: `Processor` — `Step()`, decoding via `InstructionDecoder` from `Common`, registers `[InlineArray(32)]`, flags `rFL`, external command/status queues. `ProcessorSnapshot` — a state snapshot for external observers.
- Bus and devices: `PortBus`, `ManagerDevices`, `Device`, `Emulator`.
- Integration: implements `IPortBus`, `IPortController`, `IReadOnlyNativeBuffer`, `ProcessorSnapshot` from `Contracts`.

The module depends on `Common` (ISA types) and `Contracts` (interfaces). It does not depend on `VmApplication`/`Extensions` — the dependency arrow always points inward.

> **XML documentation is complete on the public surface.** All public types, methods, and parameters are documented. The remaining defects are not XML but behavioral: the order of `Volatile.Write` in `Processor.EnqueueBiosStatus` (`_hasCommand` is written before `_hasSimulationStatus` — the status may "stick" in the queue until the next `Enqueue`/`Reset`), the absence of `ObjectDisposedException` on `MemoryBus.ReadInt*LE` after `Dispose`, the stack check order in `InstructionPUSH`, `CurrentProcessorSnapshot` as a property creates a new object on every access, the blocking `_wakeSignal.Wait(...)` on single-threaded WASM. Details — in the [What to Fix](#what-to-fix) section.

## Local Project Tree

Namespaces by layer, files flat within them:

```
Nantuc.Kernel/
├── Nantuc.csproj               # net10.0, AllowUnsafeBlocks=true, refs Common+Contracts
├── RamSystem/
│   ├── MemoryBus.cs                   # checked/unchecked RAM bus
│   ├── NativeMemoryBuffer.cs          # unsafe, NativeMemory, byte*, ReadUnaligned
│   └── NativeReadOnlyView.cs          # IReadOnlyNativeBuffer, bounds/Dispose checks
├── ProcessorSystem/
│   ├── Processor.cs                   # Step(), [InlineArray(32)], ConcurrentQueue, Volatile
│   └── ProcessorSnapshot.cs             # processor state snapshot for external observers
├── ControllersData/
│   └── PortBus.cs                     # sector port bus
├── BiosSystem/
│   └── Device.cs                      # IPortController: MemoryBus + Processor + ManualResetEventSlim
└── Utilites/
    ├── Emulator.cs                    # PortBus + ManagerDevices, CreateDevice/RegisterDevice
    └── ManagerDevices.cs              # device registry, slot reuse
```

Organization notes:
- The `Utilites` namespace (with a typo — should be `Utilities`) contains `Emulator` and `ManagerDevices`.
- `Processor` is `sealed`, uses `[InlineArray(CountReg)]` (32 × `ulong`) instead of an array: more compact for GC, AOT-friendly.
- `NativeMemoryBuffer` is `unsafe sealed class`; everything native is only here. The other classes work through `MemoryBus`/`IReadOnlyNativeBuffer`.
- `Device` implements `IPortController`, not just `IPortUse`: it has `_ioMemory` and `_wakeSignal`, which need to be disposed.
- `Emulator` is the entry point for the consumer; `ManagerDevices` is an implementation detail (public, but mainly needed by `Emulator`).
- `Processor.CurrentProcessorSnapshot` is a **property** (not a method), creates a **new** `ProcessorSnapshot` on every access (not a reused instance).

## API

### Overall Picture

Public surface by namespace:

| Type | Namespace | Kind | Purpose |
|---|---|---|---|
| `MemoryBus` | `RamSystem` | `class : IDisposable` | Checked `ReadInt*LE`/`WriteInt*LE`, `*UnSafe` variants, `UpdateAndReadStatus`, `FaultData`, `NativeReadOnlyBuffer` |
| `NativeMemoryBuffer` | `RamSystem` | `unsafe sealed class : IDisposable` | `NativeMemory.AllocZeroed`, `ReadUInt*`/`WriteUInt*`, indexer, `Clear`, `LoadProgram`, `IsDisposed` |
| `NativeReadOnlyView` | `RamSystem` | `class : IReadOnlyNativeBuffer` | `ObjectDisposedException` + `ArgumentOutOfRangeException` checks on indexing |
| `Processor` | `ProcessorSystem` | `sealed class` | `Step()`, `LaunchProgramm`, `Reset`, queues `EnqueueBiosStatus`/`EnqueueExternalCommand`, `GetDiagnosticData`, `CopyRegisters`, `CurrentProcessorSnapshot` (property) |
| `ProcessorSnapshot` | `ProcessorSystem` | `readonly struct` | State snapshot: `IsRunning`, `IsSleeping`, copy of 32 registers, `GetRegister(RegType)`, current instruction and `imm64` |
| `PortBus` | `ControllersData` | `sealed class : IPortBus` | Sector bus: `RegisterDevice`, `ReadPort`, `WritePort`, `WakeProcessor`, `IsFreeSector` |
| `Device` | `BiosSystem` | `sealed class : IPortController` | `Run`, `DebugRun`, `NextStepProcessor*`, `LoadProgram`, `TryLoadProgram`, `Stop`, `WakeProcessor`, `EnqueueExternalCommand`, `GetLastFault` |
| `Emulator` | `Utilites` | `class : IDisposable` | `CreateDevice`, `RegisterDevice`, `GetDevice`, `TryGetDevice`, `UnregisterDevice`, `ChangeDeviceSector`, `AllDeviceInfo` |
| `ManagerDevices` | `Utilites` | `class : IDisposable` | `TryAddDevice`, `RemoveDevice`, `ChangeSector`, `GetDevice`, `TryGetDevice`, `GetAllDevices`, `Count` |

### `MemoryBus` — RAM

Checked methods **do not throw**: on error they return `0` and save the status, which is read via `UpdateAndReadStatus()` (and resets to `Success`). `*UnSafe` variants — no bounds or alignment checks.

| Method | What it does | Checks |
|---|---|---|
| `ReadInt8LE(ulong)` | reads a byte | upper bound only |
| `ReadInt16LE`/`ReadInt32LE`/`ReadInt64LE` | reads an LE value | alignment to 2/4/8 + bound |
| `WriteInt8LE`/`WriteInt16LE`/`WriteInt32LE`/`WriteInt64LE` | writes an LE value | same |
| `ReadInt8LEUnSafe`/`WriteInt8LEUnSafe`… | no checks | none |
| `UpdateAndReadStatus()` | returns and resets `_lastStatus` | — |
| `LoadProgram(ReadOnlySpan<byte>, ulong)` | copies the program into RAM | `ArgumentOutOfRangeException` on out-of-bounds |
| `ClearMemory()` | zeroes the buffer | — |

**Status race:** `_lastStatus` and `_faultData` are not `volatile`, not protected by a lock. `UpdateAndReadStatus` resets the status — the consumer must call it after every checked operation, otherwise the error will "stick."

### `NativeMemoryBuffer` — Native Buffer

The only `unsafe` class. After `Dispose`, all `Read*`/`Write*`/indexer — **UB**: `_ptrRam` becomes `null`, and `*(_ptrRam + index)` — NRE or AV. Protection exists only in `NativeReadOnlyView` (`ObjectDisposedException.ThrowIf(buffer.IsDisposed, buffer)`).

- `NativeMemory.AllocZeroed(nuint)` — allocates zeroed memory; on failure — `OutOfMemoryException` (already a `NativeMemory`-level error).
- `ReadUInt16/32/64` — `Unsafe.ReadUnaligned<T>` + `BitConverter.IsLittleEndian` (on WASM `IsLittleEndian == true`, so `ReverseEndianness` is never executed).
- `this[ulong index]` — `*(_ptrRam + index)`, no checks.
- `LoadProgram(ReadOnlySpan<byte>, ulong)` — `NativeMemory.Copy(src, _ptrRam + startProg, (nuint)prog.Length)`; checks only `prog.Length > 0` and bounds.
- `Dispose` — `Volatile.Read/Write` on `bool`; **not thread-safe with respect to repeated calls** (see [What to Fix](#what-to-fix)).

### `NativeReadOnlyView` — Read-Only View

`IReadOnlyNativeBuffer` from `Contracts`. On indexing, checks `IsDisposed` and `index < Length` — the **only** place where after `Dispose` an `ObjectDisposedException` is thrown rather than UB.

### `Processor` — Processor

| Method / Property | Purpose |
|---|---|
| `Step()` | processes queues (if `Volatile.Read(_hasCommand)`), reads a 32-bit word at `rIP`, increments `rIP` by 4, calls the opcode handler |
| `LaunchProgramm(ulong)` | resets `rFL`, sets `rIP`, `rSP` (top of RAM, aligned to 8), `IsRunning = true` |
| `Reset()` | zeroes registers, clears queues, resets flags (`_faultData` is **not** reset) |
| `EnqueueBiosStatus(BiosStatus)` | puts the status into `_statusFromSimulation`; **first** `Volatile.Write(_hasCommand, true)`, **then** `Volatile.Write(_hasSimulationStatus, true)` — potential signal loss (see [What to Fix](#what-to-fix)) |
| `EnqueueExternalCommand(uint)` | puts a raw word into `_externalCommands`; **first** `Volatile.Write(_hasExternalCommand, true)`, **then** `Volatile.Write(_hasCommand, true)` |
| `GetDiagnosticData()` | snapshot of `ProcessorFault` |
| `GetRegValue(RegType)` | single value |
| `CopyRegisters(Span<ulong>)` | copies 32 registers; throws `ArgumentException` if the buffer is < 32 |
| `CurrentProcessorSnapshot` (property) | **new** `ProcessorSnapshot` on every access: copy of registers, `IsRunning`, `IsSleeping`, current instruction and `imm64` (if needed) |
| `InitRegHP(ulong)` | sets `rHP` |
| `IsRunning`/`IsSleeping` | flags |

Registers — `[InlineArray(CountReg)]` of `ulong` (32 × 8 bytes = 256 bytes per instance). `rFL` — bit field: bit 0 = Zero, bit 1 = Negative; set **only** by `CMP`/`TEST` (see `<remarks>` of `OpCode`).

`Step()` — a `switch` expression over all ~60 opcodes, with `[MethodImpl(AggressiveOptimization)]`. The `Instruction*` handler returns `BiosStatus`; if `!= Success` — fills `_faultData`, calls `_processorPolicy.ShouldContinue` and `Volatile.Write(_isRunning, ...)`.

**Immediate 64-bit operand:** `Get64IntData()` reads `rIP`, aligns up to 8 (`AlignUp(8)`), reads `ulong` via `_ram.ReadInt64LEUnSafe`, then **assigns `rIP = aligned_ip + 8`** (not `rIP += 8`). That is, after a 4-byte instruction there are up to 4 bytes of padding, and the immediate starts at the nearest 8-byte boundary; the new `rIP` = `AlignUp(old_rIP, 8) + 8`.

### `ProcessorSnapshot` — State Snapshot

Created by the **property** `Processor.CurrentProcessorSnapshot`. Contains `IsRunning`, `IsSleeping`, and a copy of 32 registers. Has `GetRegister(RegType)`. Since the snapshot is an independent copy, it can be safely saved; on the next step the state will not change.

### `PortBus` — Sector Port Bus

- The address is split into "sector" (`address >> _deviceShift`) and "offset" (`address & _offsetMask`).
- `RegisterDevice(IPortUse, uint)` — `IPortUse`, not `IPortController` (the latter extends `IPortUse`).
- `ReadPort` returns `ReadPortResult(Data, Status)`; `Status = NullDeviceInput` if there is no device at the address.
- `WritePort` returns `BiosStatus`: `Success` or `NullDeviceOutput`.
- `WakeProcessor` — `bool` (whether the device was found).
- `RegisterDevice`/`UnregisterDevice` — direct manipulation of the `_devices` array; **no thread safety** (see `<remarks>` of the type).

### `Device` — Computing Device

Its own `MemoryBus` + `Processor` + `byte[] _ioMemory` + `ManualResetEventSlim _wakeSignal`. Implements `IPortController` — can be registered on `PortBus` via `Emulator.RegisterDevice`.

| Method | Purpose |
|---|---|
| `Run(ulong, long?, bool, Action?, Action<TimeSimulationResult>?)` | blocking run until completion/failure; `limit` — step limit -> `InfinityLoop` |
| `DebugRun(ulong, long, int, Action?, Action<ProcessorSnapshot>?, Action?)` | run with delay and per-step callback |
| `BreakPointerLaunchDevice(ulong?, Action?)` | initialization without starting the loop; `start ?? 0` — if `null`, `rIP` is set to `0` (not "from the end of RAM" — the end of RAM only goes into `rSP` in `LaunchProgramm`) |
| `NextStepProcessor(Action<ProcessorSnapshot>?)` / `NextStepProcessorCount(int, Action<ProcessorSnapshot>?)` | manual steps; read `Processor.CurrentProcessorSnapshot` (property) |
| `LoadProgram(ReadOnlySpan<byte>, ulong)` / `TryLoadProgram` | loading + initialization of `rHP` (`(loadAddress + programLength).AlignUp(8)`) |
| `Stop()` / `WakeProcessor()` / `EnqueueExternalCommand(uint)` | from another thread: `Enqueue*` + `_wakeSignal.Set()` |
| `CopyRegisters` / `GetLastFault` / `ResetMemoryRam` / `ClearRegisters` | snapshots |
| `ReadPort` / `WritePort` | **without bounds checks** — return `0`/ignore on out-of-bounds of `_ioMemory.Length` (in `<remarks>` of the type — "I/O ports," the implementation does not throw) |

`Stop`/`WakeProcessor`/`EnqueueExternalCommand` are the only methods safe from another thread (they use `Enqueue*` + `_wakeSignal.Set()`). `Run`/`DebugRun`/`NextStep*` — are not.

### `Emulator` — Entry Point

- Constructor `Emulator(PortSize ports, DevicePortSize portsOnDev)` — creates `PortBus` and `ManagerDevices`.
- `CreateDevice(RamSize, IProcessorPolicy)` — a new `Device`.
- `RegisterDevice(IPortController, string?, uint, out int)` — registers an external controller; on failure **disposes** `device` (`device.Dispose()` inside `ManagerDevices.TryAddDevice`).
- `GetDevice(int)` / `TryGetDevice<T>(int, out T?)` — by `Id`.
- `UnregisterDevice(int)` / `ChangeDeviceSector(int, uint)`.

### `ManagerDevices` — Registry

- `TryAddDevice` — `IdState.Valid` on success; otherwise `NotAssigned` (registry full), `OutOfRange` (sector occupied), `NoFreeSlot` (no slot and no ID), `Rejected` (`PortBus.RegisterDevice` returned `false`, the device is **disposed**). `out int id` on failure gets `-1`/`-2`/`-3`/`-4`.
- Slots (`DeviceEntry`) are reused: `IsFreed = true` preserves `Id` so that on re-registration the `Id` remains the same.
- `Count` — O(n) every time (not cached); called in `TryAddDevice` on every addition.
- `Dispose` **does not remove** devices from `PortBus` — `_portBus._devices[sector]` continues to reference the disposed controller (see [What to Fix](#what-to-fix)).

## API Examples

**Minimal launch:**

```csharp
using Nantuc.Common;
using Nantuc.Contracts;
using Nantuc.Kernel.Utilites;

using var emulator = new Emulator(PortSize.KB64, DevicePortSize.B64);

using Device device = emulator.CreateDevice(RamSize.MB16, IProcessorPolicy.Default);

byte[] program = [/* bytes: 32-bit instruction words */];
device.LoadProgram(program, loadAddress: 0x0);

ProcessorFault fault = device.Run(
    start: 0,
    limit: 10_000,
    snowTimer: true,
    onLaunch: () => Console.WriteLine("start"),
    onEnd: r => Console.WriteLine($"steps={r.Steps}, elapsed={r.Elapsed}"));

Console.WriteLine($"{fault.Status} @ 0x{fault.InstructionPointer:X}");
```

**Step-by-step execution:**

```csharp
device.BreakPointerLaunchDevice(start: 0, titleAct: null);

device.NextStepProcessor(step => Console.WriteLine($"IP=0x{step.GetRegister(RegType.rIP):X}"));
device.NextStepProcessorCount(3, onStep: null);
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

// ⚠ after device.Dispose() indexing will throw ObjectDisposedException
```

**Checked read via MemoryBus (inside your own handler):**

```csharp
ulong addr = 0x10;
uint value = memoryBus.ReadInt32LE(addr);
BiosStatus st = memoryBus.UpdateAndReadStatus();
if (st != BiosStatus.Success)
    Console.Error.WriteLine($"{st} @ 0x{memoryBus.FaultData:X}");
```

## Module Proximity to the User

Medium (higher than `Common`/`Contracts`, lower than `VmApplication`).

- The extension device author works with `Emulator`/`Device`/`PortBus` — this is their primary API.
- The `VmApplication` user does not import this layer directly, but it is `Device.Run`/`DebugRun` that are called from `VmApplication`.
- The compiler author does not work with this layer — only with `Common`/`Contracts`.
- For production code embedding the emulator: `Emulator` is the entry point; `ManagerDevices` is a detail.

**What you should do:**
- Use `Emulator.CreateDevice` + `Device.Run`, rather than creating `Device`/`Processor`/`MemoryBus` manually.
- After every checked `MemoryBus` operation — call `UpdateAndReadStatus()`; the status is reset.
- Remember that `ReadPort`/`WritePort` in `Device` **do not throw** — out-of-bounds gives `0`/ignore, not an exception.
- Call `Stop`/`WakeProcessor`/`EnqueueExternalCommand` from another thread, and `Run`/`NextStep*` — only from a single coordinator thread.
- After `device.Dispose()`, read RAM only via `NativeReadOnlyBuffer` — there it will be `ObjectDisposedException`, not UB.
- `Processor.CurrentProcessorSnapshot` (property) returns a new `ProcessorSnapshot`; it can be saved and used after the next step.

**What you should not do:**
- Rely on `_lastStatus` between operations without `UpdateAndReadStatus` — the status will "stick."
- Read/write `_ioMemory`/`NativeMemoryBuffer` directly from another thread during `Run`.
- Register/unregister devices (`RegisterDevice`/`UnregisterDevice`/`ChangeDeviceSector`) during `Device.Run` — there is no synchronization.
- Use `NativeMemoryBuffer` directly after `Dispose` — UB, not an exception (only `NativeReadOnlyView` is protected).
- Keep an `IReadOnlyNativeBuffer` obtained from `Device.NativeReadOnlyBuffer` after the device's `Dispose()` — `ObjectDisposedException` on the next indexing.
- Expect `GetDiagnosticData()` to be reset by `Processor.Reset()`: `_faultData` is **not** reset in `Reset()` — after a reset, `GetDiagnosticData()` will return the last failure of the previous run.

## Special Information

### WASM / Cross-Platform

| Aspect | Status | Comment |
|---|---|---|
| Build to WASM | ⚠️ | Builds for `browser-wasm` without csproj changes, but `Device.Run`/`DebugRun` are synchronous blocking methods. On single-threaded WASM they block the UI thread. Requires moving to a Web Worker or replacing with an async loop. |
| `NativeMemory` | ⚠️ | `NativeMemory.AllocZeroed`/`Free`/`Clear` work on WASM (emscripten linear memory). But allocating 2 GiB (`RamSize.GB2`) in a browser will almost certainly throw `OutOfMemoryException` — the WASM linear memory limit is ~4 GiB for the whole process, with fragmentation. `MemoryBus`/`Device` do not check this limit. |
| `unsafe` / `byte*` | ✅ | `NativeMemoryBuffer` is `unsafe`; WASM supports pointers. `Unsafe.ReadUnaligned`/`WriteUnaligned` work. |
| `stackalloc` / `InlineArray` | ✅ | `stackalloc` is not used in hot paths, except `Processor.CurrentProcessorSnapshot` (32 × 8 bytes per access). `[InlineArray(32)]` in `Processor` — compile-time, AOT-friendly. |
| `Interlocked` / `Volatile` | ⚠️ | `Interlocked.Exchange` in `MemoryBus.Dispose` and `Device.Dispose`, `Volatile.Read/Write` in `NativeMemoryBuffer.Dispose` and `Processor` — on single-threaded WASM (without `SharedArrayBuffer`) this is a no-op, but the code works. With threaded WASM — atomic. |
| `ConcurrentQueue<T>` | ✅ | Two queues in `Processor` (`_externalCommands`, `_statusFromSimulation`). On single-threaded WASM — just a queue; on threaded — lock-free. |
| `ManualResetEventSlim.Wait(...)` | ⚠️⚠️ | In `Device.RunSimulationLoop`/`RunSimulationLoopWithLimit`/`RunSimulationLoopDebug` — `_wakeSignal.Wait(_processorPolicy.SleepWaitTimeout)` blocks the current thread when `IsSleeping`. In a browser — blocks the UI. Replacement: `await Task.Delay` + async loop, or a Web Worker. |
| `Thread.Sleep(delay)` | ⚠️⚠️ | `RunSimulationLoopDebug` — `Thread.Sleep(delay)`. In a browser — blocks the UI. |
| `Stopwatch` | ✅ | `Run` with `snowTimer: true` — `Stopwatch.StartNew()`; on WASM works via a `performance.now`-like timer. |
| `IFileService` | n/a | There is no interface in this batch of files. If it appears in other layers, the WASM implementation is separate. |
| `Regex` / trimming | ✅ | Regex is not used; no reflection, `Type.GetType`, `Activator.CreateInstance`, `dynamic`. Trimming-compatible. |
| AOT | ✅ | No `Expression.Compile`, `MakeGenericMethod`/`MakeGenericType`. The `switch` over opcodes in `Step` is AOT-friendly. Generic code — only `TryGetDevice<T>` (monomorphized for the `T` used). |

**Summary:** the code builds for WASM and AOT, but the **synchronous blocking execution model** (`Run`, `Thread.Sleep`, `ManualResetEventSlim.Wait`) is not suitable for browser-WASM in single-threaded mode. `NativeMemory.AllocZeroed` works, but large `RamSize` (≥ `MB512`–`GB1`) risk `OutOfMemoryException`. For production in a browser — Web Worker + async loop + RAM ≤ ~256 MiB.

### `Processor.Step` — Processing Order

1. `Volatile.Read(ref _hasCommand)` — if `true`, **first** `Volatile.Write(ref _hasCommand, false)` (reset before processing so that new commands arriving during processing are not lost), then `Volatile.Read(ref _hasExternalCommand)` -> `ProcessExternalCommandsSlow`, `Volatile.Read(ref _hasSimulationStatus)` -> `ProcessSimulationStatusSlow`.
2. If `!_isRunning | _isSleeping` — exit (note: `|`, not `||`).
3. `ip = rIP`; `rawInst = _ram.ReadInt32LEUnSafe(ip)`; `rIP += 4` (before calling the handler — the handler sees the next word already).
4. `switch` over the opcode -> `BiosStatus`.
5. If `dat != Success` — `_faultData = (dat, _lastFaultData, ip, opCode)`, `Volatile.Write(ref _isRunning, !IsTerminal(dat) && _processorPolicy.ShouldContinue(_faultData))`.

**Immediate operands:** `Get64IntData()` reads `rIP`, aligns up to 8 (`AlignUp(8)`), reads `ulong` via `_ram.ReadInt64LEUnSafe`, then **assigns `rIP = aligned_ip + 8`** — called by the handlers of `LOAD`/`STORE`/`LDI`/`JMP`/`JZ`/`JNZ`/`JG`/`JL`/`CALL` and their `*_UNSAFE` variants. `LOAD_IND`/`STORE_IND`/`CALL_IND`/`JMP_IND` and their `*_UNSAFE` — indirect via a register, do not call `Get64IntData`.

`CanRead64IntDataInst()` checks `AlignUp(rIP, 8) > _ram.Length - 8`; if `_ram.Length < 8`, underflow is possible (the check formally passes, the read goes out of bounds — it will not actually trigger only if `_ram.Length ≥ 8`).

### `rFL` Flags and Jumps

`ZeroFlagSet` — `(rFL & 1) != 0`; `NegativeFlagSet` — `(rFL & 2) != 0`. `CMP`/`TEST` set `zeroFlag | (negativeFlag << 1)` via bit hacks. `JG` checks `!Negative & !Zero`, `JL` — `Negative` only, `JZ`/`JNZ` — `Zero`/`!Zero`. `rFL` is reset to `0` on `LaunchProgramm`.

### Stack and Heap

`rSP` is initialized in `LaunchProgramm` as `_ram.Length & ~0x7UL`; `rHP` — in `Device.InitHeap` as `(loadAddress + programLength).AlignUp(8)`. `PUSH`/`CALL` decrement `rSP` by 8 and check the `rHP >= sp` collision; `POP`/`RET` increment by 8. `RET` checks `sp >= _stackBase` (empty stack -> `StackUnderflow`). With `rHP = 0` (e.g., `BreakPointerLaunchDevice` without `LoadProgram`), the `rHP >= sp` check passes everything except `sp == 0`.

### Thread Safety

- `Processor.Step`/`LaunchProgramm`/`Reset` — not thread-safe.
- `EnqueueBiosStatus`/`EnqueueExternalCommand` — safe from another thread (queue + `Volatile.Write`), but `EnqueueBiosStatus` has an inverted flag write order (see [What to Fix](#what-to-fix)).
- `Device.Stop`/`WakeProcessor`/`EnqueueExternalCommand` — safe (delegate to `Processor` + `_wakeSignal.Set()`).
- `PortBus.RegisterDevice`/`UnregisterDevice` — not thread-safe.
- `ManagerDevices`/`Emulator` — not thread-safe (see `<remarks>`).
- `MemoryBus` — not thread-safe (`_lastStatus`/`_faultData` without synchronization).
- `NativeMemoryBuffer.Dispose` — `Volatile.Read/Write` on `bool`, but check-then-act is not atomic; with simultaneous `Dispose` from two threads, double-free is possible.
- `Processor.CurrentProcessorSnapshot` — not thread-safe (reads registers without synchronization with `Step`); `_faultData` itself is also read without `Volatile`, so the snapshot may be slightly "smeared" relative to the last step.

### Platform Limitations

- Target framework: `net10.0`; `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`.
- `unsafe` — only in `NativeMemoryBuffer`; `MemoryBus`/`PortBus`/`Processor`/`Device` — managed.
- `[MethodImpl(AggressiveInlining)]`/`[AggressiveOptimization]` — JIT optimization; in AOT/WASM the inliner behaves differently.
- `Nullable=enable`, `ImplicitUsings=enable`.

### What This Module Does Not Do

- Does not contain an `IFileService` implementation — only works with RAM and ports.
- Does not provide an async API — everything is synchronous (`Run`, `NextStep*`).
- Does not manage the process lifecycle — that is `VmApplication`.
- Does not parse `.bin`/`.nanas` bytecode — only executes 32-bit words via `Processor.Step`.

## What to Fix

Brief list of known defects (not XML):

- **`EnqueueBiosStatus` — `Volatile.Write` order.** Currently `_hasCommand` is written, then `_hasSimulationStatus`. If `Step` reads `_hasCommand`, resets it, and manages to read `_hasSimulationStatus` before the second thread writes it, the status will "stick" in the queue until the next `Enqueue`/`Reset`. The correct order is as in `EnqueueExternalCommand`: first the "internal" flag, then `_hasCommand`.
- **`Processor.CurrentProcessorSnapshot` creates a new object on every access.** For hot debug loops — excessive; either `GetCurrentProcessorSnapshot(Span<ulong>)` or a reusable instance is needed.
- **`Get64IntData` sets `rIP = aligned_ip + 8`, not `rIP += 8`.** In combination with `rIP += 4` in `Step` this is correct, but with an unaligned `rIP` it skips extra padding bytes; on unaligned start addresses it behaves non-obviously.
- **`CanRead64IntDataInst`: `_ram.Length - 8`.** If `_ram.Length < 8` — underflow, the check passes, the read goes out of bounds. An explicit `Length < 8` check is needed.
- **`Reset()` does not reset `_faultData`.** After a reset, `GetDiagnosticData()` returns the previous run's failure.
- **`InstructionPUSH`: stack check order.** First `sp < 8` is checked, then `rHP >= sp` already for the decremented `sp`. With `rHP = 0` and `sp = 0` — false trigger.
- **`_lastStatus`/`_faultData` in `MemoryBus` without `Volatile`/synchronization.**
- **`NativeMemoryBuffer.Dispose` — non-atomic check-then-act**, double-free is possible.
- **`ManagerDevices.Dispose` does not remove devices from `PortBus`** — `_portBus._devices[sector]` remains a reference to the disposed controller.
- **Synchronous `Run`/`DebugRun` and `ManualResetEventSlim.Wait`/`Thread.Sleep`** block the UI on single-threaded WASM.