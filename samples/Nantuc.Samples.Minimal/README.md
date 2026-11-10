# Nantuc.Samples.Minimal — README

> A minimal working example of the Nantuc platform: compiles a MiniC program "Hello world. Привет мир", runs it on the virtual machine, and prints a UTF-16 string to `Console.Out` via a port device.
> Shows the full path "source → bytecode → VM → I/O" on a single screen.
> Depends on `[Nantuc.VmApplication.Extensions](../../src/Nantuc.VmApplication.Extensions/README.md)` (transitively — `VmApplication`, `Compiler`, `Kernel`).
> `[← Root](../README.md) · [VmApplication](../../src/Nantuc.VmApplication/README.md) · [Extensions](../../src/Nantuc.VmApplication.Extensions/README.md) · [Compiler](../../src/Nantuc.Compiler/README.md) · [Kernel](../../src/Nantuc.Kernel/README.md)`

## Table of contents
- [About this module](#about-this-module)
- [Local project tree](#local-project-tree)
- [API](#api)
- [API examples](#api-examples)
- [How close the module is to the user](#how-close-the-module-is-to-the-user)
- [Special information](#special-information)
- [What to fix](#what-to-fix)

## About this module

`Nantuc.Samples.Minimal` is a **single `Program.cs` file with top-level statements** (no `class Program`, no namespace). The project is an `Exe`, `net10.0`, and depends only on `VmApplication.Extensions`. This is the "hello world" for the platform: if you have never seen Nantuc, start here.

What the example demonstrates:

1. **`VmHostFactory.CreateDefault()`** — creating a `VmHost` (project + emulator) in one line.
2. **`SaveFileProject` / `SaveFileInclude`** — writing the source and include files to `RamFileService` (in-memory, no real file system). It uses `Libraries.print_minic` and `Libraries.io_nanasm` from `VmApplication.Extensions`.
3. **`DeviceSpec.Create(RamSize.MB128, name: "ConsoleVM")`** — a VM with 128 MiB RAM and the name `"ConsoleVM"`.
4. **`QueuedIOStream(Console.Out, PortCharEncoding.Utf16)`** — an asynchronous output device, UTF-16LE, registered on `sector 1`.
5. **`TryRegisterDevice`** — registers the VM on `sector 0` and the I/O on `sector 1`. Returns `IdState` instead of `int < 0`.
6. **`TryGetDeviceContext`** — obtains the `DeviceContext` without `!`.
7. **`CompileToIR(baseAddress: 0, optimize: true).Build()`** — compiles all files from `IFileService` into bytecode.
8. **`TryLoadProgram`** — loads the bytecode into the VM's RAM.
9. **`Run()`** — a blocking run until `END`/a fault. Returns `ProcessorFault` — the status is checked directly, without additional calls.

The source `main.minic` (MiniC):

```c
#include "print.minic"
char str[] = "Hello world. Привет мир";
void main() { print16(str); }
```

The string contains both ASCII and Cyrillic — this verifies that `print16` and the UTF-16LE path in `QueuedIOStream` work correctly with non-ASCII characters. `print.minic` (from `Libraries.print_minic`) includes `io.nanasm` and defines `print16`/`write16`/`flush`, which write to ports `0x10` (Data) and `0x12` (Flush). `QueuedIOStream` with `Utf16` assembles byte pairs into a `char` and writes to the `TextWriter`.

## Local project tree

One file:

```
Nantuc.Samples.Minimal/
├── Nantuc.Samples.Minimal.csproj   # net10.0, Exe, ref → VmApplication.Extensions
└── Program.cs                       # top-level statements, ~55 lines
```

No classes, namespaces, or helper methods — the whole example is in `Program.cs`.

## API

There is no public API — it is an `Exe`. Below is the execution flow so that the code can be read.

### Execution flow of `Program.cs`

| Step | Code | What it does |
|---|---|---|
| 1 | `using var host = VmHostFactory.CreateDefault();` | A `VmHost` with `RamFileService` + the default `PortBus` (`PortSize.KB64`, `DevicePortSize.B16`) |
| 2 | `host.Project.SaveFileProject("main.minic", source);` | Puts the source into `RamFileService` under `ProjectPath/main.minic` |
| 3 | `host.Project.SaveFileInclude("io.nanasm", Libraries.io_nanasm);` | Puts the asm lib into `IncludePath/io.nanasm` |
| 4 | `host.Project.SaveFileInclude("print.minic", Libraries.print_minic);` | Puts the C lib into `IncludePath/print.minic` |
| 5 | `host.Emulator.TryRegisterDevice(DeviceSpec.Create(RamSize.MB128, name: "ConsoleVM"), sector: 0, out int vmId)` | Registers the VM on `sector 0`; `IdState.Valid` — success, `vmId` — the id |
| 6 | `host.Emulator.TryRegisterDevice(new QueuedIOStream(Console.Out, PortCharEncoding.Utf16), name: "console", sector: 1, out _)` | Registers the I/O on `sector 1`; for a port device the name is passed as a parameter, for the VM — inside `DeviceSpec` |
| 7 | `host.Emulator.TryGetDeviceContext(vmId, out var vm)` | A `DeviceContext` for the VM without `!` and without the risk of an NRE |
| 8 | `host.Project.CompileToIR(baseAddress: 0, optimize: true).Build()` | Compiles everything from `IFileService` into a `CompilationResult` |
| 9 | `vm.TryLoadProgram(compilation.Program!, loadAddress: 0, out var error)` | Loads the bytecode into the VM's RAM; `false` + `out error` — failure |
| 10 | `ProcessorFault fault = vm.Run();` | Runs until `END`/a fault, returns `ProcessorFault` |
| 11 | `if (fault.Status != BiosStatus.EndProgram) ...` | Checks for successful completion |

`CompileToIR(ulong, bool)` — an extension from `[Nantuc.VmApplication.Extensions](../../src/Nantuc.VmApplication.Extensions/README.md)` (`ProjectExtension`). Internally it calls `ProjectBuilder.GetSourceFiles(proj.FileService, proj.Paths)` and `ProjectBuilder.Build`.

`TryRegisterDevice`/`TryGetDeviceContext` — the new emulator API, returning `IdState`/`bool` instead of "magic" negative ids and `null`. See the "`IdState` vs `int` / `null`" section below.

## API examples

This project **is** the example. Usage options:

**Replace `QueuedIOStream` with a synchronous one (easier to debug):**

```csharp
var iostream = new SynchronousIOStream(Console.Out, PortCharEncoding.Utf16);
```

**Replace UTF-16 with UTF-8 (then `print8` in `print.minic`):**

```csharp
var iostream = new QueuedIOStream(Console.Out, PortCharEncoding.Utf8);
```

and in `main.minic` call `print8(str)` instead of `print16(str)`. But `char` in MiniC is 16-bit, `print8` expects `char*` and does `write8(str[i])` — a 16→8 truncation. For ASCII (`"Hello world."`) it works, for Cyrillic (`"Привет мир"`) it does not: the characters U+041F…U+0440 do not fit in a byte. For non-ASCII, keep `Utf16`.

**Add completion diagnostics:**

```csharp
ProcessorFault fault = vm.Run(new LaunchOptions(
    StartAddress: 0,
    ShowTimer: true,
    OnEnd: r => Console.WriteLine($"steps={r?.Steps} elapsed={r?.Elapsed}")));
```

**Add `LoggingFaultPolicyMinimal` (optional):**

```csharp
using Nantuc.VmApplication.Extensions.Host;

var lfPolicy = new LoggingFaultPolicyMinimal(Console.Out);
var vmSpec = DeviceSpec.Create(RamSize.MB128, name: "ConsoleVM", faultPolicy: lfPolicy);
```

The policy logs the `ProcessorFault` to `Console.Out` and stops the simulation. In the current example it is **not used** — `Run()` already returns a `ProcessorFault`, which we check manually.

**Replace `RamFileService` with a real file system:**

```csharp
var fs = new DefaultFileService();
fs.SaveFile(Path.Combine(AppContext.BaseDirectory, "project", "main.minic"), source);
fs.SaveFile(Path.Combine(AppContext.BaseDirectory, "include", "io.nanasm"), Libraries.io_nanasm);
fs.SaveFile(Path.Combine(AppContext.BaseDirectory, "include", "print.minic"), Libraries.print_minic);

using var host = VmHostFactory.CreateDefault(fileService: fs);
```

## How close the module is to the user

**Maximum.** This is a teaching example — "copy, run, get Hello world + Привет мир". It is intended for:

- A new user of the platform who wants to see Nantuc in action.
- An extension author who needs a minimal skeleton for their own device or optimization.
- A CI build that checks the public API has not broken (if you add a test that runs `dotnet run` and compares stdout with `"Hello world. Привет мир"`).

**What you should do:**
- Run `dotnet run` in `Nantuc.Samples.Minimal` — the screen will print `Hello world. Привет мир` (UTF-16).
- Use it as a template for your own examples: copy the structure `SaveFileProject` → `TryRegisterDevice` → `TryGetDeviceContext` → `CompileToIR` → `TryLoadProgram` → `Run`.
- Check the `IdState` from `TryRegisterDevice` — this replaces the old `deviceId < 0` check.
- For debugging, replace `Run()` with `RunStepMode` + `SingleStep`, as in the `[VmApplication README](../../src/Nantuc.VmApplication/README.md)`.

**What you should not do:**
- Expect `optimize: true` to provide a speedup — without `AddStdRules()` the AST optimizer is empty (see [What to fix, item 2](#what-to-fix)).
- Change `portsPerDevice` in `VmHostFactory.CreateDefault(...)` without recalculating `portWrite`/`portFlush` in `print.minic` (see [Special information](#special-information)).
- Use `RamFileService.Combine` in your own paths — it uses `\\` on all OSes, which breaks `DefaultFileService` when mixed.
- Rely on `Console.Out` for Cyrillic on Windows without explicitly setting `Console.OutputEncoding` (see [What to fix, item 3](#what-to-fix)).

## Special information

### `IdState` vs `int` / `null`

Previously, device registration returned an `int`: `>= 0` — the id, `-2` — an error; and `GetDeviceContext(id)!` crashed with an NRE if the id was invalid. Now:

- `TryRegisterDevice(...)` returns `IdState` (`Valid` / `Invalid` / …); `out int id` is always filled.
- `TryGetDeviceContext(id, out vm)` returns `bool`.
- `TryLoadProgram(...)` returns `bool` with `out string error`.

This allows you to not rely on "magic" negative ids and not write `!` for `GetDeviceContext`.

### Ports: where `0x10` and `0x12` come from

`print.minic` (from `Libraries.print_minic`) contains:

```c
int portWrite = 0x10;
int portFlush = 0x12;
```

These are **absolute addresses** in the VM's port space, not offsets. `PortBus` (`[Nantuc.Kernel](../../src/Nantuc.Kernel/README.md)`) routes `address >> deviceShift` to the sector, `address & offsetMask` to the offset within the device. With the default `PortSize.KB64` and `DevicePortSize.B16`:

- `PortsOnDevice = 1 << 4 = 16` (bytes).
- `SectorCount = 1 << (16 - 4) = 4096`.
- Sector 1 → base `1 * 16 = 16 = 0x10`.
- `Data` (offset 0) → `0x10`, `Flush` (offset 2) → `0x12`.

If you change `portsPerDevice` in `VmHostFactory.CreateDefault` (e.g., to `DevicePortSize.B32` = 32), the base of sector 1 becomes `0x20`, and `portWrite = 0x10` lands in **sector 0** (the VM itself), and the output will disappear or break. This is a limitation of the example, not of the platform.

The addresses are placed in the global variables `portWrite`/`portFlush` so that they can be changed to match your sector topology.

### UTF-16 and `QueuedIOStream`

`QueuedIOStream` with `PortCharEncoding.Utf16` assembles byte pairs into a `char`:

```csharp
if (pendingLowByte is null) { pendingLowByte = b; /* buffered */ continue; }
char c = (char)(pendingLowByte.Value | (b << 8));
```

`print16` (in `print.minic`) writes the low byte first (`write8(c)`), the high byte second (`write8(c >> 8)`). The order matches UTF-16LE. `flush()` at the end writes `1` to the `Flush` port → `QueuedIOStream.Flush()` waits up to 2 sec for `_pendingCount` to become 0, and does `_writer.Flush()`.

The string `"Hello world. Привет мир"` verifies that:
- ASCII (`Hello world.`) goes as is (U+0048…U+0077, 2 bytes in UTF-16).
- Cyrillic (`Привет мир`, U+041F…U+0440) is also 2 bytes in UTF-16, but outside the ASCII range — `QueuedIOStream` writes the `char` directly to the `TextWriter`, and the decoding depends on the `Console.Out` encoding.

### `void main()` vs `int main()`

`main.minic` declares `void main()`. The MiniC compiler does not check the return type of `main` (`FunctionGenerator.GenerateFunction` only looks at `func.Name == "main"`), and in the epilogue of `main` it always does `EmitEnd()`. So `void main` and `int main` produce the same bytecode. For C style, `int main()` is better, but for the example `void main()` is shorter.

### `optimize: true` — no-op

`CompileToIR(0, optimize: true)` enables only AST optimizations. But `VmHostProject.Optimizator.VMAstOptimizer` is created empty — if `AddStdRules()` is not called, no rule will apply. In this example `optimize: true` is a "flag for the future". See [What to fix, item 2](#what-to-fix).

### `Run()` and `QueuedIOStream`

`vm.Run()` blocks the thread until `END`/a fault. `print16` at the end calls `flush()` → `QueuedIOStream.Flush()` synchronously waits for an empty queue. So by the time `Run()` returns, everything has been output to `Console.Out`. `host.Dispose()` (via `using`) then calls `ManagerDevices.Dispose` → `iostream.Dispose` → `_drainTask.Wait(5s)` (in case something remains). The order is safe.

### Platform limitations

- **`net10.0`**, no `AllowUnsafeBlocks` — the example does not touch native memory directly.
- **`QueuedIOStream`** uses `Task.Run` + `Channel<byte>` — it works in WASM, but the background drain may not finish before the end of a synchronous `Main`. For a WASM example, use `SynchronousIOStream`.
- **Cyrillic in `Console.Out`** — on Windows without `Console.OutputEncoding = Encoding.UTF8`, the string `"Привет мир"` may display as `????`. See [What to fix, item 3](#what-to-fix).
- **`RamFileService`** does not depend on the OS (in-memory), but `Combine` with `\\` is not a "native" path. For a real file system — `DefaultFileService` + `Path.Combine`.

## What to fix

1. **`QueuedIOStream` in WASM — background drain.**  
   File: `Nantuc.VmApplication.Extensions/Emulator/IO/QueuedIOStream.cs`. In WASM, `Task.Run` may not execute before the end of a synchronous `Main`, because there is no event loop for the background task.  
   Fix: for WASM examples use `SynchronousIOStream`, or document in `README`/`STDLIBRARIES` that `QueuedIOStream` requires an explicit `await`/`Dispose`.

2. **`optimize: true` without `AddStdRules()` — no-op.**  
   File: `Program.cs`. `CompileToIR(baseAddress: 0, optimize: true)` enables AST optimizations, but `VMAstOptimizer` is empty.  
   Fix: either call `host.Project.Optimizator.VMAstOptimizer.AddStdRules()` before compiling, or pass `optimize: false` so as not to mislead the reader.

3. **Cyrillic in `Console.Out`.**  
   File: `Program.cs`. On Windows without explicitly setting `Console.OutputEncoding`, the string `"Привет мир"` may display as `????`.  
   Fix: at the start of `Program.cs` add `Console.OutputEncoding = System.Text.Encoding.UTF8;` (before the first output), or document it in the README/platform notes.

4. **`void main()` in the example.**  
   File: `Program.cs`. It works, but for C style `int main()` is better.  
   Fix: replace with `int main() { print16(str); return 0; }` — more familiar to the reader.

5. **`LibraresIncludes` → `StdLibraries` — name synchronization.**  
   File: `Nantuc.VmApplication.Extensions/LibraresIncludes/LibraresForCompiler.cs`. The example already uses `using Nantuc.VmApplication.Extensions.StdLibraries;` and `Libraries.io_nanasm`. You need to make sure that in `VmApplication.Extensions` itself the namespace/folder/class are renamed to `StdLibraries`/`Libraries` (or, conversely, the example still uses the old path). The typos `Librares`/`Librare` — remove them in a single BREAKING commit.