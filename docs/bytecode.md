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
| 28     | `rFL` | Flags: бит 0 = Zero, бит 1 = Negative |
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
- **DataSize** (2 бита) — только для группы LOAD/STORE (`OpCodeSize`).
- **Биты 20–31** — зарезервированы, должны быть нулевыми.

Декодер (эталон): `InstructionDecoder.GetOpCode/GetReg1/GetReg2/GetDataSizeCode`.

### 3.2. Общая длина инструкции

| Форма | Длина | Когда |
|-------|-------|-------|
| Регистр–регистр / без операндов | **4 байта** | все, кроме перечисленных ниже |
| Инструкция + immediate | **12 байт** (4 + 4 паддинга + 8) | если `InstructionDecoder.HasNeed64IntData(opCode) == true` |

**Immediate** читается по адресу `AlignUp(rIP_after_instr, 8)`. Паддинг между инструкцией и immediate игнорируется (может быть любым, обычно 0). После чтения `rIP = immediate_addr + 8`.

### 3.3. Операнды-регистры по опкодам

| Операнд | Что кодирует |
|---------|--------------|
| `Reg1`  | приёмник (dst) / левый операнд / единственный операнд |
| `Reg2`  | источник (src) / правый операнд / порт (для IN/OUT) |

Конкретные соглашения см. в таблице опкодов (§ 4).

### 3.4. DataSize (`OpCodeSize`)

| Код (`b2b1`) | Значение | Размер | Требование к выравниванию адреса |
|:------------:|----------|-------:|-------------------------------|
| `00` | `S8`  | 1 байт  | — |
| `01` | `S16` | 2 байта | 2 (`addr & 1 == 0`) |
| `10` | `S32` | 4 байта | 4 (`addr & 3 == 0`) |
| `11` | `S64` | 8 байт  | 8 (`addr & 7 == 0`) |

Используется только `LOAD`, `STORE`, `LOAD_IND`, `STORE_IND` и их `_UNSAFE`-вариантами.

---

## 4. Опкоды (реестр)

Все значения — от 0 до 42; исходное перечисление `OpCode : byte`. Значения зафиксированы порядком объявления и не могут быть переставлены без обновления декодера и сохранённых образов.

| # | Мнемоника | Reg1 | Reg2 | Size | Immediate | Краткий смысл |
|--:|-----------|------|------|:----:|:---------:|---------------|
| 0  | `NOP`            | — | — | — | — | нет операции |
| 1  | `END`            | — | — | — | — | штатное завершение программы |
| 2  | `MOV`            | dst | src | — | — | `dst = src` |
| 3  | `LOAD`           | dst | — | ✓ | ✓ | `dst = [addr]` (checked) |
| 4  | `STORE`          | src | — | ✓ | ✓ | `[addr] = src` (checked) |
| 5  | `LDI`            | dst | — | — | ✓ | `dst = imm` |
| 6  | `LOAD_IND`       | dst | addr | ✓ | — | `dst = [Reg2]` (checked) |
| 7  | `STORE_IND`      | src | addr | ✓ | — | `[Reg2] = Reg1` (checked) |
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
| 20 | `JMP`            | — | — | — | ✓ | `rIP = target` |
| 21 | `JZ`             | — | — | — | ✓ | if Z: `rIP = target` |
| 22 | `JNZ`            | — | — | — | ✓ | if !Z: `rIP = target` |
| 23 | `JG`             | — | — | — | ✓ | if !N && !Z: `rIP = target` |
| 24 | `JL`             | — | — | — | ✓ | if N: `rIP = target` |
| 25 | `PUSH`           | src | — | — | — | push 8 байт |
| 26 | `POP`            | dst | — | — | — | pop 8 байт |
| 27 | `CALL`           | — | — | — | ✓ | push rIP; `rIP = target` |
| 28 | `RET`            | — | — | — | — | `rIP = pop` |
| 29 | `IN`             | dst | port | — | — | `dst = порт[port]` |
| 30 | `OUT`            | src | port | — | — | `порт[port] = (byte)src` |
| 31 | `CALL_IND`       | addr | — | — | — | `CALL [Reg1]` (checked) |
| 32 | `JMP_IND`        | addr | — | — | — | `JMP [Reg1]` (checked) |
| 33 | `HALT`           | — | — | — | — | `IsSleeping = true` |
| 34 | `WAKE`           | — | — | — | — | no-op в потоке; как внешняя команда — пробуждение |
| 35 | `WAKE_INT`       | port | — | — | — | пробудить устройство по `порт[Reg1]` |
| 36 | `LOAD_UNSAFE`    | dst | — | ✓ | ✓ | `dst = [addr]` (unchecked) |
| 37 | `STORE_UNSAFE`   | src | — | ✓ | ✓ | `[addr] = src` (unchecked) |
| 38 | `STORE_IND_UNSAFE` | src | addr | ✓ | — | `[Reg2] = Reg1` (unchecked) |
| 39 | `LOAD_IND_UNSAFE`  | dst | addr | ✓ | — | `dst = [Reg2]` (unchecked) |
| 40 | `CMP`            | left | right | — | — | обновляет `rFL` по `left - right` |
| 41 | `TEST`           | left | right | — | — | обновляет `rFL` по `left & right` |
| 42 | `MOD`            | dst | src | — | — | `dst %= src` (unsigned), `DivOnZero` при 0 |

