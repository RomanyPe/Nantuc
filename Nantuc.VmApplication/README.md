# Nantuc.VmApplication — README

> Основной API платформы: компиляция проекта в байткод, запуск VM, управление устройствами и пошаговое исполнение.  
> Единственный модуль, предназначенный для встраивания Nantuc в приложение (IDE, REPL, CI, игровой хост).  
> Зависит от `[Kernel](../Nantuc.Kernel/README.md)`, `[Kernel.Contracts](../Nantuc.Kernel.Contracts/README.md)` и `[Compiler](../Nantuc.Compiler/README.md)`.  
> `[← Корень](../README.md) · [Common](../Nantuc.Kernel.Common/README.md) · [Contracts](../Nantuc.Kernel.Contracts/README.md) · [Diagnostics](../Nantuc.Kernel.Diagnostics/README.md) · [Kernel](../Nantuc.Kernel/README.md) · [Compiler](../Nantuc.Compiler/README.md)`

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

`Nantuc.VmApplication` — фасад над двумя подсистемами: компилятором (`Nantuc.Compiler`) и рантаймом (`Nantuc.Kernel`). Все типы ядра, которые пользователю «не положено» видеть (`Processor`, `MemoryBus`, `NativeMemoryBuffer`, `PortBus`, `ManagerDevices`, `Assembler`), спрятаны за тонким слоем обёрток. Пользователь работает с четырьмя основными абстракциями:

- `VmHost` — «всё в одном»: проект (`VmHostProject`) + эмулятор (`VmEmulator`). Создаётся через `VmHostFactory.CreateDefault()`.
- `VmHostProject` — компиляция: `GetSourceFiles()` → `Compile()` / `CompileToIL()` → `CompilationResult` / `CompilationToILResult`. Настраивается через `VmHostProjectBuilder`.
- `VmEmulator` — исполнение: регистрация устройств, включая VM (`DeviceSpec`), выдача `DeviceContext`, доступ к `IPortBus`. Настраивается через `VmEmulatorBuilder`.
- `DeviceContext` — управление конкретной VM: `LoadProgram`, `Run`, `RunStepMode`/`SingleStep`, `Stop`, `ReadPort`, `GetRegistersSnapshot`, `GetLastFault`, режимы (Step/Continuous/None).

Ключевая архитектурная особенность: VM — это тоже устройство на port-шине. `VmEmulator.TryRegisterDevice(DeviceSpec, sector)` создаёт `Device` из ядра и регистрирует его как `IPortController`. Один эмулятор может держать несколько VM, и они общаются через порты, как обычные устройства (композиция VM внутри VM). `DeviceContext` — удобное окно к конкретному `Device` без прямого доступа к `Nantuc.Kernel`.

Модуль таргетит `net10.0` и включает `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`, потому что внутри вызывает `Nantuc.Kernel` (нативные буферы), но сам unsafe-кода не содержит — только `DeviceContextUnsafeExtensions` даёт escape hatch к `Device`/`PortBus`.

API следует паттерну «Try + IdState + ProcessorFault»:
- `TryRegisterDevice` / `TryGetDeviceContext` — без `!` и магических `-1..-4`.
- `IdState` — классификация возвратов `TryRegisterDevice`.
- `Run` / `DebugRun` → `ProcessorFault` — статус завершения вместо ручного `IProcessorFaultPolicy`.
- `TryLoadProgram` — безопасная загрузка без исключений.
- `GetLastFault` — доступ к последнему `ProcessorFault` без `AsRawDevice`.

## Дерево локального проекта

Namespace'ы соответствуют папкам:

