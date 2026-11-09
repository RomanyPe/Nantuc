# Nantuc — README

[English version](README.md)

> Платформа для выполнения байткода с собственным языком MiniC (почти полноценный C), ассемблером NANASM, компилятором, виртуальной машиной и публичным API для встраивания.
> **Версия проекта:** указана в `CHANGELOG.ru.md`, semver гарантирован.
> Написана на C# (.NET 10). Компилятор и рантайм разделены по слоям, `unsafe`-код изолирован в ядре, публичный API — тонкий фасад с безопасными `Try*`-методами и `IdState`-диагностикой.

Вот объединённое описание:

**Nantuc** — это мини-фреймворк и безопасный низкоуровневый скриптовый движок для .NET, предназначенный для случаев, когда нужен контроль уровня C, но коду нельзя доверять. Он включает C-подобный язык MiniC и ассемблер NANASM (текстовый и IL) с дизассемблером и пошаговой отладкой, а также полный конвейер: лексер, парсер, оптимизатор AST, генератор байткода, ассемблер и регистровую VM с памятью и портовой шиной. В состав входят устройства ввода-вывода, явные In/Out, API для встраивания, тонкий CLI (`nantuc`) и изоляция, благодаря которой падение скрипта не приводит к падению хоста.

В репозитории: Hello world на MiniC, играбельная змейка, тест аллокатора и два бенчмарка VM.

