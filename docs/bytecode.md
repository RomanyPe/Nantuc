Here is the English translation. No `.ru` localization prefixes were present in the source.

# Nantuc Virtual Machine Bytecode Specification

> Format version: 1  
> Target platform: `Nantuc.Kernel.Common` / `Nantuc.Compiler.ASM`  
> Status: implemented in the reference interpreter (`Processor.Step`, `InstructionDecoder`) and in the MiniC/NANASM code generator.

---

## 1. Terminology and conventions

- **Bytecode** — a sequence of 32-bit instruction words and 64-bit immediate operands, placed in RAM.
- **Instruction word** — a 32-bit `uint`, read from memory as **little-endian** (`MemoryBus.ReadInt32LE*`).
- **Immediate** — a 64-bit value, read from memory as **little-endian unaligned** (`ReadInt64LEUnSafe`).
- **VM** — the virtual processor implemented in `Processor`.
- **Loader** — `Device.LoadProgram` (copies program bytes into RAM and initializes `rHP`).

Key requirement: **the base load address of the program must be a multiple of 8** for correct immediate reads.

---

## 2. Machine model

### 2.1. Registers

32 registers of 64 bits each (`[InlineArray(32)]` in `Processor`). Indexing is by `RegType` (0..31).

| Index | Name   | Purpose |
|------:|--------|---------|
| 0–27  | `r0..r27` | General-purpose registers |
| 28    | `rFL` | Flags: bit 0 = Zero, bit 1 = Negative |
| 29    | `rSP` | Stack Pointer — stack pointer (grows downward) |
| 30    | `rHP` | Heap Pointer — heap pointer |
| 31    | `rIP` | Instruction Pointer — address of the next instruction |

**`rFL` flags are updated only by `CMP` and `TEST`.** Arithmetic and logical instructions do not change flags.

### 2.2. Processor state

- `IsRunning` — whether the program is executing.
- `IsSleeping` — the processor is in `HALT` mode (waiting for wakeup).

### 2.3. Initial initialization (`LaunchProgramm`)

| Field | Value |
|-------|-------|
| `rIP` | argument `startAddress` (address of the first instruction) |
| `rSP` | `AlignDown(RAM.Length, 8)` — top of RAM |
| `rFL` | `0` |
| `IsRunning` | `true`, `IsSleeping = false` |
| `rHP` | set by `Device.LoadProgram` / `InitRegHP` to `AlignUp(loadAddress + programLength, 8)` |

### 2.4. Address format

- RAM is byte-addressed; length is `RAM.Length` (power of two, ≥ 64 bytes).
- Ports are a separate byte space divided into sectors of `PortsOnDevice` ports each.

---

## 3. Instruction format

### 3.1. 32-bit word (little-endian)

```
bits:  31 … 20  |  19 … 18   |  17 … 13   |  12 … 8   |  7 … 0
       spare    | DataSize   |   Reg2     |   Reg1    | OpCode
       (12 bits)| (2 bits)   |  (5 bits)  |  (5 bits) | (8 bits)
```

- **OpCode** (8 bits) — operation code (`OpCode`).
- **Reg1** (5 bits) — first register operand, usually the destination.
- **Reg2** (5 bits) — second register operand, usually the source.
- **DataSize** (2 bits) — only for the LOAD/STORE group (`OpCodeSize`).
- **Bits 20–31** — reserved, must be zero.

Decoder (reference): `InstructionDecoder.GetOpCode/GetReg1/GetReg2/GetDataSizeCode`.

### 3.2. Total instruction length

| Form | Length | When |
|------|--------|------|
| Register–register / no operands | **4 bytes** | all except those listed below |
| Instruction + immediate | **12 bytes** (4 + 4 padding + 8) | if `InstructionDecoder.HasNeed64IntData(opCode) == true` |

**Immediate** is read at address `AlignUp(rIP_after_instr, 8)`. Padding between the instruction and the immediate is ignored (may be arbitrary, usually 0). After reading, `rIP = immediate_addr + 8`.

### 3.3. Register operands by opcode

| Operand | What it encodes |
|---------|-----------------|
| `Reg1`  | destination (dst) / left operand / only operand |
| `Reg2`  | source (src) / right operand / port (for IN/OUT) |

See the opcode table for specific conventions.

### 3.4. DataSize (`OpCodeSize`)

