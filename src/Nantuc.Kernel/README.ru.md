# Nantuc.Kernel — README

> Ядро виртуальной машины Nantuc: процессор, ОЗУ, шина портов, реестр устройств и эмулятор.  
> Единственный слой платформы, который использует `unsafe`, `NativeMemory`, `[InlineArray]` и `ConcurrentQueue`.  
> [← Корень](../README.ru.md), [Common](../../src/Nantuc.Common/README.ru.md), [Contracts](../../src/Nantuc.Contracts/README.ru.md)

## Содержание
- [Об этом модуле](#об-этом-модуле)
- [Дерево локального проекта](#дерево-локального-проекта)
- [API](#api)
- [Примеры для API](#примеры-для-api)
- [Приближенность модуля к пользователю](#приближенность-модуля-к-пользователю)
- [Особенная информация](#особенная-информация)
- [Что исправить](#что-исправить)

## Об этом модуле

`Nantuc.Kernel` — это слой исполнения. Здесь живут: процессор (`Processor` + `ProcessorSnapshot`), управляемая обёртка над нативной ОЗУ (`MemoryBus` + `NativeMemoryBuffer` + `NativeReadOnlyView`), шина портов (`PortBus`), реестр устройств (`ManagerDevices`), а также «вычислительное устройство» (`Device`), владеющее ОЗУ и процессором и реализующее `IPortController` из `Contracts`.

Модуль — единственный в платформе, где разрешён `unsafe` (`<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` в `Nantuc.csproj`). Всё нативное — в `NativeMemoryBuffer`: `NativeMemory.AllocZeroed`, `NativeMemory.Free`, `byte*`, `Unsafe.ReadUnaligned` / `Unsafe.WriteUnaligned`. Остальной код работает с управляемыми обёртками.

Ключевые роли модуля:
- ОЗУ: `MemoryBus` (проверяемые/непроверяемые типизированные операции), `NativeMemoryBuffer` (сам нативный буфер), `NativeReadOnlyView` (read-only представление с проверками).
- Процессор: `Processor` — `Step()`, декодирование через `InstructionDecoder` из `Common`, регистры `[InlineArray(32)]`, флаги `rFL`, очереди внешних команд/статусов. `ProcessorSnapshot` — снимок состояния для внешних наблюдателей.
- Шина и устройства: `PortBus`, `ManagerDevices`, `Device`.
- Интеграция: реализует `IPortBus`, `IPortController`, `IReadOnlyNativeBuffer`, `ProcessorSnapshot` из `Contracts`.

Модуль зависит от `Common` (типы ISA) и `Contracts` (интерфейсы). Не зависит от `VmApplication`/`Extensions` — стрелка зависимостей всегда внутрь.

> **XML-документация полная на публичной поверхности.** Все публичные типы, методы и параметры документированы. Оставшиеся дефекты — не XML, а поведение: порядок `Volatile.Write` в `Processor.EnqueueBiosStatus` (`_hasCommand` пишется раньше `_hasSimulationStatus` — статус может «залипнуть» в очереди до следующего `Enqueue`/`Reset`), отсутствие `ObjectDisposedException` на `MemoryBus.ReadInt*LE` после `Dispose`, порядок проверки стека в `InstructionPUSH`, `CurrentProcessorSnapshot` как property создаёт новый объект на каждое обращение, блокирующий `_wakeSignal.Wait(...)` на single-threaded WASM. Подробности — в разделе [Что исправить](#что-исправить).

## Дерево локального проекта

Namespace-ы по слоям, файлы плоские внутри них:

```
Nantuc.Kernel/
├── Nantuc.csproj               # net10.0, AllowUnsafeBlocks=true, refs Common+Contracts
├── RamSystem/
│   ├── MemoryBus.cs                   # проверяемая/непроверяемая шина ОЗУ
│   ├── NativeMemoryBuffer.cs          # unsafe, NativeMemory, byte*, ReadUnaligned
│   └── NativeReadOnlyView.cs          # IReadOnlyNativeBuffer, проверки границ/Dispose
├── ProcessorSystem/
│   ├── Processor.cs                       (~150) — поля, ctor, публичный API
│   ├── Processor.Step.cs                  (~110) — главный switch
│   ├── Processor.Arithmetic.cs            (~110) — ADD/SUB/INC/DEC/MULT/SHR/SHL/DIV/MOD/AND/OR/XOR/NOT/CASTZ/CASTS
│   ├── Processor.ControlFlow.cs           (~130) — JMP/JZ/JNZ/JG/JL/CALL/RET/JMP_IND/CALL_IND/END/HALT
│   ├── Processor.Stack.cs                 (~70)  — PUSH/POP
│   ├── Processor.Memory.cs                (~220) — LOAD/STORE (+unsafe) + Body + CanRead64/Get64
│   ├── Processor.Ports.cs                 (~50)  — IN/OUT/WAKE_INT/WAKE
│   ├── Processor.Flags.cs                 (~70)  — CMP/TEST/SetFlags/Zero/Negative
│   ├── Processor.Interrupts.cs            (~70)  — ProcessExternal/ProcessSimulation/ExecuteExternal
│   ├── Processor.Faults.cs                (~30)  — RecordFaultContext/IsTerminal
│   ├── Processor.Snapshot.cs              (~50)  — CurrentProcessorSnapshot
│   └── InstructionHelpers.cs              (~30)  — MaskOf/SignExtend (новый static-класс)
├── ControllersData/
│   └── PortBus.cs                     # секторная шина портов
├── BiosSystem/
│   └── Device.cs                      # IPortController: MemoryBus + Processor + ManualResetEventSlim
└── Utilities/
    └── ManagerDevices.cs              # реестр устройств, переиспользование слотов
```

Замечания по организации:
- Namespace `Utilities` (с опечаткой — должно быть `Utilities`) содержит `ManagerDevices`.
- `Processor` — `sealed`, использует `[InlineArray(CountReg)]` (32 × `ulong`) вместо массива: компактнее по GC, AOT-friendly.
- `NativeMemoryBuffer` — `unsafe sealed class`; всё нативное — только здесь. Остальные классы работают через `MemoryBus`/`IReadOnlyNativeBuffer`.
- `Device` реализует `IPortController`, а не просто `IPortUse`: у него есть `_ioMemory` и `_wakeSignal`, которые нужно освобождать.
- `ManagerDevices` — точка входа для потребителя
- `Processor.CurrentProcessorSnapshot` — **свойство** (не метод), создаёт **новый** `ProcessorSnapshot` при каждом обращении (не переиспользуемый экземпляр).

## API

### Общая картина

Публичная поверхность по namespace:

| Тип | Namespace | Вид | Назначение |
|---|---|---|---|
| `MemoryBus` | `RamSystem` | `class : IDisposable` | Проверяемые `ReadInt*LE`/`WriteInt*LE`, `*UnSafe`-варианты, `UpdateAndReadStatus`, `FaultData`, `NativeReadOnlyBuffer` |
| `NativeMemoryBuffer` | `RamSystem` | `unsafe sealed class : IDisposable` | `NativeMemory.AllocZeroed`, `ReadUInt*`/`WriteUInt*`, индексатор, `Clear`, `LoadProgram`, `IsDisposed` |
| `NativeReadOnlyView` | `RamSystem` | `class : IReadOnlyNativeBuffer` | Проверки `ObjectDisposedException` + `ArgumentOutOfRangeException` при индексации |
| `Processor` | `ProcessorSystem` | `sealed class` | `Step()`, `LaunchProgramm`, `Reset`, очереди `EnqueueBiosStatus`/`EnqueueExternalCommand`, `GetDiagnosticData`, `CopyRegisters`, `CurrentProcessorSnapshot` (property) |
| `ProcessorSnapshot` | `ProcessorSystem` | `readonly struct` | Снимок состояния: `IsRunning`, `IsSleeping`, копия 32 регистров, `GetRegister(RegType)`, текущая инструкция и `imm64` |
| `PortBus` | `ControllersData` | `sealed class : IPortBus` | Секторная шина: `RegisterDevice`, `ReadPort`, `WritePort`, `WakeProcessor`, `IsFreeSector` |
| `Device` | `BiosSystem` | `sealed class : IPortController` | `Run`, `DebugRun`, `StepDebug`,`Step`, `BatchStep`, `BatchStepDebug`, `LoadProgram`, `TryLoadProgram`, `Stop`, `WakeProcessor`, `EnqueueExternalCommand`, `GetLastFault` |
| `ManagerDevices` | `Utilities` | `class : IDisposable` | `TryAddDevice`, `RemoveDevice`, `ChangeSector`, `GetDevice`, `TryGetDevice`, `GetAllDevices`, `Count`, `CreateDevice`|

### `MemoryBus` — ОЗУ

Проверяемые методы **не бросают**: при ошибке возвращают `0` и сохраняют статус, который читается через `UpdateAndReadStatus()` (и сбрасывает в `Success`). `*UnSafe`-варианты — без проверок границ и выравнивания.

| Метод | Что делает | Проверки |
|---|---|---|
| `ReadInt8LE(ulong)` | читает байт | только верхняя граница |
| `ReadInt16LE`/`ReadInt32LE`/`ReadInt64LE` | читает LE-значение | выравнивание по 2/4/8 + граница |
| `WriteInt8LE`/`WriteInt16LE`/`WriteInt32LE`/`WriteInt64LE` | пишет LE-значение | те же |
| `ReadInt8LEUnSafe`/`WriteInt8LEUnSafe`… | без проверок | нет |
| `UpdateAndReadStatus()` | возвращает и сбрасывает `_lastStatus` | — |
| `LoadProgram(ReadOnlySpan<byte>, ulong)` | копирует программу в ОЗУ | `ArgumentOutOfRangeException` при выходе за границы |
| `ClearMemory()` | зануляет буфер | — |

**Гонка статуса:** `_lastStatus` и `_faultData` — не `volatile`, не защищены блокировкой. `UpdateAndReadStatus` сбрасывает статус — потребитель обязан вызывать его после каждой проверяемой операции, иначе ошибка «залипнет».

### `NativeMemoryBuffer` — нативный буфер

Единственный `unsafe`-класс. После `Dispose` все `Read*`/`Write*`/индексатор — **UB**: `_ptrRam` станет `null`, а `*(_ptrRam + index)` — NRE или AV. Защита есть только в `NativeReadOnlyView` (`ObjectDisposedException.ThrowIf(buffer.IsDisposed, buffer)`).

- `NativeMemory.AllocZeroed(nuint)` — выделяет занулённую память; при неудаче — `OutOfMemoryException` (уже, не `NativeMemory`-уровневая ошибка).
- `ReadUInt16/32/64` — `Unsafe.ReadUnaligned<T>` + `BitConverter.IsLittleEndian` (на WASM `IsLittleEndian == true`, поэтому `ReverseEndianness` никогда не выполняется).
- `this[ulong index]` — `*(_ptrRam + index)`, без проверок.
- `LoadProgram(ReadOnlySpan<byte>, ulong)` — `NativeMemory.Copy(src, _ptrRam + startProg, (nuint)prog.Length)`; проверяет только `prog.Length > 0` и границы.
- `Dispose` — `Volatile.Read/Write` на `bool`; **не потокобезопасен относительно повторных вызовов** (см. [Что исправить](#что-исправить)).

### `NativeReadOnlyView` — read-only представление

`IReadOnlyNativeBuffer` из `Contracts`. При индексации проверяет `IsDisposed` и `index < Length` — **единственное** место, где после `Dispose` выбрасывается `ObjectDisposedException`, а не UB.

### `Processor` — процессор

| Метод / свойство | Назначение |
|---|---|
| `Step()` | обрабатывает очереди (если `Volatile.Read(_hasCommand)`), читает 32-битное слово по `rIP`, увеличивает `rIP` на 4, вызывает обработчик опкода |
| `LaunchProgramm(ulong)` | сбрасывает `rFL`, выставляет `rIP`, `rSP` (вершина ОЗУ, выровненная по 8), `IsRunning = true` |
| `Reset()` | обнуляет регистры, очищает очереди, сбрасывает флаги (`_faultData` **не сбрасывается**) |
| `EnqueueBiosStatus(BiosStatus)` | ставит статус в `_statusFromSimulation`; **сначала** `Volatile.Write(_hasCommand, true)`, **потом** `Volatile.Write(_hasSimulationStatus, true)` — потенциальная потеря сигнала (см. [Что исправить](#что-исправить)) |
| `EnqueueExternalCommand(uint)` | ставит сырое слово в `_externalCommands`; **сначала** `Volatile.Write(_hasExternalCommand, true)`, **потом** `Volatile.Write(_hasCommand, true)` |
| `GetDiagnosticData()` | снимок `ProcessorFault` |
| `GetRegValue(RegType)` | одно значение |
| `CopyRegisters(Span<ulong>)` | копирует 32 регистра; бросает `ArgumentException`, если буфер < 32 |
| `CurrentProcessorSnapshot` (property) | **новый** `ProcessorSnapshot` при каждом обращении: копия регистров, `IsRunning`, `IsSleeping`, текущая инструкция и `imm64` (если нужен) |
| `InitRegHP(ulong)` | устанавливает `rHP` |
| `IsRunning`/`IsSleeping` | флаги |

Регистры — `[InlineArray(CountReg)]` из `ulong` (32 × 8 байт = 256 байт на экземпляр). `rFL` — битовое поле: бит 0 = Zero, бит 1 = Negative; устанавливается **только** `CMP`/`TEST` (см. `<remarks>` `OpCode`).

`Step()` — `switch`-выражение по всем ~60 опкодам, с `[MethodImpl(AggressiveOptimization)]`. Обработчик `Instruction*` возвращает `BiosStatus`; при `!= Success` — заполняет `_faultData`, вызывает `_processorPolicy.ShouldContinue` и `Volatile.Write(_isRunning, ...)`.

**Непосредственный 64-битный операнд:** `Get64IntData()` читает `rIP`, выравнивает вверх по 8 (`AlignUp(8)`), читает `ulong` через `_ram.ReadInt64LEUnSafe`, затем **присваивает `rIP = aligned_ip + 8`** (а не `rIP += 8`). То есть после 4-байтной инструкции до 4 байт паддинга, а immediate начинается с ближайшей 8-байтной границы; новый `rIP` = `AlignUp(old_rIP, 8) + 8`.

### `ProcessorSnapshot` — снимок состояния

Создаётся **свойством** `Processor.CurrentProcessorSnapshot`. Содержит `IsRunning`, `IsSleeping` и копию 32 регистров. Имеет `GetRegister(RegType)`. Так как снимок — независимая копия, его можно безопасно сохранять; при следующем шаге состояние не изменится.

### `PortBus` — секторная шина портов

- Адрес раскладывается на «сектор» (`address >> _deviceShift`) и «смещение» (`address & _offsetMask`).
- `RegisterDevice(IPortUse, uint)` — `IPortUse`, не `IPortController` (последний расширяет `IPortUse`).
- `ReadPort` возвращает `ReadPortResult(Data, Status)`; `Status = NullDeviceInput`, если по адресу нет устройства.
- `WritePort` возвращает `BiosStatus`: `Success` или `NullDeviceOutput`.
- `WakeProcessor` — `bool` (найдено ли устройство).
- `RegisterDevice`/`UnregisterDevice` — прямое управление массивом `_devices`; **потокобезопасности нет** (см. `<remarks>` типа).

### `Device` — вычислительное устройство

Собственные `MemoryBus` + `Processor` + `byte[] _ioMemory` + `ManualResetEventSlim _wakeSignal`. Реализует `IPortController` — может быть зарегистрировано на `PortBus` через `ManagerDevices.TryAddDevice`.

| Метод | Назначение |
|---|---|
| `Run(ulong, long?, bool, Action?, Action<TimeSimulationResult>?)` | блокирующий запуск до завершения/сбоя; `limit` — лимит шагов -> `InfinityLoop` |
| `DebugRun(ulong, long, int, Action?, Action<ProcessorSnapshot>?, Action?)` | запуск с задержкой и колбэком на шаг |
| `BreakPointerLaunchDevice(ulong?, Action?)` | инициализация без запуска цикла; `start ?? 0` — если `null`, `rIP` выставляется в `0` (не «с конца ОЗУ» — конец ОЗУ идёт только в `rSP` в `LaunchProgramm`) |
| `StepDebug(Action<ProcessorSnapshot>?)` / `BatchStepDebug(int, Action<ProcessorSnapshot>?)` | ручные шаги; читают `Processor.CurrentProcessorSnapshot` (property) |
| `Step()` / `BatchStep(int)` | ручные шаги |
| `LoadProgram(ReadOnlySpan<byte>, ulong)` / `TryLoadProgram` | загрузка + инициализация `rHP` (`(loadAddress + programLength).AlignUp(8)`) |
| `Stop()` / `WakeProcessor()` / `EnqueueExternalCommand(uint)` | с другого потока: `Enqueue*` + `_wakeSignal.Set()` |
| `CopyRegisters` / `GetLastFault` / `ResetMemoryRam` / `ClearRegisters` | снимки |
| `ReadPort` / `WritePort` | **без проверки границ** — возвращают `0`/игнорируют при выходе за `_ioMemory.Length` (в `<remarks>` типа — «порты ввода-вывода», реализация не бросает) |

`Stop`/`WakeProcessor`/`EnqueueExternalCommand` — единственные методы, безопасные из другого потока (используют `Enqueue*` + `_wakeSignal.Set()`). `Run`/`DebugRun`/`NextStep*` — нет.

### `ManagerDevices` — реестр
- `CreateDevice(RamSize, IProcessorPolicy)` — новый `Device`.
- `TryAddDevice` — `IdState.Valid` при успехе; иначе `NotAssigned` (реестр заполнен), `OutOfRange` (сектор занят), `NoFreeSlot` (нет слота и ID), `Rejected` (`PortBus.RegisterDevice` вернул `false`, устройство **dispose'ится**). `out int id` при отказе получает `-1`/`-2`/`-3`/`-4`.
- Слоты (`DeviceEntry`) переиспользуются: `IsFreed = true` сохраняет `Id`, чтобы при повторной регистрации `Id` оставался прежним.
- `Count` — O(n) каждый раз (не кэшируется); вызывается в `TryAddDevice` на каждом добавлении.
- `Dispose` **не снимает** устройства с `PortBus` — `_portBus._devices[sector]` продолжает ссылаться на disposed-контроллер (см. [Что исправить](#что-исправить)).
- `GetDevice(int)` / `TryGetDevice<T>(int, out T?)` — по `Id`.
- Конструктор —`MangerDevices(PortSize ports, DevicePortSize portsOnDev)` создаёт `PortBus`.

## Примеры для API

**Минимальный запуск:**

```csharp
using Nantuc.Common;
using Nantuc.Contracts;
using Nantuc.Kernel.Utilities;

using var manager = new ManagerDevices(PortSize.KB64, DevicePortSize.B64);

using Device device = manager.CreateDevice(RamSize.MB16, IProcessorPolicy.Default);

byte[] program = [/* байты: 32-битные слова инструкций */];
device.LoadProgram(program, loadAddress: 0x0);

ProcessorFault fault = device.Run(
    start: 0,
    limit: 10_000,
    snowTimer: true,
    onLaunch: () => Console.WriteLine("start"),
    onEnd: r => Console.WriteLine($"steps={r.Steps}, elapsed={r.Elapsed}"));

Console.WriteLine($"{fault.Status} @ 0x{fault.InstructionPointer:X}");
```

**Пошаговое выполнение в режиме отладки:**

```csharp
device.BreakPointerLaunchDevice(start: 0, titleAct: null);

device.StepDebug(step => Console.WriteLine($"IP=0x{step.GetRegister(RegType.rIP):X}"));
device.BatchStepDebug(3, onStep: null);
```
**Быстрое пошаговое выполнение:**

```csharp
device.BreakPointerLaunchDevice(start: 0, titleAct: null);

device.Step();
device.BatchStep(3);
```

**Регистрация внешнего устройства:**

```csharp
public sealed class MyDevice : IPortController
{
    public byte ReadPort(ulong offset) => 0;
    public void WritePort(ulong offset, byte value) { /* ... */ }
    public void WakeProcessor() { /* ... */ }
    public void Dispose() { /* ... */ }
}

IdState state = emulator.RegisterDevice(new MyDevice(), "my-dev", sector: 0, out int id);
if (state != IdState.Valid)
    Console.Error.WriteLine($"register failed: {state} (id={id})");
```

**Прямое чтение ОЗУ:**

```csharp
IReadOnlyNativeBuffer ram = device.NativeReadOnlyBuffer;
nuint n = (nuint)Math.Min((ulong)ram.Length, 64UL);
for (nuint i = 0; i < n; i++)
    Console.Write($"{ram[i]:X2} ");

// !! после device.Dispose() индексация бросит ObjectDisposedException
```

**Проверяемое чтение через MemoryBus (внутри своего обработчика):**

```csharp
ulong addr = 0x10;
uint value = memoryBus.ReadInt32LE(addr);
BiosStatus st = memoryBus.UpdateAndReadStatus();
if (st != BiosStatus.Success)
    Console.Error.WriteLine($"{st} @ 0x{memoryBus.FaultData:X}");
```

## Приближенность модуля к пользователю

Средняя (выше, чем `Common`/`Contracts`, ниже, чем `VmApplication`).

- Автор расширений устройств работает с `ManagerDevices`/`Device`/`PortBus` — это его основной API.
- Пользователь `VmApplication` этот слой напрямую не импортирует, но именно `Device.Run`/`DebugRun` вызываются из `VmApplication`.
- Автор компилятора не работает с этим слоем — только с `Common`/`Contracts`.

**Что стоит делать:**
- Использовать `ManagerDevices.CreateDevice` + `Device.Run`, а не создавать `Device`/`Processor`/`MemoryBus` вручную.
- После каждой проверяемой операции `MemoryBus` — вызывать `UpdateAndReadStatus()`; статус сбрасывается.
- Помнить, что `ReadPort`/`WritePort` в `Device` **не бросают** — выход за границы даёт `0`/игнор, а не исключение.
- Вызывать `Stop`/`WakeProcessor`/`EnqueueExternalCommand` из другого потока, а `Run`/`NextStep*` — только из одного потока-координатора.
- После `device.Dispose()` читать ОЗУ только через `NativeReadOnlyBuffer` — там будет `ObjectDisposedException`, а не UB.
- `Processor.CurrentProcessorSnapshot` (свойство) возвращает новый `ProcessorSnapshot`; его можно сохранять и использовать после следующего шага.

**Что не стоит делать:**
- Полагаться на `_lastStatus` между операциями без `UpdateAndReadStatus` — статус «залипнет».
- Читать/писать в `_ioMemory`/`NativeMemoryBuffer` напрямую из другого потока во время `Run`.
- Регистрировать/снимать устройства (`RegisterDevice`/`UnregisterDevice`/`ChangeDeviceSector`) во время работы `Device.Run` — синхронизации нет.
- Использовать `NativeMemoryBuffer` напрямую после `Dispose` — UB, не исключение (защищён только `NativeReadOnlyView`).
- Сохранять `IReadOnlyNativeBuffer`, полученный из `Device.NativeReadOnlyBuffer`, после `Dispose()` устройства — `ObjectDisposedException` при следующей индексации.
- Рассчитывать, что `GetDiagnosticData()` сбросится `Processor.Reset()`: `_faultData` **не сбрасывается** в `Reset()` — после сброса `GetDiagnosticData()` вернёт последний сбой предыдущего запуска.

## Особенная информация

### WASM / кроссплатформенность

| Аспект | Статус | Комментарий |
|---|---|---|
| Сборка в WASM | !!️ | Собирается под `browser-wasm` без правок csproj, но `Device.Run`/`DebugRun` — синхронные блокирующие методы. На single-threaded WASM они блокируют UI-поток. Требуется вынос в Web Worker или замена на async-цикл. |
| `NativeMemory` | !!️ | `NativeMemory.AllocZeroed`/`Free`/`Clear` работают на WASM (линейная память emscripten). Но аллокация 2 GiB (`RamSize.GB2`) в браузере почти наверняка бросит `OutOfMemoryException` — лимит линейной памяти WASM ~4 GiB на весь процесс, с фрагментацией. `MemoryBus`/`Device` не проверяют этот лимит. |
| `unsafe` / `byte*` | - | `NativeMemoryBuffer` — `unsafe`; WASM поддерживает указатели. `Unsafe.ReadUnaligned`/`WriteUnaligned` работают. |
| `stackalloc` / `InlineArray` | - | `stackalloc` не используется в горячих путях, кроме `Processor.CurrentProcessorSnapshot` (32 × 8 байт на каждое обращение). `[InlineArray(32)]` в `Processor` — compile-time, AOT-friendly. |
| `Interlocked` / `Volatile` | !!️ | `Interlocked.Exchange` в `MemoryBus.Dispose` и `Device.Dispose`, `Volatile.Read/Write` в `NativeMemoryBuffer.Dispose` и `Processor` — на single-threaded WASM (без `SharedArrayBuffer`) это no-op, но код работает. С threaded WASM — атомарно. |
| `ConcurrentQueue<T>` | - | Две очереди в `Processor` (`_externalCommands`, `_statusFromSimulation`). На single-threaded WASM — просто очередь; на threaded — lock-free. |
| `ManualResetEventSlim.Wait(...)` | !!️!!️ | В `Device.RunSimulationLoop`/`RunSimulationLoopWithLimit`/`RunSimulationLoopDebug` — `_wakeSignal.Wait(_processorPolicy.SleepWaitTimeout)` блокирует текущий поток при `IsSleeping`. В браузере — блокировка UI. Замена: `await Task.Delay` + async-цикл, либо Web Worker. |
| `Thread.Sleep(delay)` | !!️!!️ | `RunSimulationLoopDebug` — `Thread.Sleep(delay)`. В браузере — блокировка UI. |
| `Stopwatch` | - | `Run` при `snowTimer: true` — `Stopwatch.StartNew()`; на WASM работает через `performance.now`-подобный таймер. |
| `IFileService` | n/a | В этой партии файлов интерфейса нет. При появлении в других слоях WASM-реализация — отдельно. |
| `Regex` / trimming | - | Regex не используется; нет рефлексии, `Type.GetType`, `Activator.CreateInstance`, `dynamic`. Trimming-совместимо. |
| AOT | - | Нет `Expression.Compile`, `MakeGenericMethod`/`MakeGenericType`. `switch` по опкодам в `Step` — AOT-friendly. Generic-код — только `TryGetDevice<T>` (моноформируется под используемые `T`). |

**Итог:** код собирается под WASM и AOT, но **синхронная блокирующая модель исполнения** (`Run`, `Thread.Sleep`, `ManualResetEventSlim.Wait`) не подходит для browser-WASM в single-threaded режиме. `NativeMemory.AllocZeroed` работает, но большие `RamSize` (≥ `MB512`–`GB1`) рискуют `OutOfMemoryException`. Для production в браузере — Web Worker + async-цикл + RAM ≤ ~256 MiB.

### `Processor.Step` — порядок обработки

1. `Volatile.Read(ref _hasCommand)` — если `true`, **сначала** `Volatile.Write(ref _hasCommand, false)` (сброс до обработки, чтобы новые команды, пришедшие во время обработки, не потерялись), затем `Volatile.Read(ref _hasExternalCommand)` -> `ProcessExternalCommandsSlow`, `Volatile.Read(ref _hasSimulationStatus)` -> `ProcessSimulationStatusSlow`.
2. Если `!_isRunning | _isSleeping` — выход (обратите внимание: `|`, а не `||`).
3. `ip = rIP`; `rawInst = _ram.ReadInt32LEUnSafe(ip)`; `rIP += 4` (до вызова обработчика — обработчик видит уже следующее слово).
4. `switch` по опкоду -> `BiosStatus`.
5. Если `dat != Success` — `_faultData = (dat, _lastFaultData, ip, opCode)`, `Volatile.Write(ref _isRunning, !IsTerminal(dat) && _processorPolicy.ShouldContinue(_faultData))`.

**Непосредственные операнды:** `Get64IntData()` читает `rIP`, выравнивает вверх по 8 (`AlignUp(8)`), читает `ulong` через `_ram.ReadInt64LEUnSafe`, затем **присваивает `rIP = aligned_ip + 8`** — вызывается обработчиками `LOAD`/`STORE`/`LDI`/`JMP`/`JZ`/`JNZ`/`JG`/`JL`/`CALL` и их `*_UNSAFE`-вариантов. `LOAD_IND`/`STORE_IND`/`CALL_IND`/`JMP_IND` и их `*_UNSAFE` — indirect через регистр, `Get64IntData` не вызывают.

`CanRead64IntDataInst()` проверяет `AlignUp(rIP, 8) > _ram.Length - 8`; при `_ram.Length < 8` возможен underflow (проверка формально проходит, чтение выйдет за границы — реального срабатывания не будет только если `_ram.Length ≥ 8`).

### Флаги `rFL` и переходы

`ZeroFlagSet` — `(rFL & 1) != 0`; `NegativeFlagSet` — `(rFL & 2) != 0`. `CMP`/`TEST` устанавливают `zeroFlag | (negativeFlag << 1)` через битхаки. `JG` проверяет `!Negative & !Zero`, `JL` — только `Negative`, `JZ`/`JNZ` — `Zero`/`!Zero`. `rFL` сбрасывается в `0` на `LaunchProgramm`.

### Стек и куча

`rSP` инициализируется в `LaunchProgramm` как `_ram.Length & ~0x7UL`; `rHP` — в `Device.InitHeap` как `(loadAddress + programLength).AlignUp(8)`. `PUSH`/`CALL` уменьшают `rSP` на 8 и проверяют коллизию `rHP >= sp`; `POP`/`RET` увеличивают на 8. `RET` проверяет `sp >= _stackBase` (пустой стек -> `StackUnderflow`). При `rHP = 0` (например, `BreakPointerLaunchDevice` без `LoadProgram`) проверка `rHP >= sp` пропускает всё, кроме `sp == 0`.

### Потокобезопасность

- `Processor.Step`/`LaunchProgramm`/`Reset` — не потокобезопасны.
- `EnqueueBiosStatus`/`EnqueueExternalCommand` — безопасны из другого потока (очередь + `Volatile.Write`), но `EnqueueBiosStatus` имеет инвертированный порядок записи флагов (см. [Что исправить](#что-исправить)).
- `Device.Stop`/`WakeProcessor`/`EnqueueExternalCommand` — безопасны (делегируют в `Processor` + `_wakeSignal.Set()`).
- `PortBus.RegisterDevice`/`UnregisterDevice` — не потокобезопасны.
- `ManagerDevices` — не потокобезопасен (см. `<remarks>`).
- `MemoryBus` — не потокобезопасен (`_lastStatus`/`_faultData` без синхронизации).
- `NativeMemoryBuffer.Dispose` — `Volatile.Read/Write` на `bool`, но check-then-act не атомарен; при одновременном `Dispose` из двух потоков возможен double-free.
- `Processor.CurrentProcessorSnapshot` — не потокобезопасен (читает регистры без синхронизации с `Step`); сам `_faultData` тоже читается без `Volatile`, поэтому снимок может быть слегка «размазан» относительно последнего шага.

### Ограничения платформ

- Целевой framework: `net10.0`; `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`.
- `unsafe` — только в `NativeMemoryBuffer`; `MemoryBus`/`PortBus`/`Processor`/`Device` — managed.
- `[MethodImpl(AggressiveInlining)]`/`[AggressiveOptimization]` — оптимизация под JIT; в AOT/WASM инлайнер ведёт себя иначе.
- `Nullable=enable`, `ImplicitUsings=enable`.

### Что этот модуль не делает

- Не содержит реализации `IFileService` — только работает с ОЗУ и портами.
- Не предоставляет async API — всё синхронное (`Run`, `NextStep*`).
- Не управляет жизненным циклом процесса — это `VmApplication`.
- Не парсит байткод `.bin`/`.nanas` — только исполняет 32-битные слова через `Processor.Step`.