Непосредственный операнд присутствует **только** у опкодов, отмеченных `HasNeed64IntData = true` (в эталоне: `LDI`, `LOAD`, `STORE`, `CALL`, `JMP`, `JZ`, `JNZ`, `JG`, `JL`, `LOAD_UNSAFE`, `STORE_UNSAFE`).

---

## 5. Семантика инструкций

### 5.1. Системные

| Опкод | Действие |
|-------|----------|
| `NOP` | пустая операция, `rIP` уже сдвинут на 4 |
| `END` | `IsRunning = false`; возвращается `BiosStatus.EndProgramm`; далее — `IProcessorFaultPolicy.ShouldContinue`, её ответ перезаписывает `IsRunning` |

### 5.2. Пересылки и загрузка-сохранение

**`MOV Reg1, Reg2`** → `Reg1 = Reg2` (полные 64 бита).

**`LDI Reg1, imm`** → `Reg1 = imm` (64-битный immediate из потока).

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

`rFL` (28): бит 0 — **Zero (Z)**, бит 1 — **Negative (N)**. Остальные биты — 0.

**`CMP Reg1, Reg2`** — вычисляется `res = Reg1 - Reg2` (64-бит, беззнаковое):
- `Z = (res == 0)`
- `N = (res >> 63) & 1`

`Reg1`/`Reg2` не изменяются.

**`TEST Reg1, Reg2`** — вычисляется `res = Reg1 & Reg2`:
- `Z = (res == 0)`
- `N = (res >> 63) & 1`

`Reg1`/`Reg2` не изменяются.

### 5.6. Управление потоком

**`JMP target`** → `rIP = target`. Границы **не проверяются** — прыжок в недопустимую область приведёт к неопределённому поведению на следующем шаге (чтение инструкции через `ReadInt32LEUnSafe`).

**`JZ target`** — если `Z=1`, `rIP = target`.

**`JNZ target`** — если `Z=0`, `rIP = target`.

**`JG target`** — если `Z=0` и `N=0`, `rIP = target` (беззнаковое «больше»).

**`JL target`** — если `N=1`, `rIP = target` (беззнаковое «меньше»).

**`JMP_IND Reg1`** — `target = Reg1`; проверка `target < RAM.Length`; при провале — `SegmentationFault`; иначе `rIP = target`.

**`CALL target`** — вызов подпрограммы:
1. `if rSP < 8 → SegmentationFault`
2. `sp = rSP - 8`
3. `if rHP >= sp → SegmentationFault` (коллизия стека с кучей)
4. `RAM[sp] = rIP` (unchecked write, little-endian)
5. `rSP = sp; rIP = target`

**`CALL_IND Reg1`** — `target = Reg1`; проверка `target < RAM.Length`; при провале — `SegmentationFault`; иначе — эквивалент `CALL target`.

**`RET`** — возврат:
1. `if rSP >= stackBase → StackUnderflow`
2. `rIP = RAM[rSP]` (unchecked read, 8 байт LE)
3. `rSP += 8`

`stackBase` — значение `rSP` сразу после `LaunchProgramm` (вершина ОЗУ, выровненная вниз по 8).

### 5.7. Стек

