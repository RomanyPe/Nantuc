# Nantuc — README

> Платформа для выполнения байткода с собственным языком MiniC (почти полноценный C), ассемблером NANASM, компилятором, виртуальной машиной и публичным API для встраивания.
> **Версия проекта:** текущая версия проекта указана в `CHANGELOG.ru.md`, semver гарантирован.
> Написана на C# (.NET 10). Компилятор и рантайм разделены по слоям, unsafe-код изолирован в ядре, публичный API — тонкий фасад с безопасными `Try*`-методами и `IdState`-диагностикой.

Nantuc — это мини-фреймворк, в котором есть всё, что нужно для запуска программы на C-подобном языке:
лексер, парсер, AST-оптимизатор, кодогенератор в байткод, ассемблер (текстовый и IL),
виртуальная машина с регистрами, памятью и порт-шиной, дизассемблер, отладчик,
I/O-устройства и API для встраивания в приложение.
В репозитории есть вывод Hello world на MiniC, играбельная змейка на MiniC, тест аллокатора и два бенчмарка VM.

## Содержание
- [Ключевые особенности](#ключевые-особенности)
- [Безопасный API](#безопасный-api)
- [Быстрый старт](#быстрый-старт)
- [NuGet-пакеты](#nuget-пакеты)
- [Архитектура](#архитектура)
- [Структура репозитория](#структура-репозитория)
- [Модули](#модули)
- [Языки и ISA](#языки-и-isa)
- [Требования](#требования)
- [Сборка и запуск](#сборка-и-запуск)
- [Примеры](#примеры)
- [Метрики и качество](#метрики-и-качество)
- [Вклад](#вклад)
- [Лицензия](#лицензия)
- [Ссылки](#ссылки)

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
- **Два бенчмарка**: ручной `Nantuc.Benchmarks` (MIPS, стабильность, перцентили, CV, JIT-impact) и `Nantuc.Benchmarks.BenchmarkDotNet` (BenchmarkDotNet, аллокации, статистика, артефакты) на **14 MiniC-кейсах**: вызовы, ALU/ILP, память, битовые операции, умножение, ветвления, деление, рекурсия, указатели, структуры, сортировка.

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

- **`IdState`** (`Nantuc.Common`) — `Unknown` (0, default) / `Valid` (1) / `NotAssigned` (-1) / `OutOfRange` (-2) / `NoFreeSlot` (-3) / `Rejected` (-4). Возвращается из `ManagerDevices.TryAddDevice` (и из `Emulator.RegisterDevice`/`Emulator.TryRegisterDevice`). `default(IdState) == Unknown`, чтобы неинициализированный статус не воспринимался как успешный; **не полагайтесь на `default(IdState) == Valid`** — это изменилось.
- **`ProcessorFault`** (`Nantuc.Common`) — `(BiosStatus Status, ulong FaultData, ulong InstructionPointer, OpCode OpCode)`. На успехе `Status == EndProgramm`; на падении — `DivOnZero`/`SegmentationFault`/… с точным `IP` и `OpCode`.
- **`IProcessorFaultPolicy`** — используется **внутри** VM для решения «продолжать ли». Возврат из `Run`/`DebugRun` — это уже `ProcessorFault`, отдельная политика не нужна.
- **`Debugger`** (`Nantuc.VmApplication.Extensions.Debugging`) — не добавляет новых контрактов в `Kernel`/`VmApplication`, работает через `DeviceContext.RunStepMode`/`SingleStep`/`GetRegistersSnapshot`/`NativeReadOnlyBuffer`. Поставляется с `DebugStop`, `DebugStopReason`, `CallStackFrame`.

## Быстрый старт

Минимальный пример — компиляция и запуск `Hello world` на MiniC с выводом через UART, с безопасной проверкой каждого шага:

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
## NuGet-пакеты

Все модули платформы опубликованы как NuGet-пакеты (GitHub Packages, автор — `RomanyPe`). Ставится только то, что нужно сценарию; транзитивные зависимости подтягиваются автоматически.

| Пакет | Кому | Что внутри |
|---|---|---|
| `Nantuc.Common` | Авторы компилятора, расширений, дизассемблера | Опкоды, регистры, размеры RAM/портов, `BiosStatus`, `ProcessorFault`, `IdState` (default-safe), декодер инструкций |
| `Nantuc.Contracts` | Авторы устройств и расширений | `IPortBus`, `IPortUse`, `IPortController`, `ProcessorSnapshot`, `IProcessorFaultPolicy`, `OptimizationId` |
| `Nantuc.Diagnostics` | Авторы компилятора, IDE, CI | `ErrorCode`, `DiagnosticData` (AOT-safe), `IErrorMessageProvider`, `ThrowCompiler`, `CodeException` |
| `Nantuc.Kernel` | Встраивающий код, автор отладчика | `Processor`, `MemoryBus`, `PortBus`, `ManagerDevices`, `Emulator`, `Device` (`Run`/`DebugRun` → `ProcessorFault`) |
| `Nantuc.Compiler` | Автор IDE, автор правил оптимизации | `Lexer`, `Parser`, AST, кодогенератор, `Assembler`/`ILAssembler`, peephole/AST-оптимизации, `const`-correctness |
| `Nantuc.VmApplication` | Конечный пользователь платформы | `VmHost`, `DeviceContext`, `VmEmulator` (`TryRegisterDevice`/`TryGetDeviceContext`), `VmHostProject`, `DeviceSpec` |
| `Nantuc.VmApplication.Extensions` | Автор IDE, автор игр, автор своих оптимизаций | Дизассемблер, `Debugger` (шаг, точки останова, стек, RAM), `SynchronousIOStream`/`QueuedIOStream`, `AddStdRules`, `Libraries`, `RunAndKeep`/`RunOnce` |

**Зависимости (снизу вверх):**
`Common` → `Contracts` → `Diagnostics` → `Kernel` → `Compiler` → `VmApplication` → `VmApplication.Extensions`.
`Contracts` зависит только от `Common`; `Compiler` — от `Common`+`Contracts`+`Diagnostics`; `VmApplication` — единственный модуль, который одновременно знает про компилятор и рантайм.

**Что ставить под сценарий:**

- **Запустить MiniC-программу** — достаточно `Nantuc.VmApplication` + `Nantuc.VmApplication.Extensions` (остальное подтянется транзитивно).
- **Своё устройство (UART, монитор, клавиатура)** — `Nantuc.Contracts` (реализовать `IPortController`) + `Nantuc.Kernel` (если нужен `Emulator`/`PortBus`), но лучше `Nantuc.VmApplication` + `Nantuc.VmApplication.Extensions`.
- **Своя IDE / отладчик** — `Nantuc.VmApplication` + `Nantuc.VmApplication.Extensions` (`Debugger`, `Disassembler`).
- **Свой компилятор/ассемблер без рантайма** — `Nantuc.Common` + `Nantuc.Diagnostics` (никакого `unsafe`/`NativeMemory`).
- **Своё правило оптимизации** — `Nantuc.Compiler` + `Nantuc.Contracts` (`IAstOptimizationRule`/`IPeepholeRule`, `OptimizationId`).

> `Nantuc.Common`, `Nantuc.Contracts`, `Nantuc.Diagnostics` не тянут `unsafe`/`NativeMemory`/`Regex` — их можно подключать в WASM/AOT-проекты и в окружения с запретом `unsafe`.

## Архитектура

Слои идут снизу вверх. Каждый следующий зависит только от предыдущих, обратных зависимостей нет.

```
┌──────────────────────────────────────────────────────────────────────────────────┐
│  Nantuc.Samples.Minimal   Nantuc.Samples.Scratch                                 │
│  Nantuc.Benchmarks        Nantuc.Benchmarks.BenchmarkDotNet                      │
│  (примеры, конечные продукты и бенчмарки)                                        │
└──────────────────────────────────────────────────────────────────────────────────┘
                              │
┌──────────────────────────────────────────────────────────────────────────────────┐
│  Nantuc.VmApplication.Extensions                                                 │
│  (дизассемблер, отладчик, I/O, стандартные оптимизации,                          │
│   библиотеки, утилиты компиляции и запуска)                                      │
└──────────────────────────────────────────────────────────────────────────────────┘
                              │
┌──────────────────────────────────────────────────────────────────────────────────┐
│  Nantuc.VmApplication                                                            │
│ (VmHost, DeviceContext, VmEmulator, VmHostProject, CompilationResult)            │
│  TryRegisterDevice / TryGetDeviceContext / Run → ProcessorFault                  │
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

Обратите внимание:
- **`Diagnostics` не зависит ни от одного проекта Nantuc.** Автор компилятора не тянет unsafe-ядро ради кодов ошибок.
- **Компилятор не зависит от рантайма.** `Nantuc.Compiler` знает только про `Common` (ISA), `Contracts` (типы оптимизаций) и `Diagnostics` (ошибки). Ни `Processor`, ни `MemoryBus` он не видит.
- **`IdState` живёт в `Common`**, а не в `VmApplication`: `Kernel` (где `ManagerDevices`) не может зависеть от `VmApplication`, а `Common` — единственный слой, видимый и `Kernel`, и `VmApplication`, и `Extensions`.
- **VMApplication — единственный модуль, который одновременно знает про компилятор и рантайм.** Он склеивает «скомпилировать» и «запустить».
- **Отладчик живёт в `VmApplication.Extensions`**, а не в `VmApplication`/`Kernel`: он использует только публичный API (`RunStepMode`/`SingleStep`/`GetRegistersSnapshot`/`NativeReadOnlyBuffer`) и не требует `AsRawDevice`/`unsafe`. Это сохраняет разделение «ядро ничего не знает про UI отладчика».
- **Бенчмарки живут на самом верхнем слое.** Ручной `Nantuc.Benchmarks` и `Nantuc.Benchmarks.BenchmarkDotNet` используют публичный API `VmApplication`/`Extensions` и не трогают `Kernel` напрямую.

## Структура репозитория

```
Nantuc/
├── Nantuc.Common/                     # ISA, размеры, BiosStatus, ProcessorFault, IdState
├── Nantuc.Contracts/                  # публичные контракты (порты, процессор)
├── Nantuc.Diagnostics/                # ErrorCode, DiagnosticData, ThrowCompiler, CodeException
├── Nantuc.Kernel/                     # реализация CPU, RAM, PortBus, Device
├── Nantuc.Compiler/                   # MiniC + NANASM (frontend, backend, ASM, оптимизации)
├── Nantuc.VmApplication/              # основной API (TryRegisterDevice, TryGetDeviceContext, Run → ProcessorFault)
├── Nantuc.VmApplication.Extensions/   # дизассемблер, отладчик, I/O, оптимизации, библиотеки
├── Nantuc.Samples.Minimal/            # Hello world
├── Nantuc.Samples.Scratch/            # тест аллокатора + змейка
├── Nantuc.Benchmarks/                 # ручной бенчмарк VM (14 MiniC-кейсов, MIPS/статистика)
└── Nantuc.Benchmarks.BenchmarkDotNet/ # BenchmarkDotNet-бенчмарк (14 MiniC-кейсов, артефакты)
```

## Модули

| Модуль | Назначение | Кому |
|---|---|---|
| [`Nantuc.Common`](Nantuc.Common/README.ru.md) | Опкоды, регистры, размеры RAM/портов, `BiosStatus`, `ProcessorFault`, `IdState` (`Unknown` = 0, default-safe), декодер инструкций | Авторы компилятора, расширений, дизассемблера |
| [`Nantuc.Contracts`](Nantuc.Contracts/README.ru.md) | `IPortBus`, `IPortUse`, `IPortController`, `ProcessorSnapshot`, `IProcessorFaultPolicy`, `OptimizationId` | Авторы устройств и расширений |
| [`Nantuc.Diagnostics`](Nantuc.Diagnostics/README.ru.md) | `ErrorCode`, `DiagnosticData`, `IErrorMessageProvider`, `ThrowCompiler`, `CodeException` | Авторы компилятора, IDE, CI |
| [`Nantuc.Kernel`](Nantuc.Kernel/README.ru.md) | `Processor`, `MemoryBus`, `PortBus`, `ManagerDevices` (`TryAddDevice` → `IdState`), `Emulator`, `Device` (`Run`/`DebugRun` → `ProcessorFault`) | Встраивающий код, автор отладчика |
| [`Nantuc.Compiler`](Nantuc.Compiler/README.ru.md) | `Lexer`, `Parser`, AST, `FunctionGenerator`, `Assembler`, `ILAssembler`, `AssemblerParser`, `PeepholeOptimizer`, `AstOptimizer`, `const`-correctness | Автор IDE, автор правил оптимизации |
| [`Nantuc.VmApplication`](Nantuc.VmApplication/README.ru.md) | `VmHost`, `DeviceContext`, `VmEmulator` (`TryRegisterDevice`/`TryGetDeviceContext`), `VmHostProject`, `CompilationResult`, `DeviceSpec` | Конечный пользователь платформы |
| [`Nantuc.VmApplication.Extensions`](Nantuc.VmApplication.Extensions/README.ru.md) | Дизассемблер, `Debugger` (шаг, точки останова, стек, RAM), `SynchronousIOStream`/`QueuedIOStream`, `AddStdRules`, `Libraries`, `RunAndKeep`/`RunOnce` | Автор IDE, автор игр, автор своих оптимизаций |
| [`Nantuc.Samples.Minimal`](Nantuc.Samples.Minimal/README.ru.md) | Минимальный Hello world | Новый пользователь |
| [`Nantuc.Samples.Scratch`](Nantuc.Samples.Scratch/README.ru.md) | Тест аллокатора + змейка с монитором | Пример игрового цикла и I/O |
| [`Nantuc.Benchmarks`](Nantuc.Benchmarks/README.ru.md) | Ручной бенчмарк 14 MiniC-кейсов: MIPS, стабильность, перцентили, CV, JIT-impact | Автор ядра/компилятора |
| [`Nantuc.Benchmarks.BenchmarkDotNet`](Nantuc.Benchmarks.BenchmarkDotNet/README.ru.md) | BenchmarkDotNet-бенчмарк 14 MiniC-кейсов: время, аллокации, статистика, артефакты | Автор ядра/компилятора |

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
- Полная спецификация — в [`Nantuc.Common`](Nantuc.Common/README.ru.md) и [`docs/bytecode.ru.md`](docs/bytecode.ru.md).

## Требования

- **.NET 10 SDK**. Все проекты таргетят `net10.0`.
- **C# 14**: `extension`-блоки, `[InlineArray]`, `params ReadOnlySpan`, `field`-keyword, `FrozenDictionary`/`FrozenSet`, primary constructors.
- **Windows / Linux / macOS / AoT** (другие платформы не тестировались, но Nantuc не привязан к конкретной платформе). В WASM есть ограничения по модулям:
  - `Common` / `Contracts` / `Diagnostics` — полностью WASM-safe (managed, без `unsafe`, без рефлексии). `DiagnosticData` и `TokenTypeExtension.AsText` специально сделаны AOT-safe.
  - `Nantuc.Compiler` — `[GeneratedRegex]` в `AssemblerParser.GetRexExtensionAsm()` AOT-safe; `MiniCLanguageDefinition.GetKeywordRegex()` — dead code с `RegexOptions.Compiled`, может быть AOT-blocker на browser-wasm. Для строгого AOT/WASM этот путь нужно убрать или заменить.
  - `Nantuc.Kernel` — `unsafe` и `NativeMemory.AllocZeroed`/`Free`/`Clear` поддерживаются WASM (линейная память emscripten), но:
    - `Device.Run`/`DebugRun` синхронные и блокируют текущий поток (`ManualResetEventSlim.Wait(15)` при `IsSleeping`, `Thread.Sleep(delay)` в `DebugRun`) — в single-threaded browser это блокировка UI; нужен Web Worker или async-цикл.
    - `NativeMemory.AllocZeroed` для больших `RamSize` (≥ `MB512`, тем более `GB1`/`GB2`) рискует `OutOfMemoryException` из-за лимита линейной памяти WASM (~4 GiB на процесс, с фрагментацией). Практический потолок в браузере — ~256 MiB.
  - `Nantuc.VmApplication.Extensions` — `Debugger` работает, но `Continue`/`StepOver`/`StepOut`/`GetCallStack` — синхронные циклы, блокируют текущий поток; для browser-WASM — Web Worker или `Task.Run` с `RequestStop`. `QueuedIOStream` в WASM требует явного `Dispose`/`await`, иначе фоновый drain может не завершиться.
  - `Nantuc.Benchmarks` — `Process`/P/Invoke, вне WASM; ручной бенчмарк также блокируется на `Console.ReadLine()` в конце `Main`, поэтому не подходит для CI/headless.
  - `Nantuc.Benchmarks.BenchmarkDotNet` — desktop/CI-инструмент, не для browser-WASM. В CI используйте `--filter` и ограничение времени, иначе полный прогон 14 кейсов может занять десятки минут.

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
dotnet run --project Nantuc.Samples.Scratch -c Release -snake 150
```

Или через профиль IDE `Game` из `launchSettings.json`. Управление: стрелки — движение, `Q`/`Escape` — выход.

**Запустить тест аллокатора** (по умолчанию в `Nantuc.Samples.Scratch`):

```bash
dotnet run --project Nantuc.Samples.Scratch -c Release
```

**Запустить ручной бенчмарк** (Linux: для `PriorityClass.High` нужен `sudo`):

```bash
dotnet run --project Nantuc.Benchmarks -c Release
```

Результаты сохраняются в `Nantuc.Benchmarks/bin/.../logs/result_Benchmark_Logs_{timestamp}.txt`.

**Запустить BenchmarkDotNet-бенчмарк:**

```bash
# Полный прогон (может быть долгим)
dotnet run --project Nantuc.Benchmarks.BenchmarkDotNet -c Release

# Только выбранные кейсы
dotnet run --project Nantuc.Benchmarks.BenchmarkDotNet -c Release -- --filter *Call* *Memory*
```

Артефакты сохраняются в `BenchmarkDotNet.Artifacts/`: Markdown/CSV/HTML-отчёты и логи. Для точных цифр замените `[ShortRunJob]` на `[MediumRunJob]`/`[LongRunJob]`.

## Примеры

### Hello world (безопасный путь)

См. [`Nantuc.Samples.Minimal/README.ru.md`](Nantuc.Samples.Minimal/README.ru.md) — полный разбор с `TryRegisterDevice` + `TryGetDeviceContext` + `ProcessorFault`, объяснение портов `0x10`/`0x12` и `QueuedIOStream`.

### Змейка

См. [`Nantuc.Samples.Scratch/README.ru.md`](Nantuc.Samples.Scratch/README.ru.md) — `SnakeGameOnNantuc`, `MonochromeMonitor` (32×16), HALT/WAKE-цикл, тикер, ANSI-рендер. Запуск через `dotnet run -- -snake 150`.

### Тест аллокатора

См. [`Nantuc.Samples.Scratch/README.ru.md`](Nantuc.Samples.Scratch/README.ru.md), `Program.cs` — `Libraries.alloc_minic` (first-fit + split + coalescing), проверка `return 10`, дамп регистров и дизассемблер RAM.

### Полный цикл с диагностикой

```csharp
using Nantuc.Common;
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
using Nantuc.Common;
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

- ~21 000 строк, 12 проектов
- ~17 600 строк, 7 проектов (ядро и API, без двух примеров, тестов и двух бенчмарков)
- Индекс поддерживаемости: 80–97 (основные модули)
- Полная таблица метрик: [docs/METRICS.md](docs/METRICS.md)
> **Статус по модулям** — в README каждого модуля (раздел «Что исправлено в этой версии» / «Приближенность модуля к пользователю»). **История изменений** — в [`CHANGELOG.ru.md`](CHANGELOG.ru.md). **Планы** — ниже.

**В работе / планы:**
- Написание JIT-компилятора на `C` и `C++`, но это будет отдельный проект.
- Миграция `Libraries.alloc_minic` и `LibCode.snake_minic` с глобалов на локалы (баг кодогенератора исправлен, workaround больше не обязателен; см. [STDLIBRARIES.ru.md](Nantuc.VmApplication.Extensions/STDLIBRARIES.ru.md)).
- **Отладчик:** frame pointer в `Processor` для точного `GetCallStack` (сейчас — best-effort эвристика по `[rSP, RamSize)`), `Debugger.Reset()`/`Reload()` для повторного использования одного экземпляра, UI-панель для IDE (через `Stopped`-событие).

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
- Встроенные библиотеки MiniC/NANASM — [`Nantuc.VmApplication.Extensions/STDLIBRARIES.ru.md`](Nantuc.VmApplication.Extensions/STDLIBRARIES.ru.md).
- Отладчик — [`Nantuc.VmApplication.Extensions/README.ru.md`](Nantuc.VmApplication.Extensions/README.ru.md#отладчик).
- Примеры — [`Nantuc.Samples.Minimal`](Nantuc.Samples.Minimal/README.ru.md), [`Nantuc.Samples.Scratch`](Nantuc.Samples.Scratch/README.ru.md).
- Бенчмарки — [`Nantuc.Benchmarks`](Nantuc.Benchmarks/README.ru.md), [`Nantuc.Benchmarks.BenchmarkDotNet`](Nantuc.Benchmarks.BenchmarkDotNet/README.ru.md).