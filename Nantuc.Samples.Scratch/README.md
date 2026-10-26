# Nantuc.Samples.Scratch — README

> Песочница для экспериментов на платформе Nantuc: тест аллокатора (`Program.cs`), игра «Змейка» с монохромным монитором и клавиатурой (`SnakeGame/`), готовые библиотеки `monitor.nanasm` / `input.nanasm` / `halt.nanasm` / `snake.minic`.  
> Зависит от `[Nantuc.VmApplication.Extensions](../Nantuc.VmApplication.Extensions/README.md)` (транзитивно — `VmApplication`, `Compiler`, `Kernel`).  
> `[← Корень](../README.md) · [VmApplication](../Nantuc.VmApplication/README.md) · [Extensions](../Nantuc.VmApplication.Extensions/README.md) · [Compiler](../Nantuc.Compiler/README.md) · [Kernel](../Nantuc.Kernel/README.md)`

## Содержание
- [Об этом модуле](#об-этом-модуле)
- [Дерево локального проекта](#дерево-локального-проекта)
- [API](#api)
- [Примеры для API](#примеры-для-api)
- [Приближенность модуля к пользователю](#приближенность-модуля-к-пользователю)
- [Особенная информация](#особенная-информация)
- [Что исправить](#что-исправить)

## Об этом модуле

`Nantuc.Samples.Scratch` — это **набор экспериментов** (scratchpad), а не один пример. Сейчас в проекте живут два независимых сценария:

1. **`Program.cs`** — тест аллокатора. Компилирует MiniC-программу, которая делает `alloc`/`free`/`alloc` через `Librare.alloc_minic`, проверяет возврат кода (`return 1..7` для разных провалов, `return 10` для успеха), запускает VM, снимает регистры, дизассемблирует RAM через `StringDisassembler`.
2. **`SnakeGame/`** — игра «Змейка». `SnakeLauncher.LaunchSnake(period)` создаёт `VmHost`, регистрирует `MonochromeMonitor` (32×16) на `sector 1`, VM на `sector 0`, компилирует `LibCode.snake_minic` (включает `monitor.nanasm`, `input.nanasm`, `halt.nanasm`), запускает VM. Игра крутится в бесконечном цикле `while (1) { Halt(); tickGame(); drawGame(); }`, тикер `Timer` каждые `period` мс будит процессор (`portBus.WakeProcessor(0)`), клавиатура читается в фоновом потоке и пишется в `portBus.WritePort(0, code)`.

**Важно:** `Program.cs` **не** вызывает `SnakeLauncher.LaunchSnake()`. Сейчас `using Nantuc.Samples.Scratch.SnakeGame;` импортирован, но не используется — это задел/переключатель. Чтобы запустить змейку, нужно либо заменить тело `Main`, либо раскомментировать вызов `SnakeLauncher.LaunchSnake()` в `Program.cs`. См. [Что исправить, п.2](#что-исправить).

Проект показывает, как из `VmApplication` + `Extensions` собирается **настоящая игра**: несколько устройств на port-шине, фоновый ввод, таймер-тикер, ANSI-рендер в консоль, HALT/WAKE для энергоэффективного game loop.

## Дерево локального проекта

Namespace `Nantuc.Samples.Scratch` (корень) + `Nantuc.Samples.Scratch.SnakeGame` (вложенная папка):

```
Nantuc.Samples.Scratch/
├── Nantuc.Samples.Scratch.csproj   # net10.0, Exe, ref → VmApplication.Extensions
├── Program.cs                       # top-level: тест аллокатора + дамп регистров + дизасм
└── SnakeGame/
    ├── KeyboardDevice.cs            # IPortController: 2 порта (REG_KEY, REG_HAS_NEW)
    ├── LibCode.cs                   # const string: monitor_nanasm, input_nanasm, halt_nanasm, snake_minic
    ├── MonochromeMonitor.cs         # IPortController: 32×16 framebuffer, 7 портов
    └── SnakeLauncher.cs             # LaunchSnake(period) — запуск змейки
```

Замечания по организации:
- `Program.cs` — top-level statements без класса, без namespace. Для проекта-песочницы это ок, но при росте экспериментов стоит вынести каждый в отдельный `Sample*.cs` с явным `public static void Run()`.
- `KeyboardDevice.cs` в текущей версии **не используется** `SnakeLauncher` — ввод идёт через `portBus.WritePort(0, code)` (loopback в собственное IO-окно `Device`). См. [Что исправить, п.6](#что-исправить).

## API

Публичного API нет — это `Exe`. Ниже — состав экспериментов.

### `Program.cs` — тест аллокатора

Top-level statements, последовательность:

| Шаг | Что делает |
|---|---|
| `source1` | MiniC: `#include "alloc.minic"`, `int main()` с 5 сценариями alloc/free, `return s` (10 при успехе) |
| `using var host = VmHostFactory.CreateDefault();` | `RamFileService` + дефолтные порты |
| `host.Project.SaveFileProject("main.minic", source1)` | Исходник в `ProjectPath` |
| `SaveFileInclude("heap.nanasm", Librare.heap_nanasm)` | asm-lib (bump + free-list) |
| `SaveFileInclude("alloc.minic", Librare.alloc_minic)` | C-lib (first-fit аллокатор) |
| `DeviceSpec.Create(RamSize.MB128, lfPolicy, "ConsoleVM")` | VM + `LoggingFaultPolicyMinimal` |
| `QueuedIOStream(Console.Out, PortCharEncoding.Utf8)` | I/O на `sector 1` (не используется в `source1`) |
| `CompileToIL(0, optimize: true).Build()` | Компиляция всех файлов |
| `TryFastLoadProgram` → `Run()` | Загрузка и запуск |
| `GetRegistersSnapshot()` | Дамп `r0..r5`, `rSP`, `rHP` |
| `GetRamAsNewArray(0, hpPrt)` + `StringDisassembler.Disassemble(ramSnap, resCompile.CodeSize)` | Дизасм RAM до текущего `rHP` |

`source1` возвращает `int`, но `deviceCtx.Run()` возвращает `void` — фактический код возврата лежит в `r0` после `Run()`. `Program.cs` печатает `r0`, но **не проверяет**, что он равен 10. См. [Что исправить, п.3](#что-исправить).

### `SnakeGame/MonochromeMonitor` — монохромный дисплей 32×16

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

- `width`/`height` — 1..255, иначе `ArgumentOutOfRangeException`.
- Внутри — `byte[] _fb` (`width * height`), курсор `_cx`/`_cy`, `_pixel`, `_frame` (инкрементится на `CMD_FLUSH`).
- `WritePort(REG_PIXEL, value)` пишет `value` в `_fb[_cy * Width + _cx]` и запоминает `_pixel`.
- `WritePort(REG_COMMAND, CMD_FLUSH)` вызывает `_renderer(_fb, Width, Height)` — колбэк получает **ссылку на живой буфер** (не копию). Если рендерер долго работает, а VM пишет — data race.
- `ReadPort(REG_FRAME)` — для синхронизации рендера с VM (номер кадра).

`SnakeLauncher.RenderToConsole` — рендерер, который рисует `fb` через ANSI (`\x1b[H` + `██`/`  `), плюс подсказку `Arrows=move Q=quit`.

### `SnakeGame/KeyboardDevice` — клавиатура (не используется)

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

- `Press(key)` — записывает `_key = key; _hasNew = true`.
- `ReadPort(REG_KEY)` → `_key`. `ReadPort(REG_HAS_NEW)` → `1` если новое нажатие, `0` иначе (consume-on-read).
- `WritePort(REG_KEY, 0)` — сброс `_key = 0`.
- В текущем `SnakeLauncher` **не регистрируется**: ввод идёт напрямую через `portBus.WritePort(0, code)` в loopback-окно `Device`. См. [Особенная информация](#особенная-информация).

### `SnakeGame/LibCode` — исходники

```csharp
public static class LibCode
{
    public const string monitor_nanasm;   // func_MonClear, func_MonFlush, func_MonPlot
    public const string input_nanasm;     // func_ReadKey (читает порт 0, сбрасывает в 0)
    public const string halt_nanasm;      // func_Halt (HALT; RET)
    public const string snake_minic;      // #include monitor/input/halt + сама игра
}
```

`monitor_nanasm` (порты монитора через `0x10..0x13`):
- `MonClear` — `OUT 1` в порт `0x13` (`CMD_CLEAR`), затем `OUT 4` (`CMD_HOME`).
- `MonFlush` — `OUT 3` в `0x13` (`CMD_FLUSH`).
- `MonPlot(r0=x, r1=y, r2=v)` — `OUT r0, 0x10` (`REG_CURSOR_X`), `OUT r1, 0x11` (`REG_CURSOR_Y`), `OUT r2, 0x12` (`REG_PIXEL`).

`input_nanasm` (`func_ReadKey`):
```
IN r0, r1;          // читаем порт 0x00
CMP r0, r3;         // r3 = 0
JZ  no_key;
MOV r3, r0;         // сохранили код
OUT r0, r1;         // записали 0 в порт 0 (сброс)
MOV r0, r3;         // вернули код
no_key: RET;
```

`snake_minic` — игра:
- Состояние (`sx[256]`, `sy[256]`, `slen`, `dir`, `fx`, `fy`, `rngState`) — в **глобалах**, не в локалах. Это обход бага кодогенератора (см. [Особенная информация](#особенная-информация)).
- `rng()` — LCG `rngState * 1103515245 + 12345`, возвращает `(rngState >> 16) & 0x7FFF`.
- `spawnFood` — пытается `rng() % 32`, `rng() % 16`, пока не попадёт в свободную клетку (не в змейку).
- `initGame` — старт: `slen = 3`, `dir = 4` (вправо), `sx[0..2] = 16,15,14`, `sy = 8`, `rngState = 12345`.
- `drawGame` — `MonClear`, рисует еду, рисует змейку, `MonFlush`.
- `tickGame` — читает клавишу (`ReadKey`), меняет `dir` (с защитой от разворота на 180°), сдвигает голову, wrap по краям (32×16 тороид), проверяет столкновение с собой (`th = 1`) → `initGame`, иначе сдвиг хвоста (`te = slen - 1`, сдвиг с конца), `slen++` если съел еду.
- `main` — `initGame(); drawGame(); while (1) { Halt(); tickGame(); drawGame(); }`.

`Halt()` — `HALT; RET`. Процессор уходит в сон, тикер будит его через `portBus.WakeProcessor(0)`.

### `SnakeGame/SnakeLauncher` — запуск

```csharp
public class SnakeLauncher
{
    public static void LaunchSnake(int period = 200);
    private static void RenderToConsole(byte[] fb, int w, int h);
}
```

Последовательность:
1. `VmHostFactory.CreateDefault()`.
2. `SaveFileProject`/`SaveFileInclude` для `snake.minic`, `monitor.nanasm`, `halt.nanasm`, `input.nanasm`.
3. `MonochromeMonitor(32, 16, RenderToConsole)`.
4. `DeviceSpec.Create(RamSize.MB16, name: "SnakeVM")` → `sector 0`.
5. `monitor` → `sector 1`.
6. `ctx = GetDeviceContext(deviceId)!`.
7. `CompileToIL(0, optimize: false).Build()`, `TryFastLoadProgram`.
8. `inputThread` (background): читает `Console.ReadKey(intercept: true)`, конвертирует стрелки в `1..4`, `Q`/`Escape` → `running = false; ctx.Stop()`. Пишет `portBus.WritePort(0, code)`.
9. `Timer(..., null, 200, period)` — `portBus.WakeProcessor(0)` каждые `period` мс.
10. `ctx.Run()` (блокируется до `Stop`).
11. `finally` — `running = false`, курсор видим, ANSI-reset, курсор на строку 18.

## Примеры для API

**Запустить змейку (заменить `Program.cs`):**

```csharp
using Nantuc.Samples.Scratch.SnakeGame;

SnakeLauncher.LaunchSnake(period: 200);
```

**Запустить с другим тиком (медленнее/быстрее):**

```csharp
SnakeLauncher.LaunchSnake(period: 100);   // 10 FPS
SnakeLauncher.LaunchSnake(period: 50);    // 20 FPS — сложнее
```

**Добавить монитор в свой пример:**

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
// ... компиляция и загрузка программы, которая пишет в порты 0x10..0x16
```

**Тест аллокатора с проверкой кода возврата:**

```csharp
// в Program.cs после deviceCtx.Run():
var regs = deviceCtx.GetRegistersSnapshot();
ulong ret = regs[RegType.r0];
if (ret != 10)
{
    Console.Error.WriteLine($"Allocator test FAILED: r0={ret} (expected 10)");
    return;
}
Console.WriteLine("Allocator test PASSED");
```

## Приближенность модуля к пользователю

**Средняя.** Это песочница, но она ближе к «конечному продукту», чем `Nantuc.Samples.Minimal`: здесь есть играбельная змейка, устройства (монитор, клавиатура), фоновый ввод, таймер. Автору собственных игр/демо — прямой пример; автору ядра — тест-полигон для новых фич.

**Что стоит делать:**
- Перед запуском змейки — убедиться, что терминал поддерживает ANSI (`\x1b[H`). Windows Terminal — да, старый `cmd.exe` — нет.
- Запускать `SnakeLauncher.LaunchSnake()` **вместо** `Program.cs` (через замену тела `Main` или отдельный проект), а не в дополнение — иначе `Main` сначала выполнит тест аллокатора и повиснет на `Console.ReadLine()`? Нет, `Program.cs` не вызывает `ReadLine`, но выполняет свой сценарий полностью.
- Для отладки змейки — увеличить `period` (медленнее, проще ловить баги) или заменить `QueuedIOStream` на `SynchronousIOStream` в `Program.cs` (для аллокатора).
- Использовать `LoggingFaultPolicyMinimal` (как в `Program.cs`) — при fault'е в змейке будет видно `[FAULT] status=...`.

**Что не стоит делать:**
- Регистрировать `KeyboardDevice` и одновременно писать в `portBus.WritePort(0, ...)` — двойной ввод.
- Менять `portWrite = 0x10` в `snake_minic` без пересчёта `DevicePortSize` (см. [Особенная информация](#особенная-информация)).
- Ожидать, что `ctx.Run()` вернётся сам — змейка работает в `while (1)`, выход только через `Q`/`Escape` (которые вызывают `ctx.Stop()`).
- Запускать змейку в CI/headless — `Console.KeyAvailable`/`Console.ReadKey` бросят `InvalidOperationException` при перенаправленном вводе.

## Особенная информация

### Змейка и баг кодогенератора

`LibCode.snake_minic` держит **всё состояние в глобалах** (`sx`, `sy`, `slen`, `dir`, `fx`, `fy`, `rngState`, `newX`, `newY`, `ti`, `te`, `tg`, `th`, `tk`). Это не стилистика — это обход бага `FunctionGenerator.GenerateFunction` (`[Nantuc.Compiler](../Nantuc.Compiler/README.md)`, [Что исправить, п.1](../Nantuc.Compiler/README.md#что-исправить)): несимметричный push/pop регистров калечит callee-saved регистры при 3–4 локальных переменных. Пока баг не исправлен в компиляторе, любая функция с несколькими локалами может «портить» регистры вызывающего. `snake_minic` избегает этого, держа состояние в data-секции.

Это же объясняет, почему `rng()`, `occupied()`, `spawnFood()` и т.д. используют глобальные `ti`, `te`, `newX`, `newY` вместо локальных — каждая из них могла бы быть «чистой» функцией с локалами, но тогда баг кодогенератора проявился бы.

### Ввод: loopback в собственное IO-окно `Device`

`SnakeLauncher` **не** регистрирует `KeyboardDevice` на отдельном sector. Вместо этого:

- `portBus.WritePort(0, code)` — пишет в `Device._ioMemory[0]` (VM сама зарегистрирована на `sector 0`, `Device` реализует `IPortController`, `ResolveAddress(0)` → `_devices[0]` = `Device`, `Device.WritePort(0, code)`).
- `input_nanasm` делает `IN r0, r1` с `r1 = 0x00` → `portBus.ReadPort(0)` → `Device.ReadPort(0)` → возвращает `_ioMemory[0]`.

То есть **VM — сама себе клавиатура через собственное IO-окно**. Это работает, потому что `Device` зарегистрирован и в `ManagerDevices` (как VM), и в `PortBus._devices` (как `IPortController`). Но:

- `KeyboardDevice` остаётся мёртвым кодом.
- `portBus.WritePort(0, code)` возвращает `BiosStatus` (`NullDeviceOutput` если сектор 0 не зарегистрирован) — `SnakeLauncher` игнорирует результат. Если VM не зарегистрирована на sector 0 (например, ошибка `RegisterDevice`), ввод молча теряется.
- `IN` в `input_nanasm` читает и сразу сбрасывает (`OUT r0, r1` с `r0=0`). Если два нажатия придут между чтениями VM — второе затрёт первое.

### Тороид 32×16 и порты монитора

`snake_minic` использует wrap по краям (`newX < 0 → 31`, `newX > 31 → 0`, `newY < 0 → 15`, `newY > 15 → 0`) — змейка телепортируется на противоположный край. Это не баг, а фича (классический Snake).

Порты монитора в `monitor_nanasm`: `0x10` (CURSOR_X), `0x11` (CURSOR_Y), `0x12` (PIXEL), `0x13` (COMMAND). При дефолтном `DevicePortSize.B16` (`PortsOnDevice = 16 = 0x10`) `sector 1` начинается с `1 * 16 = 0x10`. Если поменять `portsPerDevice` в `CreateDefault` (например, на `B32`), база `sector 1` станет `0x20`, и все `OUT r0, 0x10` попадут в **sector 0** (VM саму себя), а не в монитор. Это хрупкое место, привязанное к дефолтному `DevicePortSize`. См. [Что исправить, п.8](#что-исправить).

### `queuedIOStream` в `Program.cs` — не используется

`Program.cs` создаёт `QueuedIOStream(Console.Out, PortCharEncoding.Utf8)` и регистрирует на `sector 1`, но `source1` (allocator test) **не пишет** в порты — только `alloc`/`free`/`Store64`/`Load64`. I/O-устройство просто висит. Это не ошибка (не мешает), но лишнее для теста аллокатора.

### `Program.cs` — `hpPrt` и `GetRamAsNewArray`

`hpPrt = regs[RegType.rHP]` после `Run()`. В `alloc_minic` `rHP` — bump pointer, растёт при `alloc`. После теста `hpPrt` указывает за последний аллоцированный блок. `GetRamAsNewArray(0, hpPrt)` дампит `RAM[0..hpPrt)` — это код + data + heap. `StringDisassembler.Disassemble(ramSnap, resCompile.CodeSize)` разбирает `[0, CodeSize)` как код, `[CodeSize, hpPrt)` как данные. В data попадут **и** статические данные, **и** heap — визуально неотличимы. Для отладки аллокатора это ок, но для понимания «что где» — misleading.

### ANSI и `Console`

`SnakeLauncher.RenderToConsole` использует `\x1b[H` (home) и `\x1b[0m` (reset). `Console.CursorVisible = false` — работает не во всех терминалах (в некоторых бросает `IOException`, но `SnakeLauncher` не ловит). `Console.KeyAvailable`/`Console.ReadKey` — в headless/redirected могут бросить. `SnakeLauncher` не проверяет `Console.IsInputRedirected`/`Console.IsOutputRedirected`.

### `KeyboardDevice` — мёртвый код

`KeyboardDevice.Press(byte key)` публичный, но нигде не вызывается. `SnakeLauncher` пишет напрямую в `portBus.WritePort(0, code)`. Класс оставлен как задел на будущее (или как альтернатива loopback-подходу). Если будете регистрировать его — не забудьте, что `ReadPort(REG_KEY)` возвращает `_key` без сброса, а `ReadPort(REG_HAS_NEW)` — consume-on-read. В `input_nanasm` сброс делается вручную (`OUT 0, 0`), что с `KeyboardDevice` было бы избыточно.

### Ограничения платформ

- **`net10.0`**, без `AllowUnsafeBlocks` — модуль не трогает нативную память.
- **`Console.KeyAvailable`/`Console.ReadKey`** — только в интерактивном терминале. В WASM/CI/headless — `InvalidOperationException`.
- **`Console.CursorVisible`/`Console.SetCursorPosition`** — могут бросить в перенаправленном выводе.
- **ANSI (`\x1b[...`)** — Windows Terminal, Linux, macOS — работают. Старый `cmd.exe` (без VT processing) — не отобразит.
- **`Timer` + фоновый поток** — в WASM `Timer` работает, но `Thread` с `Console.ReadKey` не имеет смысла. Для WASM-порта змейки нужен другой ввод (например, `KeyboardDevice` поверх `input`-событий браузера).

## Что исправить

По убыванию серьёзности. Ссылки на файлы и строки.

1. **`SnakeLauncher.LaunchSnake` — `int deviceId = host.Emulator.RegisterDevice(...)` без проверки на отрицательный ID.**  
   Файл: `SnakeGame/SnakeLauncher.cs`, строка `int deviceId = host.Emulator.RegisterDevice(handle, sector: 0);`.  
   Если сектор 0 занят (повторный запуск), `RegisterDevice` вернёт `-2`, `GetDeviceContext(-2)` вернёт `null`, `!` (строка `var ctx = host.Emulator.GetDeviceContext(deviceId)!;`) даст NRE.  
   Фикс: `if (deviceId < 0) { Console.Error.WriteLine($"RegisterDevice failed: {deviceId}"); return; }` до `GetDeviceContext`.

2. **`Program.cs` — `using Nantuc.Samples.Scratch.SnakeGame;` не используется.**  
   Файл: `Program.cs`, строка 5.  
   `SnakeGame` импортирован, но `Program.cs` не вызывает `SnakeLauncher`/`MonochromeMonitor`/`KeyboardDevice`. Это либо задел, либо забытый переключатель.  
   Фикс: либо убрать `using`, либо добавить в конец `Main` закомментированный вызов `// SnakeLauncher.LaunchSnake();`, либо вынести два эксперимента в отдельные методы `RunAllocatorTest()`/`RunSnake()` с выбором через аргумент командной строки.

3. **`Program.cs` — код возврата `r0` не проверяется.**  
   Файл: `Program.cs`, строки после `deviceCtx.Run()`:  
   ```csharp
   var regs = deviceCtx.GetRegistersSnapshot();
   Console.WriteLine($"{RegType.r0.RegName()}: {regs[RegType.r0]}");
   ```
   `source1` возвращает `int` (`return s; // 10`), но `Run()` возвращает `void`. Фактический результат — в `r0`, но `Program.cs` его только печатает, не сверяет. Тест «проходит» даже при `r0 != 10`.  
   Фикс: `if (regs[RegType.r0] != 10) { Console.Error.WriteLine($"FAILED: r0={regs[RegType.r0]} (expected 10)"); return; }` или использовать `RunWithLimit` + `LaunchOptions.OnEnd` с отдельным `IProcessorFaultPolicy`, которая считывает `r0`.

4. **`SnakeLauncher` — `portBus.WritePort(0, code)` без проверки результата.**  
   Файл: `SnakeGame/SnakeLauncher.cs`, строка `portBus.WritePort(0, code);` в `inputThread`.  
   `IPortBus.WritePort` возвращает `BiosStatus`. Если `sector 0` не зарегистрирован (см. п.1), вернётся `NullDeviceOutput`, ввод молча потеряется.  
   Фикс: `var st = portBus.WritePort(0, code); if (st != BiosStatus.Success) { /* лог или running = false */ }`.

5. **`SnakeLauncher` — `Timer(..., null, 200, period)` бросает при `period <= 0`.**  
   Файл: `SnakeGame/SnakeLauncher.cs`, строка `using var ticker = new Timer(_ => { ... }, null, 200, period);`.  
   `Timer` с `period <= 0` бросает `ArgumentOutOfRangeException`. Также если `period == Timeout.Infinite`, таймер сработает один раз.  
   Фикс: `if (period <= 0) throw new ArgumentOutOfRangeException(nameof(period), "period must be > 0");` в начале `LaunchSnake`, или `period = Math.Max(10, period)`.

6. **`SnakeLauncher` — `KeyboardDevice` не регистрируется, ввод идёт через loopback.**  
   Файл: `SnakeGame/SnakeLauncher.cs` + `SnakeGame/KeyboardDevice.cs`.  
   `KeyboardDevice` реализован, но в `LaunchSnake` регистрируется только `monitor`. Ввод — через `portBus.WritePort(0, code)` (loopback в `Device._ioMemory[0]`). Работает, но `KeyboardDevice` — мёртвый код, а loopback неочевиден для читателя.  
   Фикс: либо зарегистрировать `KeyboardDevice` на `sector 2` и поменять `input_nanasm` на чтение из `0x20` (база `sector 2` при `B16`), либо задокументировать в комментарии, что `sector 0` — это loopback, `KeyboardDevice` — альтернатива для будущего.

7. **`SnakeLauncher` — `catch (Exception ex) when (running)` не ловит исключения после `running = false`.**  
   Файл: `SnakeGame/SnakeLauncher.cs`, строка `catch (Exception ex) when (running) { Console.WriteLine($"\nVM error: {ex.Message}"); }`.  
   Если `ctx.Run()` бросит после того, как `inputThread` установил `running = false` (гонка), исключение не будет поймано → процесс упадёт.  
   Фикс: убрать `when (running)` или сделать `catch (Exception ex) { if (running) Console.WriteLine(...); }`.

8. **`SnakeLauncher` — порты монитора (`0x10..0x13`) жёстко зашиты под `DevicePortSize.B16`.**  
   Файл: `SnakeGame/LibCode.cs`, `monitor_nanasm` (`LDI r4, 0x10; OUT ...; LDI r4, 0x11; ...`) + `SnakeGame/SnakeLauncher.cs` (`VmHostFactory.CreateDefault()` без параметров).  
   База `sector 1` = `1 * PortsOnDevice`. При дефолтном `B16` = `0x10` — совпадает. При смене `portsPerDevice` — сломается.  
   Фикс: либо задокументировать в комментарии `SnakeLauncher` («требует DevicePortSize.B16, sector 1 → base 0x10»), либо передавать `portsPerDevice: DevicePortSize.B16` явно в `CreateDefault`, либо вычислять базу в C# и передавать в `snake_minic` через глобал.

9. **`MonochromeMonitor` — `_renderer(_fb, Width, Height)` передаёт живой буфер.**  
   Файл: `SnakeGame/MonochromeMonitor.cs`, строка `_renderer(_fb, Width, Height);` в `WritePort(REG_COMMAND, CMD_FLUSH)`.  
   Если рендерер асинхронный или долгий, а VM продолжает писать в `_fb` (через `REG_PIXEL`), будет data race. В `SnakeLauncher` рендерер синхронный (`Console.Write`), но контракт не защищает.  
   Фикс: либо задокументировать `/// <summary>Renderer must not store the buffer reference; copy if needed</summary>`, либо передавать копию (`(byte[])_fb.Clone()`), либо ввести двойную буферизацию (фронт/бэк).

10. **`MonochromeMonitor` — `ReadPort(REG_PIXEL)` может бросить `IndexOutOfRangeException`.**  
    Файл: `SnakeGame/MonochromeMonitor.cs`, `REG_PIXEL => _fb[_cy * Width + _cx]`.  
    `_cx`/`_cy` клампятся при записи, но если `Width`/`Height` изменятся (не могут — `get;` без `set`), или если `_fb` инициализирован с другими размерами — OOB. Сейчас безопасно, но хрупко.  
    Фикс: `if ((uint)_cx >= (uint)Width || (uint)_cy >= (uint)Height) return 0;` перед доступом.

11. **`MonochromeMonitor` — `_frame` не сбрасывается в `CMD_CLEAR`.**  
    Файл: `SnakeGame/MonochromeMonitor.cs`, `CMD_CLEAR` вызывает `Array.Clear(_fb); _cx = _cy = 0;`, но не `_frame = 0`.  
    Это ок (frame — глобальный счётчик), но читатель может подумать, что clear сбрасывает всё.  
    Фикс: либо задокументировать `_frame` как «lifetime counter, not reset by CMD_CLEAR», либо сбрасывать (если так задумано).

12. **`LibCode.input_nanasm` — гонка при двух нажатиях между чтениями VM.**  
    Файл: `SnakeGame/LibCode.cs`, `input_nanasm`: `IN r0, r1; ... OUT r0, r1;` (чтение + сброс в одном вызове).  
    Если пользователь нажмёт две стрелки быстрее, чем VM прочитает (200 мс при `period: 200`), второе нажатие затрёт первое в `_ioMemory[0]`. Игра «проглотит» первое.  
    Фикс: буферизировать в `KeyboardDevice` (очередь), а `input_nanasm` читать из `REG_HAS_NEW`/`REG_KEY` (два порта). Или уменьшить `period` (быстрее тик — меньше окно гонки, но и игра быстрее).

13. **`Program.cs` — `QueuedIOStream` создаётся и регистрируется, но не используется.**  
    Файл: `Program.cs`, строки `var iostream = new QueuedIOStream(...)` + `host.Emulator.RegisterDevice(ioDevice: iostream, ...)`.  
    `source1` (allocator test) не пишет в порты. Устройство висит, `Dispose` вызовется через `host.Dispose()`, но лишнее.  
    Фикс: убрать `iostream` и `RegisterDevice(ioDevice: ...)` из `Program.cs` (или использовать — например, `print8` результат `r0`).

14. **`Program.cs` — `using var host` без `Console.OutputEncoding`.**  
    Файл: `Program.cs`, начало. `LoggingFaultPolicyMinimal` пишет `[FAULT] ...` (ASCII), но если расширите вывод на русский — кракозябры на Windows CP866/1251.  
    Фикс (профилактически): `Console.OutputEncoding = System.Text.Encoding.UTF8;` первой строкой.

15. **`SnakeLauncher` — `Console.CursorVisible`/`SetCursorPosition` без try/catch.**  
    Файл: `SnakeGame/SnakeLauncher.cs`, строки `Console.CursorVisible = false;`, `Console.CursorVisible = true;`, `Console.SetCursorPosition(0, 18);`.  
    В перенаправленном выводе/headless могут бросить `IOException`. `finally` тогда не завершится.  
    Фикс: `try { Console.CursorVisible = false; } catch (IOException) { }` (и аналогично для остальных).

16. **`SnakeLauncher.RenderToConsole` — статический `StringBuilder sb` не потокобезопасен.**  
    Файл: `SnakeGame/SnakeLauncher.cs`, `private static readonly StringBuilder sb = new();`.  
    Рендер вызывается из VM-потока (`ctx.Run`), но если добавите второй монитор или отрисовку из другого потока — гонка.  
    Фикс: сделать `sb` локальным (`var sb = new StringBuilder(...)` в начале метода) — аллокация на кадр незначительна, зато безопасно.