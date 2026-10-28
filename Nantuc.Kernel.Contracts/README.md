# Nantuc.Kernel.Contracts — README

> Контракты платформы Nantuc: интерфейсы шины портов, процессора, политик падения и оптимизаций.  
> Единственный слой, от которого зависят и `Kernel`, и `VmApplication`, и `Extensions`.  
> `[← Корень](../README.md) · [Common](../Nantuc.Kernel.Common/README.md) · [Kernel](../Nantuc.Kernel/README.md)`

## Содержание
- [Об этом модуле](#об-этом-модуле)
- [Дерево локального проекта](#дерево-локального-проекта)
- [API](#api)
- [Примеры для API](#примеры-для-api)
- [Приближенность модуля к пользователю](#приближенность-модуля-к-пользователю)
- [Особенная информация](#особенная-информация)
- [Что исправить](#что-исправить)

## Об этом модуле

`Nantuc.Kernel.Contracts` — это слой абстракций платформы. Здесь нет ни логики процессора, ни шин, ни устройств, ни состояния: только `interface`-ы, `readonly record struct`-ы для результатов и один `readonly struct` с валидацией (`OptimizationId`). Всё, что должен уметь вызывать пользователь ядра, объявлено здесь; реализация живёт в `Kernel`/`VmApplication`/`Extensions`.

Модуль зависит только от `Nantuc.Kernel.Common` (для `BiosStatus`, `ProcessorFault`, `OpCode`, `RegType`). `Kernel` зависит от `Contracts`, `VmApplication` — от `Kernel`, `Extensions` — от `Contracts`/`Common`. `Contracts` — единственная точка, где эти три слоя сходятся без циклических ссылок.

Ключевые роли модуля:
- Шина портов: `IPortBus`, `IPortUse`, `IPortController`, `ReadPortResult`.
- Процессор: `IProcessorReader`, `IProcessorFaultPolicy` (с `Default`).
- Оптимизации: `OptimizationId`, `OptimizationLogEntry`, `IReadOnlyLogOptimization`.
- Результаты замеров: `ITimerSimulationResult`.
- Устройства: `DeviceInfo`, `INativeReadOnlyBuffer`.

Проект — листовой по зависимостям (только `Common`), не содержит `unsafe`, `NativeMemory`, `Regex` и рефлексии.

> **XML-документация почти полная.** Документированы `ITimerSimulationResult`, `OptimizationId`, `INativeReadOnlyBuffer`, `IPortBus`, `IPortUse`, `IPortController`, `IProcessorReader`, `IReadOnlyLogOptimization`, `IProcessorFaultPolicy`. Все тривиальные DTO (`OptimizationLogEntry`, `ReadPortResult`, `DeviceInfo`) теперь тоже имеют `///` на структуре и `<param>` на параметрах. Оставшиеся пробелы — семантика (поведение при ошибках, порядок `Dispose`, валидные значения `RegType`) и одно фактическое обещание в XML `OptimizationId`, не подкреплённое кодом. Подробности — в разделе [Что исправить](#что-исправить).

## Дерево локального проекта

Все типы живут в namespace `Nantuc.Kernel.Contracts`, структура плоская:

```
Nantuc.Kernel.Contracts/
├── Nantuc.Kernel.Contracts.csproj    # net10.0, ProjectReference: Common
├── DeviceInfo.cs                     # readonly record struct DeviceInfo
├── INativeReadOnlyBuffer.cs          # interface INativeReadOnlyBuffer
├── IPortBus.cs                       # interface IPortBus
├── IPortController.cs                # interface IPortController : IPortUse, IDisposable
├── IPortUse.cs                       # interface IPortUse
├── IProcessorFaultPolicy.cs          # interface IProcessorFaultPolicy + nested DefaultProcessorFaultPolicy
├── IProcessorReader.cs               # interface IProcessorReader
├── IReadOnlyLogOptimization.cs       # interface IReadOnlyLogOptimization
├── ITimerSimulationResult.cs         # interface ITimerSimulationResult
├── OptimizationLogEntry.cs           # readonly record struct OptimizationLogEntry
├── ReadPortResult.cs                 # readonly record struct ReadPortResult
└── TypeOptimization.cs               # readonly struct OptimizationId (имя файла ≠ имя типа!)
```

Замечания по организации:
- Файл `TypeOptimization.cs` содержит тип `OptimizationId`. Имя файла не соответствует имени типа — навигация страдает (см. [Что исправить](#что-исправить), п. 1).
- `IPortController.cs` — однострочный, но теперь с `///` и `<see>`-ссылками на `IPortUse`/`IDisposable`.
- `IProcessorFaultPolicy` содержит `private class DefaultProcessorFaultPolicy` — реализация политики живёт внутри интерфейса. Это нестандартно для контрактов, но осознанно: «политика по умолчанию» — часть контракта.

## API

### Общая картина

В модуле — 11 `interface` и 4 `struct`/`record struct`. Публичная поверхность:

| Тип | Вид | Назначение |
|---|---|---|
| `ITimerSimulationResult` | `interface` | `Elapsed`, `Steps` |
| `IPortBus` | `interface` | `ReadPort(ulong)`, `WritePort(ulong, byte)`, `WakeProcessor(ulong)` |
| `IPortUse` | `interface` | `ReadPort(ulong)`, `WritePort(ulong, byte)`, `WakeProcessor()` |
| `IPortController` | `interface : IPortUse, IDisposable` | Композиция «устройство на шине + освобождение» |
| `ReadPortResult` | `readonly record struct` | `(byte Data, BiosStatus Status = Success)` |
| `IProcessorReader` | `interface` | `IsRunning`, `IsSleeping`, `GetRegister(RegType)` |
| `IProcessorFaultPolicy` | `interface` + `Default` | `ShouldContinue(in ProcessorFault)` |
| `IReadOnlyLogOptimization` | `interface` | `Id`, `GetLogs()` |
| `OptimizationId` | `readonly struct` | Валидированный идентификатор `[a-z0-9.-]{1,128}` |
| `OptimizationLogEntry` | `readonly record struct` | `(int Pass, string Message)` |
| `DeviceInfo` | `readonly record struct` | `(int Id, uint Sector, string? Name)` |
| `INativeReadOnlyBuffer` | `interface` | `Length: nuint`, `this[nuint]: byte` |

### Шина портов

**`IPortBus`** — шина портов «только для I/O»: без регистраций, без валидаторов. Адреса — `ulong`. `ReadPort` возвращает `ReadPortResult` (данные + статус), `WritePort` возвращает `BiosStatus`, `WakeProcessor` — `bool`. XML прямо инструктирует: «сначала проверяйте `Status`, потом `Data`».

**`IPortUse`** — контракт устройства на шине. Отличие от `IPortBus`: `ReadPort` возвращает просто `byte` без статуса, `WakeProcessor()` — без адреса (пробуждает сам себя). Смещения — относительно начала сектора устройства.

**`IPortController`** — композиция `IPortUse + IDisposable`, маркер «устройство с ресурсами». XML есть, но порядок `Dispose` относительно `WakeProcessor`/операций шины не зафиксирован.

**`ReadPortResult`** — `readonly record struct (byte Data, BiosStatus Status = Success)`. Дефолт `Status = Success` означает: `new ReadPortResult(data)` даст «успех», даже если данные пришли из ошибки. XML на `<param>` есть, но предупреждения о дефолте нет (см. [Что исправить](#что-исправить), п. 6).

### Процессор

**`IProcessorReader`** — только чтение состояния: `IsRunning`, `IsSleeping`, `GetRegister(RegType)`. Записи нет — она в `Kernel`. Поведение `GetRegister` для невалидного `RegType` (`(RegType)200`) не задокументировано.

**`IProcessorFaultPolicy`** — политика остановки процессора при `ProcessorFault`:

```csharp
public interface IProcessorFaultPolicy
{
    public static IProcessorFaultPolicy Default => new DefaultProcessorFaultPolicy();

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

Семантика `Default`: продолжать при `Success` и `NullDeviceOutput`, останавливаться на любом другом статусе. `NullDeviceInput` не входит в «продолжать» — асимметрично (см. [Что исправить](#что-исправить), п. 4). `Default` создаёт **новый экземпляр** на каждое чтение — не кэширован (см. [Что исправить](#что-исправить), п. 3).

### Оптимизации

**`OptimizationId`** — `readonly struct` с валидацией. Принимает только `[a-z0-9.-]`, длина 1..128. Конструктор бросает на невалидном входе, `TryParse` — не бросает (для парсинга пользовательского ввода). `default(OptimizationId)` — единственное невалидное значение (`IsValid == false`, `Value == null`). Сравнение — `StringComparison.Ordinal` (`Equals`, `CompareTo` через `String.CompareOrdinal`). Культура не влияет — WASM-safe.

**`OptimizationLogEntry`** — `readonly record struct (int Pass, string Message)`. XML на структуре и `<param>` на `Pass`/`Message` есть. Формулировка summary («Оптимизационный лог с защитой от перезаписи новыми логами») описывает мотивацию, а не сам тип (см. [Что исправить](#что-исправить), п. 8).

**`IReadOnlyLogOptimization`** — `Id: OptimizationId`, `GetLogs(): IReadOnlyList<OptimizationLogEntry>`. Комментарий «снимок» остался inline (`// снимок`), не в `///` (см. [Что исправить](#что-исправить), п. 5).

### Устройства и результаты

**`DeviceInfo`** — `readonly record struct (int Id, uint Sector, string? Name)`. XML на структуре и `<param>` есть. `Name` — `string?`; в `Common` есть `NameDeviceToken` (кэш строки, `IsUnknown`, Ordinal-сравнение) — выбор `string?` не задокументирован (см. [Что исправить](#что-исправить), п. 9).

**`INativeReadOnlyBuffer`** — read-only буфер: `Length: nuint`, `this[nuint]: byte`. Несмотря на «Native» в имени, сам интерфейс — managed. Реализация может быть над `NativeMemory`, но это забота реализующего слоя (см. [WASM](#wasm--кроссплатформенность)).

**`ITimerSimulationResult`** — `Elapsed: TimeSpan`, `Steps: long`. Read-only снимок замеров.

## Примеры для API

**Чтение/запись через шину портов:**

```csharp
using Nantuc.Kernel.Common;
using Nantuc.Kernel.Contracts;

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

- Автор расширений (кастомное устройство, дизассемблер, отладчик) реализует `IPortUse`/`IPortController`/`INativeReadOnlyBuffer` — и это его основной вход в платформу.
- Пользователь `VmApplication`, который хочет читать состояние процессора, использует `IProcessorReader` и `IProcessorFaultPolicy`.
- Автор компилятора оптимизаций работает с `OptimizationId`/`OptimizationLogEntry`/`IReadOnlyLogOptimization`.
- Автор программы на MiniC/NANASM этот модуль не импортирует — он работает через `VmApplication`.

**Что стоит делать:**
- Реализовывать `IPortUse` (а не `IPortBus`) для собственных устройств; `IPortBus` — это шина, не устройство.
- Читать `ReadPortResult.Status` первым делом, `Data` — только если `Success`.
- Использовать `OptimizationId.TryParse` для пользовательского ввода и конструктор — для заведомо валидных констант.
- Возвращать `in ProcessorFault` из ядра и принимать его `in` в `ShouldContinue` — без копирования.
- `IProcessorFaultPolicy.Default` — только для быстрого прототипа; для production — своя реализация (см. [Что исправить](#что-исправить), п. 3).

**Что не стоит делать:**
- Возвращать `new ReadPortResult(data)` без явного `Status` — дефолт `Success` замаскирует ошибку.
- Реализовывать `IPortBus` в своём коде без реальной регистрации устройств — это не «устройство», а «шина».
- Полагаться на `Default`-политику в production: она продолжает на `NullDeviceOutput`, но не на `NullDeviceInput`.
- Кэшировать `IReadOnlyLogOptimization.GetLogs()` — это снимок по контракту (см. `<remarks>` в реализации, не в интерфейсе).

## Особенная информация

### WASM / кроссплатформенность

| Аспект | Статус | Комментарий |
|---|---|---|
| Сборка в WASM | ✅ | Нет `unsafe`, `DllImport`, `LibraryImport`. Все типы — `interface`/`readonly struct`. Собирается под `browser-wasm` без правок csproj. |
| `NativeMemory` | n/a | В этом проекте не используется. `INativeReadOnlyBuffer` — managed-интерфейс; риск появится только в реализации (в другом слое). |
| `stackalloc` / `InlineArray` | ✅ | Не используются. |
| `Interlocked` / `Volatile` | ✅ | Не используются. `IProcessorFaultPolicy.Default` — `static`-свойство, но не кэширует экземпляр (см. п. 3 ниже). |
| Реализация `IFileService` | n/a | Интерфейса в этой партии нет. Если появится в другом слое — WASM-реализация будет отдельной. |
| `Regex` / trimming | ✅ | Regex не используется; нет рефлексии, `Type.GetType`, `Activator.CreateInstance`, `dynamic`. `OptimizationId` валидирует строку посимвольно (`foreach (char c in value)`), без `Regex`. Trimming-совместимо. |
| AOT | ✅ | Нет `Expression.Compile`, `MakeGenericMethod`/`MakeGenericType`. `in ProcessorFault` — AOT-friendly. |

**Итог:** контракты полностью совместимы с WASM (interpreter и AOT) и trimming. Единственная зона риска — `IProcessorFaultPolicy.Default` аллоцирует новый экземпляр на каждое чтение (не WASM-специфично, но лишний GC-pressure).

### Формат `OptimizationId`

- Допустимые символы: `[a-z0-9.-]`. Только нижний регистр ASCII; `_`, `:`, `/`, пробелы — запрещены.
- Длина: 1..`MaxLength = 128`.
- `default(OptimizationId)` — единственное невалидное значение; конструктор `new OptimizationId(...)` всегда даёт валидный экземпляр (или бросает).
- Сравнение — `StringComparison.Ordinal` / `String.CompareOrdinal`; culture-independent.
- Персистентность: XML-документация утверждает, что well-known id стабильны и их можно писать в логи/кэши/конфиги. В коде **статических полей с well-known id нет** — только сам механизм (см. [Что исправить](#что-исправить), п. 2).

### `IProcessorFaultPolicy` как контракт

- `Default` возвращает `new DefaultProcessorFaultPolicy()` — новый экземпляр на каждое обращение. Это не singleton, но `DefaultProcessorFaultPolicy` — `private class` без состояния, поэтому «работает». Для production — либо кэшировать, либо реализовывать свою политику.
- `DefaultProcessorFaultPolicy.ShouldContinue` возвращает `true` для `Success` и `NullDeviceOutput`. `NullDeviceInput` — `false`. Если `NullDeviceInput` семантически «нет устройства ввода, но это не падение», поведение `Default` расходится с ожиданиями (см. [Что исправить](#что-исправить), п. 4).
- Приватный вложенный класс — нестандартное для контрактов решение, но допустимое: «политика по умолчанию» — часть контракта, а не отдельная реализация в `Kernel`.

### `IPortBus` vs `IPortUse`

- `IPortBus.ReadPort(ulong address)` → `ReadPortResult (Data, Status)` — адрес на шине, статус возвращается.
- `IPortUse.ReadPort(ulong offset)` → `byte` — смещение внутри сектора устройства, статуса нет. Ошибки — либо исключение, либо `0` (не задокументировано).
- `IPortBus.WritePort(ulong, byte)` → `BiosStatus` — ошибка возвращается.
- `IPortUse.WritePort(ulong, byte)` → `void` — ошибки не возвращаются; реализация либо бросает, либо глотает.
- Асимметрия по дизайну: шина знает об ошибках, устройство — нет. Потребитель должен понимать, на каком уровне работает.

### Endianness

В `Nantuc.Kernel.Contracts` не определён. `OptimizationId` — символьный, к endianness не относится. `ReadPortResult`/`DeviceInfo` — уже распакованные значения. Порядок байт на проводе — забота `Kernel`.

### Ограничения платформ

- Целевой framework: `net10.0`. C# 14 не требуется (в этой партии нет `extension`-блоков), но для консистентности с `Common` — оставлено.
- `unsafe` в этом модуле нет.
- `Nullable=enable`, `ImplicitUsings=enable`.
- `[DebuggerDisplay]` есть только на `OptimizationId`. На `ReadPortResult`/`OptimizationLogEntry`/`DeviceInfo` — нет; `record struct` генерирует нормальный `ToString()` из коробки.

### Что этот модуль не делает

- Не содержит реализаций — только интерфейсы и DTO. Единственное исключение — `private class DefaultProcessorFaultPolicy` внутри `IProcessorFaultPolicy` (это «политика по умолчанию», часть контракта).
- Не содержит состояния, кэшей, счётчиков.
- Не знает про `ManagerDevices`, `MemoryBus`, `Processor` — только про `BiosStatus`/`ProcessorFault`/`RegType`/`OpCode` из `Common`.
- Не реализует `INativeReadOnlyBuffer` над `NativeMemory` — только объявляет интерфейс; реализация — в слое, который знает про `NativeMemory`.

Правило «куда положить тип»: примитив без поведения → `Common`; интерфейс/контракт → `Contracts`; всё, что исполняет или хранит состояние → `Kernel`.

## Что исправить

По убыванию важности. Пункты с **[API]** — кандидаты на изменение публичной поверхности; **[XML]** — приоритеты документации; **[Дом]** — вопросы организации кода.


4. **[API] `DefaultProcessorFaultPolicy` — асимметрия `NullDeviceOutput` vs `NullDeviceInput`.**  
   Файл: `IProcessorFaultPolicy.cs`. `ShouldContinue` возвращает `true` для `NullDeviceOutput`, но `false` для `NullDeviceInput`. Семантически «нет устройства вывода» и «нет устройства ввода» — симметричные ситуации; текущая политика молча останавливает VM при отсутствии ввода, но продолжает при отсутствии вывода.  
   Фикс: либо добавить `BiosStatus.NullDeviceInput => true`, либо явно задокументировать в `<remarks>` на `DefaultProcessorFaultPolicy.ShouldContinue`, что это намеренное решение.

5. **[XML] `IReadOnlyLogOptimization.GetLogs` — «снимок» только в inline-комментарии.**  
   Файл: `IReadOnlyLogOptimization.cs`, `IReadOnlyList<OptimizationLogEntry> GetLogs();  // снимок`. Inline-комментарий есть, `/// <remarks>` о том, что возвращается копия (и что `IReadOnlyList` — это именно снимок, а не live view) — нет. Плюс: аллокация на каждый вызов не задокументирована.  
   Фикс: `/// <remarks>Returns a snapshot; the returned list is a new allocation on each call and is not updated as new entries are appended.</remarks>`.

6. **[API] `ReadPortResult` — дефолт `Status = Success` замаскирует ошибку.**  
   Файл: `ReadPortResult.cs`. `new ReadPortResult(data)` даёт `Status = Success`. XML на `<param>` есть, но предупреждения о дефолте нет. Если реализация `IPortBus.ReadPort` забыла проставить статус — ошибка уйдёт в прод молча.  
   Фикс: либо убрать дефолт (`Status` обязателен в конструкторе), либо добавить фабрики `ReadPortResult.Ok(byte)` / `ReadPortResult.Fail(BiosStatus)` и использовать их в реализации; либо `<remarks>` с явным предупреждением.

7. **[API/XML] `IPortUse.ReadPort`/`IPortUse.WritePort` — не зафиксировано поведение при ошибке.**  
   Файл: `IPortUse.cs`. `byte ReadPort(ulong offset)` — что при ошибке (бросить, вернуть `0`, вернуть `0xFF`)? Не сказано. `void WritePort(ulong, byte)` — глотает ошибку или бросает? Контракт не фиксирует. `IPortBus.ReadPort`/`WritePort` возвращают статус — снова асимметрия.  
   Фикс: либо добавить `TryReadPort(ulong, out byte, out BiosStatus)` / `BiosStatus WritePort(...)`, либо задокументировать в `<remarks>`, что реализация бросает `InvalidOperationException` при ошибке (и зафиксировать это как контракт).

8. **[XML] `OptimizationLogEntry` — summary описывает мотивацию, не тип.**  
   Файл: `OptimizationLogEntry.cs`, summary: «Оптимизационный лог с защитой от перезаписи новыми логами». «Защита от перезаписи» — это следствие `readonly record struct` (иммутабельность), а не отдельная функциональность. Читатель может решить, что тип содержит логику версионирования.  
   Фикс: переписать в терминах типа: «Запись журнала оптимизатора: номер прохода и сообщение. Иммутабельна; после создания не изменяется».

9. **[API] `DeviceInfo.Name: string?` — вместо `NameDeviceToken`.**  
   Файл: `DeviceInfo.cs`. В `Common` есть `NameDeviceToken` (кэширует строку, `IsUnknown`, сравнение Ordinal). `DeviceInfo` использует `string?` — для ключа словаря или сравнения это менее удобно и создаёт аллокации.  
   Фикс: заменить `string? Name` на `NameDeviceToken Name` (breaking change для потребителей `DeviceInfo`) — либо явно оставить `string?` и задокументировать в `<remarks>`, почему не `NameDeviceToken`.

10. **[API/XML] `INativeReadOnlyBuffer` — имя «Native», но контракт этого не требует.**  
    Файл: `INativeReadOnlyBuffer.cs`. Имя намекает на `NativeMemory`, но интерфейс этого не требует — любая реализация (включая managed `byte[]`) подойдёт. В WASM это нормально, но имя сбивает с толку.  
    Фикс: либо переименовать (`IReadOnlyBuffer`/`IByteReadOnlyBuffer`), либо добавить `<remarks>` с явным «the name is historical; implementations may be fully managed».

11. **[XML] `IProcessorReader.GetRegister` — не зафиксировано поведение для невалидного `RegType`.**  
    Файл: `IProcessorReader.cs`. `ulong GetRegister(RegType regType)` — что при `(RegType)200`? Бросает, возвращает `0`, что-то ещё? Не сказано.  
    Фикс: `<exception>` или `<remarks>` с явным «throws `ArgumentOutOfRangeException` for invalid register» либо «returns 0».

12. **[XML] `IPortController` — порядок `Dispose` не зафиксирован.**  
    Файл: `IPortController.cs`. XML есть, но кто вызывает `Dispose`, когда и как это соотносится с операциями шины (`ReadPort`/`WritePort`/`WakeProcessor`), не сказано.  
    Фикс: `<remarks>` с порядком вызовов (например, «dispose is called by the bus owner; the device must not be used after dispose»).