# Nantuc.Kernel — README

> Ядро виртуальной машины Nantuc: процессор, ОЗУ, шина портов, реестр устройств и эмулятор.  
> Единственный слой платформы, который использует `unsafe`, `NativeMemory`, `[InlineArray]` и `ConcurrentQueue`.  
> `[← Корень](../README.ru.md) · [Common](../../src/Nantuc.Common/README.ru.md) · [Contracts](../../src/Nantuc.Contracts/README.ru.md)`

## Содержание
- [Об этом модуле](#об-этом-модуле)
- [Дерево локального проекта](#дерево-локального-проекта)
- [API](#api)
- [Примеры для API](#примеры-для-api)
- [Приближенность модуля к пользователю](#приближенность-модуля-к-пользователю)
- [Особенная информация](#особенная-информация)
- [Что исправить](#что-исправить)

## Об этом модуле

`Nantuc.Kernel` — это слой исполнения. Здесь живут: процессор (`Processor` + `ProcessorSnapshot`), управляемая обёртка над нативной ОЗУ (`MemoryBus` + `NativeMemoryBuffer` + `NativeReadOnlyView`), шина портов (`PortBus`), реестр устройств (`ManagerDevices`) и эмулятор (`Emulator`), а также «вычислительное устройство» (`Device`), владеющее ОЗУ и процессором и реализующее `IPortController` из `Contracts`.

Модуль — единственный в платформе, где разрешён `unsafe` (`<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` в `Nantuc.csproj`). Всё нативное — в `NativeMemoryBuffer`: `NativeMemory.AllocZeroed`, `NativeMemory.Free`, `byte*`, `Unsafe.ReadUnaligned` / `Unsafe.WriteUnaligned`. Остальной код работает с управляемыми обёртками.

Ключевые роли модуля:
- ОЗУ: `MemoryBus` (проверяемые/непроверяемые типизированные операции), `NativeMemoryBuffer` (сам нативный буфер), `NativeReadOnlyView` (read-only представление с проверками).
- Процессор: `Processor` — `Step()`, декодирование через `InstructionDecoder` из `Common`, регистры `[InlineArray(32)]`, флаги `rFL`, очереди внешних команд/статусов. `ProcessorSnapshot` — снимок состояния для внешних наблюдателей.
- Шина и устройства: `PortBus`, `ManagerDevices`, `Device`, `Emulator`.
- Интеграция: реализует `IPortBus`, `IPortController`, `IReadOnlyNativeBuffer`, `ProcessorSnapshot` из `Contracts`.

Модуль зависит от `Common` (типы ISA) и `Contracts` (интерфейсы). Не зависит от `VmApplication`/`Extensions` — стрелка зависимостей всегда внутрь.

> **XML-документация полная на публичной поверхности.** Все публичные типы, методы и параметры документированы. Оставшиеся дефекты — не XML, а поведение: гонка `_hasCommand` в `Processor.Step`, отсутствие `ObjectDisposedException` на `MemoryBus.ReadInt*LE` после `Dispose`, порядок проверки стека в `InstructionPUSH`, блокирующий `_wakeSignal.Wait(15)` на single-threaded WASM. Подробности — в разделе [Что исправить](#что-исправить).

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
│   ├── Processor.cs                   # Step(), [InlineArray(32)], ConcurrentQueue, Volatile
│   └── ProcessorSnapshot.cs             # снимок состояния процессора для внешних наблюдателей
├── ControllersData/
│   └── PortBus.cs                     # секторная шина портов
├── BiosSystem/
│   └── Device.cs                      # IPortController: MemoryBus + Processor + ManualResetEventSlim
└── Utilites/
    ├── Emulator.cs                    # PortBus + ManagerDevices, CreateDevice/RegisterDevice
    └── ManagerDevices.cs              # реестр устройств, переиспользование слотов
```

Замечания по организации:
- Namespace `Utilites` (с опечаткой — должно быть `Utilities`) содержит `Emulator` и `ManagerDevices`.
- `Processor` — `sealed`, использует `[InlineArray(CountReg)]` (32 × `ulong`) вместо массива: компактнее по GC, AOT-friendly.
- `NativeMemoryBuffer` — `unsafe sealed class`; всё нативное — только здесь. Остальные классы работают через `MemoryBus`/`IReadOnlyNativeBuffer`.
- `Device` реализует `IPortController`, а не просто `IPortUse`: у него есть `_ioMemory` и `_wakeSignal`, которые нужно освобождать.
- `Emulator` — точка входа для потребителя; `ManagerDevices` — деталь реализации (публичный, но нужен в основном `Emulator`).
- `Processor.GetCurrentProcessorSnapshot` создаёт **новый** `ProcessorSnapshot` при каждом вызове (не переиспользуемый экземпляр).

## API

### Общая картина

Публичная поверхность по namespace:

| Тип | Namespace | Вид | Назначение |
|---|---|---|---|
| `MemoryBus` | `RamSystem` | `class : IDisposable` | Проверяемые `ReadInt*LE`/`WriteInt*LE`, `*UnSafe`-варианты, `UpdateAndReadStatus`, `FaultData`, `NativeReadOnlyBuffer` |
| `NativeMemoryBuffer` | `RamSystem` | `unsafe sealed class : IDisposable` | `NativeMemory.AllocZeroed`, `ReadUInt*`/`WriteUInt*`, индексатор, `Clear`, `LoadProgram`, `IsDisposed` |
| `NativeReadOnlyView` | `RamSystem` | `class : IReadOnlyNativeBuffer` | Проверки `ObjectDisposedException` + `ArgumentOutOfRangeException` при индексации |
| `Processor` | `ProcessorSystem` | `sealed class` | `Step()`, `LaunchProgramm`, `Reset`, очереди `EnqueueBiosStatus`/`EnqueueExternalCommand`, `GetDiagnosticData`, `GetRegistersSnapshot`, `CopyRegisters`, `GetCurrentProcessorSnapshot` |
| `ProcessorSnapshot` | `ProcessorSystem` | `readonly struct : ProcessorSnapshot` | Снимок состояния: `IsRunning`, `IsSleeping`, копия 32 регистров, `GetRegister(RegType)`, текущая инструкция и `imm64`|
| `PortBus` | `ControllersData` | `sealed class : IPortBus` | Секторная шина: `RegisterDevice`, `ReadPort`, `WritePort`, `WakeProcessor`, `IsFreeSector` |
| `Device` | `BiosSystem` | `sealed class : IPortController` | `Run`, `DebugRun`, `NextStepProcessor*`, `LoadProgram`, `TryLoadProgram`, `Stop`, `WakeProcessor`, `EnqueueExternalCommand`, `GetLastFault` |
| `Emulator` | `Utilites` | `class : IDisposable` | `CreateDevice`, `RegisterDevice`, `GetDevice`, `TryGetDevice`, `UnregisterDevice`, `ChangeDeviceSector`, `AllDeviceInfo` |
| `ManagerDevices` | `Utilites` | `class : IDisposable` | `TryAddDevice`, `RemoveDevice`, `ChangeSector`, `GetDevice`, `TryGetDevice`, `GetAllDevices`, `Count` |

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

| Метод | Назначение |
|---|---|
| `Step()` | обрабатывает очереди (если `_hasCommand`), читает 32-битное слово по `rIP`, увеличивает `rIP` на 4, вызывает обработчик опкода |
| `LaunchProgramm(ulong)` | сбрасывает `rFL`, выставляет `rIP`, `rSP` (вершина ОЗУ, выровненная по 8), `IsRunning = true` |
| `Reset()` | обнуляет регистры, очищает очереди, сбрасывает флаги |
| `EnqueueBiosStatus(BiosStatus)` | ставит статус в `_statusFromSimulation` + `Volatile.Write(_hasSimulationStatus, true)` + `Volatile.Write(_hasCommand, true)` |
| `EnqueueExternalCommand(uint)` | ставит сырое слово в `_externalCommands` + `Volatile.Write(_hasExternalCommand, true)` + `Volatile.Write(_hasCommand, true)` |
| `GetDiagnosticData()` | снимок `ProcessorFault` |
| `GetRegValue(RegType)` | одно значение |
| `CopyRegisters(Span<ulong>)` / `GetRegistersSnapshot()` | 32 регистра |
| `GetCurrentProcessorSnapshot()` | **новый** `ProcessorSnapshot` с копией регистров, `IsRunning`, `IsSleeping` |
| `InitRegHP(ulong)` | устанавливает `rHP` |
| `IsRunning`/`IsSleeping` | флаги |

Регистры — `[InlineArray(CountReg)]` из `ulong` (32 × 8 байт = 256 байт на экземпляр). `rFL` — битовое поле: бит 0 = Zero, бит 1 = Negative; устанавливается **только** `CMP`/`TEST` (см. `<remarks>` `OpCode`).

`Step()` — `switch`-выражение по всем ~60 опкодам, с `[MethodImpl(AggressiveOptimization)]`. Обработчик `Instruction*` возвращает `BiosStatus`; при `!= Success` — заполняет `_faultData`, вызывает `_processorFaultPolicy.ShouldContinue` и `Volatile.Write(_isRunning, ...)`.

**Непосредственный 64-битный операнд:** `Get64IntData()` читает `rIP`, выравнивает вверх по 8 (`(ip + 7) & ~7UL`), читает `ulong` через `_ram.ReadInt64LEUnSafe`, увеличивает `rIP` на 8. То есть после 4-байтной инструкции до 4 байт паддинга, а immediate начинается с ближайшей 8-байтной границы.

### `ProcessorSnapshot` — снимок состояния

Создаётся `Processor.GetCurrentProcessorSnapshot()`. Содержит `IsRunning`, `IsSleeping` и копию 32 регистров. Имеет `GetRegister(RegType)`. Так как снимок — независимая копия, его можно безопасно сохранять; при следующем шаге состояние не изменится.

### `PortBus` — секторная шина портов

- Адрес раскладывается на «сектор» (`address >> _deviceShift`) и «смещение» (`address & _offsetMask`).
- `RegisterDevice(IPortUse, uint)` — `IPortUse`, не `IPortController` (последний расширяет `IPortUse`).
- `ReadPort` возвращает `ReadPortResult(Data, Status)`; `Status = NullDeviceInput`, если по адресу нет устройства.
- `WritePort` возвращает `BiosStatus`: `Success` или `NullDeviceOutput`.
- `WakeProcessor` — `bool` (найдено ли устройство).
- `RegisterDevice`/`UnregisterDevice` — прямое управление массивом `_devices`; **потокобезопасности нет** (см. `<remarks>` типа).

### `Device` — вычислительное устройство

Собственные `MemoryBus` + `Processor` + `byte[] _ioMemory` + `ManualResetEventSlim _wakeSignal`. Реализует `IPortController` — может быть зарегистрировано на `PortBus` через `Emulator.RegisterDevice`.

| Метод | Назначение |
|---|---|
| `Run(ulong, long?, bool, Action?, Action<ITimerSimulationResult?>?)` | блокирующий запуск до завершения/сбоя; `limit` — лимит шагов → `InfinityLoop` |
| `DebugRun(ulong, long, int, Action?, Action<ProcessorSnapshot>?, Action?)` | запуск с задержкой и колбэком на шаг |
| `BreakPointerLaunchDevice(ulong?, Action?)` | инициализация без запуска цикла |
| `NextStepProcessor(Action<ProcessorSnapshot>?)` / `NextStepProcessorCount(int, Action<ProcessorSnapshot>?)` | ручные шаги |
| `LoadProgram(ReadOnlySpan<byte>, ulong)` / `TryLoadProgram` | загрузка + инициализация `rHP` (`programEnd.AlignUp(8)`) |
| `Stop()` / `WakeProcessor()` / `EnqueueExternalCommand(uint)` | с другого потока: `Enqueue*` + `_wakeSignal.Set()` |
| `CopyRegisters` / `GetRegistersSnapshot` / `GetLastFault` / `ResetMemoryRam` | снимки |
| `ReadPort` / `WritePort` | **без проверки границ** — возвращают `0`/игнорируют при выходе за `_ioMemory.Length` (в `<remarks>` типа — «порты ввода-вывода», реализация не бросает) |

`Stop`/`WakeProcessor`/`EnqueueExternalCommand` — единственные методы, безопасные из другого потока (используют `Enqueue*` + `_wakeSignal.Set()`). `Run`/`DebugRun`/`NextStep*` — нет.

### `Emulator` — точка входа

- Конструктор `Emulator(PortSize ports, DevicePortSize portsOnDev)` — создаёт `PortBus` и `ManagerDevices`.
- `CreateDevice(RamSize, IProcessorFaultPolicy)` — новый `Device`.
- `RegisterDevice(IPortController, string?, uint, out int)` — регистрирует внешний контроллер; при отказе **освобождает** `device` (`device.Dispose()` внутри `ManagerDevices.TryAddDevice`).
- `GetDevice(int)` / `TryGetDevice<T>(int, out T?)` — по `Id`.
- `UnregisterDevice(int)` / `ChangeDeviceSector(int, uint)`.

### `ManagerDevices` — реестр

- `TryAddDevice` — `IdState.Valid` при успехе; иначе `NotAssigned` (реестр заполнен), `OutOfRange` (сектор занят), `NoFreeSlot` (нет слота и ID), `Rejected` (`PortBus.RegisterDevice` вернул `false`, устройство **dispose'ится**). `out int id` при отказе получает `-1`/`-2`/`-3`/`-4`.
- Слоты (`DeviceEntry`) переиспользуются: `IsFreed = true` сохраняет `Id`, чтобы при повторной регистрации `Id` оставался прежним.
- `Count` — O(n) каждый раз (не кэшируется); вызывается в `TryAddDevice` на каждом добавлении.
- `Dispose` **не снимает** устройства с `PortBus` — `_portBus._devices[sector]` продолжает ссылаться на disposed-контроллер (см. [Что исправить](#что-исправить)).

## Примеры для API

**Минимальный запуск:**

```csharp
using Nantuc.Common;
using Nantuc.Contracts;
using Nantuc.Kernel.Utilites;

using var emulator = new Emulator(PortSize.KB64, DevicePortSize.B64);

using Device device = emulator.CreateDevice(RamSize.MB16, IProcessorFaultPolicy.Default);

byte[] program = [/* байты: 32-битные слова инструкций */];
device.LoadProgram(program, loadAddress: 0x0);

ProcessorFault fault = device.Run(
    start: 0,
    limit: 10_000,
    snowTimer: true,
    onLaunch: () => Console.WriteLine("start"),
    onEnd: r => Console.WriteLine($"steps={r?.Steps}, elapsed={r?.Elapsed}"));

Console.WriteLine($"{fault.Status} @ 0x{fault.InstructionPointer:X}");
```

**Пошаговое выполнение:**

```csharp
device.BreakPointerLaunchDevice(start: 0, titleAct: null);

device.NextStepProcessor(step => Console.WriteLine($"IP=0x{step.GetRegister(RegType.rIP):X}"));
device.NextStepProcessorCount(3, onStep: null);
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

// ⚠ после device.Dispose() индексация бросит ObjectDisposedException
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

- Автор расширений устройств работает с `Emulator`/`Device`/`PortBus` — это его основной API.
- Пользователь `VmApplication` этот слой напрямую не импортирует, но именно `Device.Run`/`DebugRun` вызываются из `VmApplication`.
- Автор компилятора не работает с этим слоем — только с `Common`/`Contracts`.
- Для production-кода, встраивающего эмулятор: `Emulator` — точка входа; `ManagerDevices` — деталь.

**Что стоит делать:**
- Использовать `Emulator.CreateDevice` + `Device.Run`, а не создавать `Device`/`Processor`/`MemoryBus` вручную.
- После каждой проверяемой операции `MemoryBus` — вызывать `UpdateAndReadStatus()`; статус сбрасывается.
- Помнить, что `ReadPort`/`WritePort` в `Device` **не бросают** — выход за границы даёт `0`/игнор, а не исключение.
- Вызывать `Stop`/`WakeProcessor`/`EnqueueExternalCommand` из другого потока, а `Run`/`NextStep*` — только из одного потока-координатора.
- После `device.Dispose()` читать ОЗУ только через `NativeReadOnlyBuffer` — там будет `ObjectDisposedException`, а не UB.
- `Processor.GetCurrentProcessorSnapshot()` возвращает новый `ProcessorSnapshot`; его можно сохранять и использовать после следующего шага.

**Что не стоит делать:**
- Полагаться на `_lastStatus` между операциями без `UpdateAndReadStatus` — статус «залипнет».
- Читать/писать в `_ioMemory`/`NativeMemoryBuffer` напрямую из другого потока во время `Run`.
- Регистрировать/снимать устройства (`RegisterDevice`/`UnregisterDevice`/`ChangeDeviceSector`) во время работы `Device.Run` — синхронизации нет.
- Использовать `NativeMemoryBuffer` напрямую после `Dispose` — UB, не исключение (защищён только `NativeReadOnlyView`).
- Сохранять `IReadOnlyNativeBuffer`, полученный из `Device.NativeReadOnlyBuffer`, после `Dispose()` устройства — `ObjectDisposedException` при следующей индексации.

## Особенная информация

### WASM / кроссплатформенность

| Аспект | Статус | Комментарий |
|---|---|---|
| Сборка в WASM | ⚠️ | Собирается под `browser-wasm` без правок csproj, но `Device.Run`/`DebugRun` — синхронные блокирующие методы. На single-threaded WASM они блокируют UI-поток. Требуется вынос в Web Worker или замена на async-цикл. |
| `NativeMemory` | ⚠️ | `NativeMemory.AllocZeroed`/`Free`/`Clear` работают на WASM (линейная память emscripten). Но аллокация 2 GiB (`RamSize.GB2`) в браузере почти наверняка бросит `OutOfMemoryException` — лимит линейной памяти WASM ~4 GiB на весь процесс, с фрагментацией. `MemoryBus`/`Device` не проверяют этот лимит. |
| `unsafe` / `byte*` | ✅ | `NativeMemoryBuffer` — `unsafe`; WASM поддерживает указатели. `Unsafe.ReadUnaligned`/`WriteUnaligned` работают. |
| `stackalloc` / `InlineArray` | ✅ | `stackalloc` не используется в горячих путях, кроме `Processor.GetCurrentProcessorSnapshot` (32 × 8 байт). `[InlineArray(32)]` в `Processor` — compile-time, AOT-friendly. |
| `Interlocked` / `Volatile` | ⚠️ | `Interlocked.Exchange` в `MemoryBus.Dispose` и `Device.Dispose`, `Volatile.Read/Write` в `NativeMemoryBuffer.Dispose` и `Processor` — на single-threaded WASM (без `SharedArrayBuffer`) это no-op, но код работает. С threaded WASM — атомарно. **Несогласованное использование**: см. [Что исправить](#что-исправить). |
| `ConcurrentQueue<T>` | ✅ | Две очереди в `Processor` (`_externalCommands`, `_statusFromSimulation`). На single-threaded WASM — просто очередь; на threaded — lock-free. |
| `ManualResetEventSlim.Wait(15)` | ⚠️⚠️ | В `Device.RunSimulationLoop`/`RunSimulationLoopWithLimit`/`RunSimulationLoopDebug` — `_wakeSignal.Wait(15)` блокирует текущий поток на 15 мс при `IsSleeping`. В браузере — блокировка UI. Замена: `await Task.Delay` + async-цикл, либо Web Worker. |
| `Thread.Sleep(delay)` | ⚠️⚠️ | `RunSimulationLoopDebug` — `Thread.Sleep(delay)`. В браузере — блокировка UI. |
| `Stopwatch` | ✅ | `Run` при `snowTimer: true` — `Stopwatch.StartNew()`; на WASM работает через `performance.now`-подобный таймер. |
| `IFileService` | n/a | В этой партии файлов интерфейса нет. При появлении в других слоях WASM-реализация — отдельно. |
| `Regex` / trimming | ✅ | Regex не используется; нет рефлексии, `Type.GetType`, `Activator.CreateInstance`, `dynamic`. Trimming-совместимо. |
| AOT | ✅ | Нет `Expression.Compile`, `MakeGenericMethod`/`MakeGenericType`. `switch` по опкодам в `Step` — AOT-friendly. Generic-код — только `TryGetDevice<T>` (моноформируется под используемые `T`). |

**Итог:** код собирается под WASM и AOT, но **синхронная блокирующая модель исполнения** (`Run`, `Thread.Sleep`, `ManualResetEventSlim.Wait`) не подходит для browser-WASM в single-threaded режиме. `NativeMemory.AllocZeroed` работает, но большие `RamSize` (≥ `MB512`–`GB1`) рискуют `OutOfMemoryException`. Для production в браузере — Web Worker + async-цикл + RAM ≤ ~256 MiB.

### `Processor.Step` — порядок обработки

1. Если `_hasCommand` (не `volatile`-чтение!) — при `_hasExternalCommand` вызвать `ProcessExternalCommandsSlow`, при `_hasSimulationStatus` — `ProcessSimulationStatusSlow`. Затем `_hasCommand = false` (**не `volatile`-запись**).
2. Если `!_isRunning | _isSleeping` — выход (обратите внимание: `|`, а не `||`).
3. `ip = rIP`; `rawInst = _ram.ReadInt32LEUnSafe(ip)`; `rIP += 4` (до вызова обработчика — обработчик видит уже следующее слово).
4. `switch` по опкоду → `BiosStatus`.
5. Если `dat != Success` — `_faultData = (dat, _lastFaultData, ip, opCode)`, `Volatile.Write(_isRunning, ShouldContinue(...))`.

**Непосредственные операнды:** `Get64IntData()` читает `rIP`, выравнивает вверх по 8, читает `ulong`, увеличивает `rIP` на 8 — вызывается обработчиками `LOAD`/`STORE`/`LDI`/`JMP`/`JZ`/`JNZ`/`JG`/`JL`/`CALL` и их `*_UNSAFE`-вариантов. `LOAD_IND`/`STORE_IND`/`CALL_IND`/`JMP_IND` и их `*_UNSAFE` — indirect через регистр, `Get64IntData` не вызывают.

### Флаги `rFL` и переходы

`ZeroFlagSet` — `(rFL & 1) != 0`; `NegativeFlagSet` — `(rFL & 2) != 0`. `CMP`/`TEST` устанавливают `zeroFlag | (negativeFlag << 1)` через битхаки. `JG` проверяет `!Negative & !Zero`, `JL` — только `Negative`, `JZ`/`JNZ` — `Zero`/`!Zero`. `rFL` сбрасывается в `0` на `LaunchProgramm`.

### Стек и куча

`rSP` инициализируется в `LaunchProgramm` как `_ram.Length & ~0x7UL`; `rHP` — в `Device.InitHeap` как `(loadAddress + programLength).AlignUp(8)`. `PUSH`/`CALL` уменьшают `rSP` на 8 и проверяют коллизию `rHP >= sp`; `POP`/`RET` увеличивают на 8. `RET` проверяет `sp >= _stackBase` (пустой стек → `StackUnderflow`).

### Потокобезопасность

- `Processor.Step`/`LaunchProgramm`/`Reset` — не потокобезопасны.
- `EnqueueBiosStatus`/`EnqueueExternalCommand` — безопасны из другого потока (очередь + `Volatile.Write`).
- `Device.Stop`/`WakeProcessor`/`EnqueueExternalCommand` — безопасны (делегируют в `Processor` + `_wakeSignal.Set()`).
- `PortBus.RegisterDevice`/`UnregisterDevice` — не потокобезопасны.
- `ManagerDevices`/`Emulator` — не потокобезопасны (см. `<remarks>`).
- `MemoryBus` — не потокобезопасен (`_lastStatus`/`_faultData` без синхронизации).
- `NativeMemoryBuffer.Dispose` — `Volatile.Read/Write` на `bool`, но check-then-act не атомарен; при одновременном `Dispose` из двух потоков возможен double-free.
- `Processor.GetCurrentProcessorSnapshot` — не потокобезопасен (читает регистры без синхронизации с `Step`).

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