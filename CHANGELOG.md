# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

Order of sections within a version:
**Added -> Changed -> Deprecated -> Removed -> Fixed -> Documentation**.

Short labels:
- **BREAKING** — a backward-incompatible change; may break user code when upgrading.

## [0.26.0] - 2026-11-27

### Changed
- `Device` now includes a private container for `Processor`, `MemoryBus`, and `ManualResetEventSlim`: `DeviceScope` with a safer `Dispose` implementation; after `Dispose`, `Processor`, `MemoryBus`, and `ManualResetEventSlim` are set to `null`.
- `AsyncDevice` now uses `AsyncDeviceScope`, an exact copy of `DeviceScope`.

## [0.25.1] - 2026-11-26 [HOT FIX]

### Changed
- Reverted `JL` and `JG` behavior to their previous state

## [0.25.0] - 2026-11-26

### Added
- Private method in `Device` and an asynchronous version; `GlobalInit` is needed for synchronizing initialization methods in `Debug`, `Step`, and `Run`
- Added file `AsyncDevice.cs`; `AsyncDevice`: full functional copy of `Device` from `Kernel`, fully asynchronous
- Added file `AsyncDeviceContext.cs`; `AsyncDeviceContext`: full functional copy of `DeviceContext` including most extension methods
- Added file `AsyncDeviceExtension.cs`; fully replicates the API methods `GetRamAsNewArray`, `CopyRamTo`, `TryCopyRamTo`, and `ReadRam<T>`; adds extension API:
	- `ManagerDevices`:
		- `AsyncDevice CreateAsyncDevice(RamSize, IProcessorPolicy)` - analogous to the native `ManagerDevices.CreateDevice`
	- `VmEmulator`:
		- `AsyncDeviceContext? GetAsyncDeviceContext(int)` - analogous to the native `VmEmulator.GetDeviceContext`
		- `bool TryGetAsyncDeviceContext(int, out AsyncDeviceContext?)` - analogous to the native `VmEmulator.TryGetDeviceContext`
		- `int RegisterAsyncDevice(DeviceSpec, uint)` - analogous to the native `VmEmulator.RegisterDevice`
		- `IdState TryRegisterAsyncDevice(DeviceSpec, uint, out int)` - analogous to the native `VmEmulator.TryRegisterDevice`
- Added file `ReusableWakeSignal.cs`; `ReusableWakeSignal` component for `AsyncDevice` operation in an asynchronous environment, uses only `AsyncDevice`
- `Async*` variations for `RunAndKeep`, `RunOnce`, `RunAndCleanup`, and a new class `AsyncRunOutcome`
- Tests for standard libraries
- Tests for determinism of asynchronous and synchronous VMs
- Tests for asynchronous VM sleep functionality
- Tests for restarting the VM after stopping (verifying that each run resets all states)
- Added `CarryFlagSet`: Occupies the 3rd bit (`4UL`) in the flags register `rFL`.

### Changed
- `DiagnosticResult.ToString()` explicitly overridden with format `"[{(int)ErrorCode}] {Message}"` to eliminate reflection
- `ProcessorSnapshot.ToString()` explicitly overridden, no longer uses `enum.ToString()`
- `DecodeInstructionResult.ToString()` explicitly overridden, no longer uses `enum.ToString()`
- Rewritten `InstructionCMP`: Now it explicitly calculates the unsigned condition `v1 < v2` and passes it to `SetFlags` as `carryFlag`. This ensures that when `0 - 0xFFFFFFFFFFFFFFFF`, the carry flag will be set to 1.
- Updated `InstructionTEST`: Explicitly passes 0UL for the carry flag, since logical operations (`AND`) do not generate a carry (according to `x86`/`ARM` standards).

### Fixed
- Fixed `InstructionJL`: Now checks `CarryFlagSet` instead of `NegativeFlagSet`, making it a correct "Jump if Below" instruction for unsigned numbers.
- Fixed `InstructionJG`: Now checks `!CarryFlagSet & !ZeroFlagSet` (the classic "Jump if Above" condition for unsigned numbers)

## [0.24.0] - 2026-11-26

