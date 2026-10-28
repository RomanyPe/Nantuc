# Nantuc — README

> Платформа для выполнения байткода с собственным языком MiniC (почти полноценный C), ассемблером NANASM, компилятором, виртуальной машиной и публичным API для встраивания.  

> Написана на C# (.NET 10). Компилятор и рантайм разделены по слоям, unsafe-код изолирован в ядре, публичный API — тонкий фасад с безопасными `Try*`-методами и `IdState`-диагностикой.

Nantuc — это мини-фреймворк, в котором есть всё, что нужно для запуска программы на C-подобном языке: лексер, парсер, AST-оптимизатор, кодогенератор в байткод, ассемблер (текстовый и IL), виртуальная машина с регистрами, памятью и порт-шиной, дизассемблер, I/O-устройства и API для встраивания в приложение. Проект вырос до 12 тысяч строк, индекс поддерживаемости по метрикам Visual Studio — 80–96 из 100. В репозитории есть играбельная змейка на MiniC, тест аллокатора и бенчмарк VM.

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

- **Два языка**: MiniC (C-подобный: структуры, указатели, массивы, `sizeof`, inline `asm`, `extern`, `#include`) и NANASM (ассемблер с метками и размерными суффиксами).
- **Компилятор с нуля**: лексер, рекурсивный парсер, AST, жадная раскраска регистров, кодогенерация в 32-битный байткод, AST- и peephole-оптимизации.
- **VM с изолированным `unsafe`**: нативная RAM через `NativeMemory`, регистры в `InlineArray` (без аллокаций на шаг), порт-шина с маршрутизацией `address → (device, offset)`.
- **Безопасный API**: `TryRegisterDevice`/`TryGetDeviceContext` вместо `RegisterDevice(id < 0)` + `!`, `IdState` вместо магических `-1..-4`, `ProcessorFault` из `Run`/`DebugRun` вместо ручного `IProcessorFaultPolicy`.
- **Устройства как first-class**: VM сама является устройством (`IPortController`) — можно вкладывать VM друг в друга, подключать UART, мониторы, клавиатуры.
- **Публичный API без хаков**: `VmHost` / `DeviceContext` / `VmHostProject` — весь unsafe и нативные буферы спрятаны в `Nantuc.Kernel`.
- **Дизассемблер** с секциями кода и данных, текстовый и структурный.
- **Стандартные оптимизации** (AST и peephole) и инфраструктура для своих правил.
- **Бенчмарк** с 12 кейсами: память (safe/unsafe), ALU (ILP и без), вызовы, стек, битовые операции, умножение, ветвления. MIPS, перцентили, CV, JIT-impact.

## Безопасный API

Публичный API `Nantuc.VmApplication` следует паттерну «Try + IdState + ProcessorFault»:

| Сценарий | Старый (raw) путь | Безопасный путь |
|---|---|---|
| Регистрация устройства | `int id = RegisterDevice(...)` + проверка `id < 0` | `TryRegisterDevice(..., out int id)` → `IdState` |
| Получение `DeviceContext` | `GetDeviceContext(id)!` | `TryGetDeviceContext(id, out ctx)` |
| Загрузка программы | `LoadProgram(...)` (бросает) | `TryFastLoadProgram(..., out error)` |
| Запуск VM | `Run()` → `void` | `Run(opt)` → `ProcessorFault` |
| Остановка по лимиту | `RunWithLimit(...)` | `Run(new LaunchOptions(StepLimit: N))` |

- **`IdState`** (`Nantuc.Kernel.Common`) — `Unknown` (0, default) / `Valid` (1) / `NotAssigned` (-1) / `OutOfRange` (-2) / `NoFreeSlot` (-3) / `Rejected` (-4). Возвращается из `ManagerDevices.TryAddDevice` (и из `Emulator.RegisterDevice`/`Emulator.TryRegisterDevice`). `default(IdState) == Unknown`, чтобы неинициализированный статус не воспринимался как успешный; **не полагайтесь на `default(IdState) == Valid`** — это изменилось.
- **`ProcessorFault`** (`Nantuc.Kernel.Common`) — `(BiosStatus Status, ulong FaultData, ulong InstructionPointer, OpCode OpCode)`. На успехе `Status == EndProgramm`; на падении — `DivOnZero`/`SegmentationFault`/… с точным `IP` и `OpCode`.
- **`IProcessorFaultPolicy`** — используется **внутри** VM для решения «продолжать ли». Возврат из `Run`/`DebugRun` — это уже `ProcessorFault`, отдельная политика не нужна.

