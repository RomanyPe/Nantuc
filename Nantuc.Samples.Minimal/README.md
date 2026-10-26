# Nantuc.Samples.Minimal — README

> Минимальный рабочий пример платформы Nantuc: компилирует MiniC-программу «Hello world», запускает её на виртуальной машине и выводит UTF-16 строку в `Console.Out` через порт-устройство.  
> Показывает полный путь «исходник → байткод → VM → I/O» на одном экране.  
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

`Nantuc.Samples.Minimal` — это **один файл `Program.cs` с top-level statements** (без `class Program`, без namespace). Проект — `Exe`, `net10.0`, зависит только от `VmApplication.Extensions`. Это «hello world» для платформы: если вы никогда не видели Nantuc, начните отсюда.

Что демонстрирует пример:

1. **`VmHostFactory.CreateDefault()`** — создание `VmHost` (проект + эмулятор) одной строкой.
2. **`LoggingFaultPolicyMinimal`** — кастомная политика fault'ов, которая логирует `ProcessorFault` в `Console.Out` и останавливает симуляцию.
3. **`SaveFileProject` / `SaveFileInclude`** — запись исходника и include-файлов в `RamFileService` (in-memory, без реальной ФС). Используется `Librare.print_minic` и `Librare.io_nanasm` из `VmApplication.Extensions`.
4. **`DeviceSpec.Create`** — VM с 128 MiB RAM, именем `"ConsoleVM"`, кастомной fault-политикой.
5. **`QueuedIOStream(Console.Out, PortCharEncoding.Utf16)`** — асинхронное устройство вывода, UTF-16LE, регистрируется на `sector 1`.
6. **`RegisterDevice`** — два вызова: VM на `sector 0`, I/O на `sector 1`.
7. **`CompileToIL(0, optimize: true).Build()`** — компиляция всех файлов из `IFileService` в байткод.
8. **`TryFastLoadProgram`** — быстрая загрузка (`NativeMemory.Copy`) в RAM VM.
9. **`Run()`** — блокирующий запуск до `END`/fault'а.

Исходник `source1` (MiniC):

```c
#include "print.minic"

char str[] = "Hello world";

void main() {
    print16(str);
}
```

`print.minic` (из `Librare.print_minic`) подключает `io.nanasm` и определяет `print16`/`write16`/`flush`, которые пишут в порты `0x10` (Data) и `0x12` (Flush). `QueuedIOStream` с `Utf16` собирает пары байт в `char` и пишет в `TextWriter`.

## Дерево локального проекта

Один файл:

```
Nantuc.Samples.Minimal/
├── Nantuc.Samples.Minimal.csproj   # net10.0, Exe, ref → VmApplication.Extensions
└── Program.cs                       # top-level statements, ~60 строк
```

Никаких классов, namespace'ов, helper-методов — весь пример в `Program.cs`.

## API

Публичного API нет — это `Exe`. Ниже — поток выполнения, чтобы можно было читать код.

### Поток выполнения `Program.cs`

| Шаг | Код | Что делает |
|---|---|---|
| 1 | `using var host = VmHostFactory.CreateDefault();` | `VmHost` с `RamFileService` + дефолтный `PortBus` (`PortSize.KB64`, `DevicePortSize.B16`) |
| 2 | `var lfPolicy = new LoggingFaultPolicyMinimal(Console.Out);` | Пишет `[FAULT] ...` в stdout, `ShouldContinue` возвращает `false` |
| 3 | `host.Project.SaveFileProject("main.minic", source1);` | Кладёт `source1` в `RamFileService` под `ProjectPath/main.minic` |
| 4 | `host.Project.SaveFileInclude("io.nanasm", Librare.io_nanasm);` | Кладёт asm-lib в `IncludePath/io.nanasm` |
| 5 | `host.Project.SaveFileInclude("print.minic", Librare.print_minic);` | Кладёт C-lib в `IncludePath/print.minic` |
| 6 | `DeviceSpec.Create(RamSize.MB128, lfPolicy, "ConsoleVM")` | Дескриптор VM |
| 7 | `new QueuedIOStream(Console.Out, PortCharEncoding.Utf16)` | Устройство вывода |
| 8 | `host.Emulator.RegisterDevice(handle, sector: 0)` | VM → `sector 0` |
| 9 | `host.Emulator.RegisterDevice(iostream, "console", sector: 1)` | I/O → `sector 1` |
| 10 | `host.Emulator.GetDeviceContext(deviceId)!` | `DeviceContext` для VM |
| 11 | `host.Project.CompileToIL(0, optimize: true).Build()` | Компилирует всё из `IFileService` в `CompilationResult` |
| 12 | `deviceCtx.TryFastLoadProgram(resCompile.Program, 0, out error)` | Загружает байткод в RAM VM |
| 13 | `deviceCtx.Run()` | Запускает до `END`/fault'а |