### Added
- `ThrowIfDisposed` to `Device`
- Disposal checks to execution and program-loading methods (!no checks for `Step` or an already running device!)
- Factory method `Device CreateDevice(RamSize, IProcessorPolicy)` to `ManagerDevices`
- Thread-safe `Dispose` implementation to `ManagerDevices` using the `Volatile` class

### Changed
- Refactored the `PortBus` class: replaced its 6 fields with properties that return bit-shift results
- Slightly rewrote the condition in `Device.LoadProgram` and `Device.TryLoadProgram` for a safer and more predictable behavior
- **BREAKING:** Renamed namespace `Nantuc.Kernel.Utilies` (`Utilies` -> `Utilities`)
- **BREAKING:** Renamed method `VmEmulatorUnsafeExtension.GetRawEmulator` (`GetRawEmulator` -> `GetRawManagerDevices`)
- **BREAKING:** Replaced the primary constructor `ManagerDevices(PortBus)` with `ManagerDevices(PortSize, DevicePortSize)` in `ManagerDevices`
- **BREAKING:** Fixed a typo in the class name `VmPojectUnsafeExtension` -> `VmProjectUnsafeExtension`

### Removed
- Private `PortBus` field in `Device`
- **BREAKING:** `Emulator.cs` file and the `Emulator` class

### Fixed
- `ManagerDevices.Count`: `_isDirty` flag was not reset back to `false` after recalculation

## [0.23.0] - 2026-11-25

### Added
- Static property `FrozenSet<ErrorCode> ErrorCodesValidateTable { get; }` to `DiagnosticsCompilerExtension`

### Changed
- **BREAKING:** Added parameters to `RunAndCleanup` and `RunOnce` to keep their signatures consistent with `RunAndKeep`
- Rewrote `Nantuc.Samples.Scratch` to use safer and more concise code
- Replaced `Enum.GetValues()` in `DiagnosticsCompilerExtension` with an explicit `FrozenSet<ErrorCode> = [...]` collection

### Removed
- **BREAKING:** Removed `LoggingFaultPolicyMinimal` class (dead and legacy code)


## [0.22.0] - 2026-11-24

### Added
- `Nantuc.Samples.InWeb` module to the samples

## [0.21.0] - 2026-11-24

### Changed
- Replaced `AsmLanguageDefinition.MnemonicMap` generation from manual `["OpCode"] = OpCode` entries with static generators (removed `Dictionary` allocation)

### Removed
- **BREAKING:** `CodeGenUtils.GetAlignment` method
- **BREAKING:** `GlobalMemoryManager.AllocatePseudoGlobals` method
- **BREAKING:** `GlobalMemoryManager.TryGetAddress` method
- **BREAKING:** `VariableAccessor.GetVariableSize` method

## [0.20.0] - 2026-11-23

### Added
- `IdStateExtension`: added `string AsText` extension property for `IdState`, consistent with other `enum` types

### Changed
- **BREAKING:** `OpCodeExtension.cs`: replaced `OpName()` method with `AsText` property
- **BREAKING:** `OpCodeSizeExtension.cs`: replaced `SizeName()` method with `AsText` property
- **BREAKING:** `RegTypeExtension.cs`: replaced `RegName()` method with `AsText` property
- **BREAKING:** `BiosStatusExtension.cs`: replaced `StatusName()` method with `AsText` property
- **BREAKING:** moved `ProcessorSnapshot` to `Nantuc.Common`
- minor non-functional code cleanups

### Removed
- **BREAKING:** removed unused `AlignUpArithmetic` method from `AlignmentExtensions` (dead code)

## [0.19.0] - 2026-11-23

### Added
- static `*Extension` classes for `OpCode`, `OpCodeSize`, `RegType`, and `BiosStatus`

### Removed
- **BREAKING:** `extension` methods from `Compiler` for `OpCode`, `OpCodeSize`, `RegType`, and `BiosStatus`

## [0.18.0] - 2026-11-23

### Added
- `Device.cs` `Step` method - step-by-step execution in non-debug mode
- `Device.cs` `BatchStep(int)` method - batch step-by-step execution in non-debug mode
- `DeviceContext.cs` `Step` method - step-by-step execution in non-debug mode
- `DeviceContext.cs` `BatchStep(int)` method - batch step-by-step execution in non-debug mode