| Code (`b2b1`) | Value | Size | Address alignment requirement |
|:-------------:|-------|-----:|-------------------------------|
| `00` | `S8`  | 1 byte  | — |
| `01` | `S16` | 2 bytes | 2 (`addr & 1 == 0`) |
| `10` | `S32` | 4 bytes | 4 (`addr & 3 == 0`) |
| `11` | `S64` | 8 bytes | 8 (`addr & 7 == 0`) |

Used only by `LOAD`, `STORE`, `LOAD_IND`, `STORE_IND`, and their `_UNSAFE` variants.

---

## 4. Opcodes (registry)

All values are 0 through 42; the original enum is `OpCode : byte`. Values are fixed by declaration order and cannot be reordered without updating the decoder and saved images.

| # | Mnemonic | Reg1 | Reg2 | Size | Immediate | Brief meaning |
|--:|----------|------|------|:----:|:---------:|---------------|
| 0  | `NOP`            | — | — | — | — | no operation |
| 1  | `END`            | — | — | — | — | normal program termination |
| 2  | `MOV`            | dst | src | — | — | `dst = src` |
| 3  | `LOAD`           | dst | — | ✓ | ✓ | `dst = [addr]` (checked) |
| 4  | `STORE`          | src | — | ✓ | ✓ | `[addr] = src` (checked) |
| 5  | `LDI`            | dst | — | — | ✓ | `dst = imm` |
| 6  | `LOAD_IND`       | dst | addr | ✓ | — | `dst = [Reg2]` (checked) |
| 7  | `STORE_IND`      | src | addr | ✓ | — | `[Reg2] = Reg1` (checked) |
| 8  | `ADD`            | dst | src | — | — | `dst += src` |
| 9  | `SUB`            | dst | src | — | — | `dst -= src` |
| 10 | `MULT_INT`       | dst | src | — | — | `dst *= src` (wrap-around, unsigned) |
| 11 | `SHR`            | dst | shift | — | — | `dst >>= (byte)shift` |
| 12 | `SHL`            | dst | shift | — | — | `dst <<= (byte)shift` |
| 13 | `INC`            | reg | — | — | — | `reg += 1` |
| 14 | `DEC`            | reg | — | — | — | `reg -= 1` |
| 15 | `DIV`            | dst | src | — | — | `dst /= src` (unsigned), `DivOnZero` if 0 |
| 16 | `AND`            | dst | src | — | — | `dst &= src` |
| 17 | `OR`             | dst | src | — | — | `dst \|= src` |
| 18 | `XOR`            | dst | src | — | — | `dst ^= src` |
| 19 | `NOT`            | reg | — | — | — | `reg = ~reg` |
| 20 | `JMP`            | — | — | — | ✓ | `rIP = target` |
| 21 | `JZ`             | — | — | — | ✓ | if Z: `rIP = target` |
| 22 | `JNZ`            | — | — | — | ✓ | if !Z: `rIP = target` |
| 23 | `JG`             | — | — | — | ✓ | if !N && !Z: `rIP = target` |
| 24 | `JL`             | — | — | — | ✓ | if N: `rIP = target` |
| 25 | `PUSH`           | src | — | — | — | push 8 bytes |
| 26 | `POP`            | dst | — | — | — | pop 8 bytes |
| 27 | `CALL`           | — | — | — | ✓ | push rIP; `rIP = target` |
| 28 | `RET`            | — | — | — | — | `rIP = pop` |
| 29 | `IN`             | dst | port | — | — | `dst = port[port]` |
| 30 | `OUT`            | src | port | — | — | `port[port] = (byte)src` |
| 31 | `CALL_IND`       | addr | — | — | — | `CALL [Reg1]` (checked) |
| 32 | `JMP_IND`        | addr | — | — | — | `JMP [Reg1]` (checked) |
| 33 | `HALT`           | — | — | — | — | `IsSleeping = true` |
| 34 | `WAKE`           | — | — | — | — | no-op in stream; as an external command — wakeup |
| 35 | `WAKE_INT`       | port | — | — | — | wake device at `port[Reg1]` |
| 36 | `LOAD_UNSAFE`    | dst | — | ✓ | ✓ | `dst = [addr]` (unchecked) |
| 37 | `STORE_UNSAFE`   | src | — | ✓ | ✓ | `[addr] = src` (unchecked) |
| 38 | `STORE_IND_UNSAFE` | src | addr | ✓ | — | `[Reg2] = Reg1` (unchecked) |
| 39 | `LOAD_IND_UNSAFE`  | dst | addr | ✓ | — | `dst = [Reg2]` (unchecked) |
| 40 | `CMP`            | left | right | — | — | updates `rFL` based on `left - right` |
| 41 | `TEST`           | left | right | — | — | updates `rFL` based on `left & right` |
| 42 | `MOD`            | dst | src | — | — | `dst %= src` (unsigned), `DivOnZero` if 0 |

