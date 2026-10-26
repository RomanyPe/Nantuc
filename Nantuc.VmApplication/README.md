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
- `DeviceContext` — управление конкретной VM: `LoadProgram`, `Run`, `RunStepMode`/`SingleStep`, `Stop`, `ReadPort`, `GetRegistersSnapshot`, режимы (Step/Continuous/None).

Ключевая архитектурная особенность: VM — это тоже устройство на port-шине. `VmEmulator.RegisterDevice(DeviceSpec, sector)` создаёт `Device` из ядра и регистрирует его как `IPortController`. Один эмулятор может держать несколько VM, и они общаются через порты, как обычные устройства (композиция VM внутри VM). `DeviceContext` — это удобное окно к конкретному `Device` без прямого доступа к `Nantuc.Kernel`.

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
│   ├── LaunchOptions.cs                # LaunchOptions / LaunchOptionsDebug
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
│   ├── VmHostProjectBuilder.cs         # WithPaths / WithFileService / Build
│   └── VmPeepholeOptimizer.cs          # обёртка PeepholeOptimizer
└── Unsafe/
    └── DeviceContextUnsafeExtensions.cs # AsRawDevice() / GetRawPortBus()
```

Замечание: `Emulator/DeviceContext.cs` содержит и `DeviceContext`, и `SnapshotRegisters`. Для навигации неудобно (grep `SnapshotRegisters` ведёт в файл с другим именем).

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

`VmHost.Dispose()` освобождает только `Emulator` (внутри — `Kernel.Utilites.Emulator.Dispose`), но не `Project` — `VmHostProject` не `IDisposable`. `Device`, зарегистрированные в `Emulator`, освободятся через `ManagerDevices.Dispose`.

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
    public bool TryGetDeviceContext(int id, [NotNullWhen(true)] out DeviceContext? ctx);
    public bool ChangeDeviceSector(int id, uint newSector);
    public IReadOnlyCollection<DeviceInfo> GetAllDevices();

    public int      RegisterDevice(IPortController ioDevice, string? name, uint sector);
    public int      RegisterDevice(DeviceSpec handle, uint sector);
    public IdState  TryRegisterDevice(IPortController ioDevice, string? name, uint sector, out int id);
    public IdState  TryRegisterDevice(DeviceSpec handle, uint sector, out int id);

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
- `RegisterDevice` — возвращает `int` (ID при успехе, `-1..-4` при ошибке). Для кода, который уже работает с `int`. Внутри вызывает `_emulator.RegisterDevice(..., out int id)` и возвращает `id` — то есть отрицательный код ошибки, а не `IdState`.
- `TryRegisterDevice` — возвращает `IdState` (`Valid`/`NotAssigned`/`OutOfRange`/`NoFreeSlot`/`Rejected`) и `out int id`. **Рекомендуемый способ** для нового кода.

`TryGetDeviceContext` — безопасный вариант `GetDeviceContext` без `!`: возвращает `bool` и `[NotNullWhen(true)] out DeviceContext?`. Для нового кода — предпочтительный путь.

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

    public CompilationResult     Compile(IEnumerable<SourceFile> files, ulong baseAddress, bool optimize);
    public CompilationToILResult CompileToIL(IEnumerable<SourceFile> files, ulong baseAddress, bool optimize);
    public CompilationToILResult OptimizationIL(CompilationToILResult res, int peepholeOptimizationCount);
}
```

- `Compile` — одностадийная сборка: `Assembler` (прямой эмит) → `byte[]`. Быстрее, но без peephole.
- `CompileToIL` — двухстадийная: `ILAssembler` (список `AsmItem`) → `CompilationToILResult`, затем `OptimizationIL(result, maxPasses)` прогоняет peephole и `result.Build()` собирает байты.
- `optimize: true` — включает AST-оптимизации, зарегистрированные в `Optimizator.VMAstOptimizer`. Логи собираются в `OptimizationResultLog`.
- `baseAddress` — обычно `ProjectBuilder.BaseAddressProgram` (0x00).
- `GetSourceFiles` — читает все `.c`/`.minic`/`.asm`/`.nanasm` из `FileService.GetNameFiles(Paths)` и возвращает `SourceFile[]`.

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

    public ProcessorFault Run(LaunchOptions? opt = null);
    public ProcessorFault DebugRun(LaunchOptionsDebug? opt = null);

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

