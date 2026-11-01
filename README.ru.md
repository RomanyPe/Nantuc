# Nantuc — README

> Платформа для выполнения байткода с собственным языком MiniC (почти полноценный C), ассемблером NANASM, компилятором, виртуальной машиной и публичным API для встраивания.
> **Версия проекта:** текущая версия проекта указана в `CHANGELOG.ru.md`, semver частично гарантирован.
> Написана на C# (.NET 10). Компилятор и рантайм разделены по слоям, unsafe-код изолирован в ядре, публичный API — тонкий фасад с безопасными `Try*`-методами и `IdState`-диагностикой.

Nantuc — это мини-фреймворк, в котором есть всё, что нужно для запуска программы на C-подобном языке:
лексер, парсер, AST-оптимизатор, кодогенератор в байткод, ассемблер (текстовый и IL),
виртуальная машина с регистрами, памятью и порт-шиной, дизассемблер, отладчик,
I/O-устройства и API для встраивания в приложение.
В репозитории есть вывод Hello world на MiniC, играбельная змейка на MiniC, тест аллокатора и бенчмарк VM.

## Качество кода
- ~20 000 строк, 11 проектов
- ~17 000 строк, 7 проектов (без двух примеров, тестов, бенчмарка)
- Индекс поддерживаемости: 80–97 (основные модули)
- Полная таблица метрик: [docs/METRICS.md](docs/METRICS.md)