The immediate operand is present **only** for opcodes marked `HasNeed64IntData = true` (in the reference: `LDI`, `LOAD`, `STORE`, `CALL`, `JMP`, `JZ`, `JNZ`, `JG`, `JL`, `LOAD_UNSAFE`, `STORE_UNSAFE`, `LOAD_IND`, `STORE_IND`, `LOAD_IND_UNSAFE`, `STORE_IND_UNSAFE`).

---

## 5. Instruction semantics

### 5.1. System

| Opcode | Action |
|--------|--------|
| `NOP` | no operation, `rIP` has already advanced by 4 |
| `END` | `IsRunning = false`; returns `BiosStatus.EndProgram`; then `IProcessorPolicy.ShouldContinue`, whose response overwrites `IsRunning` |

### 5.2. Moves and load/store

**`MOV Reg1, Reg2`** → `Reg1 = Reg2` (full 64 bits).

**`LDI Reg1, imm`** → `Reg1 = imm` (64-bit immediate from the stream).

**`LOAD[.size] Reg1, [imm]`** — checked read:
1. `addr = imm`
2. alignment check by `size` (`addr & (N-1) == 0`)
3. check `addr + N <= RAM.Length`
4. read `N` bytes little-endian, zero-extended to 64 bits
5. `Reg1 = value`

Errors: `AlignmentFault` (with `FaultData = addr`), `SegmentationFault` (with `FaultData = addr`).

**`STORE[.size] [imm], Reg1`** — checked write:
1. `addr = imm`
2. the same checks
3. write the least significant `N` bytes of `Reg1` little-endian to `[addr]`

**`LOAD_IND[.size] Reg1, Reg2`** — same as `LOAD`, but `addr = Reg2`.

**`STORE_IND[.size] Reg1, Reg2`** — same as `STORE`, but `addr = Reg2`.

**`*_UNSAFE` variants** — without checks. Behavior on out-of-bounds or misalignment is **undefined** (`Unsafe.ReadUnaligned`/`WriteUnaligned` on a pointer outside the buffer).

### 5.3. Arithmetic (unsigned, 64-bit, wrap-around)

All instructions modify only `Reg1`.

| Opcode | Formula |
|--------|---------|
| `ADD Reg1, Reg2` | `Reg1 = Reg1 + Reg2` |
| `SUB Reg1, Reg2` | `Reg1 = Reg1 - Reg2` |
| `MULT_INT Reg1, Reg2` | `Reg1 = Reg1 * Reg2` (overflow discarded) |
| `DIV Reg1, Reg2` | `Reg1 = Reg1 / Reg2` (unsigned); `DivOnZero` if `Reg2 == 0` |
| `MOD Reg1, Reg2` | `Reg1 = Reg1 % Reg2` (unsigned); `DivOnZero` if `Reg2 == 0` |
| `INC Reg1` | `Reg1 += 1` |
| `DEC Reg1` | `Reg1 -= 1` |
| `SHR Reg1, Reg2` | `Reg1 >>= (byte)Reg2` (least significant byte) |
| `SHL Reg1, Reg2` | `Reg1 <<= (byte)Reg2` (least significant byte) |

### 5.4. Logical

| Opcode | Formula |
|--------|---------|
| `AND Reg1, Reg2` | `Reg1 &= Reg2` |
| `OR Reg1, Reg2`  | `Reg1 \|= Reg2` |
| `XOR Reg1, Reg2` | `Reg1 ^= Reg2` (typically for zeroing: `XOR Rn, Rn`) |
| `NOT Reg1`       | `Reg1 = ~Reg1` |

### 5.5. Comparisons (update `rFL`)

`rFL` (28): bit 0 — **Zero (Z)**, bit 1 — **Negative (N)**. Other bits are 0.

**`CMP Reg1, Reg2`** — computes `res = Reg1 - Reg2` (64-bit, unsigned):
- `Z = (res == 0)`
- `N = (res >> 63) & 1`

`Reg1`/`Reg2` are not modified.

**`TEST Reg1, Reg2`** — computes `res = Reg1 & Reg2`:
- `Z = (res == 0)`
- `N = (res >> 63) & 1`

`Reg1`/`Reg2` are not modified.

### 5.6. Control flow

> check `target < RAM.Length`; on failure — `SegmentationFault`; otherwise `rIP = target`

