# Nantuc.VmApplication.Extensions — README

> Расширения к основному API: дизассемблер, I/O-устройства, стандартные оптимизации (AST и peephole), утилиты компиляции «одной строкой», встроенные библиотеки MiniC/NANASM.  
> Зависит только от `[Nantuc.VmApplication](../Nantuc.VmApplication/README.md)` (транзитивно — от `Compiler`, `Kernel`, `Contracts`, `Common`).  
> `[← Корень](../README.md) · [VmApplication](../Nantuc.VmApplication/README.md) · [Compiler](../Nantuc.Compiler/README.md) · [Kernel](../Nantuc.Kernel/README.md) · [Common](../Nantuc.Kernel.Common/README.md) · [Contracts](../Nantuc.Kernel.Contracts/README.md) · [Diagnostics](../Nantuc.Kernel.Diagnostics/README.md)`

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

`Nantuc.VmApplication.Extensions` — это «батарейки» к основному API. Здесь нет новых абстракций ядра, только расширения поверх существующих: `DeviceContext`, `VmHost`, `VmHostProject`, `VmEmulator`, `IPortController`. Модуль можно не подключать — основной API работает и без него. Но с ним появляются:

- **Дизассемблер** (`Disassembler`/`StringDisassembler`): разбор `CompilationResult.Program` на `DecodedInstruction[]` + `DecodedDataChunk[]` и текстовая форма (как `objdump`).
- **I/O-устройства** (`NumericIOStream`, `SynchronousIOStream`, `QueuedIOStream`): вывод байтов/UTF-8/UTF-16 из VM в `TextWriter`. Плюс `PortCharEncoding` и `OutputPortLayout` (раскладка 8-байтного окна).
- **Стандартные оптимизации**: AST-правила (`FoldConstants`, `PropagateConstants`, `RemoveUnusedVariables`, `RemoveUnreachableCode`, `InlineSmallVoidFunctions`) и peephole-правила (`ConstantFolding`, `RemoveNoopMov`, `RemovePushPopPair`, `RemoveSwapMovPair`, `RemoveJumpToNextLabel`, `RemoveMultiEndPair`).
- **Утилиты компиляции** (`ProjectExtension`, `HostExtensions`): `host.Project.CompileToIL("int main(){}", 0, true, SourceLanguage.C)` и `host.RunAndKeep(...)`, `host.RunOnce(...)` — одна строка вместо пайплайна.
- **Встроенные библиотеки** (`Librare`): строковые константы `heap.nanasm`, `alloc.minic` (first-fit с split/coalescing), `io.nanasm`, `print.minic` для `#include`.
- **RAM-утилиты** (`DeviceContextExtensions`): `GetRamAsNewArray`, `CopyRamTo` — читать RAM работающей/остановленной VM.

Ключевая идея — не менять контракт `Nantuc.VmApplication`, а расширять его. Все правила реализуют `IAstOptimizationRule`/`IPeepholeRule` из компилятора, все I/O-устройства наследуют `PortControlBase` из `VmApplication`. Никаких новых интерфейсов в `Kernel.Contracts`.

## Дерево локального проекта

Namespace'ы соответствуют папкам:

```
Nantuc.VmApplication.Extensions/
├── Nantuc.VmApplication.Extensions.csproj   # net10.0, ref → VmApplication
├── DeviceCtx/
│   └── DeviceContextExtensions.cs            # GetRamAsNewArray, CopyRamTo
├── Disassembler/
│   ├── DecodedDataChunk.cs                   # record struct (Address, Value, Bytes, DecodedString, ByteLength)
│   ├── DecodedInstruction.cs                 # record struct (Address, RawInstruction, OpCode, Reg1, Reg2, Size, Data64, ByteLength)
│   ├── Disassembler.cs                       # public static: extension(res).Disassemble(int?) → DisassemblyResult
│   ├── DisassemblyResult.cs                  # sealed: Instructions[], DataChunks[], BaseAddress, CodeSize, TotalSize
│   ├── InstructionFormatter.cs               # static: Format(DisassemblyResult | DecodedInstruction | DecodedDataChunk)
│   └── StringDisassembler.cs                 # static: Disassemble(ReadOnlySpan<byte>, int, ulong) → string; extension(res).DisassembleToString()
├── Emulator/IO/
│   ├── NumericIOStream.cs                    # синхронный: пишет числовые коды символов
│   ├── OutputPortLayout.cs                   # const: Data=0, Status=1, Flush=2, WindowSize=8
│   ├── PortBusExtensions.cs                  # extension(PortBus).GetBaseAddress(uint sector)
│   ├── PortCharEncoding.cs                   # enum: BytePerChar | Utf8 | Utf16
│   ├── QueuedIOStream.cs                     # async: Channel<byte> + фоновый drain
│   └── SynchronousIOStream.cs                # синхронный: пишет символы
├── Host/
│   ├── HostExtensions.cs                     # RunOutcome + RunAndKeep / RunAndCleanup / RunOnce
│   └── ProjectExtension.cs                   # Compile/CompileToIL по строке/строкам + SaveFileInclude/SaveFileProject + LoggingFaultPolicyMinimal
├── LibraresIncludes/
│   └── LibraresForCompiler.cs                # static class Librare: heap_nanasm, alloc_minic, io_nanasm, print_minic
└── Optimizators/
    ├── AstOptimizerExtension.cs              # extension(VmAstOptimizer).AddStdRules() + OptimizationId-константы
    ├── PeepholeOptimizerExtension.cs         # extension(VmPeepholeOptimizer).AddStdRules() + OptimizationId-константы + PeepholeLog
    ├── AstRules/
    │   ├── FoldConstantsRule.cs
    │   ├── InlineSmallVoidFunctionsRule.cs
    │   ├── PropagateConstantsRule.cs
    │   ├── RemoveUnreachableCodeRule.cs
    │   └── RemoveUnusedVariablesRule.cs
    └── IRAsmRules/
        ├── ConstantFoldingRule.cs
        ├── RemoveJumpToNextLabelRule.cs
        ├── RemoveMultiEndPairRule.cs
        ├── RemoveNoopMovRule.cs
        ├── RemovePushPopPairRule.cs
        ├── RemoveSwapMovPairRule.cs
        └── ReplaceSafeMemAccessWithUnsafeRule.cs
```

