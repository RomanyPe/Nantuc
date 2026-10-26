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
- [Особенная информация](#особенная-информация)
- [Что исправить](#что-исправить)

## Об этом модуле

`Nantuc.VmApplication` — это **фасад над двумя подсистемами**: компилятором (`Nantuc.Compiler`) и рантаймом (`Nantuc.Kernel`). Все типы ядра, которые пользователю «не положено» видеть (`Processor`, `MemoryBus`, `NativeMemoryBuffer`, `PortBus`, `ManagerDevices`, `Assembler`), спрятаны за тонким слоем обёрток. Пользователь работает с четырьмя основными абстракциями:

- **`VmHost`** — «всё в одном»: проект (`VmHostProject`) + эмулятор (`VmEmulator`). Создаётся через `VmHostFactory.CreateDefault()`.
- **`VmHostProject`** — компиляция: `GetSourceFiles()` → `Compile()` / `CompileToIL()` → `CompilationResult` / `CompilationToILResult`. Настраивается через `VmHostProjectBuilder`.
- **`VmEmulator`** — исполнение: регистрация устройств, включая VM (`DeviceSpec`), выдача `DeviceContext`, доступ к `IPortBus`. Настраивается через `VmEmulatorBuilder`.
- **`DeviceContext`** — управление конкретной VM: `LoadProgram`, `Run`, `RunStepMode`/`SingleStep`, `Stop`, `ReadPort`, `GetRegistersSnapshot`, режимы (Step/Continuous/None).

Ключевая архитектурная особенность: **VM — это тоже устройство на port-шине**. `VmEmulator.RegisterDevice(DeviceSpec, sector)` создаёт `Device` из ядра и регистрирует его как `IPortController`. Один эмулятор может держать несколько VM, и они общаются через порты, как обычные устройства (композиция VM внутри VM). `DeviceContext` — это удобное окно к конкретному `Device` без прямого доступа к `Nantuc.Kernel`.

Проект таргетит `net10.0` и включает `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`, потому что внутри вызывает `Nantuc.Kernel` (нативные буферы), но сам unsafe-кода не содержит — только в extension-методах `DeviceContextUnsafeExtensions` даёт escape hatch к `Device`/`PortBus`.

## Дерево локального проекта

Namespace'ы соответствуют папкам:

```
Nantuc.VmApplication/
├── Nantuc.VmApplication.csproj        # net10.0, unsafe, ref → Compiler, Contracts, Kernel
├── VmHost.cs                           # record VmHost(Project, Emulator) : IDisposable
├── VmHostFactory.cs                    # CreateDefault(...)
├── Default/
│   ├── DefaultFileService.cs           # IFileService на реальной ФС
│   └── RamFileService.cs               # IFileService в памяти
├── Emulator/                           (namespace Nantuc.VmApplication.Emulator)
│   ├── Abstraction/PortControlBase.cs  # abstract IPortController + Interlocked.Dispose
│   ├── DeviceContext.cs                # окно к Device: Run/Step/Load/Ports/Registers
│   │                                   # + SnapshotRegisters (InlineArray)
│   ├── DeviceView.cs                   # readonly struct (id, sector, name)
│   ├── LaunchModeDevice.cs             # LaunchOptions / LaunchOptionsDebug
│   ├── VmEmulator.cs                   # обёртка Kernel.Utilites.Emulator + DeviceSpec
│   └── VmEmulatorBuilder.cs            # fluent: WithPortBusSize / WithPortsPerDevice
├── Project/                            (namespace Nantuc.VmApplication.Project)
│   ├── CompilationResult.cs            # byte[] + метрики секций + ошибки
│   ├── CompilationToILResult.cs        # IRAssembler + Build() → CompilationResult
│   ├── IFileService.cs                 # FS-абстракция
│   ├── IProjectFilesConfig.cs          # пути + расширения; static Default
│   ├── OptimizationResultLog.cs        # Dictionary<OptimizationId, IReadOnlyLogOptimization>
│   ├── ProjectBuilder.cs               # оркестрация: parse → AST-merge → includes → generate
│   ├── SourceFile.cs                   # record struct (Name, Content, Language)
│   ├── SourceLanguage.cs               # enum { Asm, C, None }
│   ├── VmAstOptimizer.cs               # обёртка AstOptimizer
│   ├── VmCompilerOptimizator.cs        # AstOptimizer + PeepholeOptimizer + обёртки
│   ├── VmHostProject.cs                # GetSourceFiles / Compile / CompileToIL / OptimizationIL
│   ├── VmHostProjectBuilder.cs         # WithPaths / WithFileService / Build
│   └── VmPeepholeOptimizer.cs          # обёртка PeepholeOptimizer
└── Unsafe/
    └── DeviceContextUnsafeExtensions.cs # AsRawDevice() / GetRawPortBus()
```

Замечание: `Emulator/DeviceContext.cs` содержит и `DeviceContext`, и `SnapshotRegisters`. Для навигации это неудобно (grep `SnapshotRegisters` ведёт в файл с другим именем).

## API

### VmHost и VmHostFactory — точка входа

```csharp
public sealed record VmHost(VmHostProject Project, VmEmulator Emulator) : IDisposable;

public static class VmHostFactory
{
    public static VmHost CreateDefault(
        IProjectFilesConfig? projectConfig = null,
        IFileService? fileService = null,
        PortSize? portBusSize = null,
        DevicePortSize? portsPerDevice = null);
}
```

`CreateDefault` — самая простая точка входа:

```csharp
using var host = VmHostFactory.CreateDefault();
// host.Project — компилятор, host.Emulator — рантайм
```

`VmHost.Dispose()` освобождает только `Emulator` (внутри — `Kernel.Utilites.Emulator.Dispose`), но **не** `Project` — `VmHostProject` не `IDisposable`. `Device`, зарегистрированные в `Emulator`, освободятся через `ManagerDevices.Dispose`.

Опциональные параметры:
- `projectConfig` — пути и расширения (`IProjectFilesConfig.Default` по умолчанию).
- `fileService` — `RamFileService` по умолчанию (для тестов и песочниц), `DefaultFileService` — для реальной ФС.
- `portBusSize` — размер порт-пространства (`PortSize.KB64` по умолчанию).
- `portsPerDevice` — размер блока портов на устройство (`DevicePortSize.B16` по умолчанию).

### VmEmulator и VmEmulatorBuilder — исполнение

```csharp
public sealed class VmEmulator(Kernel.Utilites.Emulator emulator) : IDisposable
{
    public IPortBus GetPortBus();
    public DeviceContext? GetDeviceContext(int id);
    public bool ChangeDeviceSector(int id, uint newSector);
    public IReadOnlyCollection<DeviceInfo> GetAllDevices();

    public int RegisterDevice(IPortController ioDevice, string? name, uint sector);
    public int RegisterDevice(DeviceSpec handle, uint sector);
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

- `GetPortBus()` возвращает `IPortBus` (из Contracts) — интерфейс, не класс. Для низкоуровневого `PortBus` — `DeviceContextUnsafeExtensions.GetRawPortBus()`.
- `RegisterDevice(DeviceSpec, sector)` — создаёт **новую VM** через `Kernel.Utilites.Emulator.CreateDevice` и регистрирует её как устройство. Это и есть основной способ получить работающую VM.
- `RegisterDevice(IPortController, ...)` — регистрирует готовое устройство (для UART, таймеров и т.д.).
- `GetDeviceContext(id)` возвращает `null`, если `id` невалиден или устройство не является `Device` (не VM).
- `DeviceSpec.Create` даёт дефолты: `RamSize.MB1`, `null` имя, `IProcessorFaultPolicy.Default`.

### VmHostProject и VmHostProjectBuilder — компиляция

```csharp
public sealed class VmHostProject(IProjectFilesConfig paths, IFileService fileService)
{
    public static Regex AsmMnemonics { get; }
    public static Regex AsmRegisters { get; }
    public static Regex MiniCMnemonics { get; }

    public VmCompilerOptimizator Optimizator { get; }
    public IProjectFilesConfig Paths { get; }
    public IFileService FileService { get; }

    public IEnumerable<SourceFile> GetSourceFiles();

    public CompilationResult    Compile(IEnumerable<SourceFile> files, ulong baseAddress, bool optimize);
    public CompilationToILResult CompileToIL(IEnumerable<SourceFile> files, ulong baseAddress, bool optimize);
    public CompilationToILResult OptimizationIL(CompilationToILResult res, int peepholeOptimizationCount);
}

public class VmHostProjectBuilder
{
    public VmHostProjectBuilder WithPaths(IProjectFilesConfig paths);
    public VmHostProjectBuilder WithFileService(IFileService service);
    public VmHostProject Build();
}
```

- `Compile` — одностадийная сборка: `Assembler` (прямой эмит) → `byte[]`. Быстрее, но без peephole.
- `CompileToIL` — двухстадийная: `IRAssembler` (список `AsmItem`) → `CompilationToILResult`, затем `OptimizationIL(result, maxPasses)` прогоняет peephole и `result.Build()` собирает байты. Даёт логи оптимизаций и позволяет применить свои правила.
- `optimize: true` — включает AST-оптимизации, зарегистрированные в `Optimizator.VMAstOptimizer`. Логи собираются в `OptimizationResultLog`.
- `baseAddress` — обычно `ProjectBuilder.BaseAddressProgram` (0x00). Смещение, с которого загружается байткод; на него же указывает `StartAdress` в результате.
- `GetSourceFiles` — читает все `.c`/`.minic`/`.asm`/`.nanasm` из `FileService.GetNameFiles(Paths)` и возвращает `SourceFile[]`. Включает (`#include`) разрешаются автоматически в `ProjectBuilder.Build`.

`VmHostProject` также отдаёт статические regex'ы для подсветки синтаксиса в IDE: `AsmMnemonics`, `AsmRegisters`, `MiniCMnemonics`. Это ссылки на `AsmLanguageDefinition` / `MiniCLanguageDefinition` из компилятора.

### DeviceContext — управление VM

```csharp
public class DeviceContext
{
    public bool IsRunning { get; }
    public long StepCount { get; }
    public ulong RamSize { get; }
    public INativeReadOnlyBuffer NativeReadOnlyBuffer { get; }

    public void Stop();
    public void LoadProgram(byte[] program, ulong loadAddress = ProjectBuilder.BaseAddressProgram);
    public bool TryFastLoadProgram(ReadOnlySpan<byte> program, ulong loadAddress,
                                   [NotNullWhen(false)] out string? error);
    public void ResetMemoryRam();

    public void Run(LaunchOptions? opt = null);
    public void RunWithLimit(LaunchOptions? opt = null, long stepLimit = long.MaxValue);
    public void DebugRun(LaunchOptionsDebug? opt = null);

    public void RunStepMode(ulong startAddress = ProjectBuilder.BaseAddressProgram, Action? onLaunch = null);
    public void SingleStep(Action<IProcessorReader>? onStep = null);
    public void MultiStep(int count = 5, Action<IProcessorReader>? onStep = null);
    public void EndStepMode();

    public byte ReadPort(ulong offset);
    public void CopyRegisters(Span<ulong> destination);
    public SnapshotRegisters GetRegistersSnapshot();
}

public record class LaunchOptions(
    ulong StartAddress = ProjectBuilder.BaseAddressProgram,
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

Режимы (`DeviceContext.Mode` — private enum):

| Режим | Как войти | Как выйти | Что делает |
|---|---|---|---|
| `Continuous` | `Run` / `RunWithLimit` / `DebugRun` | автоматически после завершения | блокирующий цикл `Step()` |
| `Step` | `RunStepMode` | `EndStepMode` | ручной `SingleStep`/`MultiStep` |
| `None` | `Stop()` или завершение | — | ничего не запущено |

Правила режимов:
- Нельзя вызвать `Run`/`DebugRun`/`RunStepMode`, пока `IsRunning == true` — `ThrowIfRunning`.
- Нельзя сменить режим (Step ↔ Continuous) без `Stop()`/`EndStepMode` — `ThrowByMode` бросит `InvalidOperationException`.
- `SingleStep`/`MultiStep`/`EndStepMode` работают только в режиме Step — `ThrowIfNotStepMode`.
- `LoadProgram`/`TryFastLoadProgram`/`ResetMemoryRam` требуют `IsRunning == false`.

`RunWithLimit` — цикл останавливается по `stepLimit` (через `Stop()` внутри `Device.RunWithLimit`). `long.MaxValue` = без лимита.

`DebugRun` — цикл с `onStep`-колбэком, опциональным `Thread.Sleep(delay)` и `stepLimit`. `stepLimit` также через `Stop()`.

**`SnapshotRegisters`** — `readonly struct` с `[InlineArray(32)]`, снимок 32 регистров:

```csharp
public readonly struct SnapshotRegisters : IEquatable<SnapshotRegisters>
{
    public SnapshotRegisters(Span<ulong> values);
    public ulong[] AsArray();
    public ulong this[RegType r] { get; }
    public static ulong Count => 32;
    public IEnumerable<(RegType Type, ulong Value)> Enumerate();
    public bool Equals(SnapshotRegisters other);
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
    public bool Success => Errors == null || Errors.Count == 0;
}

public readonly record struct CompilationToILResult(
    IReadOnlyList<string>? Errors,
    OptimizationResultLog? OptimizationResultLog)
{
    public CompilationResult Build();
}
```

- `CompilationResult.Program` — готовый байткод, `null` если была ошибка.
- `StartAdress` — тот же `baseAddress`, что передали в `Compile`.
- `CodeSize`/`DataStart`/`DataSize` — метрики секций, полезны для дизассемблера и профилирования.
- `CompilationToILResult` хранит **не** байты, а `IRAssembler` (internal-поле) — `Build()` собирает байты. Это позволяет прогнать `OptimizationIL` между `CompileToIL` и `Build`.

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

- **`DefaultFileService`** — реальная ФС: `Directory.EnumerateFiles(ProjectPath, "*.*", AllDirectories)`, фильтрация по расширениям.
- **`RamFileService`** — in-memory `Dictionary<string, string>`. Для тестов, WASM-площадок и REPL без доступа к ФС. Добавлять файлы — через `SaveFile`.

`IProjectFilesConfig` описывает пути:

```csharp
public interface IProjectFilesConfig
{
    string BaseDirectory { get; }
    string ProjectPath { get; }
    string IncludePath { get; }
    IReadOnlyList<string> ExtensionsAsm { get; }
    IReadOnlyList<string> ExtensionsMiniC { get; }

    public static IProjectFilesConfig Default { get; }
}
```

`Default` — `AppContext.BaseDirectory`, `project/`, `include/`, `[.asm, .nanasm]`, `[.c, .minic]`.

### Оптимизаторы

```csharp
public sealed class VmCompilerOptimizator
{
    public VmAstOptimizer VMAstOptimizer { get; }
    public VmPeepholeOptimizer VMPeepholeOptimizer { get; }
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

`VmCompilerOptimizator` — просто контейнер. Создаётся в `VmHostProject` и доступен через `host.Project.Optimizator`. Оба списка правил — **пустые** по умолчанию: без явного `Add` оптимизации не применяются.

### Unsafe extensions

```csharp
namespace Nantuc.VmApplication.Unsafe;

public static class DeviceContextUnsafeExtensions
{
    extension(DeviceContext context)
    {
        public Device AsRawDevice();          // → Nantuc.Kernel.BiosSystem.Device
    }
}

public static class VmEmulatorUnsafeExtension
{
    extension(VmEmulator vm)
    {
        public PortBus GetRawPortBus();       // → Nantuc.Kernel.ControllersData.PortBus
    }
}
```

Для случаев, когда обёртки не хватает: прямой доступ к `Device` (например, `DebugRun` с кастомной логикой) или к `PortBus` (например, регистрация устройства по сектору в обход `ManagerDevices`). Использовать осторожно: после `AsRawDevice()` вы держите ссылку на `Device`, и его `Dispose` уже не контролируется `VmEmulator`.

## Примеры для API

**Полный цикл: источники → байткод → запуск.**

```csharp
using Nantuc.Kernel.Common;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Emulator;

using var host = VmHostFactory.CreateDefault();

// 1. Собрать исходники (реальная ФС)
var sources = host.Project.GetSourceFiles().ToList();

// 2. Скомпилировать
var compilation = host.Project.Compile(sources, baseAddress: 0, optimize: true);
if (!compilation.Success)
{
    foreach (var err in compilation.Errors!)
        Console.Error.WriteLine(err);
    return;
}

// 3. Создать VM и зарегистрировать её как устройство на sector 0
var spec = DeviceSpec.Create(ramSize: RamSize.KB64, name: "main");
int deviceId = host.Emulator.RegisterDevice(spec, sector: 0);

// 4. Получить контекст и запустить
var vm = host.Emulator.GetDeviceContext(deviceId)!;
if (!vm.TryFastLoadProgram(compilation.Program!, loadAddress: 0, out var error))
{
    Console.Error.WriteLine(error);
    return;
}

vm.Run(new LaunchOptions(ShowTimer: true,
    OnEnd: r => Console.WriteLine($"steps={r?.Steps} elapsed={r?.Elapsed}")));
```

**Компиляция через IL с оптимизациями.**

```csharp
using Nantuc.VmApplication.Project;

var sources = host.Project.GetSourceFiles();
var il = host.Project.CompileToIL(sources, baseAddress: 0, optimize: true);

// peephole: до 5 проходов
il = host.Project.OptimizationIL(il, peepholeOptimizationCount: 5);

var result = il.Build();
if (result.Success)
    File.WriteAllBytes("out.nbin", result.Program!);

// Логи оптимизаций
if (result.OptimizationResultLog is { } logs)
    foreach (var (id, log) in logs.Logs)
        foreach (var e in log.GetLogs())
            Console.WriteLine($"[{id}] pass {e.Pass}: {e.Message}");
```

**In-memory сценарий (без реальной ФС).**

```csharp
using Nantuc.VmApplication.Default;

var fs = new RamFileService();
fs.SaveFile("main.c", """
    int main() {
        int x = 21;
        return x * 2;
    }
    """);

using var host = VmHostFactory.CreateDefault(fileService: fs);
var sources = host.Project.GetSourceFiles();
var result = host.Project.Compile(sources, 0, optimize: false);
```

**Пошаговое исполнение.**

```csharp
var vm = host.Emulator.GetDeviceContext(deviceId)!;
vm.LoadProgram(compilation.Program!);
vm.RunStepMode(startAddress: 0);

for (int i = 0; i < 20 && vm.IsRunning; i++)
    vm.SingleStep(r => Console.WriteLine(
        $"IP=0x{r.GetRegister(RegType.rIP):X} r0={r.GetRegister(RegType.r0)}"));

vm.EndStepMode();
```

**Снимок регистров и сравнение.**

```csharp
SnapshotRegisters before = vm.GetRegistersSnapshot();
vm.MultiStep(count: 10);
SnapshotRegisters after = vm.GetRegistersSnapshot();

if (before != after)
    Console.WriteLine($"IP: 0x{before[RegType.rIP]:X} -> 0x{after[RegType.rIP]:X}");
```

**Регистрация собственного устройства (UART) рядом с VM.**

```csharp
using Nantuc.VmApplication.Emulator.Abstraction;

public sealed class UartController : PortControlBase
{
    private byte _tx;
    public override byte ReadPort(ulong offset) => offset == 0 ? _tx : (byte)0;
    public override void WritePort(ulong offset, byte value) { if (offset == 0) _tx = value; }
    public override void WakeProcessor() { /* ... */ }
}

using var host = VmHostFactory.CreateDefault();
var uart = new UartController();
host.Emulator.RegisterDevice(uart, name: "UART0", sector: 1);
// VM, зарегистрированная на sector 0, может слать OUT/IN по адресам sector 1 через port-шину
```

**Своё AST-правило оптимизации.**

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

using var host = VmHostFactory.CreateDefault();
host.Project.Optimizator.VMAstOptimizer.Add(new MyRule());
```

## Приближенность модуля к пользователю

**Максимальная.** Это и есть основной API платформы. Пользователь MiniC/NANASM после компиляции работает именно с `DeviceContext`/`VmHost`, а не с ядром напрямую.

**Кто и что использует:**
- **Конечный пользователь языка** — запускает готовый `.nbin` через `DeviceContext.Run`/`RunStepMode`/`DebugRun`. Обычно через GUI/CLI-обёртку.
- **Автор IDE/REPL/CI** — `VmHostProject.Compile`/`CompileToIL`, `GetSourceFiles`, `OptimizationResultLog`, `SnapshotRegisters`.
- **Автор игры/приложения** — `VmHostFactory.CreateDefault`, `DeviceContext`, `PortControlBase`, `LaunchOptions`.
- **Автор расширений к компилятору** — `VmAstOptimizer.Add`/`VmPeepholeOptimizer.Add`.

**Что стоит делать:**
- Всегда `using var host = VmHostFactory.CreateDefault(...)` — `Dispose` освобождает нативные буферы всех VM.
- Проверять `compilation.Success` перед `TryFastLoadProgram` — иначе загрузите `null`.
- Для больших программ — `TryFastLoadProgram` (NativeMemory.Copy), а не `LoadProgram` (посимвольно, с проверкой статуса на каждом байте).
- Использовать `RunStepMode`/`SingleStep` для отладки и `DebugRun` для отладки с автоматическим шагом.
- Для WASM/браузера — `RamFileService` + `CompileToIL` + `Build`, без `DefaultFileService`.
- Указывать `StartAddress` явно в `LaunchOptions` — по умолчанию `0`, что совпадает с `ProjectBuilder.BaseAddressProgram`, но если вы компилировали с другим `baseAddress`, дефолт промахнётся.

**Что не стоит делать:**
- Создавать несколько `VmHost` в одном процессе без необходимости — каждый держит свой `PortBus` и `ManagerDevices`. Один `VmHost` на приложение.
- Регистрировать `Device` напрямую через `host.Emulator.RegisterDevice(IPortController, ...)` без `DeviceSpec`, если хотите VM — используйте `DeviceSpec`-перегрузку, она создаёт `Device` через `Kernel.Utilites.Emulator.CreateDevice`.
- Держать `DeviceContext` после `host.Dispose()` — внутри лежит `Device`, который уже освобождён.
- Вызывать `Run` без остановки предыдущего прогона — `ThrowByMode` бросит `InvalidOperationException`, но лучше явно `Stop()`/`EndStepMode`.
- Использовать `DeviceContextUnsafeExtensions.AsRawDevice()` без острой необходимости — теряете контроль над lifetime.

## Особенная информация

### Почему VM — это устройство

`VmEmulator.RegisterDevice(DeviceSpec, sector)` создаёт `Kernel.BiosSystem.Device` и регистрирует его как `IPortController` в `PortBus`. `Device` реализует и `IPortController` (для внешней port-шины), и держит собственный `PortBus` (внутренняя шина VM). Это значит:

- Внешняя VM может слать `OUT`/`IN` по сектору внутренней VM — и наоборот.
- `DeviceContext.ReadPort(offset)` читает **собственные** порты VM (не порты порт-шины). Это то же самое `_ioMemory` в `Device`, которое видят другие устройства через `PortBus`.

### Режимы `DeviceContext`

Внутри `DeviceContext` есть `private enum Mode { Step, Continuous, None }` и `volatile Mode _mode`. Логика:

- `Run`/`RunWithLimit`/`DebugRun` → `ThrowByMode(Continuous)` → блокирующий цикл → `SetNoneMode()`.
- `RunStepMode` → `ThrowByMode(Step)` → `BreakPointerLaunchDevice` (только ставит IP и `IsRunning = true`).
- `SingleStep`/`MultiStep` → `ThrowIfNotStepMode` → `Device.NextStepProcessor`/`NextStepProcessorCount`.
- `EndStepMode` → `ThrowIfNotStepMode` → `Device.Stop()` + один `NextStepProcessor(null)` (чтобы процессить queued END/WAKE) + `SetNoneMode()`.
- `Stop` → `Device.Stop()` + `SetNoneMode()` — можно вызвать из любого режима.

Из-за `volatile` и `Interlocked` безопасно к вызову `Stop` из другого потока (например, по CancellationToken), но `Run`/`Step` — однопоточные.

### Байткод: `baseAddress` и `StartAdress`

`CompilationResult.StartAdress` — это `baseAddress`, переданный в `Compile`/`CompileToIL`. Он же — `AssemblerBase.BaseAddress` и база для `JMP`-меток. По умолчанию `ProjectBuilder.BaseAddressProgram = 0x00`, но можно передать, например, `0x1000`. Тогда `StartAdress = 0x1000`, и `LoadProgram` нужно делать с `loadAddress: 0x1000` (или `LaunchOptions.StartAddress = 0x1000`).

**Важно:** `Compile` не выставляет `StartAdress` в `LoadProgram` автоматически — это разные вещи. `LoadProgram` копирует байты по `loadAddress`, а `LaunchOptions.StartAddress` — откуда начать исполнение.

### Include resolution

`ProjectBuilder.Build` резолвит `#include "file"` из двух мест: `Paths.ProjectPath` и `Paths.IncludePath`. Если файл есть **и там, и там** — `InvalidOperationException` («please select a specific file»). Это защита от неоднозначности, но может раздражать при работе с реальной ФС. Для CI стоит избегать дубликатов.

Включаемые `.c`/`.minic` парсятся в AST и мержатся в общий `ProgramNode`. Включаемые `.asm`/`.nanasm` ассемблируются напрямую в текущий `AssemblerBase` (с проверкой `processed` для защиты от циклов).

### AST-оптимизации: пустой список по умолчанию

`VmHostProject.Optimizator.VMAstOptimizer` создаётся пустым. Если не добавить ни одного правила, `optimize: true` ничего не сделает, но `OptimizationResultLog` всё равно создастся (пустой). Стандартные правила (`FoldConstantRule` и т.п.) в этой сборке не зарегистрированы — это responsibility пользователя или внешней библиотеки правил.

### Thread-safety

`DeviceContext` не потокобезопасен, кроме `Stop` (через `Device.Stop` → `EnqueueBiosStatus` + `_wakeSignal.Set`). `SnapshotRegisters` — `readonly struct` с копией регистров, можно передавать между потоками. `VmHostProject.Compile`/`CompileToIL` — не потокобезопасны (общий `_optimizator.AstOptimizer`), но если не мутировать правила между вызовами — норм. `VmEmulator.GetDeviceContext`/`RegisterDevice`/`UnregisterDevice` — не потокобезопасны (внутри `ManagerDevices` обычные `List<>` и `Dictionary<>`).

### Ограничения платформ

- **`net10.0`, `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`** — из-за `Kernel` (нативные буферы), но в самом модуле `unsafe`-кода нет.
- **`params ReadOnlySpan<object>`** — в `ThrowCompiler` (из Diagnostics) требует `net9.0+`.
- **`[InlineArray]`** в `SnapshotRegisters` — C# 12+, работает в AOT/WASM.
- **`DefaultFileService`** использует `Directory.EnumerateFiles`/`File.ReadAllText` — в WASM это работает, но медленно. Для WASM — `RamFileService`.
- **`Thread.Sleep(delay)` в `DeviceContext.DebugRun`** (через `Device.DebugRun`) — в WASM либо no-op, либо `PlatformNotSupportedException`. Для WASM-отладки `DebugRun` с `DelayMs > 0` не подойдёт; используйте `RunStepMode` + `SingleStep`.
- **`DefaultFileService.GetNameFiles`** использует `Path.GetExtension` и `StringComparer.OrdinalIgnoreCase` — работает кросс-платформенно, но `DefaultFileService` не умеет в виртуальные пути — для этого есть `IFileService`.

### Что этот модуль не делает

- Не реализует `IFileService` для сети/облака — только `Default` (ФС) и `Ram`.
- Не содержит UI/REPL/CLI — это библиотека.
- Не содержит готовых AST/peephole правил — только инфраструктуру.
- Не решает, какую VM запускать — пользователь регистрирует столько `DeviceSpec`, сколько нужно.
- Не управляет lifetime `VmHostProject` через `Dispose` — он не `IDisposable`.

## Что исправить

1. **`RamFileService.Combine` использует `"\\"` — сломается на Linux/WASM.**  
   Файл: `Default/RamFileService.cs`, метод `Combine` (`public string Combine(string path1, string path2) => path1 + "\\" + path2;`).  
   На Linux/WASM `Combine` создаёт ключи вида `project\foo.c`, а `GetNameFiles`/`ReadFile` ищут `project/foo.c` (если пути пришли из `IProjectFilesConfig.Default`, где `Path.Combine` даёт `/`). В результате `TryFastLoadProgram`/`Compile` не находят include.  
   Фикс: `=> Path.Combine(path1, path2)` (или `$"{path1.TrimEnd('/', '\\')}/{path2.TrimStart('/', '\\')}"` для детерминизма). Проверить, что все остальные места в `RamFileService` консистентны.

2. **`RamFileService.GetNameFiles` фильтрует по `IncludePath` без `StringComparison`.**  
   Файл: `Default/RamFileService.cs`, метод `GetNameFiles` (`!file.StartsWith(path.IncludePath)`).  
   `StartsWith` без `StringComparison` — culture-sensitive, что для путей неправильно. Плюс семантика (`file.StartsWith(IncludePath)`) чувствительна к тому, что `Combine` даёт разные разделители (п.1). После п.1 используйте `file.StartsWith(path.IncludePath, StringComparison.Ordinal)`.

3. **`SnapshotRegisters.GetHashCode()` не согласован с `Equals`.**  
   Файл: `Emulator/DeviceContext.cs`, `SnapshotRegisters.GetHashCode` (`_values!.GetHashCode()`).  
   `Equals` сравнивает 32 `ulong` поэлементно; `GetHashCode` вызывает `GetHashCode` на `InlineArray`-структуре, которая по умолчанию хэширует **ссылку/первый элемент** (в зависимости от рантайма), а не все 32 значения. Два `SnapshotRegisters` с `Equals == true` могут иметь разные хэши — нарушение контракта `GetHashCode`.  
   Фикс: `public override int GetHashCode() { var h = new HashCode(); for (int i = 0; i < 32; i++) h.Add(_values[i]); return h.ToHashCode(); }`.

4. **`CompilationToILResult` публичный `record struct` с `internal` полем `Assembler`.**  
   Файл: `Project/CompilationToILResult.cs`.  
   `public readonly record struct CompilationToILResult(...) { internal readonly IRAssembler? Assembler; ... }`. `record struct` генерирует `Equals`/`GetHashCode` только по **позиционным** полям (`Errors`, `OptimizationResultLog`), `Assembler` игнорируется. Это может быть неожиданно (два результата с одинаковыми `Errors`/`Log`, но разными `Assembler`, `==` равны). Обычно норм, но задокументируйте в XML-doc, что `Assembler` не участвует в сравнении.

5. **`CompilationResult.Success` возвращает `true` для пустого `Errors` при `null` `Program`.**  
   Файл: `Project/CompilationResult.cs`, свойство `Success` (`Errors == null || Errors.Count == 0`).  
   Конструктор `CompilationResult(IReadOnlyList<string>? errors)` устанавливает `Program = null`, `Errors = errors`. Если `errors` — пустой список, `Success == true`, но `Program == null`. Проверяйте `Program != null` перед `TryFastLoadProgram`, не полагайтесь только на `Success`.  
   Фикс: `Success => Program != null && (Errors == null || Errors.Count == 0)`.

6. **`OptimizationResultLog.Upsert` — комментарий врёт про мердж.**  
   Файл: `Project/OptimizationResultLog.cs`, метод `Upsert`, комментарий «мерджим записи; иначе кладём снимок». Код: `_logs[log.Id] = log;` — перезапись, без мерджа.  
   Либо реализовать мердж (конкатенация `OptimizationLogEntry`), либо исправить комментарий. Если правило возвращает один и тот же `OptimizationId` несколько раз за проход, записи теряются.

7. **`VmHostProject.CompileInternal` теряет тип исключения и stack trace.**  
   Файл: `Project/VmHostProject.cs`, метод `CompileInternal`, `catch (Exception ex) { return createResult(null!, null, [ex.Message]); }`.  
   `ex.Message` без `ex.GetType().Name` и без stack trace. Для `CodeException` (из Diagnostics) теряется `ErrorCode`. Для отладки добавьте `$"{ex.GetType().Name}: {ex.Message}"`, а ещё лучше — `ex.ToString()` (включая stack trace) или отдельный overload `CompilationResult` для `Exception`.

8. **`CompilationResult` success-конструктор не защищён от исключений `asm.Build()`.**  
   Файл: `Project/CompilationResult.cs`, успешный конструктор (`public CompilationResult(AssemblerBase asm, OptimizationResultLog? log)`).  
   Внутри `asm.Build()` может бросить (`Asm_UndefinedLabel` и т.п.). В `VmHostProject.Compile` это ловится `CompileInternal` (общий catch), но если пользователь сам создаёт `new CompilationResult(asm, log)` вне `CompileInternal` — исключение полетит наружу. Добавьте try/catch или документируйте.

9. **`ProjectBuilder.Build` эмитит лишний `JMP __global_init` в начале.**  
   Файл: `Project/ProjectBuilder.cs`, `ProjectBuilder.Build` (`assembler.EmitJmp("__global_init")`).  
   Затем `FunctionGenerator.Generate` **внутри** ставит `MarkLabel("__global_init")` и `EmitJmp("func_main")`. Получается: `0x00: JMP __global_init`, `__global_init: <init>; JMP func_main`, `func_main: ...`. Двойной переход. Работает, но неэффективно (2 байта × 2).  
   Фикс: убрать `assembler.EmitJmp("__global_init")` из `ProjectBuilder.Build` — `FunctionGenerator.Generate` сам вставит `JMP func_main` после `__global_init`, и программу можно запускать с `StartAddress = 0x00`, если `__global_init` идёт первым. Проверьте, что `FunctionGenerator.Generate` действительно ставит `__global_init` в начало (по коду — да, `MarkLabel("__global_init")` идёт до `GenerateGlobalInit`).

10. **`VmHostProject.CompileToIL` не вызывает `OptimizationIL` автоматически.**  
    Файл: `Project/VmHostProject.cs`, метод `CompileToIL`.  
    Название намекает на IL-путь с оптимизациями, но `optimize: true` применяет только AST-правила. Peephole надо звать вручную (`OptimizationIL(il, maxPasses)`). Это задокументировано в `Build()` (`CompilationToILResult.Build`), но легко упустить.  
    Фикс: либо переименовать `CompileToIL` в `CompileToILNoPeephole`, либо добавить перегрузку `CompileToIL(files, baseAddress, optimize, peepholePasses)`.

11. **`DeviceContext.GetRegistersSnapshot` выделяет `stackalloc ulong[32]` на каждый вызов.**  
    Файл: `Emulator/DeviceContext.cs`, метод `GetRegistersSnapshot`.  
    `stackalloc` дёшев, но всё же — если вызывается из горячего цикла (отладчик с шагом), накапливается overhead. Плюс `CopyRegisters` внутри `Device.CopyRegisters` уже требует `Span<ulong>`. Не критично, но для отладчика с `onStep`-колбэком на каждом шаге `SnapshotRegisters` может быть дорогим.  
    Фикс: либо переиспользуемый буфер в `DeviceContext`, либо `ArrayPool<byte>.Shared` (но `ulong[]` — не byte[]). Минимум — задокументировать стоимость.

12. **`DeviceView` не используется нигде в публичном API.**  
    Файл: `Emulator/DeviceView.cs`.  
    `DeviceView(int id, uint sector, string? name)` — по полям совпадает с `DeviceInfo` из `Kernel.Contracts`. `GetAllDevices()` возвращает `IReadOnlyCollection<DeviceInfo>`, не `DeviceView`. `GetDeviceContext` возвращает `DeviceContext?`, не `DeviceView`. Мёртвый код либо задел на будущее.  
    Фикс: либо удалить, либо задокументировать, что это «снимок без зависимости от Kernel.Contracts» (но тогда нужен конверсионный метод `FromDeviceInfo`).

13. **`VmHost.Dispose` не освобождает `Project`** (он не `IDisposable`, это ок), но если `Project.Optimizator` содержит правила с ресурсами — утечка.  
    Файл: `VmHost.cs`.  
    Не критично (правила — обычно stateless), но если пользователь добавит правило с `IDisposable`-состоянием, ему придётся освобождать вручную. Задокументируйте, что `VmHost.Dispose` не трогает `Project`.

14. **`VmAstOptimizer` vs `VMAstOptimizer` — рассинхронизация регистра.**  
    Файл: `Project/VmCompilerOptimizator.cs`, свойство `VMAstOptimizer`. Класс называется `VmAstOptimizer`. Стилистически должно быть `VmAstOptimizer` (как `VmPeepholeOptimizer`/`VMPeepholeOptimizer` — там тоже `VMPeepholeOptimizer`, но `VmPeepholeOptimizer` как тип).  
    Мелочь, но в API консистентность важна. Переименуйте свойство в `AstOptimizer`/`PeepholeOptimizer` или типы в `VMAstOptimizer`/`VMPeepholeOptimizer`.

15. **`VmHostFactory.CreateDefault` не передаёт `AstOptimizer`/`PeepholeOptimizer`.**  
    Файл: `VmHostFactory.cs`.  
    Фабрика создаёт `VmHostProject`/`VmEmulator` с дефолтными оптимизаторами (пустыми). Если пользователь хочет свои правила — он идёт в `host.Project.Optimizator.VMAstOptimizer.Add(...)`. Это работает, но `CreateDefault` не даёт способа передать готовый `VmCompilerOptimizator`.  
    Фикс: добавить `VmCompilerOptimizator? optimizator = null` в `CreateDefault` (или в `VmHostProjectBuilder.WithOptimizator`), чтобы можно было собрать проект с предзаполненными правилами.

16. **`VmHostProjectBuilder.Build` — сообщение об ошибке устарело.**  
    Файл: `Project/VmHostProjectBuilder.cs`, `Build()`. Текст: `"ProjectPaths, Projectservice, Logger обязательны"`. `Logger` в этом классе нет. Замените на актуальное: `"ProjectPaths и FileService обязательны"`.

17. **`ProjectBuilder.ReadInclude` использует `InvalidOperationException` для конфликта путей.**  
    Файл: `Project/ProjectBuilder.cs`, метод `ReadInclude` (`throw new InvalidOperationException($"The file {fileName} exists in both...")`).  
    Для сценария «две копии файла» уместнее `CodeException` с `ErrorCode` (например, новый `Parser_IncludeAmbiguous` или `Asm_IncludeAmbiguous`), чтобы IDE/CI могли отличить ошибку компиляции от баги в коде. Сейчас ловится как обычный `Exception` в `CompileInternal` и попадает в `Errors` как строка.

18. **`PortControlBase.Dispose` — паттерн правильный, но `IsDisposed` не проверяется перед `ReadPort`/`WritePort`.**  
    Файл: `Emulator/Abstraction/PortControlBase.cs`.  
    `Dispose` идемпотентен, но после `Dispose` наследник может продолжать получать `ReadPort`/`WritePort` от `PortBus` (если `ManagerDevices.RemoveDevice` не успел). `Nantuc.Kernel` не проверяет `IsDisposed` у `IPortUse` — только `null` в массиве `_devices`. То есть после `Dispose` устройства, но до `UnregisterDevice`, порты всё ещё маршрутизируются, и `ReadPort` вернёт мусор.  
    Фикс: в `PortBus.ReadPort`/`WritePort`/`WakeProcessor` проверять `device is IPortController { IsDisposed: true }` → `NullDeviceInput`/`NullDeviceOutput` (или задокументировать, что `Dispose` и `UnregisterDevice` должны идти парой). См. также `Nantuc.Kernel` (п. 4 из того README).