**`JMP target`** → `rIP = target`.

**`JZ target`** — if `Z=1`, `rIP = target`.

**`JNZ target`** — if `Z=0`, `rIP = target`.

**`JG target`** — if `Z=0` and `N=0`, `rIP = target` (unsigned “greater”).

**`JL target`** — if `N=1`, `rIP = target` (unsigned “less”).

**`JMP_IND Reg1`** — `target = Reg1`.

**`CALL target`** — subroutine call:
1. `if rSP < 8 → SegmentationFault`
2. `sp = rSP - 8`
3. `if rHP >= sp → SegmentationFault` (stack/heap collision)
4. `RAM[sp] = rIP` (unchecked write, little-endian)
5. `rSP = sp; rIP = target`

**`CALL_IND Reg1`** — `target = Reg1`; check `target < RAM.Length`; on failure — `SegmentationFault`; otherwise equivalent to `CALL target`.

**`RET`** — return:
1. `if rSP >= stackBase → StackUnderflow`
2. `rIP = RAM[rSP]` (unchecked read, 8 bytes LE)
3. `rSP += 8`

`stackBase` is the value of `rSP` immediately after `LaunchProgramm` (top of RAM, aligned down to 8).

### 5.7. Stack

**`PUSH Reg1`**:
1. `sp = rSP - 8`
2. `if rHP >= sp → SegmentationFault`
3. `RAM[sp] = Reg1` (checked write of 8 bytes)
4. if the write fails → return its status
5. `rSP = sp`

**`POP Reg1`**:
1. `value = RAM[rSP]` (checked read of 8 bytes LE)
2. if the read fails → return status
3. `Reg1 = value; rSP += 8`

### 5.8. I/O ports

Port space: `PortBus.TotalPorts = 2^ports.Log2`, sector size `2^portsOnDev.Log2`. Address = `(sector << shift) | offset`, where `shift = portsOnDev.Log2`.

**`IN Reg1, Reg2`** — `addr = Reg2`; `Reg1 = PortBus.ReadPort(addr)`. If no device is registered at the address → `NullDeviceInput`, `FaultData = addr`. The device returns **1 byte**, zero-extended to 64 bits.

**`OUT Reg1, Reg2`** — `addr = Reg2`; `PortBus.WritePort(addr, (byte)Reg1)`. If no device is present → `NullDeviceOutput`. Only the least significant byte of `Reg1` is transferred.

**`WAKE_INT Reg1`** — `addr = Reg1`; `PortBus.WakeProcessor(addr)`. If the device is not found → `NullDeviceOutput`. The device receives the `WakeProcessor()` signal, which clears `IsSleeping` on the next step.

### 5.9. Sleep

**`HALT`** — `IsSleeping = true`. External commands continue to be processed, but program-body steps are not executed.

**`WAKE`** — as an instruction in the program body: no-op (`Success`). As an **external command** via `EnqueueExternalCommand` or `PortBus.WakeProcessor` — clears `IsSleeping`.

**`WAKE_INT Reg1`** — see 5.8. I/O ports

---

## 6. Memory model

### 6.1. RAM

- Single byte-addressable space `[0, RAM.Length)`.
- Byte order: **little-endian** (endianness is fixed by `NativeMemoryBuffer` on all platforms).
- Size: `RamSize` (power of two, 64 B … 2 GiB). By default allocated via `NativeMemory.AllocZeroed`.

### 6.2. Checked vs unchecked accesses

| Aspect | Checked (`LOAD`/`STORE`/`LOAD_IND`/`STORE_IND`) | Unchecked (`*_UNSAFE`) |
|--------|----------------------------------------------------|----------------------------|
| Alignment check | yes (for `S16/S32/S64`) | no |
| Bounds check | yes (`addr + N <= Length`) | no |
| Error status | `AlignmentFault` / `SegmentationFault` | n/a, UB |
| `FaultData` | address | n/a |
| Return value on read error | 0 | UB |

### 6.3. Immediate operands

- Read via `ReadInt64LEUnSafe` (unchecked, unaligned).
- Address = `AlignUp(rIP_after_instr, 8)`.
- After reading, `rIP = addr + 8`.

---

## 7. I/O ports

- Address space: `PortBus.TotalPorts` (power of two, `PortSize`).
- Divided into `SectorCount = TotalPorts / PortsOnDevice` sectors.
- One sector = one device (`IPortController`).
- Address: `sector = addr >> shift`, `offset = addr & (PortsOnDevice - 1)`, `shift = log2(PortsOnDevice)`.
- Sector 0..`SectorCount-1`. If the sector is not registered — error (`NullDeviceInput`/`NullDeviceOutput`).

