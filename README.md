# VMA — Virtual Machine & Emulator Platform

!ВАЖНО! Статус: платформа/тулкит, не фреймворк. API не гарантирует semver.

Платформа для сборки, компиляции и исполнения виртуальных машин с собственным байт-кодом,
компилятором C-подобного языка (MiniC) и ассемблером (VMA), плюс секторно-адресуемая шина
портов для IO и меж-устройственного взаимодействия.

Название «Kernel» в проектах `Kernel.*` — историческое. По сути это **общие (публичные)
контракты и примитивы** платформы, а не ядро. Настоящее ядро-исполнитель живёт в проекте
`Kernel` и наружу не выставляется.

---

# Слои:
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

`Kernel.Common` и `Kernel.Contracts` — публичные. Они специально выделены в отдельные проекты, чтобы истинное ядро (`Kernel`) не утекало наружу.
Ссылаться на них — нормально.

---

# Бытсрый старт

```csharp
using VMApplication;
using VMApplication.Project;

const string source =
"""
    LDI r0, 1
    LDI r1, 100
    ADD r0, r1
    END
""";

using var host = VMHostFactory.CreateDefault();

var comp = host.Project.Compiler.CompileToIL(source, 0, optimize: true, SourceLanguage.Asm).Build();

if (!comp.Success)
{
    foreach (var err in comp.Errors!) Console.WriteLine(err);
    return;
}

var handle = DeviceSpec.Create(
    ramSize: RamSize.MB128,
    name: "ConsoleVM"
);
int id = host.Emulator.RegisterDevice(handle: handle, sector: 0);
var ctx = host.Emulator.GetDeviceContext(id)!;

ctx.TryFastLoadProgram(comp.Program!, 0, out var error);
ctx.Run();
// Dispose у host сам уберёт устройство.
```

# Работа платформы

## Что такое `VMHost`
`VMHost` — это фасад всей ВМ. Он объединяет три подсистемы:

 - `VMHostProject` — компилятор (`CompileToIL`, `Compile`) и файловый сервис.
 - `VMEmulator` — реестр устройств, `PortBus`, создание/удаление `Device`.
 - `VMHostLogger` — единая точка логов.

## Управление жизненным циклом

- VMHost реализует IDisposable. При Dispose он сам убирает все устройства и освобождает их память.
- Если хост и устройство живут одинаково долго — не нужно вызывать RemoveDevice вручную.
- Не рекомендуется вызывать Dispose у DeviceContext вручную, если устройство принадлежит хосту. 
Это приведёт либо к двойному Dispose (если ваш класс это не защищает), либо к попытке хоста убрать 
уже освобождённое устройство.

### ВАЖНО!

Метод `VMHostFactory.CreateDefault()` создает хоста с стандартными сервисами (они подходят для в основном только для простых задач), если нужно более точная настройка
используйте билдеры.

Этот метод показан как пример проверки работоспособности с минимальным возможным кодом. Но лучше использовать свои сервисы.

```csharp
using var host = VMHostFactory.CreateDefault();
var handle = DeviceSpec.Create(
    ramSize: RamSize.MB128,
    name: "ConsoleVM"
);
int id = host.Emulator.RegisterDevice(handle: handle, sector: 0);
// ... работа ...
// Dispose у host сделает всё сам.
```
Если устройство нужно убрать раньше хоста — используйте `host.Emulator.RemoveDevice(id)`. Он автоматически вызовет Dispose. Учитывайте это.

## Компилятор

Компилятор встроен в `VMApplication` и вызывается через `host.Project.Compiler`:

```csharp
CompilationResult il = host.Project.Compiler.CompileToIL(source, 0, optimize: true, SourceLanguage.C).Build();
;
```

### Поддерживается:

- MiniC - C-подобный язык (`int`, `char`, `void`, `byte`, `ushort`, `ulong`, `struct`, `if`, `while`, `for`, `return`, `asm`, `extern`
, указатели, массивы).
- VMA — ассемблер (`LDI`, `MOV`, `ADD`, `LOAD`, `STORE`, `JMP`, `CALL`, `IN`, `OUT`, `HALT` и т.д.).
- **Inline asm** через `asm { ... }`.

### Важно про оптимизаторы

Оптимизаторы не заложены «**в коробку**». Встроенный компилятор работает без них. Чтобы включить:

- подключите `ExtensionsVMApplication` и вызовите `AddStdRules()` для AST- и peephole-оптимизаторов, либо
напишите свои правила (`IAstOptimizationRule`, `IPeepholeRule`).

```csharp
using ExtensionsVMApplication.Optimizators;

host.Project.Compiler.Optimizator
    .VMAstOptimizer.AddStdRules();

host.Project.Compiler.Optimizator
    .VMPeepholeOptimizer.AddStdRules();
```

### Ограничения кодогенерации

Кодогенератор сознательно простой и не претендует на качество промышленных компиляторов: 
слабое распределение регистров, 
консервативный inline, 
ограниченная поддержка указателей и структур. 
Если критична скорость готового байт-кода — либо пишите критичные участки на VMA напрямую, 
либо используйте `optimize: true` если вы подключили расширения, в ином случае без расширений оптимизаций никаких не будет.

## IO и шина портов

Ограничений на IO нет: реализуйте `IPortController` (или унаследуйтесь от `PortControlBase`) — и всё.

### Рекомендация: `PortControlBase`, а не `IPortController`

