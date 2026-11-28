# Спецификация байт-кода виртуальной машины Nantuc

> Версия формата: 1
> Целевая платформа: `Nantuc.Kernel.Common` / `Nantuc.Compiler.ASM`
> Статус: реализовано в эталонном интерпретаторе (`Processor.Step`, `InstructionDecoder`) и в кодогенераторе MiniC/NANASM.

---

## 1. Терминология и соглашения

- **Байт-код** — последовательность 32-битных слов инструкций и 64-битных непосредственных операндов (immediate), размещённая в ОЗУ.
- **Слово инструкции** — 32-битное `uint`, читается из памяти как **little-endian** (`MemoryBus.ReadInt32LE*`).
- **Immediate** — 64-битное значение, читается из памяти как **little-endian unaligned** (`ReadInt64LEUnSafe`).
- **VM** — виртуальный процессор, реализованный в `Processor`.
- **Загрузчик** — `Device.LoadProgram` (копирует байты программы в ОЗУ и инициализирует `rHP`).

Ключевое требование: **базовый адрес загрузки программы должен быть кратен 8** для корректного чтения immediate.

---

## 2. Модель машины

### 2.1. Регистры

32 регистра по 64 бита (`[InlineArray(32)]` в `Processor`). Индексация — по `RegType` (0..31).

| Индекс | Имя   | Назначение |
|-------:|-------|------------|
| 0–27   | `r0..r27` | Регистры общего назначения |
| 28     | `rFL` | Flags: бит 0 = Zero, бит 1 = Negative, бит 2 = Carry |
| 29     | `rSP` | Stack Pointer — указатель стека (растёт вниз) |
| 30     | `rHP` | Heap Pointer — указатель кучи |
| 31     | `rIP` | Instruction Pointer — адрес следующей инструкции |

**Флаги `rFL` обновляются только `CMP` и `TEST`.** Арифметические и логические инструкции флаги не изменяют.

### 2.2. Состояние процессора

- `IsRunning` — исполняется ли программа.
- `IsSleeping` — процессор в режиме `HALT` (ожидает пробуждения).

### 2.3. Начальная инициализация (`LaunchProgramm`)

| Поле  | Значение |
|-------|----------|
| `rIP` | аргумент `startAddress` (адрес первой инструкции) |
| `rSP` | `AlignDown(RAM.Length, 8)` — вершина ОЗУ |
| `rFL` | `0` |
| `IsRunning` | `true`, `IsSleeping = false` |
| `rHP` | устанавливается `Device.LoadProgram` / `InitRegHP` равным `AlignUp(loadAddress + programLength, 8)` |

### 2.4. Формат адресов

- ОЗУ адресуется по байтам, длина — `RAM.Length` (степень двойки, ≥ 64 байт).
- Порты — отдельное байтовое пространство, разбитое на сектора по `PortsOnDevice` портов.

---

## 3. Формат инструкции

### 3.1. 32-битное слово (little-endian)

```
биты:  31 … 20  |  19 … 18   |  17 … 13   |  12 … 8   |  7 … 0
       spare    | DataSize   |   Reg2     |   Reg1    | OpCode
       (12 бит) | (2 бита)   |  (5 бит)   |  (5 бит)  | (8 бит)
```

- **OpCode** (8 бит) — код операции (`OpCode`).
- **Reg1** (5 бит) — первый регистр-операнд, обычно приёмник.
- **Reg2** (5 бит) — второй регистр-операнд, обычно источник.
- **DataSize** (2 бита) — размер данных (`OpCodeSize`): для группы LOAD/STORE **и для CASTZ/CASTS**.
- **Биты 20–31** — зарезервированы, должны быть нулевыми.

Декодер (эталон): `InstructionDecoder.GetOpCode/GetReg1/GetReg2/GetDataSizeCode`.

### 3.2. Общая длина инструкции

| Форма | Длина | Когда |
|-------|-------|-------|
| Регистр–регистр / без операндов / один регистр + size | **4 байта** | все, кроме перечисленных ниже |
| Инструкция + immediate | **12 байт** (4 + 4 паддинга + 8) | если `InstructionDecoder.HasNeed64IntData(opCode) == true` |

**Immediate** читается по адресу `AlignUp(rIP_after_instr, 8)`. Паддинг между инструкцией и immediate игнорируется (может быть любым, обычно 0). После чтения `rIP = immediate_addr + 8`.

**CASTZ/CASTS — 4-байтные инструкции** без immediate (используют DataSize как параметр).

### 3.3. Операнды-регистры по опкодам

| Операнд | Что кодирует |
|---------|--------------|
| `Reg1`  | приёмник (dst) / левый операнд / единственный операнд (в т.ч. для CASTZ/CASTS) |
| `Reg2`  | источник (src) / правый операнд / порт (для IN/OUT); **не используется** для CASTZ/CASTS |