### Changed
- **BREAKING:** `Device.cs` renamed `NextStepProcessorCount` -> `BatchStepDebug`
- **BREAKING:** `Device.cs` renamed `NextStepProcessor` -> `StepDebug`
- **BREAKING:** `DeviceContext.cs` renamed `SingleStep` -> `StepDebug`
- **BREAKING:** `DeviceContext.cs` renamed `MultiStep` -> `BatchStepDebug`
- standardized step logic into a single `Device.Step()` method

## [0.17.0] - 2026-11-19

### Added
- Added a brief summary of tests to the metrics section

### Changed
- Reworked the register allocation algorithm
- Removed redundant `mov` emissions in the code generator

## [0.16.3] - 2026-11-18

### Added
- Groundwork for an improved register allocator

### Changed
- Replaced all `DiagnosticData` factory calls with explicit `DiagnosticData.Of` calls, and removed all implicit constructors for the struct

### Fixed
- Fixed the output of `ErrorCode.Parser_ExpectedToken` (`"Expected '{0}', got '{1}' at {2}:{3}"`) so that information is now formatted correctly

## [0.16.2] - 2026-11-18

### Added
- support for type casts like `(byte)300` in the compiler

## [0.16.1] - 2026-11-16

### Added
- Added borders to Snake and reduced input delay

### Fixed
- `Nantuc.CLI` is now invoked via `nantuc` instead of `nantuc.cli`

## [0.16.0] - 2026-11-15

### Changed
- `Processor` was split into 12 partial files by subsystem (flags, memory, arithmetic, control flow, stack, ports, interrupts, snapshot, faults). Method bodies were not changed — the refactoring is purely structural. `MaskOf`/`SignExtend` were moved into `InstructionHelpers`. Dead code `SetStatusNotSupportedOpCode` was removed.
- **BREAKING:** moved to `IR/Emitter`. `IRAssembler` was split into 3 partial files: `.cs` (public API + `MarkLabel` / `Emit*` / `GetItems`), `.Layout.cs` (first pass — computing label addresses and section sizes), `.Emit.cs` (second pass — byte emission).
  - `Build()` (~90 lines, two nested `switch` statements with manual offset/alignment) was decomposed into `ComputeLabelAddresses()` (pass 1) + `EmitItem(...)` (pass 2). `Build()` itself is 15 lines with a clear “layout → emit” structure.
  - Duplicated alignment logic was extracted into shared helpers `Align(offset, alignment)` and `AdvanceInstructionOffset(instr, offset)` — a single point for both passes. Changing alignment (e.g., a 16-byte boundary for SSE-like instructions in the future) becomes one edit instead of two.
  - `EmitItem` was decomposed by item type: `EmitInstructionItem` (flat, with an early `return` when `!HasImmediate && !IsJump` instead of two nested `if`s), `EmitDataItem`, `PadTo`. Jump label resolution — `ResolveJumpTarget`.
  - Padding buffer `new byte[7]` (heap allocation on every `Build()`) → `stackalloc byte[7]` (zero-alloc). The size 7 is calculated for alignment ≤ 8 — all current `EmitData` calls use 1 or 8. The latent limitation is preserved (see “Known Limitations”).
- **BREAKING:** moved to `ASM/Parser`. **`AssemblerParser`** was split into 5 partial files: `.cs` (API + fields), `.Lexing.cs` (strings, comments, sections), `.Instructions.cs` (`ParseInstruction` + `Format*`), `.DataSection.cs` (data directives), `.Numbers.cs` (numeric and register helpers). `_regMap` / `_opMap` (85 lines of manually maintained dictionaries) were moved to `AsmLanguageDefinition.MnemonicMap` / `RegisterMap` — a single source of truth with `FrozenSet` for regexes. `ProcessLines` (70 lines) was decomposed into `ProcessSectionDirective` / `ProcessDataLine` / `ProcessCodeLine`. `Format*` methods use a shared `RequireOperands` instead of 3× repetition of `if (tokens.Length < N) Throw`. The dead line `line.IndexOf(';')` was removed (the `;` delimiter is already consumed by `Split`). The typo `FormateLOAD_IND__STORE_IND` → `FormatLOAD_IND_STORE_IND` was fixed.

## [0.15.0] - 2026-11-15

