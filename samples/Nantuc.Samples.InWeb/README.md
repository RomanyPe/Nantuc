Below is the English translation of the README.

---

Below is the updated `README.ru.md` taking into account the changes in `pages/Snake.razor`: the CPU is now registered as `AsyncDevice` (`RegisterAsyncDevice` / `AsyncDeviceContext`), and `BatchStep` and `EndStepMode` are called asynchronously.

---

# Nantuc.Samples.InWeb — README

> A demo module of the Nantuc platform, built as a **Blazor WebAssembly** application and running **in the browser**. It contains two pages: `/` — a universal runner for MiniC programs (compilation + execution in WASM, output via UART) and `/snake` — an interactive "Snake" game with a monochrome 32×16 monitor, controlled by keyboard arrows.
> Depends on `[Nantuc.VmApplication.Extensions](../../src/Nantuc.VmApplication.Extension/README.ru.md)` (transitively — `VmApplication`, `Compiler`, `Kernel`).
> Related to the console sample: [`Nantuc.Samples.Scratch`](../Nantuc.Samples.Scratch/README.ru.md) — the same snake "engine", but without a browser.
> [← Root](../README.ru.md), [VmApplication](../../src/Nantuc.VmApplication/README.ru.md), [Extensions](../../src/Nantuc.VmApplication.Extension/README.ru.md), [Compiler](../../src/Nantuc.Compiler/README.ru.md), [Kernel](../../src/Nantuc.Kernel/README.ru.md)

> [!IMPORTANT]
> Due to WebAssembly limitations, when using a synchronous `Device`, the multithreading flag must be enabled in the project file (`.csproj`); `AsyncDevice` does not have this problem, otherwise a `PlatformNotSupportedException` will occur at runtime when working with threads and locks:
> ```xml
> <PropertyGroup>
>   <WasmEnableThreads>true</WasmEnableThreads>
> </PropertyGroup>
> ```
> In `Snake.razor` the CPU now works through `AsyncDeviceContext`, but `MonochromeMonitor` and `SnakeCommander` are still registered as synchronous devices, so the flag is preserved in the project.