**`PUSH Reg1`**:
1. `sp = rSP - 8`
2. `RAM[sp] = Reg1` (checked write 8 байт)
3. если запись провалилась → вернуть её статус
4. `if rHP >= sp → SegmentationFault` (запись уже выполнена!)
5. `rSP = sp`

> ⚠️ В текущей реализации проверка коллизии с кучей происходит **после** записи. Это архитектурная особенность/дефект: при коллизии значение окажется в памяти, но `rSP` не обновится, и будет возвращён `SegmentationFault`.

**`POP Reg1`**:
1. `value = RAM[rSP]` (checked read 8 байт LE)
2. если чтение провалилось → вернуть статус
3. `Reg1 = value; rSP += 8`

### 5.8. Порты ввода-вывода

Порт-пространство: `PortBus.TotalPorts = 2^ports.Log2`, размер сектора `2^portsOnDev.Log2`. Адрес = `(sector << shift) | offset`, где `shift = portsOnDev.Log2`.

**`IN Reg1, Reg2`** — `addr = Reg2`; `Reg1 = PortBus.ReadPort(addr)`. Если устройство по адресу не зарегистрировано → `NullDeviceInput`, `FaultData = addr`. Устройство возвращает **1 байт**, расширенный до 64 бит нулями.

**`OUT Reg1, Reg2`** — `addr = Reg2`; `PortBus.WritePort(addr, (byte)Reg1)`. При отсутствии устройства → `NullDeviceOutput`. Передаётся только младший байт `Reg1`.

**`WAKE_INT Reg1`** — `addr = Reg1`; `PortBus.WakeProcessor(addr)`. Если устройство не найдено → `NullDeviceOutput`. Устройство получает сигнал `WakeProcessor()`, снимающий `IsSleeping` на следующем шаге.

### 5.9. Сон

**`HALT`** — `IsSleeping = true`. Обработка внешних команд продолжается, но шаги тела программы не выполняются.

**`WAKE`** — как инструкция в теле программы: no-op (`Success`). Как **внешняя команда** через `EnqueueExternalCommand` или `PortBus.WakeProcessor` — снимает `IsSleeping`.

**`WAKE_INT Reg1`** — см. § 5.8.

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

Устройство реализует `ReadPort(offset) → byte` и `WritePort(offset, value)`.

---

## 8. Модель сбоев

### 8.1. Статусы (`BiosStatus : byte`)

| Код | Имя | Смысл |
|----:|-----|-------|
| 0  | `Success` | успех (default, безопасное значение) |
| 1  | `SegmentationFault` | выход за границы ОЗУ / коллизия стека и кучи |
| 2  | `AlignmentFault` | неверное выравнивание адреса |
| 3  | `EndProgramm` | штатное завершение (`END`) |
| 4  | `HostStopped` | остановка со стороны хоста (`Stop`) |
| 5  | `NotImplementedOpCode` | опкод не имеет обработчика |
| 6  | `NullDeviceInput` | нет устройства ввода |
| 7  | `NullDeviceOutput` | нет устройства вывода / порта пробуждения |
| 8  | `InfinityLoop` | сработал лимит шагов / политика |
| 9  | `DivOnZero` | деление на ноль (`DIV`, `MOD`) |
| 10 | `NotSupportedOpCode` | опкод известен, но не поддерживается |
| 11 | `StackUnderflow` | `RET` на пустом стеке / переполнение стека |

### 8.2. `ProcessorFault`

При получении статуса ≠ `Success` (включая «штатные» `EndProgramm`, `HostStopped`, `InfinityLoop`) формируется снимок:

```csharp
ProcessorFault(BiosStatus Status,
               ulong FaultData,
               ulong InstructionPointer,
               OpCode OpCode);
```

- `InstructionPointer` — `rIP` сразу после чтения 4-байтного слова инструкции.
- `OpCode` — реальный опкод; значение `OpCode.NOP` (0) зарезервировано под «неизвестно/нет опкода» (спорное решение, но зафиксировано).
- `FaultData` — контекст: адрес памяти, номер порта, опкод и т.п.

Далее вызывается `IProcessorFaultPolicy.ShouldContinue(fault)`; её ответ записывается в `IsRunning`. Политика **обязана** различать «настоящие ошибки» и «штатные» `EndProgramm`/`HostStopped`/`InfinityLoop`.

---

## 9. Исполнение шага (`Processor.Step`)