### Added
- `SaveDepthEstimator` — max-depth estimation for `ExpressionSaveDepth` moved out of `FunctionContext` into a separate static class (`Estimate(ASTNode?)`).
- `VariableLayoutBuilder` — variable layout (size, register/stack, signedness) moved out of `FunctionContext`.
- `LocalVarCollector` — collection of local variables over the function body moved out of `FunctionGenerator` (`Collect(BlockNode)`).
- `ConstantEvaluator` — constant folding in initializers moved out of `FunctionGenerator`.
- `VariableAccessAnalyzer` — both AST walkers from `RegisterAllocator` (`CollectAddressTakenVars`, `CollectVarEvents`).
- `GreedyGraphColorer` — greedy interference graph coloring moved out of `RegisterAllocator` (`Color(graph, availableRegs)`).
- `WordRegexBuilder` — shared builder of the `\b(...)\b` pattern from `FrozenSet<string>`; used by `MiniCLanguageDefinition` and `AsmLanguageDefinition`.
- `LanguageDefinitionDriftTests` — regression tests for consistency between `Regex` and `FrozenSet` in language definitions, including for the historical bug with `"new"`.
- Automated code metrics collection for all modules.

### Changed
- **BREAKING:** types `Lexer`, `Token`, `TokenType`, `TokenTypeExtension` moved to `Nantuc.Compiler.C.Lexer`.
- **BREAKING:** `FunctionContext`, `SaveDepthEstimator`, `VariableLayoutBuilder` moved to `Nantuc.Compiler.C.CodeGenerator.FunctionCtx`.
- **BREAKING:** `FunctionGenerator`, `LocalVarCollector`, `ConstantEvaluator` moved to `Nantuc.Compiler.C.CodeGenerator.FunctionGen`.
- **BREAKING:** `RegisterAllocator`, `VariableAccessAnalyzer`, `GreedyGraphColorer`, `RegisterAllocationResult` moved to `Nantuc.Compiler.C.CodeGenerator.RegistersAlloc`.
- **BREAKING:** `MiniCLanguageDefinition.GetKeywordRegex()` replaced with the `KeywordRegex` property.
- **BREAKING:** in `AsmLanguageDefinition`, `GetMnemonicRegex()` / `GetRegisterRegex()` replaced with the `MnemonicRegex` / `RegisterRegex` properties; `Mnemonics` / `Registers` are now `FrozenSet<string>` properties instead of `public static readonly` fields.
- `Lexer` split into 4 partial files: `.cs`, `.Trivia.cs`, `.Literals.cs`, `.Operators.cs`; `ReadOperator` converted to table-driven parsing (`Operators` is an array, order = precedence); 13 repetitions of `_position++; _column++;` eliminated via `Advance()` / `AdvanceTrackingLine()`.
- `FunctionContext.ComputeMaxBinaryDepth` — magic `+1`s replaced with the named constant `SaveSlot`; nested `Math.Max(a, Math.Max(b, c))` replaced with `Max(a, b, c)`; complex cases (`Assignment`, `FunctionCall`, `IndirectCall`) moved into named methods with comments.
- `FunctionContext.Create` split into 5 phases: `CollectVariables` → `BuildVarNodeMap` → `AllocateRegisters` → `BuildVarMap` → `ConfigureReturnInfo`; duplicates of `IsSigned` and `ContainsKey(type)` moved into `ComputeIsSigned` / `CanUseRegister`.
- `FunctionGenerator` split into 4 partial files: `.cs`, `.DataSection.cs`, `.Prologue.cs`, `.Epilogue.cs`; `GenerateFunction` decomposed into `EmitPrologue` → `GenerateBlock` → `EmitEpilogue`; parameter marshalling — `EmitRegisterParam` / `EmitStackParam`; `ErrorReporter` changed to the `_erRep` field.
- `RegisterAllocator` split into 2 partial files: `.cs`, `.Interference.cs`; `BuildInterferenceGraph` decomposed into `ComputeLiveRanges` + `Overlaps`; `AddDef` + `AddUse` merged into `AddEvent`.
- `MiniCLanguageDefinition` — `KeywordRegex` is built from `Keywords` via `WordRegexBuilder`; `[GeneratedRegex]` and the hardcoded `KeywordPattern` removed.
- `AsmLanguageDefinition` — aligned with the form of `MiniCLanguageDefinition`: property getters, `XxxRegex` naming, `WordRegexBuilder`; static constructor removed.