## Быстрый старт

Минимальный пример — компиляция и запуск `Hello world` на MiniC с выводом через UART, с безопасной проверкой каждого шага:

```csharp
using Nantuc.Kernel.Common;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Extensions.Emulator.IO;
using Nantuc.VmApplication.Extensions.Host;
using Nantuc.VmApplication.Extensions.LibraresIncludes;

const string source =
    """
    #include "print.minic"
    char str[] = "Hello world";
    void main() { print16(str); }
    """;

using var host = VmHostFactory.CreateDefault();
host.Project.SaveFileProject("main.minic", source);
host.Project.SaveFileInclude("io.nanasm", Librare.io_nanasm);
host.Project.SaveFileInclude("print.minic", Librare.print_minic);

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

// 5) Загрузка — через TryFastLoadProgram
if (!vm.TryFastLoadProgram(compilation.Program!, loadAddress: 0, out var error))
{
    Console.Error.WriteLine(error);
    return;
}

// 6) Запуск — Run возвращает ProcessorFault, статус можно проверить
ProcessorFault fault = vm.Run(new LaunchOptions(ShowTimer: true));
if (fault.Status != BiosStatus.EndProgramm)
    Console.Error.WriteLine($"VM fault: {fault.Status} at IP=0x{fault.InstructionPointer:X}");
```

Подробный разбор — в [`Nantuc.Samples.Minimal`](Nantuc.Samples.Minimal/README.md).

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
│  (дизассемблер, I/O, стандартные оптимизации, библиотеки, утилиты)   │
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
│  (IPortBus, IPortUse, IPortController, IProcessorReader, ...)        │
└──────────────────────────────────────────────────────────────────────┘
              │                                │
