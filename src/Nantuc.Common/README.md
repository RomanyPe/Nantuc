# Nantuc.Common — README

> Shared types, ISA constants, and common primitives of the Nantuc platform.  
> The foundation that the kernel, compiler, and extensions rely on.  
> `[← Root](../README.md) · [Contracts](../../src/Nantuc.Contracts/README.md) · [Kernel](../../src/Nantuc.Kernel/README.md)`

## Table of contents
- [About this module](#about-this-module)
- [Local project tree](#local-project-tree)
- [API](#api)
- [API examples](#api-examples)
- [How close the module is to the user](#how-close-the-module-is-to-the-user)
- [Special information](#special-information)
- [What this module does not do](#what-this-module-does-not-do)

## About this module

`Nantuc.Common` is the layer of shared platform primitives. There is no processor logic, no buses, no devices here: only types that are needed simultaneously by the kernel, the MiniC compiler, the NANASM assembler, the disassembler, and extensions. It is the ISA dictionary and the units of measurement for memory and ports — things that must be agreed upon by all participants of the platform.

The module contains no interfaces — only `enum`, `readonly struct`, `record struct`, and `static class` with pure functions. This makes it safe for hot paths of the processor (zero allocations) and convenient for comparison and serialization.

Key roles of the module:
- Bytecode ISA: `OpCode`, `OpCodeSize`, `RegType`, `DecodeInstructionResult`, `InstructionDecoder`.
- Units of measurement: `RamSize`, `PortSize`, `DevicePortSize` (log2 structures with factories).
- Diagnostics: `BiosStatus`, `ProcessorFault` (now in a separate file `ProcessorFault.cs`).
- Return classification: `IdState`.
- Utilities: `AlignmentExtensions` (alignment for generic numbers).

The project is a leaf in terms of dependencies: `net10.0`, no `ProjectReference`. Any change to a public type here is a potential breaking change for the entire platform.

> **XML documentation is almost complete. Two known defects and one breaking change relative to the previous version.**  
> - `DevicePortSize.cs` — **broken closing tags** `</summary` (without `>`) on the `B64`, `B128`, `B256` factories — XML does not parse.  
> - `InstructionDecoder.HasNeed64IntData` — `<remarks>` describes **old behavior** (vulnerability to signed `OpCode`), although the code is already protected by a `(uint)` cast.  
> - `IdState` — **semantics clarified**: `Valid = 0` (the default value), `Unknown = -5`. Consumers that relied on `IdState.Valid == default` remain compatible; code that treated `Unknown` as `0` must be updated.> - `DecodeInstructionResult` — **breaking change**: it was a `readonly struct` with public fields, now it is a `readonly record struct` with positional parameters (fields → get-only properties; `Equals`/`GetHashCode`/`==` are generated).  
> Details — in the [What to fix](#what-to-fix) section. *(Note: the "What to fix" section was not present in the provided source.)*

## Local project tree

All types live in the namespace `Nantuc.Common`, the structure is flat:

```
Nantuc.Common/
├── Nantuc.Common.csproj    # net10.0, no ProjectReference
├── AlignmentExtensions.cs         # static class AlignmentExtensions
├── BiosStatus.cs                  # enum BiosStatus (ProcessorFault moved out)
├── DecodeInstructionResult.cs     # readonly record struct (was readonly struct)
├── DevicePortSize.cs              # readonly struct
├── IdState.cs                     # enum IdState (now with explicit values)
├── InstructionDecoder.cs          # static class InstructionDecoder
├── OpCode.cs                      # enum OpCode : byte
├── OpCodeSize.cs                  # enum OpCodeSize
├── PortSize.cs                    # readonly struct PortSize
├── ProcessorFault.cs              # readonly record struct (separate file!)
├── RamSize.cs                     # readonly struct RamSize
└── RegType.cs                     # enum RegType : byte (with full XML)
```

Organization notes:
- `ProcessorFault` **was moved out of `BiosStatus.cs`** into a separate `ProcessorFault.cs` — navigation improved.
- `AlignmentExtensions.cs` was moved out of `InstructionDecoder.cs` — semantically different classes no longer share a file.
- `NameDeviceToken.cs` and `LogLevel.cs` are not included in this batch. If the types still exist — add them to the next one; if they were removed — update the tree.
- `PortSize` and `RamSize` are now symmetrical: both have `KB2` and `KB32`, the `<remarks>` on `PortSize` has been fixed.

## API

### Overall picture

The module has no interfaces and no default implementations. Public surface:

| Type | Kind | Purpose |
|---|---|---|
| `OpCode` | `enum : byte` | VM opcodes (~60 values), full XML documentation with the semantics of each instruction |
| `OpCodeSize` | `enum` | Operand size: `S8`/`S16`/`S32`/`S64` (full XML, closing tag defect fixed) |
| `RegType` | `enum : byte` | 32 registers: `r0..r27`, `rFL`, `rSP`, `rHP`, `rIP` (full XML) |
| `BiosStatus` | `enum : byte` | Completion/error status (all members with `///`) |
| `IdState` | `enum : int` | Classification of `-1..-4` from `ManagerDevices.AddDevice` (full XML, **explicit values**, `Unknown = -1`) |
| `ProcessorFault` | `readonly record struct` | `(BiosStatus, ulong FaultData, ulong IP, OpCode)` (separate file, full XML) |
| `RamSize` | `readonly struct` | RAM size, log2, 64 B … 2 GiB, full set of factories |
| `PortSize` | `readonly struct` | Port space size, log2, 64 B … 512 KiB, **full set of factories (including `KB2`/`KB32`)** |
| `DevicePortSize` | `readonly struct` | Device port block size, log2, 8 B … 256 B |
| `DecodeInstructionResult` | `readonly record struct` | Decoding result (fields → get-only properties) |
| `InstructionDecoder` | `static class` | Parsing of the 32-bit instruction + "does it need an immediate" table |
| `AlignmentExtensions` | `static class` | `AlignUp`/`AlignDown`/`AlignUpArithmetic` for `IBinaryInteger<T>` |

### Enumerations

**`OpCode : byte`** — about 60 values, each with `///` and detailed `<remarks>`. Groups:

- System: `NOP`, `END` (`END` terminates the program, resets `IsRunning`; `HALT` — pause).
- Memory: `MOV`, `LOAD`/`STORE` (checked via `MemoryBus`), `LDI` (64-bit immediate from the stream), `LOAD_IND`/`STORE_IND` (equivalent to `LOAD`/`STORE` with the address from a register).
- Arithmetic: `ADD`, `SUB`, `MULT_INT` (wrap-around), `SHR`/`SHL` (shift amount — low byte of `R2`), `INC`, `DEC`, `DIV` (`DivOnZero`), `MOD` (`DivOnZero`).
- Logic: `AND`, `OR`, `XOR` (for zeroing `XOR R1, R1`), `NOT`.
- Control flow: `JMP`, `JZ`, `JNZ`, `JG` (Zero and Negative cleared), `JL` (Negative set), `JMP_IND` (indirect jump to the address from a register, with bounds check).
- Stack: `PUSH` (`rSP -= 8`, collision with heap → `SegmentationFault`), `POP` (`rSP += 8`), `CALL` (saves the return address), `RET` (empty stack → `StackUnderflow`), `CALL_IND` (indirect call to the address from a register, with bounds check).
- I/O: `IN`, `OUT` (low byte of the source register).
- Sleep: `HALT` (`IsSleeping = true`), `WAKE` (external command), `WAKE_INT` (via port, no port → `NullDeviceOutput`).
- Unsafe variants: `LOAD_UNSAFE`/`STORE_UNSAFE` (fast path by absolute address without checks), `STORE_IND_UNSAFE`/`LOAD_IND_UNSAFE` (indirect by register, without checks).
- Comparison: `CMP` (`R1 - R2` → `rFL` flags), `TEST` (`R1 & R2` → `rFL` flags), `MOD`.

**`OpCodeSize`** — `S8 = 0b00` (8 bits = 1 byte), `S16 = 0b01` (2 bytes), `S32 = 0b10` (4 bytes), `S64 = 0b11` (8 bytes). **Full XML**, including `<remarks>` on the enum about the bit encoding and that the values must not be changed without the decoder. The closing tag defect (`</summary` without `>`) is fixed.

**`RegType : byte`** — 32 values: `r0..r27` (general-purpose registers, **all with `///`**), `rFL` (Zero/Negative flags), `rSP` (stack pointer), `rHP` (heap pointer), `rIP` (instruction pointer). The `<remarks>` on the enum states: 32 registers of 64 bits in `ArrayRegisters` (`[InlineArray(32)]` of `ulong`), there is no hardware-zeroed register (zeroing via `XOR rN, rN`), the order 0..31 is part of the contract. Exactly 32 = 5 bits.

**`BiosStatus : byte`** — all 11 members with `///`: `Success`, `SegmentationFault`, `AlignmentFault`, `EndProgramm`, `NotImplementedOpCode`, `NullDeviceInput`, `NullDeviceOutput`, `InfinityLoop`, `DivOnZero`, `NotSupportedOpCode`, `StackUnderflow`. The `<remarks>` on the enum explicitly warns: `EndProgramm` and `InfinityLoop` are **not errors by meaning**, but they pass through the `ProcessorFault`/`IProcessorFaultPolicy` path; the policy must distinguish them.

**`IdState`** — **breaking change** relative to the previous version:

| Value | Member | Reason (per `<remarks>`) |
|---|---|---|
| `0` | `Unknown` | Uninitialized/undefined status (default) |
| `1` | `Valid` | The Id is valid and accepted by the system |
| `-1` | `NotAssigned` | The Id was not assigned (registration was not performed) |
| `-2` | `OutOfRange` | The Id is outside the allowed range |
| `-3` | `NoFreeSlot` | No free slot |
| `-4` | `Rejected` | The Id was rejected by the system |

The `<remarks>` on the enum explicitly states: "The default value (zero) is intentionally given to `Unknown` so that an uninitialized status is not perceived as successful". Consumers that checked `IdState.Valid == default` (old behavior `Valid = 0`) **will break** — now `default(IdState) == Unknown`. The numeric values are fixed explicitly and declared part of the contract.

### `ProcessorFault` — `readonly record struct`

```csharp
public readonly record struct ProcessorFault(
    BiosStatus Status,
    ulong FaultData,
    ulong InstructionPointer,
    OpCode OpCode);
```

Moved to a separate file `ProcessorFault.cs`. Full XML: `<summary>` + `<remarks>` + `<param>` for each parameter. Key facts from `<remarks>`:
- Formed by the processor when the status ≠ `Success`, passed to `IProcessorFaultPolicy.ShouldContinue`.
- **Not an exception**, value-type: copied on the stack, does not allocate memory, suitable for passing between threads without locks.
- **Convention for the "uninitialized" value:** `OpCode.NOP` (value 0) is used. `<remarks>` explicitly calls this a controversial decision and recommends replacing it in the future with `OpCode?` or a separate sentinel.

### Sizes: `RamSize`, `PortSize`, `DevicePortSize`

Three log2 structures of the same shape. The constructor is private; instances are only via factories. Validity — the `Log2` range:

| Type | MinLog2 | MaxLog2 | Range |
|---|---|---|---|
| `RamSize` | 6 | 31 | 64 B … 2 GiB |
| `PortSize` | 6 | 19 | 64 B … 512 KiB |
| `DevicePortSize` | 3 | 8 | 8 B … 256 B |

`default(T)` gives `IsValid == false`, but `Bytes` for `default` returns `1` (a consequence of `1UL << 0`). All three have: `Bytes => 1UL << Log2`, operators `==`/`!=`/`<`/`>`/`<=`/`>=`, `IEquatable<T>`, `IComparable<T>`, `ToString()`. Comparison is by the raw `Log2`, so an invalid default value is less than any valid one.

Factories:

| Type | Available factories |
|---|---|
| `RamSize` | `B64..B512`, `KB1, KB2, KB4, KB8, KB16, KB32, KB64, KB128, KB256, KB512`, `MB1..MB512`, `GB1, GB2` |
| `PortSize` | `B64..B512`, `KB1, **KB2**, KB4, KB8, KB16, **KB32**, KB64, KB128, KB256, KB512` — **`KB2`/`KB32` added, `<remarks>` fixed** |
| `DevicePortSize` | `B8, B16, B32, B64, B128, B256` |

Difference in XML coverage:
- `PortSize`, `RamSize` — full (including operators).
- `DevicePortSize` — the struct and factories with `///`, but `Equals`/`GetHashCode`/`CompareTo`/`ToString` and all operators — without. **Plus broken `</summary` on `B64`/`B128`/`B256`** (the defect is not fixed).

### `DecodeInstructionResult` — `readonly record struct`

```csharp
public readonly record struct DecodeInstructionResult(
    OpCode OpCode,
    RegType Reg1,
    RegType Reg2,
    OpCodeSize DataSizeCode);
```

**Breaking change:** it was a `readonly struct` with public fields, now it is a `readonly record struct` with positional parameters. Fields became get-only properties, `Equals`/`GetHashCode`/`==`/`ToString`/`Deconstruct` are generated. XML is full, including `<remarks>` that `Reg1`/`Reg2` are meaningful only for opcodes that use them.

### `InstructionDecoder` — `static class`

| Method | Accepts | Returns | What it does |
|---|---|---|---|
| `GetOpCode(uint)` | raw instruction | `OpCode` | `raw & 0xFF` |
| `GetReg1(uint)` | | `RegType` | `(raw >> 8) & 0x1F` |
| `GetReg2(uint)` | | `RegType` | `(raw >> 13) & 0x1F` |
| `GetDataSizeCode(uint)` | | `OpCodeSize` | `(raw >> 18) & 0x3` |
| `DecodeRawInstToEnums(uint)` | | `DecodeInstructionResult` | all fields in one pass |
| `HasNeed64IntData(OpCode)` | opcode | `bool` | whether there is a 64-bit immediate after the instruction; throws `ArgumentOutOfRangeException` if `(uint)opCode > (uint)OpCode.MOD` |

Plus the extension method `Int()` on `RegType`.

`HasNeed64IntData` — the `CreateNeedsUlongOperandTable()` table. It marks: `LDI`, `LOAD`, `STORE`, `CALL`, `JMP`, `JZ`, `JNZ`, `JG`, `JL`. The range check — `(uint)opCode > (uint)OpCode.MOD` — is robust to changing the underlying `OpCode` type to signed. **However, the `<remarks>` on the method still describes the old vulnerability** — see [What to fix](#what-to-fix), item 1. *(Note: the "What to fix" section was not present in the provided source.)*

`CreateNeedsUlongOperandTable` — `private`, a 256-element `ImmutableArray<bool>`. `LOAD_UNSAFE`/`STORE_UNSAFE` are not marked (by the syntax from `OpCode` — `LOAD_UNSAFE R1, [addr]` and `STORE_UNSAFE [addr], R2` — they take an absolute address, i.e., an immediate; see [What to fix](#what-to-fix), item 4. *(Note: the "What to fix" section was not present in the provided source.)*

All public methods are `[MethodImpl(AggressiveInlining)]`.

### `AlignmentExtensions` — `static class`

Via the C# 14 `extension` block, it adds to `IBinaryInteger<T>`:

| Method | Semantics | `alignment` check |
|---|---|---|
| `AlignUp(T)` | up to a multiple | `DEBUG` only, requires a power of two |
| `AlignDown(T)` | down to a multiple | `DEBUG` only, requires a power of two |
| `AlignUpArithmetic(T)` | up via `%` | always, requires `> 0` (not necessarily a power of two) |

`AlignUp`/`AlignDown` in **Release** do not check `alignment` — an invalid value will yield silent garbage. For a guaranteed check — `AlignUpArithmetic`. XML on `AlignUp`/`AlignDown` exists, on `AlignUpArithmetic` — **none**. Plus a typo in the class `<summary>`: "ресширение" → "расширение" (see [What to fix](#what-to-fix), item 7. *(Note: the "What to fix" section was not present in the provided source.)*

## API examples

**Memory and port sizes — only via factories:**

```csharp
using Nantuc.Common;

RamSize ram = RamSize.MB64;
PortSize ports = PortSize.KB64;
PortSize ports2 = PortSize.KB2;      // symmetrical to RamSize
PortSize ports32 = PortSize.KB32;
DevicePortSize block = DevicePortSize.B256;

Console.WriteLine(ram);              // 67108864 B (2^26)
Console.WriteLine(block);            // 256 B (2^8)
Console.WriteLine(ram.IsValid);      // true

// ⚠ default is "uninitialized", not "0 bytes":
RamSize uninit = default;
Console.WriteLine(uninit.IsValid);   // false
Console.WriteLine(uninit.Bytes);     // 1 — not 0!
```

**Instruction decoding:**

```csharp
uint raw = 0x0000_4A01;

OpCode op = InstructionDecoder.GetOpCode(raw);
RegType r1 = InstructionDecoder.GetReg1(raw);
RegType r2 = InstructionDecoder.GetReg2(raw);
OpCodeSize sz = InstructionDecoder.GetDataSizeCode(raw);

DecodeInstructionResult d = InstructionDecoder.DecodeRawInstToEnums(raw);
if (InstructionDecoder.HasNeed64IntData(d.OpCode))
{
    // after the 4-byte instruction comes an 8-byte immediate
}

// record struct: comparison and deconstruction are available out of the box
if (d == InstructionDecoder.DecodeRawInstToEnums(raw)) { /* ... */ }
var (code, a, b, size) = d;
```

**Classifying the return of `RegisterDevice`:**

```csharp
int raw = host.Emulator.RegisterDevice(spec, sector: 0);

IdState state = raw switch
{
    >= 0 => IdState.Valid,
    -1 => IdState.NotAssigned,
    -2 => IdState.OutOfRange,
    -3 => IdState.NoFreeSlot,
    -4 => IdState.Rejected,
    _  => IdState.Unknown,
};

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

## How close the module is to the user

Low. This is the internal foundation of the platform, not a user API.

- A user who writes programs in MiniC or NANASM should not import `Nantuc.Common` directly — they work through `VmApplication` / `Kernel`.
- An extension author (disassembler, debugger, custom devices, custom backends) will use this module constantly — the opcodes, registers, and units of measurement live here.
- A compiler author (MiniC frontend → NANASM) must align generation with `OpCode` and `HasNeed64IntData`.

**What you should do:**
- Use the `RamSize`/`PortSize`/`DevicePortSize` factories — not "magic" numbers. Now the factory sets of `PortSize` and `RamSize` match.
- For decoding — only `InstructionDecoder`, without manual shifts of your own.
- Check `BiosStatus`/`ProcessorFault` instead of `bool` flags.
- Use `IdState` instead of `raw switch { -1 => ..., -2 => ... }` — a single classification. **Remember:** `default(IdState) == Unknown` (not `Valid`).
- Read the `<remarks>` on `OpCode` and `RegType`: `rFL` flags are updated only by `CMP` and `TEST`, `HALT` ≠ `END`, there is no hardware-zeroed register.

**What you should not do:**
- Add interfaces or default classes to this module — contracts belong in `Contracts`.
- Introduce processor or bus logic here — that is `Kernel`.
- Change the values of `OpCode`/`OpCodeSize`/`RegType`/`IdState` without a bytecode version: the numbers are part of the contract (explicitly fixed in `<remarks>`).
- Rely on `default(RamSize)` as "0" — it is "invalid", and `Bytes` returns `1`.
- Rely on `default(IdState) == Valid` — with the new version it is `Unknown`.

## Special information

### Bytecode ISA (instruction format)

32-bit instruction (`uint`), little-endian at the byte level:

```
 bit:  31…20  |  19…18    |  17…13  |  12…8   |  7…0
       spare   | OpCodeSize|   Reg2  |   Reg1  | OpCode
       (12 bits)| (2 bits) | (5 bits)| (5 bits)| (8 bits)
```

- `OpCode` — 8 bits → up to 256 opcodes, actually about 60 are used.
- `Reg1`, `Reg2` — 5 bits each → 32 registers, exactly as in `RegType`.
- `OpCodeSize` — 2 bits → 4 variants (`S8`/`S16`/`S32`/`S64`).
- Bits 20–31 are unused — a reserve for the future.

Full instruction length:
- 4 bytes — register-register form (most opcodes).
- 12 bytes — register-immediate form: 4 bytes of instruction + 8 bytes of immediate (`ulong`). The presence of an immediate is determined only via `InstructionDecoder.HasNeed64IntData(opCode)`. The immediate is aligned to an 8-byte boundary (fixed in the `<remarks>` of `OpCode`).

### Register file

Fixed in the `<remarks>` of `RegType`:
- 32 registers of 64 bits in `ArrayRegisters` (`[InlineArray(32)]` of `ulong`).
- `r0..r27` — general-purpose registers. There is **no** hardware-zeroed register (unlike MIPS/RISC-V); to zero — `XOR rN, rN`.
- `rFL` — flags: bit 0 — Zero, bit 1 — Negative. **Updated only by `CMP` and `TEST`**; arithmetic and logical instructions do not change the flags. The initial value is `0`.
- `rSP` — stack pointer. Initialized in `Processor.LaunchProgramm` with the top of RAM, aligned to 8 bytes. `rSP -= 8` on `PUSH`/`CALL`, `rSP += 8` on `POP`/`RET`.
- `rHP` — heap pointer. Initialized via `Processor.InitRegHP`; used to detect the "stack ↔ heap" collision.
- `rIP` — instruction pointer. At the start of `Processor.Step`, the 32-bit word at `rIP` is read, after which `rIP += 4`. Immediate operands are read separately with 8-byte alignment. Changed by jump/call/return instructions.

### WASM / cross-platform

| Aspect | Status | Comment |
|---|---|---|
| Building for WASM | - | No `unsafe`, `DllImport`, `LibraryImport`. All types are managed. Builds for `browser-wasm` without csproj changes. |
| `NativeMemory` | - | Not used. `Bytes` — a managed shift `1UL << Log2`. |
| `stackalloc` / `InlineArray` | - | `stackalloc` is not used. `[InlineArray(32)]` is mentioned in the `<remarks>` of `RegType` — it is a compile-time attribute, AOT-friendly, WASM-safe. |
| `Interlocked` / `Volatile` | - | Not used. Shared state — the single `static readonly ImmutableArray<bool> _has` in `InstructionDecoder.cs`, initialized once (the CLR guarantees the uniqueness of the static ctor). |
| `IFileService` | n/a | There is no such interface in this batch of files. |
| `Regex` / trimming | - | Regex is not used; there is no reflection, `Type.GetType`, `Activator.CreateInstance`, `dynamic`. Trimming-compatible. |
| AOT | - | No `Expression.Compile`, `MakeGenericMethod`/`MakeGenericType`. Generic math (`IBinaryInteger<T>`) is monomorphized for each `T`. C# 14 `extension` blocks — compile-time. |

**Bottom line:** the code is fully compatible with WASM (interpreter and AOT) and trimming.

### `HasNeed64IntData` as a contract

The `CreateNeedsUlongOperandTable()` table is the single source of truth about which opcodes have an 8-byte immediate. It currently covers: `LDI`, `LOAD`, `STORE`, `CALL`, `JMP`, `JZ`, `JNZ`, `JG`, `JL`. The range check — `(uint)opCode > (uint)OpCode.MOD` — is also correct for a signed underlying type. **However, the `<remarks>` on the method describes the old behavior** (without the `(uint)` cast) — see [What to fix](#what-to-fix), item 1. *(Note: the "What to fix" section was not present in the provided source.)*

According to the new XML documentation of `OpCode`: `LOAD_IND`/`STORE_IND`/`CALL_IND`/`JMP_IND` are indirect through a register (`R1 = [R2]`, `[R2] = R1`, `CALL [R]`, `JMP [R]`), they **do not need** an immediate after the instruction — their absence from the table is correct. `LOAD_IND_UNSAFE`/`STORE_IND_UNSAFE` are also indirect, their absence is correct. But `LOAD_UNSAFE R1, [addr]` and `STORE_UNSAFE [addr], R2` by syntax take an **absolute address** — this is an immediate, so they **must be** in the table; currently they are not (see [What to fix](#what-to-fix), item 4. *(Note: the "What to fix" section was not present in the provided source.)*)

### Platform limitations

- Target framework: `net10.0`. C# 14 `extension` blocks are used in `AlignmentExtensions` and `InstructionDecoder` — older toolchains will not build.
- There is no `unsafe` code in this module — it is safe for WASM/AOT and environments where `unsafe` is prohibited.
- `[MethodImpl(AggressiveInlining)]` — JIT optimization. In AOT/WASM the inliner behaves differently; profile on the target platform.
- `[Conditional("DEBUG")]` in `AlignmentExtensions`: in **Release** the power-of-two check on `alignment` is not performed. An invalid `alignment` will yield silent garbage. For a guaranteed check — `AlignUpArithmetic`.
- `Nullable=enable`, `ImplicitUsings=enable`.

### What this module does not do

- It does not contain interfaces — they are in `Contracts`.
- It does not contain processor, bus, or device logic — that is `Kernel`.
- It knows nothing about the MiniC compiler and the NANASM assembler — it only provides them with `OpCode`/`RegType`/`OpCodeSize`.
- It does not serialize bytecode — the `.bin`/`.nanas` format is defined in `Kernel`/`Extensions`.
- It does not contain `ToIdState`/`TryGetIdState` — only the `IdState` enum (see [What to fix](#what-to-fix), item 6. *(Note: the "What to fix" section was not present in the provided source.)*)

Rule for "where to put a type": a primitive without behavior → `Common`; an interface/contract → `Contracts`; anything that executes or stores state → `Kernel`.