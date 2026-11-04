# Nantuc.VmApplication.Extensions — README

> Extensions to the main API: disassembler, debugger, I/O devices, standard optimizations (AST and peephole), one-line compilation utilities, built-in MiniC/NANASM libraries.
> Depends only on `[Nantuc.VmApplication](../../src/Nantuc.VmApplication/README.md)` (transitively — on `Compiler`, `Kernel`, `Contracts`, `Common`).
> `[← Root](../README.md) · [VmApplication](../../src/Nantuc.VmApplication/README.md) · [Compiler](../../src/Nantuc.Compiler/README.md) · [Kernel](../../src/Nantuc.Kernel/README.md) · [Common](../../src/Nantuc.Kernel.Common/README.md) · [Contracts](../../src/Nantuc.Kernel.Contracts/README.md) · [Diagnostics](../../src/Nantuc.Kernel.Diagnostics/README.md) · [STDLIBRARIES](STDLIBRARIES.md)`

## Table of contents
- [About this module](#about-this-module)
- [Local project tree](#local-project-tree)
- [API](#api)
- [API examples](#api-examples)
- [How close the module is to the user](#how-close-the-module-is-to-the-user)
- [What was fixed in this version](#what-was-fixed-in-this-version)
- [Special information](#special-information)
- [What to fix](#what-to-fix)

## About this module

`Nantuc.VmApplication.Extensions` provides the "batteries" for the main API. There are no new kernel abstractions here, only extensions on top of existing ones: `DeviceContext`, `VmHost`, `VmHostProject`, `VmEmulator`, `IPortController`. The module is optional — the main API works without it. But with it you get:

- **Disassembler** (`Disassembler`/`StringDisassembler`/`InstructionFormatter`): parses `CompilationResult.Program` into `DecodedInstruction[]` + `DecodedDataChunk[]` and a textual form (like `objdump`).
- **Debugger** (`Debugger`): step-by-step execution on top of `DeviceContext` — `Step`/`StepOver`/`StepOut`/`Continue`/`ContinueTo`/`RunToEnd`, breakpoints, best-effort call stack, RAM reading (`ReadUInt16/32/64`, `ReadCString`), disassembling the current instruction via `StringDisassembler`. Does not use `AsRawDevice` or `unsafe` — only the public API of `DeviceContext`/`ProcessorSnapshot`.
- **I/O devices** (`NumericIOStream`, `SynchronousIOStream`, `QueuedIOStream`): output bytes/UTF-8/UTF-16 from the VM to a `TextWriter`. Plus `PortCharEncoding` and `OutputPortLayout` (8-byte window layout).
- **Standard optimizations**: AST rules (`FoldConstants`, `PropagateConstants`, `RemoveUnusedVariables`, `RemoveUnreachableCode`, `InlineSmallVoidFunctions`) and peephole rules (`ConstantFolding`, `RemoveNoopMov`, `RemovePushPopPair`, `RemoveSwapMovPair`, `RemoveJumpToNextLabel`, `RemoveMultiEnd`).
- **Compilation utilities** (`ProjectExtension`, `VMHostRunExtensions`): `host.Project.CompileToIL("int main(){}", 0, true, SourceLanguage.C)` and `host.RunAndKeep(...)`, `host.RunOnce(...)`, `host.RunAndCleanup(...)` — one line instead of a pipeline.
- **Built-in libraries** (`Libraries`): string constants `heap_nanasm`, `alloc_minic`, `io_nanasm`, `print_minic` for `#include`. More details in [STDLIBRARIES.md](STDLIBRARIES.md).
- **RAM utilities** (`DeviceContextExtensions`): `GetRamAsNewArray`, `CopyRamTo` — read RAM of a running/stopped VM.

The key idea is not to change the contract of `Nantuc.VmApplication` but to extend it. All rules implement `IAstOptimizationRule`/`IPeepholeRule` from the compiler, all I/O devices inherit from `PortControlBase` from `VmApplication`, and the debugger works through `DeviceContext.RunStepMode`/`SingleStep`/`GetRegistersSnapshot`/`NativeReadOnlyBuffer`. No new interfaces in `Kernel.Contracts`.

## Local project tree

Namespaces correspond to folders:

```
Nantuc.VmApplication.Extensions/
├── Nantuc.VmApplication.Extensions.csproj   # net10.0, ref → VmApplication
├── Compiler/
│   └── CompilerExtension.cs                  # extension on IReadOnlyDictionary<ErrorCode,string> (ValidateAllCodesCovered / TryValidateAllCodesCovered) — see "What to fix", item 3
├── Debugging/
│   ├── Debugger.cs                           # step-by-step debugger over DeviceContext: Step/StepOver/StepOut/Continue/ContinueTo/RunToEnd/breakpoints/RAM/disassembler/stack
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
│   ├── PortBusExtensions.cs                  # extension(PortBus).GetBaseAddress(uint sector)
│   ├── PortCharEncoding.cs                   # enum: BytePerChar | Utf8 | Utf16
│   ├── QueuedIOStream.cs                     # async: Channel<byte> + background drain
│   └── SynchronousIOStream.cs                # synchronous: writes characters
├── Host/
│   ├── HostExtensions.cs                     # RunOutcome + RunAndKeep / RunAndCleanup / RunOnce
│   └── ProjectExtension.cs                   # Compile/CompileToIL from string/strings + SaveFileInclude/SaveFileProject + LoggingFaultPolicyMinimal
├── StdLibraries/
│   └── Libraries.cs                # static class Libraries: heap_nanasm, alloc_minic, io_nanasm, print_minic
└── Optimizators/
    ├── AstOptimizerExtension.cs              # extension(VmAstOptimizer).AddStdRules() + WithASTNodePrefix + OptimizationId constants
    ├── PeepholeOptimizerExtension.cs         # extension(VmPeepholeOptimizer).AddStdRules() + WithPeepholePrefix / WithRule + OptimizationId constants + PeepholeLog
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
- `Librares` (folder), `Librare` (legacy name in docs), and `StdLibraries` (namespace) are three different typos of the word `Libraries`. The file is named `Libraries.cs`, the class is `Libraries`. The public class name is correct, but the namespace/folder is not; see [What to fix](#what-to-fix).
- `ProjectExtension.cs` contains `LoggingFaultPolicyMinimal` — a class that is not about project extensions but about fault policies. It would be more logical to move it to `Host/`.
- `AstOptimizerExtension.cs` and `PeepholeOptimizerExtension.cs` contain not only extension methods but also `OptimizationId` constants. They are part of the public contract: you look up logs in `OptimizationResultLog` by these IDs.
- `Compiler/CompilerExtension.cs` — an extension on `IReadOnlyDictionary<ErrorCode, string>` (`ValidateAllCodesCovered`/`TryValidateAllCodesCovered`). According to changelog 0.7.2 these methods are marked as removed; the file is still present in the code — see [What to fix](#what-to-fix), item 3.
- `Debugging/` — a new layer on top of `DeviceContext`, does not change `Nantuc.VmApplication`. The debugger is not thread-safe (except `RequestStop`), which matches the `DeviceContext` model. The call stack is reconstructed heuristically — the VM does not store a frame pointer. Details in [Special information](#special-information).

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
    None = 0,          // debugger has not stopped yet
    StepComplete,      // one instruction executed (Step/StepOver/StepOut)
    Breakpoint,        // breakpoint hit
    Completed,         // program executed END
    Faulted,           // BiosStatus != EndProgramm and != Success
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
    public void Wake();                  // wake from HALT (OpCode.WAKE)

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

- **`Load`** — `ctx.LoadProgram` + `ctx.RunStepMode(startAddress)`; after the call `IsActive == true`, but no instruction has been executed yet. `Debugger` is single-use per `Load` — a repeated call throws `InvalidOperationException` (protection against leaking step mode; for a new run — a new `Debugger`).
- **`Step`** — one instruction via `ctx.SingleStep(onStep)`; the result is a `DebugStop` with a processor snapshot from the callback (`ProcessorSnapshot` from the VM — with correct `IsSleeping`).
- **`StepOver`** — if the current instruction is `CALL`/`CALL_IND`, it runs until the return address (`ip + 4` for `CALL_IND`, `AlignUp(ip + 4, 8) + 8` for `CALL`), otherwise behaves like `Step`.
- **`StepOut`** — runs `Step` until `rSP` becomes greater than the value at the time of the call. Best-effort: if the function uses `PUSH`/`POP` for temporary values, the semantics break — see [What to fix](#what-to-fix), item 12.
- **`Continue`** — runs `SingleStep` until `Breakpoint`/`Completed`/`Faulted`/`Sleeping`/`StepLimit`/`UserStop`. At the start it checks `_skipBreakpointOnce` (see [Special information](#special-information)) so that `Continue` → breakpoint → `Continue` does not loop.
- **`ContinueTo(addr)`** — temporarily adds a breakpoint at `addr`, calls `Continue`, removes the breakpoint. If there was already a user breakpoint at `addr` — it is preserved.
- **`RunToEnd`** — `Continue` with a temporarily cleared set of breakpoints (for "run to the end, ignoring points").
- **`RequestStop`** — the only method safe from another thread: `volatile bool _stopRequested`. The `Continue`/`StepOver`/`StepOut` loops check it on each iteration.
- **`Wake`** — `ctx.Resume()`; queues `OpCode.WAKE`. Needed if the debugger stopped at `HALT` and you want to continue without `Stop`/re-`Load`.
- **`ReadRam`/`CopyRamTo`** — via `ctx.NativeReadOnlyBuffer`; throw `ArgumentOutOfRangeException` when out of bounds and `ObjectDisposedException` after `Dispose`. Unlike `DeviceContextExtensions.GetRamAsNewArray`, they do **not** throw `InvalidOperationException` when `IsRunning` — the debugger usually reads RAM exactly during a stop.
- **`ReadUInt16/32/64`** — little-endian via `BinaryPrimitives` (matches the `MemoryBus` contract).
- **`ReadCString`** — reads bytes until `0`; `maxLength` (default 1024) protects against reading the whole RAM if the string is not terminated.
- **`CurrentInstruction`/`DisassembleAt`/`DisassembleProgram`** — use `StringDisassembler.Disassemble`; format is like `objdump`. `DisassembleProgram` disassembles the range `[startAddress, loadAddress + program.Length)` fixed in `Load`.
- **`GetCallStack`** — best-effort: scans the stack (`[rSP, RamSize)`) for 8-byte values in the range `[ProgramStart, ProgramEnd)`, aligned to 4. False positives are possible (values of local variables). `maxDepth` limits the depth (default 16). See [Special information](#special-information).
- **`Stopped`** — an event that fires after each `DebugStop` (including the final one). Convenient for logging/visualization.

### I/O devices

**`PortCharEncoding`**: `enum { BytePerChar, Utf8, Utf16 }`.

**`OutputPortLayout`** — 8-byte window layout:

```csharp
public const ulong WindowSize = 8;
public const ulong Data   = 0;   // write: byte to output; read: 0
public const ulong Status = 1;   // 0 — ready, 1 — there is unprocessed data
public const ulong Flush  = 2;   // write 1 — flush the buffer
```

**`SynchronousIOStream : PortControlBase`**:

```csharp
public SynchronousIOStream(TextWriter writer, PortCharEncoding encoding);
```

`WritePort(Data, value)` → `_writer.Write(...)` + `Flush()`. `WritePort(Flush, _)` → `_writer.Flush()`. `ReadPort(_)` → 0. `WakeProcessor()` → `_writer.Flush()`.

**`NumericIOStream : PortControlBase`** — synchronous, writes **numeric character codes**:

```csharp
public NumericIOStream(TextWriter writer, PortCharEncoding encoding, bool hex = false);
```

`BytePerChar` → `EmitNumber(value)` (0..255). `Utf8`/`Utf16` → `EmitNumber(codePoint)`. Format: `"42 "` or `"0x2A "`.

**`QueuedIOStream : PortControlBase`** — asynchronous via `Channel<byte>`:

```csharp
public QueuedIOStream(TextWriter writer, PortCharEncoding encoding);
```

`WritePort(Data, value)` → `Channel.Writer.TryWrite(value)` + `Interlocked.Increment`. `ReadPort(Status)` → `1` if pending, `0` otherwise. `WritePort(Flush, _)` → polling up to 2 sec. `Dispose` → `Complete` + `_drainTask.Wait(5s)`.

**`PortBusExtensions`**:

```csharp
extension(PortBus bus)
{
    public ulong GetBaseAddress(uint sector) => (ulong)sector * bus.PortsOnDevice;
}
```

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
1. `PropagateConstantsRule` — `IdentifierNode` → `NumberNode` if the variable is declared with a constant and is not reassigned.
2. `FoldConstantsRule` — `Number op Number` → `Number`.
3. `RemoveUnusedVariablesRule` — removes `VariableNode` without reads (only if the initializer is side-effect-free).
4. `RemoveUnreachableCodeRule` — removes code after `return` and `if(false)`/`while(false)`.
5. `InlineSmallVoidFunctionsRule` — inlines `void` functions ≤ 10 statements, without recursion.
6. `RemoveUnreachableCodeRule` — once more (after inlining). See [What to fix](#what-to-fix).

**`PeepholeOptimizerExtension`**:

```csharp
extension(VmPeepholeOptimizer peephole)
{
    public VmPeepholeOptimizer AddStdRules();
}

extension(OptimizationId id)
{
    public OptimizationId WithPeepholePrefix();    // prefix "peephole."
    public OptimizationId WithRule();              // suffix "-rule"
}

public static readonly OptimizationId ConstantFolding;          // peephole.constant-folding
public static readonly OptimizationId RemoveJumpToNextLabel;    // peephole.remove-jump-to-next-label
public static readonly OptimizationId RemoveMultiEnd;           // peephole.remove-multi-end
public static readonly OptimizationId RemoveNoopMove;           // peephole.remove-noop-mov
public static readonly OptimizationId RemovePushPop;            // peephole.remove-push-pop
public static readonly OptimizationId RemoveSwapMov;            // peephole.remove-swap-mov
public static readonly OptimizationId ReplaceSafeAccess;        // peephole.replace-safe-mem-acces-with-unsafe
```

Standard peephole rules (`AddStdRules`):
1. `RemoveNoopMovRule` — `MOV rX, rX` (not immediately after a label).
2. `RemovePushPopPairRule` — `PUSH rX; POP rX`.
3. `RemoveSwapMovPairRule` — `MOV rA, rB; MOV rB, rA` (net effect zero).
4. `RemoveJumpToNextLabelRule` — `JMP label; label:` → remove `JMP`.
5. `ConstantFoldingRule` — `LDI rA, X; LDI rB, Y; OP rA, rB` → `LDI rA, X op Y` (no labels in between).
6. `RemoveMultiEndPairRule` — `END; END` → one `END`.

**`ReplaceSafeMemAccessWithUnsafeRule`** — **not** included in `AddStdRules`, added manually:

```csharp
public sealed class ReplaceSafeMemAccessWithUnsafeRule : IPeepholeRule
{
    public RamSize SizeRam { get; set; } = RamSize.B128;   // default
}
```

Replaces `LOAD`/`STORE`/`LOAD_IND`/`STORE_IND` with `*_UNSAFE` variants if the address is known and within `SizeRam`. Default `RamSize.B128` (128 bytes) is **underestimated**; the rule will only trigger on the smallest addresses if `SizeRam` is not set to the actual RAM size of the `Device`. See [What to fix](#what-to-fix).

**`PeepholeLog`**:

```csharp
public class PeepholeLog(OptimizationId id) : ILogOptimization
{
    public OptimizationId Id => id;                  // no additional prefix
    public void Append(int passCount, AsmItem item); // formats AsmItem to a string
    public void Append(int pass, string message);
    public void Clear();
    public IReadOnlyList<OptimizationLogEntry> GetLogs(); // returns the log, does NOT clear
}
```

### Compilation and run utilities

**`ProjectExtension`**:

```csharp
extension(VmHostProject proj)
{
    public CompilationToILResult CompileToIL(ulong baseAddress, bool optimize);
    public CompilationResult    Compile(ulong baseAddress, bool optimize);

    public CompilationToILResult CompileToIL(string source, ulong baseAddress, bool optimize, SourceLanguage language);
    public CompilationToILResult CompileToIL(IEnumerable<string> sources, ulong baseAddress, bool optimize, SourceLanguage language);
    public CompilationResult    Compile(string source, ulong baseAddress, bool optimize, SourceLanguage language);
    public CompilationResult    Compile(IEnumerable<string> sources, ulong baseAddress, bool optimize, SourceLanguage language);

    public void SaveFileInclude(string localPath, string content);
    public void SaveFileProject(string localPath, string content);
}
```

`SaveFileInclude`/`SaveFileProject` use `proj.FileService.Combine(paths.IncludePath/ProjectPath, localPath)` and `SaveFile`. No check for `..` in `localPath` — see [What to fix](#what-to-fix).

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
        string deviceName = "run", uint sector = 0);

    public bool RunAndCleanup(
        string source, SourceLanguage lang,
        RamSize? ram = null, bool optimize = false, uint sector = 0);

    public RunOutcome RunOnce(
        string source, SourceLanguage lang,
        RamSize? ram = null, bool optimize = false, uint sector = 0);
}
```

- `RunAndKeep` — compiles `source`, creates a `DeviceSpec` (`RamSize.MB16` by default), registers on `sector`, loads, runs. Returns a `RunOutcome` with a live `Device` and the `ProcessorFault` from `ctx.Run(...)`. `sector` is a parameter.
- `RunAndCleanup` — `RunAndKeep` + `UnregisterDevice`. Returns `Success`.
- `RunOnce` — same, but returns a `RunOutcome` with `Device = null, DeviceId = null`.

**`LoggingFaultPolicyMinimal`**:

```csharp
public class LoggingFaultPolicyMinimal(TextWriter output) : IProcessorFaultPolicy
{
    public bool ShouldContinue(in ProcessorFault fault);
    // writes a line to output, returns false (stop the simulation)
}
```

### Built-in libraries

**`Libraries`** — `static class` with string constants (namespace `Nantuc.VmApplication.Extensions.StdLibraries`):

- `heap_nanasm` — `func_HeapGet`, `func_HeapSet`, `func_GetSP`, `func_Load64/32/16/8`, `func_Store64/32/16/8`.
- `alloc_minic` — first-fit allocator with split and coalescing. Block layout: header (size | FREE_BIT) + payload + footer. `MIN_BLOCK = 24`.
- `io_nanasm` — `func_In`, `func_Out`.
- `print_minic` — `print8`, `print16`, `write8`, `write16`, `flush` via `io_nanasm`.

**Important:** the `FunctionGenerator.GenerateFunction` bug (asymmetric push/pop of callee-saved registers, which corrupted `alloc_minic` with 3-4 locals) **has been fixed**. Therefore the workaround "keep state in globals" is no longer required: in the next iteration `alloc_minic` can (and should) be rewritten with locals. The current code still keeps `heapFreeList`/`heapCur`/`heapStart`/`FREE_BIT`/`MIN_BLOCK` in globals as legacy. More details in [STDLIBRARIES.md](STDLIBRARIES.md).

### RAM utilities

**`DeviceContextExtensions`**:

```csharp
extension(DeviceContext ctx)
{
    public byte[] GetRamAsNewArray(nuint start, nuint length);
    public void   CopyRamTo(Span<byte> destination, nuint start);
}
```

Both throw `InvalidOperationException` if `ctx.IsRunning`. They check `start + length <= bufferLength` and `length <= int.MaxValue`. Copying is byte-by-byte (via `IReadOnlyNativeBuffer`).

### `CompilerExtension`

Extension on `IReadOnlyDictionary<ErrorCode, string>`:

```csharp
extension(IReadOnlyDictionary<ErrorCode, string> templates)
{
    public void ValidateAllCodesCovered();
    public bool TryValidateAllCodesCovered(out ErrorCode code);
}
```

Used to validate custom message providers (`DefaultErrorMessageProvider.Templates`). According to changelog 0.7.2 these methods are marked as removed — the file is still present in the code, see [What to fix](#what-to-fix), item 3.

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

**Step-by-step debugging with breakpoints:**

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

// Step with CALL stepping over
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

**Interrupt a long run from another thread:**

```csharp
using Nantuc.VmApplication.Extensions.Debugging;

using var dbg = new Debugger(vm);
dbg.Load(program, 0, 0);

var runner = Task.Run(() => dbg.Continue());     // may run for a long time
Thread.Sleep(200);
dbg.RequestStop();                                // volatile bool, safe
var stop = runner.Result;                         // Reason == UserStop
```

> `RequestStop` is the only thread-safe method of `Debugger`; the others (`Step`/`Continue`/`Load`) must be called from a single coordinator thread.

**Stop at HALT → continue:**

```csharp
var stop = dbg.Continue();
if (stop.Reason == DebugStopReason.Sleeping)
{
    // VM in HALT, waiting for WAKE_INT from a device
    // … handle sleep externally …
    dbg.Wake();                                    // Resume() → OpCode.WAKE in queue
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

var comp = host.Project.CompileToIL(sources, 0, optimize: true).Build();
if (comp.OptimizationResultLog is { } logs)
    foreach (var (id, log) in logs.Logs)
        foreach (var e in log.GetLogs())
            Console.WriteLine($"[{id}] pass {e.Pass}: {e.Message}");
```

**Standard peephole optimizations:**

```csharp
host.Project.Optimizator.VMPeepholeOptimizer.AddStdRules();

var il = host.Project.CompileToIL(sources, 0, optimize: false);
il = host.Project.OptimizationIL(il, peepholeOptimizationCount: 5);
var comp = il.Build();
```

**One-line run with diagnostics:**

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

    if (outcome.FaultInfo.Status != BiosStatus.EndProgramm)
        Console.Error.WriteLine($"VM fault: {outcome.FaultInfo.Status} " +
                                $"at IP=0x{outcome.FaultInfo.InstructionPointer:X}");
}
```

**Register on a different sector:**

```csharp
var outcome = host.RunAndKeep(source, SourceLanguage.C, sector: 5);
```

**Save sources to the project:**

```csharp
host.Project.SaveFileProject("main.c", "int main() { return 0; }");
host.Project.SaveFileInclude("heap.nanasm", Libraries.heap_nanasm);
```

**Read RAM after the run:**

```csharp
using Nantuc.VmApplication.Extensions.DeviceCtx;

var outcome = host.RunAndKeep("int main() { int x = 42; return x; }", SourceLanguage.C);
if (outcome.Success)
{
    var ram = outcome.Device!.GetRamAsNewArray(0, 256);
}
```

## How close the module is to the user

Medium-high. This is an optional layer on top of the main API. If you just want to "run and see" — plug in `Nantuc.VmApplication.Extensions` and use `RunAndKeep`/`RunOnce`/`AddStdRules`. If you are writing an IDE or debugger — use `Debugger`/`Disassembler`/`StringDisassembler`/`InstructionFormatter`. If you are writing a game — `SynchronousIOStream`/`QueuedIOStream` for UART.

**What you should do:**
- Plug in `AddStdRules()` in `VmHostFactory.CreateDefault` scenarios for compilation with optimizations.
- Use `QueuedIOStream` for non-blocking output (game loop), `SynchronousIOStream` for simple cases.
- For debugging — `DisassembleToString()` (text) or `Disassemble()` (structure).
- To save RAM — `GetRamAsNewArray` (snapshot) and `CopyRamTo` (into a prepared `Span<byte>`).
- Set `ReplaceSafeMemAccessWithUnsafeRule.SizeRam` to the actual RAM size of the `Device`.
- `SaveFileProject`/`SaveFileInclude` — check `localPath` for the absence of `..`.
- For your own optimization rules — assign `OptimizationId` via `WithPeepholePrefix()` / `WithRule()` / `WithASTNodePrefix()` so that the prefix matches the built-in ones.
- For debugging — `Debugger` on top of `DeviceContext`: `dbg.Load(program)` → `dbg.Step()`/`StepOver()`/`StepOut()`/`Continue()`, `dbg.AddBreakpoint(addr)`, `dbg.CurrentInstruction`, `dbg.GetCallStack()`. To stop a long run — `dbg.RequestStop()` from another thread.
- For debugging in an IDE — subscribe to `dbg.Stopped` to update the registers/stack/RAM panel on each stop.
- To "run to the end" (e.g., a CI run) — `dbg.RunToEnd(stepLimit: N)`, not `Continue` (the latter will get stuck at the first breakpoint).

**What you should not do:**
- Call `RunAndKeep` multiple times on the same `VmHost` with the same `sector` — the sector will be occupied. Use `sector:` explicitly or `RunOnce`.
- Use `RunAndKeep` for untrusted code — it registers the VM on the specified sector without a sandbox.
- Rely on `AddStdRules` for peephole — `ReplaceSafeMemAccessWithUnsafeRule` is **not** added automatically.
- Expect `RunOutcome.Success` to reflect runtime faults — `Success` only concerns compilation and loading. For the status — `outcome.FaultInfo`.
- Use `Libraries.alloc_minic` as a "reference" — it still has globals as legacy (the workaround is no longer needed, but the code has not been simplified yet).
- Call `Step`/`Continue`/`Load` from different threads on the same `Debugger` — not thread-safe (except `RequestStop`). The model is the same as `DeviceContext`.
- Expect an exact call stack from `GetCallStack()` — the VM does not store a frame pointer; reconstruction is heuristic; false positives are possible.
- Rely on `stop.Snapshot.IsSleeping` in `Continue` — the sleep flag comes only from `_stepSnapshot` (after `SingleStep`); when stopping via `RequestStop` it may be `false`, even if the VM is in `HALT`. For `HALT`, `stop.Reason == Sleeping` is more reliable.
- Use one `Debugger` for multiple `Load` — after the program completes (or a manual `Dispose`), create a new instance. A repeated `Load` throws `InvalidOperationException` (protection against leaking step mode).
- Expect `Debugger.ReadRam` to protect against `IsRunning` (like `DeviceContextExtensions.GetRamAsNewArray`) — the debugger reads RAM during a stop; if you call `ReadRam` from another thread during `Continue`, the data will be race-y.

## What was fixed in this version

- **Added the `Debugger`** (`Debugging/`): `Step`/`StepOver`/`StepOut`/`Continue`/`ContinueTo`/`RunToEnd`, breakpoints, `RequestStop` from another thread, `Wake`, RAM reading (`ReadRam`/`CopyRamTo`/`ReadUInt16/32/64`/`ReadCString`), `CurrentInstruction`/`DisassembleAt`/`DisassembleProgram` via `StringDisassembler`, `GetCallStack` (best-effort), `Stopped` event. Does not use `AsRawDevice`/`unsafe` — only the public API of `DeviceContext`.
- **`Disassembler` is now a `public static class`** — the extension `res.Disassemble(int?)` is available externally.
- **`PeepholeLog.GetLogs()` no longer calls `log.Clear()`**.
- **`PeepholeLog.Id => id`** — the double prefix `peephole.peephole.*` was fixed.
- **`ReplaceSafeMemAccessWithUnsafeRule.SizeRam = RamSize.B128`** — the rule now has a default.
- **`RunOutcome` contains `ProcessorFault FaultInfo`**.
- **`RunAndKeep`/`RunAndCleanup`/`RunOnce` accept `sector`**.
- **`Libraries.alloc_minic`** — first-fit with split and coalescing, header + footer, `FREE_BIT`, `MIN_BLOCK = 24`.
- **`<AllowUnsafeBlocks>` removed** from the csproj (there is no `unsafe` in the module's code).
- **The compiler bug `FunctionGenerator.GenerateFunction`** (asymmetric push/pop of callee-saved registers) has been fixed. Therefore the "keep state in globals" workaround is no longer required.

## Special information

### Double pass over the data section

Both `Disassembler.Disassemble` and `StringDisassembler.Disassemble` split the stream into code and data by `codeSize`. If `codeSize == 0`, the whole stream is data. If `codeSize == size`, there is no data section. If the last instruction with `data64` goes beyond `codeSize` (padding + 8 bytes), `pos` after the loop will be `> codeSize`, and the data loop will start at `pos`, not `codeSize`. The first `pos - codeSize` bytes of the data section are actually the tail of the last instruction and will not be shown as data. `bytesConsumed` in `DisassemblyResult` is still equal to `pos`.

### Debugger: `_skipBreakpointOnce` and repeated `Continue`

`Continue` checks two conditions at the start:
1. `_skipBreakpointOnce == rIP` → resets the flag and does **not** stop at the current address (this is the same breakpoint that fired last time).
2. `HasBreakpoint(rIP)` → returns `DebugStopReason.Breakpoint` without stepping.

`StepOneOrFinish` after each `SingleStep` sets `_skipBreakpointOnce = newIp` if there is a breakpoint at the new `rIP`. This gives correct semantics:

```
Load → Continue (hit bp at 0x00) → Continue (skip 0x00, goes further)
```

Without `_skipBreakpointOnce`, `Continue` → breakpoint → `Continue` would loop endlessly at the same `rIP`.

`ContinueTo(addr)` uses the same logic: adds `addr` to `_breakpoints`, calls `Continue`, removes the breakpoint. If `addr` was already a user breakpoint, it is preserved after the call.

### Debugger: `ProcessorSnapshot` in `DebugStop`

In `Step`/`StepOver`/`StepOut`/`Continue`, `ProcessorSnapshot` is taken from `_stepSnapshot` (`SingleStep(onStep)` callback), so:
- `IsSleeping` is correct (the VM sets `_isSleeping` before invoking the callback);
- `IsRunning` is correct (the VM resets it before invoking the callback);
- `RawInstruction` — the word at the **current** `rIP` (after the step).

In the `Continue` branch where the stop occurs before the step (initial-IP check for a breakpoint), `Snapshot` is built via `BuildSnapshotFromRegisters` — there `IsSleeping == false` always, because `DeviceContext` does not expose the sleep flag. For detecting `HALT`, `stop.Reason == Sleeping` is more reliable (this comes only from `_stepSnapshot`).

### Debugger: `GetCallStack` — heuristic, not an exact stack

The VM does not store a frame pointer and does not maintain a separate call stack. `GetCallStack` scans `[rSP, RamSize)` for 8-byte values in the range `[ProgramStart, ProgramEnd)`, aligned to 4. This finds **return addresses** pushed by `CALL` (see [`bytecode.md` §5.6](../docs/bytecode.md#56-control-flow)), but:

- **False positives** — local `ulong` values that happen to fall into the code range.
- **Misses** — if `CALL` was indirect (`CALL_IND`), the return address is also on the stack; but if the program uses its own manual call stack (like `print_minic`/`alloc_minic`), `rSP` may point elsewhere.
- **Order** — `frames[0]` is the topmost (closest to the current `rSP`), `frames[N]` is the deepest.

An exact stack requires a frame pointer (which the VM does not have) or a separate call stack in `Processor` (not implemented). In the debugger — best-effort only.

### Debugger: `StepOver` and the return address

For `CALL` (immediate), the instruction length is 12 bytes (4 + 4 padding + 8 immediate), so the return address is `AlignUp(ip + 4, 8) + 8`. For `CALL_IND` (indirect), the length is 4 bytes, the return address is `ip + 4`. This matches §3.2 and §5.6 of [`bytecode.md`](../docs/bytecode.md). An error here would cause "falling into" the called function.

### `Libraries.alloc_minic`

Block layout:
```
+0..7    : size | FREE_BIT(bit0)        — header
+8..15   : next_free (only for free)    — or the start of payload
+16..sz-8: continuation of payload / unused
+sz-8..sz: size | FREE_BIT(bit0)        — footer (for coalescing left)
```
`payload` starts at `+8`. The minimum block is 24 bytes. On free: `blkMark(b, size, 1)` → `mergeRight(b)` → `mergeLeft(b)` → `blkPush(b)`. On allocation: first-fit over `heapFreeList`, if large enough — `split` (if `csz - need >= MIN_BLOCK`), otherwise `blkMark(curr, csz, 0)` (no split); otherwise bump via `GetSP()`.

**Historical note:** `alloc_minic` used to keep all state in globals because of the `FunctionGenerator.GenerateFunction` bug in the compiler (asymmetric push/pop of callee-saved registers with 3-4 locals). The bug is fixed — the workaround is no longer needed, but the current code has not been migrated to locals yet. Details in [STDLIBRARIES.md](STDLIBRARIES.md).

### `PortBusExtensions.GetBaseAddress`

`(ulong)sector * bus.PortsOnDevice` — computes the absolute start address of the sector in the port space. `sector` is first cast to `ulong`, so there is no overflow.

### `HostExtensions` and `sector`

`RunAndKeep`/`RunOnce`/`RunAndCleanup` accept `sector` (default `0`). If `sector` is occupied by another device, `RegisterDevice` returns `-2`, and `GetDeviceContext(-2)!` gives an NRE. `RunOnce` and `RunAndCleanup` free the `sector` via `UnregisterDevice`, so they can be called repeatedly with the same `sector`. `RunAndKeep` — not, until you call `UnregisterDevice` manually.

### `RunOutcome.FaultInfo`

`FaultInfo` is the `ProcessorFault` from `ctx.Run(new LaunchOptions(...))`. On success — `BiosStatus.EndProgramm`; on failure — the corresponding status with `InstructionPointer`/`OpCode`. If compilation/loading fails, `RunOutcome.Success == false`, `Device == null`, and `FaultInfo == default(ProcessorFault)`. Important: `_faultData` in `Processor` is not reset in `LaunchProgramm` — a second `Run` on the same `Device` may return the fault from the first if the first was a failure.

### Platform limitations

- `net10.0`, `<AllowUnsafeBlocks>` is **not** enabled.
- `Thread.Sleep(1)` in `QueuedIOStream.Flush` — in WASM either a no-op or `PlatformNotSupportedException`. For WASM use `SynchronousIOStream`.
- `Channel<byte>` and `Task.Run(DrainAsync)` — work in WASM, but background tasks may not complete before an explicit `await`.
- `ImmutableArray<T>` — works in AOT/WASM, but each mutation via `CreateBuilder` allocates. For large programs (10k+ instructions), `DisassemblyResult` may consume a lot of memory. For streaming — `StringDisassembler.Disassemble`.
- `Guid.NewGuid().GetHashCode()` in `InlineSmallVoidFunctionsRule` — stable within a process, but not across processes. For deterministic builds — a problem.
- `Debugger` — `SingleStep` works in WASM (one instruction — synchronously), but `Continue`/`StepOver`/`StepOut`/`GetCallStack` are synchronous loops that block the current thread. In browser-WASM (single-threaded) — blocks the UI. For an IDE in the browser — move to a Web Worker or use `Task.Run` with `RequestStop`.

## What to fix

2. **[API/XML] `LoggingFaultPolicyMinimal` lives in `ProjectExtension.cs`.**
   File: `Host/ProjectExtension.cs`. The class is a fault policy, not a project extension. More logical — a separate file in `Host/` (e.g., `LoggingFaultPolicyMinimal.cs`).
   Fix: move to a separate file, do not change the namespace (`Nantuc.VmApplication.Extensions.Host`).

3. **[API] `CompilerExtension.cs` — the file contradicts changelog 0.7.2.**
   File: `Compiler/CompilerExtension.cs`. Changelog 0.7.2 marks `ValidateAllCodesCovered`/`TryValidateAllCodesCovered` as removed (**BREAKING**), but the file is still present in the code. Either delete the file (then simply remove the README section), or revert the changelog entry.
   Fix: decide and bring the code and changelog into agreement.

4. **[API] `alloc_minic` still keeps state in globals.**
   File: `StdLibraries/Libraries.cs`. The compiler bug that made this necessary has been fixed.
   Fix: rewrite `alloc_minic` with locals (`ulong heapFreeList = 0; ulong heapCur = 0; …`) and verify with tests (allocation/free/coalesce). After that, remove the "historical note" from the README and STDLIBRARIES.

5. **[API] `WithRule` and `WithASTNodePrefix` are not documented.**
   Files: `Optimizators/PeepholeOptimizerExtension.cs`, `Optimizators/AstOptimizerExtension.cs`. Public extension methods on `OptimizationId`. They are now described in the README (see the "Optimizations" section), but it is worth checking whether they are actually needed by the user: `WithRule` is not used anywhere inside the module.
   Fix: either use `WithRule` in one of the rules for consistency, or remove it from the public API.

6. **[API] `RunAndCleanup` does not allow choosing `deviceName`.**
   File: `Host/HostExtensions.cs`. `RunAndKeep` accepts `deviceName`, `RunAndCleanup` does not (always `"run"`). This is asymmetric and hinders debugging.
   Fix: add `string deviceName = "run"` to `RunAndCleanup` and `RunOnce`.

7. **[API/XML] `SaveFileInclude`/`SaveFileProject` — path traversal.**
   File: `Host/ProjectExtension.cs`. `localPath` is not checked for `..` and absolute paths.
   Fix: validate `localPath` (`Path.IsPathRooted` + absence of `..` in segments) or document that the call is trusted input.

8. **[XML] `ReplaceSafeMemAccessWithUnsafeRule.SizeRam` — default is underestimated.**
   File: `Optimizators/IRAsmRules/ReplaceSafeMemAccessWithUnsafeRule.cs`. `RamSize.B128` is not a "safe" default but a minimum; the rule is silently a no-op if the user does not set the size.
   Fix: either default to the `RamSize` from `Device` (but the rule does not know about `Device`), or add `<remarks>` explicitly saying "you must set `SizeRam` to the device RAM size, otherwise the rule is a no-op".

9. **[API] `AddStdRules` for AST contains `RemoveUnreachableCodeRule` twice.**
   File: `Optimizators/AstOptimizerExtension.cs`. The duplication is intentional (after inlining), but non-obvious.
   Fix: add `<remarks>` to `AddStdRules` explaining why the second pass is needed, or extract it into a separate composite method `AddStdRulesWithCleanup()`.

10. **[API] `Debugger` is single-use per `Load`.**
    File: `Debugging/Debugger.cs`. A repeated `Load` throws `InvalidOperationException` so that step mode (`ctx.RunStepMode` without `EndStepMode`) does not leak.
    Fix: either `Reset()` (exits step mode, clears breakpoints, but keeps `Context`), or an explicit `Unload()`/`Reload(program)`. For now — a new instance for each run.

11. **[API] `Debugger.ReadRam` is not protected against `IsRunning`.**
    File: `Debugging/Debugger.cs`. Unlike `DeviceContextExtensions.GetRamAsNewArray`/`CopyRamTo` (which throw `InvalidOperationException` when `ctx.IsRunning`), the debugger reads RAM **during a stop** — that is its normal scenario. But if the user calls `ReadRam` from another thread during `Continue`, the data will be race-y.
    Fix: either check `_isStepping` (a flag "currently in `SingleStep`"), or document it in `<remarks>` (currently — only in the README).

12. **[API/XML] `Debugger.StepOut` — heuristic based on `rSP`.**
    File: `Debugging/Debugger.cs`. The semantics "`rSP` became larger than it was" break if the function uses `PUSH`/`POP` for temporary values (typical for `print_minic`/`alloc_minic`). In such cases `StepOut` may exit the function earlier than expected.
    Fix: either document the limitation, or add a "call depth" via `GetCallStack` (frame count) instead of `rSP` — but that is more expensive.

13. **[API] `Debugger.GetCallStack` — best-effort, false positives.**
    File: `Debugging/Debugger.cs`. The VM does not store a frame pointer; the stack is reconstructed by scanning `[rSP, RamSize)` for 8-byte values in the code range. Local `ulong` values that accidentally fall into the range will produce false frames.
    Fix: either `/// <remarks>` with an explicit warning (currently — in the README), or implement a frame pointer in `Processor` (a breaking change for bytecode).