┌──────────────────────────────────────────────────────────────────────┐
│  Nantuc.Kernel.Common            Nantuc.Kernel.Diagnostics           │
│  (ISA, размеры, BiosStatus,      (ErrorCode, ThrowCompiler)          │
│   ProcessorFault, IdState)                                           │
└──────────────────────────────────────────────────────────────────────┘
```

Обратите внимание:
- **`Kernel.Diagnostics` не зависит ни от одного проекта Nantuc.** Автор компилятора не тянет unsafe-ядро ради кодов ошибок.
- **Компилятор не зависит от рантайма.** `Nantuc.Compiler` знает только про `Kernel.Common` (ISA) и `Kernel.Diagnostics` (ошибки).
- **`IdState` живёт в `Kernel.Common`**, а не в `VmApplication`: `Kernel` (где `ManagerDevices`) не может зависеть от `VmApplication`, а `Common` — единственный слой, видимый и `Kernel`, и `VmApplication`, и `Extensions`.
- **VMApplication — единственный модуль, который одновременно знает про компилятор и рантайм.** Он склеивает «скомпилировать» и «запустить».

## Структура репозитория

```
Nantuc/
├── Nantuc.Kernel.Common/              # ISA, размеры, BiosStatus, ProcessorFault, IdState
├── Nantuc.Kernel.Contracts/           # публичные контракты (порты, процессор)
├── Nantuc.Kernel.Diagnostics/         # ErrorCode, ThrowCompiler, CodeException
├── Nantuc.Kernel/                     # реализация CPU, RAM, PortBus, Device
├── Nantuc.Compiler/                   # MiniC + NANASM (frontend, backend, ASM, оптимизации)
├── Nantuc.VmApplication/              # основной API (TryRegisterDevice, TryGetDeviceContext, Run → ProcessorFault)
├── Nantuc.VmApplication.Extensions/   # дизассемблер, I/O, оптимизации, библиотеки
├── Nantuc.Samples.Minimal/            # Hello world
├── Nantuc.Samples.Scratch/            # тест аллокатора + змейка
└── Nantuc.Benchmarks/                 # бенчмарк VM
```

## Модули

| Модуль | Назначение | Кому |
|---|---|---|
| [`Nantuc.Kernel.Common`](Nantuc.Kernel.Common/README.md) | Опкоды, регистры, размеры RAM/портов, `BiosStatus`, `ProcessorFault`, `IdState` (`Unknown` = 0, default-safe), декодер инструкций | Авторы компилятора, расширений, дизассемблера |
| [`Nantuc.Kernel.Contracts`](Nantuc.Kernel.Contracts/README.md) | `IPortBus`, `IPortUse`, `IPortController`, `IProcessorReader`, `IProcessorFaultPolicy`, `OptimizationId` | Авторы устройств и расширений |
| [`Nantuc.Kernel.Diagnostics`](Nantuc.Kernel.Diagnostics/README.md) | `ErrorCode`, `IErrorMessageProvider`, `ThrowCompiler`, `CodeException` | Авторы компилятора, IDE, CI |
| [`Nantuc.Kernel`](Nantuc.Kernel/README.md) | `Processor`, `MemoryBus`, `PortBus`, `ManagerDevices` (`TryAddDevice` → `IdState`), `Emulator`, `Device` (`Run`/`DebugRun` → `ProcessorFault`) | Встраивающий код, автор отладчика |
| [`Nantuc.Compiler`](Nantuc.Compiler/README.md) | `Lexer`, `Parser`, AST, `FunctionGenerator`, `Assembler`, `ILAssembler`, `AssemblerParser`, `PeepholeOptimizer`, `AstOptimizer` | Автор IDE, автор правил оптимизации |
| [`Nantuc.VmApplication`](Nantuc.VmApplication/README.md) | `VmHost`, `DeviceContext`, `VmEmulator` (`TryRegisterDevice`/`TryGetDeviceContext`), `VmHostProject`, `CompilationResult`, `DeviceSpec` | Конечный пользователь платформы |
| [`Nantuc.VmApplication.Extensions`](Nantuc.VmApplication.Extensions/README.md) | Дизассемблер, `SynchronousIOStream`/`QueuedIOStream`, `AddStdRules`, `Librare`, `RunAndKeep`/`RunOnce` | Автор IDE, автор игр, автор своих оптимизаций |
| [`Nantuc.Samples.Minimal`](Nantuc.Samples.Minimal/README.md) | Минимальный Hello world | Новый пользователь |
| [`Nantuc.Samples.Scratch`](Nantuc.Samples.Scratch/README.md) | Тест аллокатора + змейка с монитором и клавиатурой | Пример игрового цикла и I/O |
| [`Nantuc.Benchmarks`](Nantuc.Benchmarks/README.md) | Бенчмарк 12 кейсов с MIPS и статистикой | Автор ядра/компилятора |

## Языки и ISA

### MiniC

C-подобный язык. Поддерживает:

- Типы: `int`, `char`, `byte`, `ushort`, `short`, `ulong`, `long`, `uint`, `void`, `struct`.
- Указатели (`*`, `&`, `->`), массивы (включая инициализаторы `{...}` и строковые литералы для `char[]`).
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

Поддерживаются `MOV`, `ADD`, `SUB`, `MULT_INT`, `DIV`, `MOD`, `SHR`, `SHL`, `AND`, `OR`, `XOR`, `NOT`, `INC`, `DEC`, `CMP`, `TEST`, `JMP`/`JZ`/`JNZ`/`JG`/`JL`, `CALL`/`RET`, `PUSH`/`POP`, `LOAD`/`STORE` и `*_IND`-варианты, `IN`/`OUT`, `HALT`/`WAKE`/`WAKE_INT`, `NOP`, `END`.

### ISA (кратко)

32-битная инструкция, little-endian:

```
бит:  31…20    | 19…18     | 17…13  | 12…8   | 7…0
      spare    | OpCodeSize| Reg2   | Reg1   | OpCode