- Наследуйтесь от PortControlBase, если вам нужна защита от двойного Dispose — она уже реализована.
- Реализуйте `IPortController` напрямую только если обязаны наследоваться от другого класса
(C# не даёт множественного наследования).

```
using VMApplication.Emulator.Abstraction;

public sealed class MyDevice : PortControlBase
{
    public override byte ReadPort(ulong offset) => 0;
    public override void WritePort(ulong offset, byte value) { /* ... */ }
    public override void WakeProcessor() { /* ... */ }

    // При необходимости:
    protected override void OnDispose() { /* освободить ресурсы */ }
}
```

### Как работает `PortBus`

`PortBus` — линейно-блочный (секторный) маршрутизатор портов. Адрес разбивается 
на сектор (`address >> deviceShift`) и смещение внутри устройства (`address & offsetMask`). 
Реализовано через битовые сдвиги и маски — размеры блоков всегда кратны степеням двойки, 
это даёт предсказуемую и быструю адресацию.

- Помимо связи CPU ↔ устройство, `PortBus` позволяет устройствам общаться друг с другом — регистрируйте 
контроллеры в разных секторах и читайте/пишите в чужие адреса.
- `PortSize` — размер всего адресного пространства портов (степень 2, от 64 B до 512 KiB).
- `DevicePortSize` — размер блока одного устройства (степень 2, от 8 B до 128 B).
Оба типа — публичные value-типы, `default(T)` невалиден.

## `Kernel` — прямой доступ к интерпретатору

Проект `Kernel` — это минимальный возможный уровень работы с интерпретатором, памятью, IO и контрактами. 
Он не предназначен для обычного использования. 
Чтобы воспользоваться им напрямую, нужно явно указать:
```
using Kernel;
```

### `Device` — интерпретатор

Если быть точным то интерпритатор это именно `Processor`, а не сам `Device`, но вы не сможете использовать его без 
`Device`, так что лучше считать именно `Device` интерпритатором.

- Хорошая производительность; инструкции работы с памятью сравнимы по скорости с арифметикой.
- Точные цифры на вашем железе — в проекте `TestVMSpeed` 
(12 кейсов: `Memory`, `Math`, `ALU`, `Call`, `PushPop`, `BitOps`, `Mul`, `Branch`).
- `Device` всегда выделяет нативную память под ОЗУ. Это позволяет создавать ВМ практически любого размера. 
Сейчас разумный предел — ~2 GiB; расширение до 16 GiB технически возможно, 
но редко осмысленно (не на каждой машине столько ОЗУ можно выделить только под эмулятор).
- ОЗУ полностью инкапсулировано внутри `Device`.
Даже `INativeReadOnlyBuffer` — это отдельный фасад-класс; 
никакие приведения не дадут записать в ОЗУ в обход API.

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
### Безопасность

Использование `Kernel` напрямую — это небезопасный сценарий. 
Вы работаете с низкоуровневой памятью, регистрами и портами без страховок, 
которые даёт `VMApplication`. Для обычных задач используйте `VMApplication` + `ExtensionsVMApplication`.

---

# Рекомендации

1. Наследуйтесь от `PortControlBase`, а не реализуйте `IPortController` вручную — иначе сами отвечаете за идемпотентность Dispose.
2. Критичные по скорости участки пишите сразу на VMA или инлайните через `asm { }` — кодогенератор слабоват.
3. Размеры портов (`PortSize`, `DevicePortSize`) — только из статических фабрик (`PortSize.KB64`, `DevicePortSize.B16`); `default` невалиден.
4. Файловый сервис (`IFileService`) и конфиг путей (`IProjectFilesConfig`) — подменяемые; удобно для тестов и `in-memory` сборок (`RamFileService` в комплекте).

## Проекты решения

| Проект | Назначение |
|-------------|-------------|
| `Kernel.Common` | Публичные примитивы: `RegType`, `OpCode`, `OpCodeSize`, `RamSize`, `PortSize`, `DevicePortSize`, `BiosStatus`, `LogLevel`, `InstructionDecoder`, `AlignmentExtensions`.|
| `Kernel.Contracts` | Публичные контракты: `IPortController`/`IPortUse`, `IPortController`, `IProcessorReader`, `IProcessorFaultPolicy`, `IDeviceLoggerContext`, `INativeReadOnlyBuffer`, `ISimulationResult`, `OptimizationId`, `IReadOnlyLogOptimization`.|
| `Kernel` | Реализация: `Processor`, `Device`, `MemoryBus`, `NativeMemoryBuffer`, `PortBus`, `ManagerDevices`, `Emulator`. Небезопасный, прямой доступ. |
| `Kernel.Diagnostics` | `ErrorCode`, `IErrorMessageProvider`, `DefaultErrorMessageProvider`, `ThrowHelper`, `CodeExpection`. |
| `Compiller` | Лексер, парсер, AST, кодогенератор, ассемблер/дизассемблер, платформенных оптимизаторов. |
| `VMApplication` | Фасад: `VMHost`, `VMHostFactory`, `VMEmulator`, `DeviceContext`, `ProjectBuilder`, `VMHostProjectCompiler`, `IFileService`, `IOutputView`, билдеры. |
| `ExtensionsVMApplication` | Готовые AST- и peephole-правила, `PortControlBase`-совместимые IO-устройства (`QueuedIOStream`, `SynchronousIOStream`), host-helpers (`RunAndKeep`, `RunOnce`, `RunAndCleanup`). |
| `ConsoleEmulatorForTests` | Консольный пример запуска. |
| `TestVMSpeed` | Бенчмарк интерпретатора (warmup + прогоны, статистика, дизассемблер). |

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

---

## Известные ограничения

- Кодогенератор MiniC: базовое распределение регистров, ограниченная поддержка указателей/структур в параметрах функций.
- Оптимизаторы нужно подключать вручную.
- `TestVMSpeed` и `ExtensionsVMApplication` таргетят net10.0; `Compiller` и `Kernel.`* — net8.0; `VMApplication` — net8.0;net10.0.
- Прямое использование `Kernel` небезопасно и не рекомендуется без веской причины.