The device implements `ReadPort(offset) → byte` and `WritePort(offset, value)`.

---

## 8. Fault model

### 8.1. Statuses (`BiosStatus : byte`)

| Code | Name | Meaning |
|----:|-----|-------|
| 0  | `Success` | success (default, safe value) |
| 1  | `SegmentationFault` | RAM out-of-bounds / stack-heap collision |
| 2  | `AlignmentFault` | incorrect address alignment |
| 3  | `EndProgram` | normal termination (`END`) |
| 4  | `HostStopped` | host-side stop (`Stop`) |
| 5  | `NotImplementedOpCode` | opcode has no handler |
| 6  | `NullDeviceInput` | no input device |
| 7  | `NullDeviceOutput` | no output device / wake port |
| 8  | `InfinityLoop` | step limit / policy triggered |
| 9  | `DivOnZero` | division by zero (`DIV`, `MOD`) |
| 10 | `NotSupportedOpCode` | opcode is known but unsupported |
| 11 | `StackUnderflow` | `RET` on an empty stack / stack overflow |
| 12 | `UnsafeOpCodeForbidden` | use of an unsafe `OpCode` when the `IProcessorPolicy.AllowUnsafeOpCodes` policy has preemptively forbidden it |

### 8.2. `ProcessorFault`

When a status ≠ `Success` is received (including the “normal” `EndProgram`, `HostStopped`, `InfinityLoop`), a snapshot is formed:

```csharp
ProcessorFault(BiosStatus Status,
               ulong FaultData,
               ulong InstructionPointer,
               OpCode OpCode);
```

- `InstructionPointer` — `rIP` immediately after reading the 4-byte instruction word.
- `OpCode` — the real opcode; the value `OpCode.NOP` (0) is reserved for “unknown/no opcode” (a controversial decision, but fixed).
- `FaultData` — context: memory address, port number, opcode, etc.

Then `IProcessorPolicy.ShouldContinue(fault)` is called; its response is written to `IsRunning`. The policy **must** distinguish “real errors” from “normal” `EndProgram`/`HostStopped`/`InfinityLoop`.

---

## 9. Step execution (`Processor.Step`)

Pseudocode for one step:

```
1. If _hasCommand:
     a. Process all external commands (_externalCommands).
     b. Process all external statuses (_statusFromSimulation):
        - if status is non-zero — form ProcessorFault and query the policy.
     c. _hasCommand = false.
2. If !IsRunning || IsSleeping → exit.
3. rIP_cur = rIP
4. rawInst = RAM[rIP_cur]                     // unchecked, 32 bits LE
5. opCode = Decode(rawInst).OpCode
6. rIP = rIP_cur + 4
7. status = switch(opCode) { ... Instruction*(...) ... }
8. If status != Success:
     _faultData = new(status, _lastFaultData, rIP, opCode)
     IsRunning = policy.ShouldContinue(_faultData)
```

Important: `_lastFaultData` is set inside the handler before returning the status (for example, `FaultData` from `MemoryBus`, port number, opcode `NotImplementedOpCode`).

---

## 10. Assembler syntax

Implemented in `AssemblerParser` (NANASM-compatible text). **Case-insensitive** for mnemonics and registers.

### 10.1. Registers

`r0..r27`, `rFL`, `rSP`, `rHP`, `rIP`.

### 10.2. Mnemonics

- No operands: `NOP`, `END`, `RET`, `HALT`, `WAKE`.
- One register: `INC`, `DEC`, `NOT`, `PUSH`, `POP`, `JMP_IND`, `CALL_IND`, `WAKE_INT`.
- Two registers: `MOV`, `ADD`, `SUB`, `AND`, `OR`, `XOR`, `IN`, `OUT`, `SHR`, `SHL`, `DIV`, `MULT_INT`, `MOD`, `CMP`, `TEST`.
- `LDI reg, imm`: `imm` — a number (`0x…` or decimal) or a label.
- `LOAD[.size] reg, addr`, `STORE[.size] addr, reg`: `addr` — a number or a label.
- `LOAD_IND[.size] reg, reg`, `STORE_IND[.size] reg, reg` — similarly.
- `LOAD_UNSAFE[.size]`, `STORE_UNSAFE[.size]`, `LOAD_IND_UNSAFE[.size]`, `STORE_IND_UNSAFE[.size]` — similarly, without checks.
- Branches: `JMP`, `JZ`, `JNZ`, `JG`, `JL`, `CALL` — label or absolute address.