```

- 8 бит на опкод, 5 бит на регистр (32 регистра: `r0..r27`, `rFL`, `rSP`, `rHP`, `rIP`), 2 бита на размер (`S8`/`S16`/`S32`/`S64`).
- Опкоды с immediate дополняются 8-байтным операндом с выравниванием до 8.
- Полная спецификация — в [`Nantuc.Kernel.Common`](Nantuc.Kernel.Common/README.md).

## Требования

- **.NET 10 SDK**. Все проекты таргетят `net10.0`.
- **C# 14**: `extension`-блоки, `[InlineArray]`, `params ReadOnlySpan`, `field`-keyword, `FrozenDictionary`/`FrozenSet`, primary constructors.
- **Windows / Linux / macOS**. В WASM есть ограничения по модулям:
  - `Kernel.Common` / `Kernel.Contracts` / `Kernel.Diagnostics` — полностью WASM-safe (managed, без `unsafe`, без `RegexOptions.Compiled`).
  - `Nantuc.Compiler` — собирается, но `MiniCLanguageDefinition._keywordRegex` использует `RegexOptions.Compiled` — **browser-wasm AOT-blocker** (`PlatformNotSupportedException`). Без этой строки (или с заменой на `[GeneratedRegex]`) компилятор работает в AOT.
  - `Nantuc.Kernel` — `unsafe` и `NativeMemory.AllocZeroed`/`Free`/`Clear` поддерживаются WASM (линейная память emscripten), но:
    - `Device.Run`/`DebugRun` синхронные и блокируют текущий поток (`ManualResetEventSlim.Wait(15)` при `IsSleeping`, `Thread.Sleep(delay)` в `DebugRun`) — в single-threaded browser это блокировка UI; нужен Web Worker или async-цикл.
    - `NativeMemory.AllocZeroed` для больших `RamSize` (≥ `MB512`, тем более `GB1`/`GB2`) рискует `OutOfMemoryException` из-за лимита линейной памяти WASM (~4 GiB на процесс, с фрагментацией). Практический потолок в браузере — ~256 MiB.
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

**Запустить змейку** (заменив `Program.cs` на вызов `SnakeLauncher.LaunchSnake(period: 200)`):

```bash
dotnet run --project Nantuc.Samples.Scratch -c Release
```

Управление: стрелки — движение, `Q`/`Escape` — выход.

**Запустить бенчмарк** (Linux: для `PriorityClass.High` нужен `sudo`):

```bash
dotnet run --project Nantuc.Benchmarks -c Release
```

Результаты сохраняются в `Nantuc.Benchmarks/bin/.../logs/result_Benchmark_Logs_{timestamp}.txt`.

## Примеры

### Hello world (безопасный путь)

См. [`Nantuc.Samples.Minimal/README.md`](Nantuc.Samples.Minimal/README.md) — полный разбор с `TryRegisterDevice` + `TryGetDeviceContext` + `ProcessorFault`, объяснение портов `0x10`/`0x12` и `QueuedIOStream`.

### Змейка

См. [`Nantuc.Samples.Scratch/README.md`](Nantuc.Samples.Scratch/README.md) — `SnakeLauncher`, `MonochromeMonitor` (32×16), `KeyboardDevice`, HALT/WAKE-цикл, тикер, ANSI-рендер.

### Тест аллокатора

См. [`Nantuc.Samples.Scratch/README.md`](Nantuc.Samples.Scratch/README.md), `Program.cs` — `Librare.alloc_minic` (first-fit + split + coalescing), проверка `return 10`, дамп регистров и дизассемблер RAM.

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
if (!vm.TryFastLoadProgram(comp.Program!, 0, out var error)) { Console.Error.WriteLine(error); return; }

ProcessorFault fault = vm.Run(new LaunchOptions(ShowTimer: true,
    OnEnd: r => Console.WriteLine($"steps={r?.Steps} elapsed={r?.Elapsed}")));

if (fault.Status != BiosStatus.EndProgramm)
    Console.Error.WriteLine($"fault {fault.Status} at IP=0x{fault.InstructionPointer:X} op={fault.OpCode}");
```

### Своя оптимизация

```csharp
using Nantuc.VmApplication.Extensions.Optimizators;

host.Project.Optimizator.VMAstOptimizer.AddStdRules();
host.Project.Optimizator.VMPeepholeOptimizer.AddStdRules();
host.Project.Optimizator.VMAstOptimizer.Add(new MyCustomRule());
```

## Метрики и качество

- **Размер кодовой базы**: около 12 000 строк.
- **Индекс поддерживаемости (Visual Studio Code Metrics)**: 80–96 из 100 по основным модулям.
- **Модульность**: 10 проектов с чёткими зависимостями, `unsafe` изолирован в `Nantuc.Kernel`, диагностика отделена в `Nantuc.Kernel.Diagnostics`.
- **Публичный API**: `Nantuc.VmApplication` — единственный модуль, который нужен конечному пользователю. Остальное — опциональные расширения.
- **Безопасность API**: `Try*`-методы вместо `null`/магических кодов, `IdState` для классификации возвратов (`default(IdState) == Unknown`, не `Valid`), `ProcessorFault` для статуса исполнения.
- **Бенчмарк**: 12 кейсов, MIPS, перцентили, CV, trimmed mean, JIT-impact. Формат отчёта — в [`Nantuc.Benchmarks/README.md`](Nantuc.Benchmarks/README.md).

## Статус и планы