`CompileToIL(ulong, bool)` — extension из `[Nantuc.VmApplication.Extensions](../Nantuc.VmApplication.Extensions/README.md)` (`ProjectExtension`). Внутри вызывает `ProjectBuilder.GetSourceFiles(proj.FileService, proj.Paths)` и `ProjectBuilder.Build` с `Assembler` (прямой) или `IRAssembler` (через `CompileToIL`).

`SaveFileProject`/`SaveFileInclude` — тоже extension из `ProjectExtension`, пишут в `IFileService` по путям `Paths.ProjectPath` / `Paths.IncludePath`.

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

и в `source1` вызвать `print8(str)` вместо `print16(str)`. Но `char` в MiniC — 16-bit, `print8` ожидает `char*` и делает `write8(str[i])` — truncation 16→8. Для ASCII работает, для не-ASCII — нет.

**Добавить диагностику завершения:**

```csharp
deviceCtx.Run(new LaunchOptions(
    StartAddress: 0,
    ShowTimer: true,
    OnEnd: r => Console.WriteLine($"steps={r?.Steps} elapsed={r?.Elapsed}")));
```

**Заменить `RamFileService` на реальную ФС:**

```csharp
var fs = new DefaultFileService();
fs.SaveFile(Path.Combine(AppContext.BaseDirectory, "project", "main.minic"), source1);
fs.SaveFile(Path.Combine(AppContext.BaseDirectory, "include", "io.nanasm"), Librare.io_nanasm);
fs.SaveFile(Path.Combine(AppContext.BaseDirectory, "include", "print.minic"), Librare.print_minic);

using var host = VmHostFactory.CreateDefault(fileService: fs);
```

## Приближенность модуля к пользователю

**Максимальная.** Это учебный пример — «скопируй, запусти, получи Hello world». Предназначен для:

- Нового пользователя платформы, который хочет увидеть Nantuc в действии.
- Автора расширений, которому нужен минимальный каркас для собственного устройства или оптимизации.
- CI-сборки, которая проверяет, что публичный API не сломан (если добавите тест, который запускает `dotnet run` и сверяет stdout с `"Hello world"`).

**Что стоит делать:**
- Запустить `dotnet run` в `Nantuc.Samples.Minimal` — на экран выведется `Hello world` (UTF-16).
- Использовать как шаблон для своих примеров: копировать структуру `SaveFileProject` → `RegisterDevice` → `CompileToIL` → `TryFastLoadProgram` → `Run`.
- Для отладки заменить `Run()` на `RunStepMode` + `SingleStep`, как в `[VmApplication README](../Nantuc.VmApplication/README.md)`.

