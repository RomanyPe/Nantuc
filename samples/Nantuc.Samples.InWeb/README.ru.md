# Nantuc.Samples.InWeb — README

> Демонстрационный модуль платформы Nantuc, собранный как **Blazor WebAssembly**-приложение и работающий **в браузере**. Содержит две страницы: `/` — универсальный запуск MiniC-программ (компиляция + исполнение в WASM, вывод через UART) и `/snake` — интерактивная "Змейка" с монохромным монитором 32×16, управляемая стрелками клавиатуры.
> Зависит от `[Nantuc.VmApplication.Extensions](../../src/Nantuc.VmApplication.Extension/README.ru.md)` (транзитивно — `VmApplication`, `Compiler`, `Kernel`).
> Связан с консольным сэмплом: [`Nantuc.Samples.Scratch`](../Nantuc.Samples.Scratch/README.ru.md) — тот же "движок" змейки, но без браузера.
> [← Корень](../README.ru.md), [VmApplication](../../src/Nantuc.VmApplication/README.ru.md), [Extensions](../../src/Nantuc.VmApplication.Extension/README.ru.md), [Compiler](../../src/Nantuc.Compiler/README.ru.md), [Kernel](../../src/Nantuc.Kernel/README.ru.md)

> [!IMPORTANT]
> Из-за особенностей WebAssembly в файле проекта (`.csproj`) должен быть включен флаг многопоточности при использование синхронного `Device`, `AsyncDevice` такой проблемы не имеет, иначе в рантайме возникнет `PlatformNotSupportedException` при работе с потоками и блокировками:
> ```xml
> <PropertyGroup>
>   <WasmEnableThreads>true</WasmEnableThreads>
> </PropertyGroup>
> ```

