# Nantuc.VmApplication.Extensions — README

> Extensions to the main API: disassembler, debugger, I/O devices, standard optimizations (AST and IR), "one-liner" compilation utilities, built-in MiniC/NANASM libraries.
> Depends on `[Nantuc.VmApplication]` (transitively on `Diagnostics`, `Kernel`, `Contracts`, `Common`; directly from `Common` it uses `OpCode`, `AlignUp`, `InstructionDecoder`).
> `[← Root](../README.md) · [VmApplication](../../src/Nantuc.VmApplication/README.md) · [Diagnostics](../../src/Nantuc.Diagnostics/README.md) · [Kernel](../../src/Nantuc.Kernel/README.md) · [Common](../../src/Nantuc.Kernel.Common/README.md) · [Contracts](../../src/Nantuc.Kernel.Contracts/README.md) · [Diagnostics](../../src/Nantuc.Kernel.Diagnostics/README.md) · [STDLIBRARIES](STDLIBRARIES.md)`

## Table of contents
- [About this module](#about-this-module)
- [Local project tree](#local-project-tree)
- [API](#api)
- [API examples](#api-examples)
- [Closeness of the module to the user](#closeness-of-the-module-to-the-user)
- [What was fixed in this version](#what-was-fixed-in-this-version)
- [Special notes](#special-notes)
- [What to fix](#what-to-fix)

## About this module

`Nantuc.VmApplication.Extensions` is the "batteries" for the main API. There are no new kernel abstractions here, only extensions on top of the existing ones: `DeviceContext`, `VmHost`, `VmHostProject`, `VmEmulator`, `IPortController`. You can omit this module — the main API works without it. But with it you get:

- **Disassembler** (`Disassembler`/`StringDisassembler`/`InstructionFormatter`): parses `CompilationResult.Program` into `DecodedInstruction[]` + `DecodedDataChunk[]` and a textual form (like `objdump`).
- **Debugger** (`Debugger`): step-by-step execution on top of `DeviceContext` — `Step`/`StepOver`/`StepOut`/`Continue`/`ContinueTo`/`RunToEnd`, breakpoints, best-effort call stack, RAM reads (`ReadUInt16/32/64`, `ReadCString`), disassembly of the current instruction via `StringDisassembler`. Does not use `AsRawDevice` or `unsafe` — only the public API of `DeviceContext`/`ProcessorSnapshot`.
- **I/O devices** (`NumericIOStream`, `SynchronousIOStream`, `QueuedIOStream`): output of bytes/UTF-8/UTF-16 from the VM to a `TextWriter`. Plus `PortCharEncoding` and `OutputPortLayout` (layout of the 8-byte window).
- **Standard optimizations**: AST rules (`FoldConstants`, `PropagateConstants`, `RemoveUnusedVariables`, `RemoveUnreachableCode`, `InlineSmallVoidFunctions`) and IR rules (`ConstantFolding`, `RemoveNoopMov`, `RemovePushPopPair`, `RemoveSwapMovPair`, `RemoveJumpToNextLabel`, `RemoveMultiEnd`).
- **Compilation utilities** (`ProjectExtension`, `VMHostRunExtensions`): `host.Project.CompileToIR("int main(){}", 0, true, SourceLanguage.C)` and `host.RunAndKeep(...)`, `host.RunOnce(...)`, `host.RunAndCleanup(...)` — one line instead of a pipeline. `RunAndKeep`/`RunOnce`/`RunAndCleanup` can register a UART via `QueuedIOStream` when `output` is passed.
- **Built-in libraries** (`Libraries`): string constants `heap_nanasm`, `alloc_minic`, `io_nanasm`, `print_minic`, `imath_minic`, `bitops_minic`, `ovf_minic` for `#include`. See [STDLIBRARIES.md](STDLIBRARIES.md) for details.
- **RAM utilities** (`DeviceContextExtensions`): `GetRamAsNewArray` / `GetAllRamAsNewArray`, `CopyRamTo` into `Span<byte>` / `byte[]` / `Memory<byte>` / `Stream`, `ReadRam<T>` / `CopyRamTo<T>` for `unmanaged` types, `TryCopyRamTo` without exceptions — read RAM of a **stopped** VM (all except `TryCopyRamTo` throw `InvalidOperationException` when `ctx.IsRunning`).
- **Bytecode utilities** (`BytecodeExtension`): `GetOpCodeSafety` / `GetUnsafeOpCodes` — static analysis of `CompilationResult.Program` for `*_UNSAFE` opcodes (`OpCodeSafety.Safe`/`HasUnsafe`/`Unknown`) and locating them (`UnsafeOpCodeLocation(OpCode, Offset, Ip)`). Useful for checking untrusted code before running and for verifying the result of `ReplaceSafeMemAccessWithUnsafeRule`.

The key idea is not to change the `Nantuc.VmApplication` contract but to extend it. All rules implement `IAstOptimizationRule`/`IOptimizerIRRule` from the compiler, all I/O devices inherit `PortControlBase` from `VmApplication`, and the debugger works through `DeviceContext.RunStepMode`/`SingleStep`/`GetRegistersSnapshot`/`NativeReadOnlyBuffer`. No new interfaces in `Kernel.Contracts`.

## Local project tree

Namespaces match folders:

```
Nantuc.VmApplication.Extensions/
├── Nantuc.VmApplication.Extensions.csproj   # net10.0, ref → VmApplication
├── Bytecode/
│   └── BytecodeExtension.cs                  # extension on CompilationResult: GetOpCodeSafety / GetUnsafeOpCodes + OpCodeSafety + UnsafeOpCodeLocation + IsUnsafeOpcode
├── Diagnostics/
│   └── DiagnosticsCompilerExtension.cs       # extension on IReadOnlyDictionary<ErrorCode,string> (ValidateAllCodesCovered / TryValidateAllCodesCovered) — see "What to fix", item 3
├── Debugging/
│   ├── Debugger.cs                           # step debugger on DeviceContext: Step/StepOver/StepOut/Continue/ContinueTo/RunToEnd/breakpoints/RAM/disassembler/stack
│   ├── DebugStop.cs                          # readonly record struct: Reason + IP + StepCount + ProcessorSnapshot + ProcessorFault
│   ├── DebugStopReason.cs                    # enum: None/StepComplete/Breakpoint/Completed/Faulted/Sleeping/StepLimit/UserStop/Error
│   └── CallStackFrame.cs                     # readonly record struct: Index + ReturnAddress + StackAddress (best-effort)
├── DeviceCtx/
│   └── DeviceContextExtensions.cs            # GetRamAsNewArray, CopyRamTo
├── Disassembler/
│   ├── DecodedDataChunk.cs                   # record struct (Address, Value, Bytes, DecodedString, ByteLength)
│   ├── DecodedInstruction.cs                 # record struct (Address, RawInstruction, OpCode, Reg1, Reg2, DataSize, Data64?, ByteLength)
│   ├── Disassembler.cs                       # public static: extension(res).Disassemble(int?) → DisassemblyResult
│   ├── DisassemblyResult.cs                  # sealed: Instructions, DataChunks, BaseAddress, CodeSize, TotalSize, BytesConsumed, HasDataSection
│   ├── InstructionFormatter.cs               # static: Format(DisassemblyResult | DecodedInstruction); Format(StringBuilder, ...)
│   └── StringDisassembler.cs                 # static: Disassemble(ReadOnlySpan<byte>, int, ulong) → string; extension(res).DisassembleToString()
├── Emulator/IO/
│   ├── NumericIOStream.cs                    # synchronous: writes numeric character codes
│   ├── OutputPortLayout.cs                   # const: WindowSize=8, Data=0, Status=1, Flush=2
│   ├── PortCharEncoding.cs                   # enum: BytePerChar | Utf8 | Utf16
│   ├── QueuedIOStream.cs                     # async: Channel<byte> + background drain
│   └── SynchronousIOStream.cs                # synchronous: writes characters
├── Host/
│   ├── HostExtensions.cs                     # RunOutcome + RunAndKeep / RunAndCleanup / RunOnce (+ UART via QueuedIOStream when output != null)
│   ├── LoggingFaultPolicyMinimal.cs          # IProcessorPolicy: logs the fault and stops the simulation
│   └── ProjectExtension.cs                   # Compile/CompileToIR from a string/lines + SaveFileInclude/SaveFileProject
├── StdLibraries/
│   └── Libraries.cs                          # static class Libraries: heap_nanasm, alloc_minic, io_nanasm, print_minic, imath_minic, bitops_minic, ovf_minic
└── Optimizators/
    ├── AstOptimizerExtension.cs              # extension(VmAstOptimizer).AddStdRules() + WithASTNodePrefix + OptimizationId constants
    ├── IROptimizerExtension.cs               # extension(VmIROptimizer).AddStdRules() + WithIRPrefix / WithRule + OptimizationId constants
    ├── IRLog.cs                              # IRLog : IRogOptimization — optimization log formatting
    ├── AstRules/
    │   ├── FoldConstantsRule.cs
    │   ├── InlineSmallVoidFunctionsRule.cs
    │   ├── PropagateConstantsRule.cs
    │   ├── RemoveUnreachableCodeRule.cs
    │   └── RemoveUnusedVariablesRule.cs
    └── IRAsmRules/
        ├── ConstantFoldingRule.cs
        ├── RemoveJumpToNextLabelRule.cs
        ├── RemoveMultiEndPairRule.cs
        ├── RemoveNoopMovRule.cs
        ├── RemovePushPopPairRule.cs
        ├── RemoveSwapMovPairRule.cs
        └── ReplaceSafeMemAccessWithUnsafeRule.cs
```

Organization notes:
- Folder and namespace — `StdLibraries`, file and public class — `Libraries`. Historically the folder was called `Librares`, and old docs had `Librare` — both typos are fixed. Namespace `Nantuc.VmApplication.Extensions.StdLibraries`, class `Libraries`.
- `AstOptimizerExtension.cs` and `IROptimizerExtension.cs` contain not only extension methods but also `OptimizationId` constants. They are part of the public contract: by these IDs you look up logs in `OptimizationResultLog`.
- `IRLog.cs` — a separate file (namespace `Nantuc.VmApplication.Extensions.Optimizators`), extracted from `IROptimizerExtension.cs`.
- `Diagnostics/DiagnosticsCompilerExtension.cs` — an extension on `IReadOnlyDictionary<ErrorCode, string>` (`ValidateAllCodesCovered`/`TryValidateAllCodesCovered`). According to changelog 0.7.2 these methods are marked as removed; the file is still present in the code — see [What to fix](#what-to-fix), item 3.
- `Debugging/` — a new layer on top of `DeviceContext`, does not change `Nantuc.VmApplication`. The debugger is not thread-safe (except `RequestStop`), which matches the `DeviceContext` model. The call stack is reconstructed heuristically — the VM does not keep a frame pointer. Details in [Special notes](#special-notes).
- `Bytecode/` — static analysis of `CompilationResult.Program`, does not touch `DeviceContext`/`VmEmulator`. `IsUnsafeOpcode` is the same set used by `ReplaceSafeMemAccessWithUnsafeRule`, so the module can be used to verify the IR rule's result (after application — all `LOAD`/`STORE`/`LOAD_IND`/`STORE_IND` are replaced with `*_UNSAFE`).
- `Host/HostExtensions.cs` — in the current version `RunAndKeep` uses only the safe API (`TryRegisterDevice`, `TryGetDeviceContext`, `TryLoadProgram`), no `!`. Side effect: on a registration/load error a `RunOutcome` with `Device == null` is returned instead of an NRE. UART is registered at `sector + 1` if `output` is passed.
- `Host/LoggingFaultPolicyMinimal.cs` — a separate file; previously the class lived in `ProjectExtension.cs`, now it is extracted.

## API

### Disassembler

**`DecodedInstruction`**:

```csharp
public readonly record struct DecodedInstruction(
    ulong Address,
    uint RawInstruction,
    OpCode OpCode,
    RegType Reg1,
    RegType Reg2,
    OpCodeSize DataSize,
    ulong? Data64,
    int ByteLength)
{
    public bool HasData64 { get; }
    public override string ToString();  // = InstructionFormatter.Format(this)
}
```

**`DecodedDataChunk`**:

```csharp
public readonly record struct DecodedDataChunk(
    ulong Address,
    ulong Value,
    byte[] Bytes,
    string? DecodedString,
    int ByteLength);
```

**`DisassemblyResult`**:

```csharp
public sealed class DisassemblyResult(
    ImmutableArray<DecodedInstruction> instructions,
    ImmutableArray<DecodedDataChunk> dataChunks,
    ulong baseAddress,
    int codeSize,
    int totalSize,
    int bytesConsumed)
{
    public ImmutableArray<DecodedInstruction> Instructions { get; }
    public ImmutableArray<DecodedDataChunk> DataChunks { get; }
    public ulong BaseAddress { get; }
    public int CodeSize { get; }
    public int TotalSize { get; }
    public int BytesConsumed { get; }
    public bool HasDataSection { get; }
    public override string ToString();
}
```

**`Disassembler`** — `public static class`:

```csharp
public static class Disassembler
{
    extension(CompilationResult res)
    {
        public DisassemblyResult Disassemble(int? codeSize = null);
    }
}
```

`compilation.Disassemble()` — the whole stream as code; `compilation.Disassemble(codeSize: compilation.CodeSize)` — with a data section.

**`StringDisassembler`** — `public static class`:

```csharp
extension(CompilationResult res)
{
    public string DisassembleToString();   // "(no program)" if !res.Success
}

public static string Disassemble(ReadOnlySpan<byte> program, int codeSize, ulong baseAddress = 0);
```

Format: `XXXXXXXX: XXXXXXXX   MNEMONIC [data = 0x...]` — like `objdump`. For the data section: `XXXXXXXX: XX XX ... .data 0xXXXXXXXXXXXXXXXX  "string"`.

**`InstructionFormatter`**:

```csharp
public static string Format(DisassemblyResult result);
public static void   Format(StringBuilder sb, DisassemblyResult result);
public static string Format(DecodedInstruction inst);
public static void   Format(StringBuilder sb, DecodedInstruction inst);
public static void   Format(StringBuilder sb, DecodedDataChunk chunk);  // StringBuilder form only
```

### Debugger

**`DebugStopReason`** — `enum`:

```csharp
public enum DebugStopReason
{
    None = 0,          // the debugger has not stopped yet
    StepComplete,      // one instruction executed (Step/StepOver/StepOut)
    Breakpoint,        // a breakpoint was hit
    Completed,         // the program executed END
    Faulted,           // BiosStatus != EndProgram && != Success
    Sleeping,          // HALT
    StepLimit,         // step limit reached in Continue
    UserStop,          // RequestStop()
    Error,             // internal debugger error
}
```

**`DebugStop`** — `readonly record struct`:

```csharp
public readonly record struct DebugStop(
    DebugStopReason Reason,
    ulong InstructionPointer,
    long StepCount,
    ProcessorSnapshot Snapshot,
    ProcessorFault Fault)
{
    public bool IsTerminal { get; }   // Completed || Faulted || Error
    public override string ToString();
}
```

**`CallStackFrame`** — `readonly record struct`:

```csharp
public readonly record struct CallStackFrame(
    int Index,
    ulong ReturnAddress,
    ulong StackAddress);
```

**`Debugger`** — `sealed class : IDisposable`:

```csharp
public sealed class Debugger : IDisposable
{
    public Debugger(DeviceContext context);

    public DeviceContext Context { get; }
    public bool IsActive { get; }
    public DebugStop? LastStop { get; }
    public IReadOnlyCollection<ulong> Breakpoints { get; }
    public event Action<DebugStop>? Stopped;

    public void Load(ReadOnlySpan<byte> program, ulong loadAddress = 0, ulong startAddress = 0);

    public DebugStop Step();
    public DebugStop StepOver();
    public DebugStop StepOut();
    public DebugStop Continue(long? stepLimit = null);
    public DebugStop ContinueTo(ulong address, long? stepLimit = null);
    public DebugStop RunToEnd(long? stepLimit = null);

    public void RequestStop();           // safe from another thread
    public void Wake();                  // resume from HALT (OpCode.WAKE)

    public void AddBreakpoint(ulong address);
    public bool RemoveBreakpoint(ulong address);
    public void ClearBreakpoints();
    public bool HasBreakpoint(ulong address);

    public SnapshotRegisters Registers { get; }
    public ulong InstructionPointer { get; }

    public byte[] ReadRam(ulong start, int length);
    public void   CopyRamTo(Span<byte> destination, ulong start);
    public byte   ReadByte(ulong address);
    public ushort ReadUInt16(ulong address);
    public uint   ReadUInt32(ulong address);
    public ulong  ReadUInt64(ulong address);
    public char   ReadChar(ulong address);
    public string ReadCString(ulong address, int maxLength = 1024);

    public string CurrentInstruction { get; }
    public string DisassembleAt(ulong address);
    public string DisassembleProgram();

    public IReadOnlyList<CallStackFrame> GetCallStack(int maxDepth = 16);

    public void Dispose();
}
```

Behavior:

- **`Load`** — `ctx.LoadProgram` + `ctx.RunStepMode(startAddress)`; after the call `IsActive == true`, but no instruction has been executed yet. `Debugger` is one-shot per `Load` — a repeated call throws `InvalidOperationException` (protection against leaking step mode; for a new run — a new `Debugger`).
- **`Step`** — one instruction via `ctx.SingleStep(onStep)`; the result is a `DebugStop` with a processor snapshot from the callback (`ProcessorSnapshot` from the VM — with correct `IsSleeping`).
- **`StepOver`** — if the current instruction is `CALL`/`CALL_IND`, runs to the return address (`ip + 4` for `CALL_IND`, `AlignUp(ip + 4, 8) + 8` for `CALL`); otherwise behaves like `Step`.
- **`StepOut`** — runs `Step` until `rSP` becomes larger than the value at call time. Best-effort: if the function uses `PUSH`/`POP` for temporaries, semantics break — see [What to fix](#what-to-fix), item 12.
- **`Continue`** — runs `SingleStep` until `Breakpoint`/`Completed`/`Faulted`/`Sleeping`/`StepLimit`/`UserStop`. At start it checks `_skipBreakpointOnce` (see [Special notes](#special-notes)) so that `Continue` → breakpoint → `Continue` does not loop.
- **`ContinueTo(addr)`** — temporarily adds a breakpoint at `addr`, calls `Continue`, removes the breakpoint. If `addr` already had a user breakpoint — it is preserved.
- **`RunToEnd`** — `Continue` with a temporarily cleared set of breakpoints (to "run to the end, ignoring points").
- **`RequestStop`** — the only method safe from another thread: `volatile bool _stopRequested`. The `Continue`/`StepOver`/`StepOut` loops check it on every iteration.
- **`Wake`** — `ctx.Resume()`; puts `OpCode.WAKE` into the queue. Needed if the debugger stopped at `HALT` and you want to continue without `Stop`/re-`Load`.
- **`ReadRam`/`CopyRamTo`** — via `ctx.NativeReadOnlyBuffer`; throw `ArgumentOutOfRangeException` on out-of-bounds and `ObjectDisposedException` after `Dispose`. Unlike `DeviceContextExtensions.GetRamAsNewArray`, they do **not** throw `InvalidOperationException` when `IsRunning` — the debugger typically reads RAM exactly during a stop.
- **`ReadUInt16/32/64`** — little-endian via `BinaryPrimitives` (matches the `MemoryBus` contract).
- **`ReadCString`** — reads bytes until `0`; `maxLength` (default 1024) protects against reading all of RAM if the string is not terminated.
- **`CurrentInstruction`/`DisassembleAt`/`DisassembleProgram`** — use `StringDisassembler.Disassemble`; format is like `objdump`. `DisassembleProgram` disassembles the range `[startAddress, loadAddress + program.Length)` fixed in `Load`.
- **`GetCallStack`** — best-effort: scans the stack (`[rSP, RamSize)`) for 8-byte values in the range `[ProgramStart, ProgramEnd)`, aligned to 4. False positives are possible (values of local variables). `maxDepth` limits the depth (default 16). See [Special notes](#special-notes).
- **`Stopped`** — an event fired after every `DebugStop` (including the final one). Convenient for logging/visualization.

### I/O devices

**`PortCharEncoding`**: `enum { BytePerChar, Utf8, Utf16 }`.

**`OutputPortLayout`** — layout of the 8-byte window:

```csharp
public const ulong WindowSize = 8;
public const ulong Data   = 0;   // write: byte to output; read: 0
public const ulong Status = 1;   // 0 — ready, 1 — unprocessed data present
public const ulong Flush  = 2;   // write 1 — flush the buffer
```

**`SynchronousIOStream : PortControlBase`**:

```csharp
public SynchronousIOStream(TextWriter writer, PortCharEncoding encoding);
```

`WritePort(Data, value)` → `_writer.Write(...)` + `Flush()`. `WritePort(Flush, _)` → `_writer.Flush()`. `ReadPort(_)` → 0. `WakeProcessor()` → `_writer.Flush()`.

**`NumericIOStream : PortControlBase`** — synchronous, writes **numeric codes** of characters:

```csharp
public NumericIOStream(TextWriter writer, PortCharEncoding encoding, bool hex = false);
```

`BytePerChar` → `EmitNumber(value)` (0..255). `Utf8`/`Utf16` → `EmitNumber(codePoint)`. Format: `"42 "` or `"0x2A "`.

**`QueuedIOStream : PortControlBase`** — asynchronous via `Channel<byte>`:

```csharp
public QueuedIOStream(TextWriter writer, PortCharEncoding encoding);
```

`WritePort(Data, value)` → `Channel.Writer.TryWrite(value)` + `Interlocked.Increment`. `ReadPort(Status)` → `1` if pending, `0` otherwise. `WritePort(Flush, _)` → polling for up to 2 sec. `Dispose` → `Complete` + `_drainTask.Wait(5s)`.

### Optimizations

**`AstOptimizerExtension`**:

```csharp
extension(VmAstOptimizer optimizer)
{
    public VmAstOptimizer AddStdRules();
}

extension(OptimizationId id)
{
    public OptimizationId WithASTNodePrefix();   // prefix "astnode-opt."
}

public static readonly OptimizationId FunctionInlined;      // astnode-opt.inline-func
public static readonly OptimizationId ConstantPropagated;   // astnode-opt.const-propagate
public static readonly OptimizationId ConstantFolded;       // astnode-opt.const-fold
public static readonly OptimizationId NodeRemoved;          // astnode-opt.remove-node
public static readonly OptimizationId VariableRemoved;      // astnode-opt.remove-variable-unused
```

Standard AST rules (`AddStdRules`):
1. `PropagateConstantsRule` — `IdentifierNode` → `NumberNode` if the variable is declared with a constant and is not reassigned (anywhere in the function).
2. `FoldConstantsRule` — `Number op Number` → `Number`, including unary `-`/`!`/`~` and recursively over all expressions (`FunctionCallNode`, `ArrayAccessNode`, `MemberAccessNode`, `AddressOfNode`, `DereferenceNode`).
3. `RemoveUnusedVariablesRule` — removes `VariableNode`s that are never read and never assigned (assignment counts as use), only if the initializer is side-effect-free. Side-effect-free: `NumberNode`, `IdentifierNode`, `BinaryOpNode`, `UnaryOpNode`, and `null`.
4. `RemoveUnreachableCodeRule` — removes code after `return`; `if(false)` is replaced with the contents of `else` (or removed); `if(true)` is replaced with the contents of `then`; `while(false)` and `for(...; false; ...)` are removed. Recursively over nested blocks.
5. `InlineSmallVoidFunctionsRule` — inlines `void` functions with `CountStatements(body) ≤ 10` (counting nested), without direct self-recursion (`!ContainsCallTo(body, name)`). Mutual recursion of two candidates (`A` calls `B`, `B` calls `A`) is **not** filtered out — duplicate expansion is possible, but not infinite (single pass). `main` bodies are not inlined.
6. `RemoveUnreachableCodeRule` — once more (after inlining). See [What to fix](#what-to-fix), item 9.

**`IROptimizerExtension`**:

```csharp
extension(VmIROptimizer IR)
{
    public VmIROptimizer AddStdRules();
}

extension(OptimizationId id)
{
    public OptimizationId WithIRPrefix();    // prefix "IR."
    public OptimizationId WithRule();              // suffix "-rule"
}

public static readonly OptimizationId ConstantFolding;          // IR.constant-folding
public static readonly OptimizationId RemoveJumpToNextLabel;    // IR.remove-jump-to-next-label
public static readonly OptimizationId RemoveMultiEnd;           // IR.remove-multi-end
public static readonly OptimizationId RemoveNoopMove;           // IR.remove-noop-mov
public static readonly OptimizationId RemovePushPop;            // IR.remove-push-pop
public static readonly OptimizationId RemoveSwapMov;            // IR.remove-swap-mov
public static readonly OptimizationId ReplaceSafeAccess;        // IR.replace-safe-mem-acces-with-unsafe
```

Standard IR rules (`AddStdRules`):
1. `RemoveNoopMovRule` — `MOV rX, rX` (not immediately after a label).
2. `RemovePushPopPairRule` — `PUSH rX; POP rX` (neither `PUSH` nor `POP` immediately after a label).
3. `RemoveSwapMovPairRule` — `MOV rA, rB; MOV rB, rA` (not immediately after a label). See the note in [What to fix](#what-to-fix), item 15: with the typical `MOV dst, src` semantics this pattern is not a no-op; the rule is kept for backward compatibility and must be verified against the real `OpCode.MOV` semantics.
4. `RemoveJumpToNextLabelRule` — `JMP label; label:` → remove `JMP`.
5. `ConstantFoldingRule` — `LDI rA, X; LDI rB, Y; OP rA, rB` → `LDI opInstr.FirstReg, X op Y` (no labels between `ldi1`, `ldi2`, `op`). Supported opcodes: `ADD`, `SUB`, `MULT_INT`, `DIV`, `AND`, `OR`, `XOR`, `SHR`. The "reverse" register order is allowed (`ldi1.FirstReg == op.SecondReg && ldi2.FirstReg == op.FirstReg`). With `DIV` and `right == 0` the rule is a **no-op** (does not touch the instructions).
6. `RemoveMultiEndPairRule` — `END; END` → a single `END` (chains of >2 `END`s are collapsed sequentially, because `newIndex == index`).

**`ReplaceSafeMemAccessWithUnsafeRule`** — is **not** part of `AddStdRules`, added manually:

```csharp
public sealed class ReplaceSafeMemAccessWithUnsafeRule : IOptimizerIRRule
{
    public RamSize SizeRam { get; set; } = RamSize.B128;   // default
}
```

Replaces `LOAD`/`STORE`/`LOAD_IND`/`STORE_IND` with `*_UNSAFE` variants if:
- the address is known: for `LOAD`/`STORE` — `Immediate`, for `LOAD_IND`/`STORE_IND` — a preceding `LDI` into the address register (`instr.SecondReg`);
- `address + dataSize ≤ SizeRam.Bytes`;
- `address % dataSize == 0` (alignment by data size; `dataSize` from `OpCodeSize`).

Default `RamSize.B128` (128 bytes) is **underestimated** — the rule only fires on the smallest addresses unless `SizeRam` is set to the actual RAM size of `Device`. See [What to fix](#what-to-fix).

**`IRLog`**:

```csharp
public class IRLog(OptimizationId id) : IRogOptimization
{
    public OptimizationId Id => id;                  // no additional prefix
    public void Append(int passCount, AsmItem item); // formats AsmItem into a string
    public void Append(int pass, string message);
    public void Clear();
    public IReadOnlyList<OptimizationLogEntry> GetLogs(); // returns the log, does NOT clear it
}
```

### Compilation and run utilities

**`ProjectExtension`**:

```csharp
extension(VmHostProject proj)
{
    public CompilationToIRResult CompileToIR(ulong baseAddress, bool optimize);
    public CompilationResult    Compile(ulong baseAddress, bool optimize);

    public CompilationToIRResult CompileToIR(string source, ulong baseAddress, bool optimize, SourceLanguage language);
    public CompilationToIRResult CompileToIR(IEnumerable<string> sources, ulong baseAddress, bool optimize, SourceLanguage language);
    public CompilationResult    Compile(string source, ulong baseAddress, bool optimize, SourceLanguage language);
    public CompilationResult    Compile(IEnumerable<string> sources, ulong baseAddress, bool optimize, SourceLanguage language);

    public void SaveFileInclude(string localPath, string content);
    public void SaveFileProject(string localPath, string content);
}
```

`SaveFileInclude`/`SaveFileProject` use `proj.FileService.Combine(paths.IncludePath/ProjectPath, localPath)` and `SaveFile`. `localPath` is not checked for `..` — see [What to fix](#what-to-fix).

**`VMHostRunExtensions`**:

```csharp
public sealed record RunOutcome(
    DeviceContext? Device,
    int? DeviceId,
    CompilationResult Compilation,
    long StepCount,
    bool Success,
    IReadOnlyList<string> Diagnostics,
    ProcessorFault FaultInfo);

extension(VmHost host)
{
    public RunOutcome RunAndKeep(
        string source, SourceLanguage lang,
        RamSize? ram = null, bool optimize = false,
        string deviceName = "run", uint sector = 0,
        TextWriter? output = null,
        PortCharEncoding encoding = PortCharEncoding.Utf16);

    public bool RunAndCleanup(
        string source, SourceLanguage lang,
        RamSize? ram = null, bool optimize = false, uint sector = 0);

    public RunOutcome RunOnce(
        string source, SourceLanguage lang,
        RamSize? ram = null, bool optimize = false, uint sector = 0);
}
```

- `RunAndKeep` — compiles `source`, registers a VM (`DeviceSpec` with `RamSize.MB16` by default if `ram == null`) at `sector`, loads and runs. If `output != null` is passed, additionally registers `QueuedIOStream(output, encoding)` as a device named `"{deviceName}-console"` at `sector + 1`; on UART registration failure the VM is rolled back. `Success` = `fault.Status == BiosStatus.EndProgram` (not "the fact of launch"). All operations go through the safe API (`TryRegisterDevice`, `TryGetDeviceContext`, `TryLoadProgram`): on failure a `RunOutcome` is returned with `Device == null`, `DeviceId == null`, `Success == false` and diagnostics in `Diagnostics`, without NRE and without exceptions.
- `RunAndCleanup` — `RunAndKeep` + `UnregisterDevice`. Returns `Success`.
- `RunOnce` — `RunAndKeep` + `UnregisterDevice`, returns a `RunOutcome` with `Device = null, DeviceId = null`.

**`LoggingFaultPolicyMinimal`**:

```csharp
public class LoggingFaultPolicyMinimal(TextWriter output) : IProcessorPolicy
{
    public bool ShouldContinue(in ProcessorFault fault);
    // writes a line to output, returns false (stop the simulation)
}
```

### Built-in libraries

**`Libraries`** — `static class` with string constants (namespace `Nantuc.VmApplication.Extensions.StdLibraries`):

- `heap_nanasm` — `func_HeapGet`, `func_HeapSet`, `func_GetSP`, `func_Load64/32/16/8`, `func_Store64/32/16/8`.
- `alloc_minic` — first-fit allocator with split and coalescing. Block layout: header (size | FREE_BIT) + payload + footer. `MIN_BLOCK = 24`, `HDR_FTR = 16`.
- `io_nanasm` — `func_In`, `func_Out`.
- `print_minic` — `print8`, `print16`, `write8`, `write16`, `flush` via `io_nanasm`.
- `imath_minic` — `imin`/`imax`/`umin`/`umax`, `iclamp`/`uclamp`, `iabs`, `igcd`/`ugcd`, `ipow`, `uisqrt`, `idiv_floor`/`idiv_ceil`/`udiv_round_up`.
- `bitops_minic` — `upopcount`, `uclz`/`uctz`, `rotl64`/`rotr64`, `urev64`, `unext_pow2`.
- `ovf_minic` — `add_ovf_i32`/`sub_ovf_i32`/`mul_ovf_i32`, `add_ovf_u64`/`sub_ovf_u64`/`mul_ovf_u64`.

`imath_minic`, `bitops_minic`, and `ovf_minic` do not require `#include` of other libraries — they are independent. See [STDLIBRARIES.md](STDLIBRARIES.md) for details.

**Important:** the `FunctionGenerator.GenerateFunction` bug (asymmetric push/pop of callee-saved registers, which crippled `alloc_minic` with 3-4 locals) is **fixed**. So the "keep state in globals" workaround is no longer necessary: in the next iteration `alloc_minic` can (and should) be rewritten with locals. The current code still keeps `heapFreeList`/`heapCur`/`heapStart`/`FREE_BIT`/`MIN_BLOCK`/`HDR_FTR` in globals as legacy. See [STDLIBRARIES.md](STDLIBRARIES.md) for details.

### RAM utilities

**`DeviceContextExtensions`** — extension on `DeviceContext`. All methods read RAM via `NativeReadOnlyBuffer`; none of them use `unsafe`/`AsRawDevice` (only `IReadOnlyNativeBuffer`). `<AllowUnsafeBlocks>` is not required.

```csharp
extension(DeviceContext ctx)
{
    // ===== Arrays =====

    public byte[] GetRamAsNewArray(nuint start, nuint length);
    public byte[] GetRamAsNewArray(nuint start);              // from start to end of RAM
    public byte[] GetAllRamAsNewArray();                      // the whole RAM

    // ===== Span / Memory / byte[] =====

    public void CopyRamTo(Span<byte> destination, nuint start);
    public void CopyRamTo(Span<byte> destination, nuint start, nuint length);
    public void CopyRamTo(byte[] destination, int destinationIndex, nuint start, int length);
    public void CopyRamTo(Memory<byte> destination, nuint start);

    // ===== Try* (no exceptions) =====

    public bool TryCopyRamTo(Span<byte> destination, nuint start);

    // ===== Typed reads (unmanaged) =====

    public T ReadRam<T>(nuint byteOffset) where T : unmanaged;
    public void CopyRamTo<T>(Span<T> destination, nuint byteOffset) where T : unmanaged;

    // ===== Stream — for large volumes without a single huge allocation =====

    public void CopyRamTo(Stream stream, nuint start, nuint length, int bufferSize = 81920);
}
```

Behavior:

- **All methods except `TryCopyRamTo`** throw `InvalidOperationException` if `ctx.IsRunning`. `TryCopyRamTo` returns `false` in that case. This is consistent with the `DeviceContext` model: while `IsRunning`, the registry-visible buffer would produce race-y data on a synchronous read.
- **Bounds** are checked via `ArgumentOutOfRangeException.ThrowIfGreaterThan(start, bufferLength)` and `(length, bufferLength - start)`. For `GetRamAsNewArray(start, length)` additionally `length <= int.MaxValue` (an array larger than that cannot be allocated).
- **`CopyRamTo(Span<byte>, start, length)`** copies exactly `length` bytes even if `destination.Length` is larger — convenient for "with headroom" buffers.
- **`CopyRamTo(byte[], destinationIndex, start, length)`** — a convenience wrapper over `destination.AsSpan(destinationIndex, length)`; checks `ArgumentNullException`/`ArgumentOutOfRangeException` for `destinationIndex`/`length`.
- **`CopyRamTo(Stream, start, length, bufferSize = 81920)`** — reads RAM in chunks of `bufferSize` and writes to `stream.Write(...)`. Bounds `[start, start + length)` are checked once before the loop; it will not go beyond RAM.
- **`ReadRam<T>` / `CopyRamTo<T>`** — `MemoryMarshal.Read<T>` / `MemoryMarshal.AsBytes(...)` over a temporary byte buffer filled byte by byte. No `unsafe` — only `MemoryMarshal` and `System.Runtime.CompilerServices.Unsafe.SizeOf<T>()` (the latter does not require `<AllowUnsafeBlocks>`).
- **Byte-by-byte copying** (`for (int i = 0; i < destination.Length; i++) destination[i] = buf[(nuint)i + start];`) — slower than `Buffer.MemoryCopy` but does not require `unsafe`. For large volumes use `CopyRamTo(Stream, ...)` (at least it does not allocate one huge array).
- **`CopyRamTo<T>` on `Span<T>` with `start` in bytes** (not in elements of `T`): `CopyRamTo<T>(Span<T>, nuint)` takes `MemoryMarshal.AsBytes(destination)` — the `byteOffset` is in RAM bytes.

### Bytecode utilities

**`BytecodeExtension`** — `public static class` in namespace `Nantuc.VmApplication.Extensions.Bytecode`. Static analysis of `CompilationResult.Program`, does not touch `DeviceContext`/`VmEmulator`. Useful for checking untrusted code and for verifying `ReplaceSafeMemAccessWithUnsafeRule`.

```csharp
public static class BytecodeExtension
{
    public enum OpCodeSafety { Safe, HasUnsafe, Unknown }

    public readonly record struct UnsafeOpCodeLocation(
        OpCode OpCode,
        int    Offset,   // offset in Program (byte array)
        ulong  Ip);      // virtual address of the instruction

    extension(CompilationResult res)
    {
        public OpCodeSafety GetOpCodeSafety();
        public IReadOnlyList<UnsafeOpCodeLocation> GetUnsafeOpCodes();
        public IReadOnlyList<UnsafeOpCodeLocation> GetUnsafeOpCodes(out bool fullyScanned);
    }

    public static bool IsUnsafeOpcode(OpCode opCode);
}
```

Behavior:

- **`GetOpCodeSafety()`**:
  - `Unknown` — if `!res.Success`, `res.CodeSize` (or `Program.Length` if `CodeSize` == null) `< 0`, `> Program.Length`, or a truncated instruction was encountered (`pos + 4 > Program.Length`, or for a `HasNeed64IntData` opcode `nextPos + 8 > Program.Length`).
  - `HasUnsafe` — if at least one `*_UNSAFE` opcode was found.
  - `Safe` — if the program was scanned up to `actualCodeSize` and there are no unsafe opcodes.
- **`GetUnsafeOpCodes()`** — returns a list of all `*_UNSAFE` opcodes with their `OpCode`, `Offset` (in `Program`) and `Ip` (virtual address = `StartAdress + pos`). On errors/truncation — returns whatever was accumulated; the full list is guaranteed only if `fullyScanned == true`.
- **`GetUnsafeOpCodes(out bool fullyScanned)`** — the variant with a flag: `fullyScanned == false` means "parsing stopped on a truncated/broken program, the list may be incomplete". `!res.Success` → `fullyScanned = false`, empty list.
- **`IsUnsafeOpcode`** — `true` for `LOAD_UNSAFE`, `STORE_UNSAFE`, `STORE_IND_UNSAFE`, `LOAD_IND_UNSAFE`. The same set as in `ReplaceSafeMemAccessWithUnsafeRule`.

The parser accounts for instruction length: 4 bytes for a normal instruction, plus padding up to `AlignUp(8)` plus 8 immediate bytes for opcodes with `HasNeed64IntData` (e.g., `LDI`, `SYSCALL`, `CALL`). This matches §3.2 of [`bytecode.md`](../docs/bytecode.md).

### `DiagnosticsCompilerExtension`

An extension on `IReadOnlyDictionary<ErrorCode, string>`:

```csharp
extension(IReadOnlyDictionary<ErrorCode, string> templates)
{
    public void ValidateAllCodesCovered();
    public bool TryValidateAllCodesCovered(out ErrorCode code);
}
```

Used to validate custom message providers (`DefaultErrorMessageProvider.Templates`). Per changelog 0.7.2 these methods are marked as removed — the file is still present in the code, see [What to fix](#what-to-fix), item 3.

## API examples

**Disassembling a compilation result:**

```csharp
using Nantuc.VmApplication;
using Nantuc.VmApplication.Extensions.Disassembler;

using var host = VmHostFactory.CreateDefault();
var sources = host.Project.GetSourceFiles();
var comp = host.Project.Compile(sources, 0, optimize: false);

var disasm = comp.Disassemble(codeSize: comp.CodeSize);
foreach (var inst in disasm.Instructions)
    Console.WriteLine($"{inst.Address:X8}: {inst.RawInstruction:X8}  {inst.OpCode}");

foreach (var chunk in disasm.DataChunks)
    Console.WriteLine($"{chunk.Address:X8}: {chunk.Value:X16}  \"{chunk.DecodedString}\"");

Console.WriteLine(comp.DisassembleToString());
```

**Step debugging with breakpoints:**

```csharp
using Nantuc.Kernel.Common;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Extensions.Debugging;
using Nantuc.VmApplication.Project;

using var host = VmHostFactory.CreateDefault();

var sources = host.Project.GetSourceFiles();
var comp = host.Project.Compile(sources, baseAddress: 0, optimize: false);
if (!comp.Success) { foreach (var e in comp.Errors!) Console.Error.WriteLine(e); return; }

var state = host.Emulator.TryRegisterDevice(
    DeviceSpec.Create(ramSize: RamSize.MB16, name: "dbg"), sector: 0, out int vmId);
if (state != IdState.Valid) return;
if (!host.Emulator.TryGetDeviceContext(vmId, out var vm)) return;

using var dbg = new Debugger(vm);
dbg.Load(comp.Program!, loadAddress: 0, startAddress: 0);

dbg.AddBreakpoint(0x00);           // stop at the entry to main
dbg.Stopped += s => Console.WriteLine($"STOP: {s}");

// Step over CALL
var stop = dbg.StepOver();
Console.WriteLine($"after step-over: {stop}");
Console.WriteLine($"  at: {dbg.CurrentInstruction}");

// Continue to the breakpoint, but no more than 100k steps
stop = dbg.Continue(stepLimit: 100_000);
if (stop.Reason == DebugStopReason.Breakpoint)
    Console.WriteLine($"  hit bp at 0x{stop.InstructionPointer:X}");

// Continue to the end, ignoring all breakpoints
var final = dbg.RunToEnd();
Console.WriteLine($"final: {final.Reason} after {final.StepCount} steps");

// Inspect RAM and registers
var regs = dbg.Registers;
Console.WriteLine($"r0={regs[RegType.r0]}, sp=0x{regs[RegType.rSP]:X}");

byte[] head = dbg.ReadRam(0, 32);
ulong counter = dbg.ReadUInt64(0x100);
Console.WriteLine($"counter={counter}");

foreach (var frame in dbg.GetCallStack())
    Console.WriteLine($"#{frame.Index} return=0x{frame.ReturnAddress:X8}");
```

**Interrupting a long run from another thread:**

```csharp
using Nantuc.VmApplication.Extensions.Debugging;

using var dbg = new Debugger(vm);
dbg.Load(program, 0, 0);

var runner = Task.Run(() => dbg.Continue());     // can spin for a long time
Thread.Sleep(200);
dbg.RequestStop();                                // volatile bool, safe
var stop = runner.Result;                         // Reason == UserStop
```

> `RequestStop` is the only thread-safe `Debugger` method; the rest (`Step`/`Continue`/`Load`) must be called from a single coordinator thread.

**HALT stop → continue:**

```csharp
var stop = dbg.Continue();
if (stop.Reason == DebugStopReason.Sleeping)
{
    // VM is in HALT, waiting for WAKE_INT from a device
    // … handle sleep externally …
    dbg.Wake();                                    // Resume() → OpCode.WAKE in the queue
    stop = dbg.Continue();
}
```

**Continue to a specific function (by its address):**

```csharp
dbg.ContinueTo(address: 0x200);                    // temporary breakpoint at 0x200
// afterwards 0x200 is no longer in dbg.Breakpoints
```

**I/O output device (UTF-8):**

```csharp
using Nantuc.VmApplication.Extensions.Emulator.IO;

using var host = VmHostFactory.CreateDefault();
var uart = new SynchronousIOStream(Console.Out, PortCharEncoding.Utf8);
host.Emulator.RegisterDevice(uart, name: "UART0", sector: 1);
```

**Queued I/O — does not block the CPU:**

```csharp
var writer = new StreamWriter("output.txt");
var uart = new QueuedIOStream(writer, PortCharEncoding.Utf8);
host.Emulator.RegisterDevice(uart, name: "UART0", sector: 1);
```

**Standard AST optimizations:**

```csharp
using Nantuc.VmApplication.Extensions.Optimizators;

host.Project.Optimizator.VMAstOptimizer.AddStdRules();

var comp = host.Project.CompileToIR(sources, 0, optimize: true).Build();
if (comp.OptimizationResultLog is { } logs)
    foreach (var (id, log) in logs.Logs)
        foreach (var e in log.GetLogs())
            Console.WriteLine($"[{id}] pass {e.Pass}: {e.Message}");
```

**Standard IR optimizations:**

```csharp
host.Project.Optimizator.VMIROptimizer.AddStdRules();

var il = host.Project.CompileToIR(sources, 0, optimize: false);
il = host.Project.OptimizationIR(il, IROptimizationCount: 5);
var comp = il.Build();
```

**One-liner run with diagnostics:**

```csharp
using Nantuc.VmApplication.Extensions.Host;
using Nantuc.VmApplication.Project;
using Nantuc.Kernel.Common;

using var host = VmHostFactory.CreateDefault();

bool ok = host.RunAndCleanup("int main() { return 0; }", SourceLanguage.C);

var outcome = host.RunAndKeep("""
    int main() {
        int x = 42;
        return x;
    }
    """, SourceLanguage.C);

if (outcome.Success)
{
    var regs = outcome.Device!.GetRegistersSnapshot();
    Console.WriteLine($"r0={regs[RegType.r0]}, steps={outcome.StepCount}");

    if (outcome.FaultInfo.Status != BiosStatus.EndProgram)
        Console.Error.WriteLine($"VM fault: {outcome.FaultInfo.Status} " +
                                $"at IP=0x{outcome.FaultInfo.InstructionPointer:X}");
}
```

**Run with console output via UART:**

```csharp
var outcome = host.RunAndKeep(source, SourceLanguage.C, output: Console.Out);
// UART is registered at sector + 1, default encoding — Utf16.
// For Utf8 — pass encoding: PortCharEncoding.Utf8.
```

**Registering on a different sector:**

```csharp
var outcome = host.RunAndKeep(source, SourceLanguage.C, sector: 5);
// UART (if output != null) takes sector 6.
```

**Saving sources into the project:**

```csharp
using Nantuc.VmApplication.Extensions.StdLibraries;

host.Project.SaveFileProject("main.c", "int main() { return 0; }");
host.Project.SaveFileInclude("heap.nanasm", Libraries.heap_nanasm);
```

**Reading RAM after a run:**

```csharp
using Nantuc.VmApplication.Extensions.DeviceCtx;

var outcome = host.RunAndKeep("int main() { int x = 42; return x; }", SourceLanguage.C);
if (outcome.Success)
{
    var ram = outcome.Device!.GetRamAsNewArray(0, 256);
}
```

**Checking untrusted code before running:**

```csharp
using Nantuc.VmApplication.Extensions.Bytecode;

var comp = host.Project.Compile("int main() { return 0; }", 0, optimize: false);
if (!comp.Success) { /* … */ }

switch (comp.GetOpCodeSafety())
{
    case OpCodeSafety.Safe:
        // all memory accesses are *_UNSAFE, potentially unsafe
        break;
    case OpCodeSafety.HasUnsafe:
        foreach (var loc in comp.GetUnsafeOpCodes())
            Console.Error.WriteLine($"unsafe {loc.OpCode} at 0x{loc.Ip:X} (offset {loc.Offset})");
        break;
    case OpCodeSafety.Unknown:
        Console.Error.WriteLine("program is truncated or not compiled");
        break;
}
```

> `Safe` means "no `*_UNSAFE` opcodes", not "safe". Ordinary `LOAD`/`STORE` can also access arbitrary addresses — that is only influenced by `SizeRam` in `ReplaceSafeMemAccessWithUnsafeRule` and the `Device` bounds. `HasUnsafe`, conversely, means the code **definitely** contains unsafe accesses — their presence must either be accepted (if the code is trusted) or forbidden (if the code is untrusted).

**Verifying the result of `ReplaceSafeMemAccessWithUnsafeRule`:**

```csharp
host.Project.Optimizator.VMIROptimizer.AddStdRules();
// … build IR with IR …
var comp = il.Build();

if (comp.GetOpCodeSafety() != OpCodeSafety.Safe)
    foreach (var loc in comp.GetUnsafeOpCodes())
        Console.Error.WriteLine($"missed at 0x{loc.Ip:X}: {loc.OpCode}");
```

## Closeness of the module to the user

Medium-high. This is an optional layer on top of the main API. If you just want to "run and look" — connect `Nantuc.VmApplication.Extensions` and use `RunAndKeep`/`RunOnce`/`AddStdRules`. If you are writing an IDE or a debugger — use `Debugger`/`Disassembler`/`StringDisassembler`/`InstructionFormatter`. If you are writing a game — `SynchronousIOStream`/`QueuedIOStream` for UART.

**What you should do:**
- Connect `AddStdRules()` in `VmHostFactory.CreateDefault`-style scenarios to compile with optimizations.
- Use `QueuedIOStream` for non-blocking output (game loop), `SynchronousIOStream` — for simple cases.
- For debugging — `DisassembleToString()` (text) or `Disassemble()` (structure).
- To save RAM — `GetRamAsNewArray` (snapshot) and `CopyRamTo` (into a prepared `Span<byte>`).
- Set `ReplaceSafeMemAccessWithUnsafeRule.SizeRam` to the actual RAM size of `Device`.
- `SaveFileProject`/`SaveFileInclude` — with a check that `localPath` does not contain `..`.
- For your own optimization rules — assign an `OptimizationId` via `WithIRPrefix()` / `WithRule()` / `WithASTNodePrefix()` so the prefix matches the built-in ones.
- For debugging — `Debugger` on top of `DeviceContext`: `dbg.Load(program)` → `dbg.Step()`/`StepOver()`/`StepOut()`/`Continue()`, `dbg.AddBreakpoint(addr)`, `dbg.CurrentInstruction`, `dbg.GetCallStack()`. To stop a long run — `dbg.RequestStop()` from another thread.
- For IDE debugging — subscribe to `dbg.Stopped` to refresh the registers/stack/RAM panel on every stop.
- For "run to the end" (e.g. a CI run) — `dbg.RunToEnd(stepLimit: N)`, not `Continue` (the latter will get stuck at the first breakpoint).
- For typical integer operations and overflow checks — `imath.minic`, `bitops.minic`, `ovf.minic` instead of hand-written implementations.
- For untrusted code — check `comp.GetOpCodeSafety()` and/or enumerate `comp.GetUnsafeOpCodes()` before loading the VM; this is a cheap static check (`*_UNSAFE` opcodes), not a replacement for a sandbox.
- To output from the VM to the console/file without manually registering a UART — `host.RunAndKeep(source, lang, output: writer)` (UART will be registered at `sector + 1`, default encoding — `Utf16`).

**What you should not do:**
- Call `RunAndKeep` multiple times on the same `VmHost` with the same `sector` — the sector will be taken. Use `sector:` explicitly or `RunOnce`.
- Call `RunAndKeep` multiple times on the same `VmHost` with `output != null` without `UnregisterDevice` — the `sector + 1` sector will also be taken by the UART.
- Use `RunAndKeep` for untrusted code — it registers the VM on the given sector without a sandbox.
- Rely on `AddStdRules` for IR — `ReplaceSafeMemAccessWithUnsafeRule` is **not** added automatically.
- Expect `RunOutcome.Success` to reflect only the fact of launch — `Success == (fault.Status == BiosStatus.EndProgram)`, i.e. the program actually reached `END`. Runtime faults are visible both in `Success` and in `FaultInfo`.
- Use `Libraries.alloc_minic` as a "reference" — it still uses globals as legacy (the workaround is no longer needed, but the code has not been simplified yet), and there is a bug in `free` (see [What to fix](#what-to-fix), item 14).
- Call `Step`/`Continue`/`Load` from different threads on the same `Debugger` — it is not thread-safe (except `RequestStop`). The model is the same as for `DeviceContext`.
- Expect an exact call stack from `GetCallStack()` — the VM does not keep a frame pointer, reconstruction is heuristic; false positives are possible.
- Rely on `stop.Snapshot.IsSleeping` in `Continue` — the sleep flag only comes from `_stepSnapshot` (after `SingleStep`); when stopping via `RequestStop` it may be `false` even if the VM is in `HALT`. For `HALT`, `stop.Reason == Sleeping` is more reliable.
- Use a single `Debugger` for several `Load`s — after the program finishes (or after a manual `Dispose`), create a new instance. A repeated `Load` throws `InvalidOperationException` (protection against leaking step mode).
- Expect `Debugger.ReadRam` to guard against `IsRunning` (like `DeviceContextExtensions.GetRamAsNewArray`) — the debugger reads RAM during a stop; if you call `ReadRam` from another thread during `Continue`, the data will be race-y.
- Assume `GetOpCodeSafety() == Safe` is a memory-safety guarantee. `Safe` is "no `*_UNSAFE`", not "no arbitrary addresses": ordinary `LOAD`/`STORE` also write to an address taken from a register. For real protection — a combination of `ReplaceSafeMemAccessWithUnsafeRule`, `SizeRam` = actual RAM size, and `Device`/`MemoryBus` bounds. `GetOpCodeSafety` is a signal "are there `*_UNSAFE`s", not "is the code safe".

## Special notes

### Double pass over the data section

Both `Disassembler.Disassemble` and `StringDisassembler.Disassemble` split the stream into code and data by `codeSize`. If `codeSize == 0`, the whole stream is data. If `codeSize == size`, there is no data section. If the last instruction with `data64` extends beyond `codeSize` (padding + 8 bytes), `pos` after the loop will be `> codeSize`, and the data loop starts from `pos`, not from `codeSize`. The first `pos - codeSize` bytes of the data section are actually the tail of the last instruction and will not be shown as data. `bytesConsumed` in `DisassemblyResult` is still equal to `pos`.

### Debugger: `_skipBreakpointOnce` and repeated `Continue`

`Continue` at start checks two conditions:
1. `_skipBreakpointOnce == rIP` → clears the flag and does **not** stop at the current address (this is the same breakpoint that fired last time).
2. `HasBreakpoint(rIP)` → returns `DebugStopReason.Breakpoint` without stepping.

`StepOneOrFinish` after each `SingleStep` sets `_skipBreakpointOnce = newIp` if there is a breakpoint at the new `rIP`. This gives correct semantics:

```
Load → Continue (hit bp at 0x00) → Continue (skip 0x00, continues)
```

Without `_skipBreakpointOnce`, `Continue` → breakpoint → `Continue` would spin forever at the same `rIP`.

`ContinueTo(addr)` uses the same logic: adds `addr` to `_breakpoints`, calls `Continue`, removes the breakpoint. If `addr` was already a user breakpoint — it is preserved after the call.

### Debugger: `ProcessorSnapshot` in `DebugStop`

In `Step`/`StepOver`/`StepOut`/`Continue` the `ProcessorSnapshot` is taken from `_stepSnapshot` (`SingleStep(onStep)` callback), so:
- `IsSleeping` is correct (the VM sets `_isSleeping` before invoking the callback);
- `IsRunning` is correct (the VM clears it before invoking the callback);
- `RawInstruction` — the word at the **current** `rIP` (after the step).

In the `Continue` branch where the stop occurs before a step (initial-IP check for a breakpoint), `Snapshot` is built via `BuildSnapshotFromRegisters` — there `IsSleeping == false` always, because `DeviceContext` does not expose the sleep flag. For `HALT` detection, `stop.Reason == Sleeping` is more reliable (this only comes from `_stepSnapshot`).

### Debugger: `GetCallStack` — heuristic, not an exact stack

The VM does not keep a frame pointer and does not maintain a separate call stack. `GetCallStack` scans `[rSP, RamSize)` for 8-byte values in the range `[ProgramStart, ProgramEnd)`, aligned to 4. This finds **return addresses** pushed by `CALL` (see [`bytecode.md` §5.6](../docs/bytecode.md#56-управление-потоком)), but:

- **False positives** — local `ulong`s whose values happen to fall in the code range.
- **Misses** — if the `CALL` was indirect (`CALL_IND`), the return address is also on the stack; but if the program uses its own manual call stack (like `print_minic`/`alloc_minic`), `rSP` may point elsewhere.
- **Order** — `frames[0]` is the topmost (closest to the current `rSP`), `frames[N]` is the deepest.

An exact stack requires a frame pointer (which the VM does not have) or a separate call stack in `Processor` (not implemented). In the debugger — best-effort only.

### Debugger: `StepOver` and the return address

For `CALL` (immediate) the instruction length is 12 bytes (4 + 4 padding + 8 immediate), so the return address is `AlignUp(ip + 4, 8) + 8`. For `CALL_IND` (indirect) the length is 4 bytes, the return address is `ip + 4`. This matches §3.2 and §5.6 of [`bytecode.md`](../docs/bytecode.md). An error here would cause "falling into" the called function.

### `Libraries.alloc_minic`

Block layout:
```
+0..7    : size | FREE_BIT(bit 0)       — header
+8..15   : next_free (only for free)    — or the start of the payload
+16..sz-8: continuation of the payload / unused
+sz-8..sz: size | FREE_BIT(bit 0)       — footer (for merging left)
```
`payload` starts at `+8`. The minimum block is 24 bytes. `HDR_FTR = 16` (8 hdr + 8 ftr) is added to the aligned `payload` in `alloc`: `need = ((size + 7) & ~7) + HDR_FTR`, but not less than `MIN_BLOCK`. On free: `blkMark(b, size, 1)` → `mergeRight(b)` → `mergeLeft(b)` → `blkPush(b)`. On alloc: first-fit over `heapFreeList`, if the size is sufficient — `split` (if `csz - need >= MIN_BLOCK`), otherwise `blkMark(curr, csz, 0)` (no split); otherwise bump via `GetSP()`.

**Known bug in the current version:** at the start of `free` there is `if (blkFree(b)) return;` **before** the declaration `ulong b = ptr - 8;` — a reference to an undeclared variable. See [What to fix](#what-to-fix), item 14.

**Historical note:** earlier `alloc_minic` kept all state in globals because of the `FunctionGenerator.GenerateFunction` bug in the compiler (asymmetric push/pop of callee-saved registers with 3-4 locals). The bug is fixed — the workaround is no longer needed, but the current code has not yet been migrated to locals. Details in [STDLIBRARIES.md](STDLIBRARIES.md).

### `Libraries.imath_minic`, `Libraries.bitops_minic`, `Libraries.ovf_minic`

Three independent libraries on top of standard MiniC operators — no `#include` and no assembly wrappers:

- `imath_minic` — `imin`/`imax`/`umin`/`umax`, `iclamp`/`uclamp`, `iabs`, `igcd`/`ugcd`, `ipow`, `uisqrt`, `idiv_floor`/`idiv_ceil`/`udiv_round_up`.
- `bitops_minic` — `upopcount` (Kernighan), `uclz`/`uctz` (`0..64`, `64` for zero), `rotl64`/`rotr64` (`n & 63`), `urev64`, `unext_pow2` (`0` — overflow for `x > 2^63`).
- `ovf_minic` — `add_ovf_i32`/`sub_ovf_i32`/`mul_ovf_i32` (for signed 32-bit; `INT_MAX = 2147483647`, `INT_MIN = -2147483647 - 1`), `add_ovf_u64`/`sub_ovf_u64`/`mul_ovf_u64` (for unsigned 64-bit). All return `1` on overflow and `0` otherwise.

Details in [STDLIBRARIES.md](STDLIBRARIES.md).

### `HostExtensions` and `sector`

`RunAndKeep`/`RunOnce`/`RunAndCleanup` take `sector` (default `0`). If `sector` is taken by another device, `TryRegisterDevice` returns a non-`Valid` `IdState`, and `RunOutcome` will have `Success == false` with the diagnostic `"register vm: {state}"` in `Diagnostics` — no NRE anymore (the safe API is used). If `output != null` is passed, the UART is registered at `sector + 1`; on UART registration failure the VM is rolled back via `UnregisterDevice` and `RunOutcome` is returned with `Success == false`, `Device == null`, diagnostic `"register uart: {state}"`. `RunOnce` and `RunAndCleanup` release **only the VM** (`UnregisterDevice(vmId)`), the UART remains registered — a potential leak if `output != null` and `RunOnce`/`RunAndCleanup` are called repeatedly with the same `sector`. See [What to fix](#what-to-fix), item 16.

### `RunOutcome.FaultInfo` and `Success`

`FaultInfo` is the `ProcessorFault` from `ctx.Run(new LaunchOptions(StartAddress: 0))`. `Success` in `RunAndKeep` is `fault.Status == BiosStatus.EndProgram`, not "the fact of launch" and not "compilation success". On compilation/registration/load failure `RunOutcome.Success == false`, `Device == null`, `FaultInfo == default(ProcessorFault)`, and the reason is in `Diagnostics`. Important: `_faultData` in `Processor` is not reset in `LaunchProgramm` — a second `Run` on the same `Device` may return a fault from the first one if the first one crashed.

### `BytecodeExtension`: truncated program and `fullyScanned`

`GetOpCodeSafety`/`GetUnsafeOpCodes` iterate over `Program` up to `actualCodeSize = CodeSize ?? Program.Length`, but the **bounds** are checked against `Program.Length` (not `actualCodeSize`):
- `pos + 4 > Program.Length` → parsing stops (for `GetOpCodeSafety` — `Unknown`, for `GetUnsafeOpCodes` — `fullyScanned = false`);
- for opcodes with `HasNeed64IntData`: `nextPos + 8 > Program.Length` → same.

Reason: `Program` is a `byte[]`, and truncation means either `Program` is broken or `CodeSize` does not match `Program`. In the first case `actualCodeSize > size` is already filtered out as `Unknown`. In the second — `CodeSize < Program.Length` (normal: the tail of `Program` may contain a data section; `BytecodeExtension` does not scan it because `actualCodeSize` limits the loop, but padding/truncation at the boundary is still checked against `Program.Length`).

`UnsafeOpCodeLocation.Offset` is the offset in `Program` (the `CompilationResult.Program` byte array), **not** in the code section. For a `Program` that matches the code section (`CodeSize == Program.Length`), `Offset` matches the address relative to the start of the program.

### Platform limitations

- `net10.0`, `<AllowUnsafeBlocks>` is **not** enabled.
- `Thread.Sleep(1)` in `QueuedIOStream.Flush` — in WASM either a no-op or `PlatformNotSupportedException`. For WASM use `SynchronousIOStream`.
- `Channel<byte>` and `Task.Run(DrainAsync)` — work in WASM, but background tasks may not run until an explicit `await`.
- `ImmutableArray<T>` — works in AOT/WASM, but each mutation through `CreateBuilder` allocates. For large programs (10k+ instructions) `DisassemblyResult` can take a lot of memory. For streaming — `StringDisassembler.Disassemble`.
- `Guid.NewGuid().GetHashCode()` in `InlineSmallVoidFunctionsRule` — stable within a process, but not across processes. For deterministic builds — a problem.
- `Debugger` — `SingleStep` works in WASM (one instruction — synchronously), but `Continue`/`StepOver`/`StepOut`/`GetCallStack` are synchronous loops that block the current thread. In browser-WASM (single-threaded) — UI blocking. For an in-browser IDE — move to a Web Worker or use `Task.Run` with `RequestStop`.

## What to fix

6. **[API] `RunAndCleanup` and `RunOnce` do not let you choose `deviceName` and do not accept `output`/`encoding`.**  
   File: `Host/HostExtensions.cs`. `RunAndKeep` takes `deviceName`, `output`, `encoding`; `RunAndCleanup`/`RunOnce` do not (always `"run"`, no UART). This is asymmetric and gets in the way during debugging and when running with output.  
   Fix: add the same parameters to `RunAndCleanup`/`RunOnce` and pass them through to `RunAndKeep`.

7. **[API/XML] `SaveFileInclude`/`SaveFileProject` — path traversal.**  
   File: `Host/ProjectExtension.cs`. `localPath` is not checked for `..` and absolute paths.  
   Fix: validate `localPath` (`Path.IsPathRooted` + absence of `..` in segments) or document that the call is trusted input.

8. **[XML] `ReplaceSafeMemAccessWithUnsafeRule.SizeRam` — default is underestimated.**  
   File: `Optimizators/IRAsmRules/ReplaceSafeMemAccessWithUnsafeRule.cs`. `RamSize.B128` is not a "safe" default but a minimum; the rule silently no-ops if the user does not set the size.  
   Fix: either default to the `RamSize` from `Device` (but the rule does not know about `Device`), or add a `<remarks>` with an explicit "you must set `SizeRam` to the device RAM size, otherwise the rule is a no-op".

9. **[API] `AddStdRules` for AST contains `RemoveUnreachableCodeRule` twice.**  
   File: `Optimizators/AstOptimizerExtension.cs`. The duplication is intentional (after inlining), but non-obvious.  
   Fix: a `<remarks>` on `AddStdRules` explaining why the second pass is needed, or extract it into a separate composite method `AddStdRulesWithCleanup()`.

10. **[API] `Debugger` — one-shot per `Load`.**  
    File: `Debugging/Debugger.cs`. A repeated `Load` throws `InvalidOperationException` so that step mode does not leak (`ctx.RunStepMode` without `EndStepMode`).  
    Fix: either `Reset()` (clears step mode, clears breakpoints, but keeps `Context`), or an explicit `Unload()`/`Reload(program)`. For now — a new instance per run.

11. **[API] `Debugger.ReadRam` is not guarded against `IsRunning`.**  
    File: `Debugging/Debugger.cs`. Unlike `DeviceContextExtensions.GetRamAsNewArray`/`CopyRamTo` (which throw `InvalidOperationException` when `ctx.IsRunning`), the debugger reads RAM **during a stop** — that is its normal scenario. But if the user calls `ReadRam` from another thread during `Continue`, the data will be race-y.  
    Fix: either check `_isStepping` (a flag "a `SingleStep` is in progress"), or document it in `<remarks>` (currently — only in the README).

12. **[API/XML] `Debugger.StepOut` — a heuristic on `rSP`.**  
    File: `Debugging/Debugger.cs`. The semantics "`rSP` became larger than it was" breaks if the function uses `PUSH`/`POP` for temporaries (typical for `print_minic`/`alloc_minic`). In such cases `StepOut` may leave the function earlier than expected.  
    Fix: either document the limitation, or add a "call depth" via `GetCallStack` (frame count) instead of `rSP` — but that is more expensive.

13. **[API] `Debugger.GetCallStack` — best-effort, false positives.**  
    File: `Debugging/Debugger.cs`. The VM does not keep a frame pointer; the stack is reconstructed by scanning `[rSP, RamSize)` for 8-byte values in the code range. Local `ulong`s that happen to fall in the range will produce false frames.  
    Fix: either a `/// <remarks>` with an explicit warning (currently — in the README), or implement a frame pointer in `Processor` (a breaking change for bytecode).

14. **[API] `alloc_minic.free` — reference to `b` before its declaration.**  
    File: `StdLibraries/Libraries.cs`. At the start of `free(ulong ptr)` there is `if (blkFree(b)) return;`, but `b` is declared below (`ulong b = ptr - 8;`). This is a bug — a reference to an undeclared variable.  
    Fix: remove the extra line `if (blkFree(b)) return;` (double free is not supported anyway, and the `if (ptr == 0) return;` check is already there), or move it **after** the declaration of `b`: `ulong b = ptr - 8; if (blkFree(b)) return;`.

15. **[API/semantics] `RemoveSwapMovPairRule` removes `MOV rA, rB; MOV rB, rA`, assuming "net effect is zero".**  
    File: `Optimizators/IRAsmRules/RemoveSwapMovPairRule.cs`. With the typical `MOV dst, src` semantics (`dst = src`) this pattern is **not** a no-op: after `MOV A, B` we have `A = old B`, after `MOV B, A` — `B = old B`; the net result is `A = old B, B = old B`. The rule is correct only if `OpCode.MOV` in the VM has swap semantics (extremely unusual) or if the author of the rule relied on a different convention.  
    Fix: (a) clarify the `OpCode.MOV` semantics in `docs/bytecode.md`; (b) either remove the rule from `AddStdRules` or rewrite it for swap (`XCHG`/`SWAP`) if such an opcode exists; (c) at a minimum — add a `<remarks>` with an explicit warning.

16. **[API] `RunOnce`/`RunAndCleanup` do not release the UART registered at `sector + 1`.**  
    File: `Host/HostExtensions.cs`. With `output != null`, `RunAndKeep` registers `QueuedIOStream` at `sector + 1`; `RunOnce`/`RunAndCleanup` release only the VM (`UnregisterDevice(vmId)`), but not the UART. A repeated call with the same `sector` and `output != null` will fail on UART registration.  
    Fix: `RunAndKeep` should return `UartDeviceId` in `RunOutcome` (or in a separate out parameter), and `RunOnce`/`RunAndCleanup` should release it too. At a minimum — document that with `output != null`, `sector + 1` is also taken.