Конкретные соглашения см. в таблице опкодов (§ 4).

### 3.4. DataSize (`OpCodeSize`)

| Код (`b2b1`) | Значение | Размер | Требование к выравниванию адреса |
|:------------:|----------|-------:|-------------------------------|
| `00` | `S8`  | 1 байт  | — |
| `01` | `S16` | 2 байта | 2 (`addr & 1 == 0`) |
| `10` | `S32` | 4 байта | 4 (`addr & 3 == 0`) |
| `11` | `S64` | 8 байт  | 8 (`addr & 7 == 0`) |

Используется `LOAD`, `STORE`, `LOAD_IND`, `STORE_IND`, их `_UNSAFE`-вариантами, **а также `CASTZ` и `CASTS`** (в последних означает ширину усечения/расширения).

---

## 4. Опкоды (реестр)

Все значения — от 0 до **46**; исходное перечисление `OpCode : byte`. Значения зафиксированы порядком объявления и не могут быть переставлены без обновления декодера и сохранённых образов.

| # | Мнемоника | Reg1 | Reg2 | Size | Immediate | Краткий смысл |
|--:|-----------|------|------|:----:|:---------:|---------------|
| 0  | `NOP`            | — | — | — | — | нет операции |
| 1  | `END`            | — | — | — | — | штатное завершение программы |
| 2  | `MOV`            | dst | src | — | — | `dst = src` |
| 3  | `LOAD`           | dst | — | + | + | `dst = [addr]` (checked) |
| 4  | `STORE`          | src | — | + | + | `[addr] = src` (checked) |
| 5  | `LDI`            | dst | — | — | + | `dst = imm` |
| 6  | `LOAD_IND`       | dst | addr | + | — | `dst = [Reg2]` (checked) |
| 7  | `STORE_IND`      | src | addr | + | — | `[Reg2] = Reg1` (checked) |
| 8  | `ADD`            | dst | src | — | — | `dst += src` |
| 9  | `SUB`            | dst | src | — | — | `dst -= src` |
| 10 | `MULT_INT`       | dst | src | — | — | `dst *= src` (wrap-around, unsigned) |
| 11 | `SHR`            | dst | shift | — | — | `dst >>= (byte)shift` |
| 12 | `SHL`            | dst | shift | — | — | `dst <<= (byte)shift` |
| 13 | `INC`            | reg | — | — | — | `reg += 1` |
| 14 | `DEC`            | reg | — | — | — | `reg -= 1` |
| 15 | `DIV`            | dst | src | — | — | `dst /= src` (unsigned), `DivOnZero` при 0 |
| 16 | `AND`            | dst | src | — | — | `dst &= src` |
| 17 | `OR`             | dst | src | — | — | `dst \|= src` |
| 18 | `XOR`            | dst | src | — | — | `dst ^= src` |
| 19 | `NOT`            | reg | — | — | — | `reg = ~reg` |
| 20 | `JMP`            | — | — | — | + | `rIP = target` |
| 21 | `JZ`             | — | — | — | + | if Z: `rIP = target` |
| 22 | `JNZ`            | — | — | — | + | if !Z: `rIP = target` |
| 23 | `JG`             | — | — | — | + | if !N && !Z: `rIP = target` |
| 24 | `JL`             | — | — | — | + | if N: `rIP = target` |
| 25 | `PUSH`           | src | — | — | — | push 8 байт |
| 26 | `POP`            | dst | — | — | — | pop 8 байт |
| 27 | `CALL`           | — | — | — | + | push rIP; `rIP = target` |
| 28 | `RET`            | — | — | — | — | `rIP = pop` |
| 29 | `IN`             | dst | port | — | — | `dst = порт[port]` |
| 30 | `OUT`            | src | port | — | — | `порт[port] = (byte)src` |
| 31 | `CALL_IND`       | addr | — | — | — | `CALL [Reg1]` (checked) |
| 32 | `JMP_IND`        | addr | — | — | — | `JMP [Reg1]` (checked) |
| 33 | `HALT`           | — | — | — | — | `IsSleeping = true` |
| 34 | `WAKE`           | — | — | — | — | no-op в потоке; как внешняя команда — пробуждение |
| 35 | `WAKE_INT`       | port | — | — | — | пробудить устройство по `порт[Reg1]` |
| 36 | `LOAD_UNSAFE`    | dst | — | + | + | `dst = [addr]` (unchecked) |
| 37 | `STORE_UNSAFE`   | src | — | + | + | `[addr] = src` (unchecked) |
| 38 | `STORE_IND_UNSAFE` | src | addr | + | — | `[Reg2] = Reg1` (unchecked) |
| 39 | `LOAD_IND_UNSAFE`  | dst | addr | + | — | `dst = [Reg2]` (unchecked) |
| 40 | `CMP`            | left | right | — | — | обновляет `rFL` по `left - right` |
| 41 | `TEST`           | left | right | — | — | обновляет `rFL` по `left & right` |
| 42 | `MOD`            | dst | src | — | — | `dst %= src` (unsigned), `DivOnZero` при 0 |
| 43 | `CASTZ`          | reg | — | + | — | `reg = zero-extend(reg, size)` — маска младших `size` байт |
| 44 | `CASTS`          | reg | — | + | — | `reg = sign-extend(reg, size)` — знаковое расширение |
| 45 | `JA`             | — | — | — | + | if !C && !Z: `rIP = target` |
| 46 | `JB`             | — | — | — | + | if C: `rIP = target` |

