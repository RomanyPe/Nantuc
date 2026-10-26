# Nantuc.Kernel — README

> Ядро платформы: процессор, шина портов, менеджер устройств, RAM-подсистема и фасад `Emulator`.  
> Реализация всех контрактов из `[Kernel.Contracts](../Nantuc.Kernel.Contracts/README.md)` поверх примитивов из `[Kernel.Common](../Nantuc.Kernel.Common/README.md)`.  
> `[← Корень](../README.md) · [Common](../Nantuc.Kernel.Common/README.md) · [Contracts](../Nantuc.Kernel.Contracts/README.md)`

## Содержание
- [Об этом модуле](#об-этом-модуле)
- [Дерево локального проекта](#дерево-локального-проекта)
- [API](#api)
- [Примеры для API](#примеры-для-api)
- [Приближенность модуля к пользователю](#приближенность-модуля-к-пользователю)
- [Рекомендации](#рекомендации)
- [Особенная информация](#особенная-информация)

## Об этом модуле

`Nantuc.Kernel` — это **исполняющая среда** платформы. Здесь живут все реализации контрактов: процессор, шина портов, менеджер устройств, RAM-подсистема. Это единственный модуль в решении, который включает `AllowUnsafeBlocks=true` — и не случайно: `NativeMemoryBuffer` держит `byte*` на нативно выделенную ОЗУ, а `Processor.Step()` читает инструкции напрямую через `ReadInt32LEUnSafe`. Всё, что требует сырого доступа к памяти, инкапсулировано здесь и не протекает наружу.

Модуль зависит от `Kernel.Common` и `Kernel.Contracts`. Обратной зависимости нет. Иерархия:

```
Kernel.Common  ←  Kernel.Contracts  ←  Kernel  ←  VmApplication / Extensions
       (ISA)            (контракты)      (реализация)
```

Ключевые роли:
- **Исполнение байткода**: `Processor` (регистры, стек, heap, опкоды, флаги, wake/sleep, внешние команды).
- **Память**: `MemoryBus` (typed read/write с диагностикой), `NativeMemoryBuffer` (`unsafe`-буфер на `NativeMemory`), `NativeReadOnlyView` (реализация `INativeReadOnlyBuffer`).
- **Маршрутизация портов**: `PortBus` (address → (device, offset) через битовые сдвиги).
- **Управление устройствами**: `ManagerDevices` (регистрация, ID, сектора).
- **Фасад**: `Emulator` — единственная точка входа для тех, кто встраивает платформу.
- **VM-инстанс**: `Device` — комбинация «RAM + CPU + собственное port-окно», реализует `IPortController`.

Важно понимать архитектурный парадокс: `Device` — это **одновременно и VM, и устройство**. Он держит `MemoryBus`, `Processor` и `_ioMemory` (своё port-пространство), поэтому реализует `IPortController`. Один `Device` может быть зарегистрирован как устройство в `PortBus` другого `Device` — это даёт композицию VM (VM внутри VM). Семантически мощно, но легко запутаться: см. [Рекомендации](#рекомендации).

## Дерево локального проекта

Namespace'ы полностью соответствуют папкам, кроме опечатки `Utilites` (должно быть `Utilities`):

```
Nantuc.Kernel/
├── Nantuc.Kernel.csproj              # net10.0, AllowUnsafeBlocks=true
├── BiosSystem/
│   └── Device.cs                     # Device : IPortController — VM-инстанс + port-окно
├── ControllersData/
│   └── PortBus.cs                    # PortBus : IPortBus — маршрутизация портов
├── ProcessorSystem/
│   └── Processor.cs                  # Processor — CPU, регистры, стек, опкоды
├── RamSystem/
│   ├── MemoryBus.cs                  # MemoryBus — typed read/write с диагностикой
│   └── NativeMemoryBuffer.cs         # NativeMemoryBuffer (unsafe) + NativeReadOnlyView
└── Utilites/                         # ⚠ опечатка в имени папки/namespace
    ├── Emulator.cs                   # Emulator — фасад
    └── ManagerDevices.cs             # ManagerDevices — реестр устройств
```

> ⚠ `Utilites` — опечатка (должно быть `Utilities`). Она уже «зашита» в namespace и в публичные типы (`Nantuc.Kernel.Utilites.Emulator`). Переименование — breaking change; делайте его сразу, пока проект молодой, или фиксируйте как «legacy namespace».

## API

### Общая картина

| Тип | Namespace | Вид | Реализует | Роль |
|---|---|---|---|---|
| `Emulator` | `Utilites` | class, `IDisposable` | — | Фасад: `PortBus` + `ManagerDevices` |
| `Device` | `BiosSystem` | sealed class, `IDisposable` | `IPortController` | VM-инстанс (RAM + CPU + port-окно) |
| `Processor` | `ProcessorSystem` | sealed class | — | CPU: `Step`, регистры, стек, heap |
| `PortBus` | `ControllersData` | sealed class | `IPortBus` | Address → device + offset |
| `ManagerDevices` | `Utilites` | class, `IDisposable` | — | Реестр `IPortController` по ID/секторам |
| `MemoryBus` | `RamSystem` | class, `IDisposable` | — | Типизированный доступ к ОЗУ + статусы |
| `NativeMemoryBuffer` | `RamSystem` | `unsafe sealed class`, `IDisposable` | — | Нативный буфер (владелец памяти) |
| `NativeReadOnlyView` | `RamSystem` | class | `INativeReadOnlyBuffer` | Read-only view на буфер |

Из всех типов **только `ProcessorReader`** (private inner в `Processor`) реализует `IProcessorReader`. Публично `IProcessorReader` получают через `Device.GetCurrentProcessorReader()` / `Processor.GetCurrentProcessorReader()`.

### `Emulator` — фасад (то, с чего начинают)

```csharp
public Emulator(PortSize ports, DevicePortSize portsOnDev)

public IEnumerable<DeviceInfo> AllDeviceInfo
public PortBus PortBus

public int  RegisterDevice(IPortController device, string? name, uint sector)
public Device CreateDevice(RamSize size, IProcessorFaultPolicy processorFaultPolicy)
public IPortController? GetDevice(int id)
public bool TryGetDevice<T>(int id, out T? device) where T : class, IPortController
public bool UnregisterDevice(int id)
public bool ChangeDeviceSector(int id, uint newSector)
```

- Создаёт внутри `PortBus` и `ManagerDevices`. Ничего не выделяет в native-памяти сам — это делает `Device`.
- `RegisterDevice` возвращает **ID или отрицательный код ошибки** (`-1`…`-4` из `ManagerDevices.AddDevice`). Это не `enum` — плохо для читаемости, но контракт такой.
- `CreateDevice` **создаёт, но не регистрирует** `Device` в менеджере. Если хотите, чтобы VM была доступна через `GetDevice`, вызовите `RegisterDevice(device, name, sector)` отдельно.
- `TryGetDevice<T>` — типизированный доступ к зарегистрированному устройству: `emulator.TryGetDevice<UartController>(id, out var uart)`.
- `Dispose` освобождает `ManagerDevices` (а тот — все устройства через `IPortController.Dispose`).

### `Device` — VM-инстанс

```csharp
public Device(PortBus portBus, RamSize size, IProcessorFaultPolicy processorFaultPolicy)

public INativeReadOnlyBuffer NativeReadOnlyBuffer
public bool IsRunning
public ulong MaxRamSize
public long StepCount

public void LoadProgram(byte[] program, ulong loadAddress)
public bool TryLoadProgramFast(ReadOnlySpan<byte> program, ulong loadAddress)
public void ResetMemoryRam()
public void ClearRegisters()

public void Run(ulong? start, bool snowTimer, Action? onLaunch, Action<ISimulationResult?>? onEnd)
public void RunWithLimit(ulong? start, long limit, bool snowTimer, Action? onLaunch, Action<ISimulationResult?>? onEnd)
public void DebugRun(ulong start, long stepLimit, int delay,
                     Action? onLaunch, Action<IProcessorReader>? onStep, Action? onEnd)
public void BreakPointerLaunchDevice(ulong? start, Action? titleAct)
public void NextStepProcessor(Action<IProcessorReader>? onStep)
public void NextStepProcessorCount(int count, Action<IProcessorReader>? onStep)
public void Stop()

public void CopyRegisters(Span<ulong> destination)
public ulong[] GetRegistersSnapshot()

// IPortController — собственное port-окно VM
public byte ReadPort(ulong offset)
public void WritePort(ulong offset, byte value)
public void WakeProcessor()
public void EnqueueExternalCommand(uint instruction)
```

Семантика:
- `LoadProgram` — **медленный** путь, пишет по байту через `MemoryBus.WriteInt8LE` с проверкой статуса. Для больших программ используйте `TryLoadProgramFast`, который копирует через `MemoryBus.LoadProgram` (`NativeMemory.Copy`).
- `Run` / `RunWithLimit` — запускает цикл `Processor.Step()` до `IsRunning == false` (END/ошибка/policy-stop). `RunWithLimit` дополнительно останавливает по `limit` шагов (через `Stop()`).
- `DebugRun` — с `onStep`-коллбэком на каждый шаг, с опциональным `Thread.Sleep(delay)` и `stepLimit`.
- `BreakPointerLaunchDevice` — только ставит IP и `IsRunning = true`, без цикла. Для ручного `NextStepProcessor`.
- `NextStepProcessor` / `NextStepProcessorCount` — ручное пошаговое исполнение. Не запускает wake-цикл.
- `Stop()` — enqueue `EndProgramm` + `WAKE` + `_wakeSignal.Set()`.
- `ReadPort` / `WritePort` — доступ к **своему** `_ioMemory` (внутри `Device`, размер = `PortBus.PortsOnDevice`). Это port-окно, через которое **другие** устройства могут обращаться к этому VM.
- `EnqueueExternalCommand(uint instruction)` — внешняя команда процессору (END/HALT/WAKE).

**Ключевое предупреждение:** `Run(start: null, ...)` ставит IP = `_ram.Length` (конец RAM). `Processor.Step()` тут же делает `ReadInt32LEUnSafe(ip)` — **чтение за границей буфера**. См. [Рекомендации](#рекомендации).

### `Processor` — CPU

```csharp
public Processor(MemoryBus ram, PortBus portBus, IProcessorFaultPolicy processorFaultPolicy)

public bool IsRunning
public bool IsSleeping

public void LaunchProgramm(ulong startAddress)   // ⚠ «Programm» — часть публичного API
public void Step()                                // один шаг
public void Reset()

public ulong GetRegValue(RegType reg)
public void  InitRegHP(ulong hpInit)
public void  CopyRegisters(Span<ulong> destination)  // требует ≥32 элементов
public ulong[] GetRegistersSnapshot()

public void EnqueueBiosStatus(BiosStatus status)
public void EnqueueExternalCommand(uint instruction)

public IProcessorReader GetCurrentProcessorReader()
```

- **32 регистра по 64 бита** (`[InlineArray(CountReg)]` — без аллокации массива на каждый `Processor`).
- `rFL` (flags): бит 0 — ZeroFlag, бит 1 — NegativeFlag. Устанавливаются `CMP`/`TEST`, читаются `JZ`/`JNZ`/`JG`/`JL`.
- `rSP` инициализируется в `_ram.Length`, выровнен вниз до 8.
- `rHP` устанавливается через `InitRegHP` (это делает `Device.LoadProgram` после загрузки программы).
- `rIP` — instruction pointer, 64-битный, не выровнен.
- `LaunchProgramm` сбрасывает `rFL`, `rSP`, `_isSleeping = false`, ставит `rIP = startAddress`.
- `Step()`:
  - Обрабатывает внешние команды (END/HALT/WAKE) и статусы из очереди.
  - Читает `uint rawInst = _ram.ReadInt32LEUnSafe(ip)`.
  - Декодирует через `InstructionDecoder` (из `Common`).
  - Инкрементирует IP на 4.
  - Для опкодов с immediate вызывает `Get64IntData()`, который **выравнивает IP вверх до 8** и читает `ulong` — важно: после 4-байтной инструкции может быть 4 байта паддинга перед 8-байтным immediate. Это деталь wire-формата, см. [Особенная информация](#особенная-информация).
  - Если статус != `Success`, вызывает `_processorFaultPolicy.ShouldContinue(fault)`; результат определяет `IsRunning`.
- `Reset()` — обнуляет регистры, сбрасывает `IsRunning`/`IsSleeping`, чистит очереди.
- `ProcessorReader` (private) — снимок состояния CPU на момент `GetCurrentProcessorReader()`. **Не live-view**; после следующего `Step()` регистры в reader'е уже могут не соответствовать реальному CPU.

### `PortBus` — маршрутизация портов

```csharp
public PortBus(PortSize ports, DevicePortSize portsOnDev)

public uint  SectorCount
public uint  PortsOnDevice
public ulong TotalPorts

public bool IsFreeSector(uint sector)
public bool RegisterDevice(IPortUse device, uint sector)
public void UnregisterDevice(uint sector)

// IPortBus
public ReadPortResult ReadPort(ulong address)
public BiosStatus     WritePort(ulong address, byte value)
public bool           WakeProcessor(ulong address)
```

- Порт-пространство разбивается на `SectorCount = 1 << (ports.Log2 - portsOnDev.Log2)` секторов, по `PortsOnDevice = 1 << portsOnDev.Log2` портов в каждом.
- `address >> deviceShift` даёт сектор; `address & offsetMask` даёт `offset` внутри устройства.
- **Незанятый сектор**: `ReadPort` возвращает `(byte)address` (младший байт адреса) и `BiosStatus.NullDeviceInput`; `WritePort` возвращает `NullDeviceOutput`; `WakeProcessor` — `false`. Ненулевое `Data` — потенциальная утечка адреса, см. Рекомендации.
- `RegisterDevice` идемпотентен только для свободных секторов: повторная регистрация в занятый — `false`.
- `UnregisterDevice` просто ставит `null` в слот, **не вызывает `Dispose`** (это делает `ManagerDevices`).

### `ManagerDevices` — реестр устройств

```csharp
public ManagerDevices(PortBus portBus)

public int Count
public IEnumerable<DeviceInfo> GetAllDevices()
public IPortController? GetDevice(int id)
public bool TryGetDevice<T>(int id, out T? device) where T : class, IPortController
public int  AddDevice(IPortController device, string? name, uint sector)  // возвращает ID или -1…-4
public bool RemoveDevice(int id)
public bool ChangeSector(int id, uint newSector)
```

- `_count = (int)portBus.SectorCount` — максимум устройств = число секторов.
- ID выдаются монотонно (`_nextId++`), освобождённые слоты переиспользуются с **тем же ID**.
- `AddDevice`:
  - `-1` — мест нет
  - `-2` — сектор занят
  - `-3` — нет свободного слота и ID кончились
  - `-4` — `PortBus.RegisterDevice` вернул `false` (в этом случае устройство **dispose'ится**)
- `RemoveDevice` вызывает `IPortController.Dispose()` — правильно: менеджер владеет устройствами.
- `ChangeSector` сначала регистрирует в новом, потом отменяет старый. Если `newSector == старый`, `RegisterDevice` вернёт `false` → метод вернёт `false`. См. Рекомендации.

### `MemoryBus` — typed-доступ к ОЗУ

```csharp
public MemoryBus(RamSize size)

public ulong Length
public INativeReadOnlyBuffer NativeReadOnlyBuffer
public ulong FaultData

public BiosStatus UpdateAndReadStatus()   // читает и сбрасывает в Success
public void LoadProgram(ReadOnlySpan<byte> prog, ulong startAdresProg)

// Safe (с проверкой границ и выравнивания)
public byte   ReadInt8LE (ulong address)
public ushort ReadInt16LE(ulong address)
public uint   ReadInt32LE(ulong address)
public ulong  ReadInt64LE(ulong address)
public void   WriteInt8LE (ulong address, byte value)
public void   WriteInt16LE(ulong address, ushort value)
public void   WriteInt32LE(ulong address, uint value)
public void   WriteInt64LE(ulong address, ulong value)

// Unsafe (без проверок)
public byte   ReadInt8LEUnSafe (ulong address)
public ushort ReadInt16LEUnSafe(ulong address)
public uint   ReadInt32LEUnSafe(ulong address)
public ulong  ReadInt64LEUnSafe(ulong address)
public void   WriteInt8LEUnSafe (ulong address, byte value)
public void   WriteInt16LEUnSafe(ulong address, ushort value)
public void   WriteInt32LEUnSafe(ulong address, uint value)
public void   WriteInt64LEUnSafe(ulong address, ulong value)

public void ClearMemory()
```

- Все `Read*/Write*` — little-endian на уровне контракта. На big-endian хосте `NativeMemoryBuffer` делает `BinaryPrimitives.ReverseEndianness`.
- Safe-методы **не бросают** исключений: при `SegmentationFault` / `AlignmentFault` они возвращают `0` (для `Read`) или `void` (для `Write`), а статус кладут во внутреннее поле. Читается через `UpdateAndReadStatus()` — **это сбрасывает статус в `Success`**. Двухфазный протокол, **не атомарный** — см. Рекомендации.
- Unsafe-методы — без проверок. Падение/утечка — на совести вызывающего. Используются в `Processor.Step()` для скорости.
- `NativeReadOnlyBuffer` отдаёт `INativeReadOnlyBuffer` (`NativeReadOnlyView`). После `Dispose` `_memory` становится `null` → NRE при обращении. Не держите view после dispose.
- `LoadProgram` — быстрая загрузка через `NativeMemory.Copy`, бросает `ArgumentOutOfRangeException`, если программа не помещается.

### `NativeMemoryBuffer` — нативный буфер (unsafe)

```csharp
public unsafe sealed class NativeMemoryBuffer : IDisposable

public NativeMemoryBuffer(RamSize sizeRam)
public bool  IsDisposed
public nuint Length
public INativeReadOnlyBuffer NativeReadOnlyBuffer

public byte this[ulong index] { get; set; }       // без проверок
public ushort ReadUInt16(ulong address)           // без проверок
public uint   ReadUInt32(ulong address)
public ulong  ReadUInt64(ulong address)
public void   WriteUInt16(ulong address, ushort value)
public void   WriteUInt32(ulong address, uint value)
public void   WriteUInt64(ulong address, ulong value)
public void   Clear()
public void   LoadProgram(ReadOnlySpan<byte> prog, ulong startProg)
public void   Dispose()
```

- Владеет памятью: `NativeMemory.AllocZeroed(ram)`, освобождает в `Dispose`.
- **`Dispose` содержит инвертированное условие** — память **не освобождается**, если не была освобождена ранее. См. Рекомендации (это критично).
- `NativeReadOnlyView` — реализация `INativeReadOnlyBuffer` с **проверками** (disposed + index >= Length). Именно её получают пользователи через `MemoryBus.NativeReadOnlyBuffer` / `Device.NativeReadOnlyBuffer`.
- `index`-setter и `UnSafe`-методы — без проверок, потенциальный OOB.

## Примеры для API

**Запуск программы через `Emulator`:**

```csharp
using Nantuc.Kernel.Common;
using Nantuc.Kernel.Contracts;
using Nantuc.Kernel.Utilites;

using var emulator = new Emulator(PortSize.KB64, DevicePortSize.B32);

using var vm = emulator.CreateDevice(RamSize.KB64, IProcessorFaultPolicy.Default);

byte[] program = File.ReadAllBytes("snake.nbin");   // 4-байтные инструкции
if (!vm.TryLoadProgramFast(program, loadAddress: 0))
    throw new InvalidOperationException("Program doesn't fit");

vm.Run(start: 0, snowTimer: true,
       onLaunch: () => Console.WriteLine("launch"),
       onEnd: result =>
       {
           if (result is not null)
               Console.WriteLine($"elapsed={result.Elapsed}, steps={result.Steps}");
       });
```

**Пошаговое исполнение с наблюдением:**

```csharp
vm.BreakPointerLaunchDevice(start: 0, titleAct: () => Console.WriteLine("ready"));

for (int i = 0; i < 1000 && vm.IsRunning; i++)
{
    vm.NextStepProcessor(reader =>
    {
        Console.WriteLine($"IP=0x{reader.GetRegister(RegType.rIP):X} " +
                          $"SP=0x{reader.GetRegister(RegType.rSP):X} " +
                          $"running={reader.IsRunning} sleeping={reader.IsSleeping}");
    });
}
```

**Снимок регистров после прогона:**

```csharp
ulong[] regs = vm.GetRegistersSnapshot();
// regs[(int)RegType.rIP], regs[(int)RegType.rSP], …
Span<ulong> buf = stackalloc ulong[32];
vm.CopyRegisters(buf);
```

**Регистрация собственного устройства:**

```csharp
using var emulator = new Emulator(PortSize.KB16, DevicePortSize.B16);

var uart = new UartController();     // реализует IPortController
int id = emulator.RegisterDevice(uart, name: "UART0", sector: 0);
if (id < 0) throw new InvalidOperationException($"register failed: {id}");

// Позже:
if (emulator.TryGetDevice<UartController>(id, out var same))
    Console.WriteLine(same.ReadPort(0));
```

**VM как устройство для другой VM (композиция):**

```csharp
using var top = new Emulator(PortSize.KB64, DevicePortSize.B32);
using var inner = top.CreateDevice(RamSize.KB32, IProcessorFaultPolicy.Default);

// inner — это и VM, и IPortController: регистрируем его как «устройство» в top
int innerId = top.RegisterDevice(inner, "INNER-VM", sector: 1);
```

**Кастомная политика fault'ов (тест / отладка):**

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

**Низкая.** Это runtime, а не пользовательский API. Пользователь MiniC/NANASM не должен импортировать `Nantuc.Kernel` вообще. Даже автор расширений работает с контрактами (`Kernel.Contracts`), а не с этим модулем.

**Кто и что трогает:**
- **Хост / встраивающий код** — `Emulator` (создание, `CreateDevice`, `RegisterDevice`, `GetDevice`, `Dispose`).
- **Автор устройства** — `IPortController` из `Contracts`; `PortBus`/`ManagerDevices` ему не нужны.
- **Автор отладчика/профайлера** — `Device.DebugRun`, `NextStepProcessor`, `IProcessorReader`, `GetRegistersSnapshot`.
- **Автор компилятора/ассемблера** — вообще не этот модуль; он генерирует байткод и подаёт его через `TryLoadProgramFast`.
- **`Processor`, `MemoryBus`, `NativeMemoryBuffer`** — «внутренние» классы. Публичные, но не для внешнего использования. Исключение — если вы пишете собственный бэкенд или очень низкоуровневые тесты.

**Что стоит делать:**
- Оборачивайте `Emulator`/`Device` в `using` — `Dispose` освобождает нативную память.
- Загружайте программу через `TryLoadProgramFast` (`NativeMemory.Copy`), а не `LoadProgram` (посимвольно).
- Для `Run`/`DebugRun` **всегда** указывайте `start` явно — иначе IP = `_ram.Length` и вы получите OOB-чтение.
- Для пошагового исполнения — `BreakPointerLaunchDevice` + `NextStepProcessor`.
- Реализуйте `IProcessorFaultPolicy`, если хотите логировать падения без остановки VM.

**Что не стоит делать:**
- Создавать `Processor`/`MemoryBus`/`PortBus` напрямую в прикладном коде — используйте `Emulator`.
- Вызывать `ReadPort`/`WritePort` у `Device` без необходимости — это port-окно для *других* устройств, а не API пользователя.
- Хранить `IProcessorReader` после следующего `Step()` — это снимок, не live-view.
- Полагаться на thread-safety `MemoryBus` из нескольких потоков — `_lastStatus`/`_faultData` не потокобезопасны.
- Освобождать `Device` вручную через `Dispose` дважды — там `Interlocked.Exchange`, повторный вызов безопасен, но `NativeMemoryBuffer.Dispose` содержит баг (см. ниже).

## Особенная информация

### Wire-формат инструкции в рантайме

`Processor.Step()` опирается на `InstructionDecoder` из `Common`, но добавляет **своё правило**: для опкодов с immediate (`HasNeed64IntData == true`) вызывается `Get64IntData()`, который:

```csharp
ulong ip = _registers[rIP];
ip = (ip + 7) & ~7UL;            // выровнять вверх до 8
ulong data = _ram.ReadInt64LEUnSafe(ip);
ip += 8;
_registers[rIP] = ip;
```

То есть после 4-байтной инструкции с immediate **может быть 4 байта паддинга** перед 8-байтным операндом. Ассемблер/компилятор обязан вставлять эти 4 байта (например, `NOP`), иначе `Get64IntData` прочитает не те данные. Это не описано ни в `docs/bytecode.md` (его пока нет), ни в XML-doc. Зафиксируйте это правило в спецификации байткода — оно критично для любого backend'а.

### Стек и heap: коллизии

- `rSP` стартует с `_ram.Length`, выровнен вниз до 8.
- `rHP` устанавливается в `AlignUp(loadAddress + programLength, 8)` (через `Device.InitHeap`).
- `PUSH` / `CALL` проверяют `rHP >= sp` → `SegmentationFault` (коллизия).
- `POP` / `RET` **не проверяют** коллизию с heap, но проверяют `sp >= _stackBase` (`RET`) → `StackUnderflow`. `POP` не проверяет `_stackBase` — если `rSP` уедет выше (что не должно случаться при корректной программе), можно прочитать мусор. Для defense-in-depth добавьте проверку и в `POP`.

### Регистр флагов (`rFL`)

- Бит 0 — ZeroFlag: устанавливается, если результат `CMP`/`TEST` == 0.
- Бит 1 — NegativeFlag: устанавливается, если старший бит результата == 1.
- Биты 2–63 не используются — задел на Carry/Overflow/Parity.
- `JZ`/`JNZ` читают ZeroFlag, `JG` = `!Z && !N`, `JL` = `N`. Обратите внимание: `JG`/`JL` работают с **беззнаковыми** сравнениями (флаги — от `CMP`, который вычитает как `ulong`). Для знаковых сравнений нужен отдельный механизм — сейчас его нет, и это повод для `docs/bytecode.md` явно сказать «сравнения беззнаковые».

### `HALT` / `WAKE` / `WAKE_INT` — модель сна

- `HALT` → `_isSleeping = true`, `_hasCommand = true`. `Step()` больше не продвигает IP.
- `WAKE` (внешняя команда или `Device.WakeProcessor()`) → `_isSleeping = false`, `_hasCommand = true`.
- `WAKE_INT Rd` → `PortBus.WakeProcessor(_registers[Rd])`; если устройство не найдено — `NullDeviceOutput` (но `IProcessorFaultPolicy.Default` продолжает на этом статусе — см. `Common/README`).
- `RunSimulationLoop` при `IsSleeping` делает `_wakeSignal.Wait(15)` (polling с таймаутом 15 мс), а не «настоящий» event-wait. Это компромисс: CPU не крутится в tight loop, но есть до 15 мс задержки пробуждения. Для интерактивных сценариев (змейка) — норм; для бенчмарков — учитывайте.

### Многопоточность

`Processor` использует `ConcurrentQueue<uint>` и `ConcurrentQueue<BiosStatus>` для внешних команд и статусов, плюс `Volatile.Read/Write` на `_isRunning`, `_hasCommand`, `_hasExternalCommand`, `_hasSimulationStatus`. Это даёт **частичную** потокобезопасность: `EnqueueExternalCommand`/`EnqueueBiosStatus` можно звать из другого потока. Но:

- `_isSleeping` — не `volatile`, читается/пишется из `ExecuteExternalCommand` и `Step()` в одном потоке (обычно), но при кросс-поточном `Stop()` возможны гонки.
- `_registers` (InlineArray) — не защищены. `GetRegistersSnapshot`/`CopyRegisters` из другого потока во время `Step()` — data race.
- `MemoryBus._lastStatus`/`_faultData` — не защищены.

Практический вывод: **`Device` — однопоточный**. Один `Device` — один поток исполнения. Если нужна параллельность — несколько `Device`'ов, каждый со своим `MemoryBus`/`Processor`, но общим `PortBus` (тогда `PortBus` должен быть потокобезопасным — сейчас он **не** является, `_devices` — обычный массив).

### Ограничения платформ

- **`net10.0`, `AllowUnsafeBlocks=true`.** `NativeMemoryBuffer` использует `NativeMemory.AllocZeroed/Free/Copy` и `Unsafe.ReadUnaligned/WriteUnaligned`. В WASM это работает, но `NativeMemory` там — обёртка над линейной памятью; возможны ограничения по размеру и производительности. Для AOT — работает, но `[MethodImpl(AggressiveOptimization)]` на `Step()` может вести себя иначе, чем в JIT.
- **`[InlineArray(CountReg)]`** — C# 12+, требует `net10.0`. В AOT инлайнится в структуру, аллокаций нет.
- **`ConcurrentQueue<T>`** — есть в WASM, но contention ведёт себя иначе. Если `Processor` в WASM станет узким местом — рассмотрите `Channel<T>` или SPSC-очередь.
- **`Thread.Sleep(delay)` в `DebugRun`** — в WASM `Thread.Sleep` либо no-op, либо бросает `PlatformNotSupportedException` (зависит от рантайма). Для WASM-отладчика делайте `await Task.Delay` в асинхронном цикле, а не блокирующий `Thread.Sleep`.
- **`NativeMemory.Copy`** требует `unsafe`; в окружениях с запретом `unsafe` (некоторые sandbox'ы) этот модуль не соберётся. `Kernel.Common`/`Kernel.Contracts` — собираются, `Kernel` — нет. Это архитектурно правильно: unsafe изолирован в одном модуле.

### Что этот модуль **не** делает

- Не компилирует MiniC/NANASM — это отдельные проекты (в `Extensions`/`Compiler`).
- Не знает про Scratch/змейку — это демо поверх `Emulator`.
- Не реализует `IReadOnlyLogOptimization` / `OptimizationId` — это контракты, реализации в компиляторе/оптимизаторе.
- Не содержит UI, консоли, REPL — только runtime.
- Не является потокобезопасным по умолчанию (см. выше).

Правило «куда положить тип»: **примитив без поведения → `Common`; интерфейс/контракт → `Contracts`; реализация, состояние, исполнение, нативная память → `Kernel`; точка входа для приложения → `VmApplication`.**