### Removed
- `FunctionGenerator.EvaluateConstant(ASTNode)` — overload without `structTable`, no calls (dead code).
- `MiniCLanguageDefinition.[GeneratedRegex]` + `KeywordPattern` — source of the drift bug.
- Duplication of `allVars` ↔ `allVarNodes` in `FunctionContext.Create`.

### Fixed
- `MiniCLanguageDefinition`: `"new"` was present in `KeywordPattern` but absent from `Keywords`; this caused a discrepancy between `Lexer` (which uses `Keywords`) and external tooling (which used `GetKeywordRegex()`). Now `KeywordRegex` is built from `Keywords` — drift is impossible as a class of error.
- `FunctionGenerator.GenerateGlobalInit`: `dummyCtx.VarMap` was `null!`; a global initializer of the form `int x = y;` would cause a `NullReferenceException` in `VariableAccessor.LoadToRegister`. Now `VarMap = []`.

### Known limitations
- `VariableAccessAnalyzer.CollectVarEvents` does not traverse `LValue` and `IndexExpr` in `AssignmentNode`: `i` in `arr[i] = v`, `p` in `*p = v`, `obj` in `obj.f = v` are not marked as `use`. This may lead to suboptimal, but not incorrect in practice, coloring. Separate PR with regression tests.
- `Lexer`: unpaired `**` and `!!` are tokenized as a single operator (non-standard for C); `SkipBlockComment` / `ReadString` / `ReadChar` do not report an error on an unterminated comment/literal.

## [0.14.1] - 2026-11-15

### Changed
- **BREAKING:** `ExpressionGenerator`, `StatementGenerator`, and `Parser` have been split into smaller `partial` files, given their own namespaces, readability has been improved, and dead code has been removed.

## [0.14.0] - 2026-11-14

### Added
- new opcodes added to the end of the `enum` — `CASTS` and `CASTZ`
- new opcodes added to the assembler parser
- the compiler now uses casts to simulate overflow of `Int8`, `Int16`, `Int32` and their `UInt*` versions
- new tests for the compiler

### Changed
- `Lisr<FieldInfo>` replaced with `Dictionary<string, FieldInfo>` in the `StructLayout` class
- **BREAKING:** renamed `ThrowCompiler` -> `ErrorReporter`
- **BREAKING:** renamed `Optimizators` -> `Optimizations`

## [0.13.2] - 2026-11-13

### Added
- new property `TimeSpan SleepWaitTimeout { get; }` with a default value of `TimeSpan.FromMilliseconds(15);` on the `IProcessorPolicy` interface

### Fixed
- fixed DoS and OOB vulnerabilities in the VM, but it has become slower

## [0.13.1] - 2026-11-12

### Fixed
- A bug that caused `a < b << c;` not to be parsed (leaving `<< c;` behind)

## [0.13.0] - 2026-11-12

### Added
- a new method in the `IProcessorPolicy` interface — `bool AllowUnsafeOpCodes { get; }` — permission for the processor to use unsafe opcodes due to security issues
- the condition blocks themselves for `*_UNSAFE` instructions
- a new structure `readonly record struct DiagnosticResult(string Message, ErrorCode ErrorCode)` for error diagnostics

### Changed
- **BREAKING:** name `IProcessorFaultPolicy` => `IProcessorPolicy`
- **BREAKING:** the `Errors` properties for compilation results have been changed; they now return not only the finished string but also, where possible, an `ErrorCode` — if they cannot return a specific one, they return `ErrorCode.NotImplemented`
- fixed a bug that caused the `LOAD_UNSAFE` and `STORE_UNSAFE` instructions to be replaced with their safe variants

### Documentation
- documentation synchronized with the code

## [0.12.0] - 2026-11-10

### Changed
- **BREAKING:** renamed class `ILAssembler` -> `IRAssembler`
- **BREAKING:** renamed types `Peephole*` -> `IR*`
- refactored the `RamFileService` class; it now better emulates a real file system but is still considered a Mock class

