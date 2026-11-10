# Nantuc.Cli — README

Command-line interface for the Nantuc platform: compile projects to bytecode, run programs in the virtual machine, disassemble binaries, and manage include libraries.

[← Root](../../README.md) · [Common](../Nantuc.Common/README.md) · [Contracts](../Nantuc.Contracts/README.md) · [Diagnostics](../Nantuc.Diagnostics/README.md) · [Kernel](../Nantuc.Kernel/README.md) · [Compiler](../Nantuc.Compiler/README.md) · [VmApplication](../Nantuc.VmApplication/README.md)

---

## Table of Contents

- [About this module](#about-this-module)
- [Installation](#installation)
- [Key components](#key-components)
- [Commands and options](#commands-and-options)
  - [compile](#compile)
  - [run](#run)
  - [disasm](#disasm)
  - [lib](#lib)
  - [help](#help)
- [Usage examples](#usage-examples)
- [Dependencies](#dependencies)
- [Implementation notes](#implementation-notes)

---

## About this module

`Nantuc.Cli` is the command-line entry point for the Nantuc platform. The module is a thin wrapper over the public API of `Nantuc.VmApplication` and provides four main commands:

- **compile** — compile project source files (MiniC / NANASM) into a binary image `program.bin`;
- **run** — load and execute a compiled program in the virtual machine emulator;
- **disasm** — disassemble a binary image back into a textual representation;
- **lib** — manage include libraries (add, remove, check existence, list).

The CLI contains no compilation or execution logic of its own — all operations are delegated to the corresponding services from `Nantuc.VmApplication` and `Nantuc.Compiler`. The module's job is to parse command-line arguments, configure the file service and project paths, invoke the required scenario, and print the result (or error) to the console.

The module targets `net10.0` and uses `Testably.Abstractions` for file system abstraction, which allows commands to be tested with `MockFileSystem`.

---

## Installation

The module is distributed as a **NuGet package** and is intended to be installed as a .NET global tool. The project has `<PackAsTool>true</PackAsTool>` enabled, so once the package is published it can be installed with:

```bash
dotnet tool install --global Nantuc.Cli
```

After installation, the `nantuc` command becomes available:

```bash
nantuc help
```

Update to the latest version:

```bash
dotnet tool update --global Nantuc.Cli
```

Uninstall:

```bash
dotnet tool uninstall --global Nantuc.Cli
```

> **Note:** installation requires the .NET SDK (or runtime) version 10.0 or newer, because the project targets `net10.0`.

---

## Key components

- **`CliApplication`** — the central command dispatcher. It receives an `IRibraryFileService`, an `IProjectFilesConfig`, and two `TextWriter` instances (output and error). In `Run`, it parses the first argument as the command name and passes the remaining arguments to the corresponding handler.
- **`ProjectsPathsConfig`** — an implementation of `IProjectFilesConfig`. It defines `BaseDirectory` (the current working directory), `ProjectPath` (same as `BaseDirectory`), `IncludePath` (set at construction; defaults to `AppContext.BaseDirectory/include`), and the extension lists for assembler and MiniC sources.
- **`FileSystemLibraryFileService`** — an implementation of `IRibraryFileService` on top of `IFileSystem`. It provides reading/writing of project files and includes, as well as library management (add, remove, existence check, list).
- **`CLIHelper`** — a set of static utilities for argument parsing: flag detection (`HaveFlag`), value extraction by flag (`TryGetArgByFlag`), and string-to-bytes conversion (`GetBytesFromText`).
- **Commands** (`CompileCommand`, `RunCommand`, `DisassembleCommand`, `LibCommand`, `ErrorCommand`) — encapsulate the logic of a specific scenario. Each command has a static `TryCreate` method (except `ErrorCommand`) that validates required arguments and creates an instance, or returns `false` and writes an error message.
- **`Program.cs`** — assembles the application: creates a `RealFileSystem`, a `ProjectsPathsConfig` with the `include` path next to the executable, a `FileSystemLibraryFileService`, and runs `CliApplication`.

---

## Commands and options

### compile

Compiles the project into a binary file `program.bin` and saves it to the specified directory.

**Required options:**
- `-s`, `-sv`, `-save <dir>` — the directory where `program.bin` will be written.

**Optional options:**
- `-o`, `-opt` — enable optimizations (AST + IR).

**Behavior:**
- Without the optimization flag, `Compile` is called and the result is saved as-is.
- With the optimization flag, `CompileToIR` is called, then `OptimizationIR` (5 passes) and `Build`.
- On compilation errors, prints them to the error stream and returns code `-1`.

**Example:**
```bash
nantuc compile -s bin -o
```

---

### run

Loads a binary file into the virtual machine emulator and executes it.

**Required options:**
- `-p`, `-prog <file>` — path to `program.bin` (or another image).

**Optional options:**
- `-ram`, `-ram-size`, `-r <size>` — RAM size (default `1MB`). Valid values: `64B` … `2GB`.
- `-ps`, `-port-size`, `-port <size>` — port size (default `1KB`). Valid values: `64B` … `512KB`.
- `-dps`, `-device-per-port`, `-device-on-port <size>` — ports per device (default `16B`). Valid values: `8B` … `256B`.
- `-l`, `-limit <N>` — maximum number of execution steps (default: unlimited).
- `-t`, `-timer`, `-lt` — print elapsed time and step count when the program finishes.
- `-dm`, `-dumbm`, `-dumb-memory <path-file>` — create a memory dump file; creates a folder at `<path-file>`, and inside it creates a file `dumb-memory-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.bin` containing only the information bytes
- `-dr`, `-dumbr`, `-dumb-registers <path-file>` — create a register dump file; creates a folder at `<path-file>`, and inside it creates a file `dumb-registers-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt` containing text in the form `r0: 0\n`

**Behavior:**
- Creates a `VmEmulatorBuilder` and configures port and RAM sizes.
- Registers the VM device (`DeviceSpec.Create(ram)`) and the I/O device `SynchronousIOStream`.
- Loads the program at address 0. On load failure, prints a message and returns `-1`.
- Runs the program with `LaunchOptions`. If the termination status is not `EndProgram`, prints fault information.
- Returns the value of register `r0` as an integer.

**Example:**
```bash
nantuc run -p bin/program.bin -ram 4MB -l 100000 -t
```

---

### disasm

Disassembles a binary image into a textual representation.

**Required options:**
- `-f`, `-file <file>` — path to the binary file.

**Optional options:**
- `-cs`, `-codes <N>` — number of bytes to disassemble (default: file size).
- `-s`, `-save <dir>` — save the result to `<dir>/result-decompile.txt` instead of printing to the console.

**Behavior:**
- Reads the file, converts text to bytes (Latin1), and calls `StringDisassembler.Disassemble`.
- If `savePath` is not set, prints the text to the console; otherwise saves the file and reports the path.

**Example:**
```bash
nantuc disasm -f bin/program.bin -cs 64 -s out
```

---

### lib

Manages include libraries.

**Actions:**

| Action   | Arguments | Description |
|----------|-----------|-------------|
| `add`    | `<name> <content>` or `-p <file>` | Add an include from a string or from a file. |
| `remove` | `<name>` | Remove an include by name. |
| `exist`  | `<name>` | Check whether an include exists. Returns exit code `0` (yes) or `1` (no). |
| `list`   | — | List all includes. |

**Examples:**
```bash
nantuc lib add -p ./extra.inc
nantuc lib add mylib "int foo() { return 42; }"
nantuc lib list
nantuc lib exist mylib
nantuc lib remove mylib
```

---

### help

Prints help for all commands and options.

```bash
nantuc help
```

---

## Usage examples

1. **Compile a project with optimizations:**
   ```bash
   nantuc compile -s ./bin -o
   ```

2. **Run a program with a step limit and timer:**
   ```bash
   nantuc run -p ./bin/program.bin -ram 8MB -ps 4KB -l 500000 -t
   ```

3. **Disassemble the first 128 bytes and save the result:**
   ```bash
   nantuc disasm -f ./bin/program.bin -cs 128 -s ./out
   ```

4. **Add an include from a file and check its existence:**
   ```bash
   nantuc lib add -p ./print.minic
   nantuc lib exist print.minic
   ```

---

## Dependencies

`Nantuc.Cli` references:

- **`Nantuc.VmApplication.Extensions`** — provides `VmHostProjectBuilder`, `VmEmulatorBuilder`, `CompilationResult`, `DeviceSpec`, `SynchronousIOStream`, `StringDisassembler`, and other types required for compilation, execution, and disassembly.
- **`Nantuc.Compiler`** (transitively via `VmApplication.Extensions`) — the MiniC compiler and assembler.
- **`Nantuc.Kernel`** (transitively) — the virtual machine kernel.
- **`Nantuc.Common`** — shared types (`RamSize`, `PortSize`, `DevicePortSize`, `BiosStatus`, `RegType`, etc.).
- **`Testably.Abstractions`** — file system abstraction for testability.

---

## Implementation notes

- **Default include path** — `AppContext.BaseDirectory/include`. The directory is created at startup if it does not exist.
- **Project and includes** — when reading a file, `FileSystemLibraryFileService` first looks in `IncludePath`, then in `ProjectPath` (the current working directory).
- **Encoding** — project and include files are read and written in UTF-8. Binary images are converted to/from strings using Latin1 encoding (byte-for-byte correspondence).
- **Return codes** — commands return `0` on success and negative values on errors (e.g., `-1` on compilation or execution failure, `-2` on invalid arguments, `-3` on an unknown command). `lib exist` returns `1` if the include is not found (this is a check result, not an error).
- **Optimization** — with `compile -o`, a `VmCompilerOptimizator` is used with the standard AST and IR rule sets, followed by 5 passes of `OptimizationIR`.
