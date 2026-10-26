# Nantuc.Kernel.Common — README

> Общие типы, константы ISA и разделяемые примитивы платформы Nantuc.  
> Фундамент, на который опираются и ядро, и контракты, и расширения.  
> `[← Корень](../README.md)`

## Содержание
- [Об этом модуле](#об-этом-модуле)
- [Дерево локального проекта](#дерево-локального-проекта)
- [API](#api)
- [Примеры для API](#примеры-для-api)
- [Приближенность модуля к пользователю](#приближенность-модуля-к-пользователю)
- [Рекомендации](#рекомендации)
- [Особенная информация](#особенная-информация)

## Об этом модуле

`Nantuc.Kernel.Common` — это **слой разделяемых примитивов** платформы. Здесь нет ни логики процессора, ни шин, ни устройств: только типы, которые нужны одновременно ядру (`Kernel`), публичным контрактам (`Kernel.Contracts`), компилятору MiniC, ассемблеру NANASM, дизассемблеру и расширениям. Если провести аналогию с классическим стеком, это не «BIOS» и не «драйверы», а «словарь ISA и единицы измерения памяти/портов» — то, что должно быть согласовано у всех участников платформы.

Модуль намеренно **не содержит интерфейсов** — только `enum`, `readonly struct`, `record struct` и `static class` с чистыми функциями. Это делает его безопасным для использования в горячих путях процессора (нулевые аллокации) и удобным для сериализации/сравнения.

Ключевые роли модуля:
- **ISA байткода**: `OpCode`, `OpCodeSize`, `RegType`, `DecodeInstructionResult`, `InstructionDecoder`.
- **Единицы измерения**: `RamSize`, `PortSize`, `DevicePortSize` (все — лог2-структуры с фабриками).
- **Диагностика**: `BiosStatus`, `ProcessorFault`, `LogLevel`.
- **Идентификация**: `NameDeviceToken`.
- **Утилиты**: `AlignmentExtensions` (выравнивание для generic-чисел).

Проект — **листовой** по зависимостям: он ни от чего внутри Nantuc не зависит и является самым нижним слоем. Поэтому любое изменение публичного типа здесь — потенциальный breaking change для всей платформы.

## Дерево локального проекта

Все типы живут в единственном namespace `Nantuc.Kernel.Common`, структура плоская:

```
Nantuc.Kernel.Common/
├── Nantuc.Kernel.Common.csproj
├── BiosStatus.cs              # enum BiosStatus + record struct ProcessorFault
├── DecodeInstructionResult.cs # readonly struct
├── DevicePortSize.cs          # readonly struct
├── InstructionDecoder.cs      # static class + AlignmentExtensions
├── LogLevel.cs                # enum
├── NameDeviceToken.cs         # readonly struct
├── OpCode.cs                  # enum OpCode : byte
├── OpCodeSize.cs              # enum OpCodeSize
├── RamSize.cs                 # readonly struct
├── RegType.cs                 # enum RegType : byte
└── SizePortOnDevice.cs        # readonly struct PortSize  (имя файла ≠ имя типа)
```

> Файл `SizePortOnDevice.cs` содержит тип `PortSize`. Это стоит переименовать в `PortSize.cs` — иначе поиск «где определён PortSize» ломается на первом же grep.

## API

### Общая картина

В модуле **нет интерфейсов и нет default-классов**. Публичная поверхность — это:

- `enum OpCode : byte` — опкоды VM.
- `enum OpCodeSize` — размер операнда (S8/S16/S32/S64), кодируется 2 битами.
- `enum RegType : byte` — 32 регистра (r0–r27, rFL, rSP, rHP, rIP).
- `enum LogLevel` — Log/Warning/Error.
- `enum BiosStatus : byte` — статус завершения/ошибки + `record struct ProcessorFault`.
- `readonly struct RamSize` — размер ОЗУ, log2, 64 B … 2 GiB.
- `readonly struct PortSize` — размер порт-пространства, log2, 64 B … 512 KiB.
- `readonly struct DevicePortSize` — размер блока портов одного устройства, log2, 8 B … 64 B (см. баг ниже).
- `readonly struct NameDeviceToken` — имя устройства (value-тип, кэширует строку, `IEquatable`).
- `readonly struct DecodeInstructionResult` — результат декодирования сырой инструкции.
- `static class InstructionDecoder` — разбор 32-битной инструкции.
- `static class AlignmentExtensions` — `AlignUp`/`AlignDown` для `IBinaryInteger<T>`.

### Перечисления и структуры

**`OpCode`** — `byte`-enum, ~60 значений, сгруппированных по назначению (системные, память, арифметика, логика, управление потоком, стек, I/O, unsafe-варианты). Комментарии в коде — часть контракта, читайте их.

**`OpCodeSize`** — `S8=0b00, S16=0b01, S32=0b10, S64=0b11`. Семантика «1/2/4/8 байт» подразумевается, но **явно не задана** — это ответственность процессора.

**`RegType`** — 32 значения: `r0…r27`, `rFL` (флаги), `rSP` (stack pointer), `rHP` (heap pointer), `rIP` (instruction pointer). Ровно 32 = 5 бит, что совпадает с полями `Reg1`/`Reg2` в инструкции.

**`BiosStatus`** — статусы: `Success`, `SegmentationFault`, `AlignmentFault`, `EndProgramm`, `NotImplementedOpCode`, `NullDeviceInput`, `NullDeviceOutput`, `InfinityLoop`, `DivOnZero`, `NotSupportedOpCode`, `StackUnderflow`.

**`ProcessorFault`** — `readonly record struct (BiosStatus Status, ulong FaultData, ulong InstructionPointer, OpCode OpCode)`. Удобно возвращать из ядра как результат «мягкого падения».

**`RamSize` / `PortSize` / `DevicePortSize`** — три одинаковых по форме log2-структуры. Ключевое: **конструктор приватный**, экземпляры получают только через фабрики (`RamSize.KB1`, `PortSize.KB64`, `DevicePortSize.B16` …). Валидность — диапазон `Log2`:

| Тип              | MinLog2 | MaxLog2 | Диапазон       |
|------------------|---------|---------|----------------|
| `RamSize`        | 6       | 31      | 64 B … 2 GiB   |
| `PortSize`       | 6       | 19      | 64 B … 512 KiB |
| `DevicePortSize` | 3       | 6       | 8 B … 64 B     |

`default(T)` даёт `IsValid == false` — это осознанный сигнал «не инициализировано».

**`NameDeviceToken`** — `readonly struct`, реализует `IEquatable<NameDeviceToken>`. Внутри кэширует `string?`; пустое имя превращается в `null`, а `Name` отдаёт `UnknownName` (`"Unknown Device"`). Сравнение — `Ordinal`. Хорошо подходит как ключ в словарях устройств.

**`DecodeInstructionResult`** — `readonly struct (OpCode, Reg1, Reg2, OpCodeSize)`. Просто DTO, без логики.

### Классы с логикой

**`InstructionDecoder` (static)** — единственное место, где «зашит» формат инструкции. Публичные методы:

| Метод | Принимает | Возвращает | Что делает |
|---|---|---|---|
| `GetOpCode(uint)` | сырая инструкция | `OpCode` | `raw & 0xFF` |
| `GetReg1(uint)` | | `RegType` | `(raw >> 8) & 0x1F` |
| `GetReg2(uint)` | | `RegType` | `(raw >> 13) & 0x1F` |
| `GetDataSizeCode(uint)` | | `OpCodeSize` | `(raw >> 18) & 0x3` |
| `DecodeRawInstToEnums(uint)` | | `DecodeInstructionResult` | всё сразу |
| `HasNeed64IntData(OpCode)` | опкод | `bool` | есть ли после инструкции 64-битный операнд |

`HasNeed64IntData` — это **таблица контракта**: если добавляете опкод с immediate-операндом, обязаны добавить его в `CreateNeedsUlongOperandTable()`, иначе процессор «съедет» с выравнивания потока инструкций. Сейчас в таблице: `LDI`, `LOAD`, `STORE`, `CALL`, `JMP`, `JZ`, `JNZ`, `JG`, `JL`.

**`AlignmentExtensions` (static)** — через C# 14 `extension`-блок добавляет к `IBinaryInteger<T>` методы `AlignUp`, `AlignDown`, `AlignUpArithmetic`. Требует, чтобы `alignment` был степенью двойки; проверка — только в `DEBUG` (через `[Conditional("DEBUG")]`), в релизе невалидный `alignment` даст мусор. Это осознанный компромисс в пользу скорости — учитывайте при отладке релизных сборок.

## Примеры для API

**Размеры памяти и портов — только через фабрики:**

```csharp
using Nantuc.Kernel.Common;

RamSize ram = RamSize.MB64;          // 64 MiB
PortSize ports = PortSize.KB64;      // 64 KiB адресного пространства портов
DevicePortSize block = DevicePortSize.B32;

Console.WriteLine(ram);              // 67108864 B (2^26)
Console.WriteLine(ram.IsValid);      // true

// Так нельзя — конструктор приватный:
// var bad = new RamSize(10);

// default — это «не инициализировано», а не «0 байт»:
RamSize uninit = default;
Console.WriteLine(uninit.IsValid);   // false
Console.WriteLine(uninit.Bytes);     // 1 (2^0) — не полагайтесь на это
```

**Декодирование инструкции:**

```csharp
uint raw = 0x0000_4A01; // условно: NOP с Reg1=r0, Reg2=r0, Size=S8

OpCode op = InstructionDecoder.GetOpCode(raw);
RegType r1 = InstructionDecoder.GetReg1(raw);
RegType r2 = InstructionDecoder.GetReg2(raw);
OpCodeSize sz = InstructionDecoder.GetDataSizeCode(raw);

DecodeInstructionResult d = InstructionDecoder.DecodeRawInstToEnums(raw);
// d.OpCode, d.Reg1, d.Reg2, d.DataSizeCode

if (InstructionDecoder.HasNeed64IntData(d.OpCode))
{
    // после 4-байтной инструкции идёт 8-байтный immediate — читайте его из памяти
}
```

**Идентификатор устройства:**

```csharp
var token = new NameDeviceToken("UART0".AsSpan());
Console.WriteLine(token.Name);        // "UART0"
Console.WriteLine(token.IsUnknown);   // false

var unknown = new NameDeviceToken(ReadOnlySpan<char>.Empty);
Console.WriteLine(unknown.Name);      // "Unknown Device"
Console.WriteLine(unknown.IsUnknown); // true

// Удобно как ключ:
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
    logger.Log(LogLevel.Error, $"{fault.Status} @ IP=0x{fault.InstructionPointer:X}");
```

**Выравнивание (C# 14 `extension`):**

```csharp
int addr = 13;
int aligned = addr.AlignUp(8);    // 16
int down    = addr.AlignDown(8);  // 8
```

## Приближенность модуля к пользователю

**Низкая.** Это внутренний фундамент платформы, а не пользовательский API.

- Пользователь, который пишет программы на MiniC или NANASM, **не должен** импортировать `Nantuc.Kernel.Common` напрямую — он работает через `VmApplication`/`Kernel`.
- Автор расширений (дизассемблер, отладчик, кастомные устройства, собственные backend'ы) **будет** использовать этот модуль постоянно — здесь лежат опкоды, регистры и единицы измерения.
- Автор компилятора (frontend MiniC → NANASM) обязан согласовывать генерацию с `OpCode` и `HasNeed64IntData` — иначе байткод развалится.

**Что стоит делать:**
- Использовать фабрики `RamSize`/`PortSize`/`DevicePortSize` — не «магические» числа.
- Для декодирования — только `InstructionDecoder`, без ручных сдвигов у себя.
- Передавать `NameDeviceToken` вместо `string`, если имя — идентификатор.
- Проверять `BiosStatus`/`ProcessorFault` вместо bool-флагов.

**Что не стоит делать:**
- Добавлять интерфейсы или default-классы в этот модуль — для контрактов есть `Kernel.Contracts`.
- Вводить сюда логику процессора/шин — это `Kernel`.
- Менять значения `OpCode`/`OpCodeSize`/`RegType` без версии байткода: числа — это wire-format.
- Полагаться на `default(RamSize)` как на «0» — это «невалидно».

## Рекомендации

1. **Переименуйте `SizePortOnDevice.cs` → `PortSize.cs`.** Мелочь, но она экономит время при навигации и в code review.
2. **Почините `DevicePortSize`.** В коде:
   ```csharp
   public const byte MaxLog2 = 6;   // 64 B
   public static DevicePortSize B128 => new(7);
   ```
   `B128` создаёт `Log2=7`, но `IsValid` требует `Log2 ≤ 6`, значит `DevicePortSize.B128.IsValid == false`. Это либо мёртвая фабрика, либо баг в `MaxLog2`. Решите, что правильно: либо `MaxLog2 = 7` (и комментарий «128 B»), либо удалить `B128`. Сейчас это ловушка.
3. **Приведите в порядок XML-документацию.** У `RamSize.Log2`/`Bytes`/`IsValid` есть `///`, у `PortSize`/`DevicePortSize` — нет. Для публичного контракта нижнего слоя стоит либо документировать всё, либо ничего — но единообразно. `GenerateDocumentationFile=true` в `.csproj` заставит компилятор ругаться на пропуски, если хотите дисциплины.
4. **Задокументируйте семантику `OpCodeSize`.** Сейчас `S8/S16/S32/S64` — это имена, а не «1/2/4/8 байт». Явно зафиксируйте маппинг (в комментарии к enum или в `docs/bytecode.md`) — иначе следующий контрибьютор решит иначе.
5. **Исправьте вводящие в заблуждение комментарии в `OpCode`.** `SHR` помечен как «Деление SHR r1, r2 (r1 = r1 >> r2)» — это сдвиг, а не деление (для деления есть `DIV`). А `CALL_IND`/`JMP_IND` описаны как «программное прерывание»/«возврат из прерывания», хотя по имени это indirect call/jump. Либо переименуйте опкоды, либо поправьте комментарии.
6. **Проверьте `HasNeed64IntData` на полноту.** Список покрывает прямые переходы и `LDI/LOAD/STORE`. Но `STORE_IND`, `LOAD_IND`, `CALL_IND`, `JMP_IND` — register-based, им immediate не нужен, это ок. Однако `HALT`, `WAKE`, `WAKE_INT`, `IN`, `OUT`, `CMP`, `TEST`, `SHL`, `MOD`, `INC`, `DEC` — тоже без immediate, тоже ок. Просто зафиксируйте это правило в `docs/bytecode.md`: «immediate есть только у опкодов из таблицы `HasNeed64IntData`», чтобы будущие опкоды не забывали.
7. **Добавьте юнит-тесты на `InstructionDecoder` и size-структуры.** Это самый дешёвый и самый ценный тест в проекте: round-trip encode/decode, границы `IsValid` у всех трёх size-структур, `default(T).IsValid == false`, `NameDeviceToken` на пустом/непустом имени. 20–30 тестов закрывают весь модуль.
8. **Заведите `CHANGELOG.md` для ISA.** Как только `OpCode` начнёт меняться, понадобится версия байткода (`v1`, `v2`, …) и запись «что добавлено/удалено/переименовано». Без этого дизассемблер и старые `.bin`-файлы разъедутся.

## Особенная информация

### ISA байткода (формат инструкции)

32-битная инструкция (`uint`), little-endian на уровне байт (порядок байт в памяти — ответственность `Kernel`):

```
 бит:  31 … 20  |  19 … 18  |  17 … 13  |  12 … 8  |  7 … 0
       spare    | OpCodeSize |   Reg2    |   Reg1   | OpCode
       (12 бит) |  (2 бита)  |  (5 бит)  |  (5 бит) | (8 бит)
```

- `OpCode` — 8 бит → до 256 опкодов, реально используется ~60.
- `Reg1`, `Reg2` — по 5 бит → 32 регистра, ровно столько, сколько в `RegType`.
- `OpCodeSize` — 2 бита → 4 варианта (`S8/S16/S32/S64`).
- Биты 20–31 не используются — задел на будущее (флаги, режимы адресации, расширение регистров). **Не занимайте их без версии байткода.**

Полная длина инструкции:
- **4 байта** — register-register форма (большинство опкодов).
- **12 байт** — register-immediate форма: 4 байта инструкции + 8 байт immediate (`ulong`). Наличие immediate определяется **только** через `InstructionDecoder.HasNeed64IntData(opCode)`.

### Регистровый файл

- `r0`–`r27` — регистры общего назначения.
- `rFL` — флаги (результат `CMP`/`TEST`).
- `rSP` — stack pointer (для `PUSH`/`POP`/`CALL`/`RET`).
- `rHP` — heap pointer (для динамической памяти).
- `rIP` — instruction pointer (не пишите в него напрямую из пользовательского кода — для этого есть `JMP`/`CALL`/`RET`).

Ширина регистров в этом модуле не зафиксирована — она определяется процессором. Судя по `OpCodeSize.S64` и `FaultData: ulong`, регистры и адреса — 64-битные. Но это **соглашение**, а не тип в `Common` — при работе с `Processor` уточняйте у ядра.

### Endianness

В `Nantuc.Kernel.Common` **не определён**. Это осознанно: `Common` оперирует уже распакованными значениями, а порядок байт в памяти — забота `MemoryBus` в `Kernel`. Если планируете обмениваться байткодом между платформами — зафиксируйте little-endian в `docs/bytecode.md` и проверяйте его в тестах.

### Ограничения платформ

- **Целевой framework: `net10.0`.** Модуль использует C# 14 `extension`-блоки (в `InstructionDecoder` и `AlignmentExtensions`) — это новая синтаксическая конструкция, старые тулчейны её не соберут. Убедитесь, что CI ставит .NET 10 SDK preview/GA.
- **`unsafe`-кода в этом модуле нет.** Это плюс: он безопасен для WASM/AOT и для окружений, где `unsafe` запрещён. Если будете добавлять сюда что-то с `Span`/`stackalloc` — следите, чтобы не протащить `unsafe`.
- **`[MethodImpl(AggressiveInlining)]`** на `InstructionDecoder` и `IsPowerOfTwo` — это оптимизация под JIT. В AOT/WASM инлайнер ведёт себя иначе; профилируйте на целевой платформе, не полагайтесь на эти атрибуты как на гарантию.
- **`[Conditional("DEBUG")]`** в `AlignmentExtensions` означает, что в **Release** проверка `alignment` на степень двойки **не выполняется**. Для WASM/AOT, где отладка сложнее, это особенно важно: невалидный `alignment` даст тихий мусор, а не исключение. Если сомневаетесь — используйте `AlignUpArithmetic` (там проверка `> 0` есть всегда) или добавляйте явную проверку у себя.
- **`Nullable=enable`, `ImplicitUsings=enable`** — учитывайте при добавлении файлов: `string?` и неявные `using System.*` включены по умолчанию.

### Что этот модуль **не** делает

- Не содержит интерфейсов — они в `Kernel.Contracts`.
- Не содержит логики процессора, шин, устройств — это `Kernel`.
- Не знает про компилятор MiniC и ассемблер NANASM — он только предоставляет им `OpCode`/`RegType`/`OpCodeSize`.
- Не сериализует байткод — формат `.bin`/`.nanas` определяется в `Kernel`/`Extensions`.

Если сомневаетесь, «куда положить тип» — правило простое: **примитив без поведения → `Kernel.Common`; интерфейс/контракт → `Kernel.Contracts`; всё, что исполняет или хранит состояние → `Kernel`.**