```
Nantuc.VmApplication/
├── Nantuc.VmApplication.csproj        # net10.0, unsafe, ref → Compiler, Contracts, Kernel
├── VmHost.cs                           # record VmHost(Project, Emulator) : IDisposable
├── VmHostFactory.cs                    # CreateDefault(...) с optimizator
├── Default/
│   ├── DefaultFileService.cs           # IFileService на реальной ФС
│   └── RamFileService.cs               # IFileService в памяти
├── Emulator/                           (namespace Nantuc.VmApplication.Emulator)
│   ├── Abstraction/PortControlBase.cs  # abstract IPortController + Interlocked.Dispose
│   ├── DeviceContext.cs                # окно к Device: Run/Step/Load/Ports/Registers/GetLastFault
│   ├── LaunchOptions.cs                # LaunchOptions / LaunchOptionsDebug
│   ├── SnapshotRegisters.cs            # readonly struct с InlineArray[32]
│   ├── VmEmulator.cs                   # обёртка Kernel.Utilites.Emulator + DeviceSpec
│   └── VmEmulatorBuilder.cs            # fluent: WithPortBusSize / WithPortsPerDevice
├── Project/                            (namespace Nantuc.VmApplication.Project)
│   ├── CompilationResult.cs            # byte[] + метрики секций + ошибки
│   ├── CompilationToILResult.cs        # ILAssembler + Build() → CompilationResult
│   ├── IFileService.cs                 # FS-абстракция
│   ├── IProjectFilesConfig.cs          # пути + расширения; static Default
│   ├── OptimizationResultLog.cs        # Dictionary<OptimizationId, IReadOnlyLogOptimization>
│   ├── ProjectBuilder.cs               # оркестрация: parse → AST-merge → includes → generate
│   ├── SourceFile.cs                   # record struct (Name, Content, Language)
│   ├── SourceLanguage.cs               # enum { Asm, C, None }
│   ├── VmAstOptimizer.cs               # обёртка AstOptimizer
│   ├── VmCompilerOptimizator.cs        # AstOptimizer + PeepholeOptimizer + обёртки
│   ├── VmHostProject.cs                # GetSourceFiles / Compile / CompileToIL / OptimizationIL
│   ├── VmHostProjectBuilder.cs         # WithPaths / WithFileService / WithOptimizator / Build
│   └── VmPeepholeOptimizer.cs          # обёртка PeepholeOptimizer
└── Unsafe/
    └── DeviceContextUnsafeExtensions.cs # AsRawDevice() / GetRawPortBus()
```

Замечания по организации:
- `SnapshotRegisters` вынесен в отдельный файл `Emulator/SnapshotRegisters.cs`.
- `DeviceView.cs` удалён (мёртвый код).
- `VmCompilerOptimizator` свойства переименованы: `VmAstOptimizer` (было `VMAstOptimizer`) и `VmPeepholeOptimizer` (было `VMPeepholeOptimizer`).

## API

### VmHost и VmHostFactory — точка входа

```csharp
public sealed record VmHost(VmHostProject Project, VmEmulator Emulator) : IDisposable;

public static class VmHostFactory
{
    public static VmHost CreateDefault(
        IProjectFilesConfig? projectConfig = null,
        IFileService? fileService = null,
        VmCompilerOptimizator? optimizator = null,   // новое: готовые правила оптимизаций
        PortSize? portBusSize = null,
        DevicePortSize? portsPerDevice = null);
}
```

`CreateDefault` — самая простая точка входа:

```csharp
using var host = VmHostFactory.CreateDefault();
```

Опциональные параметры:
- `projectConfig` — пути и расширения (`IProjectFilesConfig.Default`).
- `fileService` — `RamFileService` по умолчанию, `DefaultFileService` — для реальной ФС.
- `optimizator` — готовый `VmCompilerOptimizator` с правилами. Если `null` — `VmHostProjectBuilder.Build()` создаст новый `VmCompilerOptimizator()` (`?? new()`).
- `portBusSize` — размер порт-пространства (`PortSize.KB64`).
- `portsPerDevice` — размер блока портов на устройство (`DevicePortSize.B16`).

`VmHost.Dispose()` освобождает только `Emulator` (внутри — `Kernel.Utilites.Emulator.Dispose`), но не `Project` — `VmHostProject` не `IDisposable`.

### VmEmulator и VmEmulatorBuilder — исполнение

```csharp
public sealed class VmEmulator(Kernel.Utilites.Emulator emulator) : IDisposable
{
    public IPortBus GetPortBus();
    public bool ChangeDeviceSector(int id, uint newSector);
    public IReadOnlyCollection<DeviceInfo> GetAllDevices();

    public int      RegisterDevice(IPortController ioDevice, string? name, uint sector);
    public int      RegisterDevice(DeviceSpec spec, uint sector);
    public IdState  TryRegisterDevice(IPortController ioDevice, string? name, uint sector, out int id);
    public IdState  TryRegisterDevice(DeviceSpec spec, uint sector, out int id);

    public DeviceContext? GetDeviceContext(int id);
    public bool           TryGetDeviceContext(int id, [NotNullWhen(true)] out DeviceContext? ctx);

    public bool UnregisterDevice(int id);
    public void Dispose();
}

public readonly record struct DeviceSpec(
    RamSize RamSize,
    string? Name,
    IProcessorFaultPolicy ProcessorFaultPolicy)
{
    public static DeviceSpec Create(
        RamSize? ramSize = null,
        string? name = null,
        IProcessorFaultPolicy? processorFaultPolicy = null);
}

public class VmEmulatorBuilder
{
    public VmEmulatorBuilder WithPortBusSize(PortSize ports);
    public VmEmulatorBuilder WithPortsPerDevice(DevicePortSize portsOnDev);
    public VmEmulator Build();
}
```

