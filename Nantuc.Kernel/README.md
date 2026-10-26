# Nantuc.Kernel — README

> Ядро платформы: процессор, шина портов, менеджер устройств, RAM-подсистема и фасад `Emulator`.  
> Реализация всех контрактов из `[Kernel.Contracts](../Nantuc.Kernel.Contracts/README.md)` поверх примитивов из `[Kernel.Common](../Nantuc.Kernel.Common/README.md)`.  
> `[← Корень](../README.md) · [Common](../Nantuc.Kernel.Common/README.md) · [Contracts](../Nantuc.Kernel.Contracts/README.md) · [Diagnostics](../Nantuc.Kernel.Diagnostics/README.md)`

## Содержание
- [Об этом модуле](#об-этом-модуле)
- [Дерево локального проекта](#дерево-локального-проекта)
- [API](#api)
- [Примеры для API](#примеры-для-api)
- [Приближенность модуля к пользователю](#приближенность-модуля-к-пользователю)
- [Что исправлено в этой версии](#что-исправлено-в-этой-версии)
- [Особенная информация](#особенная-информация)
- [Что исправить](#что-исправить)

## Об этом модуле

`Nantuc.Kernel` — исполняющая среда платформы. Здесь живут все реализации контрактов: процессор, шина портов, менеджер устройств, RAM-подсистема. Это единственный модуль в решении, который включает `AllowUnsafeBlocks=true` — `NativeMemoryBuffer` держит `byte*` на нативно выделенную ОЗУ, а `Processor.Step()` читает инструкции напрямую через `ReadInt32LEUnSafe`. Всё, что требует сырого доступа к памяти, инкапсулировано здесь и не протекает наружу.

Модуль зависит от `Kernel.Common` и `Kernel.Contracts`. Обратной зависимости нет. Иерархия:

```
Kernel.Common  ←  Kernel.Contracts  ←  Kernel  ←  VmApplication / Extensions
       (ISA)            (контракты)      (реализация)
```

Ключевые роли:
- Исполнение байткода: `Processor` (регистры, стек, heap, опкоды, флаги, wake/sleep, внешние команды, `ProcessorFault` для диагностики).
- Память: `MemoryBus` (typed read/write с диагностикой), `NativeMemoryBuffer` (`unsafe`-буфер на `NativeMemory`), `NativeReadOnlyView` (реализация `INativeReadOnlyBuffer`).
- Маршрутизация портов: `PortBus` (address → (device, offset) через битовые сдвиги).
- Управление устройствами: `ManagerDevices` (регистрация, ID, сектора, `IdState`-диагностика).
- Фасад: `Emulator` — единственная точка входа для встраивающего кода.
- VM-инстанс: `Device` — комбинация «RAM + CPU + собственное port-окно», реализует `IPortController`.

Архитектурный парадокс: `Device` — одновременно и VM, и устройство. Он держит `MemoryBus`, `Processor` и `_ioMemory` (своё port-пространство), поэтому реализует `IPortController`. Один `Device` может быть зарегистрирован как устройство в `PortBus` другого `Device` — это даёт композицию VM (VM внутри VM).

## Дерево локального проекта

Namespace'ы соответствуют папкам, кроме опечатки `Utilites` (должно быть `Utilities`):

```
Nantuc.Kernel/
├── Nantuc.Kernel.csproj              # net10.0, AllowUnsafeBlocks=true
├── BiosSystem/
│   └── Device.cs                     # Device : IPortController — VM-инстанс + port-окно
├── ControllersData/
│   └── PortBus.cs                    # PortBus : IPortBus
├── ProcessorSystem/
│   └── Processor.cs                  # Processor — CPU, регистры, стек, опкоды, GetDiagnosticData
├── RamSystem/
│   ├── MemoryBus.cs                  # MemoryBus — typed read/write с диагностикой
│   └── NativeMemoryBuffer.cs         # NativeMemoryBuffer (unsafe) + NativeReadOnlyView
└── Utilites/                         # ⚠ опечатка в имени папки/namespace
    ├── Emulator.cs                   # Emulator — фасад
    └── ManagerDevices.cs             # ManagerDevices — реестр, TryAddDevice → IdState
```

## API

### Общая картина

| Тип | Namespace | Вид | Реализует | Роль |
|---|---|---|---|---|
| `Emulator` | `Utilites` | class, `IDisposable` | — | Фасад: `PortBus` + `ManagerDevices` |
| `Device` | `BiosSystem` | sealed class, `IDisposable` | `IPortController` | VM-инстанс + port-окно |
| `Processor` | `ProcessorSystem` | sealed class | — | CPU: `Step`, регистры, стек, heap, `GetDiagnosticData` |
| `PortBus` | `ControllersData` | sealed class | `IPortBus` | Address → device + offset |
| `ManagerDevices` | `Utilites` | class, `IDisposable` | — | Реестр `IPortController` по ID/секторам |
| `MemoryBus` | `RamSystem` | class, `IDisposable` | — | Типизированный доступ к ОЗУ + статусы |
| `NativeMemoryBuffer` | `RamSystem` | `unsafe sealed class`, `IDisposable` | — | Нативный буфер (владелец памяти) |
| `NativeReadOnlyView` | `RamSystem` | class | `INativeReadOnlyBuffer` | Read-only view на буфер |

### `Emulator` — фасад

```csharp
public Emulator(PortSize ports, DevicePortSize portsOnDev)

public IEnumerable<DeviceInfo> AllDeviceInfo
public PortBus PortBus

public IdState RegisterDevice(IPortController device, string? name, uint sector, out int id);
public Device CreateDevice(RamSize size, IProcessorFaultPolicy processorFaultPolicy);
public IPortController? GetDevice(int id);
public bool TryGetDevice<T>(int id, [NotNullWhen(true)] out T? device) where T : class, IPortController;
public bool UnregisterDevice(int id);
public bool ChangeDeviceSector(int id, uint newSector);
public void Dispose();
```

`RegisterDevice` теперь возвращает `IdState` и `out int id` — правильная диагностика (`Valid`/`NotAssigned`/`OutOfRange`/`NoFreeSlot`/`Rejected`), без магических `-1..-4`. `id` при неуспехе — соответствующее отрицательное значение (для обратной совместимости с теми, кто ждёт `int`).

`CreateDevice` создаёт, но не регистрирует. Если хотите, чтобы VM была доступна через `GetDevice`, вызовите `RegisterDevice(device, name, sector, out id)` отдельно.

`Dispose` освобождает `ManagerDevices` (а тот — все устройства через `IPortController.Dispose`).

### `Device` — VM-инстанс

```csharp
public Device(PortBus portBus, RamSize size, IProcessorFaultPolicy processorFaultPolicy)

public INativeReadOnlyBuffer NativeReadOnlyBuffer
public bool IsRunning
public ulong MaxRamSize
public long StepCount

public void LoadProgram(byte[] program, ulong loadAddress);
public bool TryLoadProgramFast(ReadOnlySpan<byte> program, ulong loadAddress);
public void ResetMemoryRam();
public void ClearRegisters();

public ProcessorFault Run(
    ulong? start,
    long? limit,
    bool snowTimer,
    Action? onLaunch,
    Action<ISimulationResult?>? onEnd);

public ProcessorFault DebugRun(
    ulong start,
    long stepLimit,
    int delay,
    Action? onLaunch,
    Action<IProcessorReader>? onStep,
    Action? onEnd);

public void BreakPointerLaunchDevice(ulong? start, Action? titleAct);
public void NextStepProcessor(Action<IProcessorReader>? onStep);
public void NextStepProcessorCount(int count, Action<IProcessorReader>? onStep);
public void Stop();

public void CopyRegisters(Span<ulong> destination);
public ulong[] GetRegistersSnapshot();

// IPortController — собственное port-окно VM
public byte ReadPort(ulong offset);
public void WritePort(ulong offset, byte value);
public void WakeProcessor();
public void EnqueueExternalCommand(uint instruction);
```

`Run` теперь возвращает `ProcessorFault` (через `_processor.GetDiagnosticData()`) — это позволяет после запуска узнать, чем закончилась программа (`BiosStatus.EndProgramm` при успехе, `SegmentationFault`/`DivOnZero`/... при падении). `limit` объединяет бывшие `Run` и `RunWithLimit`.

`DebugRun` тоже возвращает `ProcessorFault`.

`LoadProgram` — медленный путь (посимвольно через `MemoryBus.WriteInt8LE`). Для больших программ — `TryLoadProgramFast`.

### `Processor` — CPU

```csharp
public Processor(MemoryBus ram, PortBus portBus, IProcessorFaultPolicy processorFaultPolicy)

public bool IsRunning
public bool IsSleeping

public void LaunchProgramm(ulong startAddress);
public void Step();
public void Reset();

public ulong GetRegValue(RegType reg);
public void InitRegHP(ulong hpInit);
public void CopyRegisters(Span<ulong> destination);
public ulong[] GetRegistersSnapshot();
public ProcessorFault GetDiagnosticData();   // новое: последний fault

public void EnqueueBiosStatus(BiosStatus status);
public void EnqueueExternalCommand(uint instruction);
public IProcessorReader GetCurrentProcessorReader();
```

`GetDiagnosticData()` возвращает `_faultData` — `ProcessorFault`, который обновляется в `ProcessSimulationStatusSlow` (внешние команды END) и в failure-пути `Step()` (после `_processorFaultPolicy.ShouldContinue`). `Device.Run`/`DebugRun` прокидывают это наружу.

### `PortBus` — маршрутизация портов

```csharp
public PortBus(PortSize ports, DevicePortSize portsOnDev)

public uint SectorCount
public uint PortsOnDevice
public ulong TotalPorts

public bool IsFreeSector(uint sector);
public bool RegisterDevice(IPortUse device, uint sector);
public void UnregisterDevice(uint sector);

// IPortBus
public ReadPortResult ReadPort(ulong address);
public BiosStatus     WritePort(ulong address, byte value);
public bool           WakeProcessor(ulong address);
```

Для незанятого сектора `ReadPort` теперь возвращает `new(0, BiosStatus.NullDeviceInput)` (раньше возвращал младший байт адреса — утечка). `WritePort` — `NullDeviceOutput`, `WakeProcessor` — `false`.

`RegisterDevice` принимает `IPortUse` (не `IPortController`), но `ManagerDevices` передаёт `IPortController` — работает благодаря наследованию. `UnregisterDevice` не вызывает `Dispose` — это делает `ManagerDevices`.

### `ManagerDevices` — реестр устройств

```csharp
public ManagerDevices(PortBus portBus)

public int Count
public IEnumerable<DeviceInfo> GetAllDevices();
public IPortController? GetDevice(int id);
public bool TryGetDevice<T>(int id, [NotNullWhen(true)] out T? device) where T : class, IPortController;
public IdState TryAddDevice(IPortController device, string? name, uint sector, out int id);
public bool RemoveDevice(int id);
public bool ChangeSector(int id, uint newSector);
```

`TryAddDevice` теперь возвращает `IdState` вместо `-1..-4`:

| `IdState` | `out id` | Причина |
|---|---|---|
| `Valid` | `>= 0` | Успех |
| `NotAssigned` | `-1` | Мест нет (`Count >= _count`) |
| `OutOfRange` | `-2` | Сектор занят |
| `NoFreeSlot` | `-3` | Нет свободного слота и ID кончились |
| `Rejected` | `-4` | `PortBus.RegisterDevice` вернул `false` (устройство **dispose'ится**) |

`RemoveDevice` вызывает `IPortController.Dispose()` — правильно: менеджер владеет устройствами.

### `MemoryBus` — typed-доступ к ОЗУ

```csharp
public MemoryBus(RamSize size)

public ulong Length
public INativeReadOnlyBuffer NativeReadOnlyBuffer
public ulong FaultData
public BiosStatus UpdateAndReadStatus();   // читает и сбрасывает в Success

public void LoadProgram(ReadOnlySpan<byte> prog, ulong startAdresProg);

// Safe
public byte   ReadInt8LE (ulong address);
public ushort ReadInt16LE(ulong address);
public uint   ReadInt32LE(ulong address);
public ulong  ReadInt64LE(ulong address);
public void   WriteInt8LE (ulong address, byte value);
public void   WriteInt16LE(ulong address, ushort value);
public void   WriteInt32LE(ulong address, uint value);
public void   WriteInt64LE(ulong address, ulong value);

// Unsafe
public byte   ReadInt8LEUnSafe (ulong address);
public ushort ReadInt16LEUnSafe(ulong address);
public uint   ReadInt32LEUnSafe(ulong address);
public ulong  ReadInt64LEUnSafe(ulong address);
public void   WriteInt8LEUnSafe (ulong address, byte value);
public void   WriteInt16LEUnSafe(ulong address, ushort value);
public void   WriteInt32LEUnSafe(ulong address, uint value);
public void   WriteInt64LEUnSafe(ulong address, ulong value);

public void ClearMemory();
```

Safe-методы не бросают исключений: при `SegmentationFault`/`AlignmentFault` возвращают `0` (для `Read`) или `void` (для `Write`), а статус кладут во внутреннее поле. Читается через `UpdateAndReadStatus()` — **это сбрасывает статус в `Success`**. Двухфазный протокол, не атомарный.

### `NativeMemoryBuffer` — нативный буфер (unsafe)

```csharp
public unsafe sealed class NativeMemoryBuffer : IDisposable

public bool  IsDisposed
public nuint Length
public INativeReadOnlyBuffer NativeReadOnlyBuffer

public byte   this[ulong index] { get; set; }
public ushort ReadUInt16(ulong address);
public uint   ReadUInt32(ulong address);
public ulong  ReadUInt64(ulong address);
public void   WriteUInt16(ulong address, ushort value);
public void   WriteUInt32(ulong address, uint value);
public void   WriteUInt64(ulong address, ulong value);
public void   Clear();
public void   LoadProgram(ReadOnlySpan<byte> prog, ulong startProg);
public void   Dispose();
```

`Dispose` теперь корректен: `if (!Volatile.Read(ref _disposed))` — освобождает память ровно один раз (был баг с инвертированным условием, приводивший к утечке).

`NativeReadOnlyView` — реализация `INativeReadOnlyBuffer` с проверками (disposed + index >= Length). Именно её получают пользователи через `MemoryBus.NativeReadOnlyBuffer`.

## Примеры для API

**Запуск программы с диагностикой:**

```csharp
using Nantuc.Kernel.Common;
using Nantuc.Kernel.Contracts;
using Nantuc.Kernel.Utilites;

using var emulator = new Emulator(PortSize.KB64, DevicePortSize.B32);
using var vm = emulator.CreateDevice(RamSize.KB64, IProcessorFaultPolicy.Default);

byte[] program = File.ReadAllBytes("snake.nbin");
if (!vm.TryLoadProgramFast(program, loadAddress: 0))
    throw new InvalidOperationException("Program doesn't fit");

ProcessorFault fault = vm.Run(
    start: 0, limit: null, snowTimer: true,
    onLaunch: () => Console.WriteLine("launch"),
    onEnd: result => Console.WriteLine($"steps={result?.Steps} elapsed={result?.Elapsed}"));

if (fault.Status != BiosStatus.EndProgramm)
    Console.Error.WriteLine($"VM fault: {fault.Status} at IP=0x{fault.InstructionPointer:X}, op={fault.OpCode}");
```

**Регистрация устройства с `IdState`:**

```csharp
var uart = new UartController();
IdState state = emulator.RegisterDevice(uart, name: "UART0", sector: 0, out int id);

switch (state)
{
    case IdState.Valid:
        Console.WriteLine($"Registered at id={id}");
        break;
    case IdState.NotAssigned:
    case IdState.OutOfRange:
    case IdState.NoFreeSlot:
    case IdState.Rejected:
        Console.Error.WriteLine($"RegisterDevice failed: {state} (id={id})");
        break;
}
```

**Пошаговое исполнение:**

```csharp
vm.BreakPointerLaunchDevice(start: 0, titleAct: () => Console.WriteLine("ready"));

for (int i = 0; i < 1000 && vm.IsRunning; i++)
    vm.NextStepProcessor(reader =>
        Console.WriteLine($"IP=0x{reader.GetRegister(RegType.rIP):X} " +
                          $"SP=0x{reader.GetRegister(RegType.rSP):X} " +
                          $"running={reader.IsRunning} sleeping={reader.IsSleeping}"));
```

**VM как устройство для другой VM:**

```csharp
using var top = new Emulator(PortSize.KB64, DevicePortSize.B32);
using var inner = top.CreateDevice(RamSize.KB32, IProcessorFaultPolicy.Default);

IdState st = top.RegisterDevice(inner, "INNER-VM", sector: 1, out int innerId);
if (st != IdState.Valid)
    throw new InvalidOperationException($"register failed: {st}");
```

**Кастомная политика fault'ов:**

```csharp
public sealed class ThrowOnFaultPolicy : IProcessorFaultPolicy
{
    public bool ShouldContinue(in ProcessorFault fault)
        => throw new InvalidOperationException(
               $"fault {fault.Status} at IP=0x{fault.InstructionPointer:X}");
}

using var vm = emulator.CreateDevice(RamSize.KB64, new ThrowOnFaultPolicy());
```

## Приближенность модуля к пользователю

Низкая. Это runtime, а не пользовательский API. Пользователь MiniC/NANASM не должен импортировать `Nantuc.Kernel` вообще. Даже автор расширений работает с контрактами (`Kernel.Contracts`), а не с этим модулем.

**Кто и что трогает:**
- Хост / встраивающий код — `Emulator` (создание, `CreateDevice`, `RegisterDevice` → `IdState`, `GetDevice`, `Dispose`).
- Автор устройства — `IPortController` из `Contracts`; `PortBus`/`ManagerDevices` ему не нужны.
- Автор отладчика/профайлера — `Device.DebugRun` (возвращает `ProcessorFault`), `NextStepProcessor`, `IProcessorReader`, `GetRegistersSnapshot`, `Processor.GetDiagnosticData`.
- Автор компилятора/ассемблера — вообще не этот модуль; он генерирует байткод и подаёт его через `TryLoadProgramFast`.
- `Processor`, `MemoryBus`, `NativeMemoryBuffer` — внутренние классы. Публичные, но не для внешнего использования.

**Что стоит делать:**
- Оборачивайте `Emulator`/`Device` в `using` — `Dispose` освобождает нативную память.
- Загружайте программу через `TryLoadProgramFast`, а не `LoadProgram`.
- Для `Run`/`DebugRun` всегда указывайте `start` явно — иначе IP = `_ram.Length` и вы получите OOB-чтение (см. [Что исправить](#что-исправить)).
- Проверяйте `IdState` от `RegisterDevice`, не полагайтесь только на `int id`.
- Читайте `ProcessorFault` из `Run`/`DebugRun` — это правильный способ узнать, чем закончился прогон.

**Что не стоит делать:**
- Создавать `Processor`/`MemoryBus`/`PortBus` напрямую в прикладном коде — используйте `Emulator`.
- Хранить `IProcessorReader` после следующего `Step()` — это снимок, не live-view.
- Полагаться на thread-safety `MemoryBus` из нескольких потоков — `_lastStatus`/`_faultData` не потокобезопасны.
- Держать `ProcessorFault` от предыдущего `Run` как «результат» — `_faultData` не сбрасывается между запусками (см. [Что исправить, п.2](#что-исправить)).

## Что исправлено в этой версии

По сравнению с прошлой фиксацией `Nantuc.Kernel`:

- `NativeMemoryBuffer.Dispose` — исправлен инвертированный `if (!Volatile.Read(ref _disposed))`. Нативная память теперь освобождается корректно (была утечка на каждый `Dispose`).
- `PortBus.ReadPort` для незанятого сектора — теперь `(0, NullDeviceInput)`, а не `(byte)address`. Убрана утечка младшего байта адреса.
- `Processor.InstructionIN`/`InstructionOUT` — теперь `_lastFaultData = portAddress` (было `_ram.FaultData`, то есть мусор от предыдущей RAM-операции).
- `Emulator.RegisterDevice` → `IdState` + `out int id`. Магические `-1..-4` больше не публичный контракт.
- `ManagerDevices.TryAddDevice` → `IdState` + `out int id` (вместо `int` с кодами `-1..-4`).
- `Device.Run` объединяет `Run` и `RunWithLimit` (`long? limit`), возвращает `ProcessorFault`.
- `Device.DebugRun` возвращает `ProcessorFault`.
- `Processor.GetDiagnosticData()` — публичный способ получить последний `ProcessorFault`.
- `Processor._faultData` — обновляется в failure-пути `Step()` и в `ProcessSimulationStatusSlow`. `ProcessorFault` теперь содержит не только `Status`/`FaultData`/`IP`, но и корректный `OpCode`.
- `Device.DebugRun` — попытка исправить stepLimit (см. [Что исправить, п.1](#что-исправить) — исправление неполное).

## Особенная информация

### Wire-формат инструкции в рантайме

`Processor.Step()` опирается на `InstructionDecoder` из `Common`, но добавляет своё правило: для опкодов с immediate (`HasNeed64IntData == true`) вызывается `Get64IntData()`, который выравнивает IP вверх до 8 и читает `ulong`. После 4-байтной инструкции с immediate может быть до 4 байт паддинга перед 8-байтным операндом. Ассемблер/компилятор обязан вставлять эти 4 байта, иначе `Get64IntData` прочитает не те данные. Это правило критично для любого backend'а.

### Стек и heap: коллизии

- `rSP` стартует с `_ram.Length`, выровнен вниз до 8.
- `rHP` устанавливается в `AlignUp(loadAddress + programLength, 8)` (через `Device.InitHeap`).
- `PUSH`/`CALL` проверяют `rHP >= sp` → `SegmentationFault` (коллизия).
- `POP`/`RET` проверяют `sp >= _stackBase` (`RET`) → `StackUnderflow`. `POP` не проверяет коллизию с heap — при корректной программе не должно случаться, но для defense-in-depth стоит добавить.

### Регистр флагов (`rFL`)

- Бит 0 — ZeroFlag: устанавливается, если результат `CMP`/`TEST` == 0.
- Бит 1 — NegativeFlag: устанавливается, если старший бит результата == 1.
- Биты 2–63 не используются — задел на Carry/Overflow/Parity.
- `JZ`/`JNZ` читают ZeroFlag, `JG` = `!Z && !N`, `JL` = `N`. Сравнения беззнаковые (флаги от `CMP`, который вычитает как `ulong`). Знаковых сравнений сейчас нет — это повод для `docs/bytecode.md`.

### `HALT`/`WAKE`/`WAKE_INT` — модель сна

- `HALT` → `_isSleeping = true`, `_hasCommand = true`. `Step()` больше не продвигает IP.
- `WAKE` (внешняя команда или `Device.WakeProcessor()`) → `_isSleeping = false`, `_hasCommand = true`.
- `WAKE_INT Rd` → `PortBus.WakeProcessor(_registers[Rd])`; если устройство не найдено — `NullDeviceOutput` (но `IProcessorFaultPolicy.Default` продолжает на этом статусе).
- `RunSimulationLoop` при `IsSleeping` делает `_wakeSignal.Wait(15)` (polling с таймаутом 15 мс). Для интерактивных сценариев (змейка) — норм; для бенчмарков — учитывайте.

### Диагностика через `ProcessorFault`

`Run`/`DebugRun` возвращают `ProcessorFault` — это `_processor.GetDiagnosticData()`, то есть последний fault, зафиксированный в `Processor`. На успешном прогоне (дошли до `END`) статус будет `BiosStatus.EndProgramm`. На fault'е — соответствующий статус (`DivOnZero`, `SegmentationFault`, …), с `InstructionPointer` и `OpCode` места падения.

Важно: `_faultData` **не сбрасывается** в `LaunchProgramm`. Если запустить VM несколько раз подряд, второй `Run` может вернуть fault от первого (см. [Что исправить, п.2](#что-исправить)).

### Многопоточность

`Processor` использует `ConcurrentQueue<uint>` и `ConcurrentQueue<BiosStatus>` для внешних команд и статусов, плюс `Volatile.Read/Write` на `_isRunning`, `_hasCommand`, `_hasExternalCommand`, `_hasSimulationStatus`. Это даёт частичную потокобезопасность: `EnqueueExternalCommand`/`EnqueueBiosStatus` можно звать из другого потока. Но:

- `_isSleeping` — не `volatile`, читается/пишется из `ExecuteExternalCommand` и `Step()`.
- `_registers` (InlineArray) — не защищены. `GetRegistersSnapshot`/`CopyRegisters` из другого потока во время `Step()` — data race.
- `MemoryBus._lastStatus`/`_faultData` — не защищены.
- `PortBus._devices` — обычный массив, не защищён.

Практический вывод: **`Device` — однопоточный**. Один `Device` — один поток исполнения. Для параллельности — несколько `Device`'ов, каждый со своим `MemoryBus`/`Processor`, но тогда `PortBus` должен быть потокобезопасным (сейчас — нет).

### Ограничения платформ

- `net10.0`, `AllowUnsafeBlocks=true`. `NativeMemoryBuffer` использует `NativeMemory.AllocZeroed/Free/Copy` и `Unsafe.ReadUnaligned/WriteUnaligned`.
- `[InlineArray(CountReg)]` — C# 12+, работает в AOT/WASM.
- `ConcurrentQueue<T>` — есть в WASM, но contention ведёт себя иначе.
- `Thread.Sleep(delay)` в `DebugRun` — в WASM либо no-op, либо `PlatformNotSupportedException`.
- `NativeMemory.Copy` требует `unsafe`; в окружениях с запретом `unsafe` этот модуль не соберётся. `Kernel.Common`/`Contracts` собираются — unsafe изолирован в одном модуле.

## Что исправить

По убыванию важности. Ссылки на файлы и методы.

1. **`Processor.Step()` — OOB-чтение без проверки IP.**  
   Файл: `ProcessorSystem/Processor.cs`, строка `uint rawInst = _ram.ReadInt32LEUnSafe(ip);`.  
   Если `ip >= _ram.Length` (например, `Run(start: null)` ставит `IP = _ram.Length`), это чтение за границей `NativeMemory`-буфера. В лучшем случае — мусорная инструкция, в худшем — access violation / утечка памяти.  
   Фикс: перед чтением `if (ip + 4 > _ram.Length) { _lastFaultData = ip; return _processorFaultPolicy.ShouldContinue(new(BiosStatus.SegmentationFault, ip, ip, opCode)); }` или аналогично. Либо запретить `Run(start: null)` — бросать `ArgumentNullException`.

2. **`Processor._faultData` не сбрасывается в `LaunchProgramm`.**  
   Файл: `ProcessorSystem/Processor.cs`, `LaunchProgramm`.  
   `_faultData` инициализируется в поле как `new(BiosStatus.Success, 0, 0, OpCode.NOP)` и обновляется при fault'ах. `LaunchProgramm` его не трогает. Если VM упала в первом `Run`, а второй `Run` успешен (дошёл до `END`), второй `Run` вернёт fault от первого — потому что `_faultData` обновился только при первом падении, а при успешном `END` не сбрасывается.  
   Фикс: в `LaunchProgramm` добавить `_faultData = new(BiosStatus.Success, 0, 0, OpCode.NOP); _lastFaultData = 0;`. Плюс в `EndProgramm` явно ставить `_faultData = new(BiosStatus.EndProgramm, 0, _registers[rIP], OpCode.END)`, чтобы успешное завершение тоже отражалось в `GetDiagnosticData()`.

3. **`Processor.Step()` — при успешном исполнении `_faultData` не обновляется.**  
   Файл: `ProcessorSystem/Processor.cs`, `Step`, блок `if (dat != BiosStatus.Success) { _faultData = ... }`.  
   Когда `dat == Success`, `_faultData` остаётся от предыдущего fault'а (или от предыдущего прогона). `Run`, вернувший `EndProgramm`, на самом деле отдаёт устаревший `_faultData`.  
   Фикс: в `EndProgramm` обновлять `_faultData = new(BiosStatus.EndProgramm, 0, _registers[rIP], OpCode.END)`. Или в `Run`/`DebugRun` перед возвратом явно сбрасывать `_faultData` в `Success`, если программа дошла до `END`.

4. **`Device.DebugRun` — stepLimit не работает с дефолтной политикой.**  
   Файл: `BiosSystem/Device.cs`, `RunSimulationLoopDebug`:
   ```csharp
   if (stepCounter >= stepLimit)
   {
       if (_processorFaultPolicy.ShouldContinue(new(BiosStatus.InfinityLoop, ...)))
       {
           Stop();
       }
   }
   ```
   С `IProcessorFaultPolicy.Default` `ShouldContinue(InfinityLoop)` возвращает `false` → `Stop()` **не вызывается** → цикл идёт дальше, `stepLimit` игнорируется, VM крутится бесконечно. С `LoggingFaultPolicyMinimal` — то же.  
   Фикс: `ShouldContinue` нужен только для side-effect (логирование). Сам факт достижения `stepLimit` должен останавливать цикл независимо от политики:
   ```csharp
   if (stepCounter >= stepLimit)
   {
       _processorFaultPolicy.ShouldContinue(new(BiosStatus.InfinityLoop, ...));  // для лога
       Stop();
       break;  // или полагаться на Stop + IsRunning == false
   }
   ```

5. **`Run(start: null)` — почти гарантированный краш.**  
   Файл: `BiosSystem/Device.cs`, `Run`: `ulong startIndex = start ?? _ram.Length;`. См. п.1.  
   Фикс: сделать `start` обязательным (убрать `?`) или бросать `ArgumentNullException` с пояснением.

6. **`Processor.InstructionJG` использует побитовое `&` на `bool`.**  
   Файл: `ProcessorSystem/Processor.cs`, `InstructionJG`:
   ```csharp
   if (!NegativeFlagSet & !ZeroFlagSet) _registers[RegType.rIP.Int()] = t;
   ```
   Работает, но `&&` правильнее и быстрее (short-circuit).  
   Фикс: заменить на `!NegativeFlagSet && !ZeroFlagSet`.

7. **`PortBus.ReadPort` для незанятого сектора — всё ещё странно.**  
   Файл: `ControllersData/PortBus.cs`, `ReadPort`. Возвращает `new(0, BiosStatus.NullDeviceInput)`. Это лучше, чем `(byte)address`, но `Data = 0` при `Status = NullDeviceInput` — двусмысленно (0 может быть валидными данными).  
   Рекомендация: либо задокументировать в XML-doc, что `Data` не определён при `Status != Success`, либо возвращать `ReadPortResult(0, NullDeviceInput)` с явной пометкой в комментарии.

8. **`ManagerDevices.ChangeSector` не работает для того же сектора.**  
   Файл: `Utilites/ManagerDevices.cs`, `ChangeSector`. `RegisterDevice` в тот же сектор вернёт `false` (сектор занят этим же устройством), метод вернёт `false`.  
   Фикс: короткое замыкание `if (newSector == e.Sector) return true;` в начале, либо переставить порядок (unregister → register) с обработкой отказа.

9. **`Device.LoadProgram` — посимвольный цикл.**  
   Файл: `BiosSystem/Device.cs`, `LoadProgram`. Для программы на 64 KiB это 65 536 итераций с `UpdateAndReadStatus` на каждом байте.  
   Фикс: сделать тонкой обёрткой над `TryLoadProgramFast` с `throw` при неудаче.

10. **`Utilites` — опечатка в namespace.**  
    Файлы: `Utilites/Emulator.cs`, `Utilites/ManagerDevices.cs`, namespace `Nantuc.Kernel.Utilites`.  
    Переименуйте в `Utilities` сейчас, пока проект не разошёлся по форкам. Это breaking change, но дешёвый.

11. **`MemoryBus.Dispose` — NRE после Dispose.**  
    Файл: `RamSystem/MemoryBus.cs`, `Dispose` обнуляет `_memory`, но `NativeReadOnlyBuffer`/`Length` этого не проверяют:
    ```csharp
    public INativeReadOnlyBuffer NativeReadOnlyBuffer => _memory.NativeReadOnlyBuffer;  // NRE
    public ulong Length => _memory.Length;                                              // NRE
    ```
    Фикс: `ObjectDisposedException.ThrowIf(_memory is null, this);` в этих свойствах, или флаг `_disposed`.

12. **`NativeMemoryBuffer.ReadUInt16/32/64` и `WriteUInt16/32/64` — без проверок.**  
    Файл: `RamSystem/NativeMemoryBuffer.cs`. Публичные методы, но принимают `ulong address` и делают `_ptrRam + address` без проверки границ. Это by design (unsafe API), но стоит задокументировать в XML-doc: «caller must ensure address + sizeof(T) <= Length».  
    Рекомендация: `///` на каждом методе с явным предупреждением.

13. **`PortBus.RegisterDevice` принимает `IPortUse`, но не проверяет, не зарегистрирован ли уже.**  
    Файл: `ControllersData/PortBus.cs`, `RegisterDevice`. Проверяет только `IsFreeSector(sector)`. Если один и тот же `IPortUse` зарегистрирован на двух секторах — `UnregisterDevice` одного сектора не уберёт другой.  
    Фикс: либо запретить (проверять, что `device` не в `_devices`), либо задокументировать, что это ответственность `ManagerDevices` (который сейчас тоже не проверяет).

14. **`Device.ReadPort` возвращает `0` для OOB — без статуса.**  
    Файл: `BiosSystem/Device.cs`, `ReadPort`:
    ```csharp
    if (offset >= (ulong)_ioMemory.Length) return 0; // или ошибка
    ```
    Комментарий «или ошибка» — не реализовано. Контракт `IPortUse.ReadPort` возвращает `byte` (без статуса), так что ошибку не передать, кроме как через `PortBus.ReadPort` (который проверяет `ResolveAddress` и возвращает `NullDeviceInput` для невалидного сектора). Значит, `Device.ReadPort` с OOB-offset может вернуть 0 молча.  
    Рекомендация: либо бросать `ArgumentOutOfRangeException` (это баг вызывающего — offset приходит из `PortBus`, который уже маскирует `offset & _offsetMask`, так что OOB возможен только при неправильном `_ioMemory.Length`), либо оставить 0 и задокументировать.

15. **`ManagerDevices.AddDevice` → `TryAddDevice` — теперь `IdState`, но `id` при `Rejected` равен `-4`.**  
    Файл: `Utilites/ManagerDevices.cs`, `TryAddDevice`. При `IdState.Rejected` `id = -4`, но `device.Dispose()` уже вызван. Вызывающий может подумать, что `-4` — валидный ID и попробовать использовать.  
    Рекомендация: при `Rejected` ставить `id = 0` (невалидный) или документировать, что `id` имеет смысл только при `IdState.Valid`. Лучше — сделать `out int id` nullable через `int?`, но это меняет сигнатуру. Минимум — XML-doc.

16. **`GetAllDevices` использует `yield return` — allocations при каждом enumerate.**  
    Файл: `Utilites/ManagerDevices.cs`, `GetAllDevices`. Возвращает `IEnumerable<DeviceInfo>`, при каждом `foreach` создаётся state machine. Для горячего пути — не критично (вызывается редко), но если `Emulator.AllDeviceInfo` дёргают в цикле — заметно.  
    Рекомендация: либо задокументировать, либо возвращать `IReadOnlyList<DeviceInfo>`, собранный в `List<DeviceInfo>`.