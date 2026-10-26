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
- [Особенная информация](#особенная-информация)
- [Что исправить](#что-исправить)

## Об этом модуле

`Nantuc.VmApplication.Extensions` — это «батарейки» к основному API. Здесь нет новых абстракций ядра, только расширения поверх уже существующих: `DeviceContext`, `VmHost`, `VmHostProject`, `VmEmulator`, `IPortController`. Модуль можно не подключать — основной API работает и без него. Но с ним появляются:

- **Дизассемблер** (`Disassembler`/`StringDisassembler`): разбор `CompilationResult.Program` на `DecodedInstruction[]` + `DecodedDataChunk[]` и текстовая форма (как `objdump`).
- **I/O-устройства** (`NumericIOStream`, `SynchronousIOStream`, `QueuedIOStream`): вывод байтов/UTF-8/UTF-16 из VM в `TextWriter`. Плюс `PortCharEncoding` и `OutputPortLayout` (раскладка 8-байтного окна).
- **Стандартные оптимизации**: AST-правила (`FoldConstants`, `PropagateConstants`, `RemoveUnusedVariables`, `RemoveUnreachableCode`, `InlineSmallVoidFunctions`) и peephole-правила (`ConstantFolding`, `RemoveNoopMov`, `RemovePushPopPair`, `RemoveSwapMovPair`, `RemoveJumpToNextLabel`, `RemoveMultiEndPair`).
- **Утилиты компиляции** (`ProjectExtension`, `HostExtensions`): `host.Project.CompileToIL("int main(){}", 0, true, SourceLanguage.C)` и `host.RunAndKeep(...)`, `host.RunOnce(...)` — одна строка вместо пайплайна.
- **Встроенные библиотеки** (`Librare`): строковые константы `heap.nanasm`, `alloc.minic`, `io.nanasm`, `print.minic` для `#include`.
- **RAM-утилиты** (`DeviceContextExtensions`): `GetRamAsNewArray`, `CopyRamTo` — читать RAM работающей/остановленной VM.

Ключевая идея — **не менять контракт** `Nantuc.VmApplication`, а расширять его. Все правила реализуют `IAstOptimizationRule`/`IPeepholeRule` из компилятора, все I/O-устройства наследуют `PortControlBase` из `VmApplication`. Никаких новых интерфейсов в `Kernel.Contracts`.

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
│   ├── Disassembler.cs                       # internal static: extension(res).Disassemble(int?) → DisassemblyResult
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

**`DecodedInstruction`** (namespace `Nantuc.VmApplication.Extensions.Disassembler`):

```csharp
public readonly record struct DecodedInstruction(
    ulong Address,           // IP инструкции
    uint RawInstruction,     // исходные 4 байта
    OpCode OpCode,
    RegType Reg1,
    RegType Reg2,
    OpCodeSize DataSize,
    ulong? Data64,           // null, если immediate нет
    int ByteLength)          // 4 (без data) или 4 + pad + 8 (с data)
{
    public bool HasData64 { get; }
    public override string ToString();  // = InstructionFormatter.Format(this)
}
```

**`DecodedDataChunk`**:

```csharp
public readonly record struct DecodedDataChunk(
    ulong Address,          // IP чанка
    ulong Value,            // 64-битное значение (LE)
    byte[] Bytes,           // реальные байты (1..8)
    string? DecodedString,  // если удалось декодировать UTF-16LE (printable ASCII)
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
    public override string ToString();  // = InstructionFormatter.Format(this)
}
```

**`Disassembler`** — `internal static class`, extension:

```csharp
extension(CompilationResult res)
{
    public DisassemblyResult Disassemble(int? codeSize = null);
}
```

⚠ **Класс `internal`**, поэтому extension **не доступен из других сборок**. См. [Что исправить, п.1](#что-исправить). Внутри сборки: `compilation.Disassemble()` (весь поток как код) или `compilation.Disassemble(codeSize: compilation.CodeSize)` (с секцией данных).

**`StringDisassembler`** — `public static class`:

```csharp
extension(CompilationResult res)
{
    public string DisassembleToString();   // "(no program)" если !res.Success
}

public static string Disassemble(ReadOnlySpan<byte> program, int codeSize, ulong baseAddress = 0);
```

Повторяет логику `Disassembler.Disassemble`, но сразу в текст. Формат: `XXXXXXXX: XXXXXXXX   MNEMONIC [data = 0x...]` — как `objdump`. Для data-секции: `XXXXXXXX: XX XX ... .data 0xXXXXXXXXXXXXXXXX  "строка"`.

**`InstructionFormatter`**:

```csharp
public static string Format(DisassemblyResult result);
public static void   Format(StringBuilder sb, DisassemblyResult result);
public static string Format(DecodedInstruction inst);
public static void   Format(StringBuilder sb, DecodedInstruction inst);
public static void   Format(StringBuilder sb, DecodedDataChunk chunk);
```

Использует `InstructionEncoder.Decode(rawInstruction)` из `[Nantuc.Compiler](../Nantuc.Compiler/README.md)` для мнемоники. Data-chunk печатается с hex-байтами и ASCII-строкой (если получилось декодировать).

### I/O-устройства

**`PortCharEncoding`**:

```csharp
public enum PortCharEncoding { BytePerChar, Utf8, Utf16 }
```

- `BytePerChar` — 1 запись в порт = 1 байт (Latin-1).
- `Utf8` — 1..4 записей = 1 code point (UTF-8 через `Decoder`).
- `Utf16` — 2 записи = 1 code unit (UTF-16LE, low byte first).

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

`BytePerChar` → `EmitNumber(value)` (0..255). `Utf8`/`Utf16` → `EmitNumber(codePoint)`. Формат: `"42 "` или `"0x2A "`. Удобно для отладки кодировок.

**`QueuedIOStream : PortControlBase`** — асинхронный, через `Channel<byte>`:

```csharp
public QueuedIOStream(TextWriter writer, PortCharEncoding encoding);
```

`WritePort(Data, value)` → `Channel.Writer.TryWrite(value)` + `Interlocked.Increment(ref _pendingCount)`. `ReadPort(Status)` → `1` если pending, `0` иначе. `ReadPort(Data)` → 0. `WritePort(Flush, _)` → синхронный polling до 2 сек. `Dispose` → `Complete` + `_drainTask.Wait(5s)`.

**`PortBusExtensions`** — extension на `PortBus` из `[Nantuc.Kernel](../Nantuc.Kernel/README.md)`:

```csharp
extension(PortBus bus)
{
    public ulong GetBaseAddress(uint sector) => (ulong)sector * bus.PortsOnDevice;
}
```

### Оптимизации

**`AstOptimizerExtension`** (namespace `Nantuc.VmApplication.Extensions.Optimizators`):

```csharp
extension(VmAstOptimizer optimizer)
{
    public VmAstOptimizer AddStdRules();   // добавляет 5 правил (см. ниже)
}

public static readonly OptimizationId FunctionInlined;
public static readonly OptimizationId ConstantPropagated;
public static readonly OptimizationId ConstantFolded;
public static readonly OptimizationId NodeRemoved;
public static readonly OptimizationId VariableRemoved;
```

Стандартные AST-правила (`AddStdRules`):
1. `PropagateConstantsRule` — `IdentifierNode` → `NumberNode` если переменная объявлена с константой и не переприсваивается.
2. `FoldConstantsRule` — `Number op Number` → `Number`.
3. `RemoveUnusedVariablesRule` — убирает `VariableNode` без чтений (только если initializer side-effect-free).
4. `RemoveUnreachableCodeRule` — убирает код после `return` и `if(false)`/`while(false)`.
5. `InlineSmallVoidFunctionsRule` — инлайнит `void`-функции ≤ 10 операторов, без рекурсии.
6. `RemoveUnreachableCodeRule` — ещё раз (после инлайна). ⚠ см. [Что исправить, п.3](#что-исправить).

**`PeepholeOptimizerExtension`**:

```csharp
extension(VmPeepholeOptimizer peephole)
{
    public VmPeepholeOptimizer AddStdRules();   // 6 правил
}

public static readonly OptimizationId ConstantFolding;
public static readonly OptimizationId RemoveJumpToNextLabel;
public static readonly OptimizationId RemoveMultiEnd;
public static readonly OptimizationId RemoveNoopMove;
public static readonly OptimizationId RemovePushPop;
public static readonly OptimizationId RemoveSwapMov;
public static readonly OptimizationId ReplaceSafeAccess;   // не добавляется в AddStdRules
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
    public RamSize SizeRam { get; set; }   // ⚠ должен быть задан явно
    // ...
}
```

Заменяет `LOAD`/`STORE`/`LOAD_IND`/`STORE_IND` на `*_UNSAFE`-варианты, если адрес известен и в границах RAM. Требует явной установки `SizeRam` — иначе правило не срабатывает (см. [Что исправить, п.5](#что-исправить)).

### Утилиты компиляции и запуска

**`ProjectExtension`**:

```csharp
extension(VmHostProject proj)
{
    // Компиляция всего проекта (файлы из IFileService)
    public CompilationToILResult CompileToIL(ulong baseAddress, bool optimize);
    public CompilationResult    Compile(ulong baseAddress, bool optimize);

    // Компиляция одной строки/набора строк
    public CompilationToILResult CompileToIL(string source, ulong baseAddress, bool optimize, SourceLanguage language);
    public CompilationToILResult CompileToIL(IEnumerable<string> sources, ulong baseAddress, bool optimize, SourceLanguage language);
    public CompilationResult    Compile(string source, ulong baseAddress, bool optimize, SourceLanguage language);
    public CompilationResult    Compile(IEnumerable<string> sources, ulong baseAddress, bool optimize, SourceLanguage language);

    // Сохранение исходников
    public void SaveFileInclude(string localPath, string content);
    public void SaveFileProject(string localPath, string content);
}
```

`SaveFileInclude`/`SaveFileProject` используют `proj.FileService.Combine(paths.IncludePath/ProjectPath, localPath)` и `SaveFile`. ⚠ Без проверки `localPath` на `..` — см. [Что исправить, п.6](#что-исправить).

**`VMHostRunExtensions`**:

```csharp
public sealed record RunOutcome(
    DeviceContext? Device,
    int? DeviceId,
    CompilationResult Compilation,
    long StepCount,
    bool Success,
    IReadOnlyList<string> Diagnostics);

extension(VmHost host)
{
    public RunOutcome RunAndKeep(string source, SourceLanguage lang,
                                 RamSize? ram = null, bool optimize = false,
                                 string deviceName = "run");
    public bool RunAndCleanup(string source, SourceLanguage lang,
                              RamSize? ram = null, bool optimize = false);
    public RunOutcome RunOnce(string source, SourceLanguage lang,
                              RamSize? ram = null, bool optimize = false);
}
```

- `RunAndKeep` — компилирует `source`, создаёт `DeviceSpec` (`RamSize.MB16` по умолчанию), регистрирует на **sector 0**, грузит, запускает. Возвращает `RunOutcome` с живым `Device` — можно читать RAM/регистры после завершения.
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
- `alloc_minic` — first-fit аллокатор с free-list, использует `heap_nanasm`. Содержит важный комментарий про обход бага кодогенератора (см. [Особенная информация](#особенная-информация)).
- `io_nanasm` — `func_In`, `func_Out`.
- `print_minic` — `print8`, `print16`, `write8`, `write16`, `flush` через `io.nanasm`.

Используются через `#include "heap.nanasm"` в MiniC-исходниках (парсер `#include` резолвит их через `IFileService.ReadFile` в `ProjectBuilder.ResolveIncludesRecursive`).

### RAM-утилиты

**`DeviceContextExtensions`**:

```csharp
extension(DeviceContext ctx)
{
    public byte[] GetRamAsNewArray(nuint start, nuint length);
    public void   CopyRamTo(Span<byte> destination, nuint start);
}
```

Оба бросают `InvalidOperationException`, если `ctx.IsRunning`. Проверяют `start + length <= bufferLength` и `length <= int.MaxValue`. Копирование по-байтовое (через `INativeReadOnlyBuffer`), т.е. без `NativeMemory.Copy` — см. [Особенная информация](#особенная-информация).

## Примеры для API

**Дизассемблирование результата компиляции:**

```csharp
using Nantuc.VmApplication;
using Nantuc.VmApplication.Extensions.Disassembler;

using var host = VmHostFactory.CreateDefault();
var sources = host.Project.GetSourceFiles();
var comp = host.Project.Compile(sources, 0, optimize: false);

// Внутри сборки Extensions (или если сделать Disassembler public):
var disasm = comp.Disassemble(codeSize: comp.CodeSize);
foreach (var inst in disasm.Instructions)
    Console.WriteLine($"{inst.Address:X8}: {inst.RawInstruction:X8}  {inst.OpCode}");

// Публичный текстовый API — работает всегда:
Console.WriteLine(comp.DisassembleToString());
```

**I/O-устройство вывода (UTF-8):**

```csharp
using Nantuc.VmApplication.Extensions.Emulator.IO;

using var host = VmHostFactory.CreateDefault();
var uart = new SynchronousIOStream(Console.Out, PortCharEncoding.Utf8);
host.Emulator.RegisterDevice(uart, name: "UART0", sector: 1);

// VM на sector 0 пишет в порт 0x10 (Data) и 0x12 (Flush):
// OUT r0, r1   ; где r1 = base(UART) + Data
```

**Queued I/O — не блокирует CPU:**

```csharp
var writer = new StreamWriter("output.txt");
var uart = new QueuedIOStream(writer, PortCharEncoding.Utf8);
host.Emulator.RegisterDevice(uart, name: "UART0", sector: 1);
// VM пишет, drain-таск разгружает очередь в фоне
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
using Nantuc.VmApplication.Extensions.Optimizators;

host.Project.Optimizator.VMPeepholeOptimizer.AddStdRules();

var il = host.Project.CompileToIL(sources, 0, optimize: false);
il = host.Project.OptimizationIL(il, peepholeOptimizationCount: 5);
var comp = il.Build();
```

**Запуск «одной строкой»:**

```csharp
using Nantuc.VmApplication.Extensions.Host;
using Nantuc.VmApplication.Project;

using var host = VmHostFactory.CreateDefault();

// Просто проверить, что компилируется и не падает:
bool ok = host.RunAndCleanup("""
    int main() { return 0; }
    """, SourceLanguage.C);

// С сохранением device для инспекции:
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
}
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
    // ram[0..255] — первые 256 байт RAM
}
```

## Приближенность модуля к пользователю

**Средняя-высокая.** Это опциональный слой поверх основного API. Если вы пишете «просто запустить и посмотреть» — подключайте `Nantuc.VmApplication.Extensions` и используйте `RunAndKeep`/`RunOnce`/`AddStdRules`. Если вы пишете IDE или отладчик — используйте `Disassembler`/`StringDisassembler`/`InstructionFormatter`. Если пишете игру — `SynchronousIOStream`/`QueuedIOStream` для UART.

**Что стоит делать:**
- Подключать `AddStdRules()` в `VmHostFactory.CreateDefault`-сценарии для компиляции с оптимизациями.
- Использовать `QueuedIOStream` для неблокирующего вывода (игровой цикл), `SynchronousIOStream` — для простых случаев.
- Для отладки — `DisassembleToString()` (текст) или `Disassemble()` (структура, но только внутри сборки).
- Для сохранения RAM — `GetRamAsNewArray` (снимок) и `CopyRamTo` (в готовый `Span<byte>`).
- Задавать `ReplaceSafeMemAccessWithUnsafeRule.SizeRam` явно, если используете это правило.
- `SaveFileProject`/`SaveFileInclude` — с проверкой `localPath` на отсутствие `..` (сейчас проверки нет).

**Что не стоит делать:**
- Вызывать `RunAndKeep` несколько раз на одном `VmHost` — sector 0 будет занят (см. [Что исправить, п.4](#что-исправить)).
- Использовать `RunAndKeep` для недоверенного кода — он регистрирует VM на sector 0 без sandbox'а.
- Полагаться на `AddStdRules` для peephole — `ReplaceSafeMemAccessWithUnsafeRule` **не** добавляется автоматически.
- Ожидать, что `RunOutcome.Success` отражает runtime-фолты — он отражает только компиляцию и загрузку (см. [Что исправить, п.7](#что-исправить)).
- Использовать `Librare.alloc_minic` без понимания, что он написан в обход бага кодогенератора (см. [Особенная информация](#особенная-информация)).

## Особенная информация

### Двойной проход по data-секции

И `Disassembler.Disassemble`, и `StringDisassembler.Disassemble` делят поток на code и data по `codeSize`. Если `codeSize == 0`, весь поток — data. Если `codeSize == size`, data-секции нет. Если последняя инструкция с `data64` выходит за `codeSize` (паддинг + 8 байт), `pos` после цикла будет `> codeSize`, и data-цикл начнётся с `pos`, а не с `codeSize`. Это значит: **первые `pos - codeSize` байт data-секции фактически являются хвостом последней инструкции** и не будут показаны как data. По комментарию в коде — это осознанно («не трактуем как данные»). Но `bytesConsumed` в `DisassemblyResult` всё равно равен `pos`, поэтому `TotalSize - BytesConsumed` может быть меньше `TotalSize - CodeSize`.

### Формат вывода

Формат строки инструкции (`InstructionFormatter.Format`):
```
XXXXXXXX: XXXXXXXX   MNEMONIC [data = 0xXXXXXXXXXXXXXXXX (N)]
```
где `XXXXXXXX` — IP и raw instruction (8 hex-цифр), `MNEMONIC` — из `InstructionEncoder.Decode`.

Формат data-chunk:
```
XXXXXXXX: XX XX XX XX XX XX XX XX .data 0xXXXXXXXXXXXXXXXX  "строка"
```
8 hex-байт с паддингом (3 пробела на отсутствующий байт), `.data` — маркер, `0x...` — 64-битное значение (LE), `"строка"` — UTF-16LE printable ASCII (если удалось).

Секция данных отделяется строкой `--- data section (N bytes) ---`.

### `Librare.alloc_minic` и баг кодогенератора

В `alloc_minic` есть комментарий:
```
// ВАЖНО: обходной путь для бага кодогенерации MiniC — если функция
// держит 3-4 переменные в r5..r8, компилятор генерирует несимметричный
// push/pop и портит r5/r6 у вызывающего.
```
Это тот самый баг из `[Nantuc.Compiler](../Nantuc.Compiler/README.md)`, [Что исправить, п.1](#что-исправить): `FunctionGenerator.GenerateFunction` делает `EmitPush` в отсортированном порядке, а `EmitPop` — в порядке `UsedRegisters`, который не отсортирован. Пока баг не исправлен в компиляторе, `alloc_minic` работает как workaround: держит переменные в глобалах, а не в регистрах.

### `PortBusExtensions.GetBaseAddress`

`(ulong)sector * bus.PortsOnDevice` — вычисляет абсолютный адрес начала сектора в port-пространстве. `bus.PortsOnDevice` — `uint`, но `sector` сначала приводится к `ulong`, поэтому переполнения нет (даже при `sector = uint.MaxValue`, `sector * 64` = 274 млрд, влезает в `ulong`).

### `HostExtensions` и sector 0

`RunAndKeep` жёстко регистрирует VM на **sector 0**. Это значит:
- Повторный вызов `RunAndKeep`/`RunOnce` на том же `VmHost` после `RunAndKeep` (без `RunAndCleanup`) упадёт: `RegisterDevice` вернёт `-2` (sector busy), `GetDeviceContext(-2)` вернёт `null`, `!` даст NRE.
- `RunOnce` и `RunAndCleanup` освобождают sector 0, поэтому их можно вызывать повторно.

### `Disassembler` — internal

`internal static class Disassembler` и extension `res.Disassemble(int?)` **не доступны** из других сборок. Структурный API (`DisassemblyResult`, `DecodedInstruction`, `DecodedDataChunk`) публичный, но получить его снаружи можно только через `StringDisassembler.Disassemble` (возвращает `string`) — структурный результат недоступен. Это делает `InstructionFormatter.Format(DisassemblyResult)` практически бесполезным для внешних пользователей (они не могут создать `DisassemblyResult`).

### Ограничения платформ

- **`net10.0`**, `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` — но `unsafe`-кода в модуле нет. Возможно, включено для транзитивных зависимостей или на будущее.
- **`Thread.Sleep(1)`** в `QueuedIOStream.Flush` — в WASM либо no-op, либо `PlatformNotSupportedException`. Для WASM используйте `SynchronousIOStream`.
- **`Channel<byte>`** и `Task.Run(DrainAsync)` — работают в WASM, но фоновые задачи могут не выполниться до явного `await`. Для WASM-хоста используйте `SynchronousIOStream`.
- **`ImmutableArray<T>`** — работает в AOT/WASM, но каждая мутация через `CreateBuilder` аллоцирует. Для больших программ (10k+ инструкций) `DisassemblyResult` может занимать много памяти. Для стриминга используйте `StringDisassembler.Disassemble`.
- **`Guid.NewGuid().GetHashCode()`** в `InlineSmallVoidFunctionsRule` — стабильно в пределах процесса, но не между процессами. Для детерминированной сборки это может быть проблемой.

### Что этот модуль не делает

- Не реализует свой `IFileService` — только `DefaultFileService`/`RamFileService` из `VmApplication`.
- Не содержит UI/REPL — только библиотека.
- Не реализует `IPortController` для VM — это `DeviceContextUnsafeExtensions.AsRawDevice()` из `VmApplication.Unsafe` (для этого модуля — `AsRawDevice` не нужен, всё через `VmHost`/`DeviceContext`).
- Не регистрирует устройства автоматически — это делает пользователь через `host.Emulator.RegisterDevice(...)`.
- Не заменяет `AddStdRules` — можно добавлять свои правила вперемешку.

## Что исправить

1. **`Disassembler` — `internal`, extension недоступен снаружи.**  
   Файл: `Disassembler/Disassembler.cs`, `internal static class Disassembler` + `extension(CompilationResult res) { public DisassemblyResult Disassemble(int? codeSize = null); }`.  
   Типы `DecodedInstruction`, `DecodedDataChunk`, `DisassemblyResult` — `public`, но единственный способ их получить — этот extension, а класс `internal`. Значит, `DisassemblyResult` снаружи не получить (только `StringDisassembler.Disassemble`, который возвращает `string`).  
   Фикс: сделать `Disassembler` `public static class` (сам по себе класс не страшный — внутри только extension-метод). Если не хотите публиковать `Disassembler`, вынесите extension в `StringDisassembler` (тоже `public static`) или в отдельный `DisassemblyExtensions : public static`.

2. **`PeepholeLog.Id` — двойной префикс; `GetLogs()` очищает лог.**  
   Файл: `Optimizators/PeepholeOptimizerExtension.cs`, класс `PeepholeLog`:
   ```csharp
   public OptimizationId Id => id.WithPeepholePrefix();
   public IReadOnlyLogOptimization GetLogs() { log.Clear(); return log; }  // в каждом rule
   ```
   `PeepholeOptimizerExtension.ConstantFolding = Peephole("constant-folding")` = `peephole.constant-folding`. `new PeepholeLog(ConstantFolding).Id` = `peephole.peephole.constant-folding`. Двойной префикс.  
   Плюс: `PeepholeOptimizer.Optimize` сначала `rule.Clear()`, в конце `rule.GetLogs()`. А `GetLogs()` у большинства правил сам вызывает `log.Clear()` — то есть **лог всегда пустой**.  
   Фикс: `public OptimizationId Id => id;` (id уже с префиксом) и `GetLogs()` без `Clear()`. `Clear()` — отдельный метод, `Optimize` его вызывает сам.

3. **`AstOptimizerExtension.AddStdRules` — дублирует `RemoveUnreachableCodeRule`.**  
   Файл: `Optimizators/AstOptimizerExtension.cs`, метод `AddStdRules`:
   ```csharp
   optimizer.Add(new RemoveUnreachableCodeRule());   // строка 1
   // ...
   optimizer.Add(new InlineSmallVoidFunctionsRule());
   optimizer.Add(new RemoveUnreachableCodeRule());   // строка 6 — дубликат
   ```
   Если это намеренно (unreachable после инлайна), задокументируйте комментарием. Если нет — уберите второй вызов.

4. **`HostExtensions.RunAndKeep` — sector 0 захардкожен, `!` на `GetDeviceContext`.**  
   Файл: `Host/HostExtensions.cs`, метод `RunAndKeep`:
   ```csharp
   int id = host.Emulator.RegisterDevice(spec, 0);
   var ctx = host.Emulator.GetDeviceContext(id)!;
   ```
   Если `RegisterDevice` вернул `-1..-4` (sector 0 занят, нет места), `GetDeviceContext(-2)` вернёт `null`, `!` даст NRE.  
   Фикс: `if (id < 0) return new(null, null, comp, 0, false, [$"RegisterDevice failed: {id}"]);` и `if (ctx is null) return ...`. Плюс либо сделать sector параметром (`uint sector = 0`), либо искать первый свободный.

5. **`ReplaceSafeMemAccessWithUnsafeRule.SizeRam` — не инициализирован, правило не срабатывает.**  
   Файл: `Optimizators/IRAsmRules/ReplaceSafeMemAccessWithUnsafeRule.cs`, свойство `public RamSize SizeRam { get; set; }` с дефолтом `default(RamSize)`. `default(RamSize).Bytes == 1`, `default(RamSize).IsValid == false`. В `TryOptimize`:
   ```csharp
   if (address + (ulong)dataSize > SizeRam.Bytes) return false;
   ```
   Всегда `true` (кроме `address == 0 && dataSize == 1`) → правило — no-op.  
   Фикс: либо сделать `SizeRam` обязательным в конструкторе (`public ReplaceSafeMemAccessWithUnsafeRule(RamSize sizeRam)`), либо проверять `SizeRam.IsValid` и бросать `InvalidOperationException("SizeRam must be set")`.

6. **`ProjectExtension.SaveFileInclude/SaveFileProject` — path traversal.**  
   Файл: `Host/ProjectExtension.cs`, методы `SaveFileInclude`/`SaveFileProject`:
   ```csharp
   fs.SaveFile(fs.Combine(paths.IncludePath, localPath), content);
   ```
   Если `localPath = "../../etc/passwd"` — запись за пределы проекта. Для библиотеки, которая принимает `localPath` от пользователя (IDE, web-сервис), это дыра.  
   Фикс: `Path.GetFullPath(fs.Combine(paths.IncludePath, localPath))` и проверка, что результат начинается с `Path.GetFullPath(paths.IncludePath) + Path.DirectorySeparatorChar`. Либо запретить `..` и абсолютные пути в `localPath` явно.

7. **`RunOutcome.Success` не отражает runtime-фолты.**  
   Файл: `Host/HostExtensions.cs`, `RunAndKeep`. Возвращает `new(ctx, id, comp, ctx.StepCount, true, [])` после `ctx.Run(...)`. Если VM упала с `DivOnZero` (policy `ShouldContinue` вернул `false`), `Run` просто выходит из цикла, `IsRunning == false`, `Success == true`. Пользователь думает «всё ок».  
   Фикс: либо `RunAndKeep` должен принимать `IProcessorFaultPolicy` и возвращать информацию о фолте (сейчас он использует `IProcessorFaultPolicy.Default` из `DeviceSpec.Create` — то есть останавливает на фолте, но не сообщает о нём), либо задокументировать: «`Success` — только компиляция + загрузка; для runtime-фолтов передайте свой `IProcessorFaultPolicy` через `DeviceSpec`».

8. **`Librare` / `LibraresIncludes` / `LibraresForCompiler` — опечатки в публичном API.**  
   Файлы: `LibraresIncludes/LibraresForCompiler.cs`, namespace `Nantuc.VmApplication.Extensions.LibraresIncludes`, `public static class Librare`.  
   Должно быть `Libraries`, `LibrariesIncludes`, `LibrariesForCompiler`, `Library` (или `Libraries`). Пока не зафиксировано в потребителях — переименовать сейчас (breaking change, но опечатка стоит дороже).

9. **`StringDisassembler.Disassemble` — XML-doc не соответствует сигнатуре.**  
   Файл: `Disassembler/StringDisassembler.cs`, метод `Disassemble(ReadOnlySpan<byte> program, int codeSize, ulong baseAddress = 0)`:
   ```csharp
   /// <param name="length"> [OUTPUT] количество строк кода программы </param>
   /// <param name="size"> [OUTPUT] размер программы в byte</param>
   ```
   Но параметров `length`/`size` нет — это локальные переменные. Копипаст из старой версии. Фикс: удалить эти `<param>` или переписать под актуальные `program`/`codeSize`/`baseAddress`.

10. **`StringDisassembler.Disassemble` — `BitConverter.ToUInt32`/`ToUInt64` (endianness-зависимо).**  
    Файл: `Disassembler/StringDisassembler.cs`, строки с `BitConverter.ToUInt32(program.Slice(pos, 4))` и `BitConverter.ToUInt64(program.Slice(nextPos, 8))`. То же в `Disassembler.cs`.  
    На big-endian хосте (WASM-эмуляция) прочитает неправильно. `Nantuc.Kernel.MemoryBus` использует `BinaryPrimitives.ReadUInt32LittleEndian` — правильно.  
    Фикс: `BinaryPrimitives.ReadUInt32LittleEndian(program.Slice(pos, 4))` и `ReadUInt64LittleEndian`.

11. **`DeviceContextExtensions.CopyRamTo` — `nameof(destination)` в `ThrowIfGreaterThan`.**  
    Файл: `DeviceCtx/DeviceContextExtensions.cs`, `ArgumentOutOfRangeException.ThrowIfGreaterThan(length, bufferLength - start, nameof(destination))`. Параметр `destination` — это `Span<byte>`, а не число, поэтому сообщение «destination too big» вводит в заблуждение. Фикс: `nameof(length)` или явное сообщение через `ThrowHelper`/`throw new ArgumentOutOfRangeException(nameof(destination), ...)`.

12. **`HostExtensions.RunAndCleanup` и `RunOnce` — дублирование.**  
    Файл: `Host/HostExtensions.cs`. `RunAndCleanup` возвращает `bool`, `RunOnce` — `RunOutcome` с очищенными `Device`/`DeviceId`. Оба вызывают `RunAndKeep` + `UnregisterDevice`. Можно оставить один (`RunOnce`) и сделать `RunAndCleanup` тонкой обёрткой (`=> RunOnce(...).Success`), либо удалить `RunAndCleanup`.

13. **`ProjectExtension.LoggingFaultPolicyMinimal` — не на месте.**  
    Файл: `Host/ProjectExtension.cs`. Класс не связан с extension-методами проекта — это политика fault'ов. Фикс: перенести в `Host/HostExtensions.cs` или отдельный `Host/FaultPolicies.cs`.

14. **`InlineSmallVoidFunctionsRule` — `Guid.NewGuid().GetHashCode() & 0xFFFF`.**  
    Файл: `Optimizators/AstRules/InlineSmallVoidFunctionsRule.cs`, метод `CreateInlineBlock`. Уникальность в пределах процесса, но не между процессами (детерминизм сборки), плюс возможны коллизии (16 бит). Для inlining обычно ок, но если вы кэшируете байткод между запусками — сломается.  
    Фикс: статический `Interlocked.Increment(ref _counter)` в классе правила.

15. **`Disassembler.cs` — `ThrowIfNotSucces` (опечатка).**  
    Файл: `Disassembler/Disassembler.cs`, метод `private static void ThrowIfNotSucces`.  
    Должно быть `ThrowIfNotSuccess`. Плюс бросает `new InvalidOperationException()` без сообщения — фикс: `throw new InvalidOperationException("CompilationResult has errors; cannot disassemble.")`.

16. **`ConstantFoldingRule` — `HasLabelBetween` не проверяется для `LDI`/`LDI` (там `HasLabelBetween` есть, но порядок аргументов).**  
    Файл: `Optimizators/IRAsmRules/ConstantFoldingRule.cs`, `HasLabelBetween(items, index - 2, index)`. Проверяет `items[index-1]` (между двумя LDI и op). Если `items[index-2]` — это `LDI`, `items[index-1]` — это `LDI`, `items[index]` — `ADD`, то `HasLabelBetween(index-2, index)` проверяет `items[index-1]` на `AsmLabel` — но там `AsmInstruction`, не метка. Значит, если метка есть **перед** первой LDI, правило всё равно применится — и это правильно (метка до LDI не влияет на fold). OK, но `HasLabelBetween` не проверяет **между LDI-1 и LDI-2** — тоже правильно, там уже `LDI` (проверено). Так что логика верна, но защита от меток между LDI и `op` — есть. OK.

17. **`ReplaceSafeMemAccessWithUnsafeRule` — `LOAD_IND` смотрит на `items[index - 1]` без проверки на `AsmLabel`.**  
    Файл: `Optimizators/IRAsmRules/ReplaceSafeMemAccessWithUnsafeRule.cs`, ветка `else if (instr.OpCode is OpCode.LOAD_IND or OpCode.STORE_IND)`. Если между `LDI` и `LOAD_IND` стоит метка (например, jump target), `prev.FirstReg == instr.SecondReg` может сработать, но `address` относится к другой ветке исполнения. Фикс: `if (index > 0 && items[index-1] is AsmInstruction prev && ... && !HasLabelBetween(items, index - 1, index))` — как в `ConstantFoldingRule`.

18. **`Nantuc.VmApplication.Extensions.csproj` — `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` без `unsafe`-кода.**  
    Файл: `Nantuc.VmApplication.Extensions.csproj`. В коде модуля `unsafe` нет. Либо это для транзитивных зависимостей (но `AllowUnsafeBlocks` не наследуется через `ProjectReference` — каждая сборка решает сама), либо забыто.  
    Фикс: убрать, если действительно не нужно (или добавить комментарий «нужно для будущего»).