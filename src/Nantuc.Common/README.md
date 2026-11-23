# Nantuc.Common — README

> Shared types, ISA constants, and shared primitives of the Nantuc platform.
> The foundation that the core, compiler, and extensions rely on.
> [← Root](../README.md), [Contracts](../../src/Nantuc.Contracts/README.md), [Kernel](../../src/Nantuc.Kernel/README.md)

## Table of Contents
- [About This Module](#about-this-module)
- [Local Project Tree](#local-project-tree)
- [API](#api)
- [API Examples](#api-examples)
- [Module Proximity to the User](#module-proximity-to-the-user)
- [Special Information](#special-information)
- [What to Fix](#what-to-fix)

## About This Module

`Nantuc.Common` is the platform's layer of shared primitives. There is no processor logic, no buses, no devices here: only types that are needed simultaneously by the core, the MiniC compiler, the NANASM assembler, the disassembler, and extensions. It is the ISA dictionary and the units of measurement for memory and ports — something that must be agreed upon by all platform participants.

The module contains no interfaces — only `enum`, `readonly struct`, `record struct`, and `static class` with pure functions. This makes it safe for processor hot paths (zero allocations) and convenient for comparison and serialization.

Key roles of the module:
- Bytecode ISA: `OpCode`, `OpCodeSize`, `RegType`, `DecodeInstructionResult`, `InstructionDecoder`.
- Units of measurement: `RamSize`, `PortSize`, `DevicePortSize` (log2-structures with factories).
- Diagnostics: `BiosStatus`, `ProcessorFault`.
- Return classification: `IdState`.
- Utilities: `AlignmentExtensions` (alignment for generic numbers), extension classes for names (`RegTypeExtension`, `OpCodeSizeExtension`, `BiosStatusExtension`, `IdStateExtension`, `OpCodeExtension`).

The project is a leaf in terms of dependencies: `net10.0`, no `ProjectReference`. Any change to a public type here is a potential breaking change for the entire platform.

> **Documentation state.** XML documentation is almost complete. Closing tags in `DevicePortSize.cs` have been fixed, `HasNeed64IntData` now covers `LOAD_UNSAFE`/`STORE_UNSAFE` and checks the range via `OpCodeExtension.LastValue`. **One breaking contract regarding `IdState`:** `Valid = 0` (default value), `Unknown = -5`. Details — in the [What to Fix](#what-to-fix) section.

## Local Project Tree

All types live in the `Nantuc.Common` namespace, the structure is flat:

```
Nantuc.Common/
├── Nantuc.Common.csproj           # net10.0, no ProjectReference
├── AlignmentExtensions.cs         # static class AlignmentExtensions
├── BiosStatus.cs                  # enum BiosStatus (13 members)
├── BiosStatusExtension.cs         # static class BiosStatusExtension (StatusName)
├── DecodeInstructionResult.cs     # readonly record struct
├── DevicePortSize.cs              # readonly struct
├── IdState.cs                     # enum IdState (Valid = 0, Unknown = -5)
├── IdStateExtension.cs            # static class IdStateExtension (ToIdState)
├── InstructionDecoder.cs          # static class InstructionDecoder
├── OpCode.cs                      # enum OpCode : byte
├── OpCodeExtension.cs             # static class OpCodeExtension (LastValue)
├── OpCodeSize.cs                  # enum OpCodeSize
├── OpCodeSizeExtension.cs         # static class OpCodeSizeExtension (ToUint, SizeName)
├── PortSize.cs                    # readonly struct PortSize
├── ProcessorFault.cs              # readonly record struct
├── RamSize.cs                     # readonly struct RamSize
└── RegType.cs                     # enum RegType : byte
└── RegTypeExtension.cs            # static class RegTypeExtension (Int, ToUint, RegName)
```

Organization notes:
- `ProcessorFault` has been moved out of `BiosStatus.cs` into a separate `ProcessorFault.cs`.
- `AlignmentExtensions.cs` has been moved out of `InstructionDecoder.cs`.
- Each enum is accompanied by a companion `*Extension` class with naming/conversion extension members and `LastValue`/`LastValueInt` constants.
- `PortSize` and `RamSize` are symmetric: both have `KB2` and `KB32`.
- `NameDeviceToken.cs` and `LogLevel.cs` are not included in this batch.

## API

### Overview

The module has no interfaces and no default implementations. Public surface:

| Type | Kind | Purpose |
|---|---|---|
| `OpCode` | `enum : byte` | VM opcodes (~60 values), full XML documentation |
| `OpCodeSize` | `enum` | Operand size: `S8`/`S16`/`S32`/`S64` |
| `RegType` | `enum : byte` | 32 registers: `r0..r27`, `rFL`, `rSP`, `rHP`, `rIP` |
| `BiosStatus` | `enum : byte` | Completion/error status (13 members) |
| `IdState` | `enum : int` | Classification of `-1..-5` from `ManagerDevices.AddDevice` |
| `ProcessorFault` | `readonly record struct` | `(BiosStatus, ulong FaultData, ulong IP, OpCode)` |
| `RamSize` | `readonly struct` | RAM size, log2, 64 B … 2 GiB |
| `PortSize` | `readonly struct` | Port space size, log2, 64 B … 512 KiB |
| `DevicePortSize` | `readonly struct` | Device port block size, log2, 8 B … 256 B |
| `DecodeInstructionResult` | `readonly record struct` | Decoding result |
| `InstructionDecoder` | `static class` | 32-bit instruction parsing + immediate table |
| `AlignmentExtensions` | `static class` | `AlignUp`/`AlignDown`/`AlignUpArithmetic` for `IBinaryInteger<T>` |
| `RegTypeExtension` | `static class` | `Int()`, `ToUint()`, `RegName()`, `LastValue` |
| `OpCodeSizeExtension` | `static class` | `ToUint()`, `SizeName()`, `LastValue` |
| `BiosStatusExtension` | `static class` | `StatusName()`, `LastValue` |
| `IdStateExtension` | `static class` | `ToIdState` (extension property on `int`) |
| `OpCodeExtension` | `static class` | `LastValue` (last opcode, for range-check) |

### Enumerations

**`OpCode : byte`** — about 60 values, each with `///` and detailed `<remarks>`. Groups:

- System: `NOP`, `END` (`END` terminates the program, resets `IsRunning`; `HALT` — suspension).
- Memory: `MOV`, `LOAD`/`STORE` (checked via `MemoryBus`), `LDI` (64-bit immediate from the stream), `LOAD_IND`/`STORE_IND` (address from register).
- Arithmetic: `ADD`, `SUB`, `MULT_INT` (wrap-around), `SHR`/`SHL` (shift amount — low byte of `R2`), `INC`, `DEC`, `DIV` (`DivOnZero`), `MOD` (`DivOnZero`).
- Logic: `AND`, `OR`, `XOR` (for zeroing `XOR R1, R1`), `NOT`.
- Control flow: `JMP`, `JZ`, `JNZ`, `JG` (Zero and Negative cleared), `JL` (Negative set), `JMP_IND` (indirect jump by address from register, with bounds checking).
- Stack: `PUSH` (`rSP -= 8`, collision with heap -> `SegmentationFault`), `POP` (`rSP += 8`), `CALL` (saves return address), `RET` (empty stack -> `StackUnderflow`), `CALL_IND` (indirect call by address from register).
- I/O: `IN`, `OUT` (low byte of source register).
- Sleep: `HALT` (`IsSleeping = true`), `WAKE` (external command), `WAKE_INT` (via port, no port -> `NullDeviceOutput`).
- Unsafe variants: `LOAD_UNSAFE`/`STORE_UNSAFE` (fast path by absolute address without checks), `STORE_IND_UNSAFE`/`LOAD_IND_UNSAFE` (indirect by register, without checks).
- Comparison: `CMP` (`R1 - R2` -> `rFL` flags), `TEST` (`R1 & R2` -> `rFL` flags), `MOD`.
- Casting: `CASTZ`, `CASTS` casts `ulong` from register to `S8` (1 byte), `S16` (2 bytes), `S32` (4 bytes).

**`OpCodeSize`** — `S8 = 0b00` (1 byte), `S16 = 0b01` (2 bytes), `S32 = 0b10` (4 bytes), `S64 = 0b11` (8 bytes). Full XML, including `<remarks>` on bit encoding. `OpCodeSizeExtension.LastValue == OpCodeSize.S64`.

**`RegType : byte`** — 32 values: `r0..r27` (general-purpose registers, all with `///`), `rFL` (Zero/Negative flags), `rSP` (stack pointer), `rHP` (heap pointer), `rIP` (instruction pointer). `<remarks>` on the enum states: 32 registers of 64 bits in `ArrayRegisters` (`[InlineArray(32)]` of `ulong`), no hardware-zeroed register (zeroing via `XOR rN, rN`), order 0..31 is part of the contract. Exactly 32 = 5 bits.

**`BiosStatus : byte`** — **13 members** (this version adds `HostStopped` and `UnsafeOpCodeForbidden`):

| Value | Member | Purpose |
|---|---|---|
| 0 | `Success` | Success, default value |
| 1 | `SegmentationFault` | Out-of-bounds RAM access |
| 2 | `AlignmentFault` | Unaligned access |
| 3 | `EndProgram` | Normal termination (`END`) |
| 4 | `HostStopped` | **New:** emergency termination from host |
| 5 | `NotImplementedOpCode` | No handler (default in `Step`) |
| 6 | `NullDeviceInput` | Input device did not respond |
| 7 | `NullDeviceOutput` | Output device did not respond |
| 8 | `InfinityLoop` | Policy detected a loop |
| 9 | `DivOnZero` | Division by zero (`DIV`/`MOD`) |
| 10 | `NotSupportedOpCode` | Opcode is known but not supported |
| 11 | `StackUnderflow` | Stack overflow/invalid state |
| 12 | `UnsafeOpCodeForbidden` | **New:** unsafe opcode forbidden by policy |

`<remarks>` on the enum warns: `EndProgram`, `HostStopped`, and `InfinityLoop` are **not errors by meaning**, but pass through the `ProcessorFault`/`IProcessorPolicy` path; the policy must distinguish them.

**`IdState`** — **important:** `Valid = 0` (default value), `Unknown = -5`:

| Value | Member | Reason |
|---|---|---|
| `0` | `Valid` | Id is valid and accepted by the system (default) |
| `-1` | `NotAssigned` | Id not assigned (registration not performed) |
| `-2` | `OutOfRange` | Id outside the allowed range |
| `-3` | `NoFreeSlot` | No free slot |
| `-4` | `Rejected` | Id rejected by the system |
| `-5` | `Unknown` | Status undefined (and all values `< -5`) |

`<remarks>` on the enum explicitly states: the default value (`0`) corresponds to `Valid`, so an uninitialized field of type `IdState` is interpreted as a successful status. If you need to distinguish "not initialized" from "valid", use `IdState?`. Numeric values are explicitly fixed and declared part of the contract.

**`IdStateExtension.ToIdState`** — extension property on `int`, totally converts a numeric Id:

| Id | `IdState` |
|---|---|
| `>= 0` | `Valid` |
| `-1` | `NotAssigned` |
| `-2` | `OutOfRange` |
| `-3` | `NoFreeSlot` |
| `-4` | `Rejected` |
| any other (incl. `-5`) | `Unknown` |

Does not throw exceptions; applicable to arbitrary external data.

### `ProcessorFault` — `readonly record struct`

```csharp
public readonly record struct ProcessorFault(
    BiosStatus Status,
    ulong FaultData,
    ulong InstructionPointer,
    OpCode OpCode);
```

Full XML: `<summary>` + `<remarks>` + `<param>` for each parameter. Key facts from `<remarks>`:
- Formed by the processor when status ≠ `Success`, passed to `IProcessorPolicy.ShouldContinue`.
- **Not an exception**, value-type: copied on the stack, does not allocate memory, suitable for passing between threads without locks.
- **Agreement on "uninitialized" value:** `OpCode.NOP` (value 0) is used. `<remarks>` explicitly calls this a controversial decision and recommends replacing it in the future with `OpCode?` or a separate sentinel.

### Sizes: `RamSize`, `PortSize`, `DevicePortSize`

Three log2-structures identical in form. Constructor is private, instances — only via factories. Validity — `Log2` range:

| Type | MinLog2 | MaxLog2 | Range |
|---|---|---|---|
| `RamSize` | 6 | 31 | 64 B … 2 GiB |
| `PortSize` | 6 | 19 | 64 B … 512 KiB |
| `DevicePortSize` | 3 | 8 | 8 B … 256 B |

`default(T)` yields `IsValid == false`, but `Bytes` for `default` returns `1` (consequence of `1UL << 0`). All three have: `Bytes => 1UL << Log2`, operators `==`/`!=`/`<`/`>`/`<=`/`>=`, `IEquatable<T>`, `IComparable<T>`, `ToString()`. Comparison is by raw `Log2`, so the invalid default value is less than any valid one.

Factories:

| Type | Available factories |
|---|---|
| `RamSize` | `B64..B512`, `KB1..KB512`, `MB1..MB512`, `GB1, GB2` |
| `PortSize` | `B64..B512`, `KB1, KB2, KB4, KB8, KB16, KB32, KB64, KB128, KB256, KB512` |
| `DevicePortSize` | `B8, B16, B32, B64, B128, B256` |

XML coverage: `PortSize` and `RamSize` — complete (including operators). `DevicePortSize` — all members and operators with `///`; closing tags `</summary>` fixed.

### `DecodeInstructionResult` — `readonly record struct`

```csharp
public readonly record struct DecodeInstructionResult(
    OpCode OpCode,
    RegType Reg1,
    RegType Reg2,
    OpCodeSize DataSizeCode);
```

Positional parameters -> get-only properties, generated `Equals`/`GetHashCode`/`==`/`ToString`/`Deconstruct`. XML — complete, including `<remarks>` stating that `Reg1`/`Reg2` only make sense for opcodes that use them.

### `InstructionDecoder` — `static class`

| Method | Accepts | Returns | What it does |
|---|---|---|---|
| `GetOpCode(uint)` | raw instruction | `OpCode` | `raw & 0xFF` |
| `GetReg1(uint)` | | `RegType` | `(raw >> 8) & 0x1F` |
| `GetReg2(uint)` | | `RegType` | `(raw >> 13) & 0x1F` |
| `GetDataSizeCode(uint)` | | `OpCodeSize` | `(raw >> 18) & 0x3` |
| `DecodeRawInstToEnums(uint)` | | `DecodeInstructionResult` | all fields in one pass |
| `HasNeed64IntData(OpCode)` | opcode | `bool` | whether a 64-bit immediate follows the instruction; throws `ArgumentOutOfRangeException` if `(uint)opCode > (uint)OpCodeExtension.LastValue` |

`HasNeed64IntData` — table `CreateNeedsUlongOperandTable()` (256-element `ImmutableArray<bool>`). **In this version marked:** `LDI`, `LOAD`, `STORE`, `CALL`, `JMP`, `JZ`, `JNZ`, `JG`, `JL`, **`LOAD_UNSAFE`, `STORE_UNSAFE`** (previously missing). Range check — via `OpCodeExtension.LastValue`, not `OpCode.MOD`, so it is correct for recently added opcodes too.

All public methods are `[MethodImpl(AggressiveInlining)]`.

### Extension Classes

Via C# 14 `extension` blocks, convenient members are added:

| Class | Extension on | Members |
|---|---|---|
| `RegTypeExtension` | `RegType` | `Int()`, `ToUint()`, `RegName()`; constants `LastValueInt`, `LastValue` (`rIP`) |
| `OpCodeSizeExtension` | `OpCodeSize` | `ToUint()`, `SizeName()`; constants `LastValueInt`, `LastValue` (`S64`) |
| `BiosStatusExtension` | `BiosStatus` | `StatusName()`; constants `LastValueInt`, `LastValue` (`UnsafeOpCodeForbidden`) |
| `IdStateExtension` | `int` | `ToIdState` (extension property) |
| `OpCodeExtension` | `OpCode` | `LastValue` (last opcode) |

`RegName()`/`SizeName()`/`StatusName()` return `nameof(...)` for known values or a fallback (`"r?"`, `"???"`, `((uint)status).ToString()`).

### `AlignmentExtensions` — `static class`

Via C# 14 `extension` block adds to `IBinaryInteger<T>`:

| Method | Semantics | `alignment` check |
|---|---|---|
| `AlignUp(T)` | up to a multiple | `DEBUG` only, requires power of two |
| `AlignDown(T)` | down to a multiple | `DEBUG` only, requires power of two |
| `AlignUpArithmetic(T)` | up via `%` | always, requires `> 0` |

`AlignUp`/`AlignDown` in **Release** do not check `alignment` — an invalid value will yield silent garbage. For guaranteed checking — `AlignUpArithmetic`. The helper `ThrowIfInvalidAlignment` is marked `[Conditional("DEBUG")]` and `[MethodImpl(NoInlining)]`.

## API Examples

**Memory and port sizes — only via factories:**

```csharp
using Nantuc.Common;

RamSize ram = RamSize.MB64;
PortSize ports = PortSize.KB64;
PortSize ports2 = PortSize.KB2;
PortSize ports32 = PortSize.KB32;
DevicePortSize block = DevicePortSize.B256;

Console.WriteLine(ram);              // 67108864 B (2^26)
Console.WriteLine(block);            // 256 B (2^8)
Console.WriteLine(ram.IsValid);      // true

// ⚠ default is "not initialized", not "0 bytes":
RamSize uninit = default;
Console.WriteLine(uninit.IsValid);   // false
Console.WriteLine(uninit.Bytes);     // 1 — not 0!
```

**Decoding an instruction:**

```csharp
uint raw = 0x0000_4A01;

OpCode op = InstructionDecoder.GetOpCode(raw);
RegType r1 = InstructionDecoder.GetReg1(raw);
RegType r2 = InstructionDecoder.GetReg2(raw);
OpCodeSize sz = InstructionDecoder.GetDataSizeCode(raw);

DecodeInstructionResult d = InstructionDecoder.DecodeRawInstToEnums(raw);
if (InstructionDecoder.HasNeed64IntData(d.OpCode))
{
    // an 8-byte immediate follows the 4-byte instruction
}

// record struct: comparison and deconstruction available out of the box
if (d == InstructionDecoder.DecodeRawInstToEnums(raw)) { /* ... */ }
var (code, a, b, size) = d;
```

**Extension members:**

```csharp
RegType r = RegType.rSP;
Console.WriteLine(r.Int());       // 29
Console.WriteLine(r.RegName());   // "rSP"

BiosStatus s = BiosStatus.DivOnZero;
Console.WriteLine(s.StatusName()); // "DivOnZero"

OpCodeSize sz = OpCodeSize.S32;
Console.WriteLine(sz.SizeName()); // "S32"
Console.WriteLine(sz.ToUint());   // 2
```

**Classifying the return of `RegisterDevice`:**

```csharp
int raw = host.Emulator.RegisterDevice(spec, sector: 0);
IdState state = raw.ToIdState;

if (state != IdState.Valid)
{
    Console.Error.WriteLine($"RegisterDevice failed: {state} ({raw})");
    return;
}
// raw is a valid ID
```

**Diagnostics:**

```csharp
ProcessorFault fault = new(
    Status: BiosStatus.DivOnZero,
    FaultData: 0,
    InstructionPointer: 0x40,
    OpCode: OpCode.DIV);

if (fault.Status != BiosStatus.Success)
    Console.Error.WriteLine($"{fault.Status} @ IP=0x{fault.InstructionPointer:X}");
```

**Alignment:**

```csharp
int addr = 13;
int aligned = addr.AlignUp(8);    // 16
int down    = addr.AlignDown(8);  // 8
```

## Module Proximity to the User

Low. This is the platform's internal foundation, not a user-facing API.

- A user who writes programs in MiniC or NANASM should not import `Nantuc.Common` directly — they work through `VmApplication` / `Kernel`.
- An extension author (disassembler, debugger, custom devices, custom backends) will use this module constantly — opcodes, registers, and units of measurement live here.
- A compiler author (MiniC frontend -> NANASM) must coordinate generation with `OpCode` and `HasNeed64IntData`.

**What to do:**
- Use the factories `RamSize`/`PortSize`/`DevicePortSize` — not "magic" numbers.
- For decoding — only `InstructionDecoder`, without manual shifts in your own code.
- Check `BiosStatus`/`ProcessorFault` instead of `bool` flags.
- Use `IdState`/`ToIdState` instead of `raw switch { -1 => ..., -2 => ... }`. **Remember:** `default(IdState) == Valid` (not `Unknown`).
- Read `<remarks>` on `OpCode` and `RegType`: `rFL` flags are updated only by `CMP` and `TEST`, `HALT` ≠ `END`, there is no hardware-zeroed register.

**What not to do:**
- Add interfaces or default classes to this module — `Contracts` is for contracts.
- Introduce processor or bus logic here — that is `Kernel`.
- Change the values of `OpCode`/`OpCodeSize`/`RegType`/`IdState`/`BiosStatus` without a bytecode version: the numbers are part of the contract.
- Rely on `default(RamSize)` as "0" — it is "invalid", and `Bytes` returns `1`.
- Rely on `default(IdState) == Unknown` — in the current version `default` = `Valid`.

## Special Information

### Bytecode ISA (instruction format)

32-bit instruction (`uint`), little-endian at the byte level:

```
 bit:  31…20  |  19…18    |  17…13  |  12…8   |  7…0
       spare   | OpCodeSize|   Reg2  |   Reg1  | OpCode
       (12 bits)| (2 bits)  | (5 bits)| (5 bits)| (8 bits)
```

- `OpCode` — 8 bits -> up to 256 opcodes, ~60 actually used.
- `Reg1`, `Reg2` — 5 bits each -> 32 registers, exactly as in `RegType`.
- `OpCodeSize` — 2 bits -> 4 variants (`S8`/`S16`/`S32`/`S64`).
- Bits 20–31 are unused — reserved for the future.

Full instruction length:
- 4 bytes — register-register form.
- 12 bytes — register-immediate form: 4 bytes instruction + 8 bytes immediate (`ulong`). Presence of immediate — only via `InstructionDecoder.HasNeed64IntData(opCode)`. Immediate is aligned to an 8-byte boundary.

### Register File

Fixed in `<remarks>` of `RegType`:
- 32 registers of 64 bits in `ArrayRegisters` (`[InlineArray(32)]` of `ulong`).
- `r0..r27` — general-purpose registers. **No** hardware-zeroed register; for zeroing — `XOR rN, rN`.
- `rFL` — flags: bit 0 — Zero, bit 1 — Negative. **Updated only by `CMP` and `TEST`**; arithmetic and logic instructions do not change flags. Initial value — `0`.
- `rSP` — stack pointer. Initialized in `Processor.LaunchProgramm` to the top of RAM, aligned to 8 bytes. `rSP -= 8` on `PUSH`/`CALL`, `rSP += 8` on `POP`/`RET`.
- `rHP` — heap pointer. Initialized via `Processor.InitRegHP`; used to detect the "stack ↔ heap" collision.
- `rIP` — instruction pointer. At the start of `Processor.Step`, a 32-bit word is read at `rIP`, then `rIP += 4`. Immediate operands are read separately with 8-byte alignment.

### WASM / Cross-platform

| Aspect | Status | Comment |
|---|---|---|
| Building to WASM | - | No `unsafe`, `DllImport`, `LibraryImport`. All types — managed. Builds for `browser-wasm` without csproj changes. |
| `NativeMemory` | - | Not used. `Bytes` — managed shift `1UL << Log2`. |
| `stackalloc` / `InlineArray` | - | `stackalloc` not used. `[InlineArray(32)]` — compile-time attribute, AOT-friendly, WASM-safe. |
| `Interlocked` / `Volatile` | - | Not used. Shared state — the only `static readonly ImmutableArray<bool> _has` in `InstructionDecoder.cs`, initialized once. |
| `IFileService` | n/a | No such interface in this batch of files. |
| `Regex` / trimming | - | Regex not used; no reflection, `Type.GetType`, `Activator.CreateInstance`, `dynamic`. Trimming-compatible. |
| AOT | - | No `Expression.Compile`, `MakeGenericMethod`/`MakeGenericType`. Generic math (`IBinaryInteger<T>`) is monomorphized per `T`. C# 14 `extension` blocks — compile-time. |

**Summary:** the code is fully compatible with WASM (interpreter and AOT) and trimming.

### `HasNeed64IntData` as a Contract

The table `CreateNeedsUlongOperandTable()` is the single source of truth about which opcodes have an 8-byte immediate. **Covers:** `LDI`, `LOAD`, `STORE`, `CALL`, `JMP`, `JZ`, `JNZ`, `JG`, `JL`, **`LOAD_UNSAFE`, `STORE_UNSAFE`**. Range check — `(uint)opCode > (uint)OpCodeExtension.LastValue` — is correct and extends automatically when new opcodes are added up to `LastValue`.

`LOAD_IND`/`STORE_IND`/`CALL_IND`/`JMP_IND` — indirect via register, no immediate needed — correctly absent from the table. `LOAD_IND_UNSAFE`/`STORE_IND_UNSAFE` — also indirect.

### Platform Limitations

- Target framework: `net10.0`. C# 14 `extension` blocks are used — older toolchains will not build.
- No `unsafe` code in this module — safe for WASM/AOT.
- `[MethodImpl(AggressiveInlining)]` — JIT optimization. In AOT/WASM the inliner behaves differently; profile on the target platform.
- `[Conditional("DEBUG")]` in `AlignmentExtensions`: in **Release** the `alignment` power-of-two check is not performed.
- `Nullable=enable`, `ImplicitUsings=enable`.

### What This Module Does Not Do

- Does not contain interfaces — they are in `Contracts`.
- Does not contain processor, bus, or device logic — that is `Kernel`.
- Does not know about the MiniC compiler or NANASM assembler — it only provides them with `OpCode`/`RegType`/`OpCodeSize`.
- Does not serialize bytecode — the `.bin`/`.nanas` format is defined in `Kernel`/`Extensions`.

Rule for "where to put a type": primitive without behavior -> `Common`; interface/contract -> `Contracts`; anything that executes or stores state -> `Kernel`.