### 10.3. Size suffixes

`.S8`, `.S16`, `.S32`, `.S64` (or `.8`, `.16`, `.32`, `.64`). Default is `.S64`. Spaces around the dot are allowed: `LOAD . S8` normalizes to `LOAD.S8`.

### 10.4. Labels and patches

- `label:` — label at the current position.
- `label: <instruction>` — label + instruction.
- Patches for immediate fields are resolved at `Build()`: `targetAddr = baseAddress + offset(label)`.
- `baseAddress` must be a multiple of 8 (otherwise the immediate will be read at a different address than written).

### 10.5. Sections and data

- Directives: `.data`/`.bss`/`.section .data`/`.section .bss` — data section; `.text`/`.code`/`.section .text`/`.section .code` — code section.
- Data directives (only inside `.data`): `.byte`/`db`, `.word`/`dw` (2), `.dword`/`dd` (4), `.quad`/`dq` (8), `.ascii`, `.asciz`/`.string`, `.zero`/`.space N`.
- Strings: `.byte "..."` allows escape sequences `\n \t \r \0 \\ \" \'`.
- Data section alignment is 8 bytes.

---

## 11. Encoding examples

### 11.1. `MOV r1, r2`

```
OpCode = 2   (0x02)
Reg1   = 1
Reg2   = 2
raw = 0x02 | (1 << 8) | (2 << 13)
    = 0x00004102
Bytes in memory (LE): 02 41 00 00
```

### 11.2. `LDI r1, 42`

```
OpCode = 5
Reg1   = 1
raw = 0x05 | (1 << 8) = 0x0105
Occupied: instr (4 bytes), padding (4), imm (8 bytes)
Bytes (base=0): 05 01 00 00 | 00 00 00 00 | 2A 00 00 00 00 00 00 00
```

### 11.3. `JMP label` (label at 0x100)

```
OpCode = 20 (0x14)
Size   = S64 (3) in bits 18–19
raw = 0x14 | (3 << 18) = 0x000C0014
Bytes (base=0, label=0x100): 14 00 0C 00 | 00 00 00 00 | 00 01 00 00 00 00 00 00
```

### 11.4. `LOAD.S32 r5, [0x10]`

```
OpCode = 3
Reg1   = 5
Size   = S32 (2)
raw = 0x03 | (5 << 8) | (2 << 18) = 0x00080503
Bytes: 03 05 08 00 | 00 00 00 00 | 10 00 00 00 00 00 00 00
```

### 11.5. `ADD r1, r2`

```
OpCode = 8
Reg1   = 1
Reg2   = 2
raw = 0x08 | (1 << 8) | (2 << 13) = 0x00004108
Bytes: 08 41 00 00
```

---

## 12. Limitations and known implementation features

5. **`WAKE` as an instruction in the body is a no-op.** Waking from `HALT` is possible only via `EnqueueExternalCommand(OpCode.WAKE)` or `PortBus.WakeProcessor`.
7. **`InstructionLOAD`/`InstructionSTORE` do not return `AlignmentFault` for invalid `OpCodeSize`** (there `default` → `SegmentationFault` in LOAD and `AlignmentFault` in STORE); in practice `OpCodeSize` has exactly 4 values — `default` is unreachable.
9. **`OpCode.NOP` = 0 is used as the “unknown opcode” marker** in `ProcessorFault` (`ProcessSimulationStatusSlow`). This is a controversial convention; if a real NOP fault appears, it will be impossible to distinguish it from an “empty” value.

---

## 13. Quick reference for conventions

| Convention | Value |
|------------|-------|
| Byte order | little-endian everywhere |
| Word format | `[7:0]=op, [12:8]=reg1, [17:13]=reg2, [19:18]=size, [31:20]=0` |
| Immediate | always 64 bits, 8-byte alignment after the instruction |
| Instruction length | 4 or 12 bytes |
| Registers | 32 × 64 bits, `rIP` — address of the next instruction |
| Flags | updated only by `CMP` and `TEST` |
| Stack | 8-byte slots, `rSP` grows downward, `rHP` — upper bound of the heap |
| Access error | `AlignmentFault` / `SegmentationFault`, not exceptions |
| Policy | `IProcessorPolicy.ShouldContinue` — the final authority (last resort); `IProcessorPolicy.AllowUnsafeOpCodes` — permission to use unsafe opcodes |