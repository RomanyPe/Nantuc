# Nantuc.Kernel.Common — README

> Общие типы, константы ISA и разделяемые примитивы платформы Nantuc.  
> Фундамент, на который опираются ядро, компилятор и расширения.  
> `[← Корень](../README.md)`

## Содержание
- [Об этом модуле](#об-этом-модуле)
- [Дерево локального проекта](#дерево-локального-проекта)
- [API](#api)
- [Примеры для API](#примеры-для-api)
- [Приближенность модуля к пользователю](#приближенность-модуля-к-пользователю)
- [Особенная информация](#особенная-информация)
- [Что исправить](#что-исправить)

## Об этом модуле

`Nantuc.Kernel.Common` — это слой разделяемых примитивов платформы. Здесь нет ни логики процессора, ни шин, ни устройств: только типы, которые нужны одновременно ядру, компилятору MiniC, ассемблеру NANASM, дизассемблеру и расширениям. Это словарь ISA и единицы измерения памяти и портов — то, что должно быть согласовано у всех участников платформы.

Модуль не содержит интерфейсов — только `enum`, `readonly struct`, `record struct` и `static class` с чистыми функциями. Это делает его безопасным для горячих путей процессора (нулевые аллокации) и удобным для сравнения и сериализации.

Ключевые роли модуля:
- ISA байткода: `OpCode`, `OpCodeSize`, `RegType`, `DecodeInstructionResult`, `InstructionDecoder`.
- Единицы измерения: `RamSize`, `PortSize`, `DevicePortSize` (лог2-структуры с фабриками).
- Диагностика: `BiosStatus`, `ProcessorFault`.
- Классификация возвратов: `IdState`.
- Утилиты: `AlignmentExtensions` (выравнивание для generic-чисел).

Проект — листовой по зависимостям: `net10.0`, без `ProjectReference`. Любое изменение публичного типа здесь — потенциальный breaking change для всей платформы.

> **XML-документация в работе.** Полностью документированы `PortSize`, `RamSize`, `InstructionDecoder`, `BiosStatus`. Частично — `DevicePortSize` (фабрики и структура — с `///`, операторы и `Equals`/`GetHashCode`/`CompareTo`/`ToString` — без), `DecodeInstructionResult` (структура и параметры конструктора — с `///`, публичные поля — без), `OpCode` (inline-комментарии на большинстве опкодов, без `///`), `RegType` (только `rFL`/`rSP`/`rHP`/`rIP`). Без `///` совсем — `IdState` (только inline-комментарии `// -1`), `AlignUpArithmetic` в `AlignmentExtensions`. В разделе [Что исправить](#что-исправить) — список того, что стоит задокументировать в первую очередь.

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
├── OpCode.cs                      # enum OpCode : byte
├── OpCodeSize.cs                  # enum OpCodeSize
├── PortSize.cs                    # readonly struct PortSize
├── RamSize.cs                     # readonly struct RamSize
└── RegType.cs                     # enum RegType : byte
```

Замечания по организации:
- `AlignmentExtensions.cs` вынесен из `InstructionDecoder.cs` — семантически разные классы больше не делят файл.
- `NameDeviceToken.cs` и `LogLevel.cs` **в эту партию файлов не входят**. Если типы ещё существуют в проекте — добавьте их в следующую партию; если удалены — обновите дерево.
- `PortSize.cs` назван по имени типа (ранее — `SizePortOnDevice.cs`), навигация исправлена.

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
| `DecodeInstructionResult` | `readonly struct` | Результат декодирования сырой инструкции |
| `InstructionDecoder` | `static class` | Разбор 32-битной инструкции + таблица «требуется ли immediate» |
| `AlignmentExtensions` | `static class` | `AlignUp`/`AlignDown`/`AlignUpArithmetic` для `IBinaryInteger<T>` |

### Перечисления

**`OpCode : byte`** — около 60 значений, сгруппированных по назначению (inline-комментарии на большинстве):

- Системные: `NOP`, `END`.
- Память: `MOV`, `LOAD`, `STORE`, `LDI`, `LOAD_IND`, `STORE_IND`.
- Арифметика: `ADD`, `SUB`, `MULT_INT`, `SHR`, `SHL`, `INC`, `DEC`, `DIV`, `MOD`.
- Логика: `AND`, `OR`, `XOR`, `NOT`.
- Управление потоком: `JMP`, `JZ`, `JNZ`, `JG`, `JL`, `JMP_IND` (косвенный переход по адресу из регистра).
- Стек: `PUSH`, `POP`, `CALL`, `RET`, `CALL_IND` (косвенный вызов по адресу из регистра).
- I/O: `IN`, `OUT`.
- Сон: `HALT`, `WAKE`, `WAKE_INT`.
- Unsafe-варианты: `LOAD_UNSAFE`, `STORE_UNSAFE`, `STORE_IND_UNSAFE`, `LOAD_IND_UNSAFE` (без inline-комментариев).
- Сравнение: `CMP`, `TEST`.

**`OpCodeSize`** — `S8 = 0b00` (8 бит = 1 байт), `S16 = 0b01` (16 бит), `S32 = 0b10` (32 бита), `S64 = 0b11` (64 бита). XML-документация присутствует на enum и на каждом члене.

**`RegType : byte`** — 32 значения: `r0..r27` (общего назначения, без `///`), `rFL` (регистр флагов), `rSP` (указатель стека), `rHP` (указатель кучи), `rIP` (указатель инструкций) — у четырёх последних есть `///`. Ровно 32 = 5 бит, что совпадает с полями `Reg1`/`Reg2` в инструкции.

**`BiosStatus : byte`** — `Success`, `SegmentationFault`, `AlignmentFault`, `EndProgramm`, `NotImplementedOpCode`, `NullDeviceInput`, `NullDeviceOutput`, `InfinityLoop`, `DivOnZero`, `NotSupportedOpCode`, `StackUnderflow`. Все члены документированы `///`.

**`IdState`** — `Valid`, `NotAssigned` (`-1`), `OutOfRange` (`-2`), `NoFreeSlot` (`-3`), `Rejected` (`-4`), `Unknown`. Соответствует кодам возврата `ManagerDevices.AddDevice`. Живёт в `Common`, потому что `Kernel` (где `ManagerDevices`) не может зависеть от слоёв уровнем выше, а `Common` — единственный слой, видимый и `Kernel`, и потребителям. **XML-документации нет** — только inline `// -1` и т.д. и `/// <summary>` на самом enum.

### `ProcessorFault` — `readonly record struct`

```csharp
public readonly record struct ProcessorFault(
    BiosStatus Status,
    ulong FaultData,
    ulong InstructionPointer,
    OpCode OpCode);
```

Возвращается из ядра как результат «мягкого падения». `in`-параметр в политиках обработки — без копирования. XML-документация на структуре и всех параметрах конструктора.

### Размеры: `RamSize`, `PortSize`, `DevicePortSize`

Три одинаковые по форме лог2-структуры. Конструктор приватный, экземпляры получают только через фабрики. Валидность — диапазон `Log2`:

| Тип | MinLog2 | MaxLog2 | Диапазон |
|---|---|---|---|
| `RamSize` | 6 | 31 | 64 B … 2 GiB |
| `PortSize` | 6 | 19 | 64 B … 512 KiB |
| `DevicePortSize` | 3 | 8 | 8 B … 256 B |

`default(T)` даёт `IsValid == false` — сигнал «не инициализировано», но `Bytes` для `default` возвращает `1` (следствие `1UL << 0`). У всех трёх: `Bytes => 1UL << Log2`, операторы `==`/`!=`/`<`/`>`/`<=`/`>=`, `IEquatable<T>`, `IComparable<T>`, `ToString()`. Сравнение — по сырому `Log2`, поэтому невалидное значение по умолчанию меньше любого валидного.

Фабрики:

| Тип | Доступные фабрики |
|---|---|
| `RamSize` | `B64..B512`, `KB1, KB2, KB4, KB8, KB16, KB32, KB64, KB128, KB256, KB512`, `MB1..MB512`, `GB1, GB2` — полный набор |
| `PortSize` | `B64..B512`, `KB1, KB4, KB8, KB16, KB64, KB128, KB256, KB512` — **нет `KB2` и `KB32`** (задокументировано в `<remarks>` как намеренное) |
| `DevicePortSize` | `B8, B16, B32, B64, B128, B256` — полный набор от 8 B до 256 B |

Разница в XML-покрытии: `PortSize` и `RamSize` документированы полностью, `DevicePortSize` — фабрики и структура с `///`, а `Equals`/`GetHashCode`/`CompareTo`/`ToString` и операторы без `///`.

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

DTO без логики. `///` есть на структуре и `<param>` конструктора, но **нет `///` на публичных полях** (`OpCode`, `Reg1`, `Reg2`, `DataSizeCode`). Не `record` — `Equals`/`GetHashCode`/`==` не сгенерированы; сравнение — вручную.

### `InstructionDecoder` — `static class`

| Метод | Принимает | Возвращает | Что делает |
|---|---|---|---|
| `GetOpCode(uint)` | сырая инструкция | `OpCode` | `raw & 0xFF` |
| `GetReg1(uint)` | | `RegType` | `(raw >> 8) & 0x1F` |
| `GetReg2(uint)` | | `RegType` | `(raw >> 13) & 0x1F` |
| `GetDataSizeCode(uint)` | | `OpCodeSize` | `(raw >> 18) & 0x3` |
| `DecodeRawInstToEnums(uint)` | | `DecodeInstructionResult` | все поля за один проход |
| `HasNeed64IntData(OpCode)` | опкод | `bool` | есть ли после инструкции 64-битный операнд; бросает `ArgumentOutOfRangeException`, если `opCode > OpCode.MOD` |

Плюс extension-метод `Int()` на `RegType` (приведение к `int`) — определён в том же `extension`-блоке.

`HasNeed64IntData` — таблица контракта `CreateNeedsUlongOperandTable()`. Сейчас в ней отмечены: `LDI`, `LOAD`, `STORE`, `CALL`, `JMP`, `JZ`, `JNZ`, `JG`, `JL`. Проверка диапазона — `(uint)opCode > (uint)OpCode.MOD` — устойчива к смене базового типа `OpCode` на знаковый. `CreateNeedsUlongOperandTable` — `private`, 256-элементный `ImmutableArray<bool>`, инициализируется один раз в static-поле `_has`.

Все публичные методы `[MethodImpl(AggressiveInlining)]`, оптимизация под JIT.

### `AlignmentExtensions` — `static class`

Через C# 14 `extension`-блок добавляет к `IBinaryInteger<T>`:

| Метод | Семантика | Проверка `alignment` |
|---|---|---|
| `AlignUp(T)` | вверх до кратного | только `DEBUG`, требует степень двойки |
| `AlignDown(T)` | вниз до кратного | только `DEBUG`, требует степень двойки |
| `AlignUpArithmetic(T)` | вверх через `%` | всегда, требует `> 0` (не обязательно степень двойки) |

`AlignUp`/`AlignDown` в **Release** не проверяют `alignment` — невалидное значение даст тихий мусор. Для гарантированной проверки — `AlignUpArithmetic`. XML-документация на `AlignUp`/`AlignDown` есть, на `AlignUpArithmetic` — **нет**.

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
Console.WriteLine(uninit.Bytes);     // 1 — не 0!
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
- Проверять `BiosStatus`/`ProcessorFault` вместо `bool`-флагов.
- Использовать `IdState` вместо `raw switch { -1 => ..., -2 => ... }` в своём коде — единая классификация для `-1..-4`.

**Что не стоит делать:**
- Добавлять интерфейсы или default-классы в этот модуль — для контрактов есть `Kernel.Contracts`.
- Вводить сюда логику процессора или шин — это `Kernel`.
- Менять значения `OpCode`/`OpCodeSize`/`RegType` без версии байткода: числа — это wire-format.
- Полагаться на `default(RamSize)` как на «0» — это «невалидно», и `Bytes` вернёт `1`.
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
- `rFL` — регистр флагов (бит 0 — ZeroFlag, бит 1 — NegativeFlag; остальные — задел).
- `rSP` — stack pointer (для `PUSH`/`POP`/`CALL`/`RET`).
- `rHP` — heap pointer.
- `rIP` — instruction pointer. Не пишите в него напрямую из пользовательского кода — для этого есть `JMP`/`CALL`/`RET`.

Ширина регистров в этом модуле не зафиксирована — определяется процессором. Судя по `OpCodeSize.S64` и `ProcessorFault.FaultData: ulong`, регистры и адреса — 64-битные.

### WASM / кроссплатформенность

| Аспект | Статус | Комментарий |
|---|---|---|
| Сборка в WASM | ✅ | Нет `unsafe`, `DllImport`, `LibraryImport`. Все типы — managed. Собирается под `browser-wasm` без правок csproj. |
| `NativeMemory` | ✅ | Не используется. `Bytes` — managed-сдвиг `1UL << Log2`. |
| `stackalloc` / `InlineArray` | ✅ | Не используются. Таблица `InstructionDecoder._has` — `ImmutableArray<bool>`, создаётся в static-инициализации через `new bool[256]`. |
| `Interlocked` / `Volatile` | ✅ | Не используются. Shared-состояние — единственное `static readonly ImmutableArray<bool> _has` в `InstructionDecoder.cs`, инициализируется один раз (CLR гарантирует единственность static-ctor). |
| `IFileService` | n/a | В этой партии файлов интерфейса нет — оценивать нечего. При добавлении в другие слои WASM-реализацию придётся писать отдельно (виртуальная ФС). |
| `Regex` / trimming | ✅ | Regex не используется; нет рефлексии, `Type.GetType`, `Activator.CreateInstance`, `dynamic`. Trimming-совместимо из коробки. |
| AOT | ✅ | Нет `Expression.Compile`, `MakeGenericMethod`/`MakeGenericType`. Generic math (`IBinaryInteger<T>` в `AlignmentExtensions`) моноформируется под каждый используемый `T`. C# 14 `extension`-блоки — compile-time. |

**Итог:** код полностью совместим с WASM (interpreter и AOT) и с trimming. Единственная неопределённость — отсутствующий в поставке `IFileService`.

### Endianness

В `Nantuc.Kernel.Common` не определён. `Common` оперирует уже распакованными значениями, порядок байт в памяти — забота слоя `Kernel`. Для обмена байткодом между платформами фиксируйте little-endian в `docs/bytecode.md`.

### `HasNeed64IntData` как контракт

Таблица `CreateNeedsUlongOperandTable()` — единственный источник истины о том, у каких опкодов есть 8-байтный immediate. Сейчас покрывает: `LDI`, `LOAD`, `STORE`, `CALL`, `JMP`, `JZ`, `JNZ`, `JG`, `JL`. Метод бросает `ArgumentOutOfRangeException`, если `(uint)opCode > (uint)OpCode.MOD` — это защита и от `(OpCode)200`, и от отрицательных значений при смене базового типа на знаковый (в отличие от предыдущей версии, где проверка была только сверху — теперь исправлено). Но **не** от нового опкода, забытого в таблице.

Не отмечены `LOAD_IND`, `STORE_IND`, `CALL_IND`, `JMP_IND`, `LOAD_UNSAFE`, `STORE_UNSAFE`, `LOAD_IND_UNSAFE`, `STORE_IND_UNSAFE`. Если они принимают 64-битный операнд — таблица даёт неверный ответ. Единственная защита — code review + тест на полноту таблицы (см. [Что исправить](#что-исправить), п. 1).

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
- Не содержит `ToIdState`/`TryGetIdState` — только enum `IdState` (см. [Что исправить](#что-исправить), п. 4).

Правило «куда положить тип»: примитив без поведения → `Kernel.Common`; интерфейс/контракт → `Kernel.Contracts`; всё, что исполняет или хранит состояние → `Kernel`.