`Run` и `DebugRun` теперь возвращают `ProcessorFault` — это `_processor.GetDiagnosticData()`. На успехе — `BiosStatus.EndProgramm`; на падении — соответствующий статус с `InstructionPointer` и `OpCode` места падения.

`RunWithLimit` удалён — его роль выполняет `LaunchOptions.StepLimit` (`long?`, `null` = без лимита). Это унифицирует API: одна точка входа для запуска с лимитом и без.

Режимы (`DeviceContext.Mode` — private enum):

| Режим | Как войти | Как выйти | Что делает |
|---|---|---|---|
| `Continuous` | `Run` / `DebugRun` | автоматически после завершения | блокирующий цикл `Step()` |
| `Step` | `RunStepMode` | `EndStepMode` | ручной `SingleStep`/`MultiStep` |
| `None` | `Stop()` или завершение | — | ничего не запущено |

Правила режимов:
- Нельзя вызвать `Run`/`DebugRun`/`RunStepMode`, пока `IsRunning == true` — `ThrowIfRunning`.
- Нельзя сменить режим (Step ↔ Continuous) без `Stop()`/`EndStepMode` — `ThrowByMode` бросит `InvalidOperationException`.
- `SingleStep`/`MultiStep`/`EndStepMode` работают только в режиме Step — `ThrowIfNotStepMode`.
- `LoadProgram`/`TryFastLoadProgram`/`ResetMemoryRam` требуют `IsRunning == false`.

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

public readonly record struct CompilationToILResult
{
    internal readonly ILAssembler? Assembler;
    public IReadOnlyList<string>? Errors { get; }
    public OptimizationResultLog? OptimizationResultLog { get; }

    public CompilationToILResult(IReadOnlyList<string>? errors);
    internal CompilationToILResult(ILAssembler? assembler, OptimizationResultLog? optimizationResultLog);
    public CompilationToILResult(ILAssembler? assembler, OptimizationResultLog? optimizationResultLog,
                                 IReadOnlyList<string>? errors);

    public CompilationResult Build();
}
```

- `CompilationResult.Program` — готовый байткод, `null` если была ошибка.
- `StartAdress` — тот же `baseAddress`, что передали в `Compile`.
- `CodeSize`/`DataStart`/`DataSize` — метрики секций, полезны для дизассемблера и профилирования.
- `CompilationToILResult` хранит не байты, а `ILAssembler` (internal-поле) — `Build()` собирает байты. Это позволяет прогнать `OptimizationIL` между `CompileToIL` и `Build`.
- `CompilationToILResult` теперь `readonly record struct` без позиционных параметров — `Errors`/`OptimizationResultLog` стали публичными свойствами, добавлены публичные конструкторы для кастомных сценариев. `Assembler` — internal, но доступен через `Build()`.

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

- `DefaultFileService` — реальная ФС: `Directory.EnumerateFiles(ProjectPath, "*.*", AllDirectories)`, фильтрация по расширениям.
- `RamFileService` — in-memory `Dictionary<string, string>`. Для тестов, WASM-площадок и REPL без доступа к ФС. Добавлять файлы — через `SaveFile`. Теперь `GetNameFiles` и `Ext` используют `StringComparison.Ordinal` — культура не влияет на сравнение путей.

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

`VmCompilerOptimizator` — просто контейнер. Создаётся в `VmHostProject` и доступен через `host.Project.Optimizator`. Оба списка правил — пустые по умолчанию: без явного `Add` оптимизации не применяются.

### Unsafe extensions

```csharp
namespace Nantuc.VmApplication.Unsafe;

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