### Added
- `VmCompilerOptimizatorExtension` class — allows extracting IR and AST optimizers from `VmCompilerOptimizator` from the `Nantuc.Compiler` layer
- added RAM and register dump flags for the CLI `run` command
- `try - finally` block for `DeviceContext.Run` and `DeviceContext.DebugRun` to correctly return from the running state to the non-running state

## [0.11.0] - 2026-11-09

### Added
- `ROADMAP.md`
- benchmarks comparing `Nantuc MiniC` and `Nantuc VM` against other languages and their computation speed

## [0.10.0] - 2026-11-08

### Added
- new methods for copying RAM in `Nantuc VM`
- new class for static bytecode analysis - `Bytecode/BytecodeExtension.cs`
- all `NuGet` packages now have their own `README.md` files

### Changed
- **BREAKING:** class `CompilerExtension` renamed to `DiagnosticsCompilerExtension`
- **BREAKING:** `namespace` `Compiler` renamed to `Diagnostics`

### Documentation
- added sections about `Bytecode/BytecodeExtension.cs`
- synchronized sections about `Diagnostics/DiagnosticsCompilerExtension.cs`
- synchronized sections about `DeviceCtx/DeviceContextExtensions.cs`

## [0.9.0] - 2026-11-08

### Added
- added new module `Nantuc.CLI`
- added new tests `Nantuc.CLI.Tests`

### Documentation
- Updated root `README.md` and its localization
- Added new `README.md` for `Nantuc.CLI`

## [0.8.3] - 2026-11-05

### Added
- new std libraries in `Nantuc.VmApplication.Extensions.StdLibraries.Libraries`: `imath`, `bitops`, `ovf`
- added a test for the allocator from `std`
- 
### Changed
- **BREAKING:** the `ITimerSimulationResult` interface has been replaced with `readonly record struct TimeSimulationResult(TimeSpan Elapsed, long Steps)`; all places with the old semantics have been replaced with the new one

### Documentation 
- Updated the root `README.md` and its localization; it has become smaller

## [0.8.2] - 2026-11-04

### Changed
- Repository split into `src/`, `tests/`, `samples/`, `benchmarks/`.
- Solution file rebuilt for the new layout.
- `ProjectReference` relative paths updated after project moves.

## [0.8.1] - 2026-11-04

### Changed
- Configured packaging and publishing of NuGet packages via GitHub Actions.
- No code changes.

## [0.8.0] - 2026-11-04

### Added
- new `ErrorCode` — `Parser_IncludeNotFound = 1209`,
- added corresponding entries in `error-codes.md`
- added a corresponding entry in `DefaultErrorMessageProvider`
- added new cases in [`Nantuc.Benchmarks/SourceCasesMiniC.cs`](Nantuc.Benchmarks/SourceCasesMiniC.cs)
- source files for the benchmarks are located in a separate file [`Nantuc.Benchmarks/SourceCasesMiniC.cs`](Nantuc.Benchmarks/SourceCasesMiniC.cs)
- added a new project [`Nantuc.Benchmarks.BenchmarkDotNet`](Nantuc.Benchmarks.BenchmarkDotNet) — a project using the `BenchmarkDotNet` framework

### Changed
- **BREAKING:** all namespaces of the form `Nantuc.Kernel.*` and the corresponding projects have been changed; the `*.Kernel.*` insertions have been removed. For all except `Nantuc.Kernel`

## [0.7.4] - 2026-11-01