Два способа регистрации:
- `RegisterDevice` — возвращает `int` (ID при успехе, `-1..-4` при ошибке). Для кода, который уже работает с `int`.
- `TryRegisterDevice` — возвращает `IdState` (`Valid`/`NotAssigned`/`OutOfRange`/`NoFreeSlot`/`Rejected`) и `out int id`. **Рекомендуемый способ** для нового кода.

`TryGetDeviceContext` — безопасный вариант `GetDeviceContext` без `!`: возвращает `bool` и `[NotNullWhen(true)] out DeviceContext?`.

### VmHostProject и VmHostProjectBuilder — компиляция

```csharp
public sealed class VmHostProject(
    IProjectFilesConfig paths,
    IFileService fileService,
    VmCompilerOptimizator optimizator)   // новое: обязательный optimizator
{
    public static Regex AsmMnemonics { get; }
    public static Regex AsmRegisters { get; }
    public static Regex MiniCMnemonics { get; }

    public VmCompilerOptimizator Optimizator { get; }
    public IProjectFilesConfig Paths { get; }
    public IFileService FileService { get; }

    public IEnumerable<SourceFile> GetSourceFiles();

    public CompilationResult     Compile(IEnumerable<SourceFile> files, ulong baseAddress, bool optimize);
    public CompilationToILResult CompileToIL(IEnumerable<SourceFile> files, ulong baseAddress, bool optimize);
    public CompilationToILResult OptimizationIL(CompilationToILResult res, int peepholeOptimizationCount);
}

public class VmHostProjectBuilder
{
    public VmHostProjectBuilder WithPaths(IProjectFilesConfig paths);
    public VmHostProjectBuilder WithFileService(IFileService proj);
    public VmHostProjectBuilder WithOptimizator(VmCompilerOptimizator optimizator);   // новое
    public VmHostProject Build();
}
```

- `Compile` — одностадийная сборка: `Assembler` (прямой эмит) → `byte[]`.
- `CompileToIL` — двухстадийная: `ILAssembler` (список `AsmItem`) → `CompilationToILResult`, затем `OptimizationIL(result, maxPasses)` прогоняет peephole и `result.Build()` собирает байты.
- `optimize: true` — включает AST-оптимизации из `Optimizator.VmAstOptimizer`. Логи собираются в `OptimizationResultLog`.
- `GetSourceFiles` — читает все `.c`/`.minic`/`.asm`/`.nanasm` из `FileService.GetNameFiles(Paths)`.

`VmHostProject` отдаёт статические regex'ы для подсветки синтаксиса: `AsmMnemonics`, `AsmRegisters`, `MiniCMnemonics`.

### DeviceContext — управление VM

```csharp
public class DeviceContext
{
    public bool IsRunning { get; }
    public long StepCount { get; }
    public ulong RamSize { get; }
    public INativeReadOnlyBuffer NativeReadOnlyBuffer { get; }

    public void Stop();
    public void LoadProgram(ReadOnlySpan<byte> program, ulong loadAddress = ProjectBuilder.BaseAddressProgram);   // было byte[]
    public bool TryLoadProgram(ReadOnlySpan<byte> program, ulong loadAddress, [NotNullWhen(false)] out string? error);  // было TryFastLoadProgram
    public void ResetMemoryRam();

    public ProcessorFault Run(LaunchOptions? opt = null);
    public ProcessorFault DebugRun(LaunchOptionsDebug? opt = null);

    public void RunStepMode(ulong startAddress = ProjectBuilder.BaseAddressProgram, Action? onLaunch = null);
    public void SingleStep(Action<IProcessorReader>? onStep = null);
    public void MultiStep(int count = 5, Action<IProcessorReader>? onStep = null);
    public void EndStepMode();

    public byte ReadPort(ulong offset);
    public void CopyRegisters(Span<ulong> destination);
    public SnapshotRegisters GetRegistersSnapshot();
    public ProcessorFault GetLastFault();   // новое
}

public record class LaunchOptions(
    ulong StartAddress = ProjectBuilder.BaseAddressProgram,
    long? StepLimit = null,
    bool ShowTimer = false,
    Action? OnLaunch = null,
    Action<ISimulationResult?>? OnEnd = null)
{ public static LaunchOptions Default { get; } }

public record class LaunchOptionsDebug(
    ulong StartAddress = ProjectBuilder.BaseAddressProgram,
    int DelayMs = 0,
    long StepLimit = long.MaxValue,
    Action? OnLaunch = null,
    Action<IProcessorReader>? OnStep = null,
    Action? OnEnd = null)
{ public static LaunchOptionsDebug Default { get; } }
```

