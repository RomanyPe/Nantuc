# Nantuc.Contracts — README

> Nantuc platform contracts: interfaces for the port bus, processor, fault policies, and optimizations.  
> The only layer that `Kernel`, `VmApplication`, and `Extensions` all depend on.  
> `[← Root](../README.md) · [Common](../../src/Nantuc.Common/README.md) · [Kernel](../../src/Nantuc.Kernel/README.md)`

## Table of contents
- [About this module](#about-this-module)
- [Local project tree](#local-project-tree)
- [API](#api)
- [API examples](#api-examples)
- [How close the module is to the user](#how-close-the-module-is-to-the-user)
- [Special information](#special-information)
- [What to fix](#what-to-fix)

## About this module

`Nantuc.Contracts` is the platform's abstraction layer. There is no processor logic, no buses, no devices, no state here: only `interface`s, `readonly record struct`s for results, and one `readonly struct` with validation (`OptimizationId`). Everything the kernel user should be able to call is declared here; the implementation lives in `Kernel`/`VmApplication`/`Extensions`.

The module depends only on `Nantuc.Common` (for `BiosStatus`, `ProcessorFault`, `OpCode`, `RegType`). `Kernel` depends on `Contracts`, `VmApplication` — on `Kernel`, `Extensions` — on `Contracts`/`Common`. `Contracts` is the single point where these three layers converge without circular references.

Key roles of the module:
- Port bus: `IPortBus`, `IPortUse`, `IPortController`, `ReadPortResult`.
- Processor: `ProcessorSnapshot`, `IProcessorFaultPolicy` (with a cached `Default`).
- Optimizations: `OptimizationId`, `OptimizationLogEntry`, `IReadOnlyLogOptimization`.
- Measurement results: `ITimerSimulationResult`.
- Devices: `DeviceInfo`, `IReadOnlyNativeBuffer`.

The project is a leaf in terms of dependencies (only `Common`) and contains no `unsafe`, `NativeMemory`, `Regex`, or reflection.

> **XML documentation is almost complete.** `ITimerSimulationResult`, `OptimizationId`, `IReadOnlyNativeBuffer`, `IPortBus`, `IPortUse`, `IPortController`, `ProcessorSnapshot`, `IReadOnlyLogOptimization`, `IProcessorFaultPolicy` are documented. All trivial DTOs (`OptimizationLogEntry`, `ReadPortResult`, `DeviceInfo`) also have `///` on the struct and `<param>` on the parameters. The remaining gaps are semantics (behavior on errors, `Dispose` order, valid `RegType` values) and one factual promise in the `OptimizationId` XML that is not backed by code. Details — in the [What to fix](#what-to-fix) section. *(Note: the "What to fix" section was not present in the provided source.)*

## Local project tree

All types live in the namespace `Nantuc.Contracts`, the structure is flat:

```
Nantuc.Contracts/
├── Nantuc.Contracts.csproj    # net10.0, ProjectReference: Common
├── DeviceInfo.cs                     # readonly record struct DeviceInfo
├── IReadOnlyNativeBuffer.cs          # interface IReadOnlyNativeBuffer
├── IPortBus.cs                       # interface IPortBus
├── IPortController.cs                # interface IPortController : IPortUse, IDisposable
├── IPortUse.cs                       # interface IPortUse
├── IProcessorFaultPolicy.cs          # interface IProcessorFaultPolicy + nested DefaultProcessorFaultPolicy
├── ProcessorSnapshot.cs              # readonly struct ProcessorSnapshot, [InlineArray(32)]
├── IReadOnlyLogOptimization.cs       # interface IReadOnlyLogOptimization
├── ITimerSimulationResult.cs         # interface ITimerSimulationResult
├── OptimizationId.cs                 # readonly struct OptimizationId
├── OptimizationLogEntry.cs           # readonly record struct OptimizationLogEntry
└── ReadPortResult.cs                 # readonly record struct ReadPortResult
```

Organization notes:
- Files `OptimizationId.cs` and `OptimizationLogEntry.cs` — the file name matches the type name (previously there was `TypeOptimization.cs` with the type `OptimizationId` — now renamed).
- `IPortController.cs` — one-liner, but with `///` and `<see>` references to `IPortUse`/`IDisposable`.
- `IProcessorFaultPolicy` contains `private class DefaultProcessorFaultPolicy` — the policy implementation lives inside the interface. This is non-standard for contracts, but deliberate: the "default policy" is part of the contract.
- `ProcessorSnapshot` — a `readonly struct` with `[InlineArray(RegisterCount)] (RegisterCount = 32)` for storing a copy of the registers. Two constructors:
    - `(Span<ulong> span, bool isRunning, bool isSleeping, uint rawInstruction, ulong dataByInstruction)` — `CurrentBiosStatus = BiosStatus.Success`;
    - `(..., BiosStatus currentBiosStatus)` — same plus an explicit status.
    - `GetArr(ReadOnlySpan<ulong>)` copies exactly RegisterCount = 32 elements and throws `ArgumentException` when `span.Length` ≠ 32 (previously — `IndexOutOfRangeException` when > 32 and silent zeros when < 32).

## API

### Overall picture

The module has 7 `interface`s and 5 `struct`/`record struct`s. Public surface:

| Type | Kind | Purpose |
|---|---|---|
| `ITimerSimulationResult` | `interface` | `Elapsed`, `Steps` |
| `IPortBus` | `interface` | `ReadPort(ulong)`, `WritePort(ulong, byte)`, `WakeProcessor(ulong)` |
| `IPortUse` | `interface` | `ReadPort(ulong)`, `WritePort(ulong, byte)`, `WakeProcessor()` |
| `IPortController` | `interface : IPortUse, IDisposable` | Composition "device on the bus + disposal" |
| `ReadPortResult` | `readonly record struct` | `(byte Data, BiosStatus Status = Success)` |
| `ProcessorSnapshot` | `readonly struct` | `IsRunning`, `IsSleeping`, `GetRegister(RegType)`; `[InlineArray(RegisterCount)]` storage (32); `RawInstruction`, `DataByInstruction`, `CurrentBiosStatus`, `DataFault`; decoded from `RawInstruction`: `OpCode`, `FirstRegister`, `SecondRegister`, `DataSizeCode` |
| `IProcessorFaultPolicy` | `interface` + `Default` | `ShouldContinue(in ProcessorFault)`; `Default` — cached singleton |
| `IReadOnlyLogOptimization` | `interface` | `Id`, `GetLogs()` |
| `OptimizationId` | `readonly struct` | Validated identifier `[a-z0-9.-]{1,128}` |
| `OptimizationLogEntry` | `readonly record struct` | `(int Pass, string Message)` |
| `DeviceInfo` | `readonly record struct` | `(int Id, uint Sector, string? Name)` |
| `IReadOnlyNativeBuffer` | `interface` | `Length: nuint`, `this[nuint]: byte` |

### Port bus

**`IPortBus`** — the "I/O only" port bus: no registrations, no validators. Addresses are `ulong`. `ReadPort` returns `ReadPortResult` (data + status), `WritePort` returns `BiosStatus`, `WakeProcessor` — `bool`. The XML explicitly instructs: "check `Status` first, then `Data`".

**`IPortUse`** — the contract of a device on the bus. Difference from `IPortBus`: `ReadPort` returns just `byte` without a status, `WakeProcessor()` — without an address (wakes itself). Offsets are relative to the start of the device's sector.

**`IPortController`** — the composition `IPortUse + IDisposable`, a marker for "device with resources". XML exists, but the `Dispose` order relative to `WakeProcessor`/bus operations is not fixed.

**`ReadPortResult`** — `readonly record struct (byte Data, BiosStatus Status = Success)`. The `Status = Success` default means: `new ReadPortResult(data)` will yield "success", even if the data came from an error. XML on `<param>` exists, but there is no warning about the default (see [What to fix](#what-to-fix), item 4. *(Note: the "What to fix" section was not present in the provided source.)*).

### Processor

`ProcessorSnapshot` — a `readonly struct` with `[InlineArray(RegisterCount)] (RegisterCount = 32)`-storage of registers. Two constructors:
- `(Span<ulong> span, bool isRunning, bool isSleeping, uint rawInstruction, ulong dataByInstruction)` — `CurrentBiosStatus` = `BiosStatus.Success`;
- `(Span<ulong> span, bool isRunning, bool isSleeping, uint rawInstruction, ulong dataByInstruction, BiosStatus currentBiosStatus)`.

`GetArr(ReadOnlySpan<ulong>)` copies exactly `RegisterCount` elements; when `span.Length` ≠ 32 — `ArgumentException` (see `/// <exception>` in the code). Previously, when > 32 there was `IndexOutOfRangeException`, when < 32 — silent zeros in the tail of the array.

Properties:

- `IsRunning`, `IsSleeping` — the processor mode at the time of the snapshot.
- `GetRegister(RegType)` — the register value; indexing without a range check, behavior for an invalid RegType is not documented (see What to fix, item 9. *(Note: the "What to fix" section was not present in the provided source.)*).
- `RawInstruction` — the raw instruction (32 bits), `DataByInstruction` — the 64-bit data that came with the instruction.
- `CurrentBiosStatus` — the BiosStatus at the time of the snapshot (in the simplified constructor — `Success`).
- `DataFault` — `DataByInstruction` if `CurrentBiosStatus != Success`, otherwise 0.

Decoded from `RawInstruction` via `InstructionDecoder`: `OpCode`, `FirstRegister`, `SecondRegister`, `DataSizeCode`.

There is no writing to `ProcessorSnapshot` — that is in `Kernel`; here it is read-only.

**`IProcessorFaultPolicy`** — the processor stop policy on `ProcessorFault`:

```csharp
public interface IProcessorFaultPolicy
{
    public static IProcessorFaultPolicy Default { get; } = new DefaultProcessorFaultPolicy();

    private class DefaultProcessorFaultPolicy : IProcessorFaultPolicy
    {
        public bool ShouldContinue(in ProcessorFault fault) => fault.Status switch
        {
            BiosStatus.Success => true,
            BiosStatus.NullDeviceOutput => true,
            _ => false,
        };
    }

    bool ShouldContinue(in ProcessorFault fault);
}
```

Semantics of `Default`: continue on `Success` and `NullDeviceOutput`, stop on any other status. `NullDeviceInput` is not included in "continue" — asymmetric (see [What to fix](#what-to-fix), item 2. *(Note: the "What to fix" section was not present in the provided source.)*). `Default` is a **static property with a cached instance** (static initializer, created once on first access to the interface type).

### Optimizations

**`OptimizationId`** — a `readonly struct` with validation. Accepts only `[a-z0-9.-]`, length 1..128. The constructor throws on invalid input, `TryParse` — does not throw (for parsing user input). `default(OptimizationId)` is the only invalid value (`IsValid == false`, `Value == null`). Comparison — `StringComparison.Ordinal` (`Equals`, `CompareTo` via `String.CompareOrdinal`). Culture does not affect — WASM-safe.

**`OptimizationLogEntry`** — `readonly record struct (int Pass, string Message)`. XML on the struct and `<param>` on `Pass`/`Message` exists. The summary wording ("Optimization log with protection against being overwritten by new logs") describes the motivation, not the type itself (see [What to fix](#what-to-fix), item 6. *(Note: the "What to fix" section was not present in the provided source.)*).

**`IReadOnlyLogOptimization`** — `Id: OptimizationId`, `GetLogs(): IReadOnlyList<OptimizationLogEntry>`. The comment "snapshot" remained inline (`// snapshot`), not in `///` (see [What to fix](#what-to-fix), item 3. *(Note: the "What to fix" section was not present in the provided source.)*).

### Devices and results

**`DeviceInfo`** — `readonly record struct (int Id, uint Sector, string? Name)`. XML on the struct and `<param>` exists. `Name` is `string?`; in `Common` there is `NameDeviceToken` (string cache, `IsUnknown`, Ordinal comparison) — the choice of `string?` is not documented (see [What to fix](#what-to-fix), item 7. *(Note: the "What to fix" section was not present in the provided source.)*).

**`IReadOnlyNativeBuffer`** — a read-only buffer: `Length: nuint`, `this[nuint]: byte`. Despite "Native" in the name, the interface itself is managed. The implementation may be over `NativeMemory`, but that is the concern of the implementing layer (see [WASM](#wasm--cross-platform)).

**`ITimerSimulationResult`** — `Elapsed: TimeSpan`, `Steps: long`. A read-only snapshot of measurements.

## API examples

**Reading/writing through the port bus:**

```csharp
using Nantuc.Common;
using Nantuc.Contracts;

void WriteByte(IPortBus bus, ulong address, byte value)
{
    BiosStatus status = bus.WritePort(address, value);
    if (status != BiosStatus.Success)
        Console.Error.WriteLine($"write failed: {status}");
}

byte ReadByteChecked(IPortBus bus, ulong address)
{
    ReadPortResult r = bus.ReadPort(address);
    if (r.Status != BiosStatus.Success)
        throw new InvalidOperationException($"read failed: {r.Status}");
    return r.Data;
}
```

**Implementing a device on the bus:**

```csharp
public sealed class UartDevice : IPortController
{
    public byte ReadPort(ulong offset) => offset switch
    {
        0 => 0x00, // RX (no data)
        _ => 0x00,
    };

    public void WritePort(ulong offset, byte value)
    {
        if (offset == 1) Console.Write((char)value); // TX
    }

    public void WakeProcessor() { /* nothing */ }

    public void Dispose() { /* release resources */ }
}
```

**Checking the policy after a fault:**

```csharp
IProcessorFaultPolicy policy = IProcessorFaultPolicy.Default;

ProcessorFault fault = new(
    Status: BiosStatus.DivOnZero,
    FaultData: 0,
    InstructionPointer: 0x40,
    OpCode: OpCode.DIV);

if (!policy.ShouldContinue(in fault))
    Console.Error.WriteLine($"stop: {fault.Status}");
```

**Working with an optimization identifier:**

```csharp
// Valid id
var id = new OptimizationId("peephole");
Console.WriteLine(id.Value);      // "peephole"
Console.WriteLine(id.IsValid);    // true

// Parsing user input
if (OptimizationId.TryParse("inline.func", out var parsed))
    Console.WriteLine(parsed);
else
    Console.Error.WriteLine("invalid id");

// default — the only invalid value
OptimizationId invalid = default;
Console.WriteLine(invalid.IsValid);          // false
Console.WriteLine(invalid);                   // "OptimizationId(invalid)"
```

**Snapshot of optimization logs:**

```csharp
IReadOnlyLogOptimization log = /* ... */;
OptimizationId id = log.Id;
IReadOnlyList<OptimizationLogEntry> entries = log.GetLogs();

foreach (OptimizationLogEntry e in entries)
    Console.WriteLine($"[pass {e.Pass}] {e.Message}");
```

## How close the module is to the user

Medium. Contracts are what the extension author and the `VmApplication` user see, but not what a MiniC program author writes.

- The extension author (custom device, disassembler, debugger) implements `IPortUse`/`IPortController`/`IReadOnlyNativeBuffer` — and this is their main entry into the platform.
- A `VmApplication` user who wants to read the processor state uses `ProcessorSnapshot` and `IProcessorFaultPolicy`.
- The optimization compiler author works with `OptimizationId`/`OptimizationLogEntry`/`IReadOnlyLogOptimization`.
- A MiniC/NANASM program author does not import this module — they work through `VmApplication`.

**What you should do:**
- Implement `IPortUse` (not `IPortBus`) for your own devices; `IPortBus` is the bus, not a device.
- Read `ReadPortResult.Status` first, `Data` — only if `Success`.
- Use `OptimizationId.TryParse` for user input and the constructor — for known-valid constants.
- Return `in ProcessorFault` from the kernel and accept it `in` in `ShouldContinue` — without copying.
- `IProcessorFaultPolicy.Default` is a cached singleton (static initializer); for production with special policy requirements — your own implementation.

**What you should not do:**
- Return `new ReadPortResult(data)` without an explicit `Status` — the `Success` default will mask an error.
- Implement `IPortBus` in your code without real device registration — that is not a "device", but a "bus".
- Rely on the `Default` policy in production without understanding the `NullDeviceOutput` vs `NullDeviceInput` asymmetry.
- Cache `IReadOnlyLogOptimization.GetLogs()` — it is a snapshot by contract (see `<remarks>` in the implementation, not in the interface).
- Assume `Default` is "a new instance on each call" — it is a cached singleton.

## Special information

### WASM / cross-platform

| Aspect | Status | Comment |
|---|---|---|
| Building for WASM | ✅ | No `unsafe`, `DllImport`, `LibraryImport`. All types are `interface`/`readonly struct`. Builds for `browser-wasm` without csproj changes. |
| `stackalloc` / `InlineArray` | ✅ | `ProcessorSnapshot` uses `[InlineArray(RegisterCount)] (const int RegisterCount = 32)` — compile-time, AOT-friendly. `stackalloc` in `CurrentProcessorSnapshot` (in `Kernel`) — 32 × 8 bytes. Length validation (`ArgumentException` from `GetArr`) — `managed`, no `unsafe`. | 
| `Interlocked` / `Volatile` | ✅ | Not used. `IProcessorFaultPolicy.Default` is a `static` property with a cached instance (static initializer). |
| `Regex` / trimming | ✅ | Regex is not used; there is no reflection, `Type.GetType`, `Activator.CreateInstance`, `dynamic`. `OptimizationId` validates the string character by character (`foreach (char c in value)`, without `Regex`). Trimming-compatible. |
| AOT | ✅ | No `Expression.Compile`, `MakeGenericMethod`/`MakeGenericType`. `in ProcessorFault` — AOT-friendly. |

**Bottom line:** the contracts are fully compatible with WASM (interpreter and AOT) and trimming.

### `OptimizationId` format

- Allowed characters: `[a-z0-9.-]`. Lowercase ASCII only; `_`, `:`, `/`, spaces are forbidden.
- Length: 1..`MaxLength = 128`.
- `default(OptimizationId)` is the only invalid value; the constructor `new OptimizationId(...)` always yields a valid instance (or throws).
- Comparison — `StringComparison.Ordinal` / `String.CompareOrdinal`; culture-independent.
- Persistence: the XML documentation states that well-known ids are stable and can be written to logs/caches/configs. In the code there are **no static fields with well-known ids** — only the mechanism itself (see [What to fix](#what-to-fix), item 1. *(Note: the "What to fix" section was not present in the provided source.)*).

### `IProcessorFaultPolicy` as a contract

- `Default` is a static property with a **cached instance** of `DefaultProcessorFaultPolicy` (static initializer). Created once on first access to the `IProcessorFaultPolicy` type; repeated reads of `Default` return the same instance.
- `DefaultProcessorFaultPolicy.ShouldContinue` returns `true` for `Success` and `NullDeviceOutput`. `NullDeviceInput` — `false`. If `NullDeviceInput` semantically means "no input device, but this is not a fault", the behavior of `Default` diverges from expectations (see [What to fix](#what-to-fix), item 2. *(Note: the "What to fix" section was not present in the provided source.)*).
- The private nested class is a non-standard solution for contracts, but acceptable: the "default policy" is part of the contract, not a separate implementation in `Kernel`.

### `IPortBus` vs `IPortUse`

- `IPortBus.ReadPort(ulong address)` → `ReadPortResult (Data, Status)` — an address on the bus, the status is returned.
- `IPortUse.ReadPort(ulong offset)` → `byte` — an offset within the device's sector, no status. Errors — either an exception or `0` (not documented).
- `IPortBus.WritePort(ulong, byte)` → `BiosStatus` — the error is returned.
- `IPortUse.WritePort(ulong, byte)` → `void` — errors are not returned; the implementation either throws or swallows.
- The asymmetry is by design: the bus knows about errors, the device does not. The consumer must understand at which level they are working.

### Endianness

Not defined in `Nantuc.Contracts`. `OptimizationId` is character-based, not related to endianness. `ReadPortResult`/`DeviceInfo` — already unpacked values. The byte order on the wire is the concern of `Kernel`.

### Platform limitations

- Target framework: `net10.0`. C# 14 is not required (there are no `extension` blocks in this batch), but for consistency with `Common` it is left as is.
- There is no `unsafe` in this module.
- `Nullable=enable`, `ImplicitUsings=enable`.
- `[DebuggerDisplay]` is present only on `OptimizationId`. On `ReadPortResult`/`OptimizationLogEntry`/`DeviceInfo` — not; `record struct` generates a normal `ToString()` out of the box.

### What this module does not do

- It does not contain implementations — only interfaces and DTOs. The only exception is `private class DefaultProcessorFaultPolicy` inside `IProcessorFaultPolicy` (this is the "default policy", part of the contract).
- It does not contain state, caches, counters.
- It knows nothing about `ManagerDevices`, `MemoryBus`, `Processor` — only about `BiosStatus`/`ProcessorFault`/`RegType`/`OpCode` from `Common`.
- It does not implement `IReadOnlyNativeBuffer` over `NativeMemory` — it only declares the interface; the implementation is in the layer that knows about `NativeMemory`.

Rule for "where to put a type": a primitive without behavior → `Common`; an interface/contract → `Contracts`; anything that executes or stores state → `Kernel`.