Замечания по организации:
- `Librares` (файл) и `Librare` (класс) и `LibraresIncludes` (namespace) — три разные опечатки одного слова `Libraries`. Это публичный API, см. [Что исправить](#что-исправить).
- `ProjectExtension.cs` содержит `LoggingFaultPolicyMinimal` — класс не про расширения проекта, а про политику fault'ов. Логичнее вынести в `Host/`.
- `AstOptimizerExtension.cs` и `PeepholeOptimizerExtension.cs` содержат не только extension-методы, но и `OptimizationId`-константы (`FunctionInlined`, `ConstantFolded`, `PeepholeOptimizerExtension.ConstantFolding`, …) — это часть публичного контракта: по этим ID вы ищете логи в `OptimizationResultLog`.

## API

### Дизассемблер

**`DecodedInstruction`**:

```csharp
public readonly record struct DecodedInstruction(
    ulong Address,
    uint RawInstruction,
    OpCode OpCode,
    RegType Reg1,
    RegType Reg2,
    OpCodeSize DataSize,
    ulong? Data64,
    int ByteLength)
{
    public bool HasData64 { get; }
    public override string ToString();  // = InstructionFormatter.Format(this)
}
```

**`DecodedDataChunk`**:

```csharp
public readonly record struct DecodedDataChunk(
    ulong Address,
    ulong Value,
    byte[] Bytes,
    string? DecodedString,
    int ByteLength);
```

**`DisassemblyResult`**:

```csharp
public sealed class DisassemblyResult(
    ImmutableArray<DecodedInstruction> instructions,
    ImmutableArray<DecodedDataChunk> dataChunks,
    ulong baseAddress,
    int codeSize,
    int totalSize,
    int bytesConsumed)
{
    public ImmutableArray<DecodedInstruction> Instructions { get; }
    public ImmutableArray<DecodedDataChunk> DataChunks { get; }
    public ulong BaseAddress { get; }
    public int CodeSize { get; }
    public int TotalSize { get; }
    public int BytesConsumed { get; }
    public bool HasDataSection { get; }
    public override string ToString();
}
```

**`Disassembler`** — теперь **`public static class`** (было `internal`), extension доступен снаружи:

```csharp
public static class Disassembler
{
    extension(CompilationResult res)
    {
        public DisassemblyResult Disassemble(int? codeSize = null);
    }
}
```

`compilation.Disassemble()` — весь поток как код; `compilation.Disassemble(codeSize: compilation.CodeSize)` — с секцией данных. Структурный результат (`DecodedInstruction[]` + `DecodedDataChunk[]`) теперь можно получить извне, а не только через `StringDisassembler.Disassemble(...)` → `string`.

**`StringDisassembler`** — `public static class`:

```csharp
extension(CompilationResult res)
{
    public string DisassembleToString();   // "(no program)" если !res.Success
}

public static string Disassemble(ReadOnlySpan<byte> program, int codeSize, ulong baseAddress = 0);
```

Формат: `XXXXXXXX: XXXXXXXX   MNEMONIC [data = 0x...]` — как `objdump`. Для data-секции: `XXXXXXXX: XX XX ... .data 0xXXXXXXXXXXXXXXXX  "строка"`.

**`InstructionFormatter`**:

```csharp
public static string Format(DisassemblyResult result);
public static void   Format(StringBuilder sb, DisassemblyResult result);
public static string Format(DecodedInstruction inst);
public static void   Format(StringBuilder sb, DecodedInstruction inst);
public static void   Format(StringBuilder sb, DecodedDataChunk chunk);
```

### I/O-устройства

**`PortCharEncoding`**:

```csharp
public enum PortCharEncoding { BytePerChar, Utf8, Utf16 }
```

**`OutputPortLayout`** — раскладка 8-байтного окна:

```csharp
public static class OutputPortLayout
{
    public const ulong WindowSize = 8;
    public const ulong Data   = 0;   // запись: байт на вывод; чтение: 0
    public const ulong Status = 1;   // 0 — готово, 1 — есть необработанные данные
    public const ulong Flush  = 2;   // запись 1 — сбросить буфер
}
```

**`SynchronousIOStream : PortControlBase`** — синхронный, блокирующий:

```csharp
public SynchronousIOStream(TextWriter writer, PortCharEncoding encoding);
```

`WritePort(Data, value)` → `_writer.Write(...)` + `Flush()`. `WritePort(Flush, _)` → `_writer.Flush()`. `ReadPort(_)` → 0. `WakeProcessor()` → `_writer.Flush()`.

**`NumericIOStream : PortControlBase`** — синхронный, но пишет **числовые коды** символов:

```csharp
public NumericIOStream(TextWriter writer, PortCharEncoding encoding, bool hex = false);
```

`BytePerChar` → `EmitNumber(value)` (0..255). `Utf8`/`Utf16` → `EmitNumber(codePoint)`. Формат: `"42 "` или `"0x2A "`.

**`QueuedIOStream : PortControlBase`** — асинхронный, через `Channel<byte>`:

```csharp
public QueuedIOStream(TextWriter writer, PortCharEncoding encoding);
```

`WritePort(Data, value)` → `Channel.Writer.TryWrite(value)` + `Interlocked.Increment`. `ReadPort(Status)` → `1` если pending, `0` иначе. `WritePort(Flush, _)` → polling до 2 сек. `Dispose` → `Complete` + `_drainTask.Wait(5s)`.

**`PortBusExtensions`** — extension на `PortBus` из `[Nantuc.Kernel](../Nantuc.Kernel/README.md)`:

```csharp
extension(PortBus bus)
{
    public ulong GetBaseAddress(uint sector) => (ulong)sector * bus.PortsOnDevice;
}
```

### Оптимизации

**`AstOptimizerExtension`**:

```csharp
extension(VmAstOptimizer optimizer)
{
    public VmAstOptimizer AddStdRules();
}

public static readonly OptimizationId FunctionInlined;      // astnode-opt.inline-func
public static readonly OptimizationId ConstantPropagated;   // astnode-opt.const-propagate
public static readonly OptimizationId ConstantFolded;       // astnode-opt.const-fold
public static readonly OptimizationId NodeRemoved;          // astnode-opt.remove-node
public static readonly OptimizationId VariableRemoved;      // astnode-opt.remove-variable-unused
```

Стандартные AST-правила (`AddStdRules`):
1. `PropagateConstantsRule` — `IdentifierNode` → `NumberNode` если переменная объявлена с константой и не переприсваивается.
2. `FoldConstantsRule` — `Number op Number` → `Number`.
3. `RemoveUnusedVariablesRule` — убирает `VariableNode` без чтений (только если initializer side-effect-free).
4. `RemoveUnreachableCodeRule` — убирает код после `return` и `if(false)`/`while(false)`.
5. `InlineSmallVoidFunctionsRule` — инлайнит `void`-функции ≤ 10 операторов, без рекурсии.
6. `RemoveUnreachableCodeRule` — ещё раз (после инлайна). См. [Что исправить, п.3](#что-исправить).

**`PeepholeOptimizerExtension`**:

```csharp
extension(VmPeepholeOptimizer peephole)
{
    public VmPeepholeOptimizer AddStdRules();
}

public static readonly OptimizationId ConstantFolding;          // peephole.constant-folding
public static readonly OptimizationId RemoveJumpToNextLabel;    // peephole.remove-jump-to-next-label
public static readonly OptimizationId RemoveMultiEnd;           // peephole.remove-multi-end
public static readonly OptimizationId RemoveNoopMove;           // peephole.remove-noop-mov
public static readonly OptimizationId RemovePushPop;            // peephole.remove-push-pop
public static readonly OptimizationId RemoveSwapMov;            // peephole.remove-swap-mov
public static readonly OptimizationId ReplaceSafeAccess;        // peephole.replace-safe-mem-acces-with-unsafe
```

Стандартные peephole-правила (`AddStdRules`):
1. `RemoveNoopMovRule` — `MOV rX, rX` (не сразу после метки).
2. `RemovePushPopPairRule` — `PUSH rX; POP rX`.
3. `RemoveSwapMovPairRule` — `MOV rA, rB; MOV rB, rA` (нетто-эффект нулевой).
4. `RemoveJumpToNextLabelRule` — `JMP label; label:` → удалить `JMP`.
5. `ConstantFoldingRule` — `LDI rA, X; LDI rB, Y; OP rA, rB` → `LDI rA, X op Y` (без меток между).
6. `RemoveMultiEndPairRule` — `END; END` → один `END`.

**`ReplaceSafeMemAccessWithUnsafeRule`** — **не входит** в `AddStdRules`, добавляется вручную:

```csharp
public sealed class ReplaceSafeMemAccessWithUnsafeRule : IPeepholeRule
{
    public RamSize SizeRam { get; set; } = RamSize.B128;   // теперь с дефолтом
}
```

Заменяет `LOAD`/`STORE`/`LOAD_IND`/`STORE_IND` на `*_UNSAFE`-варианты, если адрес известен и в границах `SizeRam`. Дефолт `RamSize.B128` (128 байт) — **заниженный**, правило сработает только на самых маленьких адресах, если `SizeRam` не выставлен под реальный размер RAM у `Device`. См. [Что исправить, п.1](#что-исправить).

**`PeepholeLog`**:

```csharp
public class PeepholeLog(OptimizationId id) : ILogOptimization
{
    public OptimizationId Id => id;                  // без дополнительного префикса
    public void Append(int passCount, AsmItem item); // форматирует AsmItem в строку
    public void Append(int pass, string message);
    public void Clear();
    public IReadOnlyList<OptimizationLogEntry> GetLogs(); // возвращает лог, НЕ очищает
}
```

### Утилиты компиляции и запуска

**`ProjectExtension`**:

```csharp
extension(VmHostProject proj)
{
    public CompilationToILResult CompileToIL(ulong baseAddress, bool optimize);
    public CompilationResult    Compile(ulong baseAddress, bool optimize);

    public CompilationToILResult CompileToIL(string source, ulong baseAddress, bool optimize, SourceLanguage language);
    public CompilationToILResult CompileToIL(IEnumerable<string> sources, ulong baseAddress, bool optimize, SourceLanguage language);
    public CompilationResult    Compile(string source, ulong baseAddress, bool optimize, SourceLanguage language);
    public CompilationResult    Compile(IEnumerable<string> sources, ulong baseAddress, bool optimize, SourceLanguage language);

    public void SaveFileInclude(string localPath, string content);
    public void SaveFileProject(string localPath, string content);
}
```

`SaveFileInclude`/`SaveFileProject` используют `proj.FileService.Combine(paths.IncludePath/ProjectPath, localPath)` и `SaveFile`. Без проверки `localPath` на `..` — см. [Что исправить, п.6](#что-исправить).

**`VMHostRunExtensions`**:

```csharp
public sealed record RunOutcome(
    DeviceContext? Device,
    int? DeviceId,
    CompilationResult Compilation,
    long StepCount,
    bool Success,
    IReadOnlyList<string> Diagnostics,
    ProcessorFault FaultInfo);   // новое: результат Run

extension(VmHost host)
{
    public RunOutcome RunAndKeep(
        string source, SourceLanguage lang,
        RamSize? ram = null, bool optimize = false,
        string deviceName = "run", uint sector = 0);

    public bool RunAndCleanup(
        string source, SourceLanguage lang,
        RamSize? ram = null, bool optimize = false, uint sector = 0);

    public RunOutcome RunOnce(
        string source, SourceLanguage lang,
        RamSize? ram = null, bool optimize = false, uint sector = 0);
}
```

- `RunAndKeep` — компилирует `source`, создаёт `DeviceSpec` (`RamSize.MB16` по умолчанию), регистрирует на `sector`, грузит, запускает. Возвращает `RunOutcome` с живым `Device` и `ProcessorFault` из `ctx.Run(...)`. **`sector` теперь параметр** (было захардкожено 0).
- `RunAndCleanup` — `RunAndKeep` + `UnregisterDevice`. Возвращает `Success`.
- `RunOnce` — то же, но возвращает `RunOutcome` с `Device = null, DeviceId = null` (чтобы не путать с «живым» device).

**`LoggingFaultPolicyMinimal`**:

```csharp
public class LoggingFaultPolicyMinimal(TextWriter output) : IProcessorFaultPolicy
{
    public bool ShouldContinue(in ProcessorFault fault);
    // пишет строку в output, возвращает false (остановить симуляцию)
}
```

### Встроенные библиотеки

**`Librare`** — `static class` со строковыми константами (namespace `Nantuc.VmApplication.Extensions.LibraresIncludes`):

- `heap_nanasm` — `func_HeapGet`, `func_HeapSet`, `func_GetSP`, `func_Load64/32/16/8`, `func_Store64/32/16/8`.
- `alloc_minic` — first-fit аллокатор **с split и coalescing** (переписан). Раскладка блока: header (size | FREE_BIT) + payload + footer (для слияния влево). Минимальный блок — 24 байта (8 hdr + 8 payload + 8 ftr). Поддерживает `mergeRight`/`mergeLeft`. `MIN_BLOCK = 24`.
- `io_nanasm` — `func_In`, `func_Out`.
- `print_minic` — `print8`, `print16`, `write8`, `write16`, `flush` через `io.nanasm`.

### RAM-утилиты

**`DeviceContextExtensions`**:

```csharp
extension(DeviceContext ctx)
{
    public byte[] GetRamAsNewArray(nuint start, nuint length);
    public void   CopyRamTo(Span<byte> destination, nuint start);
}
```

Оба бросают `InvalidOperationException`, если `ctx.IsRunning`. Проверяют `start + length <= bufferLength` и `length <= int.MaxValue`. Копирование по-байтовое (через `INativeReadOnlyBuffer`).

## Примеры для API

**Дизассемблирование результата компиляции:**

```csharp
using Nantuc.VmApplication;
using Nantuc.VmApplication.Extensions.Disassembler;

using var host = VmHostFactory.CreateDefault();
var sources = host.Project.GetSourceFiles();
var comp = host.Project.Compile(sources, 0, optimize: false);

// Структурный API — доступен снаружи, т.к. Disassembler теперь public
var disasm = comp.Disassemble(codeSize: comp.CodeSize);
foreach (var inst in disasm.Instructions)
    Console.WriteLine($"{inst.Address:X8}: {inst.RawInstruction:X8}  {inst.OpCode}");

foreach (var chunk in disasm.DataChunks)
    Console.WriteLine($"{chunk.Address:X8}: {chunk.Value:X16}  \"{chunk.DecodedString}\"");

// Или текстом
Console.WriteLine(comp.DisassembleToString());
```

**I/O-устройство вывода (UTF-8):**

```csharp
using Nantuc.VmApplication.Extensions.Emulator.IO;

using var host = VmHostFactory.CreateDefault();
var uart = new SynchronousIOStream(Console.Out, PortCharEncoding.Utf8);
host.Emulator.RegisterDevice(uart, name: "UART0", sector: 1);
```

**Queued I/O — не блокирует CPU:**

```csharp
var writer = new StreamWriter("output.txt");
var uart = new QueuedIOStream(writer, PortCharEncoding.Utf8);
host.Emulator.RegisterDevice(uart, name: "UART0", sector: 1);
```

**Стандартные AST-оптимизации:**

```csharp
using Nantuc.VmApplication.Extensions.Optimizators;

host.Project.Optimizator.VMAstOptimizer.AddStdRules();

var comp = host.Project.CompileToIL(sources, 0, optimize: true).Build();
if (comp.OptimizationResultLog is { } logs)
    foreach (var (id, log) in logs.Logs)
        foreach (var e in log.GetLogs())
            Console.WriteLine($"[{id}] pass {e.Pass}: {e.Message}");
```

**Стандартные peephole-оптимизации:**

```csharp
host.Project.Optimizator.VMPeepholeOptimizer.AddStdRules();

var il = host.Project.CompileToIL(sources, 0, optimize: false);
il = host.Project.OptimizationIL(il, peepholeOptimizationCount: 5);
var comp = il.Build();
```

**Запуск «одной строкой» с диагностикой:**

```csharp
using Nantuc.VmApplication.Extensions.Host;
using Nantuc.VmApplication.Project;
using Nantuc.Kernel.Common;

using var host = VmHostFactory.CreateDefault();

bool ok = host.RunAndCleanup("int main() { return 0; }", SourceLanguage.C);

var outcome = host.RunAndKeep("""
    int main() {
        int x = 42;
        return x;
    }
    """, SourceLanguage.C);

if (outcome.Success)
{
    var regs = outcome.Device!.GetRegistersSnapshot();
    Console.WriteLine($"r0={regs[RegType.r0]}, steps={outcome.StepCount}");

    if (outcome.FaultInfo.Status != BiosStatus.EndProgramm)
        Console.Error.WriteLine($"VM fault: {outcome.FaultInfo.Status} " +
                                $"at IP=0x{outcome.FaultInfo.InstructionPointer:X}");
}
```

**Регистрация на другом секторе:**

```csharp
// Не на sector 0, а на sector 5
var outcome = host.RunAndKeep(source, SourceLanguage.C, sector: 5);
```

**Сохранение исходников в проект:**

```csharp
host.Project.SaveFileProject("main.c", "int main() { return 0; }");
host.Project.SaveFileInclude("heap.nanasm", Librare.heap_nanasm);
```

**Чтение RAM после запуска:**

```csharp
using Nantuc.VmApplication.Extensions.DeviceCtx;

var outcome = host.RunAndKeep("int main() { int x = 42; return x; }", SourceLanguage.C);
if (outcome.Success)
{
    var ram = outcome.Device!.GetRamAsNewArray(0, 256);
}
```

## Приближенность модуля к пользователю

Средняя-высокая. Это опциональный слой поверх основного API. Если вы пишете «просто запустить и посмотреть» — подключайте `Nantuc.VmApplication.Extensions` и используйте `RunAndKeep`/`RunOnce`/`AddStdRules`. Если вы пишете IDE или отладчик — используйте `Disassembler`/`StringDisassembler`/`InstructionFormatter`. Если пишете игру — `SynchronousIOStream`/`QueuedIOStream` для UART.

**Что стоит делать:**
- Подключать `AddStdRules()` в `VmHostFactory.CreateDefault`-сценарии для компиляции с оптимизациями.
- Использовать `QueuedIOStream` для неблокирующего вывода (игровой цикл), `SynchronousIOStream` — для простых случаев.
- Для отладки — `DisassembleToString()` (текст) или `Disassemble()` (структура, теперь доступна снаружи).
- Для сохранения RAM — `GetRamAsNewArray` (снимок) и `CopyRamTo` (в готовый `Span<byte>`).
- Выставлять `ReplaceSafeMemAccessWithUnsafeRule.SizeRam` под реальный размер RAM у `Device`, если используете это правило.
- `SaveFileProject`/`SaveFileInclude` — с проверкой `localPath` на отсутствие `..`.

**Что не стоит делать:**
- Вызывать `RunAndKeep` несколько раз на одном `VmHost` с тем же `sector` — сектор будет занят (см. [Что исправить, п.4](#что-исправить)). Используйте `sector:` явно или `RunOnce`.
- Использовать `RunAndKeep` для недоверенного кода — он регистрирует VM на указанном секторе без sandbox'а.
- Полагаться на `AddStdRules` для peephole — `ReplaceSafeMemAccessWithUnsafeRule` **не** добавляется автоматически.
- Ожидать, что `RunOutcome.Success` отражает runtime-фолты — `Success` только про компиляцию и загрузку. Для статуса — `outcome.FaultInfo`.
- Использовать `Librare.alloc_minic` без понимания, что он написан в обход бага кодогенератора (см. [Особенная информация](#особенная-информация)).

## Что исправлено в этой версии

По сравнению с прошлой фиксацией `Nantuc.VmApplication.Extensions`:

- **`Disassembler` теперь `public static class`** (было `internal`). Extension `res.Disassemble(int?)` доступен извне, структурный `DisassemblyResult` можно получить без хаков.
- **`PeepholeLog.GetLogs()` больше не вызывает `log.Clear()`** — лог больше не пустой.
- **`PeepholeLog.Id => id`** (было `id.WithPeepholePrefix()`) — двойной префикс `peephole.peephole.*` исправлен.
- **`ReplaceSafeMemAccessWithUnsafeRule.SizeRam = RamSize.B128`** — у правила теперь дефолт (было `default(RamSize)`, `IsValid == false`, правило было no-op).
- **`RunOutcome` теперь содержит `ProcessorFault FaultInfo`** — можно узнать статус завершения из результата.
- **`RunAndKeep`/`RunAndCleanup`/`RunOnce` принимают `sector`** (было захардкожено `0`).
- **`RunAndKeep` прокидывает `ProcessorFault` из `ctx.Run(...)`** в `RunOutcome.FaultInfo`.
- **`Librare.alloc_minic` переписан**: first-fit с split и coalescing, header + footer, `FREE_BIT`, `MIN_BLOCK = 24`. Устраняет фрагментацию, поддерживает слияние соседей.
- **`Nantuc.VmApplication.Extensions.csproj` — убран `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`** (в коде модуля `unsafe` нет).

## Особенная информация

### Двойной проход по data-секции

И `Disassembler.Disassemble`, и `StringDisassembler.Disassemble` делят поток на code и data по `codeSize`. Если `codeSize == 0`, весь поток — data. Если `codeSize == size`, data-секции нет. Если последняя инструкция с `data64` выходит за `codeSize` (паддинг + 8 байт), `pos` после цикла будет `> codeSize`, и data-цикл начнётся с `pos`, а не с `codeSize`. Первые `pos - codeSize` байт data-секции фактически являются хвостом последней инструкции и не будут показаны как data. `bytesConsumed` в `DisassemblyResult` всё равно равен `pos`.

### `Librare.alloc_minic` — обход бага кодогенератора

`alloc_minic` держит всё состояние (`heapFreeList`, `heapCur`, `heapStart`, `FREE_BIT`, `MIN_BLOCK`) в глобалах, а не в локалах. Причина — баг `FunctionGenerator.GenerateFunction` (`[Nantuc.Compiler](../Nantuc.Compiler/README.md)`, [Что исправить, п.1](../Nantuc.Compiler/README.md#что-исправить)): несимметричный push/pop регистров калечит callee-saved регистры при 3-4 локальных переменных. Пока баг не исправлен в компиляторе, `alloc_minic` работает как workaround.

Раскладка блока:
```
+0..7    : size | FREE_BIT(бит0)        — заголовок
+8..15   : next_free (только для free)  — либо начало payload
+16..sz-8: продолжение payload / unused
+sz-8..sz: size | FREE_BIT(бит0)        — футер (для слияния влево)
```
`payload` начинается с `+8`. Минимальный блок — 24 байта (8 hdr + 8 payload + 8 ftr). При освобождении: `blkMark(b, size, 1)` → `mergeRight(b)` → `mergeLeft(b)` → `blkPush(b)`. При аллокации: first-fit по `heapFreeList`, при достаточном размере — `split` (если `csz - need >= MIN_BLOCK`), иначе `blkMark(curr, csz, 0)` (без split); иначе bump через `GetSP()`.

### `PortBusExtensions.GetBaseAddress`

`(ulong)sector * bus.PortsOnDevice` — вычисляет абсолютный адрес начала сектора в port-пространстве. `sector` сначала приводится к `ulong`, переполнения нет.

### `HostExtensions` и `sector`

`RunAndKeep`/`RunOnce`/`RunAndCleanup` принимают `sector` (по умолчанию `0`). Если `sector` занят другим устройством, `RegisterDevice` вернёт `-2`, `GetDeviceContext(-2)!` даст NRE (см. [Что исправить, п.2](#что-исправить)). `RunOnce` и `RunAndCleanup` освобождают `sector` через `UnregisterDevice`, поэтому их можно вызывать повторно с тем же `sector`. `RunAndKeep` — нет, пока не сделаете `UnregisterDevice` вручную.

### `RunOutcome.FaultInfo`

`FaultInfo` — это `ProcessorFault` из `ctx.Run(new LaunchOptions(...))`. На успехе — `BiosStatus.EndProgramm`; на падении — соответствующий статус с `InstructionPointer`/`OpCode`. Важно: `_faultData` в `Processor` не сбрасывается в `LaunchProgramm` (`[Nantuc.Kernel](../Nantuc.Kernel/README.md)`, [Что исправить, п.2](../Nantuc.Kernel/README.md#что-исправить)) — второй `Run` на том же `Device` может вернуть fault от первого, если первый был падением.

### Ограничения платформ

- `net10.0`, `<AllowUnsafeBlocks>` **не** включён (было включено — убрано, `unsafe`-кода в модуле нет).
- `Thread.Sleep(1)` в `QueuedIOStream.Flush` — в WASM либо no-op, либо `PlatformNotSupportedException`. Для WASM используйте `SynchronousIOStream`.
- `Channel<byte>` и `Task.Run(DrainAsync)` — работают в WASM, но фоновые задачи могут не выполниться до явного `await`.
- `ImmutableArray<T>` — работает в AOT/WASM, но каждая мутация через `CreateBuilder` аллоцирует. Для больших программ (10k+ инструкций) `DisassemblyResult` может занимать много памяти. Для стриминга — `StringDisassembler.Disassemble`.
- `Guid.NewGuid().GetHashCode()` в `InlineSmallVoidFunctionsRule` — стабильно в пределах процесса, но не между процессами. Для детерминированной сборки — проблема.

## Что исправить

По убыванию важности. Ссылки на файлы и методы.

1. **`ReplaceSafeMemAccessWithUnsafeRule.SizeRam` — дефолт `RamSize.B128` может быть неверным.**  
   Файл: `Optimizators/IRAsmRules/ReplaceSafeMemAccessWithUnsafeRule.cs`, свойство `public RamSize SizeRam { get; set; } = RamSize.B128;`.  
   `RamSize.B128` — 128 байт. Если реальный `Device` имеет `RamSize.MB16`, правило будет считать «безопасными» только адреса `< 128` — то есть почти все `LOAD`/`STORE` останутся safe, а `*_UNSAFE` не применится. Если наоборот (`SizeRam = RamSize.GB1`, а `Device` = `RamSize.KB64`), правило может заменить safe на unsafe для адреса за реальной границей RAM — и `Processor` упадёт с access violation.  
   Фикс: либо сделать `SizeRam` обязательным в конструкторе, либо брать реальный размер у `Device`/`Emulator` в момент добавления правила. В `AddStdRules` правило не добавляется — так что текущая защита: пользователь должен сам выставить `SizeRam`. Но дефолт всё равно опасен.

2. **`HostExtensions.RunAndKeep` — `GetDeviceContext(id)!` без проверки, `Success` не отражает fault.**  
   Файл: `Host/HostExtensions.cs`, `RunAndKeep`:
   ```csharp
   int id = host.Emulator.RegisterDevice(spec, sector);
   var ctx = host.Emulator.GetDeviceContext(id)!;
   ```
   Если `RegisterDevice` вернул `-1..-4` (sector занят, нет места), `GetDeviceContext(-2)` вернёт `null`, `!` даст NRE. Также `Success` устанавливается в `true` после `ctx.Run(...)`, даже если `fault.Status != EndProgramm`.  
   Фикс:
   ```csharp
   var state = host.Emulator.TryRegisterDevice(spec, sector, out int id);
   if (state != IdState.Valid) return new(null, null, comp, 0, false, [$"RegisterDevice failed: {state}"], default);
   if (!host.Emulator.TryGetDeviceContext(id, out var ctx)) { ... }
   var fault = ctx.Run(...);
   return new(ctx, id, comp, ctx.StepCount, fault.Status == BiosStatus.EndProgramm, [], fault);
   ```

3. **`AstOptimizerExtension.AddStdRules` — дублирует `RemoveUnreachableCodeRule`.**  
   Файл: `Optimizators/AstOptimizerExtension.cs`, `AddStdRules`: `RemoveUnreachableCodeRule` добавляется дважды (до и после `InlineSmallVoidFunctionsRule`). Если это намеренно (unreachable после инлайна), задокументируйте комментарием. Если нет — уберите второй вызов.

4. **`HostExtensions` — `RunAndKeep` не освобождает `sector`, `RunOnce`/`RunAndCleanup` — да.**  
   Файл: `Host/HostExtensions.cs`. `RunAndKeep` оставляет устройство на `sector` — повторный вызов с тем же `sector` упадёт (см. п.2). `RunOnce`/`RunAndCleanup` освобождают. Это несогласованно: имя `RunAndKeep` подразумевает «оставить device», но не «занять sector навсегда».  
   Рекомендация: задокументировать в XML-doc `RunAndKeep`, что он **не** освобождает `sector`, и вызывать `host.Emulator.UnregisterDevice(outcome.DeviceId!.Value)` для повторного использования.

5. **`ProjectExtension.SaveFileInclude`/`SaveFileProject` — path traversal.**  
   Файл: `Host/ProjectExtension.cs`, методы `SaveFileInclude`/`SaveFileProject`:
   ```csharp
   fs.SaveFile(fs.Combine(paths.IncludePath, localPath), content);
   ```
   Если `localPath = "../../etc/passwd"` — запись за пределы проекта. Для библиотеки, которая принимает `localPath` от пользователя (IDE, web-сервис), это дыра.  
   Фикс: `Path.GetFullPath(fs.Combine(paths.IncludePath, localPath))` и проверка, что результат начинается с `Path.GetFullPath(paths.IncludePath) + Path.DirectorySeparatorChar`. Либо запретить `..` и абсолютные пути в `localPath` явно.

6. **`Librare` / `LibraresIncludes` / `LibraresForCompiler` — опечатки в публичном API.**  
   Файлы: `LibraresIncludes/LibraresForCompiler.cs`, namespace `Nantuc.VmApplication.Extensions.LibraresIncludes`, `public static class Librare`.  
   Должно быть `Libraries`, `LibrariesIncludes`, `LibrariesForCompiler`, `Library` (или `Libraries`). Пока не зафиксировано в потребителях — переименовать сейчас.

7. **`Disassembler.ThrowIfNotSucces` — опечатка и пустое сообщение.**  
   Файл: `Disassembler/Disassembler.cs`, `private static void ThrowIfNotSucces(CompilationResult res)`.  
   Должно быть `ThrowIfNotSuccess`. Бросает `new InvalidOperationException()` без сообщения — фикс: `throw new InvalidOperationException("CompilationResult has errors; cannot disassemble.")`.

8. **`StringDisassembler.Disassemble` / `Disassembler.Disassemble` — `BitConverter.ToUInt32/ToUInt64` (endianness-зависимо).**  
   Файлы: `Disassembler/StringDisassembler.cs` и `Disassembler/Disassembler.cs`, строки с `BitConverter.ToUInt32(program.Slice(pos, 4))` и `BitConverter.ToUInt64(program.Slice(nextPos, 8))`.  
   На big-endian хосте прочитает неправильно. `Nantuc.Kernel.MemoryBus` использует `BinaryPrimitives.ReadUInt32LittleEndian` — правильно.  
   Фикс: `BinaryPrimitives.ReadUInt32LittleEndian(program.Slice(pos, 4))` и `ReadUInt64LittleEndian(...)`.

9. **`DeviceContextExtensions.CopyRamTo` — `nameof(destination)` в `ThrowIfGreaterThan`.**  
   Файл: `DeviceCtx/DeviceContextExtensions.cs`, `ArgumentOutOfRangeException.ThrowIfGreaterThan(length, bufferLength - start, nameof(destination))`. Параметр `destination` — `Span<byte>`, а не число, сообщение «destination too big» вводит в заблуждение.  
   Фикс: `nameof(length)`.

10. **`LoggingFaultPolicyMinimal` — не на месте.**  
    Файл: `Host/ProjectExtension.cs`. Класс не связан с extension-методами проекта — это политика fault'ов.  
    Фикс: перенести в `Host/HostExtensions.cs` или отдельный `Host/FaultPolicies.cs`.

11. **`InlineSmallVoidFunctionsRule` — `Guid.NewGuid().GetHashCode() & 0xFFFF`.**  
    Файл: `Optimizators/AstRules/InlineSmallVoidFunctionsRule.cs`, метод `CreateInlineBlock`. Уникальность в пределах процесса, но не между процессами (детерминизм сборки), плюс возможны коллизии (16 бит).  
    Фикс: статический `Interlocked.Increment(ref _counter)` в классе правила.

12. **`ConstantFoldingRule` — `HasLabelBetween` проверяет только `items[index-1]`.**  
    Файл: `Optimizators/IRAsmRules/ConstantFoldingRule.cs`, `HasLabelBetween(items, index - 2, index)` — проверяет `items[index-1]` (между двумя LDI и op). Если `items[index-2]` — это `AsmLabel` (метка перед первой LDI), правило всё равно применится — это правильно. OK. Но `ReplaceSafeMemAccessWithUnsafeRule` (`Optimizators/IRAsmRules/ReplaceSafeMemAccessWithUnsafeRule.cs`) для `LOAD_IND`/`STORE_IND` смотрит на `items[index - 1]` без проверки на `AsmLabel`. Если между `LDI` и `LOAD_IND` стоит метка (jump target), `prev.FirstReg == instr.SecondReg` может сработать, но `address` относится к другой ветке исполнения.  
    Рекомендация: добавить `!HasLabelBetween(items, index - 1, index)` в `ReplaceSafeMemAccessWithUnsafeRule`.

13. **`QueuedIOStream.Flush` — polling до 2 секунд.**  
    Файл: `Emulator/IO/QueuedIOStream.cs`, `Flush`:
    ```csharp
    var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
    while (Volatile.Read(ref _pendingCount) > 0 && DateTime.UtcNow < deadline)
        Thread.Sleep(1);
    ```
    Блокирует вызывающий поток до 2 секунд, если очередь не пустеет. В игровом цикле это может быть заметно.  
    Рекомендация: либо уменьшить deadline, либо использовать `Task`/`await` (но тогда `IPortUse.WritePort` не подходит — он `void`), либо задокументировать в XML-doc, что `Flush` блокирующий.

14. **`README.md` для `Nantuc.VmApplication.Extensions` — файл `Nantuc.VmApplication.Extensions.csproj` теперь без `AllowUnsafeBlocks`.**  
    Это не баг, но если где-то в документации упоминалось `unsafe` — обновите. В этом README раздел [Ограничения платформ](#ограничения-платформ) уже отражает актуальное состояние.

15. **`OptimizationResultLog.Upsert` (в `VmApplication`) — комментарий врёт про мердж.**  
    Не в этом модуле, но связано: `OptimizationIL` использует `logger.AddRange(log)`, а не `Upsert`. `Upsert` документирован как «мерджим записи», но реализован как перезапись. Если `AstOptimizerExtension.AddStdRules` добавил два одинаковых `OptimizationId` — второй перезапишет первый в `_logs`.  
    Рекомендация: либо исправить `Upsert` (конкатенация `OptimizationLogEntry`), либо исправить комментарий.

16. **`RunOutcome.FaultInfo` — default при ошибках компиляции/загрузки.**  
    Файл: `Host/HostExtensions.cs`, `RunAndKeep`. При `!comp.Success` или `!ctx.TryFastLoadProgram(...)` возвращается `new(..., default)` — `FaultInfo` будет `default(ProcessorFault)` с `Status = BiosStatus.Success`. Это вводит в заблуждение: fault как бы «успешный».  
    Рекомендация: либо `ProcessorFault?` в `RunOutcome`, либо явно ставить `new ProcessorFault(BiosStatus.SegmentationFault, 0, 0, OpCode.NOP)` для ошибок загрузки.