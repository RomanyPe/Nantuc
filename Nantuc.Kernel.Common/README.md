# Nantuc.Kernel.Common — README

> Общие типы, константы ISA и разделяемые примитивы платформы Nantuc.  
> Фундамент, на который опираются ядро, компилятор и расширения.  
> `[← Корень](../README.md) · [Contracts](../Nantuc.Kernel.Contracts/README.md) · [Kernel](../Nantuc.Kernel/README.md)`

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
- Диагностика: `BiosStatus`, `ProcessorFault` (теперь в отдельном файле `ProcessorFault.cs`).
- Классификация возвратов: `IdState`.
- Утилиты: `AlignmentExtensions` (выравнивание для generic-чисел).

Проект — листовой по зависимостям: `net10.0`, без `ProjectReference`. Любое изменение публичного типа здесь — потенциальный breaking change для всей платформы.

> **XML-документация почти полная. Два известных дефекта и одна breaking-правка относительно предыдущей версии.**  
> - `DevicePortSize.cs` — **сломанные закрывающие теги** `</summary` (без `>`) у фабрик `B64`, `B128`, `B256` — XML не парсится.  
> - `InstructionDecoder.HasNeed64IntData` — `<remarks>` описывает **старое поведение** (уязвимость к знаковому `OpCode`), хотя код уже защищён `(uint)`-приведением.  
> - `IdState` — **breaking change**: раньше `Valid` был первым членом (= 0), теперь `Unknown = 0`, `Valid = 1`. Это сделано намеренно (`<remarks>` типа), но требует обновления потребителей, которые полагались на `IdState.Valid == default`.  
> - `DecodeInstructionResult` — **breaking change**: был `readonly struct` с публичными полями, стал `readonly record struct` с позиционными параметрами (поля → get-only свойства; сгенерированы `Equals`/`GetHashCode`/`==`).  
> Подробности — в разделе [Что исправить](#что-исправить).

## Дерево локального проекта

Все типы живут в namespace `Nantuc.Kernel.Common`, структура плоская:

```
Nantuc.Kernel.Common/
├── Nantuc.Kernel.Common.csproj    # net10.0, без ProjectReference
├── AlignmentExtensions.cs         # static class AlignmentExtensions
├── BiosStatus.cs                  # enum BiosStatus (ProcessorFault вынесен)
├── DecodeInstructionResult.cs     # readonly record struct (было readonly struct)
├── DevicePortSize.cs              # readonly struct
├── IdState.cs                     # enum IdState (теперь с явными значениями)
├── InstructionDecoder.cs          # static class InstructionDecoder
├── OpCode.cs                      # enum OpCode : byte
├── OpCodeSize.cs                  # enum OpCodeSize
├── PortSize.cs                    # readonly struct PortSize
├── ProcessorFault.cs              # readonly record struct (отдельный файл!)
├── RamSize.cs                     # readonly struct RamSize
└── RegType.cs                     # enum RegType : byte (с полным XML)
```

Замечания по организации:
- `ProcessorFault` **вынесен из `BiosStatus.cs`** в отдельный `ProcessorFault.cs` — навигация улучшена.
- `AlignmentExtensions.cs` вынесен из `InstructionDecoder.cs` — семантически разные классы больше не делят файл.
- `NameDeviceToken.cs` и `LogLevel.cs` в эту партию не входят. Если типы ещё существуют — добавьте в следующую; если удалены — обновите дерево.
- `PortSize` и `RamSize` — теперь симметричны: оба имеют `KB2` и `KB32`, `<remarks>` у `PortSize` исправлен.

## API

### Общая картина

В модуле нет интерфейсов и default-реализаций. Публичная поверхность:

| Тип | Вид | Назначение |
|---|---|---|
| `OpCode` | `enum : byte` | Опкоды VM (~60 значений), полная XML-документация с семантикой каждой инструкции |
| `OpCodeSize` | `enum` | Размер операнда: `S8`/`S16`/`S32`/`S64` (полный XML, дефект закрывающих тегов исправлен) |
| `RegType` | `enum : byte` | 32 регистра: `r0..r27`, `rFL`, `rSP`, `rHP`, `rIP` (полный XML) |
| `BiosStatus` | `enum : byte` | Статус завершения/ошибки (все члены с `///`) |
| `IdState` | `enum : int` | Классификация `-1..-4` от `ManagerDevices.AddDevice` (полный XML, **явные значения**, `Unknown = 0`) |
| `ProcessorFault` | `readonly record struct` | `(BiosStatus, ulong FaultData, ulong IP, OpCode)` (отдельный файл, полный XML) |
| `RamSize` | `readonly struct` | Размер ОЗУ, лог2, 64 B … 2 GiB, полный набор фабрик |
| `PortSize` | `readonly struct` | Размер порт-пространства, лог2, 64 B … 512 KiB, **полный набор фабрик (включая `KB2`/`KB32`)** |
| `DevicePortSize` | `readonly struct` | Размер блока портов устройства, лог2, 8 B … 256 B |
| `DecodeInstructionResult` | `readonly record struct` | Результат декодирования (поля → get-only свойства) |
| `InstructionDecoder` | `static class` | Разбор 32-битной инструкции + таблица «требуется ли immediate» |
| `AlignmentExtensions` | `static class` | `AlignUp`/`AlignDown`/`AlignUpArithmetic` для `IBinaryInteger<T>` |

### Перечисления

**`OpCode : byte`** — около 60 значений, каждый с `///` и подробным `<remarks>`. Группы:

- Системные: `NOP`, `END` (`END` завершает программу, сбрасывает `IsRunning`; `HALT` — приостановка).
- Память: `MOV`, `LOAD`/`STORE` (проверяемые через `MemoryBus`), `LDI` (64-битный immediate из потока), `LOAD_IND`/`STORE_IND` (эквивалентны `LOAD`/`STORE` с адресом из регистра).
- Арифметика: `ADD`, `SUB`, `MULT_INT` (wrap-around), `SHR`/`SHL` (величина сдвига — младший байт `R2`), `INC`, `DEC`, `DIV` (`DivOnZero`), `MOD` (`DivOnZero`).
- Логика: `AND`, `OR`, `XOR` (для обнуления `XOR R1, R1`), `NOT`.
- Управление потоком: `JMP`, `JZ`, `JNZ`, `JG` (сброшены Zero и Negative), `JL` (установлен Negative), `JMP_IND` (косвенный переход по адресу из регистра, с проверкой границ).
- Стек: `PUSH` (`rSP -= 8`, коллизия с кучей → `SegmentationFault`), `POP` (`rSP += 8`), `CALL` (сохраняет адрес возврата), `RET` (пустой стек → `StackUnderflow`), `CALL_IND` (косвенный вызов по адресу из регистра, с проверкой границ).
- I/O: `IN`, `OUT` (младший байт регистра-источника).
- Сон: `HALT` (`IsSleeping = true`), `WAKE` (внешняя команда), `WAKE_INT` (через порт, нет порта → `NullDeviceOutput`).
- Unsafe-варианты: `LOAD_UNSAFE`/`STORE_UNSAFE` (быстрый путь по абсолютному адресу без проверок), `STORE_IND_UNSAFE`/`LOAD_IND_UNSAFE` (косвенный по регистру, без проверок).
- Сравнение: `CMP` (`R1 - R2` → флаги `rFL`), `TEST` (`R1 & R2` → флаги `rFL`), `MOD`.

**`OpCodeSize`** — `S8 = 0b00` (8 бит = 1 байт), `S16 = 0b01` (2 байта), `S32 = 0b10` (4 байта), `S64 = 0b11` (8 байт). **Полный XML**, включая `<remarks>` на enum о битовой кодировке и о том, что значения менять нельзя без декодера. Дефект закрывающих тегов (`</summary` без `>`) исправлен.

**`RegType : byte`** — 32 значения: `r0..r27` (регистры общего назначения, **все с `///`**), `rFL` (флаги Zero/Negative), `rSP` (stack pointer), `rHP` (heap pointer), `rIP` (instruction pointer). `<remarks>` на enum фиксирует: 32 регистра по 64 бита в `ArrayRegisters` (`[InlineArray(32)]` из `ulong`), нет аппаратно-занулённого регистра (обнуление через `XOR rN, rN`), порядок 0..31 — часть контракта. Ровно 32 = 5 бит.

**`BiosStatus : byte`** — все 11 членов с `///`: `Success`, `SegmentationFault`, `AlignmentFault`, `EndProgramm`, `NotImplementedOpCode`, `NullDeviceInput`, `NullDeviceOutput`, `InfinityLoop`, `DivOnZero`, `NotSupportedOpCode`, `StackUnderflow`. `<remarks>` на enum явно предупреждает: `EndProgramm` и `InfinityLoop` — **не ошибки по смыслу**, но проходят через путь `ProcessorFault`/`IProcessorFaultPolicy`; политика обязана их различать.

**`IdState`** — **breaking change** относительно предыдущей версии:

| Значение | Член | Причина (по `<remarks>`) |
|---|---|---|
| `0` | `Unknown` | Неинициализированный/неопределённый статус (default) |
| `1` | `Valid` | Id валиден и принят системой |
| `-1` | `NotAssigned` | Id не присвоен (регистрация не выполнялась) |
| `-2` | `OutOfRange` | Id вне допустимого диапазона |
| `-3` | `NoFreeSlot` | Нет свободного слота |
| `-4` | `Rejected` | Id отклонён системой |

`<remarks>` на enum прямо указывает: «Значение по умолчанию (ноль) намеренно отдано `Unknown`, чтобы неинициализированный статус не воспринимался как успешный». Потребители, которые проверяли `IdState.Valid == default` (старое поведение `Valid = 0`), **сломаются** — теперь `default(IdState) == Unknown`. Числовые значения зафиксированы явно и объявлены частью контракта.

### `ProcessorFault` — `readonly record struct`

```csharp
public readonly record struct ProcessorFault(
    BiosStatus Status,
    ulong FaultData,
    ulong InstructionPointer,
    OpCode OpCode);
```

Вынесен в отдельный файл `ProcessorFault.cs`. Полный XML: `<summary>` + `<remarks>` + `<param>` на каждый параметр. Ключевые факты из `<remarks>`:
- Формируется процессором при статусе ≠ `Success`, передаётся в `IProcessorFaultPolicy.ShouldContinue`.
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
| `RamSize` | `B64..B512`, `KB1, KB2, KB4, KB8, KB16, KB32, KB64, KB128, KB256, KB512`, `MB1..MB512`, `GB1, GB2` |
| `PortSize` | `B64..B512`, `KB1, **KB2**, KB4, KB8, KB16, **KB32**, KB64, KB128, KB256, KB512` — **`KB2`/`KB32` добавлены, `<remarks>` исправлен** |
| `DevicePortSize` | `B8, B16, B32, B64, B128, B256` |

Разница в XML-покрытии:
- `PortSize`, `RamSize` — полностью (включая операторы).
- `DevicePortSize` — структура и фабрики с `///`, но `Equals`/`GetHashCode`/`CompareTo`/`ToString` и все операторы — без. **Плюс сломанные `</summary` у `B64`/`B128`/`B256`** (дефект не исправлен).

### `DecodeInstructionResult` — `readonly record struct`

```csharp
public readonly record struct DecodeInstructionResult(
    OpCode OpCode,
    RegType Reg1,
    RegType Reg2,
    OpCodeSize DataSizeCode);
```

**Breaking change:** был `readonly struct` с публичными полями, стал `readonly record struct` с позиционными параметрами. Поля стали get-only свойствами, сгенерированы `Equals`/`GetHashCode`/`==`/`ToString`/`Deconstruct`. XML — полный, включая `<remarks>` о том, что `Reg1`/`Reg2` имеют смысл только для опкодов, которые их используют.

### `InstructionDecoder` — `static class`

| Метод | Принимает | Возвращает | Что делает |
|---|---|---|---|
| `GetOpCode(uint)` | сырая инструкция | `OpCode` | `raw & 0xFF` |
| `GetReg1(uint)` | | `RegType` | `(raw >> 8) & 0x1F` |
| `GetReg2(uint)` | | `RegType` | `(raw >> 13) & 0x1F` |
| `GetDataSizeCode(uint)` | | `OpCodeSize` | `(raw >> 18) & 0x3` |
| `DecodeRawInstToEnums(uint)` | | `DecodeInstructionResult` | все поля за один проход |
| `HasNeed64IntData(OpCode)` | опкод | `bool` | есть ли после инструкции 64-битный immediate; бросает `ArgumentOutOfRangeException`, если `(uint)opCode > (uint)OpCode.MOD` |

Плюс extension-метод `Int()` на `RegType`.

`HasNeed64IntData` — таблица `CreateNeedsUlongOperandTable()`. В ней отмечены: `LDI`, `LOAD`, `STORE`, `CALL`, `JMP`, `JZ`, `JNZ`, `JG`, `JL`. Проверка диапазона — `(uint)opCode > (uint)OpCode.MOD` — устойчива к смене базового типа `OpCode` на знаковый. **Однако `<remarks>` на методе всё ещё описывает старую уязвимость** — см. [Что исправить](#что-исправить), п. 1.

`CreateNeedsUlongOperandTable` — `private`, 256-элементный `ImmutableArray<bool>`. Не отмечены `LOAD_UNSAFE`/`STORE_UNSAFE` (по синтаксису из `OpCode` — `LOAD_UNSAFE R1, [addr]` и `STORE_UNSAFE [addr], R2` — принимают абсолютный адрес, т.е. immediate; см. [Что исправить](#что-исправить), п. 4).

Все публичные методы `[MethodImpl(AggressiveInlining)]`.

### `AlignmentExtensions` — `static class`

Через C# 14 `extension`-блок добавляет к `IBinaryInteger<T>`:

| Метод | Семантика | Проверка `alignment` |
|---|---|---|
| `AlignUp(T)` | вверх до кратного | только `DEBUG`, требует степень двойки |
| `AlignDown(T)` | вниз до кратного | только `DEBUG`, требует степень двойки |
| `AlignUpArithmetic(T)` | вверх через `%` | всегда, требует `> 0` (не обязательно степень двойки) |

`AlignUp`/`AlignDown` в **Release** не проверяют `alignment` — невалидное значение даст тихий мусор. Для гарантированной проверки — `AlignUpArithmetic`. XML на `AlignUp`/`AlignDown` есть, на `AlignUpArithmetic` — **нет**. Плюс опечатка в `<summary>` класса: «ресширение» → «расширение» (см. [Что исправить](#что-исправить), п. 7).

## Примеры для API

**Размеры памяти и портов — только через фабрики:**

```csharp
using Nantuc.Kernel.Common;

RamSize ram = RamSize.MB64;
PortSize ports = PortSize.KB64;
PortSize ports2 = PortSize.KB2;      // симметрично RamSize
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
- Автор компилятора (frontend MiniC → NANASM) обязан согласовывать генерацию с `OpCode` и `HasNeed64IntData`.

**Что стоит делать:**
- Использовать фабрики `RamSize`/`PortSize`/`DevicePortSize` — не «магические» числа. Теперь набор фабрик у `PortSize` и `RamSize` совпадает.
- Для декодирования — только `InstructionDecoder`, без ручных сдвигов у себя.
- Проверять `BiosStatus`/`ProcessorFault` вместо `bool`-флагов.
- Использовать `IdState` вместо `raw switch { -1 => ..., -2 => ... }` — единая классификация. **Помнить:** `default(IdState) == Unknown` (не `Valid`).
- Читать `<remarks>` на `OpCode` и `RegType`: флаги `rFL` обновляют только `CMP` и `TEST`, `HALT` ≠ `END`, нет аппаратно-занулённого регистра.

**Что не стоит делать:**
- Добавлять интерфейсы или default-классы в этот модуль — для контрактов есть `Kernel.Contracts`.
- Вводить сюда логику процессора или шин — это `Kernel`.
- Менять значения `OpCode`/`OpCodeSize`/`RegType`/`IdState` без версии байткода: числа — часть контракта (явно зафиксировано в `<remarks>`).
- Полагаться на `default(RamSize)` как на «0» — это «невалидно», и `Bytes` вернёт `1`.
- Полагаться на `default(IdState) == Valid` — с новой версией это `Unknown`.

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
- 12 байт — register-immediate форма: 4 байта инструкции + 8 байт immediate (`ulong`). Наличие immediate определяется только через `InstructionDecoder.HasNeed64IntData(opCode)`. Immediate выравнивается по границе 8 байт (зафиксировано в `<remarks>` `OpCode`).

### Регистровый файл

Зафиксировано в `<remarks>` `RegType`:
- 32 регистра по 64 бита в `ArrayRegisters` (`[InlineArray(32)]` из `ulong`).
- `r0..r27` — регистры общего назначения. **Нет** аппаратно-занулённого регистра (в отличие от MIPS/RISC-V); для обнуления — `XOR rN, rN`.
- `rFL` — флаги: бит 0 — Zero, бит 1 — Negative. **Обновляется только `CMP` и `TEST`**; арифметические и логические инструкции флаги не меняют. Начальное значение — `0`.
- `rSP` — stack pointer. Инициализируется в `Processor.LaunchProgramm` вершиной ОЗУ, выровненной по 8 байт. `rSP -= 8` на `PUSH`/`CALL`, `rSP += 8` на `POP`/`RET`.
- `rHP` — heap pointer. Инициализируется через `Processor.InitRegHP`; используется для обнаружения коллизии «стек ↔ куча».
- `rIP` — instruction pointer. В начале `Processor.Step` читается 32-битное слово по `rIP`, после чего `rIP += 4`. Immediate-операнды читаются отдельно с выравниванием по 8 байт. Изменяется инструкциями перехода/вызова/возврата.

### WASM / кроссплатформенность

| Аспект | Статус | Комментарий |
|---|---|---|
| Сборка в WASM | ✅ | Нет `unsafe`, `DllImport`, `LibraryImport`. Все типы — managed. Собирается под `browser-wasm` без правок csproj. |
| `NativeMemory` | ✅ | Не используется. `Bytes` — managed-сдвиг `1UL << Log2`. |
| `stackalloc` / `InlineArray` | ✅ | `stackalloc` не используется. `[InlineArray(32)]` упоминается в `<remarks>` `RegType` — это compile-time атрибут, AOT-friendly, WASM-safe. |
| `Interlocked` / `Volatile` | ✅ | Не используются. Shared-состояние — единственное `static readonly ImmutableArray<bool> _has` в `InstructionDecoder.cs`, инициализируется один раз (CLR гарантирует единственность static-ctor). |
| `IFileService` | n/a | В этой партии файлов интерфейса нет. |
| `Regex` / trimming | ✅ | Regex не используется; нет рефлексии, `Type.GetType`, `Activator.CreateInstance`, `dynamic`. Trimming-совместимо. |
| AOT | ✅ | Нет `Expression.Compile`, `MakeGenericMethod`/`MakeGenericType`. Generic math (`IBinaryInteger<T>`) моноформируется под каждый `T`. C# 14 `extension`-блоки — compile-time. |

**Итог:** код полностью совместим с WASM (interpreter и AOT) и trimming.

### `HasNeed64IntData` как контракт

Таблица `CreateNeedsUlongOperandTable()` — единственный источник истины о том, у каких опкодов есть 8-байтный immediate. Сейчас покрывает: `LDI`, `LOAD`, `STORE`, `CALL`, `JMP`, `JZ`, `JNZ`, `JG`, `JL`. Проверка диапазона — `(uint)opCode > (uint)OpCode.MOD` — корректна и для знакового базового типа. **Однако `<remarks>` на методе описывает старое поведение** (без `(uint)`-приведения) — см. [Что исправить](#что-исправить), п. 1.

По новой XML-документации `OpCode`: `LOAD_IND`/`STORE_IND`/`CALL_IND`/`JMP_IND` — indirect через регистр (`R1 = [R2]`, `[R2] = R1`, `CALL [R]`, `JMP [R]`), immediate после инструкции им **не нужен** — отсутствие в таблице корректно. `LOAD_IND_UNSAFE`/`STORE_IND_UNSAFE` — тоже indirect, отсутствие корректно. Но `LOAD_UNSAFE R1, [addr]` и `STORE_UNSAFE [addr], R2` по синтаксису принимают **абсолютный адрес** — это immediate, поэтому они **должны быть** в таблице; сейчас их нет (см. [Что исправить](#что-исправить), п. 4).

### Ограничения платформ

- Целевой framework: `net10.0`. Используются C# 14 `extension`-блоки в `AlignmentExtensions` и `InstructionDecoder` — старые тулчейны не соберут.
- `unsafe`-кода в этом модуле нет — безопасен для WASM/AOT и окружений с запретом `unsafe`.
- `[MethodImpl(AggressiveInlining)]` — оптимизация под JIT. В AOT/WASM инлайнер ведёт себя иначе; профилируйте на целевой платформе.
- `[Conditional("DEBUG")]` в `AlignmentExtensions`: в **Release** проверка `alignment` на степень двойки не выполняется. Невалидный `alignment` даст тихий мусор. Для гарантированной проверки — `AlignUpArithmetic`.
- `Nullable=enable`, `ImplicitUsings=enable`.

### Что этот модуль не делает

- Не содержит интерфейсов — они в `Kernel.Contracts`.
- Не содержит логики процессора, шин, устройств — это `Kernel`.
- Не знает про компилятор MiniC и ассемблер NANASM — только предоставляет им `OpCode`/`RegType`/`OpCodeSize`.
- Не сериализует байткод — формат `.bin`/`.nanas` определяется в `Kernel`/`Extensions`.
- Не содержит `ToIdState`/`TryGetIdState` — только enum `IdState` (см. [Что исправить](#что-исправить), п. 6).

Правило «куда положить тип»: примитив без поведения → `Kernel.Common`; интерфейс/контракт → `Kernel.Contracts`; всё, что исполняет или хранит состояние → `Kernel`.

## Что исправить

По убыванию важности. **[Дефект]** — некорректный XML или несоответствие кода и документации; **[API]** — кандидаты на изменение публичной поверхности; **[XML]** — оставшиеся пробелы.

### API

3. **[API] `DecodeInstructionResult` — breaking change, но в правильную сторону.**  
   Файл: `DecodeInstructionResult.cs`. Был `readonly struct` с публичными полями; стал `readonly record struct` с позиционными параметрами. Поля → get-only свойства, сгенерированы `Equals`/`GetHashCode`/`==`/`Deconstruct`. Потребители, которые использовали `ref`/`out` на полях или `Marshal.SizeOf`, могут сломаться.  
   Рекомендация: обновить всех потребителей (это внутренний API, breaking change в пределах платформы — допустимо); явно указать в релиз-нотах.

4. **[API] `InstructionDecoder.HasNeed64IntData` — неполная таблица для `LOAD_UNSAFE`/`STORE_UNSAFE`.**  
   Файл: `InstructionDecoder.cs`, `CreateNeedsUlongOperandTable()`. `LOAD_UNSAFE R1, [addr]` и `STORE_UNSAFE [addr], R2` по синтаксису в `OpCode` принимают абсолютный адрес — это immediate, но в таблице их нет. `LOAD_IND_UNSAFE`/`STORE_IND_UNSAFE` — indirect через регистр, отсутствие корректно.  
   Фикс: добавить `table[(int)OpCode.LOAD_UNSAFE] = true;` и `table[(int)OpCode.STORE_UNSAFE] = true;`. Плюс юнит-тест «все члены `OpCode`, задуманные с immediate, отмечены в таблице».

5. **[API] `IdState` — breaking change (Valid: 0 → 1).**  
   Файл: `IdState.cs`. Раньше `Valid` был первым членом (= 0), теперь `Unknown = 0`, `Valid = 1`. Это **намеренно** (см. `<remarks>`: «неинициализированный статус не должен восприниматься как успешный»), но ломает код, который проверял `IdState.Valid == default` или использовал `IdState` в `switch` без явного маппинга.  
   Рекомендация: обновить потребителей, добавить `ToIdState`/`TryGetIdState` рядом с enum (см. п. 6), обновить релиз-ноты.

6. **[API/XML] `IdState` — нет `ToIdState`/`TryGetIdState`.**  
   Файл: `IdState.cs`. Enum теперь полностью задокументирован (все значения явные, `<remarks>` с таблицей), но `int → IdState` расширения нет.  
   Фикс: добавить `ToIdState`/`TryGetIdState` в `Nantuc.Kernel.Common` рядом с enum — это тот же слой, и его видит и `Kernel`, и потребители.

### XML (оставшиеся пробелы)

7. **[XML] `AlignmentExtensions.AlignUpArithmetic` — без `///`; опечатка в `<summary>` класса.**  
   Файл: `AlignmentExtensions.cs`. `AlignUp`/`AlignDown` имеют `///`, `AlignUpArithmetic` — нет. `<summary>` класса: «ресширение» (опечатка, должно быть «расширение»).  
   Фикс: добавить `///` на `AlignUpArithmetic` с пояснением «works for non-power-of-two alignments, always validates `> 0`»; исправить опечатку.

8. **[XML] `DevicePortSize` — часть методов и операторы без `///`.**  
   Файл: `DevicePortSize.cs`. Структура и фабрики — с `///`; `Equals`/`GetHashCode`/`CompareTo`/`ToString` и все операторы — без (в отличие от `PortSize`/`RamSize`).  
   Фикс: либо добавить, либо отметить в `<remarks>`, что XML на операторах необязательна (тогда и у `PortSize`/`RamSize` её стоит убрать для консистентности).

### Семантика `Bytes` (оставшиеся риски)

9. **[API] `RamSize`/`PortSize`/`DevicePortSize.Bytes` — для `default` возвращает `1`, а не `0`.**  
   Задокументировано в `<remarks>` у всех трёх, но легко забыть в потребительском коде.  
   Фикс: либо возвращать `0` при `!IsValid`, либо оставить и явно указывать в README (сделано выше).

10. **[API] `RamSize`/`PortSize`/`DevicePortSize.Bytes` — при `Log2 >= 64` маскируется по модулю 64.**  
    Задокументировано в `<remarks>` `PortSize.Bytes` и `RamSize.Bytes`. Сейчас `MaxLog2` = 19/31/8, не воспроизводится, но при повышении `MaxLog2` станет источником багов.  
    Фикс: либо проверять `Log2 < 64` в `Bytes`, либо запретить `MaxLog2 >= 64` на уровне типа.

### Что уже исправлено (для истории)

- `OpCodeSize.cs` — закрывающие теги исправлены, добавлен `<remarks>` о битовой кодировке.
- `PortSize.cs` — добавлены `KB2`/`KB32`, `<remarks>` про «намеренно отсутствуют» удалён, набор фабрик совпадает с `RamSize`.
- `ProcessorFault` — вынесен в отдельный файл, добавлен полный XML с `<remarks>` о соглашении `NOP`-как-сентинел.
- `RegType` — все `r0..r27` получили `///`, добавлен `<remarks>` про `[InlineArray(32)]`, отсутствие zero-регистра, порядок 0..31.
- `IdState` — все члены с `///`, явные числовые значения, `Unknown = 0` (default-safe).
- `DecodeInstructionResult` — переведён в `readonly record struct` (генерирует `Equals`/`GetHashCode`/`==`), полный XML.
- `BiosStatus` — все 11 членов с `///`, `<remarks>` про «не-ошибки по смыслу».
- `OpCodeSize` — полностью документирован.
- `OpCode` — `<remarks>` про 8-байтовое выравнивание immediate, про обновление флагов только `CMP`/`TEST`.