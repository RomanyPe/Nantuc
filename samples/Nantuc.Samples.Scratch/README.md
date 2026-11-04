# Nantuc.Samples.Scratch — README

> A sandbox for experiments on the Nantuc platform: an allocator test (`Program.cs`), a "Snake" game with a monochrome monitor and keyboard (`SnakeGame/`), and ready-made libraries `monitor.nanasm` / `input.nanasm` / `halt.nanasm` / `snake.minic`.
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

`Nantuc.Samples.Scratch` is a **collection of experiments** (a scratchpad), not a single example. The project contains two independent scenarios, switched by a command-line argument:

1. **The "Snake" game** — launched with the flag `-snake <period>` (e.g., `dotnet run -- -snake 150`). The class `SnakeGameOnNantuc` creates a `VmHost`, registers a `MonochromeMonitor` (32×16) on `sector 1`, the VM on `sector 0`, compiles `LibCode.snake_minic` (includes `monitor.nanasm`, `input.nanasm`, `halt.nanasm`), and starts the VM. The game runs in an infinite loop `while (1) { Halt(); tick(); render(); }`; a `Timer` wakes the processor every `period` ms (`portBus.WakeProcessor(0)`), and the keyboard is read on a background thread and written to `portBus.WritePort(0, code)`.
2. **The allocator test** — launched by default (without arguments). It compiles a MiniC program that does `alloc`/`free`/`alloc` via `Libraries.alloc_minic`, checks the return code (`return 1..7` for various failures, `return 10` for success), starts the VM, takes registers, and disassembles RAM via `StringDisassembler`.

**How to run:**

```bash
# Game (150 ms per tick)
dotnet run -- -snake 150

# Allocator test
dotnet run
```

There is also a `Game` profile in `launchSettings.json` (`-snake 150`), so from the IDE it is enough to select it — `dotnet run --launch-profile Game`.

The project shows how `VmApplication` + `Extensions` can be used to assemble a **real game**: multiple devices on the port bus, background input, a timer ticker, ANSI rendering to the console, and HALT/WAKE for an energy-efficient game loop.

## Local project tree

Namespace `Nantuc.Samples.Scratch` (root) + `Nantuc.Samples.Scratch.SnakeGame` (nested folder):

```
Nantuc.Samples.Scratch/
├── Nantuc.Samples.Scratch.csproj   # net10.0, Exe, ref → VmApplication.Extensions
├── Program.cs                       # top-level: -snake dispatch + allocator test
├── launchSettings.json              # profiles Default / Game (-snake 150) / WSL
└── SnakeGame/
    ├── KeyboardDevice.cs            # IPortController: 2 ports (REG_KEY, REG_HAS_NEW)
    ├── LibCode.cs                   # const string: monitor_nanasm, input_nanasm, halt_nanasm, snake_minic
    ├── MonochromeMonitor.cs         # IPortController: 32×16 framebuffer, 7 ports
    └── SnakeGameOnNantuc.cs         # SnakeGameOnNantuc(period).Run() — launches the snake
```