## Содержание
- [Быстрый старт](#быстрый-старт)
- [Ключевые особенности](#ключевые-особенности)
- [Безопасный API](#безопасный-api)
- [Архитектура](#архитектура)
- [Модули и NuGet](#модули-и-nuget)
- [Требования](#требования)
- [Сборка и запуск](#сборка-и-запуск)
- [Примеры](#примеры)
- [Метрики и качество](#метрики-и-качество)
- [Вклад](#вклад)
- [Лицензия](#лицензия)
- [Ссылки](#ссылки)

## Быстрый старт

Минимальный пример - компиляция и запуск `Hello world` на MiniC с выводом через UART:

```csharp
using Nantuc.Common;
using Nantuc.VmApplication.Extensions.Host;

const string source =
    """
    #include "print.minic"
    char str[] = "Hello world";
    void main() { print16(str); }
    """;

using var host = VmHostFactory.CreateDefault();

host.Project.SaveFileInclude("io.nanasm", Libraries.io_nanasm);
host.Project.SaveFileInclude("print.minic", Libraries.print_minic);

var result = host.RunAndKeep(source, SourceLanguage.MiniC, output: Console.Out);

if (!result.Success)
    Console.Error.WriteLine(string.Join("\n", result.Diagnostics));
```

Подробный пример:

```csharp
using Nantuc.Common;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Extensions.Emulator.IO;
using Nantuc.VmApplication.Extensions.Host;
using Nantuc.VmApplication.Extensions.StdLibraries;

const string source =
    """
    #include "print.minic"
    char str[] = "Hello world";
    void main() { print16(str); }
    """;

using var host = VmHostFactory.CreateDefault();
host.Project.SaveFileProject("main.minic", source);
host.Project.SaveFileInclude("io.nanasm", Libraries.io_nanasm);
host.Project.SaveFileInclude("print.minic", Libraries.print_minic);

var vmState = host.Emulator.TryRegisterDevice(
    DeviceSpec.Create(RamSize.MB128, name: "ConsoleVM"),
    sector: 0, out int vmId);
if (vmState != IdState.Valid) { Console.Error.WriteLine($"register vm: {vmState}"); return; }

var uartState = host.Emulator.TryRegisterDevice(
    new QueuedIOStream(Console.Out, PortCharEncoding.Utf16),
    name: "console", sector: 1, out _);
if (uartState != IdState.Valid) { Console.Error.WriteLine($"register uart: {uartState}"); return; }

if (!host.Emulator.TryGetDeviceContext(vmId, out var vm)) { Console.Error.WriteLine("no ctx"); return; }

var compilation = host.Project.CompileToIL(baseAddress: 0, optimize: true).Build();
if (!compilation.Success)
{
    foreach (var e in compilation.Errors!) Console.Error.WriteLine(e);
    return;
}

if (!vm.TryLoadProgram(compilation.Program!, loadAddress: 0, out var error))
{
    Console.Error.WriteLine(error);
    return;
}

ProcessorFault fault = vm.Run(new LaunchOptions(ShowTimer: true));
if (fault.Status != BiosStatus.EndProgramm)
    Console.Error.WriteLine($"VM fault: {fault.Status} at IP=0x{fault.InstructionPointer:X}");
```

Подробный разбор (объяснение портов `0x10`/`0x12`, `QueuedIOStream`, диагностика) — в [`Nantuc.Samples.Minimal/README.ru.md`](Nantuc.Samples.Minimal/README.ru.md).


**То же самое через CLI (`nantuc`)** — пакет ставится как .NET global tool и покрывает весь конвейер: `compile` → `run` → `disasm` + управление инклюдами через `lib`.

```bash
# Установить CLI как .NET global tool
dotnet tool install --global Nantuc.CLI

# Скомпилировать проект из текущей директории в ./bin (с оптимизациями)
nantuc compile -s ./bin -o

# Запустить с лимитом шагов и таймером
nantuc run -p ./bin/program.bin -ram 8MB -l 500000 -t

# Дизассемблировать первые 128 байт в файл ./out/result-decompile.txt
nantuc disasm -f ./bin/program.bin -cs 128 -s ./out

# Добавить инклюд из файла и проверить его наличие
nantuc lib add -p ./print.minic
nantuc lib exist print.minic
```

Справка по всем командам — `nantuc help`. Детали — в [`src/Nantuc.CLI/README.ru.md`](src/Nantuc.CLI/README.ru.md).

## Ключевые особенности

- **Два языка**: MiniC (C-подобный: структуры, указатели, массивы, `const`, `sizeof`, inline `asm`, `extern`, `#include`) и NANASM (ассемблер с метками и размерными суффиксами). Спецификации — в [`Nantuc.Compiler/README.ru.md`](Nantuc.Compiler/README.ru.md).
- **Компилятор с нуля**: лексер, рекурсивный парсер, AST, жадная раскраска регистров, кодогенерация в 32-битный байткод, AST- и peephole-оптимизации, `const`-correctness.
- **VM с изолированным `unsafe`**: нативная RAM через `NativeMemory`, регистры в `InlineArray` (без аллокаций на шаг), порт-шина с маршрутизацией `address → (device, offset)`.
- **Безопасный API**: `TryRegisterDevice`/`TryGetDeviceContext` вместо `id < 0` + `!`, `IdState` вместо магических `-1..-4`, `ProcessorFault` из `Run`/`DebugRun`, `TryLoadProgram` вместо исключений, `Pause()`/`Resume()` для управления из другого потока.
- **Устройства как first-class**: VM сама является устройством (`IPortController`) — можно вкладывать VM друг в друга, подключать UART, мониторы, клавиатуры.
- **Публичный API без хаков**: `VmHost` / `DeviceContext` / `VmHostProject` — весь unsafe и нативные буферы спрятаны в `Nantuc.Kernel`.
- **Дизассемблер и отладчик**: секции кода/данных, текстовый и структурный вывод; `Debugger` поверх `DeviceContext` с `Step`/`StepOver`/`StepOut`/`Continue`/`RunToEnd`, точками останова, `RequestStop` из другого потока, best-effort стеком вызовов, чтением RAM.
- **CLI `nantuc`**: тонкая обёртка над публичным API (`compile`, `run`, `disasm`, `lib`, `help`), без собственной логики компиляции/исполнения; ставится как .NET global tool (`Nantuc.CLI`).
- **Стандартные оптимизации** (AST и peephole) и инфраструктура для своих правил.
- **Два бенчмарка**: ручной `Nantuc.Benchmarks` (MIPS, стабильность, перцентили, CV, JIT-impact) и `Nantuc.Benchmarks.BenchmarkDotNet` на **14 MiniC-кейсах**. Рядом — `OtherLangs`: те же 14 кейсов на Python, Lua, LuaJIT, Ruby, JS, C, C++, C#, Java и Rust (отдельный раннер, [`benchmarks/OtherLangs/README.ru.md`](benchmarks/OtherLangs/README.ru.md)).

## Безопасный API

Публичный API `Nantuc.VmApplication` следует паттерну «Try + IdState + ProcessorFault»:

| Сценарий | Старый (raw) путь | Безопасный путь |
|---|---|---|
| Регистрация устройства | `int id = RegisterDevice(...)` + проверка `id < 0` | `TryRegisterDevice(..., out int id)` → `IdState` |
| Получение `DeviceContext` | `GetDeviceContext(id)!` | `TryGetDeviceContext(id, out ctx)` |
| Загрузка программы | `LoadProgram(...)` (бросает) | `TryLoadProgram(..., out error)` |
| Запуск VM | `Run()` → `void` | `Run(opt)` → `ProcessorFault` |
| Остановка по лимиту | `RunWithLimit(...)` | `Run(new LaunchOptions(StepLimit: N))` |
| Пауза/возобновление | — (было только `Stop`) | `Pause()` / `Resume()` — queue-based, безопасно из другого потока |
| Пошаговое исполнение | — | `Debugger` (в `VmApplication.Extensions`) поверх `RunStepMode`/`SingleStep` |

- **`IdState`** (`Nantuc.Common`) — `Unknown` (0, default) / `Valid` (1) / `NotAssigned` (-1) / `OutOfRange` (-2) / `NoFreeSlot` (-3) / `Rejected` (-4). `default(IdState) == Unknown`; **не полагайтесь на `default(IdState) == Valid`** — это изменилось.
- **`ProcessorFault`** (`Nantuc.Common`) — `(BiosStatus Status, ulong FaultData, ulong InstructionPointer, OpCode OpCode)`. На успехе `Status == EndProgramm`; на падении — `DivOnZero`/`SegmentationFault`/… с точным `IP` и `OpCode`.
- **`IProcessorFaultPolicy`** — используется **внутри** VM; возврат из `Run`/`DebugRun` — уже `ProcessorFault`, отдельная политика не нужна.
- **`Debugger`** (`Nantuc.VmApplication.Extensions.Debugging`) — не добавляет контрактов в `Kernel`/`VmApplication`, работает через публичный API. Подробности — в [`Nantuc.VmApplication.Extensions/README.ru.md`](src/Nantuc.VmApplication.Extensions/README.ru.md#отладчик).

## Архитектура

Слои идут снизу вверх. Каждый следующий зависит только от предыдущих, обратных зависимостей нет.

```
┌──────────────────────────────────────────────────────────────────────────────────┐
│  Nantuc.Samples.*   Nantuc.Benchmarks.*   Nantuc.CLI                             │
│  (примеры, конечные продукты, бенчмарки, консольный интерфейс)                   │
└──────────────────────────────────────────────────────────────────────────────────┘
                              │
┌──────────────────────────────────────────────────────────────────────────────────┐
│  Nantuc.VmApplication.Extensions                                                 │
│  (дизассемблер, отладчик, I/O, стандартные оптимизации, библиотеки, утилиты)     │
└──────────────────────────────────────────────────────────────────────────────────┘
                              │
┌──────────────────────────────────────────────────────────────────────────────────┐
│  Nantuc.VmApplication                                                            │
│  (VmHost, DeviceContext, VmEmulator, VmHostProject, CompilationResult)           │
└──────────────────────────────────────────────────────────────────────────────────┘
              │                                │
┌──────────────────────────┐    ┌──────────────────────────────────────────────────┐
│  Nantuc.Compiler         │    │  Nantuc.Kernel                                   │
│  (MiniC + NANASM)        │    │  (Processor, MemoryBus, PortBus,                 │
│  Assembler / ILAssembler │    │   Device, ManagerDevices)                        │
└──────────────────────────┘    └──────────────────────────────────────────────────┘
              │                                │
┌──────────────────────────────────────────────────────────────────────────────────┐
│  Nantuc.Contracts                                                                │
│  (IPortBus, IPortUse, IPortController, ProcessorSnapshot, ...)                   │
└──────────────────────────────────────────────────────────────────────────────────┘
              │                                │
┌──────────────────────────────────────────────────────────────────────────────────┐
│  Nantuc.Common            Nantuc.Diagnostics                                     │
│  (ISA, размеры, BiosStatus,      (ErrorCode, DiagnosticData,                     │
│   ProcessorFault, IdState)        ThrowCompiler)                                 │
└──────────────────────────────────────────────────────────────────────────────────┘
```

Ключевые инварианты:
- **`Diagnostics` не зависит ни от одного проекта Nantuc** — автор компилятора не тянет unsafe-ядро ради кодов ошибок.
- **Компилятор не зависит от рантайма** — `Nantuc.Compiler` знает только `Common` + `Contracts` + `Diagnostics`.
- **`VmApplication` — единственный модуль, который одновременно знает про компилятор и рантайм**; он склеивает «скомпилировать» и «запустить». Отладчик, дизассемблер и бенчмарки живут выше по слоям.
- **`Nantuc.CLI` — верхний слой**: не содержит собственной логики, только разбирает аргументы и вызывает `VmApplication`/`Extensions`.

## Модули и NuGet

Все модули опубликованы как NuGet-пакеты на [nuget.org](https://www.nuget.org/profiles/RomanyPe) (автор — `RomanyPe`). Ставится только то, что нужно сценарию; транзитивные зависимости подтягиваются автоматически. У каждого модуля есть свой README с деталями и примерами.

| Пакет | Назначение | README |
|---|---|---|
| `Nantuc.Common` | Опкоды, регистры, размеры RAM/портов, `BiosStatus`, `ProcessorFault`, `IdState` (default-safe), декодер | [README](src/Nantuc.Common/README.ru.md) |
| `Nantuc.Contracts` | `IPortBus`, `IPortUse`, `IPortController`, `ProcessorSnapshot`, `IProcessorFaultPolicy`, `OptimizationId` | [README](src/Nantuc.Contracts/README.ru.md) |
| `Nantuc.Diagnostics` | `ErrorCode`, `DiagnosticData` (AOT-safe), `IErrorMessageProvider`, `ThrowCompiler`, `CodeException` | [README](src/Nantuc.Diagnostics/README.ru.md) |
| `Nantuc.Kernel` | `Processor`, `MemoryBus`, `PortBus`, `ManagerDevices` (`TryAddDevice` → `IdState`), `Emulator`, `Device` (`Run`/`DebugRun` → `ProcessorFault`) | [README](src/Nantuc.Kernel/README.ru.md) |
| `Nantuc.Compiler` | `Lexer`, `Parser`, AST, `FunctionGenerator`, `Assembler`, `ILAssembler`, `AssemblerParser`, `PeepholeOptimizer`, `AstOptimizer`, `const`-correctness | [README](src/Nantuc.Compiler/README.ru.md) |
| `Nantuc.VmApplication` | `VmHost`, `DeviceContext`, `VmEmulator` (`TryRegisterDevice`/`TryGetDeviceContext`), `VmHostProject`, `CompilationResult`, `DeviceSpec` | [README](src/Nantuc.VmApplication/README.ru.md) |
| `Nantuc.VmApplication.Extensions` | Дизассемблер, `Debugger`, `SynchronousIOStream`/`QueuedIOStream`, `AddStdRules`, `Libraries`, `RunAndKeep`/`RunOnce` | [README](src/Nantuc.VmApplication.Extensions/README.ru.md) |
| `Nantuc.CLI` | CLI `nantuc`: `compile`, `run`, `disasm`, `lib`, `help`; ставится как .NET global tool | [README](src/Nantuc.CLI/README.ru.md) |

**Зависимости (снизу вверх):** `Common` → `Contracts` → `Diagnostics` → `Kernel` → `Compiler` → `VmApplication` → `VmApplication.Extensions` → `Nantuc.CLI`.

**Что ставить под сценарий:**

```bash
# Запустить MiniC-программу (остальное подтянется транзитивно)
dotnet add package Nantuc.VmApplication
dotnet add package Nantuc.VmApplication.Extensions

# Только компилятор, без рантайма (без unsafe / NativeMemory)
dotnet add package Nantuc.Compiler
dotnet add package Nantuc.Diagnostics

# Своё устройство (реализовать IPortController)
dotnet add package Nantuc.Contracts
dotnet add package Nantuc.Kernel

# CLI как .NET global tool
dotnet tool install --global Nantuc.CLI
nantuc help
```

Версия подтягивается последняя стабильная; зафиксировать конкретную — `--version 0.8.2`.

> `Nantuc.Common`, `Nantuc.Contracts`, `Nantuc.Diagnostics` не тянут `unsafe`/`NativeMemory`/`Regex` — их можно подключать в WASM/AOT-проекты и в окружения с запретом `unsafe`.

## Требования

- **.NET 10 SDK** (все проекты таргетят `net10.0`).
- **C# 14**: `extension`-блоки, `[InlineArray]`, `params ReadOnlySpan`, `field`-keyword, `FrozenDictionary`/`FrozenSet`, primary constructors.
- **Windows / Linux / macOS / AoT** (другие платформы не тестировались, но Nantuc не привязан к конкретной платформе).
- **WASM**: `Common`/`Contracts`/`Diagnostics` — полностью WASM-safe; `Compiler`, `Kernel`, `Extensions`, `Benchmarks`, `CLI` имеют ограничения (блокирующие вызовы, лимиты линейной памяти, single-thread browser). Полный разбор — [`docs/wasm.ru.md`](docs/wasm.ru.md).

## Сборка и запуск

```bash
# Собрать всё
dotnet build Nantuc.slnx -c Release

# Hello world
dotnet run --project Nantuc.Samples.Minimal -c Release

# Змейка (управление: стрелки, Q/Escape — выход)
dotnet run --project Nantuc.Samples.Scratch -c Release -snake 150

# Тест аллокатора (по умолчанию в Samples.Scratch)
dotnet run --project Nantuc.Samples.Scratch -c Release

# CLI локально (без установки как global tool)
dotnet run --project src/Nantuc.CLI -c Release -- compile -s ./bin -o
dotnet run --project src/Nantuc.CLI -c Release -- run -p ./bin/program.bin -l 100000 -t

# Ручной бенчмарк (результаты в bin/.../logs/result_Benchmark_Logs_{ts}.txt)
dotnet run --project Nantuc.Benchmarks -c Release

# BenchmarkDotNet (полный прогон долгий; фильтр по кейсам)
dotnet run --project Nantuc.Benchmarks.BenchmarkDotNet -c Release -- --filter *Call* *Memory*
```

Артефакты BenchmarkDotNet — в `BenchmarkDotNet.Artifacts/`. Профиль IDE `Game` — из `launchSettings.json`.

## Примеры

| Пример | Что показывает | README |
|---|---|---|
| `Nantuc.Samples.Minimal` | Hello world, `TryRegisterDevice` + `TryGetDeviceContext` + `ProcessorFault`, порты `0x10`/`0x12`, `QueuedIOStream` | [README](samples/Nantuc.Samples.Minimal/README.ru.md) |
| `Nantuc.Samples.Scratch` | `SnakeGameOnNantuc`, `MonochromeMonitor` (32×16), HALT/WAKE-цикл, ANSI-рендер; тест аллокатора (`Libraries.alloc_minic`) | [README](samples/Nantuc.Samples.Scratch/README.ru.md) |
| `Nantuc.Benchmarks` | Ручной бенчмарк 14 MiniC-кейсов: MIPS, стабильность, перцентили, CV, JIT-impact | [README](benchmarks/Nantuc.Benchmarks/README.ru.md) |
| `Nantuc.Benchmarks.BenchmarkDotNet` | BenchmarkDotNet-бенчмарк 14 MiniC-кейсов: время, аллокации, статистика, артефакты | [README](benchmarks/Nantuc.Benchmarks.BenchmarkDotNet/README.ru.md) |
| `OtherLangs` | Сравнение с 10 другими языками на тех же 14 MiniC-кейсах: свой процесс, свой раннер, сводный `results.json`/`results.csv` | [README](benchmarks/OtherLangs/README.ru.md) |
| `Nantuc.CLI` | `nantuc`: `compile`/`run`/`disasm`/`lib`/`help`, установка как .NET global tool | [README](src/Nantuc.CLI/README.ru.md) |

Дополнительные примеры (полный цикл с диагностикой, пошаговая отладка, своя оптимизация, Pause/Resume) — в README соответствующих модулей (`VmApplication`, `VmApplication.Extensions`, `CLI`).

## Метрики и качество

- ~22 000 строк, 13 проектов; ядро и API (без сэмплов, тестов и бенчмарков) — ~17,900 строк, 7 проектов.
- Конечный продукт `Nantuc.CLI` (консольное приложение): ~865 строк.
- Индекс поддерживаемости: 80–97 (основные модули).
- Полная таблица метрик — [docs/METRICS.md](docs/METRICS.md).

**В работе / планы:**
- JIT-компилятор на C/C++ (отдельный проект).
- **Отладчик:** frame pointer в `Processor` для точного `GetCallStack`, `Debugger.Reset()`/`Reload()`, UI-панель для IDE (через `Stopped`-событие).
- **CLI:** интерактивный REPL-режим, конфигурационный файл проекта вместо флагов на каждую команду.

## Вклад

Проект открыт для issues и pull request'ов.

- **Нашли баг** — issue с минимальным примером (MiniC/NANASM + ожидаемое/фактическое поведение).
- **Хотите фичу** — issue с описанием сценария (что делаете, что мешает).
- **Правка** — fork → branch → PR. Для крупных изменений сначала issue (обсудить дизайн).
- **Стиль**: следуйте стилю соседних файлов; `Nullable=enable`, `ImplicitUsings=enable`; `unsafe` — только в `Nantuc.Kernel`.

Перед PR:

```bash
dotnet build Nantuc.slnx -c Release
dotnet test -c Release
```

## Лицензия

См. [`LICENSE`](LICENSE) в корне репозитория.

## Ссылки

- GitHub: [RomanyPe/Nantuc](https://github.com/RomanyPe/Nantuc)
- NuGet-профиль автора: [nuget.org/profiles/RomanyPe](https://www.nuget.org/profiles/RomanyPe)
- История изменений: [`CHANGELOG.ru.md`](CHANGELOG.ru.md)
- Спецификация байткода: [`docs/bytecode.ru.md`](docs/bytecode.ru.md)
- Справочник кодов ошибок: [`docs/error-codes.ru.md`](docs/error-codes.ru.md)
- Метрики: [`docs/METRICS.md`](docs/METRICS.md)
- WASM-ограничения: [`docs/wasm.ru.md`](docs/wasm.ru.md)
- Встроенные библиотеки MiniC/NANASM: [`src/Nantuc.VmApplication.Extensions/STDLIBRARIES.ru.md`](src/Nantuc.VmApplication.Extensions/STDLIBRARIES.ru.md)
- CLI: [`src/Nantuc.CLI/README.ru.md`](src/Nantuc.CLI/README.ru.md)
- Сравнение с другими языками (OtherLangs): [`benchmarks/OtherLangs/README.ru.md`](benchmarks/OtherLangs/README.ru.md)
- README модулей: см. столбец «README» в разделе [Модули и NuGet](#модули-и-nuget)