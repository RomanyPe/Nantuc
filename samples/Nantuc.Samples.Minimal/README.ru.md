# Nantuc.Samples.Minimal — README

> Минимальный рабочий пример платформы Nantuc: компилирует MiniC-программу «Hello world. Привет мир», запускает её на виртуальной машине и выводит UTF-16 строку в `Console.Out` через порт-устройство.
> Показывает полный путь «исходник → байткод → VM → I/O» на одном экране.
> Зависит от `[Nantuc.VmApplication.Extensions](../../src/Nantuc.VmApplication.Extensions/README.ru.md)` (транзитивно — `VmApplication`, `Compiler`, `Kernel`).
> `[← Корень](../README.ru.md) · [VmApplication](../../src/Nantuc.VmApplication/README.ru.md) · [Extensions](../../src/Nantuc.VmApplication.Extensions/README.ru.md) · [Compiler](../../src/Nantuc.Compiler/README.ru.md) · [Kernel](../../src/Nantuc.Kernel/README.ru.md)`

## Содержание
- [Об этом модуле](#об-этом-модуле)
- [Дерево локального проекта](#дерево-локального-проекта)
- [API](#api)
- [Примеры для API](#примеры-для-api)
- [Приближенность модуля к пользователю](#приближенность-модуля-к-пользователю)
- [Особенная информация](#особенная-информация)
- [Что исправить](#что-исправить)

## Об этом модуле

`Nantuc.Samples.Minimal` — это **один файл `Program.cs` с top-level statements** (без `class Program`, без namespace). Проект — `Exe`, `net10.0`, зависит только от `VmApplication.Extensions`. Это «hello world» для платформы: если вы никогда не видели Nantuc, начните отсюда.

Что демонстрирует пример:

1. **`VmHostFactory.CreateDefault()`** — создание `VmHost` (проект + эмулятор) одной строкой.
2. **`SaveFileProject` / `SaveFileInclude`** — запись исходника и include-файлов в `RamFileService` (in-memory, без реальной ФС). Используются `Libraries.print_minic` и `Libraries.io_nanasm` из `VmApplication.Extensions`.
3. **`DeviceSpec.Create(RamSize.MB128, name: "ConsoleVM")`** — VM с 128 MiB RAM и именем `"ConsoleVM"`.
4. **`QueuedIOStream(Console.Out, PortCharEncoding.Utf16)`** — асинхронное устройство вывода, UTF-16LE, регистрируется на `sector 1`.
5. **`TryRegisterDevice`** — регистрация VM на `sector 0` и I/O на `sector 1`. Возвращает `IdState` вместо `int < 0`.
6. **`TryGetDeviceContext`** — получение `DeviceContext` без `!`.
7. **`CompileToIR(baseAddress: 0, optimize: true).Build()`** — компиляция всех файлов из `IFileService` в байткод.
8. **`TryLoadProgram`** — загрузка байткода в RAM VM.
9. **`Run()`** — блокирующий запуск до `END`/fault'а. Возвращает `ProcessorFault` — статус проверяется напрямую, без дополнительных вызовов.

Исходник `main.minic` (MiniC):

```c
#include "print.minic"
char str[] = "Hello world. Привет мир";
void main() { print16(str); }
```

Строка содержит и ASCII, и кириллицу — это проверяет, что `print16` и UTF-16LE-путь в `QueuedIOStream` корректно работают с не-ASCII символами. `print.minic` (из `Libraries.print_minic`) подключает `io.nanasm` и определяет `print16`/`write16`/`flush`, которые пишут в порты `0x10` (Data) и `0x12` (Flush). `QueuedIOStream` с `Utf16` собирает пары байт в `char` и пишет в `TextWriter`.

## Дерево локального проекта

Один файл:

```
Nantuc.Samples.Minimal/
├── Nantuc.Samples.Minimal.csproj   # net10.0, Exe, ref → VmApplication.Extensions
└── Program.cs                       # top-level statements, ~55 строк
```

Никаких классов, namespace'ов, helper-методов — весь пример в `Program.cs`.

## API

Публичного API нет — это `Exe`. Ниже — поток выполнения, чтобы можно было читать код.

### Поток выполнения `Program.cs`

| Шаг | Код | Что делает |
|---|---|---|
| 1 | `using var host = VmHostFactory.CreateDefault();` | `VmHost` с `RamFileService` + дефолтный `PortBus` (`PortSize.KB64`, `DevicePortSize.B16`) |
| 2 | `host.Project.SaveFileProject("main.minic", source);` | Кладёт исходник в `RamFileService` под `ProjectPath/main.minic` |
| 3 | `host.Project.SaveFileInclude("io.nanasm", Libraries.io_nanasm);` | Кладёт asm-lib в `IncludePath/io.nanasm` |
| 4 | `host.Project.SaveFileInclude("print.minic", Libraries.print_minic);` | Кладёт C-lib в `IncludePath/print.minic` |
| 5 | `host.Emulator.TryRegisterDevice(DeviceSpec.Create(RamSize.MB128, name: "ConsoleVM"), sector: 0, out int vmId)` | Регистрирует VM на `sector 0`; `IdState.Valid` — успех, `vmId` — id |
| 6 | `host.Emulator.TryRegisterDevice(new QueuedIOStream(Console.Out, PortCharEncoding.Utf16), name: "console", sector: 1, out _)` | Регистрирует I/O на `sector 1`; у порт-устройства имя передаётся параметром, у VM — внутри `DeviceSpec` |
| 7 | `host.Emulator.TryGetDeviceContext(vmId, out var vm)` | `DeviceContext` для VM без `!` и без риска NRE |
| 8 | `host.Project.CompileToIR(baseAddress: 0, optimize: true).Build()` | Компилирует всё из `IFileService` в `CompilationResult` |
| 9 | `vm.TryLoadProgram(compilation.Program!, loadAddress: 0, out var error)` | Загружает байткод в RAM VM; `false` + `out error` — неудача |
| 10 | `ProcessorFault fault = vm.Run();` | Запускает до `END`/fault'а, возвращает `ProcessorFault` |
| 11 | `if (fault.Status != BiosStatus.EndProgram) ...` | Проверка успешного завершения |

`CompileToIR(ulong, bool)` — extension из `[Nantuc.VmApplication.Extensions](../../src/Nantuc.VmApplication.Extensions/README.ru.md)` (`ProjectExtension`). Внутри вызывает `ProjectBuilder.GetSourceFiles(proj.FileService, proj.Paths)` и `ProjectBuilder.Build`.

`TryRegisterDevice`/`TryGetDeviceContext` — новый API эмулятора, возвращает `IdState`/`bool` вместо «магических» отрицательных id и `null`. См. раздел «`IdState` vs `int` / `null`» ниже.

## Примеры для API

Этот проект **и есть** пример. Варианты использования:

**Заменить `QueuedIOStream` на синхронный (проще отлаживать):**

```csharp
var iostream = new SynchronousIOStream(Console.Out, PortCharEncoding.Utf16);
```

**Заменить UTF-16 на UTF-8 (тогда `print8` в `print.minic`):**

```csharp
var iostream = new QueuedIOStream(Console.Out, PortCharEncoding.Utf8);
```

и в `main.minic` вызвать `print8(str)` вместо `print16(str)`. Но `char` в MiniC — 16-bit, `print8` ожидает `char*` и делает `write8(str[i])` — truncation 16→8. Для ASCII (`"Hello world."`) работает, для кириллицы (`"Привет мир"`) — нет: символы U+041F…U+0440 не влезают в байт. Для не-ASCII оставляйте `Utf16`.

**Добавить диагностику завершения:**

```csharp
ProcessorFault fault = vm.Run(new LaunchOptions(
    StartAddress: 0,
    ShowTimer: true,
    OnEnd: r => Console.WriteLine($"steps={r?.Steps} elapsed={r?.Elapsed}")));
```

**Добавить `LoggingFaultPolicyMinimal` (по желанию):**

```csharp
using Nantuc.VmApplication.Extensions.Host;

var lfPolicy = new LoggingFaultPolicyMinimal(Console.Out);
var vmSpec = DeviceSpec.Create(RamSize.MB128, name: "ConsoleVM", faultPolicy: lfPolicy);
```

Политика логирует `ProcessorFault` в `Console.Out` и останавливает симуляцию. В текущем примере **не используется** — `Run()` и так возвращает `ProcessorFault`, который мы проверяем вручную.

**Заменить `RamFileService` на реальную ФС:**

```csharp
var fs = new DefaultFileService();
fs.SaveFile(Path.Combine(AppContext.BaseDirectory, "project", "main.minic"), source);
fs.SaveFile(Path.Combine(AppContext.BaseDirectory, "include", "io.nanasm"), Libraries.io_nanasm);
fs.SaveFile(Path.Combine(AppContext.BaseDirectory, "include", "print.minic"), Libraries.print_minic);

using var host = VmHostFactory.CreateDefault(fileService: fs);
```

## Приближенность модуля к пользователю

**Максимальная.** Это учебный пример — «скопируй, запусти, получи Hello world + Привет мир». Предназначен для:

- Нового пользователя платформы, который хочет увидеть Nantuc в действии.
- Автора расширений, которому нужен минимальный каркас для собственного устройства или оптимизации.
- CI-сборки, которая проверяет, что публичный API не сломан (если добавите тест, который запускает `dotnet run` и сверяет stdout с `"Hello world. Привет мир"`).

**Что стоит делать:**
- Запустить `dotnet run` в `Nantuc.Samples.Minimal` — на экран выведется `Hello world. Привет мир` (UTF-16).
- Использовать как шаблон для своих примеров: копировать структуру `SaveFileProject` → `TryRegisterDevice` → `TryGetDeviceContext` → `CompileToIR` → `TryLoadProgram` → `Run`.
- Проверять `IdState` от `TryRegisterDevice` — это заменяет старую проверку `deviceId < 0`.
- Для отладки заменить `Run()` на `RunStepMode` + `SingleStep`, как в `[VmApplication README](../../src/Nantuc.VmApplication/README.ru.md)`.

**Что не стоит делать:**
- Ожидать, что `optimize: true` даст ускорение — без `AddStdRules()` AST-оптимизатор пуст (см. [Что исправить, п.2](#что-исправить)).
- Менять `portsPerDevice` в `VmHostFactory.CreateDefault(...)` без пересчёта `portWrite`/`portFlush` в `print.minic` (см. [Особенная информация](#особенная-информация)).
- Использовать `RamFileService.Combine` в своих путях — он использует `\\` на всех ОС, что ломает `DefaultFileService` при смешивании.
- Полагаться на `Console.Out` для кириллицы на Windows без явной настройки `Console.OutputEncoding` (см. [Что исправить, п.3](#что-исправить)).

## Особенная информация

### `IdState` vs `int` / `null`

Раньше регистрация устройства возвращала `int`: `>= 0` — id, `-2` — ошибка; а `GetDeviceContext(id)!` падал с NRE, если id невалиден. Сейчас:

- `TryRegisterDevice(...)` возвращает `IdState` (`Valid` / `Invalid` / …); `out int id` заполняется всегда.
- `TryGetDeviceContext(id, out vm)` возвращает `bool`.
- `TryLoadProgram(...)` возвращает `bool` с `out string error`.

Это позволяет не полагаться на «магические» отрицательные id и не писать `!` для `GetDeviceContext`.

### Порты: откуда `0x10` и `0x12`

`print.minic` (из `Libraries.print_minic`) содержит:

```c
int portWrite = 0x10;
int portFlush = 0x12;
```

Это **абсолютные адреса** в порт-пространстве VM, а не смещения. `PortBus` (`[Nantuc.Kernel](../../src/Nantuc.Kernel/README.ru.md)`) маршрутизирует `address >> deviceShift` в сектор, `address & offsetMask` в смещение внутри устройства. При дефолтных `PortSize.KB64` и `DevicePortSize.B16`:

- `PortsOnDevice = 1 << 4 = 16` (байт).
- `SectorCount = 1 << (16 - 4) = 4096`.
- Сектор 1 → база `1 * 16 = 16 = 0x10`.
- `Data` (offset 0) → `0x10`, `Flush` (offset 2) → `0x12`.

Если поменять `portsPerDevice` в `VmHostFactory.CreateDefault` (например, на `DevicePortSize.B32` = 32), база сектора 1 станет `0x20`, а `portWrite = 0x10` попадёт в **сектор 0** (VM самого себя), и вывод пропадёт или сломается. Это ограничение примера, не платформы.

Адреса вынесены в глобальные переменные `portWrite`/`portFlush`, чтобы их можно было менять под свою топологию секторов.

### UTF-16 и `QueuedIOStream`

`QueuedIOStream` с `PortCharEncoding.Utf16` собирает пары байт в `char`:

```csharp
if (pendingLowByte is null) { pendingLowByte = b; /* buffered */ continue; }
char c = (char)(pendingLowByte.Value | (b << 8));
```

`print16` (в `print.minic`) пишет low byte первым (`write8(c)`), high — вторым (`write8(c >> 8)`). Порядок совпадает с UTF-16LE. `flush()` в конце пишет `1` в `Flush`-порт → `QueuedIOStream.Flush()` ждёт до 2 сек, пока `_pendingCount` не станет 0, и делает `_writer.Flush()`.

Строка `"Hello world. Привет мир"` проверяет, что:
- ASCII (`Hello world.`) идёт как есть (U+0048…U+0077, 2 байта в UTF-16).
- Кириллица (`Привет мир`, U+041F…U+0440) тоже 2 байта в UTF-16, но вне ASCII-диапазона — `QueuedIOStream` пишет `char` напрямую в `TextWriter`, декодирование зависит от кодировки `Console.Out`.

### `void main()` vs `int main()`

`main.minic` объявляет `void main()`. Компилятор MiniC не проверяет тип возврата `main` (`FunctionGenerator.GenerateFunction` смотрит только `func.Name == "main"`), а в эпилоге `main` всегда `EmitEnd()`. Так что `void main` и `int main` дают одинаковый байткод. Для стиля C лучше `int main()`, но для примера `void main()` короче.

### `optimize: true` — no-op

`CompileToIR(0, optimize: true)` включает только AST-оптимизации. Но `VmHostProject.Optimizator.VMAstOptimizer` создаётся пустым — если не вызвать `AddStdRules()`, ни одно правило не применится. В этом примере `optimize: true` — «флаг на будущее». См. [Что исправить, п.2](#что-исправить).

### `Run()` и `QueuedIOStream`

`vm.Run()` блокирует поток до `END`/fault'а. `print16` в конце вызывает `flush()` → `QueuedIOStream.Flush()` синхронно ждёт пустой очереди. Так что к моменту возврата из `Run()` всё выведено в `Console.Out`. `host.Dispose()` (через `using`) затем вызывает `ManagerDevices.Dispose` → `iostream.Dispose` → `_drainTask.Wait(5s)` (на случай, если что-то осталось). Порядок безопасен.

### Ограничения платформ

- **`net10.0`**, без `AllowUnsafeBlocks` — пример не трогает нативную память напрямую.
- **`QueuedIOStream`** использует `Task.Run` + `Channel<byte>` — в WASM работает, но фоновый drain может не завершиться до конца синхронного `Main`. Для WASM-примера используйте `SynchronousIOStream`.
- **Кириллица в `Console.Out`** — на Windows без `Console.OutputEncoding = Encoding.UTF8` строка `"Привет мир"` может отображаться как `????`. См. [Что исправить, п.3](#что-исправить).
- **`RamFileService`** не зависит от ОС (in-memory), но `Combine` с `\\` — не «нативный» путь. Для реальной ФС — `DefaultFileService` + `Path.Combine`.

## Что исправить

1. **`QueuedIOStream` в WASM — фоновый drain.**  
   Файл: `Nantuc.VmApplication.Extensions/Emulator/IO/QueuedIOStream.cs`. В WASM `Task.Run` может не выполниться до конца синхронного `Main`, потому что нет event loop для фоновой задачи.  
   Фикс: для WASM-примеров использовать `SynchronousIOStream`, либо задокументировать в `README`/`STDLIBRARIES`, что `QueuedIOStream` требует явного `await`/`Dispose`.

2. **`optimize: true` без `AddStdRules()` — no-op.**  
   Файл: `Program.cs`. `CompileToIR(baseAddress: 0, optimize: true)` включает AST-оптимизации, но `VMAstOptimizer` пуст.  
   Фикс: либо вызвать `host.Project.Optimizator.VMAstOptimizer.AddStdRules()` перед компиляцией, либо передать `optimize: false`, чтобы не вводить читателя в заблуждение.

3. **Кириллица в `Console.Out`.**  
   Файл: `Program.cs`. На Windows без явной настройки `Console.OutputEncoding` строка `"Привет мир"` может отобразиться как `????`.  
   Фикс: в начале `Program.cs` добавить `Console.OutputEncoding = System.Text.Encoding.UTF8;` (до первого вывода), либо задокументировать в README/platform-notes.

4. **`void main()` в примере.**  
   Файл: `Program.cs`. Работает, но для стиля C лучше `int main()`.  
   Фикс: заменить на `int main() { print16(str); return 0; }` — читателю привычнее.

5. **`LibraresIncludes` → `StdLibraries` — синхронизация имён.**  
   Файл: `Nantuc.VmApplication.Extensions/LibraresIncludes/LibraresForCompiler.cs`. В примере уже используется `using Nantuc.VmApplication.Extensions.StdLibraries;` и `Libraries.io_nanasm`. Нужно убедиться, что в самом `VmApplication.Extensions` namespace/папка/класс переименованы в `StdLibraries`/`Libraries` (или, наоборот, в примере остался старый путь). Опечатки `Librares`/`Librare` — убрать одним BREAKING-коммитом.
```
