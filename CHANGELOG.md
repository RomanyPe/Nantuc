# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

Order of sections within a version:
**Added → Changed → Deprecated → Removed → Fixed → Documentation**.

Short labels:
- **BREAKING** — a backward-incompatible change; may break user code when upgrading.

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
	- `Debugger` — step-by-step execution on top of `DeviceContext`: `Step`/`StepOver`/`StepOut`/`Continue`/`ContinueTo`/`RunToEnd`, breakpoints (`AddBreakpoint`/`RemoveBreakpoint`/`ClearBreakpoints`/`HasBreakpoint`), `RequestStop()` (the only thread-safe method — `volatile bool`), `Wake()` from `HALT` (via `ctx.Resume()`), RAM reads (`ReadRam`/`CopyRamTo`/`ReadByte`/`ReadUInt16`/`ReadUInt32`/`ReadUInt64`/`ReadChar`/`ReadCString`), disassembly (`CurrentInstruction`/`DisassembleAt`/`DisassembleProgram` via `StringDisassembler`), best-effort call stack (`GetCallStack`), `Stopped` event. Does not use `AsRawDevice`/`unsafe` — only the public `DeviceContext` API (`RunStepMode`/`SingleStep`/`GetRegistersSnapshot`/`NativeReadOnlyBuffer`).
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
- added file [`docs/error-code.md`](docs/error-code.md) with a table “code → arguments → template”
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
- **BREAKING:** `ThrowCompiler` — instance class (previously `static`).
- **BREAKING:** `IErrorMessageProvider.GetMessage` and `ThrowCompiler.Throw` accept `ReadOnlySpan<DiagnosticData>` (previously `object`).
- `DefaultErrorMessageProvider` merges `overrides` on top of the standard templates (previously replacement).
- AST nodes have been split into separate files in `C/AST/`.
- `RegisterAllocationResult` has been moved to a separate file.
- More AOT-friendly: `Enum.ToString()` removed from diagnostics, `DiagnosticData` without boxing.

### Removed
- **BREAKING:** `ValidateAllCodesCovered` / `TryValidateAllCodesCovered`

### Documentation
- **Compiler README:** new sections “Constness (`const`)” and “Diagnostics: `DiagnosticData`”; updated tree, API table, pipeline, “What to fix”, “What has been done”.
- **Kernel.Diagnostics README:** new section about `DiagnosticData` and AOT; `ThrowCompiler` described as instance; updated `ErrorCode`, `DefaultErrorMessageProvider`, examples.

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