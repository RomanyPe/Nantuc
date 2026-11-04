# Nantuc.Contracts — README

> Контракты платформы Nantuc: интерфейсы шины портов, процессора, политик падения и оптимизаций.  
> Единственный слой, от которого зависят и `Kernel`, и `VmApplication`, и `Extensions`.  
> `[← Корень](../README.ru.md) · [Common](../../src/Nantuc.Common/README.ru.md) · [Kernel](../../src/Nantuc.Kernel/README.ru.md)`

## Содержание
- [Об этом модуле](#об-этом-модуле)
- [Дерево локального проекта](#дерево-локального-проекта)
- [API](#api)
- [Примеры для API](#примеры-для-api)
- [Приближенность модуля к пользователю](#приближенность-модуля-к-пользователю)
- [Особенная информация](#особенная-информация)
- [Что исправить](#что-исправить)

## Об этом модуле

`Nantuc.Contracts` — это слой абстракций платформы. Здесь нет ни логики процессора, ни шин, ни устройств, ни состояния: только `interface`-ы, `readonly record struct`-ы для результатов и один `readonly struct` с валидацией (`OptimizationId`). Всё, что должен уметь вызывать пользователь ядра, объявлено здесь; реализация живёт в `Kernel`/`VmApplication`/`Extensions`.

Модуль зависит только от `Nantuc.Common` (для `BiosStatus`, `ProcessorFault`, `OpCode`, `RegType`). `Kernel` зависит от `Contracts`, `VmApplication` — от `Kernel`, `Extensions` — от `Contracts`/`Common`. `Contracts` — единственная точка, где эти три слоя сходятся без циклических ссылок.

Ключевые роли модуля:
- Шина портов: `IPortBus`, `IPortUse`, `IPortController`, `ReadPortResult`.
- Процессор: `ProcessorSnapshot`, `IProcessorFaultPolicy` (с кэшированным `Default`).
- Оптимизации: `OptimizationId`, `OptimizationLogEntry`, `IReadOnlyLogOptimization`.
- Результаты замеров: `ITimerSimulationResult`.
- Устройства: `DeviceInfo`, `IReadOnlyNativeBuffer`.

Проект — листовой по зависимостям (только `Common`), не содержит `unsafe`, `NativeMemory`, `Regex` и рефлексии.

> **XML-документация почти полная.** Документированы `ITimerSimulationResult`, `OptimizationId`, `IReadOnlyNativeBuffer`, `IPortBus`, `IPortUse`, `IPortController`, `ProcessorSnapshot`, `IReadOnlyLogOptimization`, `IProcessorFaultPolicy`. Все тривиальные DTO (`OptimizationLogEntry`, `ReadPortResult`, `DeviceInfo`) тоже имеют `///` на структуре и `<param>` на параметрах. Оставшиеся пробелы — семантика (поведение при ошибках, порядок `Dispose`, валидные значения `RegType`) и одно фактическое обещание в XML `OptimizationId`, не подкреплённое кодом. Подробности — в разделе [Что исправить](#что-исправить).

## Дерево локального проекта

Все типы живут в namespace `Nantuc.Contracts`, структура плоская:

```
Nantuc.Contracts/
├── Nantuc.Contracts.csproj    # net10.0, ProjectReference: Common
├── DeviceInfo.cs                     # readonly record struct DeviceInfo
├── IReadOnlyNativeBuffer.cs          # interface IReadOnlyNativeBuffer
├── IPortBus.cs                       # interface IPortBus
├── IPortController.cs                # interface IPortController : IPortUse, IDisposable
├── IPortUse.cs                       # interface IPortUse
├── IProcessorFaultPolicy.cs          # interface IProcessorFaultPolicy + nested DefaultProcessorFaultPolicy
├── ProcessorSnapshot.cs              # readonly struct ProcessorSnapshot, [InlineArray(32)]
├── IReadOnlyLogOptimization.cs       # interface IReadOnlyLogOptimization
├── ITimerSimulationResult.cs         # interface ITimerSimulationResult
├── OptimizationId.cs                 # readonly struct OptimizationId
├── OptimizationLogEntry.cs           # readonly record struct OptimizationLogEntry
└── ReadPortResult.cs                 # readonly record struct ReadPortResult
```

Замечания по организации:
- Файлы `OptimizationId.cs` и `OptimizationLogEntry.cs` — имя файла соответствует имени типа (ранее было `TypeOptimization.cs` с типом `OptimizationId` — теперь переименовано).
- `IPortController.cs` — однострочный, но с `///` и `<see>`-ссылками на `IPortUse`/`IDisposable`.
- `IProcessorFaultPolicy` содержит `private class DefaultProcessorFaultPolicy` — реализация политики живёт внутри интерфейса. Это нестандартно для контрактов, но осознанно: «политика по умолчанию» — часть контракта.
- `ProcessorSnapshot` — `readonly struct` с `[InlineArray(RegisterCount)] (RegisterCount = 32)` для хранения копии регистров. Два конструктора:
    - `(Span<ulong> span, bool isRunning, bool isSleeping, uint rawInstruction, ulong dataByInstruction)` — `CurrentBiosStatus = BiosStatus.Success`;
    - `(..., BiosStatus currentBiosStatus)` — то же плюс явный статус.
    - `GetArr(ReadOnlySpan<ulong>)` копирует ровно RegisterCount = 32 элементов и бросает `ArgumentException` при `span.Length` ≠ 32 (раньше — `IndexOutOfRangeException` при > 32 и молчаливые нули при < 32).
## API

### Общая картина

В модуле — 7 `interface` и 5 `struct`/`record struct`. Публичная поверхность:

| Тип | Вид | Назначение |
|---|---|---|
| `ITimerSimulationResult` | `interface` | `Elapsed`, `Steps` |
| `IPortBus` | `interface` | `ReadPort(ulong)`, `WritePort(ulong, byte)`, `WakeProcessor(ulong)` |
| `IPortUse` | `interface` | `ReadPort(ulong)`, `WritePort(ulong, byte)`, `WakeProcessor()` |
| `IPortController` | `interface : IPortUse, IDisposable` | Композиция «устройство на шине + освобождение» |
| `ReadPortResult` | `readonly record struct` | `(byte Data, BiosStatus Status = Success)` |
| `ProcessorSnapshot` | `readonly struct` | `IsRunning`, `IsSleeping`, `GetRegister(RegType)`; `[InlineArray(RegisterCount)]`-хранилище (32); `RawInstruction`, `DataByInstruction`, `CurrentBiosStatus`, `DataFault`; декодированные из `RawInstruction`: `OpCode`, `FirstRegister`, `SecondRegister`, `DataSizeCode`|
| `IProcessorFaultPolicy` | `interface` + `Default` | `ShouldContinue(in ProcessorFault)`; `Default` — кэшированный singleton |
| `IReadOnlyLogOptimization` | `interface` | `Id`, `GetLogs()` |
| `OptimizationId` | `readonly struct` | Валидированный идентификатор `[a-z0-9.-]{1,128}` |
| `OptimizationLogEntry` | `readonly record struct` | `(int Pass, string Message)` |
| `DeviceInfo` | `readonly record struct` | `(int Id, uint Sector, string? Name)` |
| `IReadOnlyNativeBuffer` | `interface` | `Length: nuint`, `this[nuint]: byte` |

### Шина портов

**`IPortBus`** — шина портов «только для I/O»: без регистраций, без валидаторов. Адреса — `ulong`. `ReadPort` возвращает `ReadPortResult` (данные + статус), `WritePort` возвращает `BiosStatus`, `WakeProcessor` — `bool`. XML прямо инструктирует: «сначала проверяйте `Status`, потом `Data`».

**`IPortUse`** — контракт устройства на шине. Отличие от `IPortBus`: `ReadPort` возвращает просто `byte` без статуса, `WakeProcessor()` — без адреса (пробуждает сам себя). Смещения — относительно начала сектора устройства.

**`IPortController`** — композиция `IPortUse + IDisposable`, маркер «устройство с ресурсами». XML есть, но порядок `Dispose` относительно `WakeProcessor`/операций шины не зафиксирован.

**`ReadPortResult`** — `readonly record struct (byte Data, BiosStatus Status = Success)`. Дефолт `Status = Success` означает: `new ReadPortResult(data)` даст «успех», даже если данные пришли из ошибки. XML на `<param>` есть, но предупреждения о дефолте нет (см. [Что исправить](#что-исправить), п. 4).

### Процессор

`ProcessorSnapshot` — `readonly struct` с `[InlineArray(RegisterCount)] (RegisterCount = 32)`-хранилищем регистров. Два конструктора:
- `(Span<ulong> span, bool isRunning, bool isSleeping, uint rawInstruction, ulong dataByInstruction)`— `CurrentBiosStatus` = `BiosStatus.Success`;
- `(Span<ulong> span, bool isRunning, bool isSleeping, uint rawInstruction, ulong dataByInstruction, BiosStatus currentBiosStatus)`.
`GetArr(ReadOnlySpan<ulong>)` копирует ровно `RegisterCount` элементов; при `span.Length` ≠ 32 — `ArgumentException (см. /// <exception> в коде)`. Ранее при > 32 был `IndexOutOfRangeException`, при < 32 — молчаливые нули в хвосте массива.

Свойства:

- `IsRunning`, `IsSleeping `— режим процессора на момент снимка.
- `GetRegister(RegType)` — значение регистра; индексация без проверки диапазона, поведение для невалидного RegType не задокументировано (см. Что исправить, п. 9).
- `RawInstruction `— сырая инструкция (32 бита), `DataByInstruction` — 64-битные данные, пришедшие вместе с инструкцией.
- `CurrentBiosStatus `— BiosStatus на момент снимка (в упрощённом конструкторе — `Success`).
- `DataFault `— `DataByInstruction`, если `CurrentBiosStatus != Success`, иначе 0.

Декодированные из `RawInstruction` через `InstructionDecoder`: `OpCode`, `FirstRegister`, `SecondRegister`, `DataSizeCode`.

Записи в `ProcessorSnapshot` нет — она в `Kernel`; здесь только чтение снимка.

**`IProcessorFaultPolicy`** — политика остановки процессора при `ProcessorFault`:

```csharp
public interface IProcessorFaultPolicy
{
    public static IProcessorFaultPolicy Default { get; } = new DefaultProcessorFaultPolicy();

    private class DefaultProcessorFaultPolicy : IProcessorFaultPolicy
    {
        public bool ShouldContinue(in ProcessorFault fault) => fault.Status switch
        {
            BiosStatus.Success => true,
            BiosStatus.NullDeviceOutput => true,
            _ => false,
        };
    }

    bool ShouldContinue(in ProcessorFault fault);
}
```

Семантика `Default`: продолжать при `Success` и `NullDeviceOutput`, останавливаться на любом другом статусе. `NullDeviceInput` не входит в «продолжать» — асимметрично (см. [Что исправить](#что-исправить), п. 2). `Default` — **статическое свойство с кэшированным экземпляром** (static initializer, создаётся один раз при первом обращении к типу интерфейса).

### Оптимизации

**`OptimizationId`** — `readonly struct` с валидацией. Принимает только `[a-z0-9.-]`, длина 1..128. Конструктор бросает на невалидном входе, `TryParse` — не бросает (для парсинга пользовательского ввода). `default(OptimizationId)` — единственное невалидное значение (`IsValid == false`, `Value == null`). Сравнение — `StringComparison.Ordinal` (`Equals`, `CompareTo` через `String.CompareOrdinal`). Культура не влияет — WASM-safe.

**`OptimizationLogEntry`** — `readonly record struct (int Pass, string Message)`. XML на структуре и `<param>` на `Pass`/`Message` есть. Формулировка summary («Оптимизационный лог с защитой от перезаписи новыми логами») описывает мотивацию, а не сам тип (см. [Что исправить](#что-исправить), п. 6).

**`IReadOnlyLogOptimization`** — `Id: OptimizationId`, `GetLogs(): IReadOnlyList<OptimizationLogEntry>`. Комментарий «снимок» остался inline (`// снимок`), не в `///` (см. [Что исправить](#что-исправить), п. 3).

### Устройства и результаты

**`DeviceInfo`** — `readonly record struct (int Id, uint Sector, string? Name)`. XML на структуре и `<param>` есть. `Name` — `string?`; в `Common` есть `NameDeviceToken` (кэш строки, `IsUnknown`, Ordinal-сравнение) — выбор `string?` не задокументирован (см. [Что исправить](#что-исправить), п. 7).

**`IReadOnlyNativeBuffer`** — read-only буфер: `Length: nuint`, `this[nuint]: byte`. Несмотря на «Native» в имени, сам интерфейс — managed. Реализация может быть над `NativeMemory`, но это забота реализующего слоя (см. [WASM](#wasm--кроссплатформенность)).

**`ITimerSimulationResult`** — `Elapsed: TimeSpan`, `Steps: long`. Read-only снимок замеров.

## Примеры для API

**Чтение/запись через шину портов:**

```csharp
using Nantuc.Common;
using Nantuc.Contracts;

void WriteByte(IPortBus bus, ulong address, byte value)
{
    BiosStatus status = bus.WritePort(address, value);
    if (status != BiosStatus.Success)
        Console.Error.WriteLine($"write failed: {status}");
}

byte ReadByteChecked(IPortBus bus, ulong address)
{
    ReadPortResult r = bus.ReadPort(address);
    if (r.Status != BiosStatus.Success)
        throw new InvalidOperationException($"read failed: {r.Status}");
    return r.Data;
}
```

**Реализация устройства на шине:**

```csharp
public sealed class UartDevice : IPortController
{
    public byte ReadPort(ulong offset) => offset switch
    {
        0 => 0x00, // RX (нет данных)
        _ => 0x00,
    };

    public void WritePort(ulong offset, byte value)
    {
        if (offset == 1) Console.Write((char)value); // TX
    }

    public void WakeProcessor() { /* ничего */ }

    public void Dispose() { /* освободить ресурсы */ }
}
```

**Проверка политики после падения:**

```csharp
IProcessorFaultPolicy policy = IProcessorFaultPolicy.Default;

ProcessorFault fault = new(
    Status: BiosStatus.DivOnZero,
    FaultData: 0,
    InstructionPointer: 0x40,
    OpCode: OpCode.DIV);

if (!policy.ShouldContinue(in fault))
    Console.Error.WriteLine($"stop: {fault.Status}");
```

**Работа с идентификатором оптимизации:**

```csharp
// Валидный id
var id = new OptimizationId("peephole");
Console.WriteLine(id.Value);      // "peephole"
Console.WriteLine(id.IsValid);    // true

// Парсинг пользовательского ввода
if (OptimizationId.TryParse("inline.func", out var parsed))
    Console.WriteLine(parsed);
else
    Console.Error.WriteLine("invalid id");

// default — единственное невалидное значение
OptimizationId invalid = default;
Console.WriteLine(invalid.IsValid);          // false
Console.WriteLine(invalid);                   // "OptimizationId(invalid)"
```

**Снимок логов оптимизации:**

```csharp
IReadOnlyLogOptimization log = /* ... */;
OptimizationId id = log.Id;
IReadOnlyList<OptimizationLogEntry> entries = log.GetLogs();

foreach (OptimizationLogEntry e in entries)
    Console.WriteLine($"[pass {e.Pass}] {e.Message}");
```

## Приближенность модуля к пользователю

Средняя. Контракты — это то, что видит автор расширений и пользователь `VmApplication`, но не то, что пишет автор программы на MiniC.

- Автор расширений (кастомное устройство, дизассемблер, отладчик) реализует `IPortUse`/`IPortController`/`IReadOnlyNativeBuffer` — и это его основной вход в платформу.
- Пользователь `VmApplication`, который хочет читать состояние процессора, использует `ProcessorSnapshot` и `IProcessorFaultPolicy`.
- Автор компилятора оптимизаций работает с `OptimizationId`/`OptimizationLogEntry`/`IReadOnlyLogOptimization`.
- Автор программы на MiniC/NANASM этот модуль не импортирует — он работает через `VmApplication`.

**Что стоит делать:**
- Реализовывать `IPortUse` (а не `IPortBus`) для собственных устройств; `IPortBus` — это шина, не устройство.
- Читать `ReadPortResult.Status` первым делом, `Data` — только если `Success`.
- Использовать `OptimizationId.TryParse` для пользовательского ввода и конструктор — для заведомо валидных констант.
- Возвращать `in ProcessorFault` из ядра и принимать его `in` в `ShouldContinue` — без копирования.
- `IProcessorFaultPolicy.Default` — кэшированный singleton (static initializer); для production с особыми требованиями к политике — своя реализация.

**Что не стоит делать:**
- Возвращать `new ReadPortResult(data)` без явного `Status` — дефолт `Success` замаскирует ошибку.
- Реализовывать `IPortBus` в своём коде без реальной регистрации устройств — это не «устройство», а «шина».
- Полагаться на `Default`-политику в production без понимания асимметрии `NullDeviceOutput` vs `NullDeviceInput`.
- Кэшировать `IReadOnlyLogOptimization.GetLogs()` — это снимок по контракту (см. `<remarks>` в реализации, не в интерфейсе).
- Считать `Default` «новым экземпляром на каждый вызов» — это кэшированный singleton.

## Особенная информация

### WASM / кроссплатформенность

| Аспект | Статус | Комментарий |
|---|---|---|
| Сборка в WASM | ✅ | Нет `unsafe`, `DllImport`, `LibraryImport`. Все типы — `interface`/`readonly struct`. Собирается под `browser-wasm` без правок csproj. |
| `stackalloc` / `InlineArray` | ✅ | `ProcessorSnapshot` использует `[InlineArray(RegisterCount)] (const int RegisterCount = 32)` — compile-time, AOT-friendly. `stackalloc` в `CurrentProcessorSnapshot` (в `Kernel`) — 32 × 8 байт. Валидация длины (`ArgumentException` из `GetArr`) — `managed`, без `unsafe`. | 
| `Interlocked` / `Volatile` | ✅ | Не используются. `IProcessorFaultPolicy.Default` — `static`-свойство с кэшированным экземпляром (static initializer). |
| `Regex` / trimming | ✅ | Regex не используется; нет рефлексии, `Type.GetType`, `Activator.CreateInstance`, `dynamic`. `OptimizationId` валидирует строку посимвольно (`foreach (char c in value)`), без `Regex`. Trimming-совместимо. |
| AOT | ✅ | Нет `Expression.Compile`, `MakeGenericMethod`/`MakeGenericType`. `in ProcessorFault` — AOT-friendly. |

**Итог:** контракты полностью совместимы с WASM (interpreter и AOT) и trimming.

### Формат `OptimizationId`

- Допустимые символы: `[a-z0-9.-]`. Только нижний регистр ASCII; `_`, `:`, `/`, пробелы — запрещены.
- Длина: 1..`MaxLength = 128`.
- `default(OptimizationId)` — единственное невалидное значение; конструктор `new OptimizationId(...)` всегда даёт валидный экземпляр (или бросает).
- Сравнение — `StringComparison.Ordinal` / `String.CompareOrdinal`; culture-independent.
- Персистентность: XML-документация утверждает, что well-known id стабильны и их можно писать в логи/кэши/конфиги. В коде **статических полей с well-known id нет** — только сам механизм (см. [Что исправить](#что-исправить), п. 1).

### `IProcessorFaultPolicy` как контракт

- `Default` — статическое свойство с **кэшированным экземпляром** `DefaultProcessorFaultPolicy` (static initializer). Создаётся один раз при первом обращении к типу `IProcessorFaultPolicy`; повторные чтения `Default` возвращают тот же экземпляр.
- `DefaultProcessorFaultPolicy.ShouldContinue` возвращает `true` для `Success` и `NullDeviceOutput`. `NullDeviceInput` — `false`. Если `NullDeviceInput` семантически «нет устройства ввода, но это не падение», поведение `Default` расходится с ожиданиями (см. [Что исправить](#что-исправить), п. 2).
- Приватный вложенный класс — нестандартное для контрактов решение, но допустимое: «политика по умолчанию» — часть контракта, а не отдельная реализация в `Kernel`.

### `IPortBus` vs `IPortUse`

- `IPortBus.ReadPort(ulong address)` → `ReadPortResult (Data, Status)` — адрес на шине, статус возвращается.
- `IPortUse.ReadPort(ulong offset)` → `byte` — смещение внутри сектора устройства, статуса нет. Ошибки — либо исключение, либо `0` (не задокументировано).
- `IPortBus.WritePort(ulong, byte)` → `BiosStatus` — ошибка возвращается.
- `IPortUse.WritePort(ulong, byte)` → `void` — ошибки не возвращаются; реализация либо бросает, либо глотает.
- Асимметрия по дизайну: шина знает об ошибках, устройство — нет. Потребитель должен понимать, на каком уровне работает.

### Endianness

В `Nantuc.Contracts` не определён. `OptimizationId` — символьный, к endianness не относится. `ReadPortResult`/`DeviceInfo` — уже распакованные значения. Порядок байт на проводе — забота `Kernel`.

### Ограничения платформ

- Целевой framework: `net10.0`. C# 14 не требуется (в этой партии нет `extension`-блоков), но для консистентности с `Common` — оставлено.
- `unsafe` в этом модуле нет.
- `Nullable=enable`, `ImplicitUsings=enable`.
- `[DebuggerDisplay]` есть только на `OptimizationId`. На `ReadPortResult`/`OptimizationLogEntry`/`DeviceInfo` — нет; `record struct` генерирует нормальный `ToString()` из коробки.

### Что этот модуль не делает

- Не содержит реализаций — только интерфейсы и DTO. Единственное исключение — `private class DefaultProcessorFaultPolicy` внутри `IProcessorFaultPolicy` (это «политика по умолчанию», часть контракта).
- Не содержит состояния, кэшей, счётчиков.
- Не знает про `ManagerDevices`, `MemoryBus`, `Processor` — только про `BiosStatus`/`ProcessorFault`/`RegType`/`OpCode` из `Common`.
- Не реализует `IReadOnlyNativeBuffer` над `NativeMemory` — только объявляет интерфейс; реализация — в слое, который знает про `NativeMemory`.

Правило «куда положить тип»: примитив без поведения → `Common`; интерфейс/контракт → `Contracts`; всё, что исполняет или хранит состояние → `Kernel`.