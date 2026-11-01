# Nantuc.Kernel — README

> The core of the Nantuc virtual machine: processor, RAM, port bus, device registry, and emulator.  
> The only layer of the platform that uses `unsafe`, `NativeMemory`, `[InlineArray]`, and `ConcurrentQueue`.  
> `[← Root](../README.md) · [Common](../Nantuc.Kernel.Common/README.md) · [Contracts](../Nantuc.Kernel.Contracts/README.md)`

## Table of contents
- [About this module](#about-this-module)
- [Local project tree](#local-project-tree)
- [API](#api)
- [API examples](#api-examples)
- [How close the module is to the user](#how-close-the-module-is-to-the-user)
- [Special information](#special-information)
- [What to fix](#what-to-fix)

## About this module

`Nantuc.Kernel` is the execution layer. Here live: the processor (`Processor` + `ProcessorSnapshot`), the managed wrapper over native RAM (`MemoryBus` + `NativeMemoryBuffer` + `NativeReadOnlyView`), the port bus (`PortBus`), the device registry (`ManagerDevices`) and the emulator (`Emulator`), as well as the "computing device" (`Device`), which owns RAM and the processor and implements `IPortController` from `Contracts`.

The module is the only one in the platform where `unsafe` is allowed (`<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` in `Nantuc.Kernel.csproj`). Everything native is in `NativeMemoryBuffer`: `NativeMemory.AllocZeroed`, `NativeMemory.Free`, `byte*`, `Unsafe.ReadUnaligned` / `Unsafe.WriteUnaligned`. The rest of the code works with managed wrappers.

Key roles of the module:
- RAM: `MemoryBus` (checked/unchecked typed operations), `NativeMemoryBuffer` (the native buffer itself), `NativeReadOnlyView` (a read-only view with checks).
- Processor: `Processor` — `Step()`, decoding via `InstructionDecoder` from `Common`, registers `[InlineArray(32)]`, flags `rFL`, queues of external commands/statuses. `ProcessorSnapshot` — a state snapshot for external observers.
- Bus and devices: `PortBus`, `ManagerDevices`, `Device`, `Emulator`.
- Integration: implements `IPortBus`, `IPortController`, `IReadOnlyNativeBuffer`, `ProcessorSnapshot` from `Contracts`.

The module depends on `Common` (ISA types) and `Contracts` (interfaces). It does not depend on `VmApplication`/`Extensions` — the dependency arrow always points inward.

> **XML documentation is complete on the public surface.** All public types, methods, and parameters are documented. The remaining defects are not XML but behavior: the `_hasCommand` race in `Processor.Step`, the missing `ObjectDisposedException` on `MemoryBus.ReadInt*LE` after `Dispose`, the stack check order in `InstructionPUSH`, the blocking `_wakeSignal.Wait(15)` on single-threaded WASM. Details — in the [What to fix](#what-to-fix) section.

## Local project tree

Namespaces by layer, files flat within them:

```
Nantuc.Kernel/
├── Nantuc.Kernel.csproj               # net10.0, AllowUnsafeBlocks=true, refs Common+Contracts
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
- The namespace `Utilites` (with a typo — should be `Utilities`) contains `Emulator` and `ManagerDevices`.
- `Processor` is `sealed` and uses `[InlineArray(CountReg)]` (32 × `ulong`) instead of an array: more compact for the GC, AOT-friendly.
- `NativeMemoryBuffer` — `unsafe sealed class`; everything native is only here. The other classes work through `MemoryBus`/`IReadOnlyNativeBuffer`.
- `Device` implements `IPortController`, not just `IPortUse`: it has `_ioMemory` and `_wakeSignal`, which need to be freed.
- `Emulator` is the entry point for the consumer; `ManagerDevices` is an implementation detail (public, but mainly needed by `Emulator`).
- `Processor.GetCurrentProcessorSnapshot` creates a **new** `ProcessorSnapshot` on each call (not a reused instance).

## API

### Overall picture

Public surface by namespace:

| Type | Namespace | Kind | Purpose |
|---|---|---|---|
| `MemoryBus` | `RamSystem` | `class : IDisposable` | Checked `ReadInt*LE`/`WriteInt*LE`, `*UnSafe` variants, `UpdateAndReadStatus`, `FaultData`, `NativeReadOnlyBuffer` |
| `NativeMemoryBuffer` | `RamSystem` | `unsafe sealed class : IDisposable` | `NativeMemory.AllocZeroed`, `ReadUInt*`/`WriteUInt*`, indexer, `Clear`, `LoadProgram`, `IsDisposed` |
| `NativeReadOnlyView` | `RamSystem` | `class : IReadOnlyNativeBuffer` | `ObjectDisposedException` + `ArgumentOutOfRangeException` checks on indexing |
| `Processor` | `ProcessorSystem` | `sealed class` | `Step()`, `LaunchProgramm`, `Reset`, queues `EnqueueBiosStatus`/`EnqueueExternalCommand`, `GetDiagnosticData`, `GetRegistersSnapshot`, `CopyRegisters`, `GetCurrentProcessorSnapshot` |
| `ProcessorSnapshot` | `ProcessorSystem` | `readonly struct : ProcessorSnapshot` | State snapshot: `IsRunning`, `IsSleeping`, a copy of 32 registers, `GetRegister(RegType)`, the current instruction and `imm64` |
| `PortBus` | `ControllersData` | `sealed class : IPortBus` | Sector bus: `RegisterDevice`, `ReadPort`, `WritePort`, `WakeProcessor`, `IsFreeSector` |
| `Device` | `BiosSystem` | `sealed class : IPortController` | `Run`, `DebugRun`, `NextStepProcessor*`, `LoadProgram`, `TryLoadProgram`, `Stop`, `WakeProcessor`, `EnqueueExternalCommand`, `GetLastFault` |
| `Emulator` | `Utilites` | `class : IDisposable` | `CreateDevice`, `RegisterDevice`, `GetDevice`, `TryGetDevice`, `UnregisterDevice`, `ChangeDeviceSector`, `AllDeviceInfo` |
| `ManagerDevices` | `Utilites` | `class : IDisposable` | `TryAddDevice`, `RemoveDevice`, `ChangeSector`, `GetDevice`, `TryGetDevice`, `GetAllDevices`, `Count` |

### `MemoryBus` — RAM

The checked methods **do not throw**: on error they return `0` and store the status, which is read via `UpdateAndReadStatus()` (and reset to `Success`). The `*UnSafe` variants — without bounds and alignment checks.

| Method | What it does | Checks |
|---|---|---|
| `ReadInt8LE(ulong)` | reads a byte | upper bound only |
| `ReadInt16LE`/`ReadInt32LE`/`ReadInt64LE` | reads an LE value | alignment to 2/4/8 + bound |
| `WriteInt8LE`/`WriteInt16LE`/`WriteInt32LE`/`WriteInt64LE` | writes an LE value | same |
| `ReadInt8LEUnSafe`/`WriteInt8LEUnSafe`… | without checks | none |
| `UpdateAndReadStatus()` | returns and resets `_lastStatus` | — |
| `LoadProgram(ReadOnlySpan<byte>, ulong)` | copies the program into RAM | `ArgumentOutOfRangeException` on out-of-bounds |
| `ClearMemory()` | zeroes the buffer | — |

**Status race:** `_lastStatus` and `_faultData` are not `volatile` and are not protected by a lock. `UpdateAndReadStatus` resets the status — the consumer must call it after each checked operation, otherwise the error will "stick".

### `NativeMemoryBuffer` — native buffer

The only `unsafe` class. After `Dispose`, all `Read*`/`Write*`/indexer are **UB**: `_ptrRam` becomes `null`, and `*(_ptrRam + index)` — NRE or AV. Protection exists only in `NativeReadOnlyView` (`ObjectDisposedException.ThrowIf(buffer.IsDisposed, buffer)`).

- `NativeMemory.AllocZeroed(nuint)` — allocates zeroed memory; on failure — `OutOfMemoryException` (already a `NativeMemory`-level error).
- `ReadUInt16/32/64` — `Unsafe.ReadUnaligned<T>` + `BitConverter.IsLittleEndian` (on WASM `IsLittleEndian == true`, so `ReverseEndianness` never executes).
- `this[ulong index]` — `*(_ptrRam + index)`, without checks.
- `LoadProgram(ReadOnlySpan<byte>, ulong)` — `NativeMemory.Copy(src, _ptrRam + startProg, (nuint)prog.Length)`; checks only `prog.Length > 0` and bounds.
- `Dispose` — `Volatile.Read/Write` on `bool`; **not thread-safe with respect to repeated calls** (see [What to fix](#what-to-fix)).

### `NativeReadOnlyView` — read-only view

`IReadOnlyNativeBuffer` from `Contracts`. On indexing it checks `IsDisposed` and `index < Length` — the **only** place where, after `Dispose`, `ObjectDisposedException` is thrown instead of UB.

### `Processor` — processor

| Method | Purpose |
|---|---|
| `Step()` | processes the queues (if `_hasCommand`), reads the 32-bit word at `rIP`, increments `rIP` by 4, calls the opcode handler |
| `LaunchProgramm(ulong)` | resets `rFL`, sets `rIP`, `rSP` (top of RAM, aligned to 8), `IsRunning = true` |
| `Reset()` | zeroes the registers, clears the queues, resets the flags |
| `EnqueueBiosStatus(BiosStatus)` | puts a status into `_statusFromSimulation` + `Volatile.Write(_hasSimulationStatus, true)` + `Volatile.Write(_hasCommand, true)` |
| `EnqueueExternalCommand(uint)` | puts a raw word into `_externalCommands` + `Volatile.Write(_hasExternalCommand, true)` + `Volatile.Write(_hasCommand, true)` |
| `GetDiagnosticData()` | a `ProcessorFault` snapshot |
| `GetRegValue(RegType)` | a single value |
| `CopyRegisters(Span<ulong>)` / `GetRegistersSnapshot()` | 32 registers |
| `GetCurrentProcessorSnapshot()` | a **new** `ProcessorSnapshot` with a copy of the registers, `IsRunning`, `IsSleeping` |
| `InitRegHP(ulong)` | sets `rHP` |
| `IsRunning`/`IsSleeping` | flags |

Registers — `[InlineArray(CountReg)]` of `ulong` (32 × 8 bytes = 256 bytes per instance). `rFL` — a bit field: bit 0 = Zero, bit 1 = Negative; it is set **only** by `CMP`/`TEST` (see `<remarks>` on `OpCode`).

`Step()` — a `switch` expression over all ~60 opcodes, with `[MethodImpl(AggressiveOptimization)]`. The `Instruction*` handler returns `BiosStatus`; on `!= Success` — fills `_faultData`, calls `_processorFaultPolicy.ShouldContinue` and `Volatile.Write(_isRunning, ...)`.

**Immediate 64-bit operand:** `Get64IntData()` reads `rIP`, aligns it up to 8 (`(ip + 7) & ~7UL`), reads a `ulong` via `_ram.ReadInt64LEUnSafe`, increments `rIP` by 8. So after a 4-byte instruction there are up to 4 bytes of padding, and the immediate starts at the nearest 8-byte boundary.

### `ProcessorSnapshot` — state snapshot

Created by `Processor.GetCurrentProcessorSnapshot()`. Contains `IsRunning`, `IsSleeping`, and a copy of the 32 registers. Has `GetRegister(RegType)`. Since the snapshot is an independent copy, it can be safely stored; on the next step the state will not change.

### `PortBus` — sector port bus

- The address is decomposed into a "sector" (`address >> _deviceShift`) and an "offset" (`address & _offsetMask`).
- `RegisterDevice(IPortUse, uint)` — `IPortUse`, not `IPortController` (the latter extends `IPortUse`).
- `ReadPort` returns `ReadPortResult(Data, Status)`; `Status = NullDeviceInput` if there is no device at the address.
- `WritePort` returns `BiosStatus`: `Success` or `NullDeviceOutput`.
- `WakeProcessor` — `bool` (whether a device was found).
- `RegisterDevice`/`UnregisterDevice` — direct control of the `_devices` array; **there is no thread safety** (see `<remarks>` on the type).

### `Device` — computing device

Its own `MemoryBus` + `Processor` + `byte[] _ioMemory` + `ManualResetEventSlim _wakeSignal`. Implements `IPortController` — can be registered on `PortBus` via `Emulator.RegisterDevice`.

| Method | Purpose |
|---|---|
| `Run(ulong, long?, bool, Action?, Action<ITimerSimulationResult?>?)` | blocking run until completion/fault; `limit` — step limit → `InfinityLoop` |
| `DebugRun(ulong, long, int, Action?, Action<ProcessorSnapshot>?, Action?)` | run with a delay and a per-step callback |
| `BreakPointerLaunchDevice(ulong?, Action?)` | initialization without starting the loop |
| `NextStepProcessor(Action<ProcessorSnapshot>?)` / `NextStepProcessorCount(int, Action<ProcessorSnapshot>?)` | manual steps |
| `LoadProgram(ReadOnlySpan<byte>, ulong)` / `TryLoadProgram` | loading + `rHP` initialization (`programEnd.AlignUp(8)`) |
| `Stop()` / `WakeProcessor()` / `EnqueueExternalCommand(uint)` | from another thread: `Enqueue*` + `_wakeSignal.Set()` |
| `CopyRegisters` / `GetRegistersSnapshot` / `GetLastFault` / `ResetMemoryRam` | snapshots |
| `ReadPort` / `WritePort` | **no bounds checks** — return `0`/ignore when out of `_ioMemory.Length` (in the type's `<remarks>` — "I/O ports"; the implementation does not throw) |

`Stop`/`WakeProcessor`/`EnqueueExternalCommand` are the only methods safe from another thread (they use `Enqueue*` + `_wakeSignal.Set()`). `Run`/`DebugRun`/`NextStep*` are not.

### `Emulator` — entry point

- Constructor `Emulator(PortSize ports, DevicePortSize portsOnDev)` — creates `PortBus` and `ManagerDevices`.
- `CreateDevice(RamSize, IProcessorFaultPolicy)` — a new `Device`.
- `RegisterDevice(IPortController, string?, uint, out int)` — registers an external controller; on failure it **disposes** `device` (`device.Dispose()` inside `ManagerDevices.TryAddDevice`).
- `GetDevice(int)` / `TryGetDevice<T>(int, out T?)` — by `Id`.
- `UnregisterDevice(int)` / `ChangeDeviceSector(int, uint)`.

### `ManagerDevices` — registry

- `TryAddDevice` — `IdState.Valid` on success; otherwise `NotAssigned` (registry full), `OutOfRange` (sector occupied), `NoFreeSlot` (no slot and no ID), `Rejected` (`PortBus.RegisterDevice` returned `false`, the device is **disposed**). `out int id` on failure gets `-1`/`-2`/`-3`/`-4`.
- Slots (`DeviceEntry`) are reused: `IsFreed = true` preserves `Id` so that on re-registration the `Id` stays the same.
- `Count` — O(n) every time (not cached); called in `TryAddDevice` on every add.
- `Dispose` **does not remove** devices from `PortBus` — `_portBus._devices[sector]` still references the disposed controller (see [What to fix](#what-to-fix)).

## API examples

**Minimal launch:**

```csharp
using Nantuc.Kernel.Common;
using Nantuc.Kernel.Contracts;
using Nantuc.Kernel.Utilites;

using var emulator = new Emulator(PortSize.KB64, DevicePortSize.B64);

using Device device = emulator.CreateDevice(RamSize.MB16, IProcessorFaultPolicy.Default);

byte[] program = [/* bytes: 32-bit instruction words */];
device.LoadProgram(program, loadAddress: 0x0);

ProcessorFault fault = device.Run(
    start: 0,
    limit: 10_000,
    snowTimer: true,
    onLaunch: () => Console.WriteLine("start"),
    onEnd: r => Console.WriteLine($"steps={r?.Steps}, elapsed={r?.Elapsed}"));

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

**Direct RAM reading:**

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

## How close the module is to the user

Medium (higher than `Common`/`Contracts`, lower than `VmApplication`).

- The author of device extensions works with `Emulator`/`Device`/`PortBus` — this is their main API.
- The `VmApplication` user does not import this layer directly, but it is `Device.Run`/`DebugRun` that are called from `VmApplication`.
- The compiler author does not work with this layer — only with `Common`/`Contracts`.
- For production code embedding the emulator: `Emulator` is the entry point; `ManagerDevices` is a detail.

**What you should do:**
- Use `Emulator.CreateDevice` + `Device.Run`, not create `Device`/`Processor`/`MemoryBus` manually.
- After each checked `MemoryBus` operation — call `UpdateAndReadStatus()`; the status is reset.
- Remember that `ReadPort`/`WritePort` in `Device` **do not throw** — out-of-bounds yields `0`/ignores, not an exception.
- Call `Stop`/`WakeProcessor`/`EnqueueExternalCommand` from another thread, and `Run`/`NextStep*` — only from a single coordinator thread.
- After `device.Dispose()`, read RAM only through `NativeReadOnlyBuffer` — there you will get `ObjectDisposedException`, not UB.
- `Processor.GetCurrentProcessorSnapshot()` returns a new `ProcessorSnapshot`; it can be saved and used after the next step.

**What you should not do:**
- Rely on `_lastStatus` between operations without `UpdateAndReadStatus` — the status will "stick".
- Read/write `_ioMemory`/`NativeMemoryBuffer` directly from another thread during `Run`.
- Register/unregister devices (`RegisterDevice`/`UnregisterDevice`/`ChangeDeviceSector`) while `Device.Run` is running — there is no synchronization.
- Use `NativeMemoryBuffer` directly after `Dispose` — UB, not an exception (only `NativeReadOnlyView` is protected).
- Keep an `IReadOnlyNativeBuffer` obtained from `Device.NativeReadOnlyBuffer` after the device's `Dispose()` — `ObjectDisposedException` on the next indexing.

## Special information

### WASM / cross-platform

| Aspect | Status | Comment |
|---|---|---|
| Building for WASM | ⚠️ | Builds for `browser-wasm` without csproj changes, but `Device.Run`/`DebugRun` are synchronous blocking methods. On single-threaded WASM they block the UI thread. Requires moving to a Web Worker or replacing with an async loop. |
| `NativeMemory` | ⚠️ | `NativeMemory.AllocZeroed`/`Free`/`Clear` work on WASM (emscripten linear memory). But allocating 2 GiB (`RamSize.GB2`) in a browser will almost certainly throw `OutOfMemoryException` — the WASM linear memory limit is ~4 GiB for the whole process, with fragmentation. `MemoryBus`/`Device` do not check this limit. |
| `unsafe` / `byte*` | ✅ | `NativeMemoryBuffer` is `unsafe`; WASM supports pointers. `Unsafe.ReadUnaligned`/`WriteUnaligned` work. |
| `stackalloc` / `InlineArray` | ✅ | `stackalloc` is not used in hot paths, except `Processor.GetCurrentProcessorSnapshot` (32 × 8 bytes). `[InlineArray(32)]` in `Processor` — compile-time, AOT-friendly. |
| `Interlocked` / `Volatile` | ⚠️ | `Interlocked.Exchange` in `MemoryBus.Dispose` and `Device.Dispose`, `Volatile.Read/Write` in `NativeMemoryBuffer.Dispose` and `Processor` — on single-threaded WASM (without `SharedArrayBuffer`) this is a no-op, but the code works. With threaded WASM — atomic. **Inconsistent use**: see [What to fix](#what-to-fix). |
| `ConcurrentQueue<T>` | ✅ | Two queues in `Processor` (`_externalCommands`, `_statusFromSimulation`). On single-threaded WASM — just a queue; on threaded — lock-free. |
| `ManualResetEventSlim.Wait(15)` | ⚠️⚠️ | In `Device.RunSimulationLoop`/`RunSimulationLoopWithLimit`/`RunSimulationLoopDebug` — `_wakeSignal.Wait(15)` blocks the current thread for 15 ms when `IsSleeping`. In a browser — blocks the UI. Replacement: `await Task.Delay` + async loop, or a Web Worker. |
| `Thread.Sleep(delay)` | ⚠️⚠️ | `RunSimulationLoopDebug` — `Thread.Sleep(delay)`. In a browser — blocks the UI. |
| `Stopwatch` | ✅ | `Run` with `snowTimer: true` — `Stopwatch.StartNew()`; on WASM it works via a `performance.now`-like timer. |
| `IFileService` | n/a | There is no such interface in this batch of files. If it appears in other layers, the WASM implementation is separate. |
| `Regex` / trimming | ✅ | Regex is not used; there is no reflection, `Type.GetType`, `Activator.CreateInstance`, `dynamic`. Trimming-compatible. |
| AOT | ✅ | No `Expression.Compile`, `MakeGenericMethod`/`MakeGenericType`. The `switch` on opcodes in `Step` is AOT-friendly. Generic code — only `TryGetDevice<T>` (monomorphized for the used `T`). |

**Bottom line:** the code builds for WASM and AOT, but the **synchronous blocking execution model** (`Run`, `Thread.Sleep`, `ManualResetEventSlim.Wait`) is not suitable for browser-WASM in single-threaded mode. `NativeMemory.AllocZeroed` works, but large `RamSize` (≥ `MB512`–`GB1`) risk `OutOfMemoryException`. For production in a browser — Web Worker + async loop + RAM ≤ ~256 MiB.

### `Processor.Step` — processing order

1. If `_hasCommand` (not a `volatile` read!) — if `_hasExternalCommand`, call `ProcessExternalCommandsSlow`; if `_hasSimulationStatus` — `ProcessSimulationStatusSlow`. Then `_hasCommand = false` (**not a `volatile` write**).
2. If `!_isRunning | _isSleeping` — exit (note: `|`, not `||`).
3. `ip = rIP`; `rawInst = _ram.ReadInt32LEUnSafe(ip)`; `rIP += 4` (before the handler call — the handler already sees the next word).
4. `switch` on the opcode → `BiosStatus`.
5. If `dat != Success` — `_faultData = (dat, _lastFaultData, ip, opCode)`, `Volatile.Write(_isRunning, ShouldContinue(...))`.

**Immediate operands:** `Get64IntData()` reads `rIP`, aligns it up to 8, reads a `ulong`, increments `rIP` by 8 — called by the `LOAD`/`STORE`/`LDI`/`JMP`/`JZ`/`JNZ`/`JG`/`JL`/`CALL` handlers and their `*_UNSAFE` variants. `LOAD_IND`/`STORE_IND`/`CALL_IND`/`JMP_IND` and their `*_UNSAFE` — indirect through a register, do not call `Get64IntData`.

### `rFL` flags and jumps

`ZeroFlagSet` — `(rFL & 1) != 0`; `NegativeFlagSet` — `(rFL & 2) != 0`. `CMP`/`TEST` set `zeroFlag | (negativeFlag << 1)` via bit hacks. `JG` checks `!Negative & !Zero`, `JL` — only `Negative`, `JZ`/`JNZ` — `Zero`/`!Zero`. `rFL` is reset to `0` on `LaunchProgramm`.

### Stack and heap

`rSP` is initialized in `LaunchProgramm` as `_ram.Length & ~0x7UL`; `rHP` — in `Device.InitHeap` as `(loadAddress + programLength).AlignUp(8)`. `PUSH`/`CALL` decrement `rSP` by 8 and check the collision `rHP >= sp`; `POP`/`RET` increment by 8. `RET` checks `sp >= _stackBase` (empty stack → `StackUnderflow`).

### Thread safety

- `Processor.Step`/`LaunchProgramm`/`Reset` — not thread-safe.
- `EnqueueBiosStatus`/`EnqueueExternalCommand` — safe from another thread (queue + `Volatile.Write`).
- `Device.Stop`/`WakeProcessor`/`EnqueueExternalCommand` — safe (delegate to `Processor` + `_wakeSignal.Set()`).
- `PortBus.RegisterDevice`/`UnregisterDevice` — not thread-safe.
- `ManagerDevices`/`Emulator` — not thread-safe (see `<remarks>`).
- `MemoryBus` — not thread-safe (`_lastStatus`/`_faultData` without synchronization).
- `NativeMemoryBuffer.Dispose` — `Volatile.Read/Write` on a `bool`, but check-then-act is not atomic; with simultaneous `Dispose` from two threads a double-free is possible.
- `Processor.GetCurrentProcessorSnapshot` — not thread-safe (reads registers without synchronization with `Step`).

### Platform limitations

- Target framework: `net10.0`; `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`.
- `unsafe` — only in `NativeMemoryBuffer`; `MemoryBus`/`PortBus`/`Processor`/`Device` — managed.
- `[MethodImpl(AggressiveInlining)]`/`[AggressiveOptimization]` — JIT optimization; in AOT/WASM the inliner behaves differently.
- `Nullable=enable`, `ImplicitUsings=enable`.

### What this module does not do

- It does not contain an `IFileService` implementation — it only works with RAM and ports.
- It does not provide an async API — everything is synchronous (`Run`, `NextStep*`).
- It does not manage the process lifecycle — that is `VmApplication`.
- It does not parse `.bin`/`.nanas` bytecode — it only executes 32-bit words via `Processor.Step`.

## What to fix

*(This section was not present in the provided source; the document ends at "What this module does not do". If you have the "What to fix" content, please provide it and I'll translate it as well.)*