## Содержание
- [Об этом модуле](#об-этом-модуле)
- [Дерево локального проекта](#дерево-локального-проекта)
- [API](#api)
- [Примеры для API](#примеры-для-api)
- [Приближенность модуля к пользователю](#приближенность-модуля-к-пользователю)
- [Особенная информация](#особенная-информация)
- [Что исправить](#что-исправить)

## Об этом модуле

`Nantuc.Samples.InWeb` — это **два независимых сценария на одной WASM-сборке**, каждый на своей странице Blazor:

1. **`/` (`pages/Home.razor`) — "Nantuc VM в браузере".** Универсальный раннер: текстовый редактор MiniC-кода с подсветкой синтаксиса, кнопка "Скомпилировать и запустить". При нажатии:
   - `VmHostFactory.CreateDefault()`;
   - в проект пишется `main.minic` + include-библиотеки `io.nanasm` / `print.minic`;
   - `TryRegisterDevice` (`DeviceSpec.Create(RamSize.MB16, ...)`) на `sector 0` — VM;
   - `TryRegisterDevice` (`SynchronousIOStream` поверх `StringWriter`, `PortCharEncoding.Utf16`) на `sector 1` — UART/console;
   - `host.Project.CompileToIR(baseAddress: 0, optimize: true).Build()`;
   - `vm.Run()` (не StepMode) — выполнение до `EndProgram` или до fault;
   - `wr.ToString()` возвращается на страницу и рендерится в `<pre>`.

   Так модуль показывает **"чистый" синхронный сценарий** компиляции/исполнения без кооперативного планировщика — в противоположность змейке.

2. **`/snake` (`pages/Snake.razor`) — "Змейка" в браузере.** Полноценная игра:
   - VM на `sector 0`, `MonochromeMonitor` 32×16 на `sector 1`, `SnakeCommander` на `sector 2`;
   - `CompileToIR(0, optimize: false)` компилирует `LibCode.snake_minic` (с `monitor.nanasm` и `commander.nanasm`);
   - `_ctx.RunStepMode(startAddress: 0)` — VM переводится в **step-режим** (не `Run()`), потому что игра работает кооперативно;
   - игровой цикл `GameLoopAsync` — `async`-цикл с `Task.Delay`, `FixedDtMs = 150`, `FrameBudgetMs = 8`, `StepChunk = 1000`, `MaxStepsPerTick = 200_000`;
   - каждые 150 мс: `_commander.SignalTick()` -> `_ctx.BatchStep(StepChunk)` в цикле -> `_commander.ConsumeYielded()` -> `break`;
   - кадр (`CMD_FLUSH` в мониторе) копируется в `_frameBuffer` (`OnFrameProduced`), затем передаётся в `snake.js` через `IJSRuntime`, рисуется на `<canvas>`;
   - ввод с клавиатуры: `@onkeydown` на `div[tabindex=0]` -> `ConcurrentQueue<byte>` -> `_commander.SetInput(code)`;
   - Game Over: оверлей 900 мс, затем `_showGameOver = false` (но см. [Что исправить, п.3](#что-исправить)).

   `snake_minic` в этой версии **не использует `Halt()`** — ожидание тика реализовано busy-poll'ом `CmdWaitTick()` через `SnakeCommander.ReadPort(R_TICK_READY)`. Это принципиальное отличие от `Nantuc.Samples.Scratch`: там `Halt()` + `WakeProcessor`, здесь — кооперативная пауза через порт.

**Как запустить (WASM):**

```bash
# из каталога проекта
dotnet run
# откройте http://localhost:5xxx/        — /snake
# откройте http://localhost:5xxx/snake   — /snake
```

`dotnet publish` даёт статический сайт — `wwwroot` можно раздавать любым статик-сервером.

**Как запустить (dev-loop):** `dotnet watch run` — пересборка при изменении `.razor`/`.cs`/`snake_minic`.

**Что показывает модуль:**
- как интегрировать `VmApplication` в браузер (без потоков, без `Console`, без `Thread.Sleep`);
- паттерн **RunStepMode + BatchStep + кооперативный yield через порт** — VM сама говорит "я отрисовала кадр, разбуди меня на следующем тике";
- интеграцию с **JS Interop** (`snake.js` + `<canvas>`) — VM не знает про `CanvasRenderingContext2D`, монитор отдаёт `byte[]`, а JS рисует;
- два подхода к вводу: **событие Blazor** (Snake) vs **текстовый редактор** (Home);
- безопасный API `TryRegisterDevice` / `TryGetDeviceContext` (в отличие от `Nantuc.Samples.Scratch`, где ещё встречается `RegisterDevice` + `!`).

## Дерево локального проекта

Namespace `Nantuc.Samples.InWeb` (все классы страниц — в `Nantuc.Samples.InWeb.Pages`, файлы физически лежат в `pages/`).

```
Nantuc.Samples.InWeb/
├── Nantuc.Samples.InWeb.csproj       # net10.0, Blazor WebAssembly, ref -> VmApplication.Extensions
├── Program.cs                        # WASM entry point: CreateDefault + RootComponents
├── App.razor                         # Router: Found / NotFound
├── _Imports.razor                    # @using Microsoft.AspNetCore.Components.Web
├── pages/
│   ├── Home.razor                    # VM demo: compile+run MiniC, UART-вывод, подсветка синтаксиса
│   ├── Snake.razor                   # игра: step-режим, JS-канвас, ввод, game over
│   ├── LibCode.cs                    # const string: monitor_nanasm, commander_nanasm, snake_minic
│   ├── SnakeCommander.cs             # PortControlBase: тик/ввод/yield/gameover/score
│   └── MonochromeMonitor.cs          # PortControlBase: 32×16 framebuffer, 7 портов
└── wwwroot/
    ├── index.html                    # шаблон Blazor WASM (+ loading-progress)
    ├── css/app.css                   # app + .loading-progress-* (Blazor WASM boot)
    └── js/snake.js                   # draw(canvas, b64, w, h) -> Canvas 2D
```

Замечания по организации:
- `MonochromeMonitor` и `SnakeCommander` — почти копии `Nantuc.Samples.Scratch`, но с уточнённым контрактом (см. ниже). Логика почти та же — это осознанная дубликация, чтобы сэмплы были независимы.
- `LibCode.snake_minic` в InWeb **сильно отличается** от Scratch: `Halt()` заменён на `CmdWaitTick()`, добавлен `commander_nanasm`, добавлены `CmdSetScore`/`CmdGameOver`/`CmdYield`, убрана смерть "прямо в `tick()`" (вместо `resetGame` из `tick` возвращается `1`, `main` сам решает, что делать).
- `wwwroot/index.html` — стандартный шаблон с Blazor-WASM loading-progress (переменные `--blazor-load-percentage` и `--blazor-load-percentage-text` устанавливает boot-скрипт `_framework/blazor.webassembly.js`).
- `Pages/LibCode.cs` содержит **три** исходника: `monitor_nanasm`, `commander_nanasm`, `snake_minic` — как и в Scratch, но `input_nanasm`/`halt_nanasm` отсутствуют (их роль выполняет `commander_nanasm`).

## API

Публичного API нет — это WASM-приложение (`OutputType=Exe`, `Microsoft.NET.Sdk.BlazorWebAssembly`).

### `Program.cs` — WASM-точка входа

```csharp
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
await builder.Build().RunAsync();
```

Стандарт. Никакой кастомной логики — вся она на страницах.

### `pages/Home.razor` — универсальный VM-раннер

Страница `@page "/"`. Состояние:

| Поле | Назначение |
|---|---|
| `SourceCode` | текущий текст в `<textarea>` (двусторонняя привязка, `oninput`) |
| `HighlightedCode` | вычисляемая "подсвеченная" версия — regex + `WebUtility.HtmlEncode`, вставляется как `MarkupString` в `<pre>` под textarea |
| `Output` | вывод UART (или сообщение об ошибке) |
| `IsRunning` / `HasError` | блокировка кнопки и цвет `<pre>` |

Ключевой метод — `RunNantucVm(string source)`:

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

Особенности:
- **Используется новый API** `TryRegisterDevice`/`TryGetDeviceContext`/`IdState` — так и надо, см. `Nantuc.Samples.Scratch` [Что исправить п.2](../Nantuc.Samples.Scratch/README.ru.md#что-исправить).
- `SynchronousIOStream(wr, PortCharEncoding.Utf16)` — UART пишет `char` в `StringWriter`. `Utf16` выбран потому, что `print16` печатает 16-битные символы.
- `vm.Run()` (не StepMode) — страница блокируется на время исполнения. Для маленьких программ это нормально, для больших — лаг UI (см. [Что исправить, п.11](#что-исправить)).

**Подсветка синтаксиса** — `TokenRegex`:

```csharp
new Regex(
    $@"(?<string>""(?:\\.|[^""\\])*"")|(?<directive>#include\b)|(?<keyword>(?:{VmHostProject.MiniCKeyword}))",
    RegexOptions.Compiled);
```

Три группы: строки (`#ce9178`), директивы (`#c586c0`), ключевые слова (`#569cd6`, bold). `HighlightedCode` вызывается в каждом рендере, перестраивает `StringBuilder` по `Matches`.

### `pages/Snake.razor` — игра

Страница `@page "/snake"`, `@implements IAsyncDisposable`, `@inject IJSRuntime JS`.

Константы:

| Имя | Значение | Смысл |
|---|---|---|
| `W`, `H`, `Cell` | `32`, `16`, `20` | размер поля и клетки (canvas 640×320) |
| `FixedDtMs` | `150` | период тика змейки (6–7 FPS) |
| `FrameBudgetMs` | `8` | как часто игровой цикл просыпается |
| `StepChunk` | `1000` | инструкций за один `BatchStep` |
| `MaxStepsPerTick` | `200_000` | предохранитель от зависания VM |

Поля состояния:
- `_canvas`, `_focusHost` — `ElementReference` для JS и фокуса;
- `_js` — `IJSObjectReference` на `./js/snake.js`;
- `_host`, `_ctx`, `_monitor`, `_commander` — устройства;
- `_cts`, `_loopTask` — жизненный цикл игрового цикла;
- `_input` — `ConcurrentQueue<byte>` для нажатий;
- `_frameBuffer`, `_hasNewFrame` — двойная буферизация кадра;
- `_score`, `_status`, `_running`, `_showGameOver`, `_disposed`.

`StartAsync`:
1. `_host = VmHostFactory.CreateDefault()`.
2. `SaveFileProject` + `SaveFileInclude` для `monitor.nanasm` и `commander.nanasm`.
3. `_frameBuffer = new byte[W*H]`, `_monitor = new MonochromeMonitor(W, H, OnFrameProduced)`, `_commander = new SnakeCommander()`.
4. `RegisterDevice(DeviceSpec.Create(RamSize.MB16, ...), 0)` — VM, `RegisterDevice(_monitor, 1)`, `RegisterDevice(_commander, 2)`.
   !!️ Используется **старый** `RegisterDevice` + `GetDeviceContext(cpuId)!` (не `Try*`), см. [Что исправить, п.1](#что-исправить).
5. `CompileToIR(0, optimize: false).Build()`; при ошибке — `_status = "Compile: ..."`.
6. `TryLoadProgram`; при ошибке — `_status = "Load: ..."`.
7. **`_ctx.RunStepMode(startAddress: 0)`** — переводит CPU в step-режим, инструкции выполняются только через `BatchStep`.
8. `_running = true; _cts = new CancellationTokenSource(); _loopTask = GameLoopAsync(_cts.Token);`.
9. `_focusHost.FocusAsync()` — для клавиатуры.

`GameLoopAsync` — `Input → Update → FixedUpdate → Render → Sleep`:

```csharp
while (!ct.IsCancellationRequested && _running)
{
    // 1. INPUT
    while (_input.TryDequeue(out byte key)) _commander!.SetInput(key);

    // 3. FIXED UPDATE
    long now = Environment.TickCount64;
    if (now - lastTick >= FixedDtMs)
    {
        lastTick = now;
        _commander!.SignalTick();

        int total = 0;
        while (total < MaxStepsPerTick && _running)
        {
            _ctx!.BatchStep(StepChunk);
            total += StepChunk;
            if (_commander.ConsumeYielded()) break;   // VM сказала "кадр готов"
        }

        _score = _commander.Score;
        if (_commander.ConsumeGameOver())
        {
            _showGameOver = true;
            await Task.Delay(900, ct);
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
            await _js.InvokeVoidAsync("draw", _canvas, b64, W, H);
            StateHasChanged();
        });
    }

    await Task.Delay(8, ct);
}
```

`HandleKeyDown` — маппинг `e.Key` в код змейки (`ArrowUp=1`, `ArrowDown=2`, `ArrowLeft=3`, `ArrowRight=4`), `q/Q/Escape` -> `StopAsync`. `@onkeydown:preventDefault="true"` глушит дефолт для **всех** клавиш (см. [Что исправить, п.7](#что-исправить)).

`OnFrameProduced(byte[] fb, int w, int h)` — **синхронный** колбэк из `MonochromeMonitor.WritePort(REG_COMMAND, CMD_FLUSH)` (тот же поток). Копирует `fb` в `_frameBuffer` и ставит `_hasNewFrame = true`. Это и есть двойная буферизация: `MonochromeMonitor` не отдаёт наружу живой `_fb`, а `_frameBuffer` в `Snake.razor` читается JS-вызовом уже после копирования.

`StopAsync`:
- `_running = false`, отмена `_cts`, `await _loopTask`, `_ctx?.EndStepMode()`, `_host?.Dispose()`;
- обнуление полей и очереди ввода;
- `_status = "Stopped. Press Start."`.

### `pages/SnakeCommander.cs` — порт управления

```csharp
public sealed class SnakeCommander : PortControlBase
{
    // Read (VM читает)
    public const byte R_INPUT      = 0x00; // 0..4, auto-consume
    public const byte R_TICK_READY = 0x01; // 0|1, auto-consume

    // Write (VM пишет)
    public const byte W_YIELD     = 0x00; // 1 = "кадр готов, управление вернулось"
    public const byte W_GAME_OVER = 0x02;
    public const byte W_SCORE     = 0x03;

    public void SignalTick();
    public void SetInput(byte key);       // игнорирует key == 0
    public bool ConsumeYielded();         // true -> сбрасывает флаг
    public bool ConsumeGameOver();
    public int  Score { get; }

    public override byte ReadPort(ulong offset)  => ...;
    public override void WritePort(ulong offset, byte value) => ...;
    public override void WakeProcessor() { }
}
```

- Регистрируется на `sector 2`. При дефолтном `DevicePortSize.B16` (`PortsOnDevice = 16 = 0x10`) база = `2 * 16 = 0x20`. Смещения `0x00..0x03` -> адреса `0x20..0x23` — совпадает с `commander_nanasm`.
- `R_INPUT` и `W_YIELD` — один и тот же offset `0x00`, но **разные направления** (чтение vs запись). В `SnakeCommander` это разведено через `ReadPort`/`WritePort`.
- `SetInput` намеренно не пишет `0` — "нет нажатия" и "нет нового нажатия" — разные состояния. Сброс `_pendingInput` происходит **на чтении** `R_INPUT` (`ConsumeInput`).
- `Score` — `int`, но `W_SCORE` — `byte`, поэтому счёт фактически ограничен `0..255` (см. [Что исправить, п.2](#что-исправить)).

### `pages/MonochromeMonitor.cs` — монохромный монитор

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

- `width`/`height` — 1..255, иначе `ArgumentOutOfRangeException`.
- `WritePort(REG_PIXEL, value)` пишет `value` в `_fb[_cy * Width + _cx]` и запоминает `_pixel`.
- `WritePort(REG_COMMAND, CMD_FLUSH)` вызывает `_renderer(_fb, Width, Height)` — колбэк получает **живой** `_fb`. В `Snake.razor` `OnFrameProduced` **сразу копирует** его в `_frameBuffer` (`Buffer.BlockCopy`), поэтому `snake.js` уже не видит гонку.
- `ReadPort(REG_FRAME)` — счётчик кадров (инкрементируется на `CMD_FLUSH`). `CMD_CLEAR` его **не** сбрасывает.
- Порты жёстко привязаны к `sector 1` + `B16` (база `0x10`).

### `pages/LibCode.cs` — исходники для змейки

```csharp
public static class LibCode
{
    public const string monitor_nanasm;   // func_MonClear, func_MonFlush, func_MonPlot
    public const string commander_nanasm; // func_CmdWaitTick, CmdReadInput, CmdYield, CmdGameOver, CmdSetScore
    public const string snake_minic;      // #include monitor/commander + игра
}
```

`monitor_nanasm` (порты через `0x10..0x13`):
- `MonClear` — `OUT 1` в `0x13` (`CMD_CLEAR`), `OUT 4` (`CMD_HOME`).
- `MonFlush` — `OUT 3` в `0x13` (`CMD_FLUSH`).
- `MonPlot(r0=x, r1=y, r2=v)` — `OUT r0, 0x10` (`REG_CURSOR_X`), `OUT r1, 0x11` (`REG_CURSOR_Y`), `OUT r2, 0x12` (`REG_PIXEL`).

`commander_nanasm` (порты через `0x20..0x23`):
- `CmdWaitTick` — busy-poll `IN 0x21` (`R_TICK_READY`) пока не `1`.
- `CmdReadInput` — `IN 0x20` (`R_INPUT`) — consume-on-read.
- `CmdYield` — `OUT 1 -> 0x20` (`W_YIELD`).
- `CmdGameOver` — `OUT 1 -> 0x22` (`W_GAME_OVER`).
- `CmdSetScore(r0)` — `OUT r0 -> 0x23` (`W_SCORE`).

`snake_minic` — игра:
- состояние (`sx[256]`, `sy[256]`, `slen`, `dir`, `foodX`, `foodY`, `rngState`) — в **глобалах** (легаси от старого бага `FunctionGenerator.GenerateFunction`, см. `Nantuc.Samples.Scratch` [Особенная информация](../Nantuc.Samples.Scratch/README.ru.md#особенная-информация));
- `rng()` — LCG `state * 1103515245 + 12345`;
- `spawnFood`, `resetGame`, `render` — почти как в Scratch;
- `tick()` — `CmdReadInput`, поворот (с защитой от разворота), шаг, **wrap по краям 32×16**, проверка столкновения; при смерти **не** вызывает `resetGame`, возвращает `1` (чтобы Blazor успел показать кадр);
- `main()` — `resetGame(); render(); CmdYield(); while(1) { CmdWaitTick(); if (tick()) { render(); CmdGameOver(); CmdYield(); resetGame(); } else { render(); CmdYield(); } }`.

Обратите внимание: **нет `Halt()`**, ожидание тика — `CmdWaitTick()` через `R_TICK_READY`. Это позволяет `BatchStep` не "съедать" цикл вхолостую, а останавливаться по `ConsumeYielded`. См. [Особенная информация](#особенная-информация).

### `wwwroot/js/snake.js` — рендер кадра

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

- Принимает base64-кодированный `_frameBuffer` (512 байт для 32×16), декодирует в строку `bin`, рисует прямоугольники 18×18 с отступом 1px.
- Не хранит ссылку на canvas между вызовами — canvas пересоздаётся Blazor'ом только при `@ref`-пересборке, что здесь не делается.
- `atob` — стандартный Web API, `charCodeAt` даёт `byte` (для 32×16 всегда `< 256`).

### `wwwroot/index.html` / `wwwroot/css/app.css`

- Стандартный WASM-шаблон + Blazor loading-progress (`<svg class="loading-progress">` с двумя `<circle>`, `stroke-dasharray: calc(3.141 * var(--blazor-load-percentage) * 0.8)`), текст — `var(--blazor-load-percentage-text)`.
- `<script src="_framework/blazor.webassembly#[.{fingerprint}].js"></script>` — fingerprinting, ставится publish-таргетом.
- `<link rel="preload" id="webassembly" />` — плейсхолдер, который boot-скрипт Blazor заполняет `href` на `.wasm`.
- `#blazor-error-ui` — жёлтая панель снизу, показывается при необработанных исключениях Blazor. У нас — стандартная.

## Примеры для API

**Запустить WASM-демо:**

```bash
dotnet run
# затем откройте http://localhost:5xxx/         — Home
# и http://localhost:5xxx/snake                 — Snake
```

**Проверить Hello world на странице `/`:**

Код по умолчанию:

```c
#include "print.minic"

char str[] = "Hello world from WASM! Привет мир из WASM!";

void main() {
    print16(str);
}
```

Нажмите "Скомпилировать и запустить" — в `<pre>` под кнопкой появится та же строка (UTF-16-строка прошла через `print16` -> UART -> `StringWriter` -> Blazor).

**Изменить программу на `/`:**

Замените на:

```c
#include "print.minic"

void main() {
    print16("Nantuc in WASM");
}
```

и нажмите кнопку — получите `Nantuc in WASM`.

**Сыграть в змейку:**

1. Откройте `/snake`, нажмите **Start** — начнётся компиляция `snake_minic`, затем `RunStepMode` и игровой цикл.
2. Управление: **стрелки** (Up/Down/Left/Right), выход — **Q** / **Escape**.
3. **Stop** — остановит VM (`EndStepMode`) и освободит `_host`.
4. **Start** после Stop — новый `VmHost`, новая VM, счёт с 0.

**Добавить свой пример использования VM в браузере:**

Создайте `pages/MyDemo.razor`:

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

Добавьте ссылку на страницу в `pages/Home.razor` (`<a href="/my-demo">My demo</a>`).

**Добавить свой порт в монитор:**

1. В `pages/MonochromeMonitor.cs` — новый `REG_*` и ветка в `ReadPort`/`WritePort`.
2. В `pages/LibCode.cs` — обёртка в `monitor_nanasm` (учесть `sector 1 -> base 0x10`).
3. В `pages/Snake.razor` — ничего, если порт читается/пишется только из VM.

## Приближенность модуля к пользователю

**Высокая** для "хочу показать Nantuc в браузере": две готовые страницы, живая игра, JS-канвас, подсветка синтаксиса, безопасный `Try*` API.

**Средняя** для "хочу писать свои игры на Nantuc": `Snake.razor` — плотный пример, но с заметной спецификой — step-режим, кооперативный yield, двойная буферизация кадра. Автору своей игры придётся разобраться, зачем `RunStepMode`/`BatchStep`, а не `Run()`.

**Низкая** для "хочу изучить внутренности VM": этот сэмпл — про интеграцию, а не про архитектуру (для этого — `Nantuc.VmApplication`, `Nantuc.Compiler`, `Nantuc.Kernel`).

**Что стоит делать:**
- Запускать `dotnet run` и открывать обе страницы — они показывают два разных сценария.
- Для отладки змейки — уменьшить `FixedDtMs` (например, до 300 мс) или увеличить `FrameBudgetMs`.
- Использовать `TryRegisterDevice`/`TryGetDeviceContext` в новом коде — как в `Home.razor`.
- Передавать в `MonochromeMonitor` колбэк, **копирующий** `fb`, а не сохраняющий ссылку.

**Что не стоит делать:**
- Ждать, что `BatchStep` сам вернёт управление "когда надо" — надо проверять `ConsumeYielded()`.
- Менять `StepChunk`/`MaxStepsPerTick` без понимания, что `BatchStep` — синхронный: слишком большой `StepChunk` заморозит UI, слишком маленький — увеличит накладные расходы.
- Полагаться на то, что `_commander.SetInput` доставит все нажатия между тиками — см. [Что исправить, п.5](#что-исправить).
- Использовать `RegisterDevice` + `GetDeviceContext(id)!` — как в `Snake.razor.StartAsync`; лучше `Try*` (см. [Что исправить, п.1](#что-исправить)).

## Особенная информация

### `RunStepMode` + `BatchStep` + кооперативный yield

В браузере нет `Thread.Sleep`, `Console.ReadKey` и нет "фонового потока VM". Поэтому игра построена иначе, чем в `Nantuc.Samples.Scratch`:

- `_ctx.RunStepMode(0)` — VM **не выполняется**, пока не позовут `BatchStep`.
- `_ctx.BatchStep(StepChunk)` — выполнить до `StepChunk` инструкций.
- `_commander.SignalTick()` — VM "разбужена": `CmdWaitTick()` увидит `R_TICK_READY = 1`.
- `_commander.ConsumeYielded()` — VM закончила тик: `CmdYield()` записала `W_YIELD = 1`.

Порядок в `GameLoopAsync`:
1. `SignalTick()` -> `R_TICK_READY = 1`.
2. Цикл `BatchStep(1000)`, пока VM не сделает `CmdYield` или не наберётся `MaxStepsPerTick`.
3. `_score = _commander.Score` — после `ConsumeYielded`.
4. Если VM умерла и вызвала `CmdGameOver` — оверлей 900 мс.
5. Кадр (`_hasNewFrame`) отдаётся в JS.

**Важно:** между тиками VM **не выполняется**. Это и есть экономия CPU в браузере. `MaxStepsPerTick = 200_000` — предохранитель: если VM застряла в `while(1)` без `CmdYield`, цикл выйдет по лимиту и UI не зависнет.

### Двойная буферизация кадра

`MonochromeMonitor.WritePort(REG_COMMAND, CMD_FLUSH)` синхронно вызывает `_renderer(_fb, Width, Height)`. Если бы `Snake.razor` передавал `_fb` напрямую в `snake.js`, JS успел бы прочитать буфер, пока VM пишет следующий кадр. Поэтому:

- `OnFrameProduced` делает `Buffer.BlockCopy(fb, 0, _frameBuffer, 0, fb.Length)` и ставит `_hasNewFrame = true`;
- `GameLoopAsync` копирует `_frameBuffer` в base64 и передаёт в JS;
- JS работает уже с **копией**.

Это делает `MonochromeMonitor` безопасным при **синхронном** рендерере. Если завтра `renderer` станет асинхронным — контракт придётся менять (см. [Что исправить, п.9](#что-исправить)).

### Порты жёстко привязаны к `B16`

`monitor_nanasm` использует `0x10..0x13`, `commander_nanasm` — `0x20..0x23`. Это работает, пока `DevicePortSize.B16` (`PortsOnDevice = 0x10`):

- `sector 1` (monitor) -> база `0x10`;
- `sector 2` (commander) -> база `0x20`.

При смене `portsPerDevice` в `VmHostFactory.CreateDefault(...)` (например, на `B32`) база `sector 1` станет `0x20`, и `monitor_nanasm` начнёт писать в **sector 2** (commander) вместо монитора. Это хрупкое место, унаследованное от Scratch.

### `commander_nanasm`: `R_INPUT` и `W_YIELD` — один offset

В `SnakeCommander`:

- `ReadPort(0x00)` -> `ConsumeInput()` (читает `_pendingInput`, сбрасывает в 0);
- `WritePort(0x00, v)` -> если `v != 0`, `_yielded = true`.

Это позволяет `input_nanasm`/`commander_nanasm` использовать **один** адрес `0x20` и для чтения, и для записи. `OUT r0, r1` с `r1 = 0x20` никогда не путается с `IN r0, r1` с тем же `r1` — на шине разные операции.

### `SetInput` и "последнее нажатие выигрывает"

`_input` — `ConcurrentQueue<byte>`, но `SnakeCommander.SetInput` перезаписывает `_pendingInput`. Если два нажатия пришли между `CmdReadInput` (200 мс — окно), второе затрёт первое. Для змейки это нормально (хотим "последнее направление"), но для более сложных игр — потеря ввода. См. [Что исправить, п.5](#что-исправить).

### Game Over: сброс **до** показа оверлея

В `snake_minic`:

```
if (tick()) {
    render();          // кадр аварии (голова уже на месте столкновения)
    CmdGameOver();     // _gameOver = true
    CmdYield();        // _yielded = true — Blazor увидит
    resetGame();       // _score = 0, змейка снова в (16,8)
}
```

`resetGame` вызывается **после** `CmdYield`. Это значит, что к моменту `ConsumeYielded` и `_score = _commander.Score` (в `GameLoopAsync`) VM **уже** сброшена: счёт уже 0. А `_showGameOver = true` покажет оверлей на 900 мс. За это время `GameLoopAsync` успеет сделать ещё ~6 тиков (900/150) — и новая игра уже будет идти. Игрок увидит: "GAME OVER" 900 мс, счёт 0, затем оверлей исчезает — а змейка уже сдвинулась на 6 клеток. Не баг, но UX-особенность. См. [Что исправить, п.3](#что-исправить).

### `Home.razor`: `RegexOptions.Compiled` в WASM — no-op

`TokenRegex` объявлен с `RegexOptions.Compiled`. В WASM (AOT) `Reflection.Emit` недоступен, поэтому `Compiled` молча игнорируется (используется интерпретатор). Для 220px-редактора это нормально, но оптимизации нет. При желании — `RegexOptions.NonBacktracking` или source-generated `[GeneratedRegex]` в .NET 7+.

### `Home.razor`: `vm.Run()` блокирует UI

`RunNantucVm` синхронный: `CompileToIR` + `vm.Run()` выполняются в UI-потоке Blazor WASM. Для `Hello world` — миллисекунды, для программы на 100k инструкций — заметный фриз. В WASM нет реальных потоков, `Task.Run` не поможет — только разбивать `vm.Run()` на `RunStepMode`/`BatchStep` и `await Task.Delay(0)` между чанками. Сейчас — сознательный компромисс в пользу простоты.

### `@onkeydown:preventDefault="true"` на `<div tabindex="0">`

Глушит **все** дефолтные действия клавиатуры, включая `Tab` (навигация), `F5` (обновление), `Ctrl+R` и т.п. См. [Что исправить, п.7](#что-исправить).

### `_focusHost.FocusAsync()` и потеря фокуса

`StartAsync` вызывает `FocusAsync()` на обёртке. Если пользователь кликнет вне игры, стрелки перестанут работать. Обработчика `@onfocusin`/`@onclick` для возврата фокуса нет. См. [Что исправить, п.8](#что-исправить).

## Что исправить

По убыванию серьёзности.

1. **`Snake.razor.StartAsync` — `RegisterDevice` + `GetDeviceContext(cpuId)!` без `Try*`.**
   Файл: `pages/Snake.razor`, строки:
   ```csharp
   int cpuId = _host.Emulator.RegisterDevice(handle, sector: 0);
   ...
   _ctx = _host.Emulator.GetDeviceContext(cpuId)!;
   ```
   Если сектор 0 занят, `RegisterDevice` вернёт `-2`, `GetDeviceContext(-2)` вернёт `null`, `!` упадёт NRE уже в `_ctx!.TryLoadProgram(...)`. Это **расхождение с `Home.razor`**, где уже используется `TryRegisterDevice`/`IdState.Valid` — читатель видит два разных API для одной задачи.
   Фикс: `TryRegisterDevice` + проверка `IdState.Valid` + `TryGetDeviceContext` (как в `Home.razor`). Аналогично для `_monitor` и `_commander`.

2. **`SnakeCommander.Score` — `int`, а `W_SCORE` — `byte`.**
   Файл: `pages/SnakeCommander.cs`, `public int Score => _score;` и `case W_SCORE: _score = value; break;`. `value` — `byte`, поэтому счёт ограничен `0..255`. Поле 32×16 = 512 клеток, змейка теоретически может набрать 509.
   Фикс: либо `ushort` через два порта (`W_SCORE_LO`/`W_SCORE_HI`), либо клампить в `Math.Min(value, 255)` и документировать. Или перейти на `W_SCORE_LO` = `byte`, `W_SCORE_HI` = `byte` и `score = lo | (hi << 8)`.

3. **`Snake.razor.GameLoopAsync` — Game Over: новая игра уже идёт под оверлеем.**
   Файл: `pages/Snake.razor`, блок `if (_commander.ConsumeGameOver()) { _showGameOver = true; await Task.Delay(900, ct); _showGameOver = false; }`.
   `resetGame()` в `snake_minic` выполняется сразу после `CmdYield()`, и VM продолжает тикать в следующих итерациях `GameLoopAsync`. За 900 мс оверлея VM успевает сделать ~6 тиков.
   Фикс: на время `_showGameOver` **не** вызывать `_commander.SignalTick()` — тогда VM останется в `CmdWaitTick` до конца оверлея. Либо перенести `resetGame()` в `main` после `CmdWaitTick`-следующего-тика.

4. **`Snake.razor.StartAsync` — `catch (Exception)` не освобождает `_host`.**
   Файл: `pages/Snake.razor`, `catch (Exception ex) { _running = false; _status = "Start failed: " + ex.Message; StateHasChanged(); }`.
   Если исключение произошло **после** `_host = VmHostFactory.CreateDefault()` (например, в `CompileToIR`), `_host` остаётся живым до `DisposeAsync` компонента. При повторном `Start` — новый `VmHost` при живом старом.
   Фикс: `try { ... } catch { _host?.Dispose(); _host = null; _ctx = null; _monitor = null; _commander = null; throw; }` или использовать `try/finally`.

5. **`SnakeCommander.SetInput` теряет промежуточные нажатия между тиками.**
   Файл: `pages/SnakeCommander.cs`, `public void SetInput(byte key) { if (key != 0) _pendingInput = key; }`.
   За 150 мс пользователь может нажать 2–3 стрелки; `_input` (`ConcurrentQueue`) их сохранит, но `GameLoopAsync` дренирует очередь каждые 8 мс, вызывая `SetInput` на каждую; `SetInput` перезаписывает `_pendingInput`. В итоге `CmdReadInput` увидит только **последнее**.
   Фикс: либо `_pendingInputs` — очередь в `SnakeCommander` (с лимитом, чтобы не росла), либо в `GameLoopAsync` передавать только последнюю нажатие (как сейчас, но **осознанно**), задокументировав "last wins". Для змейки это ОК, для RTS/файтинга — нет.

6. **`Snake.razor.GameLoopAsync` — `await Task.Delay(900, ct)` вне `try/catch`.**
   Файл: `pages/Snake.razor`. Если `ct` отменён во время Game Over, `OperationCanceledException` уйдёт из `GameLoopAsync`, `_showGameOver = false` не выполнится. `StopAsync` ловит `await _loopTask` в `try/catch`, но оверлей останется висеть до перерендера.
   Фикс: `try { await Task.Delay(900, ct); } finally { _showGameOver = false; }`.

7. **`Snake.razor` — `@onkeydown:preventDefault="true"` глушит `Tab`/`F5`/`Ctrl+R`.**
   Файл: `pages/Snake.razor`, `<div class="snake-wrap" tabindex="0" @onkeydown:preventDefault="true">`.
   Ломает доступность и дефолтные браузерные горячие клавиши.
   Фикс: убрать `preventDefault` с div и в `HandleKeyDown` для стрелок/`Q`/`Escape` вызывать `e.preventDefault()` — но Blazor-событие не даёт `preventDefault` из C#; придётся ставить JS-обработчик `keydown` с `preventDefault` только для `Arrow*`, либо оставить как есть и задокументировать.

8. **`Snake.razor` — нет возврата фокуса после клика вне игры.**
   Файл: `pages/Snake.razor`. `_focusHost.FocusAsync()` вызывается только в `StartAsync`.
   Фикс: добавить `@onclick="_ => _focusHost.FocusAsync()"` на `.snake-wrap` (или `@onfocusin`) и/или `tabindex="-1"` на `<canvas>`.

9. **`MonochromeMonitor` — `_renderer(_fb, Width, Height)` отдаёт живой буфер.**
   Файл: `pages/MonochromeMonitor.cs`, `CMD_FLUSH`. В `Snake.razor` `OnFrameProduced` копирует — поэтому сейчас безопасно. Но контракт `Action<byte[], int, int>` этого не гарантирует.
   Фикс: XML-doc `/// <summary>Renderer must copy the buffer; it is reused on next flush.</summary>` или передавать копию (лишние 512 байт на кадр — незначительно).

10. **`Snake.razor.GameLoopAsync` — `BatchStep` при `Halted` VM крутит пустые итерации.**
    Файл: `pages/Snake.razor`, `while (total < MaxStepsPerTick && _running) { _ctx!.BatchStep(StepChunk); ... }`.
    Если VM дошла до `HALT` (в `snake_minic` этого нет, но в других программах возможно), `BatchStep` вернётся мгновенно, а цикл сделает `200_000 / 1000 = 200` пустых итераций.
    Фикс: проверять `_ctx.IsHalted` (если такое API есть) и выходить из цикла.

11. **`Home.razor` — `vm.Run()` синхронный, блокирует UI.**
    Файл: `pages/Home.razor`, `RunNantucVm`. Для длинных программ — фриз Blazor WASM (анимация, ввод).
    Фикс: перейти на `RunStepMode` + `BatchStep` с `await Task.Delay(0)` между чанками (как в `Snake.razor`), либо оставить как есть и показать предупреждение "выполняется...".

12. **`Home.razor` — `HasError` определяется по префиксу `Output`.**
    Файл: `pages/Home.razor`, `HasError = Output.StartsWith("[ERROR]") || Output.StartsWith("VM fault:");`.
    Хрупко: любое сообщение пользовательской программы, начинающееся с `[ERROR]`, будет принято за ошибку.
    Фикс: возвращать из `RunNantucVm` кортеж `(string output, bool isError)` или бросать кастомное исключение на ошибках.

13. **`Snake.razor` — `_js` может быть `null` во время `StartAsync`.**
    Файл: `pages/Snake.razor`, `OnAfterRenderAsync(first)` подгружает `snake.js`. Если пользователь нажал Start **до** завершения `OnAfterRenderAsync`, `_js == null`, кадры не рисуются (`_hasNewFrame` сбрасывается в `false` до проверки `_js`). Следующий кадр придёт через 150 мс — не критично, но первые ~2 кадра теряются.
    Фикс: в `StartAsync` дождаться `_js` (например, `if (_js is null) await OnAfterRenderAsync(...)` — костыльно) или `TaskCompletionSource`, который резолвится в `OnAfterRenderAsync`.

14. **`Snake.razor` — `OnFrameProduced` использует `_frameBuffer!` без проверки.**
    Файл: `pages/Snake.razor`, `Buffer.BlockCopy(fb, 0, _frameBuffer!, 0, fb.Length)`. Сейчас безопасно (буфер инициализируется до `RunStepMode`), но `!` — потенциальный NRE при рефакторинге.
    Фикс: `if (_frameBuffer is null || _frameBuffer.Length < fb.Length) return;` — и `_frameBuffer = new byte[fb.Length]`.

15. **`Snake.razor` — `_score`/`_showGameOver`/`_status` не `volatile`.**
    Файл: `pages/Snake.razor`. В WASM single-threaded — безопасно. При переносе на Server/Blazor Hybrid с фоновым потоком — гонка.
    Фикс (профилактически): либо задокументировать "single-threaded assumption", либо `volatile` / `lock`.

16. **`LibCode.input_nanasm` — гонка двух нажатий (нет в InWeb, но есть в Scratch).**
    В InWeb `snake_minic` использует `commander_nanasm`, а не `input_nanasm`. Но `SnakeCommander.SetInput` всё равно теряет промежуточные нажатия (см. п.5).

17. **`Snake.razor` — `MonochromeMonitor` и `SnakeCommander` используют `RegisterDevice` (bool-непроверяемый) на `sector 1`/`sector 2`.**
    Файл: `pages/Snake.razor`, `_host.Emulator.RegisterDevice(_monitor, name: "monitor", sector: 1);` — без `out`-параметра. Если `sector 1` занят — исключение/`-2`, не проверяется. То же для `sector 2`.
    Фикс: `TryRegisterDevice(_monitor, "monitor", 1, out _)` + проверка `IdState.Valid`.

18. **`pages/Home.razor` — `char str[]` в строке по умолчанию содержит `Привет мир` (UTF-8 в исходнике).**
    Файл: `pages/Home.razor`, `SourceCode`. Если компилятор MiniC не поддерживает нерусские строковые литералы в UTF-8 — программа не скомпилируется. В текущей версии — компилируется (UTF-8 в WASM работает), но это стоит проверить на CI.

19. **`pages/Snake.razor` — нет паузы.**
    Файл: `pages/Snake.razor`. Только Start/Stop. "Пауза" реализуется как Stop+Start (пересоздание `VmHost` и VM, счёт теряется). Для отладки — неудобно.
    Фикс: `_running = false` без `_ctx.EndStepMode()` и `_host.Dispose()` — тогда можно возобновить с того же места. Или отдельная кнопка Pause.

20. **`pages/Snake.razor` — `lastFrame` объявлен, но не используется.**
    Файл: `pages/Snake.razor`, `long lastFrame = lastTick;` и `lastFrame = now;` в render-блоке. Мёртвый код.
    Фикс: удалить (или использовать для FPS-счётчика).

---

**Сравнение с `Nantuc.Samples.Scratch`:**

| Аспект | Scratch (консоль) | InWeb (браузер) |
|---|---|---|
| Исполнение VM | `ctx.Run()` (блокирующее) | `RunStepMode` + `BatchStep` (кооперативное) |
| Ожидание тика | `Halt()` + `WakeProcessor` | `CmdWaitTick()` (busy-poll через порт) |
| Ввод | `portBus.WritePort(0, code)` (loopback) | `SnakeCommander` на `sector 2` |
| Рендер | ANSI в `Console` | `snake.js` + `<canvas>` (JS Interop) |
| Буферизация кадра | нет (ANSI сразу) | двойная (`_fb` -> `_frameBuffer` -> JS) |
| Регистрация | `RegisterDevice` + `!` (легаси) | `TryRegisterDevice`/`TryGetDeviceContext` (Home) |
| Таймер | `System.Threading.Timer` | `Task.Delay` в `async`-цикле |
| `Halt` | есть (`halt_nanasm`) | нет (`commander_nanasm`) |
| `keyboard.nanasm` | есть (не используется) | нет (ввод через `SnakeCommander`) |

Оба сэмпла — про одну и ту же игру, но с **разными транспортными слоями**. Scratch полезен как "как быстро запустить VM в консоли", InWeb — как "как встроить VM в браузерный UI".