> **Важно:** опкод `NOP = 0` и `END = 1` — семантически несовместимы с обычным "нет операции" в контексте `ProcessorFault`; см. § 12 п. 3.

Непосредственный операнд присутствует **только** у опкодов, отмеченных `HasNeed64IntData = true`. В эталоне:

```
LDI, LOAD, STORE, LOAD_UNSAFE, STORE_UNSAFE,
CALL, JMP, JZ, JNZ, JG, JL, JB, JA
```

> **Исправление:** в прежней версии спецификации в этот список ошибочно попадали `LOAD_IND`/`STORE_IND` и их `_UNSAFE`-варианты. Эти опкоды берут адрес из `Reg2` и **immediate не читают**. Таблица `_has` в `InstructionDecoder.CreateNeedsUlongOperandTable()` содержит ровно 11 записей, перечисленных выше.

> **Проверка границ `HasNeed64IntData`:** реализована по `_has.Length` (256), а не по `OpCode.MOD` (42) — иначе `CASTZ`/`CASTS`/`CMP`/`TEST`/`WAKE_INT`/`JMP_IND`/`CALL_IND`/`HALT`/`WAKE` (все выше `MOD`) приводили бы к `ArgumentOutOfRangeException` в дизассемблере.

---

## 5. Семантика инструкций

### 5.1. Системные

| Опкод | Действие |
|-------|----------|
| `NOP` | пустая операция, `rIP` уже сдвинут на 4 |
| `END` | `IsRunning = false`; возвращается `BiosStatus.EndProgram`; далее — `IProcessorPolicy.ShouldContinue`, её ответ перезаписывает `IsRunning` |

### 5.2. Пересылки и загрузка-сохранение

**`MOV Reg1, Reg2`** -> `Reg1 = Reg2` (полные 64 бита).

**`LDI Reg1, imm`** -> `Reg1 = imm` (64-битный immediate из потока).

**`LOAD[.size] Reg1, [imm]`** — проверяемое чтение:
1. `addr = imm`
2. проверка выравнивания по `size` (`addr & (N-1) == 0`)
3. проверка `addr + N <= RAM.Length`
4. чтение `N` байт little-endian, расширение до 64 бит **беззнаковое** (нулевое расширение)
5. `Reg1 = value`

Ошибки: `AlignmentFault` (с `FaultData = addr`), `SegmentationFault` (с `FaultData = addr`).

**`STORE[.size] [imm], Reg1`** — проверяемая запись:
1. `addr = imm`
2. проверки те же
3. запись младших `N` байт `Reg1` little-endian в `[addr]`

**`LOAD_IND[.size] Reg1, Reg2`** — то же, что `LOAD`, но `addr = Reg2`.

**`STORE_IND[.size] Reg1, Reg2`** — то же, что `STORE`, но `addr = Reg2`.

**`*_UNSAFE`-варианты** — без проверок. Поведение при выходе за границы или нарушении выравнивания — **неопределённое** (`Unsafe.ReadUnaligned`/`WriteUnaligned` по указателю за пределами буфера).

### 5.2.1. Приведение размера

**`CASTZ[.size] Reg1`** — zero-extend (маска):
1. `mask = MaskOf(size)` — младшие `size` байт (для `S8` — `0xFF`, `S16` — `0xFFFF`, `S32` — `0xFFFFFFFF`, `S64` — `0xFFFFFFFFFFFFFFFF`)
2. `Reg1 = Reg1 & mask`

Результат: значение в диапазоне `[0, 2^(8·size) − 1]`.

**`CASTS[.size] Reg1`** — sign-extend (знаковое расширение):
1. `Reg1 = (signed)(Reg1 & mask)` — старшие биты регистра заполняются знаковым битом `size`-разрядного значения
2. Для `S64` — тождественная операция
3. Результат: значение в диапазоне `[−2^(8·size−1), 2^(8·size−1) − 1]` для signed-интерпретации