Для случаев, когда обёртки не хватает: прямой доступ к `Device` (например, `DebugRun` с кастомной логикой) или к `PortBus` (например, регистрация устройства по сектору в обход `ManagerDevices`). Использовать осторожно: после `AsRawDevice()` вы держите ссылку на `Device`, и его `Dispose` уже не контролируется `VmEmulator`.

## Примеры для API

**Полный цикл: источники → байткод → запуск с диагностикой.**

```csharp
using Nantuc.Kernel.Common;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Emulator;

using var host = VmHostFactory.CreateDefault();

var sources = host.Project.GetSourceFiles().ToList();
var compilation = host.Project.Compile(sources, baseAddress: 0, optimize: true);
if (!compilation.Success)
{
    foreach (var err in compilation.Errors!)
        Console.Error.WriteLine(err);
    return;
}

var spec = DeviceSpec.Create(ramSize: RamSize.KB64, name: "main");
var registerState = host.Emulator.TryRegisterDevice(spec, sector: 0, out int deviceId);
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

if (!vm.TryFastLoadProgram(compilation.Program!, loadAddress: 0, out var error))
{
    Console.Error.WriteLine(error);
    return;
}

ProcessorFault fault = vm.Run(new LaunchOptions(ShowTimer: true,
    OnEnd: r => Console.WriteLine($"steps={r?.Steps} elapsed={r?.Elapsed}")));

if (fault.Status != BiosStatus.EndProgramm)
    Console.Error.WriteLine($"VM fault: {fault.Status} at IP=0x{fault.InstructionPointer:X}, op={fault.OpCode}");
```

**Компиляция через IL с оптимизациями:**

```csharp
using Nantuc.VmApplication.Project;

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

**In-memory сценарий (без реальной ФС):**

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
vm.LoadProgram(compilation.Program!);
vm.RunStepMode(startAddress: 0);

for (int i = 0; i < 20 && vm.IsRunning; i++)
    vm.SingleStep(r => Console.WriteLine(
        $"IP=0x{r.GetRegister(RegType.rIP):X} r0={r.GetRegister(RegType.r0)}"));

vm.EndStepMode();
```

**Снимок регистров и сравнение:**

```csharp
SnapshotRegisters before = vm.GetRegistersSnapshot();
vm.MultiStep(count: 10);
SnapshotRegisters after = vm.GetRegistersSnapshot();

if (before != after)
    Console.WriteLine($"IP: 0x{before[RegType.rIP]:X} -> 0x{after[RegType.rIP]:X}");
```

**Регистрация собственного устройства (UART) рядом с VM:**

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
host.Emulator.RegisterDevice(uart, name: "UART0", sector: 1);
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