## Содержание
- [Ключевые особенности](#ключевые-особенности)
- [Безопасный API](#безопасный-api)
- [Быстрый старт](#быстрый-старт)
- [Архитектура](#архитектура)
- [Структура репозитория](#структура-репозитория)
- [Модули](#модули)
- [Языки и ISA](#языки-и-isa)
- [Требования](#требования)
- [Сборка и запуск](#сборка-и-запуск)
- [Примеры](#примеры)
- [Метрики и качество](#метрики-и-качество)
- [Статус и планы](#статус-и-планы)
- [Вклад](#вклад)
- [Лицензия](#лицензия)

## Ключевые особенности

- **Два языка**: MiniC (C-подобный: структуры, указатели, массивы, `const`, `sizeof`, inline `asm`, `extern`, `#include`) и NANASM (ассемблер с метками и размерными суффиксами).
- **Компилятор с нуля**: лексер, рекурсивный парсер, AST, жадная раскраска регистров, кодогенерация в 32-битный байткод, AST- и peephole-оптимизации, `const`-correctness.
- **VM с изолированным `unsafe`**: нативная RAM через `NativeMemory`, регистры в `InlineArray` (без аллокаций на шаг), порт-шина с маршрутизацией `address → (device, offset)`.
- **Безопасный API**: `TryRegisterDevice`/`TryGetDeviceContext` вместо `RegisterDevice(id < 0)` + `!`, `IdState` вместо магических `-1..-4`, `ProcessorFault` из `Run`/`DebugRun` вместо ручного `IProcessorFaultPolicy`, `TryLoadProgram` вместо исключений, `Pause()`/`Resume()` для управления из другого потока.
- **Устройства как first-class**: VM сама является устройством (`IPortController`) — можно вкладывать VM друг в друга, подключать UART, мониторы, клавиатуры.
- **Публичный API без хаков**: `VmHost` / `DeviceContext` / `VmHostProject` — весь unsafe и нативные буферы спрятаны в `Nantuc.Kernel`.
- **Дизассемблер** с секциями кода и данных, текстовый и структурный.
- **Отладчик** (`Debugger`) поверх `DeviceContext`: `Step`/`StepOver`/`StepOut`/`Continue`/`ContinueTo`/`RunToEnd`, точки останова, `RequestStop` из другого потока, best-effort стек вызовов, чтение RAM (`ReadUInt16/32/64`, `ReadCString`), дизассемблирование текущей инструкции через `StringDisassembler`. Без `AsRawDevice` и `unsafe` — только публичный API `DeviceContext`.
- **Стандартные оптимизации** (AST и peephole) и инфраструктура для своих правил.
- **Бенчмарк** с 12 кейсами: память (safe/unsafe), ALU (ILP и без), вызовы, стек, битовые операции, умножение, ветвления. MIPS, перцентили, CV, JIT-impact.

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

- **`IdState`** (`Nantuc.Kernel.Common`) — `Unknown` (0, default) / `Valid` (1) / `NotAssigned` (-1) / `OutOfRange` (-2) / `NoFreeSlot` (-3) / `Rejected` (-4). Возвращается из `ManagerDevices.TryAddDevice` (и из `Emulator.RegisterDevice`/`Emulator.TryRegisterDevice`). `default(IdState) == Unknown`, чтобы неинициализированный статус не воспринимался как успешный; **не полагайтесь на `default(IdState) == Valid`** — это изменилось.
- **`ProcessorFault`** (`Nantuc.Kernel.Common`) — `(BiosStatus Status, ulong FaultData, ulong InstructionPointer, OpCode OpCode)`. На успехе `Status == EndProgramm`; на падении — `DivOnZero`/`SegmentationFault`/… с точным `IP` и `OpCode`.
- **`IProcessorFaultPolicy`** — используется **внутри** VM для решения «продолжать ли». Возврат из `Run`/`DebugRun` — это уже `ProcessorFault`, отдельная политика не нужна.
- **`Debugger`** (`Nantuc.VmApplication.Extensions.Debugging`) — не добавляет новых контрактов в `Kernel`/`VmApplication`, работает через `DeviceContext.RunStepMode`/`SingleStep`/`GetRegistersSnapshot`/`NativeReadOnlyBuffer`. Поставляется с `DebugStop`, `DebugStopReason`, `CallStackFrame`.

## Быстрый старт

Минимальный пример — компиляция и запуск `Hello world` на MiniC с выводом через UART, с безопасной проверкой каждого шага:

```csharp
using Nantuc.Kernel.Common;
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

// 1) Регистрация VM — через TryRegisterDevice, IdState вместо int < 0
var vmState = host.Emulator.TryRegisterDevice(
    DeviceSpec.Create(RamSize.MB128, name: "ConsoleVM"),
    sector: 0,
    out int vmId);
if (vmState != IdState.Valid) { Console.Error.WriteLine($"register vm: {vmState}"); return; }

// 2) Регистрация UART — тоже через TryRegisterDevice
var uartState = host.Emulator.TryRegisterDevice(
    new QueuedIOStream(Console.Out, PortCharEncoding.Utf16),
    name: "console",
    sector: 1,
    out _);
if (uartState != IdState.Valid) { Console.Error.WriteLine($"register uart: {uartState}"); return; }

// 3) DeviceContext — через TryGetDeviceContext (без !)
if (!host.Emulator.TryGetDeviceContext(vmId, out var vm)) { Console.Error.WriteLine("no ctx"); return; }

// 4) Компиляция
var compilation = host.Project.CompileToIL(baseAddress: 0, optimize: true).Build();
if (!compilation.Success)
{
    foreach (var e in compilation.Errors!) Console.Error.WriteLine(e);
    return;
}

// 5) Загрузка — через TryLoadProgram
if (!vm.TryLoadProgram(compilation.Program!, loadAddress: 0, out var error))
{
    Console.Error.WriteLine(error);
    return;
}

// 6) Запуск — Run возвращает ProcessorFault, статус можно проверить
ProcessorFault fault = vm.Run(new LaunchOptions(ShowTimer: true));
if (fault.Status != BiosStatus.EndProgramm)
    Console.Error.WriteLine($"VM fault: {fault.Status} at IP=0x{fault.InstructionPointer:X}");
```

Подробный разбор — в [`Nantuc.Samples.Minimal`](Nantuc.Samples.Minimal/README.ru.md).

## Архитектура

Слои идут снизу вверх. Каждый следующий зависит только от предыдущих, обратных зависимостей нет.

```
┌──────────────────────────────────────────────────────────────────────┐
│  Nantuc.Samples.Minimal   Nantuc.Samples.Scratch   Nantuc.Benchmarks  │
│  (примеры и конечные продукты)                                       │
└──────────────────────────────────────────────────────────────────────┘
                              │
┌──────────────────────────────────────────────────────────────────────┐
│  Nantuc.VmApplication.Extensions                                     │
│  (дизассемблер, отладчик, I/O, стандартные оптимизации,              │
│   библиотеки, утилиты)                                               │
└──────────────────────────────────────────────────────────────────────┘
                              │
┌──────────────────────────────────────────────────────────────────────┐
│  Nantuc.VmApplication                                                │
│  (VmHost, DeviceContext, VmEmulator, VmHostProject, CompilationResult)│
│  TryRegisterDevice / TryGetDeviceContext / Run → ProcessorFault       │
└──────────────────────────────────────────────────────────────────────┘
              │                                │
┌──────────────────────────┐    ┌──────────────────────────────────────┐
│  Nantuc.Compiler         │    │  Nantuc.Kernel                       │
│  (MiniC + NANASM)        │    │  (Processor, MemoryBus, PortBus,     │
│  Assembler / ILAssembler │    │   Device, ManagerDevices)            │
└──────────────────────────┘    └──────────────────────────────────────┘
              │                                │
┌──────────────────────────────────────────────────────────────────────┐
│  Nantuc.Kernel.Contracts                                             │
│  (IPortBus, IPortUse, IPortController, ProcessorSnapshot, ...)       │
└──────────────────────────────────────────────────────────────────────┘
              │                                │
┌──────────────────────────────────────────────────────────────────────┐
│  Nantuc.Kernel.Common            Nantuc.Kernel.Diagnostics           │
│  (ISA, размеры, BiosStatus,      (ErrorCode, DiagnosticData,         │
│   ProcessorFault, IdState)        ThrowCompiler)                     │
└──────────────────────────────────────────────────────────────────────┘
```

Обратите внимание:
- **`Kernel.Diagnostics` не зависит ни от одного проекта Nantuc.** Автор компилятора не тянет unsafe-ядро ради кодов ошибок.
- **Компилятор не зависит от рантайма.** `Nantuc.Compiler` знает только про `Kernel.Common` (ISA), `Kernel.Contracts` (типы оптимизаций) и `Kernel.Diagnostics` (ошибки). Ни `Processor`, ни `MemoryBus` он не видит.
- **`IdState` живёт в `Kernel.Common`**, а не в `VmApplication`: `Kernel` (где `ManagerDevices`) не может зависеть от `VmApplication`, а `Common` — единственный слой, видимый и `Kernel`, и `VmApplication`, и `Extensions`.
- **VMApplication — единственный модуль, который одновременно знает про компилятор и рантайм.** Он склеивает «скомпилировать» и «запустить».
- **Отладчик живёт в `VmApplication.Extensions`**, а не в `VmApplication`/`Kernel`: он использует только публичный API (`RunStepMode`/`SingleStep`/`GetRegistersSnapshot`/`NativeReadOnlyBuffer`) и не требует `AsRawDevice`/`unsafe`. Это сохраняет разделение «ядро ничего не знает про UI отладчика».

## Структура репозитория

```
Nantuc/
├── Nantuc.Kernel.Common/              # ISA, размеры, BiosStatus, ProcessorFault, IdState
├── Nantuc.Kernel.Contracts/           # публичные контракты (порты, процессор)
├── Nantuc.Kernel.Diagnostics/         # ErrorCode, DiagnosticData, ThrowCompiler, CodeException
├── Nantuc.Kernel/                     # реализация CPU, RAM, PortBus, Device
├── Nantuc.Compiler/                   # MiniC + NANASM (frontend, backend, ASM, оптимизации)
├── Nantuc.VmApplication/              # основной API (TryRegisterDevice, TryGetDeviceContext, Run → ProcessorFault)
├── Nantuc.VmApplication.Extensions/   # дизассемблер, отладчик, I/O, оптимизации, библиотеки
├── Nantuc.Samples.Minimal/            # Hello world
├── Nantuc.Samples.Scratch/            # тест аллокатора + змейка
└── Nantuc.Benchmarks/                 # бенчмарк VM
```

## Модули

| Модуль | Назначение | Кому |
|---|---|---|
| [`Nantuc.Kernel.Common`](Nantuc.Kernel.Common/README.ru.md) | Опкоды, регистры, размеры RAM/портов, `BiosStatus`, `ProcessorFault`, `IdState` (`Unknown` = 0, default-safe), декодер инструкций | Авторы компилятора, расширений, дизассемблера |
| [`Nantuc.Kernel.Contracts`](Nantuc.Kernel.Contracts/README.ru.md) | `IPortBus`, `IPortUse`, `IPortController`, `ProcessorSnapshot`, `IProcessorFaultPolicy`, `OptimizationId` | Авторы устройств и расширений |
| [`Nantuc.Kernel.Diagnostics`](Nantuc.Kernel.Diagnostics/README.ru.md) | `ErrorCode`, `DiagnosticData`, `IErrorMessageProvider`, `ThrowCompiler`, `CodeException` | Авторы компилятора, IDE, CI |
| [`Nantuc.Kernel`](Nantuc.Kernel/README.ru.md) | `Processor`, `MemoryBus`, `PortBus`, `ManagerDevices` (`TryAddDevice` → `IdState`), `Emulator`, `Device` (`Run`/`DebugRun` → `ProcessorFault`) | Встраивающий код, автор отладчика |
| [`Nantuc.Compiler`](Nantuc.Compiler/README.ru.md) | `Lexer`, `Parser`, AST, `FunctionGenerator`, `Assembler`, `ILAssembler`, `AssemblerParser`, `PeepholeOptimizer`, `AstOptimizer` | Автор IDE, автор правил оптимизации |
| [`Nantuc.VmApplication`](Nantuc.VmApplication/README.ru.md) | `VmHost`, `DeviceContext`, `VmEmulator` (`TryRegisterDevice`/`TryGetDeviceContext`), `VmHostProject`, `CompilationResult`, `DeviceSpec` | Конечный пользователь платформы |
| [`Nantuc.VmApplication.Extensions`](Nantuc.VmApplication.Extensions/README.ru.md) | Дизассемблер, `Debugger` (шаг, точки останова, стек, RAM), `SynchronousIOStream`/`QueuedIOStream`, `AddStdRules`, `Libraries`, `RunAndKeep`/`RunOnce` | Автор IDE, автор игр, автор своих оптимизаций |
| [`Nantuc.Samples.Minimal`](Nantuc.Samples.Minimal/README.ru.md) | Минимальный Hello world | Новый пользователь |
| [`Nantuc.Samples.Scratch`](Nantuc.Samples.Scratch/README.ru.md) | Тест аллокатора + змейка с монитором | Пример игрового цикла и I/O |
| [`Nantuc.Benchmarks`](Nantuc.Benchmarks/README.ru.md) | Бенчмарк 12 кейсов с MIPS и статистикой | Автор ядра/компилятора |

## Языки и ISA

### MiniC

C-подобный язык. Поддерживает:

- Типы: `int`, `char`, `byte`, `ushort`, `short`, `ulong`, `long`, `uint`, `void`, `struct`.
- Указатели (`*`, `&`, `->`), массивы (включая инициализаторы `{...}` и строковые литералы для `char[]`).
- `const`-квалификаторы: `const int`, `int * const`, `const int *`, `const int * const`, `const int arr[]`, `const`-параметры (см. раздел «Константность» в README компилятора).
- `if`/`else`, `while`, `for`, `do-while`, `break`, `continue`, `return`.
- `sizeof(type)` и `sizeof(expr)`.
- Inline-ассемблер `asm { ... }`.
- `extern` для внешних функций (реализуются в NANASM).
- `#include "file"` для C и ASM.
- `+=`, `-=`, `*=`, `/=`, `%=`, `&=`, `|=`, `^=`, `<<=`, `>>=`.
- Префиксные/постфиксные `++`/`--`.

### NANASM

Ассемблер с метками и размерными суффиксами для инструкций памяти:

```asm
start:
    LDI r0, 0x10
    LOAD.S64 r1, [r0]
    ADD r1, r1
    STORE.S64 [r0], r1
    END
```

Поддерживаются `MOV`, `ADD`, `SUB`, `MULT_INT`, `DIV`, `MOD`, `SHR`, `SHL`, `AND`, `OR`, `XOR`, `NOT`, `INC`, `DEC`, `CMP`, `TEST`, `JMP`/`JZ`/`JNZ`/`JG`/`JL`, `CALL`/`RET`, `PUSH`/`POP`, `LOAD`/`STORE` и `*_IND`-варианты, `IN`/`OUT`, `HALT`/`WAKE`/`WAKE_INT`, `NOP`, `END`. Плюс секции `.data`/`.text` и директивы `.byte`, `.word`, `.dword`, `.quad`, `.ascii`, `.asciz`, `.zero`.

### ISA (кратко)

32-битная инструкция, little-endian:

```
бит:  31…20    | 19…18     | 17…13  | 12…8   | 7…0
      spare    | OpCodeSize| Reg2   | Reg1   | OpCode
```

- 8 бит на опкод, 5 бит на регистр (32 регистра: `r0..r27`, `rFL`, `rSP`, `rHP`, `rIP`), 2 бита на размер (`S8`/`S16`/`S32`/`S64`).
- Опкоды с immediate дополняются 8-байтным операндом с выравниванием до 8.
- Полная спецификация — в [`Nantuc.Kernel.Common`](Nantuc.Kernel.Common/README.ru.md) и [`docs/bytecode.ru.md`](docs/bytecode.ru.md).

## Требования

- **.NET 10 SDK**. Все проекты таргетят `net10.0`.
- **C# 14**: `extension`-блоки, `[InlineArray]`, `params ReadOnlySpan`, `field`-keyword, `FrozenDictionary`/`FrozenSet`, primary constructors.
- **Windows / Linux / macOS / AoT** (другие платформы не тестировались, но Nantuc не привязан к конкретной платформе). В WASM есть ограничения по модулям:
  - `Kernel.Common` / `Kernel.Contracts` / `Kernel.Diagnostics` — полностью WASM-safe (managed, без `unsafe`, без рефлексии).
  - `Nantuc.Compiler` — `[GeneratedRegex]` в `AssemblerParser.GetRexExtensionAsm()` — AOT-safe. `MiniCLanguageDefinition._keywordRegex` пока использует `RegexOptions.Compiled` и является WASM-blocker на browser-wasm AOT (см. [Compiler README](Nantuc.Compiler/README.ru.md), «Что исправить»).
  - `Nantuc.Kernel` — `unsafe` и `NativeMemory.AllocZeroed`/`Free`/`Clear` поддерживаются WASM (линейная память emscripten), но:
    - `Device.Run`/`DebugRun` синхронные и блокируют текущий поток (`ManualResetEventSlim.Wait(15)` при `IsSleeping`, `Thread.Sleep(delay)` в `DebugRun`) — в single-threaded browser это блокировка UI; нужен Web Worker или async-цикл.
    - `NativeMemory.AllocZeroed` для больших `RamSize` (≥ `MB512`, тем более `GB1`/`GB2`) рискует `OutOfMemoryException` из-за лимита линейной памяти WASM (~4 GiB на процесс, с фрагментацией). Практический потолок в браузере — ~256 MiB.
  - `Nantuc.VmApplication.Extensions` — `Debugger` работает, но `Continue`/`StepOver`/`StepOut`/`GetCallStack` — синхронные циклы, блокируют текущий поток; для browser-WASM — Web Worker или `Task.Run` с `RequestStop`.
  - `Nantuc.Benchmarks` — `Process`/P/Invoke, вне WASM.

## Сборка и запуск

**Собрать всё:**

```bash
dotnet build Nantuc.sln -c Release
```

**Запустить Hello world:**

```bash
dotnet run --project Nantuc.Samples.Minimal -c Release
```

**Запустить змейку** (`Nantuc.Samples.Scratch` — `-snake <period>`):

```bash
dotnet run --project Nantuc.Samples.Scratch -c Release -- -snake 150
```

Или через профиль IDE `Game` из `launchSettings.json`. Управление: стрелки — движение, `Q`/`Escape` — выход.

**Запустить тест аллокатора** (по умолчанию в `Nantuc.Samples.Scratch`):

```bash
dotnet run --project Nantuc.Samples.Scratch -c Release
```

**Запустить бенчмарк** (Linux: для `PriorityClass.High` нужен `sudo`):

```bash
dotnet run --project Nantuc.Benchmarks -c Release
```

Результаты сохраняются в `Nantuc.Benchmarks/bin/.../logs/result_Benchmark_Logs_{timestamp}.txt`.

## Примеры

### Hello world (безопасный путь)

См. [`Nantuc.Samples.Minimal/README.ru.md`](Nantuc.Samples.Minimal/README.ru.md) — полный разбор с `TryRegisterDevice` + `TryGetDeviceContext` + `ProcessorFault`, объяснение портов `0x10`/`0x12` и `QueuedIOStream`.

### Змейка

См. [`Nantuc.Samples.Scratch/README.ru.md`](Nantuc.Samples.Scratch/README.ru.md) — `SnakeGameOnNantuc`, `MonochromeMonitor` (32×16), HALT/WAKE-цикл, тикер, ANSI-рендер. Запуск через `dotnet run -- -snake 150`.

### Тест аллокатора

См. [`Nantuc.Samples.Scratch/README.ru.md`](Nantuc.Samples.Scratch/README.ru.md), `Program.cs` — `Libraries.alloc_minic` (first-fit + split + coalescing), проверка `return 10`, дамп регистров и дизассемблер RAM.

### Полный цикл с диагностикой

```csharp
using Nantuc.Kernel.Common;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Extensions.Host;
using Nantuc.VmApplication.Project;

using var host = VmHostFactory.CreateDefault();

var sources = host.Project.GetSourceFiles();
var comp = host.Project.Compile(sources, baseAddress: 0, optimize: true);
if (!comp.Success) { foreach (var e in comp.Errors!) Console.Error.WriteLine(e); return; }

var state = host.Emulator.TryRegisterDevice(
    DeviceSpec.Create(RamSize.MB16, name: "main"), sector: 0, out int vmId);
if (state != IdState.Valid) { Console.Error.WriteLine($"register: {state}"); return; }

if (!host.Emulator.TryGetDeviceContext(vmId, out var vm)) return;
if (!vm.TryLoadProgram(comp.Program!, 0, out var error)) { Console.Error.WriteLine(error); return; }

ProcessorFault fault = vm.Run(new LaunchOptions(ShowTimer: true,
    OnEnd: r => Console.WriteLine($"steps={r?.Steps} elapsed={r?.Elapsed}")));

if (fault.Status != BiosStatus.EndProgramm)
    Console.Error.WriteLine($"fault {fault.Status} at IP=0x{fault.InstructionPointer:X} op={fault.OpCode}");
```

### Пошаговая отладка

```csharp
using Nantuc.Kernel.Common;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Extensions.Debugging;
using Nantuc.VmApplication.Project;

using var host = VmHostFactory.CreateDefault();

var sources = host.Project.GetSourceFiles();
var comp = host.Project.Compile(sources, baseAddress: 0, optimize: false);
if (!comp.Success) { foreach (var e in comp.Errors!) Console.Error.WriteLine(e); return; }

var state = host.Emulator.TryRegisterDevice(
    DeviceSpec.Create(RamSize.MB16, name: "dbg"), sector: 0, out int vmId);
if (state != IdState.Valid) return;
if (!host.Emulator.TryGetDeviceContext(vmId, out var vm)) return;

using var dbg = new Debugger(vm);
dbg.Load(comp.Program!, loadAddress: 0, startAddress: 0);

dbg.AddBreakpoint(0x00);           // остановиться на входе в main
dbg.Stopped += s => Console.WriteLine($"STOP: {s}");

// Шаг с обходом CALL
var stop = dbg.StepOver();
Console.WriteLine($"  at: {dbg.CurrentInstruction}");

// Продолжить до breakpoint'а, но не больше 100k шагов
stop = dbg.Continue(stepLimit: 100_000);
if (stop.Reason == DebugStopReason.Breakpoint)
    Console.WriteLine($"  hit bp at 0x{stop.InstructionPointer:X}");

// Продолжить до конца, игнорируя все breakpoints
var final = dbg.RunToEnd();

// Инспекция RAM и регистров
var regs = dbg.Registers;
Console.WriteLine($"r0={regs[RegType.r0]}, sp=0x{regs[RegType.rSP]:X}");
ulong counter = dbg.ReadUInt64(0x100);

foreach (var frame in dbg.GetCallStack())
    Console.WriteLine($"#{frame.Index} return=0x{frame.ReturnAddress:X8}");
```

**Прервать длинный прогон из другого потока** (`RequestStop` — единственный потокобезопасный метод `Debugger`):

```csharp
var runner = Task.Run(() => dbg.Continue());
Thread.Sleep(200);
dbg.RequestStop();                 // volatile bool
var stop = runner.Result;          // Reason == UserStop
```

### Своя оптимизация

```csharp
using Nantuc.VmApplication.Extensions.Optimizators;

host.Project.Optimizator.VmAstOptimizer.AddStdRules();
host.Project.Optimizator.VmPeepholeOptimizer.AddStdRules();
host.Project.Optimizator.VmAstOptimizer.Add(new MyCustomRule());
```

### Pause/Resume из другого потока

```csharp
var runner = Task.Run(() => vm.Run());
Thread.Sleep(200);
vm.Pause();     // OpCode.HALT через очередь внешних команд
Thread.Sleep(100);
vm.Resume();    // OpCode.WAKE
runner.Wait();
```

## Метрики и качество

**Готово:**
- MiniC (лексер, парсер, AST, кодогенерация, структуры, указатели, массивы, `const`-correctness, inline asm, `#include`, `extern`).
- NANASM (текстовый ассемблер, размерные суффиксы, метки, секции `.data`/`.text`).
- VM (регистры, RAM, стек, heap, порты, HALT/WAKE, внешние команды, fault-policy).
- Устройства: UART (sync/async/numeric), монитор (32×16), клавиатура.
- Дизассемблер (текстовый и структурный, `Disassembler` — public).
- **Отладчик** (`Debugger` в `VmApplication.Extensions`): `Step`/`StepOver`/`StepOut`/`Continue`/`ContinueTo`/`RunToEnd`, точки останова, `RequestStop` из другого потока, `Wake` из `HALT`, best-effort стек вызовов, чтение RAM (`ReadUInt16/32/64`, `ReadCString`), дизассемблирование текущей инструкции. Без `AsRawDevice`/`unsafe`.
- Стандартные AST- и peephole-оптимизации.
- Безопасный публичный API: `TryRegisterDevice`, `TryGetDeviceContext`, `TryLoadProgram`, `Run`/`DebugRun` → `ProcessorFault`, `Pause()`/`Resume()`, `IdState` (`default(IdState) == Unknown`, не `Valid`).
- Публичный API (`VmHost`, `DeviceContext`, `VmHostProject`, `DeviceSpec`).
- Примеры: Hello world, змейка, тест аллокатора.
- Бенчмарк.
- AOT-friendly диагностика: `DiagnosticData` (без боксинга, без `Enum.ToString()`), `TokenTypeExtension.AsText` (ручной switch), `[GeneratedRegex]` в `AssemblerParser.GetRexExtensionAsm()`.
- Частично реализована песочница: Nantuc VM из коробки работает только в полностью изолированной песочнице — может отправлять данные и получать их по запросу `IN`/`OUT`, без прямого доступа к памяти хоста.
> Ограничение `IN`/`OUT` должно быть на вызывающем коде, а лучше всего — через маленькие короткие фасады.

> VM — минимальная среда с фиксированным ОЗУ, единым адресным пространством и безопасным набором инструкций (проверки границ/выравнивания, остановка с причиной вместо исключений). UNSAFE LOAD/STORE — явный escape hatch без проверок, ответственность на хосте. Внешние эффекты — только через IO-функции, предоставленные хостом. Остановка кооперативная и потокобезопасная; таймауты и бюджеты — политика хоста.
- Тесты: юнит на лексер/парсер/кодогенерацию, golden-тесты на исполнение, fuzz парсера, тесты на `const`-correctness, тесты на диагностику (`DefaultErrorMessageProvider`).
- Справочник кодов ошибок — [`docs/error-codes.ru.md`](docs/error-codes.ru.md).
- Полная спецификация байткода — [`docs/bytecode.ru.md`](docs/bytecode.ru.md).
- Справочник по встроенным библиотекам — [`Nantuc.VmApplication.Extensions/STDLIBRARIES.ru.md`](Nantuc.VmApplication.Extensions/STDLIBRARIES.ru.md).

**В работе / планы:**
- NuGet-пакеты для `Kernel.Common`, `Kernel.Contracts`, `Kernel.Diagnostics`, `Compiler`, `VmApplication`.
- Публикация бенчмарков в BenchmarkDotNet-совместимом формате.
- Написание JIT-компилятора на `C` и `C++`, но это будет отдельный проект.
- Миграция `Libraries.alloc_minic` и `LibCode.snake_minic` с глобалов на локалы (баг кодогенератора исправлен, workaround больше не обязателен; см. [STDLIBRARIES.ru.md](Nantuc.VmApplication.Extensions/STDLIBRARIES.ru.md)).
- **Отладчик:** frame pointer в `Processor` для точного `GetCallStack` (сейчас — best-effort эвристика по `[rSP, RamSize)`), `Debugger.Reset()`/`Reload()` для повторного использования одного экземпляра, UI-панель для IDE (через `Stopped`-событие).
- **Документация:** полный перевод всей документации проекта на английский
## Вклад

Проект открыт для issues и pull request'ов.

- **Нашли баг** — issue с минимальным примером (MiniC/NANASM + ожидаемое/фактическое поведение).
- **Хотите фичу** — issue с описанием сценария (что делаете, что мешает).
- **Правка** — fork → branch → PR. Для крупных изменений сначала issue (обсудить дизайн).
- **Стиль**: следуйте стилю соседних файлов; `Nullable=enable`, `ImplicitUsings=enable`; `unsafe` — только в `Nantuc.Kernel`.

Перед PR:
```bash
dotnet build Nantuc.sln -c Release
dotnet test -c Release
```

## Лицензия

См. файл [`LICENSE`](LICENSE) в корне репозитория.

## Ссылки

- GitHub: [RomanyPe/Nantuc](https://github.com/RomanyPe/Nantuc)
- Документация по модулям — README в каждой папке (ссылки в разделе [Модули](#модули)).
- Спецификация байткода — [`docs/bytecode.ru.md`](docs/bytecode.ru.md).
- Справочник кодов ошибок — [`docs/error-codes.ru.md`](docs/error-codes.ru.md).
- Метрики кода — [`docs/METRICS.md`](docs/METRICS.md).
- Встроенные библиотеки MiniC/NANASM — [`Nantuc.VmApplication.Extensions/STDLIBRARIES.ru.md`](Nantuc.VmApplication.Extensions/STDLIBRARIES.md).
- Отладчик — [`Nantuc.VmApplication.Extensions/README.ru.md`](Nantuc.VmApplication.Extensions/README.ru.md#отладчик).
- Примеры — [`Nantuc.Samples.Minimal`](Nantuc.Samples.Minimal/README.ru.md), [`Nantuc.Samples.Scratch`](Nantuc.Samples.Scratch/README.ru.md).
- Бенчмарк — [`Nantuc.Benchmarks`](Nantuc.Benchmarks/README.ru.md).
