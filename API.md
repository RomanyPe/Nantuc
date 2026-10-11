# VMA API Documentation

> **Важно!** VMA — это платформа/тулкит, а не фреймворк. API не гарантирует семантическую версию (semver).

## Содержание

- [Архитектура](#архитектура)
- [Быстрый старт](#быстрый-старт)
- [VMHost — Главный фасад](#vmhost---главный-фасад)
- [Компилятор](#компилятор)
- [Эмулятор и устройства](#эмулятор-и-устройства)
- [IO и шина портов](#io-и-шина-портов)
- [Оптимизаторы](#оптимизаторы)
- [Kernel — Низкоуровневый доступ](#kernel---низкоуровневый-доступ)
- [Расширения (Extensions)](#расширения-extensions)
- [Справочник типов](#справочник-типов)
- [Известные ограничения](#известные-ограничения)

---

## Архитектура

Проект состоит из нескольких слоёв, каждый из которых отвечает за свою подсистему:

```
┌────────────────────────────────────────────────────────────────────┐
│  Хосты и тесты                                                     │
│  ConsoleEmulatorForTests, TestVMSpeed                              │
└────────────────────────────────────────────────────────────────────┘
                               │
┌────────────────────────────────────────────────────────────────────┐
│  ExtensionsVMApplication                                           │
│  Готовые peephole-правила, AST-правила, IO-устройства, host-helpers│
└────────────────────────────────────────────────────────────────────┘
                               │
┌────────────────────────────────────────────────────────────────────┐
│  VMApplication                                                     │
│  VMHost, VMHostFactory, VMEmulator, DeviceContext, ProjectBuilder, │
│  компилятор-фасад, оптимизатор-фасады, IFileService, IOutputView   │
└────────────────────────────────────────────────────────────────────┘
                               │
┌────────────────────────────────────────────────────────────────────┐
│  Kernel                       │  Compiller                         │
│  Processor, Device,           │  Lexer, Parser, CodeGen,           │
│  MemoryBus, PortBus,          │  Assembler, Disassembler,          │
│  ManagerDevices, Emulator     │  AstOptimizer, PeepholeOptimizer   │
└────────────────────────────────────────────────────────────────────┘
                               │
┌────────────────────────────────────────────────────────────────────┐
│  Kernel.Common                │  Kernel.Contracts                  │
│  RegType, OpCode, RamSize,    │  IPortController, IProcessorReader,│
│  PortSize, BiosStatus,        │  IProcessorFaultPolicy,            │
│  LogLevel, InstructionDecoder │  IDeviceLoggerContext,             │
│                               │  ISimulationResult, OptimizationId │
└────────────────────────────────────────────────────────────────────┘
```

> `Kernel.Common` и `Kernel.Contracts` — публичные. Они специально выделены в отдельные проекты, чтобы истинное ядро (`Kernel`) не утекало наружу.

---

## Быстрый старт

### Пример на VMA (ассемблер)

```csharp
using VMApplication;
using VMApplication.Project;

const string source =
"""
main:
    LDI  r1, 0x41          // 'A'
    LDI  r2, 0x10          // адрес порта Data
    OUT  r1, r2

    LDI  r1, 1
    LDI  r2, 0x12          // Flush
    OUT  r1, r2

    END
""";

using var host = VMHostFactory.CreateDefault();

var il   = host.Project.Compiler.CompileToIL(source, 0, optimize: true, SourceLanguage.Asm);
var comp = host.Project.Compiler.Compile(il);

if (!comp.Success)
{
    foreach (var err in comp.Errors!) Console.WriteLine(err);
    return;
}

int id  = host.Emulator.CreateAndAddDevice(RamSize.MB64, sector: 0, name: "vm");
var ctx = host.Emulator.GetDeviceContext(id)!;

ctx.TryFastLoadProgram(comp.Program!, 0, out var error);
ctx.Run();
// Dispose у host сам уберёт устройство.
```

### Пример на MiniC (C-подобный язык)

```csharp
using VMApplication;
using VMApplication.Project;

const string source =
"""
int add(int a, int b) {
    return a + b;
}

int main() {
    int result = add(5, 3);
    return 0;
}
""";

using var host = VMHostFactory.CreateDefault();

// Подключаем оптимизаторы (не включены по умолчанию!)
host.Project.Compiler.Optimizator.VMAstOptimizer.AddStdRules();
host.Project.Compiler.Optimizator.VMPeepholeOptimizer.AddStdRules();

var il   = host.Project.Compiler.CompileToIL(source, 0, optimize: true, SourceLanguage.C);
var comp = host.Project.Compiler.Compile(il);

if (!comp.Success)
{
    foreach (var err in comp.Errors!) Console.WriteLine(err);
    return;
}

int id  = host.Emulator.CreateAndAddDevice(RamSize.MB64, sector: 0, name: "vm");
var ctx = host.Emulator.GetDeviceContext(id)!;

ctx.TryFastLoadProgram(comp.Program!, 0, out var error);
ctx.Run();
```

---

## VMHost — Главный фасад

`VMHost` — это фасад всей виртуальной машины. Он объединяет три подсистемы:

| Свойство | Тип | Описание |
|----------|-----|----------|
| `Project` | `VMHostProject` | Компилятор (`CompileToIL`, `Compile`) и файловый сервис |
| `Emulator` | `VMEmulator` | Реестр устройств, `PortBus`, создание/удаление `Device` |
| `Logger` | `VMHostLogger` | Единая точка логов |

### Создание хоста

```csharp
// Простой способ (для тестов и простых задач)
using var host = VMHostFactory.CreateDefault();

// Или с кастомными сервисами
using var host = VMHostFactory.CreateDefault(
    outputView: new ConsoleOutputView(),
    projectConfig: new DefaultProjectFilesConfig(),
    fileService: new DefaultFileService("/path/to/project"),
    portBusSize: PortSize.KB64,
    portsPerDevice: DevicePortSize.B16
);
```

> **Важно!** `VMHostFactory.CreateDefault()` создаёт хоста со стандартными сервисами. Для точной настройки используйте билдеры напрямую.

### Жизненный цикл

- `VMHost` реализует `IDisposable`. При `Dispose` он автоматически удаляет все устройства и освобождает их память.
- Если хост и устройство живут одинаково долго — не нужно вызывать `RemoveDevice` вручную.
- **Не рекомендуется** вызывать `Dispose` у `DeviceContext` вручную, если устройство принадлежит хосту. Это приведёт либо к двойному `Dispose`, либо к попытке хоста убрать уже освобождённое устройство.

### Удаление устройства

```csharp
int deviceId = host.Emulator.CreateAndAddDevice(RamSize.MB16, sector: 0);
// ... работа ...
host.Emulator.RemoveDevice(deviceId); // Удалить раньше хоста
```

---

## Компилятор

Компилятор встроен в `VMApplication` и вызывается через `host.Project.Compiler`.

### Поддерживаемые языки

| Язык | Расширения | Описание |
|------|------------|----------|
| **MiniC** | `.mic`, `.c` | C-подобный язык |
| **VMA** | `.soe`, `.asm` | Ассемблер |

### Поддерживаемые конструкции MiniC

- **Типы:** `int`, `char`, `void`, `byte`, `ushort`, `ulong`
- **Структуры:** `struct` с полями разных типов
- **Управляющие конструкции:** `if`, `else`, `while`, `for`, `return`
- **Прямые операции:** `asm { ... }` (inline-ассемблер), `extern` (внешние объявления)
- **Память:** указатели (`*`), массивы (`a[n]`)
- **Доступ к полям:** `.fieldName` и `->fieldName`

### Базовое использование

```csharp
// Компиляция строки в IL (Intermediate Language)
CompilationToILResult il = host.Project.Compiler.CompileToIL(
    source,       // исходный код
    0,            // базовый адрес
    optimize: true,
    SourceLanguage.C
);

// Компиляция IL в байт-код
CompilationResult res = host.Project.Compiler.Compile(il);

if (!res.Success)
{
    foreach (var err in res.Errors!) Console.WriteLine(err);
}
```

### Компиляция из файлов

```csharp
// Компиляция из списка файлов
var il = host.Project.Compiler.CompileToIL(
    new[] { new SourceFile("main.c", source, SourceLanguage.C) },
    baseAddress: 0,
    optimize: true
);

// Компиляция из списка строк
var il2 = host.Project.Compiler.CompileToIL(
    new[] { source1, source2 },
    baseAddress: 0,
    optimize: true,
    language: SourceLanguage.C
);
```

### Оптимизаторы

> **Важно!** Оптимизаторы **не включены по умолчанию**. Встроенный компилятор работает без них.

#### Включение стандартных правил

```csharp
using ExtensionsVMApplication.Optimizators;

host.Project.Compiler.Optimizator.VMAstOptimizer.AddStdRules();
host.Project.Compiler.Optimizator.VMPeepholeOptimizer.AddStdRules();
```

#### Стандартные AST-правила

| Правило | Описание |
|---------|----------|
| `PropagateConstantsRule` | Подстановка констант в переменные |
| `FoldConstantsRule` | Сворачивание константных выражений |
| `RemoveUnusedVariablesRule` | Удаление неиспользуемых переменных |
| `RemoveUnreachableCodeRule` | Удаление недостижимого кода |
| `InlineSmallVoidFunctionsRule` | Инлайн маленьких `void`-функций |

#### Стандартные Peephole-правила

| Правило | Описание |
|---------|----------|
| `RemoveNoopMovRule` | Удаление `MOV r, r` |
| `RemovePushPopPairRule` | Удаление пар `PUSH r; POP r` |
| `RemoveSwapMovPairRule` | Удаление пар `MOV r1, r2; MOV r2, r1` |
| `RemoveJumpToNextLabelRule` | Удаление перехода на следующую метку |
| `ConstantFoldingRule` | Сворачивание констант в ассемблере |
| `RemoveMultiEndPairRule` | Удаление дублирующихся `END` |

#### Создание своих правил

```csharp
// AST-правило
public class MyAstRule : IAstOptimizationRule
{
    public OptimizationId Type => new("my.rule");
    public IReadOnlyLogOptimization Optimize(ProgramNode program)
    {
        // Ваша логика оптимизации AST
        return new ASTNodesOptimization(new StringBuilder(), Type);
    }
}

// Peephole-правило
public class MyPeepholeRule : IPeepholeRule
{
    public bool TryOptimize(List<AsmItem> items, int index, PeepholeLog log, out int newIndex)
    {
        newIndex = index;
        // Ваша логика оптимизации
        return false;
    }
}

// Добавление правил
host.Project.Compiler.Optimizator.VMAstOptimizer.Add(new MyAstRule());
host.Project.Compiler.Optimizator.VMPeepholeOptimizer.Add(new MyPeepholeRule());
```

---

## Эмулятор и устройства

### VMEmulator

`VMEmulator` управляет реестром устройств и шиной портов.

#### Создание устройства

```csharp
// Создание RAM-устройства
int deviceId = host.Emulator.CreateAndAddDevice(
    ramSize: RamSize.MB64,
    sector: 0,
    deviceCtx: null,           // IDeviceLoggerContext?
    name: "vm",                // Имя устройства
    processorFaultPolicy: null  // IProcessorFaultPolicy?
);

// Создание пользовательского устройства
int id = host.Emulator.AddDevice(myDevice, "my-device", sector: 1);
```

#### Получение контекста устройства

```csharp
DeviceContext ctx = host.Emulator.GetDeviceContext(deviceId)!;
```

#### Информация о устройствах

```csharp
IReadOnlyCollection<DeviceInfo> allDevices = host.Emulator.GetAllDevices();
IPortController? device = host.Emulator.GetDevice(deviceId);
MyDevice? typed = host.Emulator.GetDeviceAs<MyDevice>(deviceId);

if (host.Emulator.TryGetDevice<MyDevice>(deviceId, out var dev))
{
    // device найден и преобразован
}
```

### DeviceContext

`DeviceContext` — пользовательский обёртка над `Device`, предоставляющая методы выполнения.

| Свойство | Тип | Описание |
|----------|-----|----------|
| `Name` | `string?` | Имя устройства |
| `IsRunning` | `bool` | Запущено ли устройство |
| `StepCount` | `long` | Количество выполненных шагов |
| `MaxRamSize` | `ulong` | Максимальный размер RAM |
| `NativeReadOnlyBuffer` | `INativeReadOnlyBuffer` | Доступ к нативной памяти |

#### Методы выполнения

| Метод | Описание |
|-------|----------|
| `Run(LaunchOptions?)` | Запуск симуляции |
| `Stop()` | Остановка симуляции |
| `SingleStep()` | Выполнение одной инструкции |
| `MultiStep(int)` | Выполнение N инструкций |
| `SetStepMode(ulong, Action<IDeviceLoggerContext>?)` | Установка точечного режима (breakpoints) |

#### Загрузка программы

```csharp
// Безопасная загрузка с проверкой
if (ctx.TryFastLoadProgram(programBytes, 0, out var error))
{
    ctx.Run();
}
else
{
    Console.WriteLine(error);
}

// Стандартная загрузка
ctx.LoadProgram(programBytes, loadAddress: 0);
```

#### Управление регистрами и памятью

```csharp
ctx.ResetMemoryRam();       // Сброс RAM
ctx.ResetRegisters();       // Сброс регистров
ctx.CopyRegisters(dest);    // Копирование регистров
ctx.GetRegistersSnapshot(); // Снимок регистров
ctx.ReadPort(offset);       // Чтение порта
```

#### Запуск с опциями

```csharp
ctx.Run(new LaunchOptions(
    StartAddress: 0,
    DelayMs: 0,           // Задержка между шагами (мс)
    ShowTimer: false,     // Показывать таймер
    OnLaunch: null,       // Действие при запуске
    OnStart: null,        // Действие при старте
    OnEnd: null           // Действие при завершении
));
```

---

## IO и шина портов

### PortBus

`PortBus` — линейно-блочный (секторный) маршрутизатор портов. Адрес разбивается на сектор и смещение:

```
address >> deviceShift  →  sector
address & offsetMask    →  offset внутри устройства
```

Размеры блоков всегда кратны степеням двойки, что обеспечивает предсказуемую и быструю адресацию.

#### Создание PortBus

```csharp
var emulatorBuilder = new VMEmulatorBuilder()
    .WithPortBusSize(PortSize.KB64)           // Размер адресного пространства портов
    .WithPortsPerDevice(DevicePortSize.B16);   // Размер блока одного устройства
```

### Создание IO-устройств

#### Рекомендуемый способ: PortControlBase

```csharp
using VMApplication.Emulator.Abstraction;

public sealed class MyDevice : PortControlBase
{
    public override byte ReadPort(ulong offset) => 0;
    public override void WritePort(ulong offset, byte value) { /* ... */ }
    public override void WakeProcessor() { /* ... */ }

    protected override void OnDispose() { /* освободить ресурсы */ }
}
```

#### Прямая реализация IPortController

```csharp
public sealed class MyDevice : IPortController
{
    public byte ReadPort(ulong offset) => 0;
    public void WritePort(ulong offset, byte value) { /* ... */ }
    public void WakeProcessor() { /* ... */ }
}
```

> **Рекомендация:** Наследуйтесь от `PortControlBase`, если вам нужна защита от двойного `Dispose`. Реализуйте `IPortController` напрямую только если обязаны наследоваться от другого класса.

### Готовые IO-устройства

#### SynchronousIOStream

Синхронный вывод в консоль/терминал.

```csharp
using ExtensionsVMApplication.Emulator.IO;

var device = new SynchronousIOStream(Console.Out, PortCharEncoding.BytePerChar);
int id = host.Emulator.AddDevice(device, "console", sector: 0);
```

#### QueuedIOStream

Асинхронный вывод с буферизацией.

```csharp
var device = new QueuedIOStream(Console.Out, PortCharEncoding.Utf8);
int id = host.Emulator.AddDevice(device, "console", sector: 0);
```

#### Кодировки

| Кодировка | Описание |
|-----------|----------|
| `BytePerChar` | 1 байт = 1 символ (Latin-1). Простейший режим |
| `Utf8` | 1..4 байта = 1 символ, полноценный UTF-8 |
| `Utf16` | 2 байта = 1 символ, UTF-16 little-endian |

#### Раскладка портов вывода

Устройство вывода занимает 8 байт:

| Смещение | Константа | Описание |
|----------|-----------|----------|
| 0 | `OutputPortLayout.Data` | Запись байта на вывод. Чтение возвращает 0 |
| 1 | `OutputPortLayout.Status` | Статус: 0 — готово, 1 — есть необработанные данные в очереди |
| 2 | `OutputPortLayout.Flush` | Запись 1 — сбросить буфер вывода |

---

## Оптимизаторы

### AST-оптимизатор

Работает с абстрактным синтаксическим деревом до кодогенерации.

```csharp
// Получение логирования оптимизаций
var log = host.Project.Compiler.Optimizator.VMAstOptimizer.Add(new MyRule());
```

#### Интерфейс IAstOptimizationRule

```csharp
public interface IAstOptimizationRule
{
    OptimizationId Type { get; }
    IReadOnlyLogOptimization Optimize(ProgramNode program);
}
```

### Peephole-оптимизатор

Работает с промежуточным представлением (IL) после кодогенерации, но перед сборкой байт-кода.

#### Интерфейс IPeepholeRule

```csharp
public interface IPeepholeRule
{
    bool TryOptimize(List<AsmItem> items, int index, PeepholeLog log, out int newIndex);
}
```

- `items` — список элементов (инструкции, метки, данные)
- `index` — текущая позиция в списке
- `newIndex` — новая позиция (обычно `index` или `index - количество удалённых`)
- Возвращает `true`, если правило применимо

### Элементы IL (AsmItem)

| Класс | Описание |
|-------|----------|
| `AsmInstruction` | Инструкция с OpCode, регистрами, immediate-данными |
| `AsmLabel` | Метка |
| `AsmData` | Сырые данные |

---

## Kernel — Низкоуровневый доступ

> **Внимание!** Прямое использование `Kernel` небезопасно. Вы работаете с низкоуровневой памятью, регистрами и портами без страховок, которые даёт `VMApplication`.

### Processor

Ядро интерпретатора. Обрабатывает 32-битные инструкции:

```
Bits 0-7:   OpCode
Bits 8-12:  Reg1 (Destination/Primary)
Bits 13-17: Reg2 (Source/Secondary)
Bits 18-19: OpCodeSize (S8, S16, S32, S64)
Bits 20-31: Reserved/Operand data
```

### Device

Обёртка над `Processor` и `MemoryBus`. Обрабатывает загрузку программы, симуляцию и аппаратные прерывания.

### MemoryBus

Управляет нативной памятью с проверкой выравнивания для 16/32/64-битного доступа.

| Метод | Описание |
|-------|----------|
| `ReadIntXLE` | Безопасное чтение |
| `ReadIntXLEUnSafe` | Небезопасное чтение (без проверок) |

### ManagerDevices

Управление устройствами в `Kernel`. Поддерживает переиспользование слотов при удалении устройств.

---

## Расширения (Extensions)

### HostExtensions

Помощники для быстрого запуска:

```csharp
using ExtensionsVMApplication.Host;

// Запуск с сохранением устройства
var outcome = host.RunAndKeep(
    source: "main: LDI r1, 0x41; END",
    lang: SourceLanguage.Asm,
    ram: RamSize.MB16,
    optimize: false,
    deviceName: "vm"
);

// outcome.Device — контекст устройства
// outcome.StepCount — количество шагов
// outcome.Success — успех выполнения

// Запуск с удалением устройства после выполнения
bool success = host.RunAndCleanup(source, lang, ram, optimize);

// Запуск с удалением устройства и очисткой результата
var outcome2 = host.RunOnce(source, lang, ram, optimize);
```

### Пользовательские оптимизации

```csharp
// Замена безопасного доступа к памяти на небезопасный
host.Project.Compiler.Optimizator.VMPeepholeOptimizer.Add(
    new ReplaceSafeMemAccessWithUnsafeRule { SizeRam = RamSize.MB16 }
);
```

---

## Справочник типов

### Kernel.Common

#### RegType — Типы регистров

32 регистра: `r0`–`r27`, `rFL`, `rSP`, `rHP`, `rIP`

#### RamSize — Размер памяти

Value-тип, хранит размер как `log2(bytes)`. `default(RamSize)` невалиден.


| Статическое свойство | Размер | Значение в байтах |
|----------------------|--------|-------------------|
| `B64`                | 64 B   | 64                |
| `B128`               | 128 B  | 128               |
| `B256`               | 256 B  | 256               |
| `B512`               | 512 B  | 512               |
| `KB1`                | 1 KiB  | 1,024             |
| `KB2`                | 2 KiB  | 2,048             |
| `KB4`                | 4 KiB  | 4,096             |
| `KB8`                | 8 KiB  | 8,192             |
| `KB16`               | 16 KiB | 16,384            |
| `KB32`               | 32 KiB | 32,768            |
| `KB64`               | 64 KiB | 65,536            |
| `KB128`              | 128 KiB| 131,072           |
| `KB256`              | 256 KiB| 262,144           |
| `KB512`              | 512 KiB| 524,288           |
| `MB1`                | 1 MiB  | 1,048,576         |
| `MB2`                | 2 MiB  | 2,097,152         |
| `MB4`                | 4 MiB  | 4,194,304         |
| `MB8`                | 8 MiB  | 8,388,608         |
| `MB16`               | 16 MiB | 16,777,216        |
| `MB32`               | 32 MiB | 33,554,432        |
| `MB64`               | 64 MiB | 67,108,864        |
| `MB128`              | 128 MiB| 134,217,728       |
| `MB256`              | 256 MiB| 268,435,456       |
| `MB512`              | 512 MiB| 536,870,912       |
| `GB1`                | 1 GiB  | 1,073,741,824     |
| `GB2`                | 2 GiB  | 2,147,483,648     |


#### PortSize — Размер шины портов

От 64 B до 512 KiB. `default(PortSize)` невалиден.

| Статическое свойство | Размер | Значение в байтах |
|----------------------|--------|-------------------|
| `B64`                | 64 B   | 64                |
| `B128`               | 128 B  | 128               |
| `B256`               | 256 B  | 256               |
| `B512`               | 512 B  | 512               |
| `KB1`                | 1 KiB  | 1,024             |
| `KB2`                | 2 KiB  | 2,048             |
| `KB4`                | 4 KiB  | 4,096             |
| `KB8`                | 8 KiB  | 8,192             |
| `KB16`               | 16 KiB | 16,384            |
| `KB32`               | 32 KiB | 32,768            |
| `KB64`               | 64 KiB | 65,536            |
| `KB128`              | 128 KiB| 131,072           |
| `KB256`              | 256 KiB| 262,144           |
| `KB512`              | 512 KiB| 524,288           |

#### DevicePortSize — Размер блока устройства

От 8 B до 128 B. `default(DevicePortSize)` невалиден.

| Статическое свойство | Размер | Значение в байтах |
|----------------------|--------|-------------------|
| `B8`                 | 8 B    | 8                 |
| `B16`                | 16 B   | 16                |
| `B32`                | 32 B   | 32                |
| `B64`                | 64 B   | 64                |
| `B128`               | 128 B  | 128               |

#### OpCode — Коды операций

60+ инструкций, сгруппированных:

| Группа | Инструкции |
|--------|------------|
| **Системные** | `NOP`, `END`, `HALT`, `WAKE` |
| **Память** | `MOV`, `LOAD`, `STORE`, `LOAD_UNSAFE`, `STORE_UNSAFE`, `LDI` |
| **ALU** | `ADD`, `SUB`, `MULT_INT`, `SHR`, `DIV`, `CMP`, `TEST`, `INC`, `DEC` |
| **Логика** | `AND`, `OR`, `XOR`, `NOT` |
| **Потоки** | `JMP`, `JZ`, `JNZ`, `JG`, `JL`, `CALL`, `RET`|
| **Указатели** | `JMP_IND`, `CALL_IND`, `LOAD_IND`, `STORE_IND` |
| **Стек** | `PUSH`, `POP` |
| **IO** | `IN`, `OUT` |

#### BiosStatus — Статус BIOS

| Значение | Описание |
|----------|----------|
| `Success` | Успешно выполнено |
| `SegmentationFault` | Выход за границы ОЗУ |
| `AlignmentFault` | Попытка прочесть данные по невыровненному адресу (например, 0x03, зависит от размера числа) |
| `ReadViolation` | Попытка чтения из защищенной области памяти |
| `EndProgramm` | Остановка программы (не является ошибкой), но может появиться при `Stop()` устройства |
| `NotImplementedOpCode` | Неизвестная команда OpCode |
| `NullDeviceInput` | Пустой ввод устройства |
| `NullDeviceOutput` | Пустой вывод устройства |
| `InfinityLoop` | Бесконечный цикл |
| `DivOnZero` | Деление на ноль |
| `NotSupportedOpCode` | Неподдерживаемая команда OpCode |
| `StackUnderflow` | Опустошение стека (Stack Underflow) |

#### LogLevel — Уровень логирования

Используется в `VMHostLogger`.

### Kernel.Contracts

#### IPortController

Интерфейс для IO-устройств:

```csharp
public interface IPortController
{
    byte ReadPort(ulong offset);
    void WritePort(ulong offset, byte value);
    void WakeProcessor();
}
```

#### IProcessorFaultPolicy

Определяет, продолжать ли выполнение после ошибки:

```csharp
public interface IProcessorFaultPolicy
{
    bool ShouldContinue(in ProcessorFault fault);
}

// По умолчанию:
// - Success → продолжать
// - NullDeviceOutput → продолжать
// - Остальные → остановить
```

#### IDeviceLoggerContext

Контекст логирования для устройств.

#### INativeReadOnlyBuffer

Фасад для чтения нативной памяти ВМ.

#### ISimulationResult

Результат симуляции.

#### OptimizationId

Идентификатор правила оптимизации.

---

## Известные ограничения

### Кодогенератор MiniC

- Базовое распределение регистров
- Консервативный inline
- Ограниченная поддержка указателей и структур в параметрах функций
- **Рекомендация:** Критичные по скорости участки пишите на VMA или инлайните через `asm { }`

### Оптимизаторы

- Не включены по умолчанию
- Отсутсвуют встроенные оптимизаторы
- Нужно подключать вручную через `AddStdRules()` или добавление своих правил

### Целевые платформы

| Проект | Target |
|--------|--------|
| `Kernel.*`, `Compiller` | `net8.0` |
| `VMApplication` | `net8.0; net10.0` |
| `ExtensionsVMApplication`, `TestVMSpeed` | `net10.0` |

### Прямое использование Kernel

- Небезопасно
- Не рекомендуется без веской причины
- Используйте `VMApplication` + `ExtensionsVMApplication` для обычных задач
- `VMApplication` покрывает большинство сценариев

### Размеры памяти

- Разумный предел — ~2 GiB
- Технически возможно до 16 GiB, но редко осмысленно

---

## Рекомендации

1. Наследуйтесь от `PortControlBase`, а не реализуйте `IPortController` вручную.
2. Критичные по скорости участки пишите сразу на VMA или инлайните через `asm { }`.
3. Размеры портов (`PortSize`, `DevicePortSize`) — только из статических фабрик (`PortSize.KB64`, `DevicePortSize.B16`); `default` невалиден.
4. Файловый сервис (`IFileService`) и конфиг путей (`IProjectFilesConfig`) — подменяемые; удобно для тестов и `in-memory` сборок (`RamFileService` в комплекте).
5. Логи идут через `VMHostLogger` → `IOutputView`. Для «тихой» работы используйте `NullOutputView.Instance`.

---

## Примеры проектов

| Проект | Назначение |
|--------|------------|
| `ConsoleEmulatorForTests` | Консольный пример запуска |
| `TestVMSpeed` | Бенчмарк интерпретатора (12 кейсов: Memory, Math, ALU, Call, PushPop, BitOps, Mul, Branch) |
| `include/` | Стандартные библиотеки VMA (`sys.vma`, `std.vma`, `math.vma`) |

---


Таблица мнемоник AssemblerParser
Легенда типов мнемоник:

|Тип |	Значение	  |Формат операндов|
|----|----------------|----------------|
|`N`| нет операндов  | - |
|`R`| `register-register`	| `reg`, `reg` |
|`U`| `unary` / один регистр	| `reg` |
|`I`| `immediate` / адрес |	`reg`, `imm` или `reg`, `addr` |
|`J`| `jump` / `call` |	`label` или `abs_addr` |
|`RS`| `register-register-size` | `reg`, `reg[, .Sxx]` |

- Опциональный суффикс размера: `.S8`, `.S16`, `.S32`, `.S64` или `.8`, `.16`, `.32`, `.64`.
- Парсер разбирает суффикс для всех мнемоник, но фактически использует его только в `LOAD*`, `STORE*`, `LOAD_IND*`, `STORE_IND*`. По умолчанию — `S64`.


| Мнемоника | Тип | Операнды | Метод парсера | Кодирование |
| :--- | :---: | :--- | :--- | :--- |
| NOP | N | — | case "NOP" | EncodeNOP() |
| END | N | — | case "END" | EncodeEND() |
| RET | N | — | case "RET" | EncodeRET() |
| HALT | N | — | case "HALT" | EncodeHALT() |
| WAKE | N | — | case "WAKE" | EncodeWAKE() |
| ADD | R | reg, reg | FormatR | EncodeR |
| AND | R | reg, reg | FormatR | EncodeR |
| CMP | R | reg, reg | FormatR | EncodeR |
| DIV | R | reg, reg | FormatR | EncodeR |
| IN | R | reg, reg | FormatR | EncodeR |
| MOV | R | reg, reg | FormatR | EncodeR |
| MULT_INT | R | reg, reg | FormatR | EncodeR |
| OR | R | reg, reg | FormatR | EncodeR |
| OUT | R | reg, reg | FormatR | EncodeR |
| SHR | R | reg, reg | FormatR | EncodeR |
| SUB | R | reg, reg | FormatR | EncodeR |
| TEST | R | reg, reg | FormatR | EncodeR |
| XOR | R | reg, reg | FormatR | EncodeR |
| CALL_IND | U | reg | FormatU | EncodeU |
| DEC | U | reg | FormatU | EncodeU |
| INC | U | reg | FormatU | EncodeU |
| JMP_IND | U | reg | FormatU | EncodeU |
| NOT | U | reg | FormatU | EncodeU |
| POP | U | reg | FormatU | EncodeU |
| PUSH | U | reg | FormatU | EncodeU |
| WAKE_INT | U | reg | FormatU | EncodeU |
| LDI | I | reg, imm/label | FormatLDI | EncodeLDI + 64-bit |
| LOAD | I | reg, addr | FormatLOAD__STORE | EncodeLOAD + 64-bit |
| LOAD_UNSAFE | I | reg, addr | FormatLOAD__STORE | EncodeSTORE + 64-bit* |
| STORE | I | reg, addr | FormatLOAD__STORE | EncodeSTORE + 64-bit |
| STORE_UNSAFE | I | reg, addr | FormatLOAD__STORE | EncodeSTORE + 64-bit |
| LOAD_IND | RS | reg, reg[, .Sxx] | FormatLOAD_IND__STORE_IND | EncodeRS |
| LOAD_IND_UNSAFE | RS | reg, reg[, .Sxx] | FormatLOAD_IND__STORE_IND | EncodeRS |
| STORE_IND | RS | reg, reg[, .Sxx] | FormatLOAD_IND__STORE_IND | EncodeRS |
| STORE_IND_UNSAFE | RS | reg, reg[, .Sxx] | FormatLOAD_IND__STORE_IND | EncodeRS |
| CALL | J | label/abs | FormatJumpType | EncodeJ + target |
| JG | J | label/abs | FormatJumpType | EncodeJ + target |
| JL | J | label/abs | FormatJumpType | EncodeJ + target |
| JMP | J | label/abs | FormatJumpType | EncodeJ + target |
| JNZ | J | label/abs | FormatJumpType | EncodeJ + target |
| JZ | J | label/abs | FormatJumpType | EncodeJ + target |