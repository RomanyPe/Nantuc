# Nantuc.Kernel.Contracts — README

> Публичные контракты платформы: интерфейсы шин, портов, устройств, результатов исполнения и идентификаторов оптимизаций.  
> Слой, через который `Kernel`, расширения и компилятор договариваются о взаимодействии.  
> `[← Корень](../README.md) · [Common](../Nantuc.Kernel.Common/README.md)`

## Содержание
- [Об этом модуле](#об-этом-модуле)
- [Дерево локального проекта](#дерево-локального-проекта)
- [API](#api)
- [Примеры для API](#примеры-для-api)
- [Приближенность модуля к пользователю](#приближенность-модуля-к-пользователю)
- [Рекомендации](#рекомендации)
- [Особенная информация](#особенная-информация)

## Об этом модуле

`Nantuc.Kernel.Contracts` — это **слой публичных контрактов**. Здесь нет ни исполнения, ни состояния: только `interface`, `readonly struct`, `record struct` и один «богатый» value-тип `OptimizationId`. Всё, что ядро (`Kernel`) выставляет наружу для расширений, и всё, что расширения и внешний код ожидают от ядра, объявлено именно здесь. Если `Kernel.Common` — это «словарь ISA», то `Kernel.Contracts` — это «правила разговора»: как процессор читает порты, как устройство отвечает, как выглядит результат симуляции, как идентифицируется проход оптимизации.

Модуль **зависит только от `Kernel.Common`** (через `ProjectReference`). Обратная зависимость запрещена: `Common` не знает про `Contracts`. Это даёт чёткий нижний слой иерархии:

```
Kernel.Common  ←  Kernel.Contracts  ←  Kernel (реализация)  ←  VmApplication
```

Ключевые группы контрактов:

- **Порты/устройства**: `IPortBus`, `IPortUse`, `IPortController`, `DeviceInfo`, `ReadPortResult`. Разделение на «шину» (её использует процессор) и «устройство» (её реализует драйвер) — центральная идея архитектуры.
- **Исполнение**: `IProcessorReader`, `IProcessorFaultPolicy`, `ISimulationResult`. Наблюдение за процессором и политика «продолжать ли после fault».
- **Оптимизации/диагностика**: `IReadOnlyLogOptimization`, `OptimizationLogEntry`, `OptimizationId`.
- **Буферы**: `INativeReadOnlyBuffer` — доступ к «нативной» памяти без копирования.

Правило именования внутри модуля простое: интерфейсы начинаются с `I`, DTO — это `record struct` (не класс), value-типы с поведением — `readonly struct` с реализацией `IEquatable`/`IComparable`.

## Дерево локального проекта

Все типы — в namespace `Nantuc.Kernel.Contracts`, структура плоская:

```
Nantuc.Kernel.Contracts/
├── Nantuc.Kernel.Contracts.csproj   # net10.0, ref → Nantuc.Kernel.Common
├── DeviceInfo.cs                    # DeviceInfo + IPortBus + ReadPortResult
├── INativeReadOnlyBuffer.cs         # INativeReadOnlyBuffer
├── IPortController.cs               # IPortController : IPortUse, IDisposable
├── IPortUse.cs                      # IPortUse
├── IProcessorFaultPolicy.cs         # IProcessorFaultPolicy + private DefaultProcessorFaultPolicy
├── IProcessorReader.cs              # IProcessorReader
├── IReadOnlyLogOptimization.cs      # IReadOnlyLogOptimization + OptimizationLogEntry
├── ISimulationResult.cs             # ISimulationResult
└── TypeOptimization.cs              # OptimizationId
```

> В файлах встречаются смешанные объявления: `DeviceInfo.cs` содержит `IPortBus` и `ReadPortResult`, `IReadOnlyLogOptimization.cs` содержит `OptimizationLogEntry`. Для навигации это неудобно (grep по имени типа ведёт в «неожиданный» файл). См. [Рекомендации](#рекомендации).

> Модуль, судя по всему, содержит и другие контракты — этот README описывает **предоставленные файлы**. По мере поступления остальных контрактов раздел API будет расширяться.

## API

### Общая картина

В модуле **8 интерфейсов**, **3 `record struct`**, **1 `readonly struct`** и **1 константный `const`**. Из всех интерфейсов **только у одного** (`IProcessorFaultPolicy`) есть встроенная default-реализация — через статическое свойство `Default` и приватный вложенный класс.

| Тип | Вид | Default-реализация | Назначение |
|---|---|---|---|
| `IPortBus` | interface | нет | Шина портов: чтение/запись по абсолютному адресу, wake |
| `IPortUse` | interface | нет | Устройство: чтение/запись по смещению, wake |
| `IPortController` | interface | нет | `IPortUse` + `IDisposable` — владение устройством |
| `INativeReadOnlyBuffer` | interface | нет | Read-only буфер нативной памяти |
| `IProcessorReader` | interface | нет | Наблюдение за состоянием процессора |
| `IProcessorFaultPolicy` | interface | **да** | Решение «продолжать ли после `ProcessorFault`» |
| `IReadOnlyLogOptimization` | interface | нет | Read-only снимок лога оптимизаций |
| `ISimulationResult` | interface | нет | Результат симуляции: время и шаги |
| `OptimizationId` | readonly struct | — | Стабильный строковый идентификатор прохода/события |
| `OptimizationLogEntry` | readonly record struct | — | Запись лога: `(int Pass, string Message)` |
| `DeviceInfo` | readonly record struct | — | Снимок: `(int Id, uint Sector, string? Name)` |
| `ReadPortResult` | record struct | — | `(byte Data, BiosStatus Status = Success)` |

### Интерфейсы подробно

**`IPortBus`** — «шина портов», то, к чему обращается **процессор**:

```csharp
ReadPortResult ReadPort(ulong address);
BiosStatus     WritePort(ulong address, byte value);
bool           WakeProcessor(ulong address);
```

- `address` — **абсолютный** адрес в порт-пространстве (см. `PortSize` в `Kernel.Common`).
- `ReadPort` возвращает `ReadPortResult` со `Status` (по умолчанию `Success`) — ошибки чтения обрабатываются через статус, а не исключение.
- `WritePort` возвращает `BiosStatus` — так же, без исключений.
- `WakeProcessor` — сигнал «порт изменился, разбуди `WAKE`-инструкцию». Возвращает `bool`: удалось ли кого-то разбудить.

**`IPortUse`** — «устройство», то, что реализует **драйвер**:

```csharp
byte ReadPort(ulong offset);
void WritePort(ulong offset, byte value);
void WakeProcessor();
```

- `offset` — **смещение внутри блока устройства** (см. `DevicePortSize` в `Kernel.Common`), а не абсолютный адрес.
- Проще и «тише»: ни `BiosStatus`, ни `bool`. Валидацию адреса делает шина до вызова.
- `WakeProcessor` без параметра — устройство само решает, кого и когда будить.

**`IPortController : IPortUse, IDisposable`** — то, что держит **потребитель**:

- Наследует `IPortUse`, добавляет `IDisposable`. Семантика: пока контроллер жив — устройство доступно; `Dispose` освобождает ресурсы (порты, native-память, обработчики).
- Разница между `IPortUse` и `IPortController` — это разница между «я умею отвечать на порты» и «я владею ресурсом и обязан его освободить». Реализация `IPortUse` не обязана быть `IDisposable`; контроллер — обязан.

> 💡 Слоистость: `Processor → IPortBus → (внутренняя маршрутизация) → IPortUse/IPortController → Device`. Процессор не знает про устройства, устройства не знают про абсолютные адреса. Это самая ценная часть архитектуры — не ломайте её.

**`IProcessorReader`** — read-only «окно» в процессор:

```csharp
bool  IsRunning { get; }
bool  IsSleeping { get; }
ulong GetRegister(RegType regType);
```

- Никаких `Set*` — это намеренно: наблюдение без вмешательства. Для отладчика, логгера, UI.
- `GetRegister` возвращает `ulong`, что согласуется с 64-битными регистрами (см. `Kernel.Common`).
- `RegType` приходит из `Kernel.Common` — здесь нет дублирования.

**`IProcessorFaultPolicy`** — политика «что делать после fault»:

```csharp
public static IProcessorFaultPolicy Default => new DefaultProcessorFaultPolicy();

bool ShouldContinue(in ProcessorFault fault);
```

- **Единственный интерфейс с default-реализацией.** Приватный вложенный `DefaultProcessorFaultPolicy` продолжает исполнение только при `BiosStatus.Success` и `BiosStatus.NullDeviceOutput`, на всех остальных статусах — останавливает.
- `in ProcessorFault` — параметр передаётся по readonly-ссылке; `ProcessorFault` — `readonly record struct` из `Kernel.Common`, аллокаций нет.
- Замена политики — штатный способ кастомизировать поведение ядра: реализуйте интерфейс и передайте инстанс в `Kernel`.

**`IReadOnlyLogOptimization`** — read-only лог оптимизаций:

```csharp
OptimizationId Id { get; }
IReadOnlyList<OptimizationLogEntry> GetLogs();  // снимок
```

- Комментарий `// снимок` в исходнике — часть контракта: метод возвращает **иммутабельный снимок** на момент вызова, не live-вид. Реализация не обязана поддерживать его в актуальном состоянии после вызова.
- `Id` — `OptimizationId`, привязка лога к конкретному проходу (см. ниже).

**`ISimulationResult`** — результат прогона:

```csharp
TimeSpan Elapsed { get; }
long     Steps   { get; }
```

- Минимальный immutable-снимок. Никаких `Success`/`Fault` — статус отдельно, через `ProcessorFault`/`BiosStatus`. Это позволяет одному `ISimulationResult` описывать и успешный, и упавший прогон.

**`INativeReadOnlyBuffer`** — read-only буфер нативной памяти:

```csharp
nuint Length { get; }
byte  this[nuint index] { get; }
```

- `nuint` — намеренно: размеры и индексы согласованы с native-адресацией (`UIntPtr`-совместимо).
- Только чтение: запись — отдельный контракт (в этом модуле его нет, вероятно, будет `INativeBuffer` в будущем/в другом модуле).
- Индексатор не бросает `IndexOutOfRangeException` по контракту — поведение out-of-bounds **не определено**. См. [Особенная информация](#особенная-информация).

### `OptimizationId` — единственный «богатый» value-тип

Это самый проработанный тип модуля и образец для остальных идентификаторов.

- `readonly struct`, реализует `IEquatable<OptimizationId>`, `IComparable<OptimizationId>`.
- `Value : string?` — `null` только у `default`. `IsValid => Value is not null`.
- Конструктор `OptimizationId(string)` **бросает** `ArgumentException` на невалидный id.
- `TryParse(string?, out OptimizationId)` — non-throwing аналог для пользовательского ввода.
- Допустимый алфавит: `[a-z0-9.-]`, длина 1..128 (`MaxLength = 128`). Примеры: `"peephole"`, `"inline.func"`, `"node.removed.before-inline"`.
- **Well-known ids** — статические `readonly`-поля, гарантированно стабильные между версиями. Сейчас: `OptimizationId.Peephole`. Их нельзя переименовывать после релиза — только добавлять новые. Это гарантия для логов, кэшей и конфигов.
- Сравнение — `StringComparison.Ordinal` / `String.CompareOrdinal`. Операторы `==/!=/</>/<=/>=` определены.
- `ToString()` для `default` возвращает `"OptimizationId(invalid)"` — удобно для логов, но не для парсинга.

### `record struct` — простые DTO

- `OptimizationLogEntry(int Pass, string Message)` — номер прохода + сообщение.
- `DeviceInfo(int Id, uint Sector, string? Name)` — снимок-описание устройства. `Id` — целочисленный, `Sector` — `uint` (сектор/смещение в порт-пространстве), `Name` — опциональное.
- `ReadPortResult(byte Data, BiosStatus Status = BiosStatus.Success)` — результат чтения порта. `Status` по умолчанию `Success` — большинство чтений успешны, это удобно для `new ReadPortResult(data)`.

## Примеры для API

**Реализация устройства (`IPortUse`):**

```csharp
using Nantuc.Kernel.Contracts;

public sealed class UartDevice : IPortUse
{
    private byte _tx;

    public byte ReadPort(ulong offset) => offset switch
    {
        0 => _tx,
        _ => 0
    };

    public void WritePort(ulong offset, byte value)
    {
        if (offset == 0) _tx = value;
    }

    public void WakeProcessor() { /* разбудить ожидающий процессор */ }
}
```

**Реализация контроллера (`IPortController`) — с освобождением ресурсов:**

```csharp
public sealed class UartController : IPortController
{
    private readonly UartDevice _device = new();
    private bool _disposed;

    public byte ReadPort(ulong offset)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _device.ReadPort(offset);
    }

    public void WritePort(ulong offset, byte value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _device.WritePort(offset, value);
    }

    public void WakeProcessor() => _device.WakeProcessor();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // освободить порты, отписаться от шины
    }
}
```

**Шина портов (`IPortBus`) — маршрутизация `address` → `(device, offset)`:**

```csharp
public sealed class PortBus : IPortBus
{
    private readonly Dictionary<uint, IPortUse> _devices = new();
    // ключ — Sector из DeviceInfo, значение — устройство
    // (полноценная реализация — в Nantuc.Kernel)

    public ReadPortResult ReadPort(ulong address)
    {
        var (dev, offset) = Resolve(address);
        return dev is null
            ? new ReadPortResult(0, BiosStatus.NullDeviceInput)
            : new ReadPortResult(dev.ReadPort(offset));
    }

    public BiosStatus WritePort(ulong address, byte value)
    {
        var (dev, offset) = Resolve(address);
        if (dev is null) return BiosStatus.NullDeviceOutput;
        dev.WritePort(offset, value);
        return BiosStatus.Success;
    }

    public bool WakeProcessor(ulong address)
    {
        var (dev, _) = Resolve(address);
        if (dev is null) return false;
        dev.WakeProcessor();
        return true;
    }

    private (IPortUse? Device, ulong Offset) Resolve(ulong address) =>
        throw new NotImplementedException();
}
```

**Кастомная политика fault'ов:**

```csharp
using Nantuc.Kernel.Common;
using Nantuc.Kernel.Contracts;

public sealed class LogAndStopPolicy : IProcessorFaultPolicy
{
    public bool ShouldContinue(in ProcessorFault fault)
    {
        Console.WriteLine($"[fault] {fault.Status} @ IP=0x{fault.InstructionPointer:X}");
        return false;
    }
}

// где-то при конфигурации ядра:
IProcessorFaultPolicy policy = new LogAndStopPolicy();
// kernel.Configure(policy: policy);
```

**Наблюдение за процессором (`IProcessorReader`):**

```csharp
void Dump(IProcessorReader reader)
{
    Console.WriteLine($"running={reader.IsRunning} sleeping={reader.IsSleeping}");
    Console.WriteLine($"IP=0x{reader.GetRegister(RegType.rIP):X}");
    Console.WriteLine($"SP=0x{reader.GetRegister(RegType.rSP):X}");
}
```

**Проход оптимизации с логом:**

```csharp
public sealed class PeepholePass : IReadOnlyLogOptimization
{
    private readonly List<OptimizationLogEntry> _log = new();

    public OptimizationId Id => OptimizationId.Peephole;

    public IReadOnlyList<OptimizationLogEntry> GetLogs() => _log.ToArray();

    public void Run(/* IR */)
    {
        _log.Add(new OptimizationLogEntry(Pass: 1, Message: "removed redundant LOAD"));
    }
}
```

## Приближенность модуля к пользователю

**Низкая, но выше, чем `Kernel.Common`.** Это уже «язык», на котором говорит платформа, но ещё не пользовательский API.

- Пользователь MiniC/NANASM **не должен** импортировать `Nantuc.Kernel.Contracts`.
- **Автор устройства/расширения** — да, это его основной контракт: реализует `IPortUse`/`IPortController`, читает `DeviceInfo`, отдаёт `IReadOnlyLogOptimization`.
- **Автор компилятора и оптимизаций** — да: `OptimizationId`, `IReadOnlyLogOptimization`, `ISimulationResult`.
- **Внешний код, встраивающий Nantuc** (хост, отладчик, тест-раннер) — да: `IProcessorReader`, `IProcessorFaultPolicy`, `ISimulationResult`.

**Что стоит делать:**
- Реализовывать устройства через `IPortUse`, а владение — через `IPortController` + `using`/`Dispose`.
- Возвращать `ReadPortResult` с корректным `BiosStatus`, а не бросать исключения из `ReadPort`/`WritePort`: контракт не предусматривает throw.
- Использовать `OptimizationId.Peephole`/well-known ids для стабильных идентификаторов; для собственных — `new OptimizationId("my.pass")` или `TryParse`.
- Заменять `IProcessorFaultPolicy` только когда действительно нужно другое поведение (тесты, отладка, «мягкий» режим).

**Что не стоит делать:**
- Возвращать из `IPortBus.ReadPort` «сырой» `byte` без статуса — потеряете диагностику. `ReadPortResult` для этого и существует.
- Кидать исключения из `IPortUse.WritePort` — шина ждёт `void`; ошибки должны идти через `IPortBus.WritePort` → `BiosStatus`.
- Полагаться на live-обновление `GetLogs()` — это снимок.
- Использовать `OptimizationId.ToString()` для `default` как валидный id — `"OptimizationId(invalid)"` не пройдёт `TryParse`.
- Смешивать абсолютные адреса (`IPortBus`) и смещения (`IPortUse`). Процессор работает с первыми, устройство — со вторыми.

## Рекомендации

1. **Разложите типы по файлам 1:1.** Сейчас `DeviceInfo.cs` содержит `IPortBus` и `ReadPortResult`, `IReadOnlyLogOptimization.cs` — `OptimizationLogEntry`. Правило «имя файла = имя типа» экономит время в code review и `grep`. Особенно важно для контрактов, которые открывают в первую очередь.
2. **Закэшируйте `IProcessorFaultPolicy.Default`.** Сейчас `public static IProcessorFaultPolicy Default => new DefaultProcessorFaultPolicy();` — **новая аллокация на каждое обращение**. Замените на:
   ```csharp
   private static readonly IProcessorFaultPolicy _default = new DefaultProcessorFaultPolicy();
   public static IProcessorFaultPolicy Default => _default;
   ```
   Или, если структура позволит, — `static` авто-свойство с инициализатором. Это горячий путь конфигурации ядра.
3. **Задокументируйте `IReadOnlyLogOptimization.GetLogs()` как снимок** в XML-doc (сейчас только комментарий `// снимок`). Пользователи API это прочитают в IntelliSense, а не в исходнике.
4. **Задокументируйте `INativeReadOnlyBuffer`.** В частности: (а) кто владеет памятью, (б) что происходит при `index >= Length` (undefined? throw? clamp?), (в) время жизни буфера относительно вызывающего кода. Без этого очень легко получить use-after-free в native-сценариях. Если поведение не определено — так и напишите, это честнее.
5. **XML-doc на все публичные члены.** Сейчас `OptimizationId` документирован отлично, а `IPortBus`, `IPortUse`, `IProcessorReader`, `ISimulationResult`, `INativeReadOnlyBuffer` — без `///`. Для контрактного модуля это критично: именно эти описания увидят авторы расширений. Включите `GenerateDocumentationFile=true` и добейте пропуски.
6. **Проверьте `ISimulationResult` на достаточность.** Сейчас только `Elapsed` и `Steps`. Для отладки и бенчмарков часто нужны `InstructionsExecuted` vs `Steps` (шаг ≠ инструкция при halt/wake), `PeakMemory`, `Fault`. Если добавлять — то сейчас, пока контракт молодой.
7. **`DeviceInfo.Sector` — поясните единицы.** `uint Sector` — это индекс сектора, абсолютный адрес или смещение? Судя по `IPortUse(offset)`, скорее «начало блока устройства в порт-пространстве». Задокументируйте — это поле будет читать каждый автор драйвера.
8. **`OptimizationId.Peephole` — вынесите в отдельный статический класс `OptimizationIds`,** когда well-known id'ов станет больше одного-двух. Сейчас `OptimizationId.Peephole` выглядит нормально, но по мере роста это превратится в свалку на самом типе. Правило: value-тип — про значение, статические константы — про каталог.
9. **Юнит-тесты на `OptimizationId`** — самое дешёвое и ценное: границы `MaxLength`, алфавит, `TryParse`/конструктор, `default`-поведение, `CompareTo`, `==`/`!=`. Плюс тест, что `IProcessorFaultPolicy.Default` действительно singleton (если последуете п. 2).
10. **Проверьте `IProcessorFaultPolicy` на `NullDeviceOutput => true`.** Это осознанное решение «не падать на отсутствующем выходе устройства». Убедитесь, что оно задокументировано в XML-doc: без пояснения читатель решит, что это баг.

## Особенная информация

### Разделение портов: `IPortBus` vs `IPortUse` vs `IPortController`

Три интерфейса с похожими методами — это **не дублирование**, а три роли:

| Роль | Кто реализует | Адресация | Ошибки | Владение |
|---|---|---|---|---|
| `IPortBus` | шина (ядро) | абсолютный адрес | `BiosStatus` | нет |
| `IPortUse` | устройство (драйвер) | смещение в блоке | без статуса, `void`/`byte` | нет |
| `IPortController` | потребитель устройства | смещение | без статуса | `IDisposable` |

Процессор видит только `IPortBus`. Устройство видит только `IPortUse`. Пользователь устройства держит `IPortController` и вызывает `Dispose`. Если вы добавляете новое устройство — реализуйте `IPortUse` (и при необходимости `IPortController`), **не** `IPortBus`. Шина — ровно одна на процессор.

### `in`-параметры и аллокации

`IProcessorFaultPolicy.ShouldContinue(in ProcessorFault fault)` принимает `readonly record struct` по `in`. Это значит: реализация не может изменить `fault`, но и копирования не будет. При реализации **не** пишите `ShouldContinue(ProcessorFault fault)` — сигнатура изменится, и ядро не сможет вас вызвать через интерфейс.

### `record struct` vs `readonly record struct`

`OptimizationLogEntry` и `DeviceInfo` объявлены как `readonly record struct` — иммутабельны, `Equals`/`GetHashCode` генерируются. `ReadPortResult` — `record struct` (без `readonly`), потому что `Status` имеет default-значение и предполагается, что его можно менять при построении. При использовании `ReadPortResult` в горячем пути учитывайте: это value-type, копирование дёшево (9 байт), но при возврате из метода всё равно копируется — не храните ссылку на него «для экономии».

### `OptimizationId` как версионируемый контракт

Well-known ids — это **публичный контракт с гарантией стабильности**. Если вы добавите `OptimizationId.Inline` и выпустите версию, переименовать его в `OptimizationId.InlineFunc` уже нельзя — только добавить новый id и пометить старый `[Obsolete]`. Это тот же принцип, что у `OpCode` в `Kernel.Common`: числа и строки в персистентных данных — это wire-format.

### Ограничения платформ

- **Целевой framework — `net10.0`** (тот же, что у `Common`). C# 14, `extension`-блоки, `[NotNullWhen]` и `ObjectDisposedException.ThrowIf` — всё доступно. Старые тулчейны не соберут.
- **`unsafe` в этих файлах нет**, но `INativeReadOnlyBuffer` семантически связан с native-памятью. Реализация почти наверняка будет `unsafe` (или через `NativeMemory`/`Span<byte>`). Для WASM/AOT: (а) убедитесь, что рантайм разрешает `unsafe`, (б) индексатор `this[nuint]` в WASM может быть медленнее, чем в JIT — профилируйте.
- **`nuint`** в `INativeReadOnlyBuffer` — 32/64-битный в зависимости от платформы. Не полагайтесь на 64-битность в WASM (wasm32) — `nuint` там 32-битный.
- **`OptimizationId` — `readonly struct` с `string?`-полем.** В AOT/WASM это просто ссылка; аллокаций при `Equals`/`CompareTo` нет (используется `Ordinal`). Но сам `new OptimizationId("...")` валидирует строку в конструкторе — в горячем пути избегайте создания новых id, используйте well-known.
- **`IProcessorFaultPolicy.Default` — аллокация на каждое обращение** (см. п. 2 Рекомендаций). На AOT это заметнее, чем на JIT.

### Что этот модуль **не** делает

- Не реализует ни один интерфейс (кроме приватного `DefaultProcessorFaultPolicy`) — реализации в `Kernel`.
- Не хранит состояние — все типы либо интерфейсы, либо immutable DTO.
- Не знает про компилятор MiniC/NANASM — только про оптимизации (`OptimizationId`, `IReadOnlyLogOptimization`).
- Не содержит `IReadOnlyList<...>`-обёрток — возвращает их из методов, но не определяет собственных коллекций.
- Не описывает формат байткода — это `Kernel.Common` + `docs/bytecode.md`.

Если сомневаетесь, «куда положить тип» — правило: **примитив без поведения → `Kernel.Common`; контракт/интерфейс → `Kernel.Contracts`; реализация, состояние, исполнение → `Kernel`; точка входа для приложения → `VmApplication`.**