Обе инструкции:
- **4 байта**, без immediate
- Используют только `Reg1` и `DataSize`
- Не меняют `rFL` и другие регистры
- Используются кодогенератором MiniC для реализации **переполнения узких типов** (см. Приложение A)

### 5.3. Арифметика (беззнаковая, 64-бит, wrap-around)

Все инструкции изменяют только `Reg1`.

| Опкод | Формула |
|-------|---------|
| `ADD Reg1, Reg2` | `Reg1 = Reg1 + Reg2` |
| `SUB Reg1, Reg2` | `Reg1 = Reg1 - Reg2` |
| `MULT_INT Reg1, Reg2` | `Reg1 = Reg1 * Reg2` (переполнение отбрасывается) |
| `DIV Reg1, Reg2` | `Reg1 = Reg1 / Reg2` (unsigned); `DivOnZero`, если `Reg2 == 0` |
| `MOD Reg1, Reg2` | `Reg1 = Reg1 % Reg2` (unsigned); `DivOnZero`, если `Reg2 == 0` |
| `INC Reg1` | `Reg1 += 1` |
| `DEC Reg1` | `Reg1 -= 1` |
| `SHR Reg1, Reg2` | `Reg1 >>= (byte)Reg2` (младший байт) |
| `SHL Reg1, Reg2` | `Reg1 <<= (byte)Reg2` (младший байт) |

### 5.4. Логические

| Опкод | Формула |
|-------|---------|
| `AND Reg1, Reg2` | `Reg1 &= Reg2` |
| `OR Reg1, Reg2`  | `Reg1 \|= Reg2` |
| `XOR Reg1, Reg2` | `Reg1 ^= Reg2` (типично для обнуления: `XOR Rn, Rn`) |
| `NOT Reg1`       | `Reg1 = ~Reg1` |

### 5.5. Сравнения (обновляют `rFL`)

`rFL` (28): бит 0 — **Zero (Z)**, бит 1 — **Negative (N)**, бит 2 - **Carry (C)**. Остальные биты — 0.

**`CMP Reg1, Reg2`** — вычисляется `res = Reg1 - Reg2` (64-бит, беззнаковое):
- `Z = (res == 0)`
- `N = (res >> 63) & 1`
- `c = (v1 < v2) ? 1UL : 0UL`

`Reg1`/`Reg2` не изменяются.

**`TEST Reg1, Reg2`** — вычисляется `res = Reg1 & Reg2`:
- `Z = (res == 0)`
- `N = (res >> 63) & 1`
- `c = (v1 < v2) ? 1UL : 0UL`

`Reg1`/`Reg2` не изменяются.

### 5.6. Управление потоком

> проверка `target < RAM.Length`; при провале — `SegmentationFault`; иначе `rIP = target`

**`JMP target`** -> `rIP = target`.

**`JZ target`** — если `Z=1`, `rIP = target`.

**`JNZ target`** — если `Z=0`, `rIP = target`.

**`JG target`** — если `Z=0` и `N=0`, `rIP = target` (знаковое "больше").

**`JL target`** — если `N=1`, `rIP = target` (знаковое "меньше").

**`JA target`** — если `C=0` и `Z=0`, `rIP = target` (беззнаковое "больше").

**`JB target`** — если `C=1`, `rIP = target` (беззнаковое "меньше").

**`JMP_IND Reg1`** — `target = Reg1`.

**`CALL target`** — вызов подпрограммы:
1. `if rSP < 8 -> SegmentationFault`
2. `sp = rSP - 8`
3. `if rHP >= sp -> SegmentationFault` (коллизия стека с кучей)
4. `RAM[sp] = rIP` (unchecked write, little-endian)
5. `rSP = sp; rIP = target`

**`CALL_IND Reg1`** — `target = Reg1`; проверка `target < RAM.Length`; при провале — `SegmentationFault`; иначе — эквивалент `CALL target`.

**`RET`** — возврат:
1. `if rSP >= stackBase -> StackUnderflow`
2. `rIP = RAM[rSP]` (unchecked read, 8 байт LE)
3. `rSP += 8`

`stackBase` — значение `rSP` сразу после `LaunchProgramm` (вершина ОЗУ, выровненная вниз по 8).

### 5.7. Стек

**`PUSH Reg1`**:
1. `sp = rSP - 8`
2. `if rHP >= sp -> SegmentationFault`
3. `RAM[sp] = Reg1` (checked write 8 байт)
4. если запись провалилась -> вернуть её статус
5. `rSP = sp`

**`POP Reg1`**:
1. `value = RAM[rSP]` (checked read 8 байт LE)
2. если чтение провалилось -> вернуть статус
3. `Reg1 = value; rSP += 8`

