# Nantuc

> Платформа для выполнения байткода с собственным языком MiniC (почти полноценный C), ассемблером NANASM, компилятором, виртуальной машиной и публичным API для встраивания.  
> Написана на C# (.NET 10). Компилятор и рантайм разделены по слоям, unsafe-код изолирован в ядре, публичный API — тонкий фасад.

Nantuc — это мини-фреймворк, в котором есть всё, что нужно для запуска программы на C-подобном языке: лексер, парсер, AST-оптимизатор, кодогенератор в байткод, ассемблер (текстовый и IR), виртуальная машина с регистрами, памятью и порт-шиной, дизассемблер, I/O-устройства и API для встраивания в приложение. Проект вырос до 12 тысяч строк, индекс поддерживаемости по метрикам Visual Studio — 80–96 из 100. В репозитории есть играбельная змейка на MiniC, тест аллокатора и бенчмарк VM.

## Содержание
- [Ключевые особенности](#ключевые-особенности)
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
- **Устройства как first-class**: VM сама является устройством (`IPortController`) — можно вкладывать VM друг в друга, подключать UART, мониторы, клавиатуры.
- **Публичный API без хаков**: `VmHost` / `DeviceContext` / `VmHostProject` — весь unsafe и нативные буферы спрятаны в `Nantuc.Kernel`.
- **Дизассемблер** с секциями кода и данных, текстовый и структурный.
- **Стандартные оптимизации** (AST и peephole) и инфраструктура для своих правил.
- **Бенчмарк** с 12 кейсами: память (safe/unsafe), ALU (ILP и без), вызовы, стек, битовые операции, умножение, ветвления. MIPS, перцентили, CV, JIT-impact.

## Быстрый старт

Минимальный пример — компиляция и запуск `Hello world` на MiniC с выводом через UART:

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

int vmId = host.Emulator.RegisterDevice(
    DeviceSpec.Create(RamSize.MB128, name: "ConsoleVM"), sector: 0);
host.Emulator.RegisterDevice(
    new QueuedIOStream(Console.Out, PortCharEncoding.Utf16), name: "console", sector: 1);

var ctx = host.Emulator.GetDeviceContext(vmId)!;
var compilation = host.Project.CompileToIL(baseAddress: 0, optimize: true).Build();
if (!compilation.Success) { foreach (var e in compilation.Errors!) Console.WriteLine(e); return; }
if (!ctx.TryFastLoadProgram(compilation.Program!, 0, out var error)) { Console.WriteLine(error); return; }

ctx.Run();
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
└──────────────────────────────────────────────────────────────────────┘
              │                                │
┌──────────────────────────┐    ┌──────────────────────────────────────┐
│  Nantuc.Compiler         │    │  Nantuc.Kernel                       │
│  (MiniC + NANASM)        │    │  (Processor, MemoryBus, PortBus,     │
│                          │    │   Device, ManagerDevices)            │
└──────────────────────────┘    └──────────────────────────────────────┘
              │                                │
┌──────────────────────────────────────────────────────────────────────┐
│  Nantuc.Kernel.Contracts                                             │
│  (IPortBus, IPortUse, IPortController, IProcessorReader, ...)        │
└──────────────────────────────────────────────────────────────────────┘
              │                                │
┌──────────────────────────────────────────────────────────────────────┐
│  Nantuc.Kernel.Common            Nantuc.Kernel.Diagnostics           │
│  (ISA, размеры, BiosStatus)      (ErrorCode, ThrowCompiler)          │
└──────────────────────────────────────────────────────────────────────┘
```

Обратите внимание:
- **`Kernel.Diagnostics` не зависит ни от одного проекта Nantuc.** Это сделано намеренно: автор компилятора не тянет unsafe-ядро ради кодов ошибок.
- **Компилятор не зависит от рантайма.** `Nantuc.Compiler` знает только про `Kernel.Common` (ISA) и `Kernel.Diagnostics` (ошибки), но не про `Processor`/`MemoryBus`.
- **VMApplication — единственный модуль, который одновременно знает про компилятор и рантайм.** Именно он склеивает «скомпилировать» и «запустить».

## Структура репозитория

```
Nantuc/
├── Nantuc.Kernel.Common/              # ISA, размеры, BiosStatus, ProcessorFault
├── Nantuc.Kernel.Contracts/           # публичные контракты (порты, процессор)
├── Nantuc.Kernel.Diagnostics/         # ErrorCode, ThrowCompiler, CodeException
├── Nantuc.Kernel/                     # реализация CPU, RAM, PortBus, Device
├── Nantuc.Compiler/                   # MiniC + NANASM (frontend, backend, ASM, оптимизации)
├── Nantuc.VmApplication/              # основной API
├── Nantuc.VmApplication.Extensions/   # дизассемблер, I/O, оптимизации, библиотеки
├── Nantuc.Samples.Minimal/            # Hello world
├── Nantuc.Samples.Scratch/            # тест аллокатора + змейка
└── Nantuc.Benchmarks/                 # бенчмарк VM
```

## Модули

| Модуль | Назначение | Кому |
|---|---|---|
| [`Nantuc.Kernel.Common`](Nantuc.Kernel.Common/README.md) | Опкоды, регистры, размеры RAM/портов, `BiosStatus`, декодер инструкций | Авторы компилятора, расширений, дизассемблера |
| [`Nantuc.Kernel.Contracts`](Nantuc.Kernel.Contracts/README.md) | `IPortBus`, `IPortUse`, `IPortController`, `IProcessorReader`, `IProcessorFaultPolicy`, `OptimizationId` | Авторы устройств и расширений |
| [`Nantuc.Kernel.Diagnostics`](Nantuc.Kernel.Diagnostics/README.md) | `ErrorCode`, `IErrorMessageProvider`, `ThrowCompiler`, `CodeException` | Авторы компилятора, IDE, CI |
| [`Nantuc.Kernel`](Nantuc.Kernel/README.md) | `Processor`, `MemoryBus`, `PortBus`, `ManagerDevices`, `Emulator`, `Device` | Встраивающий код, автор отладчика |
| [`Nantuc.Compiler`](Nantuc.Compiler/README.md) | `Lexer`, `Parser`, AST, `FunctionGenerator`, `Assembler`, `AssemblerParser`, `PeepholeOptimizer`, `AstOptimizer` | Автор IDE, автор правил оптимизации |
| [`Nantuc.VmApplication`](Nantuc.VmApplication/README.md) | `VmHost`, `DeviceContext`, `VmEmulator`, `VmHostProject`, `CompilationResult`, `DeviceSpec` | Конечный пользователь платформы |
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
- Опкоды с immediate дополняются 8-байтным операндом с выравниванием до 8 (может быть до 4 байт паддинга).
- Полная спецификация — в [`Nantuc.Kernel.Common`](Nantuc.Kernel.Common/README.md) и в `docs/bytecode.md` (по мере наполнения).

## Требования

- **.NET 10 SDK** (preview/GA — по состоянию на момент сборки). Все проекты таргетят `net10.0`.
- **C# 14**: используются `extension`-блоки, `[InlineArray]`, `params ReadOnlySpan`, `field`-keyword, `FrozenDictionary`/`FrozenSet`, primary constructors.
- **Windows / Linux / macOS**. В WASM работает `Kernel.Common`/`Contracts`/`Diagnostics`/`Compiler`, но `Nantuc.Kernel` требует `unsafe`, а `Nantuc.Benchmarks` — `Process`/P/Invoke.

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

**Запустить тесты** (если есть тестовый проект):

```bash
dotnet test -c Release
```

## Примеры

### Hello world (UTF-16 через UART)

См. [`Nantuc.Samples.Minimal/README.md`](Nantuc.Samples.Minimal/README.md) — полный разбор `Program.cs`, объяснение портов `0x10`/`0x12` и `QueuedIOStream`.

### Змейка

См. [`Nantuc.Samples.Scratch/README.md`](Nantuc.Samples.Scratch/README.md) — `SnakeLauncher`, `MonochromeMonitor` (32×16), `KeyboardDevice`, HALT/WAKE-цикл, тикер, ANSI-рендер.

### Тест аллокатора

См. [`Nantuc.Samples.Scratch/README.md`](Nantuc.Samples.Scratch/README.md), `Program.cs` — `Librare.alloc_minic` (first-fit + free-list), проверка `return 10`, дамп регистров и дизассемблер RAM.

### Своя игра / демо

```csharp
using var host = VmHostFactory.CreateDefault();
host.Project.SaveFileProject("main.minic", source);
host.Project.SaveFileInclude("monitor.nanasm", LibCode.monitor_nanasm);

int vmId = host.Emulator.RegisterDevice(
    DeviceSpec.Create(RamSize.MB32, name: "MyVM"), sector: 0);
host.Emulator.RegisterDevice(new MonochromeMonitor(64, 32, MyRenderer), "mon", sector: 1);

var ctx = host.Emulator.GetDeviceContext(vmId)!;
var comp = host.Project.CompileToIL(0, optimize: true).Build();
if (comp.Success) { ctx.TryFastLoadProgram(comp.Program!, 0, out _); ctx.Run(); }
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
- **Бенчмарк**: 12 кейсов, MIPS, перцентили, CV, trimmed mean, JIT-impact. Формат отчёта — в [`Nantuc.Benchmarks/README.md`](Nantuc.Benchmarks/README.md).

## Статус и планы

**Готово:**
- MiniC (лексер, парсер, AST, кодогенерация, структуры, указатели, массивы, inline asm, `#include`, `extern`).
- NANASM (текстовый ассемблер, размерные суффиксы, метки).
- VM (регистры, RAM, стек, heap, порты, HALT/WAKE, внешние команды, fault-policy).
- Устройства: UART (sync/async/numeric), монитор (32×16), клавиатура.
- Дизассемблер (текстовый и структурный).
- Стандартные AST- и peephole-оптимизации.
- Публичный API (`VmHost`, `DeviceContext`, `VmHostProject`, `DeviceSpec`).
- Примеры: Hello world, змейка, тест аллокатора.
- Бенчмарк.

**В работе / планы:**
- Полная спецификация байткода (`docs/bytecode.md`).
- Отладчик поверх `IProcessorReader` (точки останова, шаг, инспекция стека).
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