Изменения в этой версии:
- `LoadProgram` принимает `ReadOnlySpan<byte>` (было `byte[]`) — без аллокаций.
- `TryFastLoadProgram` → `TryLoadProgram` — имя согласовано с `Device.TryLoadProgram` в ядре.
- `GetLastFault()` — публичный доступ к `ProcessorFault`, который обновляется в `Run`/`DebugRun` (раньше только через `AsRawDevice`).

`Run` и `DebugRun` возвращают `ProcessorFault` — на успехе `BiosStatus.EndProgramm`, на падении — статус с `InstructionPointer`/`OpCode`.

Режимы (`DeviceContext.Mode` — private enum):

| Режим | Как войти | Как выйти | Что делает |
|---|---|---|---|
| `Continuous` | `Run` / `DebugRun` | автоматически после завершения | блокирующий цикл `Step()` |
| `Step` | `RunStepMode` | `EndStepMode` | ручной `SingleStep`/`MultiStep` |
| `None` | `Stop()` или завершение | — | ничего не запущено |

**`SnapshotRegisters`** — в отдельном файле, `readonly struct` с `[InlineArray(32)]`:

```csharp
public readonly struct SnapshotRegisters : IEquatable<SnapshotRegisters>
{
    public SnapshotRegisters(Span<ulong> values);
    public ulong[] AsArray();
    public ulong this[RegType r] { get; }
    public static ulong Count => 32;
    public IEnumerable<(RegType Type, ulong Value)> Enumerate();
    public bool Equals(SnapshotRegisters other);
    public override int GetHashCode();   // исправлено: HashCode builder по 32 значениям
    public static bool operator ==(SnapshotRegisters a, SnapshotRegisters b);
    public static bool operator !=(SnapshotRegisters a, SnapshotRegisters b);
}
```

### Результаты компиляции

```csharp
public readonly struct CompilationResult
{
    public CompilationResult(AssemblerBase asm, OptimizationResultLog? log);
    public CompilationResult(IReadOnlyList<string>? errors);

    public byte[]? Program { get; }
    public ulong StartAdress { get; }
    public int CodeSize { get; }
    public int DataStart { get; }
    public int DataSize { get; }
    public OptimizationResultLog? OptimizationResultLog { get; }
    public IReadOnlyList<string>? Errors { get; }
    public bool Success => Program != null && (Errors == null || Errors.Count == 0);
}
```

`Success` теперь требует `Program != null` — раньше `CompilationResult(new List<string>())` давал `Success == true` при `Program == null`, что приводило к NRE в `TryLoadProgram`.

```csharp
public readonly record struct CompilationToILResult
{
    internal readonly ILAssembler? Assembler;
    public IReadOnlyList<string>? Errors { get; }
    public OptimizationResultLog? OptimizationResultLog { get; }

    public CompilationToILResult(IReadOnlyList<string>? errors);
    public CompilationToILResult(ILAssembler? assembler, OptimizationResultLog? optimizationResultLog);   // было internal
    public CompilationToILResult(ILAssembler? assembler, OptimizationResultLog? optimizationResultLog, IReadOnlyList<string>? errors);

    public CompilationResult Build();
}
```

Конструктор `(ILAssembler?, OptimizationResultLog?)` теперь публичный — позволяет создавать результат вручную.

### Файловые сервисы

```csharp
public interface IFileService
{
    IEnumerable<string> GetNameFiles(IProjectFilesConfig path);
    string ReadFile(string fileName);
    bool Exist(string fileName);
    void SaveFile(string fileName, string content);
    string Combine(string path1, string path2);
}
```

- `DefaultFileService` — реальная ФС.
- `RamFileService` — in-memory. **`Combine` теперь `Path.Combine`** (было `"\\"`), `GetNameFiles`/`Ext` — `StringComparison.Ordinal`.