### 5.8. Порты ввода-вывода

Порт-пространство: `PortBus.TotalPorts = 2^ports.Log2`, размер сектора `2^portsOnDev.Log2`. Адрес = `(sector << shift) | offset`, где `shift = portsOnDev.Log2`.

**`IN Reg1, Reg2`** — `addr = Reg2`; `Reg1 = PortBus.ReadPort(addr)`. Если устройство по адресу не зарегистрировано -> `NullDeviceInput`, `FaultData = addr`. Устройство возвращает **1 байт**, расширенный до 64 бит нулями.

**`OUT Reg1, Reg2`** — `addr = Reg2`; `PortBus.WritePort(addr, (byte)Reg1)`. При отсутствии устройства -> `NullDeviceOutput`. Передаётся только младший байт `Reg1`.

**`WAKE_INT Reg1`** — `addr = Reg1`; `PortBus.WakeProcessor(addr)`. Если устройство не найдено -> `NullDeviceOutput`. Устройство получает сигнал `WakeProcessor()`, снимающий `IsSleeping` на следующем шаге.

### 5.9. Сон

**`HALT`** — `IsSleeping = true`. Обработка внешних команд продолжается, но шаги тела программы не выполняются.

**`WAKE`** — как инструкция в теле программы: no-op (`Success`). Как **внешняя команда** через `EnqueueExternalCommand` или `PortBus.WakeProcessor` — снимает `IsSleeping`.

**`WAKE_INT Reg1`** — см. 5.8. Порты ввода-вывода

---

## 6. Модель памяти

### 6.1. ОЗУ

- Единое байтовое адресное пространство `[0, RAM.Length)`.
- Порядок байтов: **little-endian** (endianness фиксируется `NativeMemoryBuffer` на всех платформах).
- Размер: `RamSize` (степень двойки, 64 B … 2 GiB). По умолчанию выделяется через `NativeMemory.AllocZeroed`.

### 6.2. Проверяемые vs непроверяемые обращения

| Аспект | Проверяемые (`LOAD`/`STORE`/`LOAD_IND`/`STORE_IND`) | Непроверяемые (`*_UNSAFE`) |
|--------|----------------------------------------------------|----------------------------|
| Проверка выравнивания | да (для `S16/S32/S64`) | нет |
| Проверка границ | да (`addr + N <= Length`) | нет |
| Статус ошибки | `AlignmentFault` / `SegmentationFault` | н/д, UB |
| `FaultData` | адрес | н/д |
| Возвращаемое значение при ошибке чтения | 0 | UB |

### 6.3. Immediate-операнды

- Читаются через `ReadInt64LEUnSafe` (непроверяемое, unaligned).
- Адрес = `AlignUp(rIP_after_instr, 8)`.
- После чтения `rIP = addr + 8`.

---

## 7. Порты ввода-вывода

- Адресное пространство: `PortBus.TotalPorts` (степень двойки, `PortSize`).
- Разбито на `SectorCount = TotalPorts / PortsOnDevice` секторов.
- Один сектор = одно устройство (`IPortController`).
- Адрес: `sector = addr >> shift`, `offset = addr & (PortsOnDevice - 1)`, `shift = log2(PortsOnDevice)`.
- Сектор 0..`SectorCount-1`. Если сектор не зарегистрирован — ошибка (`NullDeviceInput`/`NullDeviceOutput`).

Устройство реализует `ReadPort(offset) -> byte` и `WritePort(offset, value)`.

---

## 8. Модель сбоев

### 8.1. Статусы (`BiosStatus : byte`)

| Код | Имя | Смысл |
|----:|-----|-------|
| 0  | `Success` | успех (default, безопасное значение) |
| 1  | `SegmentationFault` | выход за границы ОЗУ / коллизия стека и кучи |
| 2  | `AlignmentFault` | неверное выравнивание адреса |
| 3  | `EndProgram` | штатное завершение (`END`) |
| 4  | `HostStopped` | остановка со стороны хоста (`Stop`) |
| 5  | `NotImplementedOpCode` | опкод не имеет обработчика |
| 6  | `NullDeviceInput` | нет устройства ввода |
| 7  | `NullDeviceOutput` | нет устройства вывода / порта пробуждения |
| 8  | `InfinityLoop` | сработал лимит шагов / политика |
| 9  | `DivOnZero` | деление на ноль (`DIV`, `MOD`) |
| 10 | `NotSupportedOpCode` | опкод известен, но не поддерживается |
| 11 | `StackUnderflow` | `RET` на пустом стеке / переполнение стека |
| 12 | `UnsafeOpCodeForbidden`| использование небезопасного `OpCode` при предварительном запрете политики `IProcessorPolicy.AllowUnsafeOpCodes` |