using var host = VmHostFactory.CreateDefault();
host.Project.Optimizator.VMAstOptimizer.Add(new MyRule());
```

## Приближенность модуля к пользователю

Максимальная. Это и есть основной API платформы. Пользователь MiniC/NANASM после компиляции работает именно с `DeviceContext`/`VmHost`, а не с ядром напрямую.

**Кто и что использует:**
- Конечный пользователь языка — запускает готовый `.nbin` через `DeviceContext.Run`/`RunStepMode`/`DebugRun`. Обычно через GUI/CLI-обёртку.
- Автор IDE/REPL/CI — `VmHostProject.Compile`/`CompileToIL`, `GetSourceFiles`, `OptimizationResultLog`, `SnapshotRegisters`.
- Автор игры/приложения — `VmHostFactory.CreateDefault`, `DeviceContext`, `PortControlBase`, `LaunchOptions`.
- Автор расширений к компилятору — `VmAstOptimizer.Add`/`VmPeepholeOptimizer.Add`.

**Что стоит делать:**
- Всегда `using var host = VmHostFactory.CreateDefault(...)` — `Dispose` освобождает нативные буферы всех VM.
- Проверять `compilation.Success` перед `TryFastLoadProgram` — иначе загрузите `null`.
- Использовать `TryRegisterDevice` (возвращает `IdState` + `out int id`) вместо `RegisterDevice` — единая диагностика.
- Использовать `TryGetDeviceContext` вместо `GetDeviceContext(id)!` — без `null-forgiving`.
- Для больших программ — `TryFastLoadProgram` (NativeMemory.Copy), а не `LoadProgram` (посимвольно).
- Читать `ProcessorFault` из `Run`/`DebugRun` — это правильный способ узнать статус завершения.
- Для WASM/браузера — `RamFileService` + `CompileToIL` + `Build`, без `DefaultFileService`.
- Указывать `StartAddress` явно в `LaunchOptions` — по умолчанию `0`, что совпадает с `ProjectBuilder.BaseAddressProgram`, но если вы компилировали с другим `baseAddress`, дефолт промахнётся.

**Что не стоит делать:**
- Создавать несколько `VmHost` в одном процессе без необходимости — каждый держит свой `PortBus` и `ManagerDevices`. Один `VmHost` на приложение.
- Регистрировать `Device` напрямую через `RegisterDevice(IPortController, ...)` без `DeviceSpec`, если хотите VM — используйте `DeviceSpec`-перегрузку.
- Держать `DeviceContext` после `host.Dispose()` — внутри лежит `Device`, который уже освобождён.
- Вызывать `Run` без остановки предыдущего прогона — `ThrowByMode` бросит `InvalidOperationException`.
- Использовать `DeviceContextUnsafeExtensions.AsRawDevice()` без острой необходимости — теряете контроль над lifetime.

## Что исправлено в этой версии

По сравнению с прошлой фиксацией `Nantuc.VmApplication`:

- `Run`/`DebugRun` теперь возвращают `ProcessorFault` (было `void`). Правильный способ узнать статус завершения — без обёрток и `IProcessorFaultPolicy`-хаков.
- `RunWithLimit` удалён, его роль выполняет `LaunchOptions.StepLimit` (`long?`, `null` = без лимита). Одна точка входа для запуска.
- `VmEmulator.TryRegisterDevice` — новый API, возвращает `IdState` и `out int id`. Позволяет не полагаться на магические `-1..-4`. `RegisterDevice` (с `int`) остался для обратной совместимости.
- `VmEmulator.TryGetDeviceContext(int, out DeviceContext?)` — новый API, убирает необходимость `!` на `GetDeviceContext`.
- `RamFileService.GetNameFiles`/`Ext` используют `StringComparison.Ordinal` (было culture-sensitive по умолчанию).
- `CompilationToILResult` переработан: `Errors`/`OptimizationResultLog` — публичные свойства, добавлены публичные конструкторы, `Assembler` — `ILAssembler?` (не `IRAssembler?`). `record struct` без позиционных параметров.
- `CompilationResult`, `VmHostProject`, `VmHostFactory.CreateDefault`, `DeviceContextUnsafeExtensions` — добавлены XML-документация.
- `OptimizationIL` при ошибке возвращает `new CompilationToILResult([ex.Message])` (раньше — `new CompilationToILResult(null, res.OptimizationResultLog, [ex.Message])` — терял лог; сейчас тоже теряет, но хотя бы сигнатура упрощена).

## Особенная информация

### Почему VM — это устройство

`VmEmulator.RegisterDevice(DeviceSpec, sector)` создаёт `Kernel.BiosSystem.Device` и регистрирует его как `IPortController` в `PortBus`. `Device` реализует и `IPortController` (для внешней port-шины), и держит собственный `PortBus` (внутренняя шина VM). Это значит:
- Внешняя VM может слать `OUT`/`IN` по сектору внутренней VM — и наоборот.
- `DeviceContext.ReadPort(offset)` читает собственные порты VM (не порты порт-шины).

### Режимы `DeviceContext`

Внутри `DeviceContext` есть `private enum Mode { Step, Continuous, None }` и `volatile Mode _mode`:
- `Run`/`DebugRun` → `ThrowByMode(Continuous)` → блокирующий цикл → `SetNoneMode()`.
- `RunStepMode` → `ThrowByMode(Step)` → `BreakPointerLaunchDevice`.
- `SingleStep`/`MultiStep` → `ThrowIfNotStepMode` → `Device.NextStepProcessor`/`NextStepProcessorCount`.
- `EndStepMode` → `ThrowIfNotStepMode` → `Device.Stop()` + один `NextStepProcessor(null)` + `SetNoneMode()`.
- `Stop` → `Device.Stop()` + `SetNoneMode()` — можно вызвать из любого режима.

Из-за `volatile` и `Interlocked` безопасно к вызову `Stop` из другого потока (например, по CancellationToken), но `Run`/`Step` — однопоточные.

### Байткод: `baseAddress` и `StartAdress`

`CompilationResult.StartAdress` — это `baseAddress`, переданный в `Compile`/`CompileToIL`. Он же — `AssemblerBase.BaseAddress` и база для `JMP`-меток. По умолчанию `ProjectBuilder.BaseAddressProgram = 0x00`. `Compile` не выставляет `StartAdress` в `LoadProgram` автоматически — это разные вещи. `LoadProgram` копирует байты по `loadAddress`, а `LaunchOptions.StartAddress` — откуда начать исполнение.

### Include resolution

`ProjectBuilder.Build` резолвит `#include "file"` из двух мест: `Paths.ProjectPath` и `Paths.IncludePath`. Если файл есть и там, и там — `InvalidOperationException` («please select a specific file»). Это защита от неоднозначности. Включаемые `.c`/`.minic` парсятся в AST и мержатся в общий `ProgramNode`. Включаемые `.asm`/`.nanasm` ассемблируются напрямую в текущий `AssemblerBase` (с проверкой `processed` для защиты от циклов).