`IProjectFilesConfig` — без изменений: `BaseDirectory`, `ProjectPath`, `IncludePath`, `ExtensionsAsm`, `ExtensionsMiniC`, `static Default`.

### Оптимизаторы

```csharp
public sealed class VmCompilerOptimizator
{
    public VmAstOptimizer VmAstOptimizer { get; }          // было VMAstOptimizer
    public VmPeepholeOptimizer VmPeepholeOptimizer { get; } // было VMPeepholeOptimizer
}

public sealed class VmAstOptimizer
{
    public int Count { get; }
    public void Add(IAstOptimizationRule rule);
    public void Insert(int index, IAstOptimizationRule item);
    public void Remove(IAstOptimizationRule item);
    public void RemoveAt(int index);
    public OptimizationId GetType(int i);
}

public sealed class VmPeepholeOptimizer
{
    public int Count { get; }
    public void Add(IPeepholeRule rule);
    public void Insert(int index, IPeepholeRule item);
    public void Remove(IPeepholeRule item);
    public void RemoveAt(int index);
}
```

`VmCompilerOptimizator` — контейнер, создаётся в `VmHostFactory.CreateDefault` (если не передан) и доступен через `host.Project.Optimizator`. Оба списка правил пустые по умолчанию.

### Unsafe extensions

```csharp
public static class DeviceContextUnsafeExtensions
{
    extension(DeviceContext context)
    {
        public Device AsRawDevice();
    }
}

public static class VmEmulatorUnsafeExtension
{
    extension(VmEmulator vm)
    {
        public PortBus GetRawPortBus();
    }
}
```

Для случаев, когда обёртки не хватает: прямой доступ к `Device`/`PortBus`. Использовать осторожно: после `AsRawDevice()` вы держите ссылку на `Device`, и его `Dispose` уже не контролируется `VmEmulator`.

## Примеры для API

**Полный цикл с безопасным API:**

```csharp
using Nantuc.Kernel.Common;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Project;

using var host = VmHostFactory.CreateDefault();

var sources = host.Project.GetSourceFiles().ToList();
var compilation = host.Project.Compile(sources, baseAddress: 0, optimize: true);
if (!compilation.Success)
{
    foreach (var err in compilation.Errors!)
        Console.Error.WriteLine(err);
    return;
}

var registerState = host.Emulator.TryRegisterDevice(
    DeviceSpec.Create(ramSize: RamSize.KB64, name: "main"),
    sector: 0,
    out int deviceId);
if (registerState != IdState.Valid)
{
    Console.Error.WriteLine($"RegisterDevice failed: {registerState} (id={deviceId})");
    return;
}

if (!host.Emulator.TryGetDeviceContext(deviceId, out var vm))
{
    Console.Error.WriteLine("DeviceContext not available");
    return;
}

if (!vm.TryLoadProgram(compilation.Program!, loadAddress: 0, out var error))
{
    Console.Error.WriteLine(error);
    return;
}

ProcessorFault fault = vm.Run(new LaunchOptions(ShowTimer: true,
    OnEnd: r => Console.WriteLine($"steps={r?.Steps} elapsed={r?.Elapsed}")));

if (fault.Status != BiosStatus.EndProgramm)
    Console.Error.WriteLine($"VM fault: {fault.Status} at IP=0x{fault.InstructionPointer:X}");

// Последний fault можно получить и после Run
ProcessorFault lastFault = vm.GetLastFault();
```

**Компиляция через IL с оптимизациями:**

```csharp
var sources = host.Project.GetSourceFiles();
var il = host.Project.CompileToIL(sources, baseAddress: 0, optimize: true);

il = host.Project.OptimizationIL(il, peepholeOptimizationCount: 5);

var result = il.Build();
if (result.Success)
    File.WriteAllBytes("out.nbin", result.Program!);

if (result.OptimizationResultLog is { } logs)
    foreach (var (id, log) in logs.Logs)
        foreach (var e in log.GetLogs())
            Console.WriteLine($"[{id}] pass {e.Pass}: {e.Message}");
```

**In-memory сценарий:**

```csharp
using Nantuc.VmApplication.Default;

var fs = new RamFileService();
fs.SaveFile("main.c", "int main() { int x = 21; return x * 2; }");

using var host = VmHostFactory.CreateDefault(fileService: fs);

var result = host.Project.Compile(host.Project.GetSourceFiles(), 0, optimize: false);
```

**Пошаговое исполнение:**

