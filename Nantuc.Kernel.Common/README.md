# Nantuc.Kernel.Common — README

> Общие типы, константы ISA и разделяемые примитивы платформы Nantuc.  
> Фундамент, на который опираются ядро, контракты, компилятор и расширения.  
> `[← Корень](../README.md) · [Contracts](../Nantuc.Kernel.Contracts/README.md) · [Diagnostics](../Nantuc.Kernel.Diagnostics/README.md) · [Kernel](../Nantuc.Kernel/README.md)`

## Содержание
- [Об этом модуле](#об-этом-модуле)
- [Дерево локального проекта](#дерево-локального-проекта)
- [API](#api)
- [Примеры для API](#примеры-для-api)
- [Приближенность модуля к пользователю](#приближенность-модуля-к-пользователю)
- [Особенная информация](#особенная-информация)
- [Что исправить](#что-исправить)

## Об этом модуле

`Nantuc.Kernel.Common` — это слой разделяемых примитивов платформы. Здесь нет ни логики процессора, ни шин, ни устройств: только типы, которые нужны одновременно ядру (`Kernel`), публичным контрактам (`Kernel.Contracts`), компилятору MiniC, ассемблеру NANASM, дизассемблеру и расширениям. Это не BIOS и не драйверы, а словарь ISA и единицы измерения памяти и портов — то, что должно быть согласовано у всех участников платформы.

Модуль не содержит интерфейсов — только `enum`, `readonly struct`, `record struct` и `static class` с чистыми функциями. Это делает его безопасным для горячих путей процессора (нулевые аллокации) и удобным для сравнения и сериализации.

Ключевые роли модуля:
- ISA байткода: `OpCode`, `OpCodeSize`, `RegType`, `DecodeInstructionResult`, `InstructionDecoder`.
- Единицы измерения: `RamSize`, `PortSize`, `DevicePortSize` (лог2-структуры с фабриками).
- Диагностика: `BiosStatus`, `ProcessorFault`.
- Идентификация: `NameDeviceToken`.
- Классификация возвратов: `IdState` (соответствует `-1..-4` из `ManagerDevices.AddDevice`).
- Утилиты: `AlignmentExtensions` (выравнивание для generic-чисел).

Проект — листовой по зависимостям: ни от чего внутри Nantuc не зависит. Любое изменение публичного типа здесь — потенциальный breaking change для всей платформы.

> **XML-документация в работе.** Часть типов уже документирована (`PortSize`, `OptimizationId` в `Contracts`), часть — ещё нет (`OpCodeSize`, `RegType`, `IdState`, `NameDeviceToken`, методы `InstructionDecoder`). Это осознанно: приоритет — стабилизировать форму API, потом — наполнить `///`. В разделе [Что исправить](#что-исправить) — список того, что стоит задокументировать в первую очередь (по убыванию риска неправильного прочтения).

## Дерево локального проекта

Все типы живут в namespace `Nantuc.Kernel.Common`, структура плоская:

```
Nantuc.Kernel.Common/
├── Nantuc.Kernel.Common.csproj    # net10.0, без ProjectReference
├── AlignmentExtensions.cs         # static class AlignmentExtensions
├── BiosStatus.cs                  # enum BiosStatus + record struct ProcessorFault
├── DecodeInstructionResult.cs     # readonly struct
├── DevicePortSize.cs              # readonly struct
├── IdState.cs                     # enum IdState
├── InstructionDecoder.cs          # static class InstructionDecoder
├── NameDeviceToken.cs             # readonly struct
├── OpCode.cs                      # enum OpCode : byte
├── OpCodeSize.cs                  # enum OpCodeSize
├── PortSize.cs                    # readonly struct PortSize
├── RamSize.cs                     # readonly struct RamSize
└── RegType.cs                     # enum RegType : byte
```

Замечания по организации:
- `PortSize.cs` теперь называется по имени типа (было `SizePortOnDevice.cs`) — навигация исправлена.
- `AlignmentExtensions.cs` вынесен из `InstructionDecoder.cs` — семантически разные классы больше не делят файл.
- `LogLevel.cs` не был включён в эту партию файлов. Если тип ещё существует в проекте — добавьте его в следующую партию; если удалён — обновите дерево и таблицу API ниже.

## API

### Общая картина

В модуле нет интерфейсов и default-реализаций. Публичная поверхность — это:

| Тип | Вид | Назначение |
|---|---|---|
| `OpCode` | `enum : byte` | Опкоды VM (~60 значений) |
| `OpCodeSize` | `enum` | Размер операнда: `S8`/`S16`/`S32`/`S64` |
| `RegType` | `enum : byte` | 32 регистра: `r0..r27`, `rFL`, `rSP`, `rHP`, `rIP` |
| `BiosStatus` | `enum : byte` | Статус завершения/ошибки |
| `IdState` | `enum` | Классификация `-1..-4` от `ManagerDevices.AddDevice` |
| `ProcessorFault` | `readonly record struct` | `(BiosStatus, ulong FaultData, ulong IP, OpCode)` |
| `RamSize` | `readonly struct` | Размер ОЗУ, лог2, 64 B … 2 GiB |
| `PortSize` | `readonly struct` | Размер порт-пространства, лог2, 64 B … 512 KiB |
| `DevicePortSize` | `readonly struct` | Размер блока портов устройства, лог2, 8 B … 256 B |
| `NameDeviceToken` | `readonly struct` | Имя устройства (кэширует строку) |
| `DecodeInstructionResult` | `readonly struct` | Результат декодирования сырой инструкции |
| `InstructionDecoder` | `static class` | Разбор 32-битной инструкции |
| `AlignmentExtensions` | `static class` | `AlignUp`/`AlignDown`/`AlignUpArithmetic` для `IBinaryInteger<T>` |

### Перечисления

**`OpCode : byte`** — около 60 значений, сгруппированных по назначению:

- Системные: `NOP`, `END`.
- Память: `MOV`, `LOAD`, `STORE`, `LDI`, `LOAD_IND`, `STORE_IND`.
- Арифметика: `ADD`, `SUB`, `MULT_INT`, `SHR`, `INC`, `DEC`, `DIV`.
- Логика: `AND`, `OR`, `XOR`, `NOT`.
- Управление потоком: `JMP`, `JZ`, `JNZ`, `JG`, `JL`.
- Стек: `PUSH`, `POP`, `CALL`, `RET`.
- I/O: `IN`, `OUT`, `CALL_IND`, `JMP_IND`.
- Сон: `HALT`, `WAKE`, `WAKE_INT`.
- Unsafe-варианты: `LOAD_UNSAFE`, `STORE_UNSAFE`, `STORE_IND_UNSAFE`, `LOAD_IND_UNSAFE`.
- Сравнение: `CMP`, `TEST`.
- Сдвиги: `SHL` (влево), `SHR` (вправо).
- Остаток: `MOD`.

**`OpCodeSize`** — `S8 = 0b00`, `S16 = 0b01`, `S32 = 0b10`, `S64 = 0b11`. Семантика «1/2/4/8 байт» подразумевается по именам, но явно не задокументирована в файле — маппинг в `Nantuc.Compiler/C/CodeGenerator/CodeGenUtils.GetSizeInBytes`.

**`RegType : byte`** — 32 значения: `r0..r27` (общего назначения), `rFL` (флаги: бит 0 = Zero, бит 1 = Negative), `rSP` (stack pointer), `rHP` (heap pointer), `rIP` (instruction pointer). Ровно 32 = 5 бит, что совпадает с полями `Reg1`/`Reg2` в инструкции.

**`BiosStatus : byte`** — `Success`, `SegmentationFault`, `AlignmentFault`, `EndProgramm`, `NotImplementedOpCode`, `NullDeviceInput`, `NullDeviceOutput`, `InfinityLoop`, `DivOnZero`, `NotSupportedOpCode`, `StackUnderflow`.

**`IdState`** — `Valid`, `NotAssigned` (`-1`), `OutOfRange` (`-2`), `NoFreeSlot` (`-3`), `Rejected` (`-4`), `Unknown`. Соответствует кодам возврата `ManagerDevices.AddDevice` (`Nantuc.Kernel/Utilites/ManagerDevices.cs`). Живёт в `Common`, потому что `Kernel` (где `ManagerDevices`) не может зависеть от `VmApplication`, а `VmApplication` и `Extensions` зависят от `Common` — так `IdState` виден всем, кому нужен.

### `ProcessorFault` — `readonly record struct`

```csharp
public readonly record struct ProcessorFault(
    BiosStatus Status,
    ulong FaultData,
    ulong InstructionPointer,
    OpCode OpCode);
```

Возвращается из ядра как результат «мягкого падения». `in`-параметр в `IProcessorFaultPolicy.ShouldContinue` — без копирования.

### Размеры: `RamSize`, `PortSize`, `DevicePortSize`

Три одинаковые по форме лог2-структуры. Конструктор приватный, экземпляры получают только через фабрики. Валидность — диапазон `Log2`:

| Тип | MinLog2 | MaxLog2 | Диапазон |
|---|---|---|---|
| `RamSize` | 6 | 31 | 64 B … 2 GiB |
| `PortSize` | 6 | 19 | 64 B … 512 KiB |
| `DevicePortSize` | 3 | 8 | 8 B … 256 B |

`default(T)` даёт `IsValid == false` — сигнал «не инициализировано». У всех трёх: `Bytes => 1UL << Log2`, операторы `==`/`!=`/`<`/`>`/`<=`/`>=`, `IEquatable<T>`, `IComparable<T>`, `ToString()`.

Фабрики:

| Тип | Доступные фабрики |
|---|---|
| `RamSize` | `B64..B512`, `KB1, KB2, KB4, KB8, KB16, KB32, KB64, KB128, KB256, KB512`, `MB1..MB512`, `GB1, GB2` — полный набор |
| `PortSize` | `B64..B512`, `KB1, KB4, KB8, KB16, KB64, KB128, KB256, KB512` — нет `KB2` и `KB32` |
| `DevicePortSize` | `B8, B16, B32, B64, B128, B256` — полный набор от 8 B до 256 B |

### `NameDeviceToken` — `readonly struct`

```csharp
public readonly struct NameDeviceToken : IEquatable<NameDeviceToken>
{
    public const string UnknownName = "Unknown Device";
    public NameDeviceToken(ReadOnlySpan<char> name);
    public bool   IsUnknown { get; }
    public string Name { get; }
    // Equals, GetHashCode, ==, !=, ToString
}
```

Кэширует `string?`; пустое имя превращается в `null`, `Name` отдаёт `UnknownName`. Сравнение — `Ordinal`. Хорошо подходит как ключ в словарях устройств.

### `DecodeInstructionResult` — `readonly struct`

```csharp
public readonly struct DecodeInstructionResult(
    OpCode opCode, RegType reg1, RegType reg2, OpCodeSize dataSizeCode)
{
    public readonly OpCode       OpCode;
    public readonly RegType      Reg1;
    public readonly RegType      Reg2;
    public readonly OpCodeSize   DataSizeCode;
}
```

DTO без логики.

### `InstructionDecoder` — `static class`

| Метод | Принимает | Возвращает | Что делает |
|---|---|---|---|
| `GetOpCode(uint)` | сырая инструкция | `OpCode` | `raw & 0xFF` |
| `GetReg1(uint)` | | `RegType` | `(raw >> 8) & 0x1F` |
| `GetReg2(uint)` | | `RegType` | `(raw >> 13) & 0x1F` |
| `GetDataSizeCode(uint)` | | `OpCodeSize` | `(raw >> 18) & 0x3` |
| `DecodeRawInstToEnums(uint)` | | `DecodeInstructionResult` | всё сразу |
| `HasNeed64IntData(OpCode)` | опкод | `bool` | есть ли после инструкции 64-битный операнд; бросает `ArgumentOutOfRangeException`, если `opCode > OpCode.MOD` |

`HasNeed64IntData` — таблица контракта. Сейчас в ней: `LDI`, `LOAD`, `STORE`, `CALL`, `JMP`, `JZ`, `JNZ`, `JG`, `JL`. Метод **бросает**, если передан опкод вне диапазона (защита от `(OpCode)200`). `CreateNeedsUlongOperandTable` теперь `private` — деталь реализации скрыта.

Все методы — `[MethodImpl(AggressiveInlining)]`, оптимизация под JIT.

### `AlignmentExtensions` — `static class`

Через C# 14 `extension`-блок добавляет к `IBinaryInteger<T>`:

| Метод | Семантика | Проверка `alignment` |
|---|---|---|
| `AlignUp(T)` | вверх до кратного | только `DEBUG`, требует степень двойки |
| `AlignDown(T)` | вниз до кратного | только `DEBUG`, требует степень двойки |
| `AlignUpArithmetic(T)` | вверх через `%` | всегда, требует `> 0` (не обязательно степень двойки) |

`AlignUp`/`AlignDown` в **Release** не проверяют `alignment` — невалидное значение даст тихий мусор. Для гарантированной проверки — `AlignUpArithmetic`.

## Примеры для API

**Размеры памяти и портов — только через фабрики:**

```csharp
using Nantuc.Kernel.Common;

RamSize ram = RamSize.MB64;
PortSize ports = PortSize.KB64;
DevicePortSize block = DevicePortSize.B256;

Console.WriteLine(ram);              // 67108864 B (2^26)
Console.WriteLine(block);            // 256 B (2^8)
Console.WriteLine(ram.IsValid);      // true

// ⚠ default — это «не инициализировано», а не «0 байт»:
RamSize uninit = default;
Console.WriteLine(uninit.IsValid);   // false
```

**Декодирование инструкции:**

```csharp
uint raw = 0x0000_4A01;

OpCode op = InstructionDecoder.GetOpCode(raw);
RegType r1 = InstructionDecoder.GetReg1(raw);
RegType r2 = InstructionDecoder.GetReg2(raw);
OpCodeSize sz = InstructionDecoder.GetDataSizeCode(raw);

DecodeInstructionResult d = InstructionDecoder.DecodeRawInstToEnums(raw);
if (InstructionDecoder.HasNeed64IntData(d.OpCode))
{
    // после 4-байтной инструкции идёт 8-байтный immediate
}
```

**Классификация возврата `RegisterDevice`:**

```csharp
int raw = host.Emulator.RegisterDevice(spec, sector: 0);

IdState state = raw switch
{
    >= 0 => IdState.Valid,
    -1 => IdState.NotAssigned,
    -2 => IdState.OutOfRange,
    -3 => IdState.NoFreeSlot,
    -4 => IdState.Rejected,
    _  => IdState.Unknown,
};

if (state != IdState.Valid)
{
    Console.Error.WriteLine($"RegisterDevice failed: {state} ({raw})");
    return;
}
// raw — валидный ID
```

**Идентификатор устройства:**

```csharp
var token = new NameDeviceToken("UART0".AsSpan());
Console.WriteLine(token.Name);        // "UART0"
Console.WriteLine(token.IsUnknown);   // false

var unknown = new NameDeviceToken(ReadOnlySpan<char>.Empty);
Console.WriteLine(unknown.Name);      // "Unknown Device"

var devices = new Dictionary<NameDeviceToken, IDevice>();
devices[token] = myUart;
```

**Диагностика:**

```csharp
ProcessorFault fault = new(
    Status: BiosStatus.DivOnZero,
    FaultData: 0,
    InstructionPointer: 0x40,
    OpCode: OpCode.DIV);

if (fault.Status != BiosStatus.Success)
    Console.Error.WriteLine($"{fault.Status} @ IP=0x{fault.InstructionPointer:X}");
```

**Выравнивание:**

```csharp
int addr = 13;
int aligned = addr.AlignUp(8);    // 16
int down    = addr.AlignDown(8);  // 8
```

## Приближенность модуля к пользователю

Низкая. Это внутренний фундамент платформы, а не пользовательский API.

- Пользователь, который пишет программы на MiniC или NANASM, не должен импортировать `Nantuc.Kernel.Common` напрямую — он работает через `VmApplication` / `Kernel`.
- Автор расширений (дизассемблер, отладчик, кастомные устройства, собственные backend'ы) будет использовать этот модуль постоянно — здесь лежат опкоды, регистры и единицы измерения.
- Автор компилятора (frontend MiniC → NANASM) обязан согласовывать генерацию с `OpCode` и `HasNeed64IntData` — иначе байткод развалится.

**Что стоит делать:**
- Использовать фабрики `RamSize`/`PortSize`/`DevicePortSize` — не «магические» числа.
- Для декодирования — только `InstructionDecoder`, без ручных сдвигов у себя.
- Передавать `NameDeviceToken` вместо `string`, если имя — идентификатор.
- Проверять `BiosStatus`/`ProcessorFault` вместо `bool`-флагов.
- Использовать `IdState` вместо `raw switch { -1 => ..., -2 => ... }` в своём коде — единая классификация для `-1..-4`.

**Что не стоит делать:**
- Добавлять интерфейсы или default-классы в этот модуль — для контрактов есть `Kernel.Contracts`.
- Вводить сюда логику процессора или шин — это `Kernel`.
- Менять значения `OpCode`/`OpCodeSize`/`RegType` без версии байткода: числа — это wire-format.
- Полагаться на `default(RamSize)` как на «0» — это «невалидно».
- Дублировать `IdState`-маппинг в своём коде — импортируйте `IdState` из `Common`.

## Особенная информация

### ISA байткода (формат инструкции)

32-битная инструкция (`uint`), little-endian на уровне байт:

```
 бит:  31…20  |  19…18    |  17…13  |  12…8   |  7…0
       spare   | OpCodeSize|   Reg2  |   Reg1  | OpCode
       (12 бит)|  (2 бита) |  (5 бит)|  (5 бит)| (8 бит)
```

- `OpCode` — 8 бит → до 256 опкодов, реально используется ~60.
- `Reg1`, `Reg2` — по 5 бит → 32 регистра, ровно как в `RegType`.
- `OpCodeSize` — 2 бита → 4 варианта (`S8`/`S16`/`S32`/`S64`).
- Биты 20–31 не используются — задел на будущее.

Полная длина инструкции:
- 4 байта — register-register форма (большинство опкодов).
- 12 байт — register-immediate форма: 4 байта инструкции + 8 байт immediate (`ulong`). Наличие immediate определяется только через `InstructionDecoder.HasNeed64IntData(opCode)`.

### Регистровый файл

- `r0`–`r27` — регистры общего назначения.
- `rFL` — флаги: бит 0 — ZeroFlag, бит 1 — NegativeFlag. Остальные биты не используются — задел на Carry/Overflow/Parity.
- `rSP` — stack pointer (для `PUSH`/`POP`/`CALL`/`RET`).
- `rHP` — heap pointer.
- `rIP` — instruction pointer. Не пишите в него напрямую из пользовательского кода — для этого есть `JMP`/`CALL`/`RET`.

Ширина регистров в этом модуле не зафиксирована — определяется процессором. Судя по `OpCodeSize.S64` и `ProcessorFault.FaultData: ulong`, регистры и адреса — 64-битные.

### Endianness

В `Nantuc.Kernel.Common` не определён. `Common` оперирует уже распакованными значениями, порядок байт в памяти — забота `MemoryBus` в `Kernel` (использует `BinaryPrimitives.ReadUInt*LittleEndian`). Для обмена байткодом между платформами — фиксируйте little-endian в `docs/bytecode.md`.

### `HasNeed64IntData` как контракт

Таблица `CreateNeedsUlongOperandTable()` — единственный источник истины о том, у каких опкодов есть 8-байтный immediate. Сейчас покрывает: `LDI`, `LOAD`, `STORE`, `CALL`, `JMP`, `JZ`, `JNZ`, `JG`, `JL`. Метод бросает `ArgumentOutOfRangeException`, если `opCode > OpCode.MOD` — это защита от `(OpCode)200`, но **не** от нового опкода, забытого в таблице: если добавите `OpCode.NEW_IMM` и забудете в таблице, `HasNeed64IntData` вернёт `false`, и процессор прочитает immediate как следующую инструкцию. Единственная защита — code review + тест на полноту таблицы (см. [Что исправить](#что-исправить)).

### `IdState` и `ManagerDevices`

`IdState` описывает коды возврата `ManagerDevices.AddDevice` (`Nantuc.Kernel/Utilites/ManagerDevices.cs`):

| Значение | `IdState` | Причина |
|---|---|---|
| `>= 0` | `Valid` | Успех, ID устройства |
| `-1` | `NotAssigned` | Мест нет (`Count >= _count`) |
| `-2` | `OutOfRange` | Сектор занят (`!_portBus.IsFreeSector(sector)`) |
| `-3` | `NoFreeSlot` | Нет свободного слота и ID кончились |
| `-4` | `Rejected` | `PortBus.RegisterDevice` вернул `false` (устройство **dispose'ится**) |
| другое | `Unknown` | Не должно случаться |

Важно: `IdState` живёт в `Common`, а не в `VmApplication`/`Extensions`, потому что `Kernel` (где `ManagerDevices`) не может зависеть от `VmApplication`. `Common` — единственный слой, видимый и `Kernel`, и `VmApplication`, и `Extensions`. Это осознанное решение, не случайное.

Замечание: само преобразование `int → IdState` (extension-метод `ToIdState` или `TryGetIdState`) **не входит** в этот модуль — его стоит положить туда же, где `IdState`, то есть в `Common`, или в `Kernel` рядом с `ManagerDevices`. Сейчас его нет ни в этой партии файлов, ни в `VmApplication`/`Extensions` (по присланным ранее файлам). См. [Что исправить, п. 8](#что-исправить).

### Ограничения платформ

- Целевой framework: `net10.0`. Используются C# 14 `extension`-блоки в `AlignmentExtensions` и `InstructionDecoder` — старые тулчейны не соберут.
- `unsafe`-кода в этом модуле нет — безопасен для WASM/AOT и окружений с запретом `unsafe`.
- `[MethodImpl(AggressiveInlining)]` — оптимизация под JIT. В AOT/WASM инлайнер ведёт себя иначе; профилируйте на целевой платформе.
- `[Conditional("DEBUG")]` в `AlignmentExtensions` означает: в **Release** проверка `alignment` на степень двойки не выполняется. Невалидный `alignment` даст тихий мусор, а не исключение. Для гарантированной проверки — `AlignUpArithmetic`.
- `Nullable=enable`, `ImplicitUsings=enable`.

### Что этот модуль не делает

- Не содержит интерфейсов — они в `Kernel.Contracts`.
- Не содержит логики процессора, шин, устройств — это `Kernel`.
- Не знает про компилятор MiniC и ассемблер NANASM — только предоставляет им `OpCode`/`RegType`/`OpCodeSize`.
- Не сериализует байткод — формат `.bin`/`.nanas` определяется в `Kernel`/`Extensions`.
- Не содержит `ToIdState`/`TryGetIdState` — только enum `IdState`.

Правило «куда положить тип»: примитив без поведения → `Kernel.Common`; интерфейс/контракт → `Kernel.Contracts`; всё, что исполняет или хранит состояние → `Kernel`.

## Что исправить

По убыванию важности. Ссылки на файлы и члены. Пункты, помеченные **[XML]**, — это то, что стоит задокументировать в первую очередь (сейчас `///` отсутствует или неполный; вы отметили, что работа над XML идёт, поэтому это не «баги», а приоритеты).

1. **`OpCode.CALL_IND`/`JMP_IND` — комментарии не соответствуют именам и реализации.**  
   Файл: `OpCode.cs`, строки `CALL_IND, // программное прерывание` и `JMP_IND, // возврат из прерывания`.  
   По имени `CALL_IND` — indirect call (вызов по адресу из регистра), `JMP_IND` — indirect jump. В `Nantuc.Kernel/ProcessorSystem/Processor.cs` (`InstructionCALL_IND`, `InstructionJMP_IND`) они реализованы именно так: `CALL`/`JMP` по адресу из регистра, без каких-либо прерываний. Комментарии описывают не то, что делает код, и дезориентируют читателя.  
   Фикс: `CALL_IND, // Косвенный вызов: CALL по адресу из регистра` и `JMP_IND, // Косвенный переход: JMP по адресу из регистра`. Либо переименовать опкоды (breaking change для байткода).

2. **`OpCodeSize` — семантика `S8`/`S16`/`S32`/`S64` не задокументирована. [XML]**  
   Файл: `OpCodeSize.cs`. Значения `S8 = 0b00`, `S16 = 0b01`, `S32 = 0b10`, `S64 = 0b11` — это имена, а не «1/2/4/8 байт». Явного маппинга «`S8` = 1 байт» в типе нет; он есть только в `Nantuc.Compiler/C/CodeGenerator/CodeGenUtils.GetSizeInBytes`. Для wire-format это критично: если новый backend решит, что `S8` — это 8 бит (по аналогии с `i8` в Rust), байткод развалится.  
   Фикс: `<summary>1-byte operand (8 bits).</summary>` на каждом члене — и так для всех четырёх. Это самый высокий приоритет среди XML-документации.

3. **`OpCode.LOAD_UNSAFE`/`STORE_UNSAFE`/`LOAD_IND_UNSAFE`/`STORE_IND_UNSAFE` — нет комментариев. [XML]**  
   Файл: `OpCode.cs`, четыре опкода в конце без `//`. По имени понятно, что это unsafe-варианты, но что именно они делают (пропускают проверки границ и выравнивания) — не описано. В `Nantuc.Kernel/ProcessorSystem/Processor.cs` это видно (`InstructionLOAD_UNSAFE` не вызывает `UpdateAndReadStatus`), но из `OpCode` — нет.  
   Фикс: `/// <summary>Load without bounds/alignment checks; caller is responsible for safety.</summary>` (и аналогично для остальных трёх).

4. **`BiosStatus` — часть значений без `///`. [XML]**  
   Файл: `BiosStatus.cs`. `Success`, `SegmentationFault`, `AlignmentFault`, `EndProgramm`, `NotImplementedOpCode` имеют `///`, а `NullDeviceInput`, `NullDeviceOutput`, `InfinityLoop`, `DivOnZero`, `NotSupportedOpCode`, `StackUnderflow` — без. Непонятно, почему часть документирована, часть нет.  
   Фикс: добавить `///` на все члены. В частности, `NullDeviceInput`/`NullDeviceOutput` стоит явно описать как «порт не найден на шине» (это не ошибка, а сигнал), а `InfinityLoop` — как «превышен лимит шагов» (не «бесконечный цикл» в буквальном смысле).

5. **`RegType` — `rFL`/`rSP`/`rHP`/`rIP` не задокументированы. [XML]**  
   Файл: `RegType.cs`. `r0..r27` — регистры общего назначения, `rFL`, `rSP`, `rHP`, `rIP` — специальные. В коде это видно (`Nantuc.Compiler/C/CodeGenerator/RegisterAllocator.cs` резервирует их), но из `RegType` неочевидно.  
   Фикс: `<summary>` на enum с `<remarks>`, или `///` на каждом специальном регистре.

6. **`IdState` — нет XML-документации, `-1..-4` только в комментариях. [XML]**  
   Файл: `IdState.cs`. Члены `NotAssigned // -1` и т.д. — это комментарии, а не `///`. Для публичного enum, который используется для интерпретации возврата `ManagerDevices.AddDevice`, это критично: читатель не поймёт, что `Valid = 0`, а `-1..-4` — конкретные коды.  
   Фикс: `/// <summary>` на каждый член + `/// <remarks>` на enum с таблицей «код → значение → причина». Пример:

   ```csharp
   /// <summary>Classifies the return value of <c>ManagerDevices.AddDevice</c>.</summary>
   /// <remarks>
   /// Values: <c>&gt;= 0</c> — <see cref="Valid"/>; <c>-1</c> — <see cref="NotAssigned"/>;
   /// <c>-2</c> — <see cref="OutOfRange"/>; <c>-3</c> — <see cref="NoFreeSlot"/>; <c>-4</c> — <see cref="Rejected"/>.
   /// </remarks>
   ```

7. **`InstructionDecoder` — методы без XML-документации. [XML]**  
   Файл: `InstructionDecoder.cs`. `GetOpCode`, `GetReg1`, `GetReg2`, `GetDataSizeCode`, `DecodeRawInstToEnums`, `HasNeed64IntData` — без `///`. Для публичного статического класса, который читают авторы расширений, это главный вход в ISA.  
   Фикс: `///` на каждом методе с описанием битовой раскладки (можно сослаться на `docs/bytecode.md`).

8. **`IdState` — нет `ToIdState`/`TryGetIdState` в этой партии файлов.**  
   Файл: `IdState.cs` содержит только enum. Extension-метода, который превращает `int → IdState`, нет ни в этой партии, ни (по присланным ранее) в `VmApplication`/`Extensions`. Значит, `IdState` пока — «мёртвый» тип: его можно использовать в `switch` вручную, но автоматической конвертации нет.  
   Рекомендация: положить `ToIdState` и `TryGetIdState` в `Nantuc.Kernel.Common` (рядом с enum) — это тот же слой, что и `IdState`, и его видит и `Kernel`, и `VmApplication`, и `Extensions`. Пример:

   ```csharp
   public static class IdStateExtensions
   {
       extension(int rawId)
       {
           public IdState ToIdState() => rawId switch
           {
               >= 0 => IdState.Valid,
               -1   => IdState.NotAssigned,
               -2   => IdState.OutOfRange,
               -3   => IdState.NoFreeSlot,
               -4   => IdState.Rejected,
               _    => IdState.Unknown,
           };

           public bool TryGetIdState(out IdState state)
           {
               state = rawId.ToIdState();
               return state == IdState.Valid;
           }
       }
   }
   ```

9. **`PortSize` — неполный набор фабрик относительно `RamSize`.**  
   Файл: `PortSize.cs` — фабрики `B64..B512`, `KB1`, `KB4`, `KB8`, `KB16`, `KB64`, `KB128`, `KB256`, `KB512`. Нет `KB2` и `KB32`, хотя `RamSize` их имеет. Либо это намеренное сужение (порт-пространство редко требует 2KB/32KB), либо забытые фабрики.  
   Фикс: либо добавить `KB2 => new(11)` и `KB32 => new(15)`, либо задокументировать в `<remarks>` на enum/структуре, что набор намеренно разрежен.

10. **`RamSize`/`PortSize`/`DevicePortSize` — нет `TryParse`, `ToString` не для парсинга. [XML]**  
    Файлы: `RamSize.cs`, `PortSize.cs`, `DevicePortSize.cs`. `ToString()` возвращает `"67108864 B (2^26)"` или `"RamSize(invalid)"`. Это не парсится обратно.  
    Рекомендация: либо задокументировать в `///` на `ToString`, что он только для отладки, либо добавить `TryParse` (не обязательно сейчас, но полезно для конфигов).

11. **`InstructionDecoder.HasNeed64IntData` — нет проверки полноты таблицы.**  
    Файл: `InstructionDecoder.cs`. Метод бросает, если `opCode > OpCode.MOD` (это защита от `(OpCode)200`), но не проверяет, что **все** опкоды из `OpCode` есть в таблице. Если новый опкод с immediate забыть в `CreateNeedsUlongOperandTable`, `HasNeed64IntData` вернёт `false`, и процессор прочитает immediate как следующую инструкцию.  
    Рекомендация: добавить юнит-тест «все члены `OpCode`, для которых задуман immediate, отмечены в таблице» — или `Debug.Assert` в статическом конструкторе, что `CreateNeedsUlongOperandTable` покрывает ожидаемые члены. Защита от регрессии при добавлении опкодов.

12. **`AlignmentExtensions` — `AlignUpArithmetic` без `///`. [XML]**  
    Файл: `AlignmentExtensions.cs`. `AlignUp` и `AlignDown` имеют `///`, `AlignUpArithmetic` — нет. Плюс `IsOutOfRangeException` и `GenerateOutOfRangeException` — `private`, но с магической строкой на русском; для консистентности с остальным кодом стоит либо оставить (это `Common`, здесь русские комментарии), либо перевести на английский.  
    Рекомендация: добавить `///` на `AlignUpArithmetic` с пояснением «works for non-power-of-two alignments, always validates `> 0`».

13. **`LogLevel.cs` не был включён в эту партию файлов.**  
    Тип был в предыдущей версии модуля (`enum LogLevel { Log, Warning, Error }`). Если он ещё существует — добавьте в следующую партию, чтобы README и дерево были актуальны. Если удалён — ничего делать не нужно, но стоит обновить `Nantuc.Kernel/README.md`, если он на него ссылается.

14. **`NameDeviceToken` — нет XML-документации. [XML]**  
    Файл: `NameDeviceToken.cs`. Публичный `readonly struct`, используемый как ключ в словарях устройств. Стоит задокументировать: `IsUnknown` для `default`/пустого имени, `Name` возвращает `UnknownName` в этом случае, `Equals`/`GetHashCode` — Ordinal.  
    Рекомендация: `///` на `Name`, `IsUnknown`, `UnknownName`.