### Added
- **Debugger** in `Nantuc.VmApplication.Extensions.Debugging`:
	- `Debugger` — step-by-step execution on top of `DeviceContext`: `Step`/`StepOver`/`StepOut`/`Continue`/`ContinueTo`/`RunToEnd`, breakpoints (`AddBreakpoint`/`RemoveBreakpoint`/`ClearBreakpoints`/`HasBreakpoint`), `RequestStop()` (the only thread-safe method — `volatile bool`), `Wake()` from `HALT` (via `ctx.Resume()`), RAM reads (`ReadRam`/`CopyRamTo`/`ReadByte`/`ReadUInt16`/`ReadUInt32`/`ReadUInt64`/`ReadChar`/`ReadCString`), disassembly (`CurrentInstruction`/`DisassembleAt`/`DisassembleProgram` via `StringDisassembler`), best-effort call stack (`GetCallStack`), `Stopped` event. Does not use `AsRawDevice`/`unsafe` — only the public `DeviceContext` API (`RunStepMode`/`StepDebug`/`GetRegistersSnapshot`/`NativeReadOnlyBuffer`).
	- `DebugStop` (`readonly record struct`) — `Reason` + `InstructionPointer` + `StepCount` + `ProcessorSnapshot` + `ProcessorFault`; property `IsTerminal` (`Completed || Faulted || Error`).
	- `DebugStopReason` (`enum`) — `None`/`StepComplete`/`Breakpoint`/`Completed`/`Faulted`/`Sleeping`/`StepLimit`/`UserStop`/`Error`.
	- `CallStackFrame` (`readonly record struct`) — `Index` + `ReturnAddress` + `StackAddress` (best-effort: scanning `[rSP, RamSize)` for 8-byte values within the code range; the VM does not store a frame pointer).
- New properties on `ProcessorSnapshot` (needed by the debugger for `DebugStop.Snapshot` and `CurrentInstruction`):
	- `uint RawInstruction { get; }` — raw instruction word at the current `rIP`
	- `ulong DataByInstruction { get; }` — 64-bit immediate that came with the instruction
	- `BiosStatus CurrentBiosStatus { get; }` — status at the time of the snapshot
	- `ulong DataFault { get; }` — `DataByInstruction` if `CurrentBiosStatus != Success`, otherwise `0`
	- `OpCode OpCode { get; }` — decoded opcode from `RawInstruction`
	- `RegType FirstRegister { get; }` — decoded `Reg1`
	- `RegType SecondRegister { get; }` — decoded `Reg2`
	- `OpCodeSize DataSizeCode { get; }` — decoded `OpCodeSize` (meaningful for `LOAD`/`STORE`/`*_IND`)
- added file [`docs/error-code.md`](docs/error-code.md) with a table “code -> arguments -> template”
- added an approximate translation for the documentation; the Russian documentation remains — just add `*.ru.*` before `*.md`


### Changed
- **BREAKING:** `ProcessorReader` class renamed to `ProcessorSnapshot`
- **BREAKING:** `INativeReadOnlyBuffer` interface renamed to `IReadOnlyNativeBuffer`
- Obtaining `ProcessorSnapshot` from `Processor` has been changed internally in accordance with the new properties (`RawInstruction` is read from `rIP`, `OpCode`/`FirstRegister`/`SecondRegister`/`DataSizeCode` — via `InstructionDecoder.DecodeRawInstToEnums`, `DataByInstruction` — from `_lastFaultData`/immediate, `DataFault` — only when `CurrentBiosStatus != Success`)
- Caching and `IsDirty` marking for `ManagerDevices`; the `Count` value is cached, and if changes occur the system recalculates everything again


### Fixed
- closed all TODOs in module READMEs:
	- `Kernel.Common` — broken `</summary` in `DevicePortSize.B64/B128/B256`, outdated `<remarks>` on `InstructionDecoder.HasNeed64IntData`
	- `Kernel.Contracts` — `ProcessorSnapshot.GetArr` throws `ArgumentException` when `span.Length != 32` (previously `IndexOutOfRangeException`/silent zeros), XML on `IReadOnlyLogOptimization.GetLogs`
	- `Kernel.Diagnostics` — XML on `IErrorMessageProvider.GetMessage`/`TryGetTemplate`
	- `Kernel` — `Device.Dispose` frees `_wakeSignal` (previously a leak)
- `Device.Dispose` now frees `_wakeSignal`
- desynchronization in README files

### Removed
- class `BenchmarkOptimizer` from the `Nantuc.Benchmarks` module


## [0.7.3] - 2026-10-31

### Added
- Methods `Pause()` and `Resume()` for `DeviceContext` — they simply make the processor sleep.
- File [`docs/bytecode.md`](docs/bytecode.md) — bytecode specification.
- File [`docs/METRICS.md`](docs/METRICS.md) — code metrics.
- The compiler distinguishes taking a function pointer from calling it; pointer arrays have been fixed.