### 8.2. `ProcessorFault`

При получении статуса ≠ `Success` (включая "штатные" `EndProgram`, `HostStopped`, `InfinityLoop`) формируется снимок:

```csharp
ProcessorFault(BiosStatus Status,
               ulong FaultData,
               ulong InstructionPointer,
               OpCode OpCode);
```

- `InstructionPointer` — `rIP` сразу после чтения 4-байтного слова инструкции.
- `OpCode` — реальный опкод; значение `OpCode.NOP` (0) зарезервировано под "неизвестно/нет опкода" (спорное решение, но зафиксировано).
- `FaultData` — контекст: адрес памяти, номер порта, опкод и т.п.

Далее вызывается `IProcessorPolicy.ShouldContinue(fault)`; её ответ записывается в `IsRunning`. Политика **обязана** различать "настоящие ошибки" и "штатные" `EndProgram`/`HostStopped`/`InfinityLoop`.

---

## 9. Исполнение шага (`Processor.Step`)

Псевдокод одного шага:

```
1. Если _hasCommand:
     a. Обработать все внешние команды (_externalCommands).
     b. Обработать все внешние статусы (_statusFromSimulation):
        - при ненулевом статусе — сформировать ProcessorFault и опросить политику.
     c. _hasCommand = false.
2. Если !IsRunning || IsSleeping -> выход.
3. rIP_cur = rIP
4. rawInst = RAM[rIP_cur]                     // unchecked, 32 бита LE
5. opCode = Decode(rawInst).OpCode
6. rIP = rIP_cur + 4
7. status = switch(opCode) { ... Instruction*(...) ... }
8. Если status != Success:
     _faultData = new(status, _lastFaultData, rIP, opCode)
     IsRunning = policy.ShouldContinue(_faultData)
```

Важно: `_lastFaultData` устанавливается внутри обработчика до возврата статуса (например, `FaultData` из `MemoryBus`, номер порта, опкод `NotImplementedOpCode`).

---

## 10. Ассемблерный синтаксис

Реализован в `AssemblerParser` (NANASM-совместимый текст). **Регистронезависимо** для мнемоник и регистров.

### 10.1. Регистры

`r0..r27`, `rFL`, `rSP`, `rHP`, `rIP`.

### 10.2. Мнемоники

- Без операндов: `NOP`, `END`, `RET`, `HALT`, `WAKE`.
- Один регистр: `INC`, `DEC`, `NOT`, `PUSH`, `POP`, `JMP_IND`, `CALL_IND`, `WAKE_INT`.
- **Один регистр + size: `CASTZ[.size] reg`, `CASTS[.size] reg`**.
- Два регистра: `MOV`, `ADD`, `SUB`, `AND`, `OR`, `XOR`, `IN`, `OUT`, `SHR`, `SHL`, `DIV`, `MULT_INT`, `MOD`, `CMP`, `TEST`.
- `LDI reg, imm`: `imm` — число (`0x…` или десятичное) или метка.
- `LOAD[.size] reg, addr`, `STORE[.size] addr, reg`: `addr` — число или метка.
- `LOAD_IND[.size] reg, reg`, `STORE_IND[.size] reg, reg` — аналогично.
- `LOAD_UNSAFE[.size]`, `STORE_UNSAFE[.size]`, `LOAD_IND_UNSAFE[.size]`, `STORE_IND_UNSAFE[.size]` — аналогично без проверок.
- Переходы: `JMP`, `JZ`, `JNZ`, `JG`, `JL`, `JA`, `JB`, `CALL` — метка или абсолютный адрес.

**Пример (CASTZ/CASTS):**
```asm
CASTZ.S8 r0     ; маска 8 бит: r0 &= 0xFF
CASTS.S32 r1    ; знаковое расширение 32->64: r1 = (int)(r1 & 0xFFFFFFFF)
CASTZ r2        ; эквивалент CASTZ.S64 — no-op (size по умолчанию S64)
```

### 10.3. Суффиксы размера

`.S8`, `.S16`, `.S32`, `.S64` (или `.8`, `.16`, `.32`, `.64`). По умолчанию — `.S64`. Разрешены пробелы вокруг точки: `LOAD . S8` нормализуется в `LOAD.S8`.

### 10.4. Метки и патчи

- `label:` — метка на текущую позицию.
- `label: <инструкция>` — метка + инструкция.
- Патчи для immediate-полей разрешаются при `Build()`: `targetAddr = baseAddress + offset(label)`.
- `baseAddress` должен быть кратен 8 (иначе immediate прочитается по другому адресу, чем записан).

### 10.5. Секции и данные