### Thread-safety

`DeviceContext` не потокобезопасен, кроме `Stop` (через `Device.Stop` → `EnqueueBiosStatus` + `_wakeSignal.Set`). `SnapshotRegisters` — `readonly struct` с копией регистров, можно передавать между потоками. `VmHostProject.Compile`/`CompileToIL` — не потокобезопасны (общий `_optimizator.AstOptimizer`). `VmEmulator.GetDeviceContext`/`RegisterDevice`/`UnregisterDevice` — не потокобезопасны.

### `ProcessorFault` из `Run`/`DebugRun`

`Run` и `DebugRun` возвращают `ProcessorFault` из `_processor.GetDiagnosticData()`. На успехе — `BiosStatus.EndProgramm`. На падении — статус с `InstructionPointer` и `OpCode` места падения. Важно: `_faultData` в `Processor` не сбрасывается в `LaunchProgramm` (см. `[Nantuc.Kernel](../Nantuc.Kernel/README.md)`, [Что исправить, п.2](../Nantuc.Kernel/README.md#что-исправить)) — второй `Run` может вернуть fault от первого, если первый был падением, а второй — успехом.

### Ограничения платформ

- `net10.0`, `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` — из-за `Kernel`, но в самом модуле unsafe-кода нет.
- `params ReadOnlySpan<object>` — в `ThrowCompiler` (из Diagnostics) требует `net9.0+`.
- `[InlineArray]` в `SnapshotRegisters` — C# 12+, работает в AOT/WASM.
- `DefaultFileService` использует `Directory.EnumerateFiles`/`File.ReadAllText` — в WASM работает, но медленно. Для WASM — `RamFileService`.
- `Thread.Sleep(delay)` в `DeviceContext.DebugRun` (через `Device.DebugRun`) — в WASM либо no-op, либо `PlatformNotSupportedException`.

## Что исправить

По убыванию важности. Ссылки на файлы и методы.

1. **`RamFileService.Combine` — всё ещё `"\\"`.**  
   Файл: `Default/RamFileService.cs`, `Combine`: `path1 + "\\" + path2`. На Linux/WASM пути из `IProjectFilesConfig.Default` используют `/` (через `Path.Combine`), а `RamFileService` создаёт ключи с `\`. `GetNameFiles` теперь использует `StringComparison.Ordinal` — но это не помогает, потому что разделители путей не совпадают.  
   Фикс: `Path.Combine(path1, path2)` (или `$"{path1.TrimEnd('/', '\\')}/{path2.TrimStart('/', '\\')}"` для детерминизма).

2. **`VmHostProjectBuilder.Build` — устаревшее сообщение об ошибке.**  
   Файл: `Project/VmHostProjectBuilder.cs`, `Build()`. Текст: `"ProjectPaths, Projectservice, Logger обязательны"`. `Logger` в этом классе нет. `Projectservice` — опечатка.  
   Фикс: `"ProjectPaths и FileService обязательны"`.

3. **`CompilationResult.Success` возвращает `true` для пустого `Errors` при `null` `Program`.**  
   Файл: `Project/CompilationResult.cs`, свойство `Success` (`Errors == null || Errors.Count == 0`). Конструктор `CompilationResult(IReadOnlyList<string>? errors)` устанавливает `Program = null`. Если `errors` — пустой список, `Success == true`, но `Program == null`.  
   Фикс: `Success => Program != null && (Errors == null || Errors.Count == 0)`.

4. **`OptimizationResultLog.Upsert` — комментарий врёт про мердж.**  
   Файл: `Project/OptimizationResultLog.cs`, `Upsert`, комментарий «мерджим записи; иначе кладём снимок». Код: `_logs[log.Id] = log;` — перезапись, без мерджа.  
   Фикс: либо реализовать мердж (конкатенация `OptimizationLogEntry`), либо исправить комментарий.

5. **`VmHostProject.CompileInternal` теряет тип исключения.**  
   Файл: `Project/VmHostProject.cs`, `CompileInternal`, `catch (Exception ex) { return createResult(null!, null, [ex.Message]); }`. `ex.Message` без `ex.GetType().Name` и без stack trace. Для `CodeException` (из Diagnostics) теряется `ErrorCode`.  
   Фикс: `$"{ex.GetType().Name}: {ex.Message}"` или `ex.ToString()`.

6. **`VmHostFactory.CreateDefault` не передаёт `VmCompilerOptimizator`.**  
   Файл: `VmHostFactory.cs`. Фабрика создаёт `VmHostProject`/`VmEmulator` с дефолтными оптимизаторами (пустыми). Если пользователь хочет свои правила — он идёт в `host.Project.Optimizator.VMAstOptimizer.Add(...)`. Это работает, но `CreateDefault` не даёт способа передать готовый `VmCompilerOptimizator`.  
   Фикс: добавить `VmCompilerOptimizator? optimizator = null` в `CreateDefault` (или в `VmHostProjectBuilder.WithOptimizator`).

7. **`CompilationToILResult` — `Assembler` internal, но `Build()` публичный.**  
   Файл: `Project/CompilationToILResult.cs`. Внешний пользователь может только `Build()`, но не может инспектировать `ILAssembler` (например, чтобы прогнать свои peephole-правила).  
   Рекомендация: либо сделать `Assembler` публичным (и рискнуть, что кто-то его модифицирует между `CompileToIL` и `Build`), либо добавить публичный `Optimize(Func<List<AsmItem>, ...>)` extension.

8. **`VmEmulator.RegisterDevice` (с `int`) — потенциально небезопасный.**  
   Файл: `Emulator/VmEmulator.cs`, `RegisterDevice`. Возвращает `int id`, который может быть `-1..-4`. Пользователь, который не проверяет `id < 0`, получит NRE на `GetDeviceContext(id)!`.  
   Рекомендация: задокументировать в XML-doc (сделано), но также — либо `[Obsolete]` на `RegisterDevice`, либо перенаправить в `TryRegisterDevice` с `.id` на выходе.

9. **`VmHostProject.OptimizationIL` при ошибке теряет `OptimizationResultLog`.**  
   Файл: `Project/VmHostProject.cs`, `OptimizationIL`, `catch (Exception ex) { return new CompilationToILResult([ex.Message]); }`. Лог, который был в `res.OptimizationResultLog`, теряется.  
   Фикс: `return new CompilationToILResult(null, res.OptimizationResultLog, [ex.Message]);`.

10. **`VmAstOptimizer` vs `VMAstOptimizer` — рассинхронизация регистра.**  
    Файл: `Project/VmCompilerOptimizator.cs`, свойство `VMAstOptimizer`. Класс называется `VmAstOptimizer`. Стилистически должно быть `VmAstOptimizer` (как `VmPeepholeOptimizer`/`VMPeepholeOptimizer` — там тоже `VMPeepholeOptimizer`, но `VmPeepholeOptimizer` как тип).  
    Мелочь, но в API консистентность важна.

11. **`ProjectBuilder.ReadInclude` использует `InvalidOperationException` для конфликта путей.**  
    Файл: `Project/ProjectBuilder.cs`, `ReadInclude` (`throw new InvalidOperationException($"The file {fileName} exists in both...")`). Для сценария «две копии файла» уместнее `CodeException` с `ErrorCode` (например, новый `Parser_IncludeAmbiguous`), чтобы IDE/CI могли отличить ошибку компиляции от баги в коде. Сейчас ловится как обычный `Exception` в `CompileInternal` и попадает в `Errors` как строка.

12. **`PortControlBase.IsDisposed` не проверяется перед `ReadPort`/`WritePort`.**  
    Файл: `Emulator/Abstraction/PortControlBase.cs`. `Dispose` идемпотентен, но после `Dispose` наследник может продолжать получать `ReadPort`/`WritePort` от `PortBus` (если `ManagerDevices.RemoveDevice` не успел).  
    Рекомендация: в наследниках (`SynchronousIOStream`, `QueuedIOStream`, `NumericIOStream`) уже есть `ThrowIfDisposed` — но контракт `IPortUse` этого не требует. Стоит задокументировать в `PortControlBase` или в `IPortUse`, что после `Dispose` `ReadPort`/`WritePort` могут бросать `ObjectDisposedException`.

13. **`GetHashCode` в `SnapshotRegisters` не согласован с `Equals`.**  
    Файл: `Emulator/DeviceContext.cs`, `SnapshotRegisters.GetHashCode`: `_values!.GetHashCode()`. `Equals` сравнивает 32 `ulong` поэлементно; `GetHashCode` на `InlineArray`-структуре хэширует не все 32 значения. Два `SnapshotRegisters` с `Equals == true` могут иметь разные хэши.  
    Фикс: `public override int GetHashCode() { var h = new HashCode(); for (int i = 0; i < 32; i++) h.Add(_values[i]); return h.ToHashCode(); }`.

14. **`RunStepMode` — нет возврата `ProcessorFault`.**  
    Файл: `Emulator/DeviceContext.cs`, `RunStepMode`. Ставит IP и `IsRunning = true`, но после ручного `SingleStep`-цикла пользователь не может получить `ProcessorFault` — только через `GetRegistersSnapshot` (не то) или через `AsRawDevice().GetDiagnosticData()` (unsafe extension).  
    Рекомендация: добавить `ProcessorFault GetCurrentFault()` на `DeviceContext` — `_device` уже имеет доступ к `_processor.GetDiagnosticData()` через `AsRawDevice`, но это публичный API.

15. **`ProjectExtension` (в `Nantuc.VmApplication.Extensions`) использует `CompileToIL(ulong, bool)` — старую сигнатуру.**  
    Это не в этом модуле, но связано: `CompilationToILResult` изменил конструкторы (public `errors`-only, public `(assembler, log, errors)`, internal `(assembler, log)`). `ProjectExtension.CompileToIL(...)` в Extensions-модуле может не скомпилироваться, если он использовал старый internal конструктор. Проверьте сборку Extensions после мержа.