## Contents
- [About this module](#about-this-module)
- [Local project tree](#local-project-tree)
- [API](#api)
- [API examples](#api-examples)
- [How close the module is to the user](#how-close-the-module-is-to-the-user)
- [Special information](#special-information)
- [What to fix](#what-to-fix)

## About this module

`Nantuc.Samples.InWeb` is **two independent scenarios in one WASM build**, each on its own Blazor page:

1. **`/` (`pages/Home.razor`) — "Nantuc VM in the browser".** A universal runner: a text editor for MiniC code with syntax highlighting, and a "Compile and run" button. When pressed:
   - `VmHostFactory.CreateDefault()`;
   - `main.minic` + include libraries `io.nanasm` / `print.minic` are written into the project;
   - `TryRegisterDevice` (`DeviceSpec.Create(RamSize.MB16, ...)`) on `sector 0` — VM;
   - `TryRegisterDevice` (`SynchronousIOStream` over `StringWriter`, `PortCharEncoding.Utf16`) on `sector 1` — UART/console;
   - `host.Project.CompileToIR(baseAddress: 0, optimize: true).Build()`;
   - `vm.Run()` (not StepMode) — execution until `EndProgram` or until a fault;
   - `wr.ToString()` is returned to the page and rendered into `<pre>`.

   This way the module demonstrates a **"pure" synchronous scenario** of compilation/execution without a cooperative scheduler — in contrast to the snake.

2. **`/snake` (`pages/Snake.razor`) — "Snake" in the browser.** A full-fledged game:
   - the CPU/VM on `sector 0` is registered via `RegisterAsyncDevice` and works through `AsyncDeviceContext`;
   - `MonochromeMonitor` 32×16 on `sector 1`, `SnakeCommander` on `sector 2`;
   - `CompileToIR(0, optimize: false)` compiles `LibCode.snake_minic` (with `monitor.nanasm` and `commander.nanasm`);
   - `_ctx.RunStepMode(startAddress: 0)` — the VM is switched into **step mode** (not `Run()`), because the game works cooperatively;
   - the game loop `GameLoopAsync` — an `async` loop with `Task.Delay`, `FixedDtMs = 150`, `FrameBudgetMs = 8`, `StepChunk = 1000`, `MaxStepsPerTick = 200_000`;
   - every 150 ms: `_commander.SignalTick()` -> `await _ctx.BatchStep(StepChunk)` in a loop -> `_commander.ConsumeYielded()` -> `break`;
   - the frame (`CMD_FLUSH` in the monitor) is copied into `_frameBuffer` (`OnFrameProduced`), then passed to `snake.js` via `IJSRuntime`, and drawn on `<canvas>`;
   - keyboard input: `@onkeydown` on `div[tabindex=0]` -> `ConcurrentQueue<byte>` -> `_commander.SetInput(code)`;
   - Game Over: a 900 ms overlay, then `_showGameOver = false` (but see [What to fix, item 3](#what-to-fix)).

   In this version, `snake_minic` **does not use `Halt()`** — waiting for a tick is implemented via busy-poll `CmdWaitTick()` through `SnakeCommander.ReadPort(R_TICK_READY)`. This is a fundamental difference from `Nantuc.Samples.Scratch`: there `Halt()` + `WakeProcessor`, here — a cooperative pause through the port.

**How to run (WASM):**

```bash
# from the project directory
dotnet run
# open http://localhost:5xxx/        — /snake
# open http://localhost:5xxx/snake   — /snake
```

`dotnet publish` produces a static site — `wwwroot` can be served by any static server.

**How to run (dev-loop):** `dotnet watch run` — rebuild on changes to `.razor`/`.cs`/`snake_minic`.

**What the module demonstrates:**
- how to integrate `VmApplication` into the browser (without threads, without `Console`, without `Thread.Sleep`);
- the pattern **RunStepMode + `await BatchStep` + cooperative yield through a port** — the VM itself says "I've rendered a frame, wake me on the next tick";
- integration with **`AsyncDeviceContext`** for the CPU in the browser;
- integration with **JS Interop** (`snake.js` + `<canvas>`) — the VM knows nothing about `CanvasRenderingContext2D`, the monitor returns `byte[]`, and JS draws it;
- two approaches to input: **Blazor event** (Snake) vs **text editor** (Home);
- the safe `TryRegisterDevice` / `TryGetDeviceContext` API in `Home.razor`; in `Snake.razor` for the CPU, `RegisterAsyncDevice` + `GetAsyncDeviceContext` is used, and for monitor/commander — `RegisterDevice` (see [What to fix](#what-to-fix)).

## Local project tree

Namespace `Nantuc.Samples.InWeb` (all page classes are in `Nantuc.Samples.InWeb.Pages`, files physically reside in `pages/`).

```
Nantuc.Samples.InWeb/
├── Nantuc.Samples.InWeb.csproj       # net10.0, Blazor WebAssembly, ref -> VmApplication.Extensions
├── Program.cs                        # WASM entry point: CreateDefault + RootComponents
├── App.razor                         # Router: Found / NotFound
├── _Imports.razor                    # @using Microsoft.AspNetCore.Components.Web
├── pages/
│   ├── Home.razor                    # VM demo: compile+run MiniC, UART output, syntax highlighting
│   ├── Snake.razor                   # game: AsyncDeviceContext, step mode, JS canvas, input, game over
│   ├── LibCode.cs                    # const string: monitor_nanasm, commander_nanasm, snake_minic
│   ├── SnakeCommander.cs             # PortControlBase: tick/input/yield/gameover/score
│   └── MonochromeMonitor.cs          # PortControlBase: 32×16 framebuffer, 7 ports
└── wwwroot/
    ├── index.html                    # Blazor WASM template (+ loading-progress)
    ├── css/app.css                   # app + .loading-progress-* (Blazor WASM boot)
    └── js/snake.js                   # draw(canvas, b64, w, h) -> Canvas 2D
```

Organization notes:
- `MonochromeMonitor` and `SnakeCommander` are almost copies of `Nantuc.Samples.Scratch`, but with a refined contract (see below). The logic is almost the same — this is deliberate duplication so that the samples are independent.
- `LibCode.snake_minic` in InWeb **differs significantly** from Scratch: `Halt()` is replaced by `CmdWaitTick()`, `commander_nanasm` is added, `CmdSetScore`/`CmdGameOver`/`CmdYield` are added, and death "right inside `tick()`" is removed (instead of `resetGame` from `tick`, `1` is returned, and `main` decides what to do).
- `wwwroot/index.html` — the standard template with Blazor-WASM loading-progress (the variables `--blazor-load-percentage` and `--blazor-load-percentage-text` are set by the boot script `_framework/blazor.webassembly.js`).
- `Pages/LibCode.cs` contains **three** sources: `monitor_nanasm`, `commander_nanasm`, `snake_minic` — as in Scratch, but `input_nanasm`/`halt_nanasm` are absent (their role is performed by `commander_nanasm`).

## API

There is no public API — this is a WASM application (`OutputType=Exe`, `Microsoft.NET.Sdk.BlazorWebAssembly`).

### `Program.cs` — WASM entry point

```csharp
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
await builder.Build().RunAsync();
```

Standard. No custom logic — all of it is on the pages.

### `pages/Home.razor` — universal VM runner

Page `@page "/"`. State:

| Field | Purpose |
|---|---|
| `SourceCode` | the current text in `<textarea>` (two-way binding, `oninput`) |
| `HighlightedCode` | a computed "highlighted" version — regex + `WebUtility.HtmlEncode`, inserted as `MarkupString` into `<pre>` below the textarea |
| `Output` | UART output (or an error message) |
| `IsRunning` / `HasError` | button lock and `<pre>` color |

The key method is `RunNantucVm(string source)`:

```csharp
using var host = VmHostFactory.CreateDefault();
host.Project.SaveFileProject("main.minic", source);
host.Project.SaveFileInclude("io.nanasm", Libraries.io_nanasm);
host.Project.SaveFileInclude("print.minic", Libraries.print_minic);

if (host.Emulator.TryRegisterDevice(DeviceSpec.Create(RamSize.MB16, name: "ConsoleVM"), 0, out int vmId) != IdState.Valid)
    return $"[ERROR] register vm: ...";

using var wr = new StringWriter();
if (host.Emulator.TryRegisterDevice(new SynchronousIOStream(wr, PortCharEncoding.Utf16), "console", 1, out _) != IdState.Valid)
    return "[ERROR] ...";

if (!host.Emulator.TryGetDeviceContext(vmId, out var vm))
    return "[ERROR] no ctx";

var compilation = host.Project.CompileToIR(0, optimize: true).Build();
if (!compilation.Success) return $"[ERROR] {string.Join("\n", compilation.Errors!)}";
if (!vm.TryLoadProgram(compilation.Program!, 0, out var error)) return $"[ERROR] {error}";

ProcessorFault fault = vm.Run();
if (fault.Status != BiosStatus.EndProgram)
    return $"VM fault: {fault.Status} at IP=0x{fault.InstructionPointer:X}";

wr.Flush();
return wr.ToString();
```

Features:
- **The new API** `TryRegisterDevice`/`TryGetDeviceContext`/`IdState` is used — this is the right way, see `Nantuc.Samples.Scratch` [What to fix item 2](../Nantuc.Samples.Scratch/README.ru.md#what-to-fix).
- `SynchronousIOStream(wr, PortCharEncoding.Utf16)` — UART writes `char` into `StringWriter`. `Utf16` is chosen because `print16` prints 16-bit characters.
- `vm.Run()` (not StepMode) — the page blocks during execution. For small programs this is fine; for large ones — UI lag (see [What to fix, item 11](#what-to-fix)).

**Syntax highlighting** — `TokenRegex`:

```csharp
new Regex(
    $@"(?<string>""(?:\\.|[^""\\])*"")|(?<directive>#include\b)|(?<keyword>(?:{VmHostProject.MiniCKeyword}))",
    RegexOptions.Compiled);
```

Three groups: strings (`#ce9178`), directives (`#c586c0`), keywords (`#569cd6`, bold). `HighlightedCode` is called on every render and rebuilds a `StringBuilder` over `Matches`.

### `pages/Snake.razor` — the game

Page `@page "/snake"`, `@implements IAsyncDisposable`, `@inject IJSRuntime JS`. Additionally, `@using Nantuc.VmApplication.Extensions.AsyncDeviceExtensions` is included.

Constants:

| Name | Value | Meaning |
|---|---|---|
| `W`, `H`, `Cell` | `32`, `16`, `20` | field and cell size (canvas 640×320) |
| `FixedDtMs` | `150` | snake tick period (6–7 FPS) |
| `FrameBudgetMs` | `8` | time budget per game loop iteration |
| `StepChunk` | `1000` | instructions per one `BatchStep` |
| `MaxStepsPerTick` | `200_000` | safeguard against VM hang |

State fields:
- `_canvas`, `_focusHost` — `ElementReference` for JS and focus;
- `_js` — `IJSObjectReference` to `./js/snake.js`;
- `_host`, `_ctx` (`AsyncDeviceContext?`), `_monitor`, `_commander` — host and devices;
- `_cts`, `_loopTask` — game loop lifecycle;
- `_input` — `ConcurrentQueue<byte>` for key presses;
- `_frameBuffer`, `_hasNewFrame` — double frame buffering;
- `_score`, `_status`, `_running`, `_showGameOver`, `_disposed`.

`StartAsync`:
1. `_host = VmHostFactory.CreateDefault()`.
2. `SaveFileProject` + `SaveFileInclude` for `monitor.nanasm` and `commander.nanasm`.
3. `_frameBuffer = new byte[W*H]`, `_monitor = new MonochromeMonitor(W, H, OnFrameProduced)`, `_commander = new SnakeCommander()`.
4. `RegisterAsyncDevice(DeviceSpec.Create(RamSize.MB16, ...), 0)` — CPU/VM; `RegisterDevice(_monitor, 1)`, `RegisterDevice(_commander, 2)`; `_ctx = GetAsyncDeviceContext(cpuId)` with a `null` check.
   The **non-`Try*` API** is used for CPU registration, and the old `RegisterDevice` for monitor/commander, see [What to fix, item 1](#what-to-fix) and [item 17](#what-to-fix).
5. `CompileToIR(0, optimize: false).Build()`; on error — `_status = "Compile: ..."`.
6. `TryLoadProgram`; on error — `_status = "Load: ..."`.
7. **`_ctx.RunStepMode(startAddress: 0)`** — switches the CPU into step mode; instructions are executed only via `BatchStep`.
8. `_running = true; _cts = new CancellationTokenSource(); _loopTask = GameLoopAsync(_cts.Token);`.
9. `_focusHost.FocusAsync()` — for keyboard.

`GameLoopAsync` — `Input → Update → FixedUpdate → Render → Sleep`:

```csharp
while (!ct.IsCancellationRequested && _running)
{
    long now = Environment.TickCount64;

    // 1. INPUT
    while (_input.TryDequeue(out byte key)) _commander!.SetInput(key);

    // 3. FIXED UPDATE
    if (now - lastTick >= (long)FixedDtMs)
    {
        lastTick = now;
        _commander!.SignalTick();

        int total = 0;
        while (total < MaxStepsPerTick && _running)
        {
            await _ctx!.BatchStep(StepChunk);
            total += StepChunk;
            if (_commander.ConsumeYielded()) break;   // VM said "frame is ready"
        }

        _score = _commander.Score;
        if (_commander.ConsumeGameOver())
        {
            _showGameOver = true;
            try { await Task.Delay(900, ct); } catch { break; }
            _showGameOver = false;
        }
    }

    // 4. RENDER
    if (_hasNewFrame && _js is not null && _frameBuffer is not null)
    {
        _hasNewFrame = false;
        string b64 = Convert.ToBase64String(_frameBuffer);
        await InvokeAsync(async () =>
        {
            try { await _js.InvokeVoidAsync("draw", _canvas, b64, W, H); }
            catch { /* component disposed */ }
            StateHasChanged();
        });
        lastFrame = now;
    }

    int sleep = Math.Max(1, FrameBudgetMs - (int)(Environment.TickCount64 - now));
    try { await Task.Delay(sleep, ct); }
    catch (OperationCanceledException) { break; }
}
```

`HandleKeyDown` — maps `e.Key` to the snake code (`ArrowUp=1`, `ArrowDown=2`, `ArrowLeft=3`, `ArrowRight=4`), `q/Q/Escape` -> `StopAsync`. `@onkeydown:preventDefault="true"` suppresses the default for **all** keys (see [What to fix, item 7](#what-to-fix)).

`OnFrameProduced(byte[] fb, int w, int h)` — a **synchronous** callback from `MonochromeMonitor.WritePort(REG_COMMAND, CMD_FLUSH)` (same thread). Copies `fb` into `_frameBuffer` and sets `_hasNewFrame = true`. This is the double buffering: `MonochromeMonitor` does not expose the live `_fb`, and `_frameBuffer` in `Snake.razor` is read by the JS call only after copying.

`StopAsync`:
- `_running = false`, cancels `_cts`, `await _loopTask`, `await _ctx.EndStepMode()`, `_host?.Dispose()`;
- resets fields and the input queue;
- `_status = "Stopped. Press Start."`.

### `pages/SnakeCommander.cs` — control port

```csharp
public sealed class SnakeCommander : PortControlBase
{
    // Read (VM reads)
    public const byte R_INPUT      = 0x00; // 0..4, auto-consume
    public const byte R_TICK_READY = 0x01; // 0|1, auto-consume

    // Write (VM writes)
    public const byte W_YIELD     = 0x00; // 1 = "frame is ready, control returned"
    public const byte W_GAME_OVER = 0x02;
    public const byte W_SCORE     = 0x03;

    public void SignalTick();
    public void SetInput(byte key);       // ignores key == 0
    public bool ConsumeYielded();         // true -> resets the flag
    public bool ConsumeGameOver();
    public int  Score { get; }

    public override byte ReadPort(ulong offset)  => ...;
    public override void WritePort(ulong offset, byte value) => ...;
    public override void WakeProcessor() { }
}
```

- Registered on `sector 2`. With the default `DevicePortSize.B16` (`PortsOnDevice = 16 = 0x10`) the base = `2 * 16 = 0x20`. Offsets `0x00..0x03` -> addresses `0x20..0x23` — matching `commander_nanasm`.
- `R_INPUT` and `W_YIELD` are the same offset `0x00`, but with **different directions** (read vs write). In `SnakeCommander` this is separated via `ReadPort`/`WritePort`.
- `SetInput` deliberately does not write `0` — "no press" and "no new press" are different states. Resetting `_pendingInput` happens **on read** of `R_INPUT` (`ConsumeInput`).
- `Score` is `int`, but `W_SCORE` is `byte`, so the score is effectively limited to `0..255` (see [What to fix, item 2](#what-to-fix)).

### `pages/MonochromeMonitor.cs` — monochrome monitor

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
    public const byte CMD_FIRL  = 2;
    public const byte CMD_FLUSH = 3;
    public const byte CMD_HOME  = 4;

    public MonochromeMonitor(int width, int height, Action<byte[], int, int>? renderer = null);
    public int Width  { get; }
    public int Height { get; }

    public override byte ReadPort(ulong offset)  => ...;
    public override void WritePort(ulong offset, byte value) => ...;
    public override void WakeProcessor() { }
}
```

- `width`/`height` — 1..255, otherwise `ArgumentOutOfRangeException`.
- `WritePort(REG_PIXEL, value)` writes `value` into `_fb[_cy * Width + _cx]` and remembers `_pixel`.
- `WritePort(REG_COMMAND, CMD_FLUSH)` calls `_renderer(_fb, Width, Height)` — the callback receives the **live** `_fb`. In `Snake.razor`, `OnFrameProduced` **immediately copies** it into `_frameBuffer` (`Buffer.BlockCopy`), so `snake.js` no longer sees a race.
- `ReadPort(REG_FRAME)` — frame counter (incremented on `CMD_FLUSH`). `CMD_CLEAR` does **not** reset it.
- The ports are hard-wired to `sector 1` + `B16` (base `0x10`).

### `pages/LibCode.cs` — sources for the snake

```csharp
public static class LibCode
{
    public const string monitor_nanasm;   // func_MonClear, func_MonFlush, func_MonPlot
    public const string commander_nanasm; // func_CmdWaitTick, CmdReadInput, CmdYield, CmdGameOver, CmdSetScore
    public const string snake_minic;      // #include monitor/commander + game
}
```

`monitor_nanasm` (ports via `0x10..0x13`):
- `MonClear` — `OUT 1` to `0x13` (`CMD_CLEAR`), `OUT 4` (`CMD_HOME`).
- `MonFlush` — `OUT 3` to `0x13` (`CMD_FLUSH`).
- `MonPlot(r0=x, r1=y, r2=v)` — `OUT r0, 0x10` (`REG_CURSOR_X`), `OUT r1, 0x11` (`REG_CURSOR_Y`), `OUT r2, 0x12` (`REG_PIXEL`).

`commander_nanasm` (ports via `0x20..0x23`):
- `CmdWaitTick` — busy-poll `IN 0x21` (`R_TICK_READY`) until `1`.
- `CmdReadInput` — `IN 0x20` (`R_INPUT`) — consume-on-read.
- `CmdYield` — `OUT 1 -> 0x20` (`W_YIELD`).
- `CmdGameOver` — `OUT 1 -> 0x22` (`W_GAME_OVER`).
- `CmdSetScore(r0)` — `OUT r0 -> 0x23` (`W_SCORE`).

`snake_minic` — the game:
- state (`sx[256]`, `sy[256]`, `slen`, `dir`, `foodX`, `foodY`, `rngState`) — in **globals** (legacy from the old `FunctionGenerator.GenerateFunction` bug, see `Nantuc.Samples.Scratch` [Special information](../Nantuc.Samples.Scratch/README.ru.md#special-information));
- `rng()` — LCG `state * 1103515245 + 12345`;
- `spawnFood`, `resetGame`, `render` — almost as in Scratch;
- `tick()` — `CmdReadInput`, turn (with protection against reversal), step, **wrap around the 32×16 edges**, collision check; on death it does **not** call `resetGame`, it returns `1` (so that Blazor has time to show the frame);
- `main()` — `resetGame(); render(); CmdYield(); while(1) { CmdWaitTick(); if (tick()) { render(); CmdGameOver(); CmdYield(); resetGame(); } else { render(); CmdYield(); } }`.

Note: **there is no `Halt()`**; waiting for a tick is `CmdWaitTick()` via `R_TICK_READY`. This allows `BatchStep` not to "eat" the loop idly, but to stop on `ConsumeYielded`. See [Special information](#special-information).

### `wwwroot/js/snake.js` — frame rendering

```js
export function draw(canvas, b64, w, h) {
    const bin = atob(b64);
    const ctx = canvas.getContext("2d");
    const cw = canvas.width / w;
    const ch = canvas.height / h;

    ctx.fillStyle = "#0a0a0a";
    ctx.fillRect(0, 0, canvas.width, canvas.height);

    ctx.fillStyle = "#22ff44";
    for (let y = 0; y < h; y++) {
        const row = y * w;
        for (let x = 0; x < w; x++) {
            if (bin.charCodeAt(row + x) !== 0) {
                ctx.fillRect(x * cw + 1, y * ch + 1, cw - 2, ch - 2);
            }
        }
    }
}
```

- Accepts a base64-encoded `_frameBuffer` (512 bytes for 32×16), decodes it into the string `bin`, and draws 18×18 rectangles with a 1px inset.
- Does not keep a reference to the canvas between calls — the canvas is recreated by Blazor only on `@ref` rebuild, which is not done here.
- `atob` — a standard Web API, `charCodeAt` yields a `byte` (for 32×16 always `< 256`).

### `wwwroot/index.html` / `wwwroot/css/app.css`

- The standard WASM template + Blazor loading-progress (`<svg class="loading-progress">` with two `<circle>`, `stroke-dasharray: calc(3.141 * var(--blazor-load-percentage) * 0.8)`), the text — `var(--blazor-load-percentage-text)`.
- `<script src="_framework/blazor.webassembly#[.{fingerprint}].js"></script>` — fingerprinting, set by the publish target.
- `<link rel="preload" id="webassembly" />` — a placeholder that the Blazor boot script fills with `href` to the `.wasm`.
- `#blazor-error-ui` — a yellow bar at the bottom, shown on unhandled Blazor exceptions. Ours is standard.

## API examples

**Run the WASM demo:**

```bash
dotnet run
# then open http://localhost:5xxx/         — Home
# and http://localhost:5xxx/snake          — Snake
```

**Test Hello world on the `/` page:**

Default code:

```c
#include "print.minic"

char str[] = "Hello world from WASM! Привет мир из WASM!";

void main() {
    print16(str);
}
```

Click "Compile and run" — the same string appears in the `<pre>` below the button (the UTF-16 string went through `print16` -> UART -> `StringWriter` -> Blazor).

**Change the program on `/`:**

Replace with:

```c
#include "print.minic"

void main() {
    print16("Nantuc in WASM");
}
```

and click the button — you get `Nantuc in WASM`.

**Play the snake:**

1. Open `/snake`, press **Start** — compilation of `snake_minic` begins, then `RunStepMode` and the game loop.
2. Controls: **arrows** (Up/Down/Left/Right), exit — **Q** / **Escape**.
3. **Stop** — stops the VM (`await EndStepMode()`) and releases `_host`.
4. **Start** after Stop — a new `VmHost`, a new VM, score from 0.

**Add your own example of using the VM in the browser:**

Create `pages/MyDemo.razor`:

```razor
@page "/my-demo"
@using Nantuc.VmApplication
@using Nantuc.VmApplication.Emulator
@using Nantuc.VmApplication.Extensions.Host
@using Nantuc.VmApplication.Extensions.StdLibraries

<button @onclick="Run">Run</button>
<pre>@_out</pre>

@code {
    string _out = "";

    void Run()
    {
        using var host = VmHostFactory.CreateDefault();
        host.Project.SaveFileProject("main.minic", "#include \"print.minic\"\nvoid main(){ print16(\"hi\"); }");
        host.Project.SaveFileInclude("io.nanasm", Libraries.io_nanasm);
        host.Project.SaveFileInclude("print.minic", Libraries.print_minic);

        host.Emulator.TryRegisterDevice(DeviceSpec.Create(RamSize.MB16, name: "VM"), 0, out int vmId);
        using var wr = new StringWriter();
        host.Emulator.TryRegisterDevice(new SynchronousIOStream(wr, PortCharEncoding.Utf16), "console", 1, out _);
        host.Emulator.TryGetDeviceContext(vmId, out var vm);

        var res = host.Project.CompileToIR(0, optimize: true).Build();
        vm!.TryLoadProgram(res.Program!, 0, out _);
        vm.Run();
        _out = wr.ToString();
    }
}
```

Add a link to the page in `pages/Home.razor` (`<a href="/my-demo">My demo</a>`).

For an asynchronous CPU variant, see `Snake.razor`: `RegisterAsyncDevice` + `GetAsyncDeviceContext` + `await _ctx.BatchStep(...)`.

**Add your own port to the monitor:**

1. In `pages/MonochromeMonitor.cs` — a new `REG_*` and a branch in `ReadPort`/`WritePort`.
2. In `pages/LibCode.cs` — a wrapper in `monitor_nanasm` (account for `sector 1 -> base 0x10`).
3. In `pages/Snake.razor` — nothing, if the port is read/written only from the VM.

## How close the module is to the user

**High** for "I want to show Nantuc in the browser": two ready-made pages, a live game, a JS canvas, syntax highlighting, the safe `Try*` API in `Home.razor`.

**Medium** for "I want to write my own games on Nantuc": `Snake.razor` is a dense example, but with noticeable specifics — step mode, cooperative yield, `AsyncDeviceContext`, double frame buffering. The author of their own game will have to figure out why `RunStepMode`/`await BatchStep` and not `Run()`.

**Low** for "I want to study the VM internals": this sample is about integration, not architecture (for that — `Nantuc.VmApplication`, `Nantuc.Compiler`, `Nantuc.Kernel`).

**What you should do:**
- Run `dotnet run` and open both pages — they show two different scenarios.
- To debug the snake — decrease `FixedDtMs` (e.g. to 300 ms) or increase `FrameBudgetMs`.
- Use `TryRegisterDevice`/`TryGetDeviceContext` in new code — as in `Home.razor`; for async CPU, check the result of `RegisterAsyncDevice`/`GetAsyncDeviceContext`, as in `Snake.razor`.
- Pass a callback to `MonochromeMonitor` that **copies** `fb`, not one that stores a reference.

**What you should not do:**
- Expect `BatchStep` to return control "when needed" by itself — you must check `ConsumeYielded()`.
- Change `StepChunk`/`MaxStepsPerTick` without understanding that `BatchStep`, although async, executes a chunk of instructions in a batch: too large a `StepChunk` will freeze the UI, too small — will increase overhead.
- Rely on `_commander.SetInput` delivering all key presses between ticks — see [What to fix, item 5](#what-to-fix).
- Use `RegisterDevice` without checking the result — as in `Snake.razor.StartAsync` for monitor/commander; prefer `Try*` (see [What to fix, item 17](#what-to-fix)).

## Special information

### `RunStepMode` + `await BatchStep` + cooperative yield

In the browser there is no `Thread.Sleep`, no `Console.ReadKey`, and no "background VM thread". That is why the game is built differently than in `Nantuc.Samples.Scratch`:

- `_ctx.RunStepMode(0)` — the VM **does not execute** until `BatchStep` is called.
- `await _ctx.BatchStep(StepChunk)` — execute up to `StepChunk` instructions.
- `_commander.SignalTick()` — the VM is "woken up": `CmdWaitTick()` will see `R_TICK_READY = 1`.
- `_commander.ConsumeYielded()` — the VM finished the tick: `CmdYield()` wrote `W_YIELD = 1`.

Order in `GameLoopAsync`:
1. `SignalTick()` -> `R_TICK_READY = 1`.
2. Loop `await BatchStep(1000)` until the VM does `CmdYield` or `MaxStepsPerTick` is reached.
3. `_score = _commander.Score` — after `ConsumeYielded`.
4. If the VM died and called `CmdGameOver` — a 900 ms overlay.
5. The frame (`_hasNewFrame`) is handed to JS.

**Important:** between ticks the VM **does not execute**. This is the CPU saving in the browser. `MaxStepsPerTick = 200_000` is a safeguard: if the VM gets stuck in `while(1)` without `CmdYield`, the loop exits by the limit and the UI does not freeze.

### `AsyncDeviceContext` for the CPU

In `Snake.razor` the CPU is registered via `RegisterAsyncDevice` and then `AsyncDeviceContext` is used:

- `RegisterAsyncDevice(DeviceSpec.Create(RamSize.MB16, ...), sector: 0)`;
- `GetAsyncDeviceContext(cpuId)`;
- `_ctx.RunStepMode(startAddress: 0)`;
- `await _ctx.BatchStep(StepChunk)`;
- `await _ctx.EndStepMode()`.

At the same time, `MonochromeMonitor` and `SnakeCommander` still inherit from `PortControlBase` and are registered via the synchronous `RegisterDevice`. This is a mixed scenario: async CPU + sync port devices.

### Double frame buffering

`MonochromeMonitor.WritePort(REG_COMMAND, CMD_FLUSH)` synchronously calls `_renderer(_fb, Width, Height)`. If `Snake.razor` passed `_fb` directly to `snake.js`, JS would have time to read the buffer while the VM writes the next frame. Therefore:

- `OnFrameProduced` does `Buffer.BlockCopy(fb, 0, _frameBuffer, 0, fb.Length)` and sets `_hasNewFrame = true`;
- `GameLoopAsync` copies `_frameBuffer` to base64 and passes it to JS;
- JS works with a **copy** already.

This makes `MonochromeMonitor` safe with a **synchronous** renderer. If tomorrow `renderer` becomes asynchronous, the contract will have to be changed (see [What to fix, item 9](#what-to-fix)).

### Ports are hard-wired to `B16`

`monitor_nanasm` uses `0x10..0x13`, `commander_nanasm` — `0x20..0x23`. This works as long as `DevicePortSize.B16` (`PortsOnDevice = 0x10`):

- `sector 1` (monitor) -> base `0x10`;
- `sector 2` (commander) -> base `0x20`.

If `portsPerDevice` is changed in `VmHostFactory.CreateDefault(...)` (e.g. to `B32`), the base of `sector 1` becomes `0x20`, and `monitor_nanasm` will start writing to **sector 2** (commander) instead of the monitor. This is a fragile spot inherited from Scratch.

### `commander_nanasm`: `R_INPUT` and `W_YIELD` — one offset

In `SnakeCommander`:

- `ReadPort(0x00)` -> `ConsumeInput()` (reads `_pendingInput`, resets to 0);
- `WritePort(0x00, v)` -> if `v != 0`, `_yielded = true`.

This allows `input_nanasm`/`commander_nanasm` to use **one** address `0x20` for both reading and writing. `OUT r0, r1` with `r1 = 0x20` is never confused with `IN r0, r1` with the same `r1` — they are different bus operations.

### `SetInput` and "last press wins"

`_input` is a `ConcurrentQueue<byte>`, but `SnakeCommander.SetInput` overwrites `_pendingInput`. If two presses arrive between `CmdReadInput` (a 200 ms window), the second overwrites the first. For the snake this is fine (we want the "last direction"), but for more complex games it means input loss. See [What to fix, item 5](#what-to-fix).

### Game Over: reset **before** showing the overlay

In `snake_minic`:

```
if (tick()) {
    render();          // crash frame (head already at the collision point)
    CmdGameOver();     // _gameOver = true
    CmdYield();        // _yielded = true — Blazor will see it
    resetGame();       // _score = 0, the snake is at (16,8) again
}
```

`resetGame` is called **after** `CmdYield`. This means that by the time `ConsumeYielded` and `_score = _commander.Score` run (in `GameLoopAsync`), the VM **has already** been reset: the score is already 0. And `_showGameOver = true` shows the overlay for 900 ms. During this time `GameLoopAsync` will manage ~6 more ticks (900/150) — and the new game is already running. The player sees: "GAME OVER" for 900 ms, score 0, then the overlay disappears — and the snake has already moved 6 cells. Not a bug, but a UX quirk. See [What to fix, item 3](#what-to-fix).

### `Home.razor`: `RegexOptions.Compiled` in WASM — a no-op

`TokenRegex` is declared with `RegexOptions.Compiled`. In WASM (AOT) `Reflection.Emit` is unavailable, so `Compiled` is silently ignored (the interpreter is used). For a 220px editor this is fine, but there is no optimization. If desired — `RegexOptions.NonBacktracking` or source-generated `[GeneratedRegex]` in .NET 7+.

### `Home.razor`: `vm.Run()` blocks the UI

`RunNantucVm` is synchronous: `CompileToIR` + `vm.Run()` execute on the Blazor WASM UI thread. For `Hello world` — milliseconds, for a 100k-instruction program — a noticeable freeze. In WASM there are no real threads, `Task.Run` won't help — only splitting `vm.Run()` into `RunStepMode`/`BatchStep` and `await Task.Delay(0)` between chunks, as in `Snake.razor`. Right now it is a conscious compromise in favor of simplicity.

### `@onkeydown:preventDefault="true"` on `<div tabindex="0">`

Suppresses **all** default keyboard actions, including `Tab` (navigation), `F5` (refresh), `Ctrl+R`, etc. See [What to fix, item 7](#what-to-fix).

### `_focusHost.FocusAsync()` and focus loss

`StartAsync` calls `FocusAsync()` on the wrapper. If the user clicks outside the game, the arrows stop working. There is no `@onfocusin`/`@onclick` handler to restore focus. See [What to fix, item 8](#what-to-fix).

## What to fix

In descending order of severity.

1. **`Snake.razor.StartAsync` — the CPU is registered via `RegisterAsyncDevice`/`GetAsyncDeviceContext` without `Try*`.**
   File: `pages/Snake.razor`, lines:
   ```csharp
   int cpuId = _host.Emulator.RegisterAsyncDevice(handle, sector: 0);
   ...
   _ctx = _host.Emulator.GetAsyncDeviceContext(cpuId);
   if (_ctx is null) { ... return; }
   ```
   `GetAsyncDeviceContext` is checked for `null`, but the result of `RegisterAsyncDevice` (`cpuId`) is not checked for `IdState.Valid`/a negative value. If `sector 0` is occupied, the error will show up later or will be unclear. This is a **discrepancy with `Home.razor`**, where `TryRegisterDevice`/`IdState.Valid` is used.
   Fix: check `cpuId` (or use `TryRegisterAsyncDevice` if it exists) and `TryGetAsyncDeviceContext` if such an API is available. Similarly for `_monitor` and `_commander` — see item 17.

2. **`SnakeCommander.Score` — `int`, while `W_SCORE` — `byte`.**
   File: `pages/SnakeCommander.cs`, `public int Score => _score;` and `case W_SCORE: _score = value; break;`. `value` is `byte`, so the score is limited to `0..255`. A 32×16 field = 512 cells, the snake could theoretically reach 509.
   Fix: either `ushort` via two ports (`W_SCORE_LO`/`W_SCORE_HI`), or clamp to `Math.Min(value, 255)` and document it. Or switch to `W_SCORE_LO` = `byte`, `W_SCORE_HI` = `byte` and `score = lo | (hi << 8)`.

3. **`Snake.razor.GameLoopAsync` — Game Over: the new game is already running under the overlay.**
   File: `pages/Snake.razor`, the block `if (_commander.ConsumeGameOver()) { _showGameOver = true; try { await Task.Delay(900, ct); } catch { break; } _showGameOver = false; }`.
   `resetGame()` in `snake_minic` executes immediately after `CmdYield()`, and the VM keeps ticking in the next iterations of `GameLoopAsync`. During the 900 ms overlay the VM manages to do ~6 ticks.
   Fix: during `_showGameOver`, **do not** call `_commander.SignalTick()` — then the VM will stay in `CmdWaitTick` until the end of the overlay. Or move `resetGame()` in `main` after the next `CmdWaitTick` tick.

4. **`Snake.razor.StartAsync` — `catch (Exception)` does not release `_host`.**
   File: `pages/Snake.razor`, `catch (Exception ex) { _running = false; _status = "Start failed: " + ex.Message; StateHasChanged(); }`.
   If the exception occurred **after** `_host = VmHostFactory.CreateDefault()` (e.g. in `CompileToIR`), `_host` remains alive until `DisposeAsync` of the component. On a second `Start` — a new `VmHost` while the old one is still alive.
   Fix: `try { ... } catch { _host?.Dispose(); _host = null; _ctx = null; _monitor = null; _commander = null; throw; }` or use `try/finally`.

5. **`SnakeCommander.SetInput` loses intermediate presses between ticks.**
   File: `pages/SnakeCommander.cs`, `public void SetInput(byte key) { if (key != 0) _pendingInput = key; }`.
   In 150 ms the user can press 2–3 arrows; `_input` (`ConcurrentQueue`) will save them, but `GameLoopAsync` drains the queue every 8 ms, calling `SetInput` for each; `SetInput` overwrites `_pendingInput`. As a result, `CmdReadInput` sees only the **last** one.
   Fix: either `_pendingInputs` — a queue in `SnakeCommander` (with a limit so it doesn't grow), or in `GameLoopAsync` pass only the last press (as now, but **deliberately**), documenting "last wins". For the snake this is OK, for RTS/fighting — not.

6. **`Snake.razor.GameLoopAsync` — on cancellation during Game Over, `_showGameOver` is not reset.**
   File: `pages/Snake.razor`, `try { await Task.Delay(900, ct); } catch { break; }`. If `ct` is cancelled during Game Over, the loop exits via `break` before `_showGameOver = false`. `StopAsync` does not reset the overlay, so it can stay visible until the next `StartAsync`/re-render.
   Fix: `try { await Task.Delay(900, ct); } finally { _showGameOver = false; }` or reset `_showGameOver` in `StopAsync`.

7. **`Snake.razor` — `@onkeydown:preventDefault="true"` suppresses `Tab`/`F5`/`Ctrl+R`.**
   File: `pages/Snake.razor`, `<div class="snake-wrap" tabindex="0" @onkeydown:preventDefault="true">`.
   Breaks accessibility and default browser hotkeys.
   Fix: remove `preventDefault` from the div and in `HandleKeyDown` call `e.preventDefault()` for arrows/`Q`/`Escape` — but a Blazor event does not allow `preventDefault` from C#; you would have to add a JS `keydown` handler with `preventDefault` only for `Arrow*`, or leave it as is and document it.

8. **`Snake.razor` — no focus restoration after clicking outside the game.**
   File: `pages/Snake.razor`. `_focusHost.FocusAsync()` is called only in `StartAsync`.
   Fix: add `@onclick="_ => _focusHost.FocusAsync()"` on `.snake-wrap` (or `@onfocusin`) and/or `tabindex="-1"` on the `<canvas>`.

9. **`MonochromeMonitor` — `_renderer(_fb, Width, Height)` exposes the live buffer.**
   File: `pages/MonochromeMonitor.cs`, `CMD_FLUSH`. In `Snake.razor` `OnFrameProduced` copies — so it is currently safe. But the `Action<byte[], int, int>` contract does not guarantee this.
   Fix: XML-doc `/// <summary>Renderer must copy the buffer; it is reused on next flush.</summary>` or pass a copy (an extra 512 bytes per frame — negligible).

10. **`Snake.razor.GameLoopAsync` — `await BatchStep` when the VM is `Halted` spins empty iterations.**
    File: `pages/Snake.razor`, `while (total < MaxStepsPerTick && _running) { await _ctx!.BatchStep(StepChunk); ... }`.
    If the VM reached `HALT` (not present in `snake_minic`, but possible in other programs), `BatchStep` returns quickly, and the loop will do `200_000 / 1000 = 200` empty iterations.
    Fix: check `_ctx.IsHalted` (if such an API exists) and exit the loop.

11. **`Home.razor` — `vm.Run()` is synchronous, blocks the UI.**
    File: `pages/Home.razor`, `RunNantucVm`. For long programs — a Blazor WASM freeze (animation, input).
    Fix: switch to `RunStepMode` + `BatchStep` with `await Task.Delay(0)` between chunks (as in `Snake.razor`), or leave it as is and show a warning "running...".

12. **`Home.razor` — `HasError` is determined by the `Output` prefix.**
    File: `pages/Home.razor`, `HasError = Output.StartsWith("[ERROR]") || Output.StartsWith("VM fault:");`.
    Fragile: any message from a user program that starts with `[ERROR]` will be taken as an error.
    Fix: return a tuple `(string output, bool isError)` from `RunNantucVm` or throw a custom exception on errors.

13. **`Snake.razor` — `_js` may be `null` during `StartAsync`.**
    File: `pages/Snake.razor`, `OnAfterRenderAsync(first)` loads `snake.js`. If the user pressed Start **before** `OnAfterRenderAsync` completes, `_js == null`, and frames are not drawn (`_hasNewFrame` is reset to `false` before the `_js` check). The next frame arrives in 150 ms — not critical, but the first ~2 frames are lost.
    Fix: in `StartAsync`, wait for `_js` (e.g. a `TaskCompletionSource` resolved in `OnAfterRenderAsync`).

14. **`Snake.razor` — `OnFrameProduced` uses `_frameBuffer!` without a check.**
    File: `pages/Snake.razor`, `Buffer.BlockCopy(fb, 0, _frameBuffer!, 0, fb.Length)`. Currently safe (the buffer is initialized before `RunStepMode`), but `!` is a potential NRE during refactoring.
    Fix: `if (_frameBuffer is null || _frameBuffer.Length < fb.Length) return;` — and `_frameBuffer = new byte[fb.Length]`.

15. **`Snake.razor` — `_score`/`_showGameOver`/`_status` are not `volatile`.**
    File: `pages/Snake.razor`. In single-threaded WASM — safe. When ported to Server/Blazor Hybrid with a background thread — a race.
    Fix (preventively): either document the "single-threaded assumption", or use `volatile` / `lock`.

16. **`LibCode.input_nanasm` — a race of two presses (not in InWeb, but in Scratch).**
    In InWeb, `snake_minic` uses `commander_nanasm`, not `input_nanasm`. But `SnakeCommander.SetInput` still loses intermediate presses (see item 5).

17. **`Snake.razor` — `MonochromeMonitor` and `SnakeCommander` are registered via `RegisterDevice` without checks.**
    File: `pages/Snake.razor`, `_host.Emulator.RegisterDevice(_monitor, name: "monitor", sector: 1);` — without an `out` parameter and without checking `IdState.Valid`. If `sector 1` is occupied — the error is not checked. Same for `sector 2`.
    Fix: `TryRegisterDevice(_monitor, "monitor", 1, out _)` + check `IdState.Valid`.

18. **`pages/Home.razor` — `char str[]` in the default string contains `Привет мир` (UTF-8 in the source).**
    File: `pages/Home.razor`, `SourceCode`. If the MiniC compiler does not support non-Russian string literals in UTF-8, the program won't compile. In the current version it does compile (UTF-8 in WASM works), but this is worth checking on CI.

19. **`pages/Snake.razor` — no pause.**
    File: `pages/Snake.razor`. Only Start/Stop. "Pause" is implemented as Stop+Start (recreating `VmHost` and the VM, the score is lost). For debugging — inconvenient.
    Fix: `_running = false` without `await _ctx.EndStepMode()` and `_host.Dispose()` — then you can resume from the same place. Or a separate Pause button.

20. **`pages/Snake.razor` — `lastFrame` is declared but not used.**
    File: `pages/Snake.razor`, `long lastFrame = lastTick;` and `lastFrame = now;` in the render block. Dead code.
    Fix: remove it (or use it for an FPS counter).

---

**Comparison with `Nantuc.Samples.Scratch`:**

| Aspect | Scratch (console) | InWeb (browser) |
|---|---|---|
| VM execution | `ctx.Run()` (blocking) | `RunStepMode` + `await BatchStep` (cooperative) |
| VM context | `DeviceContext` | `AsyncDeviceContext` |
| Waiting for a tick | `Halt()` + `WakeProcessor` | `CmdWaitTick()` (busy-poll via port) |
| Input | `portBus.WritePort(0, code)` (loopback) | `SnakeCommander` on `sector 2` |
| Rendering | ANSI in `Console` | `snake.js` + `<canvas>` (JS Interop) |
| Frame buffering | none (ANSI immediately) | double (`_fb` -> `_frameBuffer` -> JS) |
| CPU registration | `RegisterDevice` + `!` (legacy) | `RegisterAsyncDevice` + `GetAsyncDeviceContext` (Snake); `TryRegisterDevice`/`TryGetDeviceContext` (Home) |
| Port registration | `RegisterDevice` + `!` | Home — `Try*`; Snake — `RegisterDevice` without `Try*` |
| Timer | `System.Threading.Timer` | `Task.Delay` in an `async` loop |
| `Halt` | present (`halt_nanasm`) | absent (`commander_nanasm`) |
| `keyboard.nanasm` | present (unused) | absent (input via `SnakeCommander`) |

Both samples are about the same game, but with **different transport layers**. Scratch is useful as "how to quickly run the VM in the console", InWeb — as "how to embed the VM in a browser UI".