- Директивы: `.data`/`.bss`/`.section .data`/`.section .bss` — секция данных; `.text`/`.code`/`.section .text`/`.section .code` — секция кода.
- Директивы данных (только внутри `.data`): `.byte`/`db`, `.word`/`dw` (2), `.dword`/`dd` (4), `.quad`/`dq` (8), `.ascii`, `.asciz`/`.string`, `.zero`/`.space N`.
- Строки: `.byte "..."` допускает escape-последовательности `\n \t \r \0 \\ \" \'`.
- Выравнивание секции данных — по 8 байт.

---

## 11. Примеры кодирования

### 11.1. `MOV r1, r2`

```
OpCode = 2   (0x02)
Reg1   = 1
Reg2   = 2
raw = 0x02 | (1 << 8) | (2 << 13)
    = 0x00004102
Байты в памяти (LE): 02 41 00 00
```

### 11.2. `LDI r1, 42`

```
OpCode = 5
Reg1   = 1
raw = 0x05 | (1 << 8) = 0x0105
Занято: instr (4 байта), паддинг (4), imm (8 байт)
Байты (base=0): 05 01 00 00 | 00 00 00 00 | 2A 00 00 00 00 00 00 00
```

### 11.3. `JMP label` (метка на 0x100)

```
OpCode = 20 (0x14)
Size   = S64 (3) в битах 18–19
raw = 0x14 | (3 << 18) = 0x000C0014
Байты (base=0, label=0x100): 14 00 0C 00 | 00 00 00 00 | 00 01 00 00 00 00 00 00
```

### 11.4. `LOAD.S32 r5, [0x10]`

```
OpCode = 3
Reg1   = 5
Size   = S32 (2)
raw = 0x03 | (5 << 8) | (2 << 18) = 0x00080503
Байты: 03 05 08 00 | 00 00 00 00 | 10 00 00 00 00 00 00 00
```

### 11.5. `ADD r1, r2`

```
OpCode = 8
Reg1   = 1
Reg2   = 2
raw = 0x08 | (1 << 8) | (2 << 13) = 0x00004108
Байты: 08 41 00 00
```

### 11.6. `CASTS.S16 r5`

```
OpCode = 44 (0x2C)
Reg1   = 5
Size   = S16 (1)
raw = 0x2C | (5 << 8) | (1 << 18) = 0x0004052C
Байты: 2C 05 04 00
```

Дизассемблер: `CASTS.S16 r5` (раньше — `UNKNOWN 0x2C`).

---

## 12. Ограничения и известные особенности реализации

1. **`NOP` (0) как маркер "неизвестного опкода** в `ProcessorFault` (`ProcessSimulationStatusSlow`). Спорное соглашение; при появлении реального NOP-сбоя отличить его от "пустого" значения будет невозможно.
2. **`WAKE` как инструкция в теле — no-op.** Пробуждение из `HALT` возможно только через `EnqueueExternalCommand(OpCode.WAKE)` или `PortBus.WakeProcessor`.
3. **`InstructionLOAD`/`InstructionSTORE` не возвращают `AlignmentFault` для невалидного `OpCodeSize`** (там `default` -> `SegmentationFault` у LOAD и `AlignmentFault` у STORE); на практике `OpCodeSize` имеет ровно 4 значения — `default` недостижим.
4. **`InstructionDecoder.HasNeed64IntData` валидирует `opCode` по `_has.Length` (256), а не по `OpCode.MOD`.** Раньше bound-check был `opCode > OpCode.MOD`, из-за чего любой опкод выше 42 (`CASTZ`, `CASTS`, `CMP`, `TEST`, `HALT`, `WAKE`, `WAKE_INT`, `JMP_IND`, `CALL_IND`) вызывал `ArgumentOutOfRangeException` при дизассемблировании. Исправлено.
5. **`AssemblerParser` не потокобезопасен.** Поля `_asm`, `_inDataSection`, `_pendingData`, `_pendingLabel`, `_currentLine` — instance-level. `StatementGenerator.GenerateInlineAsm` создаёт `new AssemblerParser()` на каждый `asm`-блок, но контракт не задокументирован.
6. **`IRAssembler.EmitInstruction64WithLabel` не валидирует `HasNeed64IntData`** (в отличие от `EmitInstruction64`). При добавлении label-aware опкодов с 64-битным операндом проверку придётся добавить вручную.

---

## 13. Быстрая шпаргалка по соглашениям