Псевдокод одного шага:

```
1. Если _hasCommand:
     a. Обработать все внешние команды (_externalCommands).
     b. Обработать все внешние статусы (_statusFromSimulation):
        - при ненулевом статусе — сформировать ProcessorFault и опросить политику.
     c. _hasCommand = false.
2. Если !IsRunning || IsSleeping → выход.
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
- Два регистра: `MOV`, `ADD`, `SUB`, `AND`, `OR`, `XOR`, `IN`, `OUT`, `SHR`, `SHL`, `DIV`, `MULT_INT`, `MOD`, `CMP`, `TEST`.
- `LDI reg, imm`: `imm` — число (`0x…` или десятичное) или метка.
- `LOAD[.size] reg, addr`, `STORE[.size] addr, reg`: `addr` — число или метка.
- `LOAD_IND[.size] reg, reg`, `STORE_IND[.size] reg, reg` — аналогично.
- `LOAD_UNSAFE[.size]`, `STORE_UNSAFE[.size]`, `LOAD_IND_UNSAFE[.size]`, `STORE_IND_UNSAFE[.size]` — аналогично без проверок.
- Переходы: `JMP`, `JZ`, `JNZ`, `JG`, `JL`, `CALL` — метка или абсолютный адрес.

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

---

## 12. Ограничения и известные особенности реализации

1. **`JMP`/`JZ`/`JNZ`/`JG`/`JL` не проверяют границы** целевого адреса (в отличие от `JMP_IND`/`CALL_IND`). Прыжок за пределы ОЗУ приводит к UB на следующем шаге (`ReadInt32LEUnSafe`).
2. **`PUSH` пишет в память до проверки коллизии с кучей** (`rHP`), поэтому при `SegmentationFault` запись остаётся в памяти, но `rSP` не меняется.
3. **Проверка границ в `MemoryBus`**: `address + N <= Length` реализована как `address <= Length - N`; при `Length < N` возможен `underflow` (в эталоне исключён, т.к. `Length ≥ 64`).
4. **Чтение immediate использует `ReadInt64LEUnSafe`**, т.е. unaligned. На платформах, где unaligned-чтение не поддерживается аппаратно, требуется 8-байтное выравнивание адреса загрузки программы.
5. **`WAKE` как инструкция в теле — no-op.** Пробуждение из `HALT` возможно только через `EnqueueExternalCommand(OpCode.WAKE)` или `PortBus.WakeProcessor`.
6. **`END` не гарантирует остановку**: `IsRunning = false` выставляется, но затем перезаписывается ответом `IProcessorFaultPolicy.ShouldContinue(...)`. Политика должна вернуть `false` для `EndProgramm`.
7. **`InstructionLOAD`/`InstructionSTORE` не возвращают `AlignmentFault` для невалидного `OpCodeSize`** (там `default` → `SegmentationFault` у LOAD и `AlignmentFault` у STORE); на практике `OpCodeSize` имеет ровно 4 значения — `default` недостижим.
8. **`HasNeed64IntData`** устойчив к смене базового типа `OpCode` на знаковый: проверка `(uint)opCode > (uint)OpCode.MOD`, затем индексация в `_has[0..42]`.
9. **`OpCode.NOP` = 0 используется как маркер «неизвестного опкода»** в `ProcessorFault` (`ProcessSimulationStatusSlow`). Это спорное соглашение; при появлении реального NOP-сбоя отличить его от «пустого» значения будет невозможно.

---

## 13. Быстрая шпаргалка по соглашениям

| Соглашение | Значение |
|------------|----------|
| Порядок байт | little-endian везде |
| Формат слова | `[7:0]=op, [12:8]=reg1, [17:13]=reg2, [19:18]=size, [31:20]=0` |
| Immediate | всегда 64 бита, 8-байтное выравнивание после инструкции |
| Длина инструкции | 4 или 12 байт |
| Регистры | 32 × 64 бита, `rIP` — адрес следующей инструкции |
| Флаги | обновляются только `CMP` и `TEST` |
| Стек | 8-байтные слоты, `rSP` растёт вниз, `rHP` — верхняя граница кучи |
| Ошибка доступа | `AlignmentFault` / `SegmentationFault`, не исключения |
| Политика | `IProcessorFaultPolicy.ShouldContinue` — последняя инстанция |