**Готово:**
- MiniC (лексер, парсер, AST, кодогенерация, структуры, указатели, массивы, inline asm, `#include`, `extern`).
- NANASM (текстовый ассемблер, размерные суффиксы, метки).
- VM (регистры, RAM, стек, heap, порты, HALT/WAKE, внешние команды, fault-policy).
- Устройства: UART (sync/async/numeric), монитор (32×16), клавиатура.
- Дизассемблер (текстовый и структурный, `Disassembler` теперь public).
- Стандартные AST- и peephole-оптимизации.
- Безопасный публичный API: `TryRegisterDevice`, `TryGetDeviceContext`, `Run`/`DebugRun` → `ProcessorFault`, `IdState` (`default(IdState) == Unknown`, не `Valid`).
- Публичный API (`VmHost`, `DeviceContext`, `VmHostProject`, `DeviceSpec`).
- Примеры: Hello world, змейка, тест аллокатора.
- Бенчмарк.

**В работе / планы:**
- Полная спецификация байткода (`docs/bytecode.md`).
- Отладчик поверх `IProcessorReader` (точки останова, шаг, инспекция стека).
> Коментарий от разработчика: \
Готовой реализации нет, но имеется возможность получать информацию об стеке из более сырых данных, 
а именно `DeviceContext.GetRegisterSnapshot()` и `DeviceContext.NativeMemoryBuffer`, 
последний позволяет полностью прочитать всю ОЗУ, 
а с учетом расширений которые создают новый массив байт из этой озу 
и метода `CopyRamTo(Span<byte> destination, nuint start)` можно скопировать сразу в готовый буфер

- **WASM/AOT-фиксы**: убрать `RegexOptions.Compiled` из `MiniCLanguageDefinition` (browser-wasm AOT-blocker) или заменить на `[GeneratedRegex]`; вынести синхронные `Run`/`DebugRun` в async-цикл / Web Worker для browser-WASM.
- Sandbox для недоверенного кода (лимиты памяти/времени/стека, ограничение `OUT`).
- Тесты: юнит на лексер/парсер/кодогенерацию, golden-тесты на исполнение, fuzz парсера.
- CI (GitHub Actions): сборка, тесты, бенчмарк на Windows/Linux.
- NuGet-пакеты для `Kernel.Common`, `Kernel.Contracts`, `Kernel.Diagnostics`, `Compiler`, `VmApplication`.
- Публикация бенчмарков в BenchmarkDotNet-совместимом формате.

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

См. файл `LICENSE` в корне репозитория.

## Ссылки

- GitHub: [RomanyPe/Nantuc](https://github.com/RomanyPe/Nantuc)
- Документация по модулям — README в каждой папке (ссылки в разделе [Модули](#модули)).
- Спецификация байткода — `docs/bytecode.md` (в работе).
- Примеры — [`Nantuc.Samples.Minimal`](Nantuc.Samples.Minimal/README.md), [`Nantuc.Samples.Scratch`](Nantuc.Samples.Scratch/README.md).
- Бенчмарк — [`Nantuc.Benchmarks`](Nantuc.Benchmarks/README.md).

---

**Что изменено относительно предыдущей версии README (сводка):**

1. **`IdState`** — обновлён bullet: `Unknown` (0, default) / `Valid` (1) / `NotAssigned` (-1) / `OutOfRange` (-2) / `NoFreeSlot` (-3) / `Rejected` (-4); добавлено явное предупреждение `default(IdState) == Unknown`, а не `Valid` (breaking change в `Kernel.Common`).
2. **`IdState` в таблице модулей** — добавлено уточнение «`Unknown` = 0, default-safe».
3. **«Требования» → WASM** — развёрнуто по модулям: `Compiler` содержит `RegexOptions.Compiled` (browser-wasm AOT-blocker); `Kernel` использует `unsafe` + `NativeMemory` (работает в WASM), но `Run`/`DebugRun` синхронные (блокируют UI) и большие `RamSize` рискуют OOM.
4. **«Готово»** — добавлено «`default(IdState) == Unknown`, не `Valid`».
5. **«В работе / планы»** — добавлен пункт «WASM/AOT-фиксы» (убрать `RegexOptions.Compiled`, async-цикл для `Run`/`DebugRun`).
6. Комментарий разработчика про отладчик (raw stack access через `GetRegisterSnapshot()` и `NativeMemoryBuffer`/`CopyRamTo`) сохранён без изменений.