### Changed
- **BREAKING:** `ProcessorSnapshot` is now a `readonly struct` (previously: `IProcessorSnapshot` — `interface`).
- `onStep` from `DebugRun` is called before `_processor.Step()` — thanks to this we get the current `ProcessorSnapshot`, not the state of the next instruction.
- Minor code edits.
- Compiler tests have been split into smaller files.

### Documentation
- **VmApplication README:** fixed sections “API and constructors”, “Callbacks and results”, “Types and return values”, “Files, loading, and documentation”.
- **Root README:** code metrics moved to `docs/METRICS.md`.
- Minor documentation edits.

## [0.7.2] - 2026-10-30

### Added
- The compiler recognizes `const` (see the “Constness” section in the compiler README).
- New `ErrorCode`: `CodeGen_AssignmentToConst = 1316`, `CodeGen_IncrementOfConst = 1317`.
- `TokenTypeExtension.AsText` — no reflection (AOT-safe).
- `DiagnosticData` (`Nantuc.Kernel.Diagnostics`) — typed diagnostic argument.
- New fields in `VariableNode`, `ParameterNode`, `GlobalInfo`, `VarLocation` (`IsConst`/`IsPointedConst`).
- Tests for `const` (positive + negative — `RunCompile_ConstViolation`).

### Changed
- **BREAKING:** `ErrorReporter` — instance class (previously `static`).
- **BREAKING:** `IErrorMessageProvider.GetMessage` and `ErrorReporter.Throw` accept `ReadOnlySpan<DiagnosticData>` (previously `object`).
- `DefaultErrorMessageProvider` merges `overrides` on top of the standard templates (previously replacement).
- AST nodes have been split into separate files in `C/AST/`.
- `RegisterAllocationResult` has been moved to a separate file.
- More AOT-friendly: `Enum.ToString()` removed from diagnostics, `DiagnosticData` without boxing.

### Removed
- **BREAKING:** `ValidateAllCodesCovered` / `TryValidateAllCodesCovered`

### Documentation
- **Compiler README:** new sections “Constness (`const`)” and “Diagnostics: `DiagnosticData`”; updated tree, API table, pipeline, “What to fix”, “What has been done”.
- **Kernel.Diagnostics README:** new section about `DiagnosticData` and AOT; `ErrorReporter` described as instance; updated `ErrorCode`, `DefaultErrorMessageProvider`, examples.

## [0.7.1] - 2026-10-30

### Added
- `IdStateExtension` — extension class for converting `int` to `IdState`.
- New `HostStopped` member in `BiosStatus` — sent to the processor when `Stop()` is called on `Device` or `DeviceContext`.

### Changed
- **Sandbox:** the sandbox policy on the host is partially implemented; out of the box, the Nantuc VM runs only in a fully isolated sandbox — it can only send data and receive it on request via `IN`/`OUT`. The definition of “sandbox” in the root README has been corrected.
- `ResetMemoryRam` now passes the message `"Cannot clear RAM while device is running. Call Stop() first."` to `ThrowIfRunning()` instead of the default “Cannot launch program while device is running…”.

### Fixed
- `InstructionDecode.HasNeed64IntData(Opcode opcode)` previously ignored `LOAD_UNSAFE` and `STORE_UNSAFE` — they are now included in the table.
- XML documentation for the `VmHostProject` class has been fixed and supplemented.

### Documentation
- More detailed XML documentation has been added to `AlignmentExtensions`.
- **Root README:** code metrics updated to current values.

## [0.7.0] - 2026-10-29

### Added
- The NANASM compiler distinguishes `.data` and `.code` segments.
- `Nantuc.VmApplication.Unsafe` — new classes for obtaining `Kernel` objects.
- Full support for WASM and Native AOT.
- New tests:
  - check that the ID returned with invalid data is invalid;
  - check that `PortControllerBase` is safe on double `Dispose`;
  - check that when a device is removed from the system, its `Dispose` is called and the device is removed from the system.

### Changed
- Changelog started; the project version is fixed as 0.7.0.

### Fixed
- `HaltWakeTests` (`DebugRun_ShouldStopAfterStepLimit`, `ReleaseRun_ShouldStopAfterStepLimit`): fixed a bug where the property returned `StepCount` one greater than expected.

### Documentation
- Corresponding changes in the READMEs of affected sections.

> The changelog has been maintained since 2026-10-29; before that date, history is available only in commits.