```csharp
if (!host.Emulator.TryGetDeviceContext(deviceId, out var vm)) return;
vm.LoadProgram(compilation.Program!);   // ReadOnlySpan<byte> — работает и с byte[]
vm.RunStepMode(startAddress: 0);

for (int i = 0; i < 20 && vm.IsRunning; i++)
    vm.SingleStep(r => Console.WriteLine(
        $"IP=0x{r.GetRegister(RegType.rIP):X} r0={r.GetRegister(RegType.r0)}"));

vm.EndStepMode();
```

**Снимок регистров:**

```csharp
SnapshotRegisters before = vm.GetRegistersSnapshot();
vm.MultiStep(count: 10);
SnapshotRegisters after = vm.GetRegistersSnapshot();

if (before != after)
    Console.WriteLine($"IP: 0x{before[RegType.rIP]:X} -> 0x{after[RegType.rIP]:X}");
```

**Регистрация собственного устройства (UART):**

```csharp
using Nantuc.VmApplication.Emulator.Abstraction;

public sealed class UartController : PortControlBase
{
    private byte _tx;
    public override byte ReadPort(ulong offset) => offset == 0 ? _tx : (byte)0;
    public override void WritePort(ulong offset, byte value) { if (offset == 0) _tx = value; }
    public override void WakeProcessor() { }
}

using var host = VmHostFactory.CreateDefault();
var uart = new UartController();
var state = host.Emulator.TryRegisterDevice(uart, name: "UART0", sector: 1, out _);
```

**Своё AST-правило оптимизации:**

```csharp
using Nantuc.Compiler.C.Optimizators;
using Nantuc.Kernel.Contracts;

public sealed class MyRule : IAstOptimizationRule
{
    public OptimizationId Type => new("my.rule");
    private readonly OptimizationResult _log = new(new OptimizationId("my.rule"));

    public IReadOnlyLogOptimization Optimize(ProgramNode program)
    {
        _log.Clear();
        // обход program, изменения AST
        return _log;
    }
}

var optimizator = new VmCompilerOptimizator();
optimizator.VmAstOptimizer.Add(new MyRule());

using var host = VmHostFactory.CreateDefault(optimizator: optimizator);
```

## Приближенность модуля к пользователю

Максимальная. Это основной API платформы. Пользователь MiniC/NANASM после компиляции работает именно с `DeviceContext`/`VmHost`, а не с ядром напрямую.

**Кто и что использует:**
- Конечный пользователь языка — запускает готовый `.nbin` через `DeviceContext.Run`/`RunStepMode`/`DebugRun`.
- Автор IDE/REPL/CI — `VmHostProject.Compile`/`CompileToIL`, `GetSourceFiles`, `OptimizationResultLog`, `SnapshotRegisters`.
- Автор игры/приложения — `VmHostFactory.CreateDefault`, `DeviceContext`, `PortControlBase`, `LaunchOptions`.
- Автор расширений к компилятору — `VmAstOptimizer.Add`/`VmPeepholeOptimizer.Add`.

**Что стоит делать:**
- Всегда `using var host = VmHostFactory.CreateDefault(...)` — `Dispose` освобождает нативные буферы всех VM.
- Проверять `compilation.Success` перед `TryLoadProgram` — `Success` также требует `Program != null`.
- Использовать `TryRegisterDevice` (возвращает `IdState` + `out int id`) вместо `RegisterDevice` — единая диагностика.
- Использовать `TryGetDeviceContext` вместо `GetDeviceContext(id)!`.
- Для больших программ — `TryLoadProgram` (NativeMemory.Copy), а не `LoadProgram` (посимвольно).
- Читать `ProcessorFault` из `Run`/`DebugRun` или через `GetLastFault()` — это правильный способ узнать статус завершения.
- Для WASM/браузера — `RamFileService` + `CompileToIL` + `Build`, без `DefaultFileService`.