Organization notes:
- `Program.cs` — top-level statements with CLI dispatch: `-snake <period>` → `SnakeGameOnNantuc`, otherwise — the allocator test. For a sandbox project this is fine, but as experiments grow it is worth moving each one into a separate `Sample*.cs` with an explicit `public static void Run()`.
- `KeyboardDevice.cs` is **not used** by `SnakeGameOnNantuc` in the current version — input goes through `portBus.WritePort(0, code)` (loopback into the `Device`'s own IO window). See [What to fix, item 6](#what-to-fix).

## API

There is no public API — it is an `Exe`. Below is the composition of the experiments.

### `Program.cs` — CLI dispatch and allocator test

Top-level statements. The first block is the dispatch:

```csharp
if (args.Length >= 2 && args[0] == "-snake")
{
    int period = int.TryParse(args[1], out int res) && res > 0 ? res : 200;
    var game = new SnakeGameOnNantuc(period);
    game.Run();
    return;
}
```

If `-snake` is not passed (or there is only one argument), the allocator test runs:

| Step | What it does |
|---|---|
| `source1` | MiniC: `#include "alloc.minic"`, `int main()` with 5 alloc/free scenarios, `return s` (10 on success) |
| `using var host = VmHostFactory.CreateDefault();` | `RamFileService` + default ports |
| `host.Project.SaveFileProject("main.minic", source1)` | Source in `ProjectPath` |
| `SaveFileInclude("heap.nanasm", Libraries.heap_nanasm)` | asm-lib (bump + free-list) |
| `SaveFileInclude("alloc.minic", Libraries.alloc_minic)` | C-lib (first-fit allocator) |
| `var lfPolicy = new LoggingFaultPolicyMinimal(Console.Out);` | Policy that logs `[FAULT]` on failure and stops the simulation |
| `DeviceSpec.Create(ramSize: RamSize.MB128, processorFaultPolicy: lfPolicy, name: "ConsoleVM")` | VM + fault policy |
| `QueuedIOStream(Console.Out, PortCharEncoding.Utf8)` | I/O on `sector 1` (not used in `source1`) |
| `CompileToIL(baseAddress: 0, optimize: true).Build()` | Compiles all files |
| `RegisterDevice(spec, sector: 0)` / `RegisterDevice(ioDevice, "console", sector: 1)` | Registers the VM and the I/O |
| `GetDeviceContext(deviceId)!` | `DeviceContext` for the VM |
| `TryLoadProgram(program, loadAddress: 0, out error)` → `Run()` | Load and run |
| `GetRegistersSnapshot()` | Dump of `r0..r5`, `rSP`, `rHP` |
| `GetRamAsNewArray(0, hpPrt)` + `StringDisassembler.Disassemble(ramSnap, resCompile.CodeSize)` | RAM disassembly up to the current `rHP` |

`source1` returns an `int` (`return s;` — 10 on success), but `Run()` returns a `ProcessorFault`, and the return code is in `r0`. `Program.cs` prints `r0` in the register dump but **does not check** that it equals 10. See [What to fix, item 3](#what-to-fix).

### `SnakeGame/SnakeGameOnNantuc` — launching the snake

```csharp
public class SnakeGameOnNantuc(int period = 200)
{
    private readonly StringBuilder sb = new();
    private readonly int _period = period;

    public void Run();
    private void RenderToConsole(byte[] fb, int w, int h);
}
```

The sequence in `Run()`:

1. `VmHostFactory.CreateDefault()`.
2. `SaveFileProject("snake.minic", …)` / `SaveFileInclude` for `monitor.nanasm`, `halt.nanasm`, `input.nanasm`.
3. `MonochromeMonitor(32, 16, RenderToConsole)`.
4. `DeviceSpec.Create(ramSize: RamSize.MB16, name: "SnakeVM")` → `RegisterDevice(handle, sector: 0)`.
5. `RegisterDevice(monitor, name: "monitor", sector: 1)`.
6. `GetDeviceContext(deviceId)!`.
7. `CompileToIL(0, optimize: false).Build()`, `TryLoadProgram`.
8. `inputThread` (background): `Console.ReadKey(intercept: true)` → arrows into `1..4`, `Q`/`Escape` → `running = false; ctx.Stop()`. Writes `portBus.WritePort(0, code)`.
9. `Timer(..., null, 200, _period)` — `portBus.WakeProcessor(0)` every `_period` ms.
10. `ctx.Run()` (blocks until `Stop`).
11. `finally` — `running = false`, cursor visible, ANSI reset, cursor to line 18.

**Difference from the allocator test:** the snake does **not** pass a `faultPolicy` to `DeviceSpec.Create`. Faults simply lead to the VM stopping, but are not logged.

### `SnakeGame/MonochromeMonitor` — monochrome 32×16 display

```csharp
public sealed class MonochromeMonitor : PortControlBase
{
    public const int REG_CURSOR_X = 0;
    public const int REG_CURSOR_Y = 1;
    public const int REG_PIXEL    = 2;
    public const int REG_COMMAND  = 3;
    public const int REG_WIDTH    = 4;
    public const int REG_HEIGHT   = 5;
    public const int REG_FRAME    = 6;

    public const byte CMD_CLEAR = 1;
    public const byte CMD_FILL  = 2;
    public const byte CMD_FLUSH = 3;
    public const byte CMD_HOME  = 4;

    public MonochromeMonitor(int width, int height, Action<byte[], int, int>? renderer = null);

    public int Width { get; }
    public int Height { get; }

    public override byte ReadPort(ulong offset);
    public override void WritePort(ulong offset, byte value);
    public override void WakeProcessor();
}
```

- `width`/`height` — 1..255, otherwise `ArgumentOutOfRangeException`.
- Inside — `byte[] _fb` (`width * height`), cursor `_cx`/`_cy`, `_pixel`, `_frame`.
- `WritePort(REG_PIXEL, value)` writes `value` to `_fb[_cy * Width + _cx]` and remembers `_pixel`.
- `WritePort(REG_COMMAND, CMD_FLUSH)` calls `_renderer(_fb, Width, Height)` — the callback receives a **reference to the live buffer** (not a copy). If the renderer runs long while the VM writes — data race.
- `ReadPort(REG_FRAME)` — for synchronizing the render with the VM (frame number).

`SnakeGameOnNantuc.RenderToConsole` — the renderer that draws `fb` via ANSI (`\x1b[H` + `██`/`  `), plus the hint `Arrows=move Q=quit`.

### `SnakeGame/KeyboardDevice` — keyboard (not used)

```csharp
public sealed class KeyboardDevice : PortControlBase
{
    public const byte REG_KEY      = 0x00;
    public const byte REG_HAS_NEW  = 0x01;

    public const byte KEY_NONE  = 0;
    public const byte KEY_UP    = 1;
    public const byte KEY_DOWN  = 2;
    public const byte KEY_LEFT  = 3;
    public const byte KEY_RIGHT = 4;

    public void Press(byte key);
    public override byte ReadPort(ulong offset);
    public override void WritePort(ulong offset, byte value);
    public override void WakeProcessor();
}
```

- `Press(key)` — writes `_key = key; _hasNew = true`.
- `ReadPort(REG_KEY)` → `_key`. `ReadPort(REG_HAS_NEW)` → `1` if there is a new press, `0` otherwise (consume-on-read).
- `WritePort(REG_KEY, 0)` — resets `_key = 0`.
- In the current `SnakeGameOnNantuc` it is **not registered**: input goes directly through `portBus.WritePort(0, code)` into the `Device`'s loopback window. See [Special information](#special-information).

### `SnakeGame/LibCode` — sources

```csharp
public static class LibCode
{
    public const string monitor_nanasm;   // func_MonClear, func_MonFlush, func_MonPlot
    public const string input_nanasm;     // func_ReadKey (reads port 0, resets to 0)
    public const string halt_nanasm;      // func_Halt (HALT; RET)
    public const string snake_minic;      // #include monitor/input/halt + the game itself
}
```

`monitor_nanasm` (monitor ports via `0x10..0x13`):
- `MonClear` — `OUT 1` to port `0x13` (`CMD_CLEAR`), then `OUT 4` (`CMD_HOME`).
- `MonFlush` — `OUT 3` to `0x13` (`CMD_FLUSH`).
- `MonPlot(r0=x, r1=y, r2=v)` — `OUT r0, 0x10` (`REG_CURSOR_X`), `OUT r1, 0x11` (`REG_CURSOR_Y`), `OUT r2, 0x12` (`REG_PIXEL`).

`input_nanasm` (`func_ReadKey`):
```
LDI r1, 0x00;
IN  r0, r1;          // read port 0x00
LDI r3, 0x00;
CMP r0, r3;
JZ  no_key;
MOV r3, r0;          // saved the code
LDI r0, 0x00;
LDI r1, 0x00;
OUT r0, r1;          // wrote 0 to port 0 (reset)
MOV r0, r3;          // returned the code
no_key:
RET;
```

`snake_minic` — the game:
- State (`sx[256]`, `sy[256]`, `slen`, `dir`, `foodX`, `foodY`, `rngState`) — in **globals**, not locals. This is legacy from a codegen bug (see [Special information](#special-information)); the bug is fixed, migration to locals is a task for the next iteration.
- `rng()` — LCG `rngState * 1103515245 + 12345`, returns `(rngState >> 16) & 0x7FFF`.
- `spawnFood` — tries `rng() % 32`, `rng() % 16` until it hits a free cell.
- `resetGame` — `slen = 3`, `dir = 4` (right), `sx[0..2] = 16,15,14`, `sy = 8`, `rngState = 12345`.
- `render` — `MonClear`, draws food, draws the snake, `MonFlush`.
- `tick` — `ReadKey`, changes `dir` (with protection against a 180° reversal), moves the head, wraps at the edges (32×16 toroid), checks collision with itself → `resetGame`, otherwise shifts the tail, `slen++` if food was eaten.
- `main` — `resetGame(); render(); while (1) { Halt(); tick(); render(); }`.

`Halt()` — `HALT; RET`. The processor goes to sleep, the ticker wakes it via `portBus.WakeProcessor(0)`.

## API examples

**Launch the snake via CLI:**

```bash
dotnet run -- -snake 150       # 150 ms per tick
dotnet run -- -snake 100       # 100 ms per tick (10 FPS, harder)
dotnet run -- -snake 50        # 50 ms per tick (20 FPS, even harder)
```

**Launch the snake from code:**

```csharp
using Nantuc.Samples.Scratch.SnakeGame;

new SnakeGameOnNantuc(period: 200).Run();
```

**Add a monitor to your own example:**

```csharp
using Nantuc.Samples.Scratch.SnakeGame;

var monitor = new MonochromeMonitor(64, 32, (fb, w, h) =>
{
    for (int y = 0; y < h; y++)
    {
        for (int x = 0; x < w; x++)
            Console.Write(fb[y * w + x] != 0 ? '#' : '.');
        Console.WriteLine();
    }
});

using var host = VmHostFactory.CreateDefault();
int devId = host.Emulator.RegisterDevice(DeviceSpec.Create(RamSize.MB16, name: "VM"), sector: 0);
host.Emulator.RegisterDevice(monitor, name: "monitor", sector: 1);
// ... compilation and loading of a program that writes to ports 0x10..0x16
```

**Allocator test with return-code checking:**

```csharp
// in Program.cs after deviceCtx.Run():
var regs = deviceCtx.GetRegistersSnapshot();
ulong ret = regs[RegType.r0];
if (ret != 10)
{
    Console.Error.WriteLine($"Allocator test FAILED: r0={ret} (expected 10)");
    return;
}
Console.WriteLine("Allocator test PASSED");
```

## How close the module is to the user

**Medium.** This is a sandbox, but it is closer to a "finished product" than `Nantuc.Samples.Minimal`: here there is a playable snake, devices (monitor, keyboard), background input, and a timer. For the author of their own games/demos — a direct example; for the kernel author — a test polygon for new features.

**What you should do:**
- Before launching the snake — make sure the terminal supports ANSI (`\x1b[H`). Windows Terminal — yes, old `cmd.exe` — no.
- Run `dotnet run -- -snake <period>` (or the `Game` profile from `launchSettings.json`).
- For debugging the snake — increase `period` (slower, easier to catch bugs) or replace `QueuedIOStream` with `SynchronousIOStream` in the allocator test.
- Use `LoggingFaultPolicyMinimal` (as in the allocator test) — on a fault you will see `[FAULT] status=...`.

**What you should not do:**
- Register `KeyboardDevice` and simultaneously write to `portBus.WritePort(0, ...)` — double input.
- Change `portWrite = 0x10` in `snake_minic` without recalculating `DevicePortSize` (see [Special information](#special-information)).
- Expect `ctx.Run()` to return on its own — the snake runs in `while (1)`, exit only via `Q`/`Escape` (which call `ctx.Stop()`).
- Run the snake in CI/headless — `Console.KeyAvailable`/`Console.ReadKey` will throw `InvalidOperationException` when input is redirected.

## Special information

### The snake and the codegen bug

`LibCode.snake_minic` keeps **all state in globals** (`sx`, `sy`, `slen`, `dir`, `foodX`, `foodY`, `rngState`). This is legacy from a bug in `FunctionGenerator.GenerateFunction` (`[Nantuc.Compiler](../../src/Nantuc.Compiler/README.md)`): asymmetric push/pop of registers corrupted callee-saved registers with 3–4 local variables.

**The bug is fixed.** Therefore `rng()`, `occupied()`, `spawnFood()`, `tick()`, `render()` **could** use locals. The current code has not been migrated yet — this is a task for the next iteration. When writing new samples with 3+ locals in a function, you no longer need to be afraid.

### Input: loopback into the `Device`'s own IO window

`SnakeGameOnNantuc` does **not** register `KeyboardDevice` on a separate sector. Instead:

- `portBus.WritePort(0, code)` — writes to `Device._ioMemory[0]` (the VM itself is registered on `sector 0`, `Device` implements `IPortController`, `ResolveAddress(0)` → `_devices[0]` = `Device`, `Device.WritePort(0, code)`).
- `input_nanasm` does `IN r0, r1` with `r1 = 0x00` → `portBus.ReadPort(0)` → `Device.ReadPort(0)` → returns `_ioMemory[0]`.

That is, **the VM is its own keyboard through its own IO window**. This works because `Device` is registered both in `ManagerDevices` (as a VM) and in `PortBus._devices` (as an `IPortController`). But:

- `KeyboardDevice` remains dead code.
- `portBus.WritePort(0, code)` returns a `BiosStatus` (`NullDeviceOutput` if sector 0 is not registered) — `SnakeGameOnNantuc` ignores the result. If the VM is not registered on sector 0, input is silently lost.
- `IN` in `input_nanasm` reads and immediately resets (`OUT r0, r1` with `r0=0`). If two presses arrive between VM reads — the second overwrites the first.

### 32×16 toroid and monitor ports

`snake_minic` uses wrap at the edges (`nx < 0 → 31`, `nx > 31 → 0`, `ny < 0 → 15`, `ny > 15 → 0`) — the snake teleports to the opposite edge. Classic Snake, not a bug.

Monitor ports in `monitor_nanasm`: `0x10` (CURSOR_X), `0x11` (CURSOR_Y), `0x12` (PIXEL), `0x13` (COMMAND). With the default `DevicePortSize.B16` (`PortsOnDevice = 16 = 0x10`), `sector 1` starts at `1 * 16 = 0x10`. If you change `portsPerDevice` in `CreateDefault` (e.g., to `B32`), the base of `sector 1` becomes `0x20`, and all `OUT r0, 0x10` will go to **sector 0** (the VM itself), not to the monitor. This is a fragile spot tied to the default `DevicePortSize`. See [What to fix, item 8](#what-to-fix).

### `QueuedIOStream` in the allocator test — not used

`Program.cs` (allocator path) creates `QueuedIOStream(Console.Out, PortCharEncoding.Utf8)` and registers it on `sector 1`, but `source1` (allocator test) **does not write** to ports — only `alloc`/`free`/`Store64`/`Load64`. The I/O device just hangs there. This is not an error (does not interfere), but it is superfluous for the allocator test.

### `hpPrt` — a typo in `Program.cs`

In `Program.cs` the variable is called `hpPrt` (an extra `r`):
```csharp
var hpPrt = regs[RegType.rHP];
var ramSnap = deviceCtx.GetRamAsNewArray(0, (nuint)hpPrt);
```
It does not affect functionality, but it is worth renaming to `hpPtr`.

### ANSI and `Console`

`SnakeGameOnNantuc.RenderToConsole` uses `\x1b[H` (home) and `\x1b[0m` (reset). `Console.CursorVisible = false` — does not work in all terminals (in some it throws `IOException`, but the code does not catch it). `Console.KeyAvailable`/`Console.ReadKey` — in headless/redirected can throw. `SnakeGameOnNantuc` does not check `Console.IsInputRedirected`/`Console.IsOutputRedirected`.

### `KeyboardDevice` — dead code

`KeyboardDevice.Press(byte key)` is public, but is never called. `SnakeGameOnNantuc` writes directly to `portBus.WritePort(0, code)`. The class is left as a groundwork for the future (or as an alternative to the loopback approach). If you register it — do not forget that `ReadPort(REG_KEY)` returns `_key` without a reset, and `ReadPort(REG_HAS_NEW)` — consume-on-read. In `input_nanasm` the reset is done manually (`OUT 0, 0`), which with `KeyboardDevice` would be redundant.

### Platform limitations

- **`net10.0`**, no `AllowUnsafeBlocks` — the module does not touch native memory.
- **`Console.KeyAvailable`/`Console.ReadKey`** — only in an interactive terminal. In WASM/CI/headless — `InvalidOperationException`.
- **`Console.CursorVisible`/`Console.SetCursorPosition`** — may throw when output is redirected.
- **ANSI (`\x1b[...`)** — Windows Terminal, Linux, macOS — work. Old `cmd.exe` (without VT processing) — will not display.
- **`Timer` + background thread** — in WASM `Timer` works, but a `Thread` with `Console.ReadKey` makes no sense. For a WASM port of the snake, a different input is needed (e.g., `KeyboardDevice` over browser `input` events).

## What to fix

In descending order of severity. References to files and lines.

1. **`SnakeGameOnNantuc.Run` — `int deviceId = host.Emulator.RegisterDevice(...)` without checking for a negative ID.**
   File: `SnakeGame/SnakeGameOnNantuc.cs`, line `int deviceId = host.Emulator.RegisterDevice(handle, sector: 0);`.
   If sector 0 is occupied (re-launch), `RegisterDevice` returns `-2`, `GetDeviceContext(-2)` returns `null`, and `!` (the line `var ctx = host.Emulator.GetDeviceContext(deviceId)!;`) gives an NRE.
   Fix: `if (deviceId < 0) { Console.Error.WriteLine($"RegisterDevice failed: {deviceId}"); return; }` before `GetDeviceContext`. Or — switch to `TryRegisterDevice` + `IdState.Valid` (see item 2).

2. **`Program.cs` (allocator path) — also `RegisterDevice` + `GetDeviceContext(deviceId)!`.**
   File: `Program.cs`, lines `int deviceId = host.Emulator.RegisterDevice(spec: handle, sector: 0);` + `var deviceCtx = host.Emulator.GetDeviceContext(deviceId)!;`.
   The same problem as item 1, plus **a divergence from `Nantuc.Samples.Minimal`**, where `TryRegisterDevice`/`TryGetDeviceContext` with `IdState` is already used. This confuses the reader: two samples show different APIs for the same task.
   Fix: bring both samples to the new API (`TryRegisterDevice` → `IdState.Valid`, `TryGetDeviceContext` → `bool`), as in the Minimal README.

3. **`Program.cs` — the return code `r0` is not checked.**
   File: `Program.cs`, lines after `deviceCtx.Run()`:
   ```csharp
   var regs = deviceCtx.GetRegistersSnapshot();
   Console.WriteLine($"{RegType.r0.RegName()}: {regs[RegType.r0]}");
   ```
   `source1` returns an `int` (`return s; // 10`), but `Run()` returns a `ProcessorFault`, not the return code. The actual result is in `r0`, but `Program.cs` only prints it, does not compare it. The test "passes" even if `r0 != 10`.
   Fix: `if (regs[RegType.r0] != 10) { Console.Error.WriteLine($"FAILED: r0={regs[RegType.r0]} (expected 10)"); return; }` — e.g., immediately after `Run()`, before the dump.

4. **`SnakeGameOnNantuc` — `portBus.WritePort(0, code)` without checking the result.**
   File: `SnakeGame/SnakeGameOnNantuc.cs`, line `portBus.WritePort(0, code);` in `inputThread`.
   `IPortBus.WritePort` returns a `BiosStatus`. If `sector 0` is not registered (see item 1), `NullDeviceOutput` is returned, and the input is silently lost.
   Fix: `var st = portBus.WritePort(0, code); if (st != BiosStatus.Success) { /* log or running = false */ }`.

5. **`SnakeGameOnNantuc` — the class does not validate `period`.**
   File: `SnakeGame/SnakeGameOnNantuc.cs`, constructor `SnakeGameOnNantuc(int period = 200)` and field `_period`.
   `Timer(..., null, 200, _period)` will throw `ArgumentOutOfRangeException` if `_period <= 0`. `Program.cs` validates `period > 0` on CLI dispatch, but a **direct call** `new SnakeGameOnNantuc(0).Run()` will throw.
   Fix: `if (period <= 0) throw new ArgumentOutOfRangeException(nameof(period), "period must be > 0");` in the constructor — then the class contract is self-contained and `Program.cs` need not validate.

6. **`SnakeGameOnNantuc` — `KeyboardDevice` is not registered, input goes through loopback.**
   File: `SnakeGame/SnakeGameOnNantuc.cs` + `SnakeGame/KeyboardDevice.cs`.
   `KeyboardDevice` is implemented, but only `monitor` is registered in `Run()`. Input — through `portBus.WritePort(0, code)` (loopback into `Device._ioMemory[0]`). It works, but `KeyboardDevice` is dead code, and the loopback is non-obvious to the reader.
   Fix: either register `KeyboardDevice` on `sector 2` and change `input_nanasm` to read from `0x20` (the base of `sector 2` with `B16`), or document in a comment that `sector 0` is a loopback and `KeyboardDevice` is an alternative for the future.

7. **`SnakeGameOnNantuc` — `catch (Exception ex) when (running)` does not catch exceptions after `running = false`.**
   File: `SnakeGame/SnakeGameOnNantuc.cs`, line `catch (Exception ex) when (running) { Console.WriteLine($"\nVM error: {ex.Message}"); }`.
   If `ctx.Run()` throws after `inputThread` has set `running = false` (race), the exception will not be caught → the process will crash.
   Fix: remove `when (running)` or use `catch (Exception ex) { if (running) Console.WriteLine(...); }`.

8. **`SnakeGameOnNantuc` — monitor ports (`0x10..0x13`) are hardcoded for `DevicePortSize.B16`.**
   File: `SnakeGame/LibCode.cs`, `monitor_nanasm` + `SnakeGame/SnakeGameOnNantuc.cs` (`VmHostFactory.CreateDefault()` with no parameters).
   The base of `sector 1` = `1 * PortsOnDevice`. With the default `B16` = `0x10` — it matches. If `portsPerDevice` changes — it will break.
   Fix: either document in a comment in `SnakeGameOnNantuc` ("requires DevicePortSize.B16, sector 1 → base 0x10"), or pass `portsPerDevice: DevicePortSize.B16` explicitly in `CreateDefault`, or compute the base in C# and pass it to `snake_minic` via a global.

9. **`MonochromeMonitor` — `_renderer(_fb, Width, Height)` passes the live buffer.**
   File: `SnakeGame/MonochromeMonitor.cs`, line `_renderer(_fb, Width, Height);` in `WritePort(REG_COMMAND, CMD_FLUSH)`.
   If the renderer is asynchronous or long-running while the VM continues to write to `_fb` (via `REG_PIXEL`), there will be a data race. In `SnakeGameOnNantuc` the renderer is synchronous (`Console.Write`), but the contract does not protect against it.
   Fix: either document `/// <summary>Renderer must not store the buffer reference; copy if needed</summary>`, or pass a copy (`(byte[])_fb.Clone()`), or introduce double buffering (front/back).

10. **`MonochromeMonitor` — `ReadPort(REG_PIXEL)` may throw `IndexOutOfRangeException`.**
    File: `SnakeGame/MonochromeMonitor.cs`, `REG_PIXEL => _fb[_cy * Width + _cx]`.
    `_cx`/`_cy` are clamped on write, but if `Width`/`Height` change (they cannot — `get;` without `set`), or if `_fb` is initialized with different sizes — OOB. Currently safe, but fragile.
    Fix: `if ((uint)_cx >= (uint)Width || (uint)_cy >= (uint)Height) return 0;` before the access.

11. **`MonochromeMonitor` — `_frame` is not reset in `CMD_CLEAR`.**
    File: `SnakeGame/MonochromeMonitor.cs`, `CMD_CLEAR` calls `Array.Clear(_fb); _cx = _cy = 0;`, but not `_frame = 0`.
    This is fine (frame is a global counter), but the reader may think that clear resets everything.
    Fix: either document `_frame` as "lifetime counter, not reset by CMD_CLEAR", or reset it.

12. **`LibCode.input_nanasm` — race on two presses between VM reads.**
    File: `SnakeGame/LibCode.cs`, `input_nanasm`: `IN r0, r1; ... OUT r0, r1;` (read + reset in one call).
    If the user presses two arrows faster than the VM reads (200 ms with `period: 200`), the second press overwrites the first in `_ioMemory[0]`.
    Fix: buffer in `KeyboardDevice` (queue), and have `input_nanasm` read from `REG_HAS_NEW`/`REG_KEY` (two ports). Or reduce `period` (faster tick — smaller race window).

13. **`Program.cs` (allocator path) — `QueuedIOStream` is created and registered but not used.**
    File: `Program.cs`, lines `var iostream = new QueuedIOStream(...)` + `host.Emulator.RegisterDevice(ioDevice: iostream, ...)`.
    `source1` (allocator test) does not write to ports. The device hangs there, `Dispose` will be called via `host.Dispose()`, but it is superfluous.
    Fix: remove `iostream` and `RegisterDevice(ioDevice: ...)` from `Program.cs` (or use it — e.g., `print8` the `r0` result).

14. **`Program.cs` — `using var host` without `Console.OutputEncoding`.**
    File: `Program.cs`, beginning. `LoggingFaultPolicyMinimal` writes `[FAULT] ...` (ASCII), but if you extend the output to Russian — mojibake on Windows CP866/1251.
    Fix (preventively): `Console.OutputEncoding = System.Text.Encoding.UTF8;` as the first line.

15. **`SnakeGameOnNantuc` — `Console.CursorVisible`/`SetCursorPosition` without try/catch.**
    File: `SnakeGame/SnakeGameOnNantuc.cs`, lines `Console.CursorVisible = false;`, `Console.CursorVisible = true;`, `Console.SetCursorPosition(0, 18);`.
    In redirected output/headless they may throw `IOException`. Then `finally` will not complete.
    Fix: `try { Console.CursorVisible = false; } catch (IOException) { }` (and similarly for the rest).

16. **`SnakeGameOnNantuc` — `RenderToConsole` uses an instance `StringBuilder`.**
    File: `SnakeGame/SnakeGameOnNantuc.cs`, `private readonly StringBuilder sb = new();`.
    It was `static` (race between instances), now it is an instance — this is **a step forward**. A theoretical risk remains: if two threads call `Run()` on **one** instance, there will be a race. Currently `Run()` is called once, but the class contract does not forbid it.
    Fix (low priority): either make `sb` local in `RenderToConsole` (`var sb = new StringBuilder(...)` — one allocation per frame), or document that `Run()` is not thread-safe.

17. **`Program.cs` — typo `hpPrt` (extra `r`).**
    File: `Program.cs`, `var hpPrt = regs[RegType.rHP];`.
    Fix: rename to `hpPtr`.