| Соглашение | Значение |
|------------|----------|
| Порядок байт | little-endian везде |
| Формат слова | `[7:0]=op, [12:8]=reg1, [17:13]=reg2, [19:18]=size, [31:20]=0` |
| Immediate | всегда 64 бита, 8-байтное выравнивание после инструкции |
| Длина инструкции | 4 байта (без immediate) или 12 байт (с immediate) |
| Регистры | 32 × 64 бита, `rIP` — адрес следующей инструкции |
| Флаги | обновляются только `CMP` и `TEST` |
| Стек | 8-байтные слоты, `rSP` растёт вниз, `rHP` — верхняя граница кучи |
| Ошибка доступа | `AlignmentFault` / `SegmentationFault`, не исключения |
| Политика | `IProcessorPolicy.ShouldContinue` — последняя инстанция, `IProcessorPolicy.AllowUnsafeOpCodes` — разрешение использовать небезопасные опкоды |
| CASTZ/CASTS | 4-байтные, размер в DataSize, без immediate, не меняют `rFL` |

---

## Приложение A. Модель переполнения целых типов в MiniC

### A.1. Каноническая форма

После любой записи в переменную или регистр значение приводится к канонической форме своего типа через `CASTZ`/`CASTS`:

| Тип | Размер | Знаковый | Канон в регистре |
|-----|:------:|:--------:|------------------|
| `byte` | S8 | нет | `0 .. 255` (верхние 56 бит нули) |
| `char` | S16 | да | `−32768 .. 32767`, sign-extended до 64 бит |
| `short` | S16 | да | `−32768 .. 32767`, sign-extended |
| `ushort` | S16 | нет | `0 .. 65535` |
| `int` | S32 | да | `−2^31 .. 2^31−1`, sign-extended |
| `uint` | S32 | нет | `0 .. 2^32−1` |
| `long` | S64 | да | `−2^63 .. 2^63−1` |
| `ulong` | S64 | нет | `0 .. 2^64−1` |

> **`char` в MiniC — 16-битный знаковый (`S16`)**, не 8-битный как в стандартном C. Следствия:
> - `sizeof(char) == 2`
> - `char c = 120; c += 100;` -> `c == 220` (обёртки нет, влезает в `int16`)
> - `char c = 32767; c += 1;` -> `c == −32768` (обёртка `S16`)

### A.2. Где вставляются касты (кодогенератор `Nantuc.Compiler.C.CodeGenerator`)

| Событие | Каст |
|---------|------|
| `LOAD[.size]` из памяти | `LOAD` zero-extends -> **`CASTS.S<size>`** для signed-типов (иначе значение знакового типа окажется "положительным") |
| `STORE[.size]` в память | `STORE` сам усекает -> каст не нужен |
| `x = value` (register-переменная) | после `MOV`/`ADD`/`INC` — **`CASTZ`/`CASTS`** |
| `x op= value`, `x++` | после операции — **`CASTZ`/`CASTS`** в home-регистре |
| `(x = value)` / `(x op= value)` в выражении | после операции — **`CASTZ`/`CASTS`** в `targetReg` |
| `arr[i] = value`, `*p = value`, `obj.f = value` | после load — **`CASTZ`/`CASTS`**; после операции — **`CASTZ`/`CASTS`**; `STORE_IND` усекает |
| `arr[i]++`, `(*p)++`, `(obj.f)++` | после load и после `INC`/`DEC` — **`CASTZ`/`CASTS`** |
| Call-site: аргумент -> `r0..r3` | после генерации выражения — **`CASTZ`/`CASTS`** к типу параметра |
| Prologue: параметр 4+ из pseudo-global | после `LOAD` — **`CASTZ`/`CASTS`** |
| `RETURN`: значение в `r0` | перед `JMP epilogue` / `END` — **`CASTZ`/`CASTS`** к типу возврата (для non-pointer scalar) |

### A.3. Что **не** кастуется

- **Промежуточный результат арифметики.** C-семантика: `byte + byte` даёт `int`; `int + int` даёт `int` с wrap-around `S32`; усечение происходит только на **следующей** записи.
- **`long`/`ulong`** — `S64`, `EmitCastToType` для них no-op.
- **Указатели и структуры** — каст не применяется (канон — 8-байтный адрес).

### A.4. Примеры

```c
byte b = 250;   b += 10;   // b == 4    (CASTZ.S8 после ADD)
byte b = 0;     b--;       // b == 255  (CASTZ.S8 после DEC)
char c = 32767; c += 1;    // c == -32768  (CASTS.S16 после ADD)
short s = 30000; s += 10000; // s == -25536 (CASTS.S16)
int  i = 0x7FFFFFFF; i += 1; // i == INT_MIN (CASTS.S32)
ushort u = 65535; u += 1;  // u == 0    (CASTZ.S16)
byte f() { return 300; }   // -> 44       (CASTZ.S8 перед END/RET)
int  g(byte x) { return x; }
g(300);                    // x == 44    (CASTZ.S8 на call-site)
byte a[2]; a[0] = 250; a[0] += 10; // a[0] == 4 (CASTZ.S8 на load и на result)
```