**Что не стоит делать:**
- Создавать несколько `VmHost` в одном процессе без необходимости — каждый держит свой `PortBus` и `ManagerDevices`.
- Держать `DeviceContext` после `host.Dispose()` — внутри лежит `Device`, который уже освобождён.
- Вызывать `Run` без остановки предыдущего прогона — `ThrowByMode` бросит `InvalidOperationException`.
- Использовать `DeviceContextUnsafeExtensions.AsRawDevice()` без острой необходимости.
- Полагаться на `GetLastFault()` как «результат последнего шага» — `_faultData` не обновляется каждую итерацию (для оптимизации) и не сбрасывается в `LaunchProgramm`. См. [Особенная информация](#особенная-информация).

## Что исправлено в этой версии

По сравнению с прошлой фиксацией `Nantuc.VmApplication`:

- **`VmHostProjectBuilder` — добавлены `_compilerOptimizator` и `WithOptimizator(...)`.** Раньше `VmHostFactory.cs` вызывал несуществующий `WithOptimizator`, а `Build()` передавал 2 аргумента в 3-аргументный конструктор `VmHostProject` — модуль не собирался. Сейчас `Build()` передаёт `_compilerOptimizator ?? new()`.
- **`DeviceContext.LoadProgram`** — принимает `ReadOnlySpan<byte>` вместо `byte[]`.
- **`TryFastLoadProgram` → `TryLoadProgram`** — имя согласовано с `Device.TryLoadProgram` в ядре.
- **`DeviceContext.GetLastFault()`** — публичный доступ к `ProcessorFault` (раньше только через `AsRawDevice`).
- **`SnapshotRegisters`** вынесен в отдельный файл `Emulator/SnapshotRegisters.cs`.
- **`SnapshotRegisters.GetHashCode`** — исправлен: `HashCode` builder по 32 значениям, согласован с `Equals`.
- **`CompilationResult.Success`** требует `Program != null` — раньше `Success == true` при `Program == null` для пустого списка ошибок.
- **`CompilationToILResult(ILAssembler?, OptimizationResultLog?)`** — конструктор стал публичным.
- **`ProjectBuilder.ReadInclude`** для конфликта путей бросает `CodeException(ErrorCode.Parser_IncludeAmbiguous, ...)` через `ThrowCompiler.Throw` (было `InvalidOperationException`).
- **`VmHostProject.CompileInternal`** на ошибке возвращает `res` (лог) и `ex.ToString()` (со stack trace) вместо `null` и `ex.Message`.
- **`VmHostProject.OptimizationIL`** на ошибке сохраняет `OptimizationResultLog` из исходного результата.
- **`VmCompilerOptimizator`** — свойства переименованы: `VMAstOptimizer` → `VmAstOptimizer`, `VMPeepholeOptimizer` → `VmPeepholeOptimizer`.
- **`VmHostFactory.CreateDefault`** — новый параметр `VmCompilerOptimizator? optimizator = null`.
- **`VmHostProject`** — конструктор с обязательным `VmCompilerOptimizator`.
- **`RamFileService.Combine`** — `Path.Combine` вместо `"\\"`, `GetNameFiles`/`Ext` — `StringComparison.Ordinal`.
- **`DeviceView`** удалён.
- **XML-документация** добавлена на все публичные члены (ранее отмеченные как отсутствующие).

## Особенная информация

### Почему VM — это устройство

`VmEmulator.TryRegisterDevice(DeviceSpec, sector)` создаёт `Kernel.BiosSystem.Device` и регистрирует его как `IPortController` в `PortBus`. `Device` реализует `IPortController` (для внешней port-шины) и держит собственный `PortBus` (внутренняя шина VM). Внешняя VM может слать `OUT`/`IN` по сектору внутренней VM — и наоборот. `DeviceContext.ReadPort(offset)` читает собственные порты VM.

### Режимы `DeviceContext`

Внутри `DeviceContext` — `private enum Mode { Step, Continuous, None }` и `volatile Mode _mode`:
- `Run`/`DebugRun` → `ThrowByMode(Continuous)` → блокирующий цикл → `SetNoneMode()`.
- `RunStepMode` → `ThrowByMode(Step)` → `BreakPointerLaunchDevice`.
- `SingleStep`/`MultiStep` → `ThrowIfNotStepMode` → `Device.NextStepProcessor`/`NextStepProcessorCount`.
- `EndStepMode` → `ThrowIfNotStepMode` → `Device.Stop()` + один `NextStepProcessor(null)` + `SetNoneMode()`.
- `Stop` → `Device.Stop()` + `SetNoneMode()` — можно вызвать из любого режима.

`Stop` можно вызвать из другого потока, `Run`/`Step` — однопоточные.

### Байткод: `baseAddress` и `StartAdress`

`CompilationResult.StartAdress` — это `baseAddress`, переданный в `Compile`/`CompileToIL`. По умолчанию `ProjectBuilder.BaseAddressProgram = 0x00`. `Compile` не выставляет `StartAdress` в `LoadProgram` автоматически — `LoadProgram` копирует байты по `loadAddress`, а `LaunchOptions.StartAddress` — откуда начать исполнение.

### Include resolution

`ProjectBuilder.Build` резолвит `#include "file"` из двух мест: `Paths.ProjectPath` и `Paths.IncludePath`. Если файл есть и там, и там — `CodeException(ErrorCode.Parser_IncludeAmbiguous, ...)` (для нового кода); `FileNotFoundException`, если файл не найден нигде. Включаемые `.c`/`.minic` парсятся в AST, `.asm`/`.nanasm` ассемблируются напрямую (с проверкой `processed` от циклов).

### Thread-safety

`DeviceContext` не потокобезопасен, кроме `Stop`. `SnapshotRegisters` — `readonly struct` с копией регистров. `VmHostProject.Compile`/`CompileToIL` — не потокобезопасны (общий `_optimizator.AstOptimizer`). `VmEmulator.GetDeviceContext`/`RegisterDevice`/`UnregisterDevice` — не потокобезопасны.

### `ProcessorFault` из `Run`/`DebugRun`/`GetLastFault`

`Run` и `DebugRun` возвращают `ProcessorFault` из `_device.Run(...)`/`_device.DebugRun(...)`. `GetLastFault()` — публичный доступ к `_faultData` в `Processor`. Важно: `_faultData` не сбрасывается в `LaunchProgramm` (`[Nantuc.Kernel](../Nantuc.Kernel/README.md)`, [Что исправить, п.2](../Nantuc.Kernel/README.md#что-исправить)) — второй `Run` может вернуть fault от первого. `XML-doc` на `GetLastFault` это отражает: «Не обновляется каждую итерацию ради оптимизаций».

### Ограничения платформ

- `net10.0`, `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` — из-за `Kernel`, но в самом модуле unsafe-кода нет.
- `params ReadOnlySpan<object>` в `ThrowCompiler` (Diagnostics) требует `net9.0+`.
- `[InlineArray]` в `SnapshotRegisters` — C# 12+, работает в AOT/WASM.
- `DefaultFileService` использует `Directory.EnumerateFiles`/`File.ReadAllText` — в WASM работает, но медленно. Для WASM — `RamFileService`.
- `Thread.Sleep(delay)` в `DeviceContext.DebugRun` (через `Device.DebugRun`) — в WASM либо no-op, либо `PlatformNotSupportedException`.

## Что исправить

По убыванию важности. Ссылки на файлы и методы.


10. **`VmEmulator` — `using System.Xml.Linq;` (неиспользуемый).**  
    Файл: `Emulator/VmEmulator.cs`. Не используется.  
    Фикс: удалить `using`.

11. **`DeviceContext.ThrowIfRunning` — сообщение «launch program» в `LoadProgram`.**  
    Файл: `Emulator/DeviceContext.cs`, `LoadProgram` вызывает `ThrowIfRunning("Cannot load program while device is running. Call Stop() first.")` — здесь сообщение переопределено правильно. Но `ResetMemoryRam` вызывает `ThrowIfRunning()` с дефолтным `"Cannot launch program while device is running..."` — при попытке очистить RAM сообщение говорит «launch».  
    Фикс: `ResetMemoryRam` → `ThrowIfRunning("Cannot clear RAM while device is running. Call Stop() first.")`.

12. **`VmHostProject.AsmMnemonics` / `AsmRegisters` / `MiniCMnemonics` — XML-doc обрывается.**  
    Файл: `Project/VmHostProject.cs`. Комментарии: `/// <summary><see cref="Regex"/> регистров NANASM </summary` — незакрытый `</summary>`.  
    Фикс: `</summary>` вместо `</summary` (без `>`).

13. **`DeviceSpec` — нет XML-doc на самом record struct.**  
    Файл: `Emulator/VmEmulator.cs`, `public readonly record struct DeviceSpec(...)`. Есть только `DeviceSpec.Create(...)` с `///`. Сам тип без `///` — непонятно, что это «паспорт устройства».  
    Рекомендация: `/// <summary>Параметры для создания Nantuc-устройства через <see cref="VmEmulator.RegisterDevice(DeviceSpec, uint)"/>.</summary>`.

14. **`PortControlBase` — `/// <summary>Асбстрактный класс</summary>` (опечатка).**  
    Файл: `Emulator/Abstraction/PortControlBase.cs`. `Асбстрактный` — должно быть `Абстрактный`.  
    Фикс: исправить.