**Что не стоит делать:**
- Ожидать, что `optimize: true` даст ускорение — без `AddStdRules()` AST-оптимизатор пуст (см. [Что исправить, п.6](#что-исправить)).
- Менять `portsPerDevice` в `VmHostFactory.CreateDefault(...)` без пересчёта `portWrite`/`portFlush` в `print.minic` (см. [Особенная информация](#особенная-информация)).
- Использовать `RamFileService.Combine` в своих путях — он использует `\\` на всех ОС, что ломает `DefaultFileService` при смешивании.

## Особенная информация

### Порты: откуда `0x10` и `0x12`

`print.minic` (из `Librare`) содержит:

```c
int portWrite = 0x10;
int portFlush = 0x12;
```

Это **абсолютные адреса** в порт-пространстве VM, а не смещения. `PortBus` (`[Nantuc.Kernel](../Nantuc.Kernel/README.md)`) маршрутизирует `address >> deviceShift` в сектор, `address & offsetMask` в смещение внутри устройства. При дефолтных `PortSize.KB64` и `DevicePortSize.B16`:

- `PortsOnDevice = 1 << 4 = 16` (байт).
- `SectorCount = 1 << (16 - 4) = 4096`.
- Сектор 1 → база `1 * 16 = 16 = 0x10`.
- `Data` (offset 0) → `0x10`, `Flush` (offset 2) → `0x12`.

Если поменять `portsPerDevice` в `VmHostFactory.CreateDefault` (например, на `DevicePortSize.B32` = 32), база сектора 1 станет `0x20`, а `portWrite = 0x10` попадёт в **сектор 0** (VM самого себя), и вывод пропадёт или сломается. Это ограничение примера, не платформы.

### UTF-16 и `QueuedIOStream`

`QueuedIOStream` с `PortCharEncoding.Utf16` собирает пары байт в `char`:

```csharp
if (pendingLowByte is null) { pendingLowByte = b; /* buffered */ continue; }
char c = (char)(pendingLowByte.Value | (b << 8));
```

`print16` (в `print.minic`) пишет low byte первым (`write8(c)`), high — вторым (`write8(c >> 8)`). Порядок совпадает с UTF-16LE. `flush()` в конце пишет `1` в `Flush`-порт → `QueuedIOStream.Flush()` ждёт до 2 сек, пока `_pendingCount` не станет 0, и делает `_writer.Flush()`.

### `RamFileService` и `Combine`

`RamFileService.Combine(path1, path2)` реализован как `path1 + "\\" + path2` (жёсткий `\\`, а не `Path.Combine`). В этом примере это работает, потому что и запись (`SaveFileProject`/`SaveFileInclude`), и чтение (`GetNameFiles`/`ReadFile`/`ReadInclude`) идут через один и тот же `Combine` с одним и тем же `path1`. Если смешать `RamFileService` и `DefaultFileService` (например, сохранить через один, прочитать через другой), пути не совпадут. Это баг `[Nantuc.VmApplication](../Nantuc.VmApplication/README.md)` (см. п.1 там), не примера.

### `print.minic` — обход бага кодогенератора

`Librare.print_minic` держит `portWrite`/`portFlush` в **глобалах**, а не в локальных переменных, хотя они константы. Причина — тот же баг `FunctionGenerator.GenerateFunction` (`[Nantuc.Compiler](../Nantuc.Compiler/README.md)`, [Что исправить, п.1](../Nantuc.Compiler/README.md#что-исправить)): несимметричный push/pop регистров калечит callee-saved регистры при 3-4 локальных переменных. Пока баг не исправлен в компиляторе, пример работает только потому, что `print16`/`write16` держат мало переменных. Если будете расширять `print.minic` — держите состояние в глобалах.

### `void main()` vs `int main()`

`source1` объявляет `void main()`. Компилятор MiniC не проверяет тип возврата `main` (`FunctionGenerator.GenerateFunction` смотрит только `func.Name == "main"`), а в эпилоге `main` всегда `EmitEnd()`. Так что `void main` и `int main` дают одинаковый байткод. Для стиля C лучше `int main()`, но для примера `void main()` короче.

### `optimize: true` — no-op

`CompileToIL(0, optimize: true)` включает только AST-оптимизации. Но `VmHostProject.Optimizator.VMAstOptimizer` создаётся пустым — если не вызвать `AddStdRules()`, ни одно правило не применится. В этом примере `optimize: true` — просто «флаг на будущее». См. [Что исправить, п.6](#что-исправить).

### `Run()` и `QueuedIOStream`

`deviceCtx.Run()` блокирует поток до `END`/fault'а. `print16` в конце вызывает `flush()` → `QueuedIOStream.Flush()` синхронно ждёт пустой очереди. Так что к моменту возврата из `Run()` всё выведено в `Console.Out`. `host.Dispose()` (через `using`) затем вызывает `ManagerDevices.Dispose` → `iostream.Dispose` → `_drainTask.Wait(5s)` (на случай, если что-то осталось). Порядок безопасен.

### Ограничения платформ

- **`net10.0`**, без `AllowUnsafeBlocks` — пример не трогает нативную память напрямую.
- **`QueuedIOStream`** использует `Task.Run` + `Channel<byte>` — в WASM работает, но фоновый drain может не завершиться до конца синхронного `Main`. Для WASM-примера используйте `SynchronousIOStream`.
- **`LoggingFaultPolicyMinimal`** пишет в `Console.Out` — в headless/CI работает, но без `Console.OutputEncoding = UTF8` кириллица в сообщениях о fault'ах может отображаться некорректно.
- **`RamFileService`** не зависит от ОС (in-memory), но `Combine` с `\\` — не «нативный» путь. Для реальной ФС — `DefaultFileService` + `Path.Combine`.

## Что исправить

1. **`int deviceId = host.Emulator.RegisterDevice(...)` — нет проверки на отрицательный ID.**  
   `Program.cs`, строка `int deviceId = host.Emulator.RegisterDevice(handle: handle, sector: 0);`.  
   Если `RegisterDevice` вернёт `-1..-4` (нет места, сектор занят, `PortBus.RegisterDevice` fail), следующий `host.Emulator.GetDeviceContext(deviceId)!` вернёт `null`, `!` даст `NullReferenceException`.  
   Фикс: `if (deviceId < 0) { Console.Error.WriteLine($"RegisterDevice failed: {deviceId}"); return; }`.

2. **`host.Emulator.RegisterDevice(ioDevice: iostream, ...)` — нет проверки, `iostream` может протечь.**  
   `Program.cs`, строка `host.Emulator.RegisterDevice(ioDevice: iostream, name: "console", sector: 1);`.  
   `ManagerDevices.AddDevice` для `-1/-2/-3` возвращает ошибку **без** `device.Dispose()` (dispose только для `-4`). `QueuedIOStream` содержит `Task` + `Channel` + `CancellationTokenSource` — без `Dispose` фоновый таск утечёт и не остановится.  
   Фикс: `using var iostream = new QueuedIOStream(...)` (Dispose идемпотентен, `ManagerDevices` тоже вызовет — не страшно) **или** `int ioId = host.Emulator.RegisterDevice(iostream, ...); if (ioId < 0) { iostream.Dispose(); return; }`.

3. **`iostream` не `using var` — Dispose зависит от `host.Dispose()`.**  
   `Program.cs`, строка `var iostream = new QueuedIOStream(...)`.  
   В нормальном сценарии `host.Dispose()` (через `using var host`) вызовет `ManagerDevices.Dispose`, который вызовет `iostream.Dispose()`. Но если `RegisterDevice` для iostream не выполнится (см. п.2), `iostream` не будет в `_devices` — и не будет освобождён.  
   Фикс: `using var iostream = new QueuedIOStream(Console.Out, PortCharEncoding.Utf16);`. Порядок dispose: `iostream` (внутренний `using`) → `host` (внешний). `QueuedIOStream.Dispose` вызовется до `host.Dispose`, drain-таск завершится первым. Безопасно.

4. **`Run()` — нет `OnEnd`, нет диагностики.**  
   `Program.cs`, последняя строка `deviceCtx.Run();`.  
   `Run()` использует `LaunchOptions.Default` (`StartAddress = 0`, `ShowTimer = false`, `OnEnd = null`). Если VM упадёт с fault'ом, `LoggingFaultPolicyMinimal` напишет `[FAULT] ...` в `Console.Out` (это хорошо), но не будет видно `steps`/`elapsed`.  
   Фикс: `deviceCtx.Run(new LaunchOptions(ShowTimer: true, OnEnd: r => Console.WriteLine($"steps={r?.Steps} elapsed={r?.Elapsed}")));`.

5. **`portWrite = 0x10`, `portFlush = 0x12` — жёстко зашито, ломается при смене `portsPerDevice`.**  
   Это в `Librare.print_minic` (не в `Program.cs`, но пример использует его). База сектора 1 = `1 * PortsOnDevice`. При дефолтном `DevicePortSize.B16` = 16 = `0x10` — совпадает. Если поменять `portsPerDevice` в `CreateDefault` — сломается.  
   Фикс: либо задокументировать в `Program.cs` (комментарий «требует DevicePortSize.B16, sector 1 → base 0x10»), либо вычислять порты в `Program.cs` и передавать в `print.minic` через глобалы/`extern` (сложнее), либо использовать `OutputPortLayout.Data`/`Flush` из `[Nantuc.VmApplication.Extensions](../Nantuc.VmApplication.Extensions/README.md)` вместо магических чисел (но это C#, а `print.minic` — MiniC, так что нужен промежуточный слой).

6. **`CompileToIL(baseAddress: 0, optimize: true)` — `optimize: true` инертен.**  
   `Program.cs`, строка `var resCompile = host.Project.CompileToIL(baseAddress: 0, optimize: true).Build();`.  
   `VmAstOptimizer` пустой, `AddStdRules()` не вызывается. AST-оптимизации не применяются. Peephole тоже не применяется (`CompileToIL` без `OptimizationIL`).  
   Фикс: либо `host.Project.Optimizator.VMAstOptimizer.AddStdRules();` перед компиляцией, либо `optimize: false` с комментарием «оптимизации выключены для наглядности».

7. **`Console.OutputEncoding` не выставлен — кириллица в `LoggingFaultPolicyMinimal` может отображаться некорректно.**  
   `Program.cs`, начало файла. `LoggingFaultPolicyMinimal.ShouldContinue` пишет `"[FAULT] status=..., data=..., ip=..., op=..."` — латиница, но `LoggingFaultPolicyMinimal` определён в `ProjectExtension.cs` с русскими `Console.WriteLine` в других местах? Проверил — `LoggingFaultPolicyMinimal` пишет только `[FAULT] ...` (ASCII). Так что проблема только если вы добавите свои сообщения на русском.  
   Фикс (профилактически): `Console.OutputEncoding = System.Text.Encoding.UTF8;` первой строкой.

8. **`using var host = VmHostFactory.CreateDefault();` — без параметров.**  
   `Program.cs`, строка 1. Дефолты: `RamFileService`, `PortSize.KB64`, `DevicePortSize.B16`. Это ок для примера, но если читатель захочет реальную ФС — ему нужно знать про `fileService: new DefaultFileService()`.  
   Фикс: добавить комментарий `// RamFileService + PortSize.KB64 + DevicePortSize.B16` рядом с вызовом.

9. **`Librare.io_nanasm` сохраняется явно, но `print.minic` его тоже инклюдит.**  
   `Program.cs`, строка `host.Project.SaveFileInclude("io.nanasm", Librare.io_nanasm);`.  
   Это **не** дублирование (без сохранения `io.nanasm` в `IncludePath` `ReadInclude` бросит `FileNotFoundException` при резолве `#include "io.nanasm"` из `print.minic`). Но читатель может подумать, что это лишнее.  
   Фикс: комментарий `// print.minic делает #include "io.nanasm" — файл должен быть в IncludePath`.

10. **`source1` использует `print16` (UTF-16), но MiniC `char` = 16-bit (см. `CodeGenUtils.GetSizeForType`).**  
    `Program.cs`, `source1` + `Librare.print_minic`.  
    `char str[] = "Hello world"` — 11 символов + `'\0'` = 12 `char` × 2 байта = 24 байта в data-секции. `print16` читает `str[i]` как `char` (16-bit), пишет low/high в порт. `QueuedIOStream(Utf16)` собирает обратно. Всё согласовано, но читателю неочевидно, что `char` в MiniC — 16-bit.  
    Фикс: комментарий в `source1` `// char = 16-bit (UTF-16 code unit), print16 = 2 порта на символ`.

11. **`void main()` — не идиоматично для C.**  
    `Program.cs`, `source1`: `void main() { ... }`. Компилятор принимает (`isMain = func.Name == "main"`, тип возврата не проверяется), но в C `main` возвращает `int`.  
    Фикс: `int main() { print16(str); return 0; }` — компилятор сгенерирует `RET`-эквивалент (`EmitEnd` в эпилоге `main`), но пример станет ближе к C-стилю.