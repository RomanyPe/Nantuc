# Nantuc.Common — README

> Общие типы, константы ISA и разделяемые примитивы платформы Nantuc.  
> Фундамент, на который опираются ядро, компилятор и расширения.  
> [← Корень](../README.ru.md), [Contracts](../../src/Nantuc.Contracts/README.ru.md), [Kernel](../../src/Nantuc.Kernel/README.ru.md)

## Содержание
- [Об этом модуле](#об-этом-модуле)
- [Дерево локального проекта](#дерево-локального-проекта)
- [API](#api)
- [Примеры для API](#примеры-для-api)
- [Приближенность модуля к пользователю](#приближенность-модуля-к-пользователю)
- [Особенная информация](#особенная-информация)
- [Что исправить](#что-исправить)

## Об этом модуле

`Nantuc.Common` — это слой разделяемых примитивов платформы. Здесь нет ни логики процессора, ни шин, ни устройств: только типы, которые нужны одновременно ядру, компилятору MiniC, ассемблеру NANASM, дизассемблеру и расширениям. Это словарь ISA и единицы измерения памяти и портов — то, что должно быть согласовано у всех участников платформы.

Модуль не содержит интерфейсов — только `enum`, `readonly struct`, `record struct` и `static class` с чистыми функциями. Это делает его безопасным для горячих путей процессора (нулевые аллокации) и удобным для сравнения и сериализации.

Ключевые роли модуля:
- ISA байткода: `OpCode`, `OpCodeSize`, `RegType`, `DecodeInstructionResult`, `InstructionDecoder`.
- Единицы измерения: `RamSize`, `PortSize`, `DevicePortSize` (лог2-структуры с фабриками).
- Диагностика: `BiosStatus`, `ProcessorFault`.
- Классификация возвратов: `IdState`.
- Утилиты: `AlignmentExtensions` (выравнивание для generic-чисел), extension-классы для имён (`RegTypeExtension`, `OpCodeSizeExtension`, `BiosStatusExtension`, `IdStateExtension`, `OpCodeExtension`).

Проект — листовой по зависимостям: `net10.0`, без `ProjectReference`. Любое изменение публичного типа здесь — потенциальный breaking change для всей платформы.

> **Состояние документации.** XML-документация почти полная. Закрывающие теги в `DevicePortSize.cs` исправлены, `HasNeed64IntData` теперь покрывает `LOAD_UNSAFE`/`STORE_UNSAFE` и проверяет диапазон через `OpCodeExtension.LastValue`. **Один breaking-контракт по `IdState`:** `Valid = 0` (значение по умолчанию), `Unknown = -5`. Подробности — в разделе [Что исправить](#что-исправить).

## Дерево локального проекта

Все типы живут в namespace `Nantuc.Common`, структура плоская:

```
Nantuc.Common/
├── Nantuc.Common.csproj           # net10.0, без ProjectReference
├── AlignmentExtensions.cs         # static class AlignmentExtensions
├── BiosStatus.cs                  # enum BiosStatus (13 членов)
├── BiosStatusExtension.cs         # static class BiosStatusExtension (StatusName)
├── DecodeInstructionResult.cs     # readonly record struct
├── DevicePortSize.cs              # readonly struct
├── IdState.cs                     # enum IdState (Valid = 0, Unknown = -5)
├── IdStateExtension.cs            # static class IdStateExtension (ToIdState)
├── InstructionDecoder.cs          # static class InstructionDecoder
├── OpCode.cs                      # enum OpCode : byte
├── OpCodeExtension.cs             # static class OpCodeExtension (LastValue)
├── OpCodeSize.cs                  # enum OpCodeSize
├── OpCodeSizeExtension.cs         # static class OpCodeSizeExtension (ToUint, SizeName)
├── PortSize.cs                    # readonly struct PortSize
├── ProcessorFault.cs              # readonly record struct
├── ProcessorSnapshot.cs           # readonly struct ProcessorSnapshot, [InlineArray(32)]
├── RamSize.cs                     # readonly struct RamSize
├── RegType.cs                     # enum RegType : byte
└── RegTypeExtension.cs            # static class RegTypeExtension (Int, ToUint, RegName)
```

Замечания по организации:
- `ProcessorFault` вынесен из `BiosStatus.cs` в отдельный `ProcessorFault.cs`.
- `AlignmentExtensions.cs` вынесен из `InstructionDecoder.cs`.
- Каждый enum снабжён companion-классом `*Extension` с именующими/преобразующими extension-членами и константами `LastValue`/`LastValueInt`.
- `PortSize` и `RamSize` симметричны: оба имеют `KB2` и `KB32`.
- `ProcessorSnapshot` — `readonly struct` с `[InlineArray(RegisterCount)] (RegisterCount = 32)` для хранения копии регистров. Два конструктора:
    - `(ReadOnlySpan<ulong> span, bool isRunning, bool isSleeping, uint rawInstruction, ulong dataByInstruction)` — `CurrentBiosStatus = BiosStatus.Success`;
    - `(..., BiosStatus currentBiosStatus)` — то же плюс явный статус.
    - `GetArr(ReadOnlySpan<ulong>)` копирует ровно RegisterCount = 32 элементов и бросает `ArgumentException` при `span.Length` ≠ 32 (раньше — `IndexOutOfRangeException` при > 32 и молчаливые нули при < 32).

## API

### Общая картина

В модуле нет интерфейсов и default-реализаций. Публичная поверхность:

| Тип | Вид | Назначение |
|---|---|---|
| `OpCode` | `enum : byte` | Опкоды VM (~60 значений), полная XML-документация |
| `OpCodeSize` | `enum` | Размер операнда: `S8`/`S16`/`S32`/`S64` |
| `RegType` | `enum : byte` | 32 регистра: `r0..r27`, `rFL`, `rSP`, `rHP`, `rIP` |
| `BiosStatus` | `enum : byte` | Статус завершения/ошибки (13 членов) |
| `IdState` | `enum : int` | Классификация `-1..-5` от `ManagerDevices.AddDevice` |
| `ProcessorFault` | `readonly record struct` | `(BiosStatus, ulong FaultData, ulong IP, OpCode)` |
| `RamSize` | `readonly struct` | Размер ОЗУ, лог2, 64 B … 2 GiB |
| `PortSize` | `readonly struct` | Размер порт-пространства, лог2, 64 B … 512 KiB |
| `DevicePortSize` | `readonly struct` | Размер блока портов устройства, лог2, 8 B … 256 B |
| `DecodeInstructionResult` | `readonly record struct` | Результат декодирования |
| `InstructionDecoder` | `static class` | Разбор 32-битной инструкции + таблица immediate |
| `AlignmentExtensions` | `static class` | `AlignUp`/`AlignDown`/`AlignUpArithmetic` для `IBinaryInteger<T>` |
| `RegTypeExtension` | `static class` | `Int()`, `ToUint()`, `AsText`, `LastValue` |
| `OpCodeSizeExtension` | `static class` | `ToUint()`, `AsText`, `LastValue` |
| `BiosStatusExtension` | `static class` | `AsText`, `LastValue` |
| `IdStateExtension` | `static class` | `ToIdState` (extension-свойство на `int`) |
| `OpCodeExtension` | `static class` | `LastValue` (последний опкод, для range-check) |
| `ProcessorSnapshot` | `readonly struct` | `IsRunning`, `IsSleeping`, `GetRegister(RegType)`; `[InlineArray(RegisterCount)]`-хранилище (32); `RawInstruction`, `DataByInstruction`, `CurrentBiosStatus`, `DataFault`; декодированные из `RawInstruction`: `OpCode`, `FirstRegister`, `SecondRegister`, `DataSizeCode`|


### Перечисления

**`OpCode : byte`** — около 60 значений, каждый с `///` и подробным `<remarks>`. Группы:

- Системные: `NOP`, `END` (`END` завершает программу, сбрасывает `IsRunning`; `HALT` — приостановка).
- Память: `MOV`, `LOAD`/`STORE` (проверяемые через `MemoryBus`), `LDI` (64-битный immediate из потока), `LOAD_IND`/`STORE_IND` (адрес из регистра).
- Арифметика: `ADD`, `SUB`, `MULT_INT` (wrap-around), `SHR`/`SHL` (величина сдвига — младший байт `R2`), `INC`, `DEC`, `DIV` (`DivOnZero`), `MOD` (`DivOnZero`).
- Логика: `AND`, `OR`, `XOR` (для обнуления `XOR R1, R1`), `NOT`.
- Управление потоком: `JMP`, `JZ`, `JNZ`, `JG` (сброшены Zero и Negative), `JL` (установлен Negative), `JMP_IND` (косвенный переход по адресу из регистра, с проверкой границ).
- Стек: `PUSH` (`rSP -= 8`, коллизия с кучей -> `SegmentationFault`), `POP` (`rSP += 8`), `CALL` (сохраняет адрес возврата), `RET` (пустой стек -> `StackUnderflow`), `CALL_IND` (косвенный вызов по адресу из регистра).
- I/O: `IN`, `OUT` (младший байт регистра-источника).
- Сон: `HALT` (`IsSleeping = true`), `WAKE` (внешняя команда), `WAKE_INT` (через порт, нет порта -> `NullDeviceOutput`).
- Unsafe-варианты: `LOAD_UNSAFE`/`STORE_UNSAFE` (быстрый путь по абсолютному адресу без проверок), `STORE_IND_UNSAFE`/`LOAD_IND_UNSAFE` (косвенный по регистру, без проверок).
- Сравнение: `CMP` (`R1 - R2` -> флаги `rFL`), `TEST` (`R1 & R2` -> флаги `rFL`), `MOD`.
- Привидение: `CASTZ`, `CASTS` приводит `ulong` из регистра к `S8` (1 байт), `S16` (2 байта), `S32` (4 байта).

**`OpCodeSize`** — `S8 = 0b00` (1 байт), `S16 = 0b01` (2 байта), `S32 = 0b10` (4 байта), `S64 = 0b11` (8 байт). Полный XML, включая `<remarks>` о битовой кодировке. `OpCodeSizeExtension.LastValue == OpCodeSize.S64`.

**`RegType : byte`** — 32 значения: `r0..r27` (регистры общего назначения, все с `///`), `rFL` (флаги Zero/Negative), `rSP` (stack pointer), `rHP` (heap pointer), `rIP` (instruction pointer). `<remarks>` на enum фиксирует: 32 регистра по 64 бита в `ArrayRegisters` (`[InlineArray(32)]` из `ulong`), нет аппаратно-занулённого регистра (обнуление через `XOR rN, rN`), порядок 0..31 — часть контракта. Ровно 32 = 5 бит.

**`BiosStatus : byte`** — **13 членов** (в этой версии добавлены `HostStopped` и `UnsafeOpCodeForbidden`):

| Значение | Член | Назначение |
|---|---|---|
| 0 | `Success` | Успех, значение по умолчанию |
| 1 | `SegmentationFault` | Выход за границы ОЗУ |
| 2 | `AlignmentFault` | Невыровненный доступ |
| 3 | `EndProgram` | Штатное завершение (`END`) |
| 4 | `HostStopped` | **Новое:** экстренное завершение от хоста |
| 5 | `NotImplementedOpCode` | Нет обработчика (default в `Step`) |
| 6 | `NullDeviceInput` | Устройство ввода не ответило |
| 7 | `NullDeviceOutput` | Устройство вывода не ответило |
| 8 | `InfinityLoop` | Политика обнаружила зацикливание |
| 9 | `DivOnZero` | Деление на ноль (`DIV`/`MOD`) |
| 10 | `NotSupportedOpCode` | Опкод известен, но не поддержан |
| 11 | `StackUnderflow` | Переполнение/недопустимое состояние стека |
| 12 | `UnsafeOpCodeForbidden` | **Новое:** запрет unsafe-опкода политикой |

`<remarks>` на enum предупреждает: `EndProgram`, `HostStopped` и `InfinityLoop` — **не ошибки по смыслу**, но проходят через путь `ProcessorFault`/`IProcessorPolicy`; политика обязана их различать.

**`IdState`** — **важно:** `Valid = 0` (значение по умолчанию), `Unknown = -5`:

| Значение | Член | Причина |
|---|---|---|
| `0` | `Valid` | Id валиден и принят системой (default) |
| `-1` | `NotAssigned` | Id не присвоен (регистрация не выполнялась) |
| `-2` | `OutOfRange` | Id вне допустимого диапазона |
| `-3` | `NoFreeSlot` | Нет свободного слота |
| `-4` | `Rejected` | Id отклонён системой |
| `-5` | `Unknown` | Статус не определён (и все значения `< -5`) |

`<remarks>` на enum прямо указывает: значение по умолчанию (`0`) соответствует `Valid`, поэтому неинициализированное поле типа `IdState` интерпретируется как успешный статус. Если нужно отличать «не инициализировано» от «валидно», используйте `IdState?`. Числовые значения зафиксированы явно и объявлены частью контракта.

**`IdStateExtension.ToIdState`** — extension-свойство на `int`, тотально преобразует числовой Id:

| Id | `IdState` |
|---|---|
| `>= 0` | `Valid` |
| `-1` | `NotAssigned` |
| `-2` | `OutOfRange` |
| `-3` | `NoFreeSlot` |
| `-4` | `Rejected` |
| любое иное (в т.ч. `-5`) | `Unknown` |

Не бросает исключений; применимо к произвольным внешним данным.

### `ProcessorFault` — `readonly record struct`

```csharp
public readonly record struct ProcessorFault(
    BiosStatus Status,
    ulong FaultData,
    ulong InstructionPointer,
    OpCode OpCode);
```

Полный XML: `<summary>` + `<remarks>` + `<param>` на каждый параметр. Ключевые факты из `<remarks>`:
- Формируется процессором при статусе ≠ `Success`, передаётся в `IProcessorPolicy.ShouldContinue`.
- **Не исключение**, value-type: копируется по стеку, не аллоцирует память, пригоден для передачи между потоками без блокировок.
- **Соглашение о «неинициализированном» значении:** используется `OpCode.NOP` (значение 0). `<remarks>` явно называет это спорным решением и рекомендует в будущем заменить на `OpCode?` или отдельный сентинел.

### Размеры: `RamSize`, `PortSize`, `DevicePortSize`

Три одинаковые по форме лог2-структуры. Конструктор приватный, экземпляры — только через фабрики. Валидность — диапазон `Log2`:

| Тип | MinLog2 | MaxLog2 | Диапазон |
|---|---|---|---|
| `RamSize` | 6 | 31 | 64 B … 2 GiB |
| `PortSize` | 6 | 19 | 64 B … 512 KiB |
| `DevicePortSize` | 3 | 8 | 8 B … 256 B |

`default(T)` даёт `IsValid == false`, но `Bytes` для `default` возвращает `1` (следствие `1UL << 0`). У всех трёх: `Bytes => 1UL << Log2`, операторы `==`/`!=`/`<`/`>`/`<=`/`>=`, `IEquatable<T>`, `IComparable<T>`, `ToString()`. Сравнение — по сырому `Log2`, поэтому невалидное значение по умолчанию меньше любого валидного.

Фабрики:

| Тип | Доступные фабрики |
|---|---|
| `RamSize` | `B64..B512`, `KB1..KB512`, `MB1..MB512`, `GB1, GB2` |
| `PortSize` | `B64..B512`, `KB1, KB2, KB4, KB8, KB16, KB32, KB64, KB128, KB256, KB512` |
| `DevicePortSize` | `B8, B16, B32, B64, B128, B256` |

XML-покрытие: `PortSize` и `RamSize` — полностью (включая операторы). `DevicePortSize` — все члены и операторы с `///`; закрывающие теги `</summary>` исправлены.

### `DecodeInstructionResult` — `readonly record struct`

```csharp
public readonly record struct DecodeInstructionResult(
    OpCode OpCode,
    RegType Reg1,
    RegType Reg2,
    OpCodeSize DataSizeCode);
```

Позиционные параметры -> get-only свойства, сгенерированы `Equals`/`GetHashCode`/`==`/`ToString`/`Deconstruct`. XML — полный, включая `<remarks>` о том, что `Reg1`/`Reg2` имеют смысл только для опкодов, которые их используют.

### `InstructionDecoder` — `static class`

| Метод | Принимает | Возвращает | Что делает |
|---|---|---|---|
| `GetOpCode(uint)` | сырая инструкция | `OpCode` | `raw & 0xFF` |
| `GetReg1(uint)` | | `RegType` | `(raw >> 8) & 0x1F` |
| `GetReg2(uint)` | | `RegType` | `(raw >> 13) & 0x1F` |
| `GetDataSizeCode(uint)` | | `OpCodeSize` | `(raw >> 18) & 0x3` |
| `DecodeRawInstToEnums(uint)` | | `DecodeInstructionResult` | все поля за один проход |
| `HasNeed64IntData(OpCode)` | опкод | `bool` | есть ли после инструкции 64-битный immediate; бросает `ArgumentOutOfRangeException`, если `(uint)opCode > (uint)OpCodeExtension.LastValue` |

`HasNeed64IntData` — таблица `CreateNeedsUlongOperandTable()` (256-элементный `ImmutableArray<bool>`). **В этой версии отмечены:** `LDI`, `LOAD`, `STORE`, `CALL`, `JMP`, `JZ`, `JNZ`, `JG`, `JL`, **`LOAD_UNSAFE`, `STORE_UNSAFE`** (ранее отсутствовали). Проверка диапазона — по `OpCodeExtension.LastValue`, а не по `OpCode.MOD`, поэтому корректна и для недавно добавленных опкодов.

Все публичные методы `[MethodImpl(AggressiveInlining)]`.

### ProcessorSnapshot

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


### Extension-классы

Через C# 14 `extension`-блоки добавлены удобные члены:

| Класс | Extension на | Члены |
|---|---|---|
| `RegTypeExtension` | `RegType` | `Int()`, `ToUint()`, `AsText`; константы `LastValueInt`, `LastValue` (`rIP`) |
| `OpCodeSizeExtension` | `OpCodeSize` | `ToUint()`, `AsText`; константы `LastValueInt`, `LastValue` (`S64`) |
| `BiosStatusExtension` | `BiosStatus` | `AsText`; константы `LastValueInt`, `LastValue` (`UnsafeOpCodeForbidden`) |
| `IdStateExtension` | `int` | `ToIdState` (extension-свойство) |
| `OpCodeExtension` | `OpCode` | `LastValue` (последний опкод) |

`AsText`/`AsText`/`AsText` возвращают `nameof(...)` для известных значений или fallback (`"r?"`, `"???"`, `((uint)status).ToString()`).

### `AlignmentExtensions` — `static class`

Через C# 14 `extension`-блок добавляет к `IBinaryInteger<T>`:

| Метод | Семантика | Проверка `alignment` |
|---|---|---|
| `AlignUp(T)` | вверх до кратного | только `DEBUG`, требует степень двойки |
| `AlignDown(T)` | вниз до кратного | только `DEBUG`, требует степень двойки |
| `AlignUpArithmetic(T)` | вверх через `%` | всегда, требует `> 0` |

`AlignUp`/`AlignDown` в **Release** не проверяют `alignment` — невалидное значение даст тихий мусор. Для гарантированной проверки — `AlignUpArithmetic`. Вспомогательный `ThrowIfInvalidAlignment` помечен `[Conditional("DEBUG")]` и `[MethodImpl(NoInlining)]`.

## Примеры для API

**Размеры памяти и портов — только через фабрики:**

```csharp
using Nantuc.Common;

RamSize ram = RamSize.MB64;
PortSize ports = PortSize.KB64;
PortSize ports2 = PortSize.KB2;
PortSize ports32 = PortSize.KB32;
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

// record struct: сравнение и деконструкция доступны из коробки
if (d == InstructionDecoder.DecodeRawInstToEnums(raw)) { /* ... */ }
var (code, a, b, size) = d;
```

**Extension-члены:**

```csharp
RegType r = RegType.rSP;
Console.WriteLine(r.Int());       // 29
Console.WriteLine(r.AsText);   // "rSP"

BiosStatus s = BiosStatus.DivOnZero;
Console.WriteLine(s.AsText); // "DivOnZero"

OpCodeSize sz = OpCodeSize.S32;
Console.WriteLine(sz.AsText); // "S32"
Console.WriteLine(sz.ToUint());   // 2
```

**Классификация возврата `RegisterDevice`:**

```csharp
int raw = host.Emulator.RegisterDevice(spec, sector: 0);
IdState state = raw.ToIdState;

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

- Пользователь, который пишет программы на MiniC или NANASM, не должен импортировать `Nantuc.Common` напрямую — он работает через `VmApplication` / `Kernel`.
- Автор расширений (дизассемблер, отладчик, кастомные устройства, собственные backend'ы) будет использовать этот модуль постоянно — здесь лежат опкоды, регистры и единицы измерения.
- Автор компилятора (frontend MiniC -> NANASM) обязан согласовывать генерацию с `OpCode` и `HasNeed64IntData`.

**Что стоит делать:**
- Использовать фабрики `RamSize`/`PortSize`/`DevicePortSize` — не «магические» числа.
- Для декодирования — только `InstructionDecoder`, без ручных сдвигов у себя.
- Проверять `BiosStatus`/`ProcessorFault` вместо `bool`-флагов.
- Использовать `IdState`/`ToIdState` вместо `raw switch { -1 => ..., -2 => ... }`. **Помнить:** `default(IdState) == Valid` (не `Unknown`).
- Читать `<remarks>` на `OpCode` и `RegType`: флаги `rFL` обновляют только `CMP` и `TEST`, `HALT` ≠ `END`, нет аппаратно-занулённого регистра.

**Что не стоит делать:**
- Добавлять интерфейсы или default-классы в этот модуль — для контрактов есть `Contracts`.
- Вводить сюда логику процессора или шин — это `Kernel`.
- Менять значения `OpCode`/`OpCodeSize`/`RegType`/`IdState`/`BiosStatus` без версии байткода: числа — часть контракта.
- Полагаться на `default(RamSize)` как на «0» — это «невалидно», и `Bytes` вернёт `1`.
- Полагаться на `default(IdState) == Unknown` — в текущей версии `default` = `Valid`.

## Особенная информация

### ISA байткода (формат инструкции)

32-битная инструкция (`uint`), little-endian на уровне байт:

```
 бит:  31…20  |  19…18    |  17…13  |  12…8   |  7…0
       spare   | OpCodeSize|   Reg2  |   Reg1  | OpCode
       (12 бит)|  (2 бита) |  (5 бит)|  (5 бит)| (8 бит)
```

- `OpCode` — 8 бит -> до 256 опкодов, реально используется ~60.
- `Reg1`, `Reg2` — по 5 бит -> 32 регистра, ровно как в `RegType`.
- `OpCodeSize` — 2 бита -> 4 варианта (`S8`/`S16`/`S32`/`S64`).
- Биты 20–31 не используются — задел на будущее.

Полная длина инструкции:
- 4 байта — register-register форма.
- 12 байт — register-immediate форма: 4 байта инструкции + 8 байт immediate (`ulong`). Наличие immediate — только через `InstructionDecoder.HasNeed64IntData(opCode)`. Immediate выравнивается по границе 8 байт.

### Регистровый файл

Зафиксировано в `<remarks>` `RegType`:
- 32 регистра по 64 бита в `ArrayRegisters` (`[InlineArray(32)]` из `ulong`).
- `r0..r27` — регистры общего назначения. **Нет** аппаратно-занулённого регистра; для обнуления — `XOR rN, rN`.
- `rFL` — флаги: бит 0 — Zero, бит 1 — Negative. **Обновляется только `CMP` и `TEST`**; арифметические и логические инструкции флаги не меняют. Начальное значение — `0`.
- `rSP` — stack pointer. Инициализируется в `Processor.LaunchProgramm` вершиной ОЗУ, выровненной по 8 байт. `rSP -= 8` на `PUSH`/`CALL`, `rSP += 8` на `POP`/`RET`.
- `rHP` — heap pointer. Инициализируется через `Processor.InitRegHP`; используется для обнаружения коллизии «стек ↔ куча».
- `rIP` — instruction pointer. В начале `Processor.Step` читается 32-битное слово по `rIP`, после чего `rIP += 4`. Immediate-операнды читаются отдельно с выравниванием по 8 байт.

### WASM / кроссплатформенность

| Аспект | Статус | Комментарий |
|---|---|---|
| Сборка в WASM | - | Нет `unsafe`, `DllImport`, `LibraryImport`. Все типы — managed. Собирается под `browser-wasm` без правок csproj. |
| `NativeMemory` | - | Не используется. `Bytes` — managed-сдвиг `1UL << Log2`. |
| `stackalloc` / `InlineArray` | - | `stackalloc` не используется. `[InlineArray(32)]` — compile-time атрибут, AOT-friendly, WASM-safe. |
| `Interlocked` / `Volatile` | - | Не используются. Shared-состояние — единственное `static readonly ImmutableArray<bool> _has` в `InstructionDecoder.cs`, инициализируется один раз. |
| `IFileService` | n/a | В этой партии файлов интерфейса нет. |
| `Regex` / trimming | - | Regex не используется; нет рефлексии, `Type.GetType`, `Activator.CreateInstance`, `dynamic`. Trimming-совместимо. |
| AOT | - | Нет `Expression.Compile`, `MakeGenericMethod`/`MakeGenericType`. Generic math (`IBinaryInteger<T>`) моноформируется под каждый `T`. C# 14 `extension`-блоки — compile-time. |

**Итог:** код полностью совместим с WASM (interpreter и AOT) и trimming.

### `HasNeed64IntData` как контракт

Таблица `CreateNeedsUlongOperandTable()` — единственный источник истины о том, у каких опкодов есть 8-байтный immediate. **Покрывает:** `LDI`, `LOAD`, `STORE`, `CALL`, `JMP`, `JZ`, `JNZ`, `JG`, `JL`, **`LOAD_UNSAFE`, `STORE_UNSAFE`**. Проверка диапазона — `(uint)opCode > (uint)OpCodeExtension.LastValue` — корректна и расширяется автоматически при добавлении новых опкодов до `LastValue`.

`LOAD_IND`/`STORE_IND`/`CALL_IND`/`JMP_IND` — indirect через регистр, immediate не нужен — отсутствуют в таблице корректно. `LOAD_IND_UNSAFE`/`STORE_IND_UNSAFE` — тоже indirect.

### Ограничения платформ

- Целевой framework: `net10.0`. Используются C# 14 `extension`-блоки — старые тулчейны не соберут.
- `unsafe`-кода в этом модуле нет — безопасен для WASM/AOT.
- `[MethodImpl(AggressiveInlining)]` — оптимизация под JIT. В AOT/WASM инлайнер ведёт себя иначе; профилируйте на целевой платформе.
- `[Conditional("DEBUG")]` в `AlignmentExtensions`: в **Release** проверка `alignment` на степень двойки не выполняется.
- `Nullable=enable`, `ImplicitUsings=enable`.

### Что этот модуль не делает

- Не содержит интерфейсов — они в `Contracts`.
- Не содержит логики процессора, шин, устройств — это `Kernel`.
- Не знает про компилятор MiniC и ассемблер NANASM — только предоставляет им `OpCode`/`RegType`/`OpCodeSize`.
- Не сериализует байткод — формат `.bin`/`.nanas` определяется в `Kernel`/`Extensions`.

Правило «куда положить тип»: примитив без поведения -> `Common`; интерфейс/контракт -> `Contracts`; всё, что исполняет или хранит состояние -> `Kernel`.