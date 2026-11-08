﻿# Nantuc.VmApplication.Extensions — README

> Расширения к основному API: дизассемблер, отладчик, I/O-устройства, стандартные оптимизации (AST и peephole), утилиты компиляции «одной строкой», встроенные библиотеки MiniC/NANASM.
> Зависит от `[Nantuc.VmApplication]` (транзитивно — от `Diagnostics`, `Kernel`, `Contracts`, `Common`; напрямую из `Common` используется `OpCode`, `AlignUp`, `InstructionDecoder`).
> `[← Корень](../README.ru.md) · [VmApplication](../../src/Nantuc.VmApplication/README.ru.md) · [Diagnostics](../../src/Nantuc.Diagnostics/README.ru.md) · [Kernel](../../src/Nantuc.Kernel/README.ru.md) · [Common](../../src/Nantuc.Kernel.Common/README.ru.md) · [Contracts](../../src/Nantuc.Kernel.Contracts/README.ru.md) · [Diagnostics](../../src/Nantuc.Kernel.Diagnostics/README.ru.md) · [STDLIBRARIES](STDLIBRARIES.ru.md)`

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

- **Дизассемблер** (`Disassembler`/`StringDisassembler`/`InstructionFormatter`): разбор `CompilationResult.Program` на `DecodedInstruction[]` + `DecodedDataChunk[]` и текстовая форма (как `objdump`).
- **Отладчик** (`Debugger`): пошаговое исполнение поверх `DeviceContext` — `Step`/`StepOver`/`StepOut`/`Continue`/`ContinueTo`/`RunToEnd`, точки останова, best-effort стек вызовов, чтение RAM (`ReadUInt16/32/64`, `ReadCString`), дизассемблирование текущей инструкции через `StringDisassembler`. Не использует `AsRawDevice` и `unsafe` — только публичный API `DeviceContext`/`ProcessorSnapshot`.
- **I/O-устройства** (`NumericIOStream`, `SynchronousIOStream`, `QueuedIOStream`): вывод байтов/UTF-8/UTF-16 из VM в `TextWriter`. Плюс `PortCharEncoding` и `OutputPortLayout` (раскладка 8-байтного окна).
- **Стандартные оптимизации**: AST-правила (`FoldConstants`, `PropagateConstants`, `RemoveUnusedVariables`, `RemoveUnreachableCode`, `InlineSmallVoidFunctions`) и peephole-правила (`ConstantFolding`, `RemoveNoopMov`, `RemovePushPopPair`, `RemoveSwapMovPair`, `RemoveJumpToNextLabel`, `RemoveMultiEnd`).
- **Утилиты компиляции** (`ProjectExtension`, `VMHostRunExtensions`): `host.Project.CompileToIL("int main(){}", 0, true, SourceLanguage.C)` и `host.RunAndKeep(...)`, `host.RunOnce(...)`, `host.RunAndCleanup(...)` — одна строка вместо пайплайна. `RunAndKeep`/`RunOnce`/`RunAndCleanup` умеют регистрировать UART через `QueuedIOStream`, если передан `output`.
- **Встроенные библиотеки** (`Libraries`): строковые константы `heap_nanasm`, `alloc_minic`, `io_nanasm`, `print_minic`, `imath_minic`, `bitops_minic`, `ovf_minic` для `#include`. Подробнее — в [STDLIBRARIES.ru.md](STDLIBRARIES.ru.md).
- **RAM-утилиты** (`DeviceContextExtensions`): `GetRamAsNewArray` / `GetAllRamAsNewArray`, `CopyRamTo` в `Span<byte>` / `byte[]` / `Memory<byte>` / `Stream`, `ReadRam<T>` / `CopyRamTo<T>` для `unmanaged`-типов, `TryCopyRamTo` без исключений — читать RAM **остановленной** VM (все, кроме `TryCopyRamTo`, бросают `InvalidOperationException` при `ctx.IsRunning`).
- **Bytecode-утилиты** (`BytecodeExtension`): `GetOpCodeSafety` / `GetUnsafeOpCodes` — статический разбор `CompilationResult.Program` на наличие `*_UNSAFE` опкодов (`OpCodeSafety.Safe`/`HasUnsafe`/`Unknown`) и поиск их местоположений (`UnsafeOpCodeLocation(OpCode, Offset, Ip)`). Полезно для проверки недоверенного кода перед запуском и для проверки результата `ReplaceSafeMemAccessWithUnsafeRule`.
Ключевая идея — не менять контракт `Nantuc.VmApplication`, а расширять его. Все правила реализуют `IAstOptimizationRule`/`IPeepholeRule` из компилятора, все I/O-устройства наследуют `PortControlBase` из `VmApplication`, отладчик работает через `DeviceContext.RunStepMode`/`SingleStep`/`GetRegistersSnapshot`/`NativeReadOnlyBuffer`. Никаких новых интерфейсов в `Kernel.Contracts`.

## Дерево локального проекта

Namespace'ы соответствуют папкам:

```
Nantuc.VmApplication.Extensions/
├── Nantuc.VmApplication.Extensions.csproj   # net10.0, ref → VmApplication
├── Bytecode/
│   └── BytecodeExtension.cs                  # extension on CompilationResult: GetOpCodeSafety / GetUnsafeOpCodes + OpCodeSafety + UnsafeOpCodeLocation + IsUnsafeOpcode
├── Diagnostics/
│   └── DiagnosticsCompilerExtension.cs                  # extension on IReadOnlyDictionary<ErrorCode,string> (ValidateAllCodesCovered / TryValidateAllCodesCovered) — см. «Что исправить», п.3
├── Debugging/
│   ├── Debugger.cs                           # пошаговый отладчик над DeviceContext: Step/StepOver/StepOut/Continue/ContinueTo/RunToEnd/breakpoints/RAM/дизассемблер/стек
│   ├── DebugStop.cs                          # readonly record struct: Reason + IP + StepCount + ProcessorSnapshot + ProcessorFault
│   ├── DebugStopReason.cs                    # enum: None/StepComplete/Breakpoint/Completed/Faulted/Sleeping/StepLimit/UserStop/Error
│   └── CallStackFrame.cs                     # readonly record struct: Index + ReturnAddress + StackAddress (best-effort)
├── DeviceCtx/
│   └── DeviceContextExtensions.cs            # GetRamAsNewArray, CopyRamTo
├── Disassembler/
│   ├── DecodedDataChunk.cs                   # record struct (Address, Value, Bytes, DecodedString, ByteLength)
│   ├── DecodedInstruction.cs                 # record struct (Address, RawInstruction, OpCode, Reg1, Reg2, DataSize, Data64?, ByteLength)
│   ├── Disassembler.cs                       # public static: extension(res).Disassemble(int?) → DisassemblyResult
│   ├── DisassemblyResult.cs                  # sealed: Instructions, DataChunks, BaseAddress, CodeSize, TotalSize, BytesConsumed, HasDataSection
│   ├── InstructionFormatter.cs               # static: Format(DisassemblyResult | DecodedInstruction); Format(StringBuilder, ...)
│   └── StringDisassembler.cs                 # static: Disassemble(ReadOnlySpan<byte>, int, ulong) → string; extension(res).DisassembleToString()
├── Emulator/IO/
│   ├── NumericIOStream.cs                    # синхронный: пишет числовые коды символов
│   ├── OutputPortLayout.cs                   # const: WindowSize=8, Data=0, Status=1, Flush=2
│   ├── PortBusExtensions.cs                  # extension(PortBus).GetBaseAddress(uint sector)
│   ├── PortCharEncoding.cs                   # enum: BytePerChar | Utf8 | Utf16
│   ├── QueuedIOStream.cs                     # async: Channel<byte> + фоновый drain
│   └── SynchronousIOStream.cs                # синхронный: пишет символы
├── Host/
│   ├── HostExtensions.cs                     # RunOutcome + RunAndKeep / RunAndCleanup / RunOnce (+ UART через QueuedIOStream, если output != null)
│   └── ProjectExtension.cs                   # Compile/CompileToIL по строке/строкам + SaveFileInclude/SaveFileProject + LoggingFaultPolicyMinimal
├── StdLibraries/
│   └── Libraries.cs                          # static class Libraries: heap_nanasm, alloc_minic, io_nanasm, print_minic, imath_minic, bitops_minic, ovf_minic
└── Optimizators/
    ├── AstOptimizerExtension.cs              # extension(VmAstOptimizer).AddStdRules() + WithASTNodePrefix + OptimizationId-константы
    ├── PeepholeOptimizerExtension.cs         # extension(VmPeepholeOptimizer).AddStdRules() + WithPeepholePrefix / WithRule + OptimizationId-константы + PeepholeLog
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
- Папка и namespace — `StdLibraries`, файл и публичный класс — `Libraries`. Исторически папка называлась `Librares`, в старых доках встречалось `Librare` — обе опечатки исправлены. Namespace `Nantuc.VmApplication.Extensions.StdLibraries`, класс `Libraries`.
- `ProjectExtension.cs` содержит `LoggingFaultPolicyMinimal` — класс не про расширения проекта, а про политику fault'ов. Логичнее вынести в `Host/`.
- `AstOptimizerExtension.cs` и `PeepholeOptimizerExtension.cs` содержат не только extension-методы, но и `OptimizationId`-константы. Они — часть публичного контракта: по этим ID вы ищете логи в `OptimizationResultLog`.
- `Diagnostics/DiagnosticsCompilerExtension.cs` — расширение над `IReadOnlyDictionary<ErrorCode, string>` (`ValidateAllCodesCovered`/`TryValidateAllCodesCovered`). По changelog 0.7.2 эти методы помечены как удалённые; в коде файл пока присутствует — см. [Что исправить](#что-исправить), п.3.
- `Debugging/` — новый слой поверх `DeviceContext`, не меняет `Nantuc.VmApplication`. Отладчик не потокобезопасен (кроме `RequestStop`), что совпадает с моделью `DeviceContext`. Стек вызовов восстанавливается эвристически — VM не хранит frame pointer. Подробности — в [Особенная информация](#особенная-информация).
- `Bytecode/` — статический разбор `CompilationResult.Program`, не трогает `DeviceContext`/`VmEmulator`. `IsUnsafeOpcode` — тот же набор, что использует `ReplaceSafeMemAccessWithUnsafeRule`, поэтому модуль можно использовать для верификации результата peephole-правила (после применения — все `LOAD`/`STORE`/`LOAD_IND`/`STORE_IND` заменены на `*_UNSAFE`).
- `Host/HostExtensions.cs` — `RunAndKeep` в текущей версии использует только safe-API (`TryRegisterDevice`, `TryGetDeviceContext`, `TryLoadProgram`), без `!`. Побочный эффект: при ошибке регистрации/загрузки возвращается `RunOutcome` с `Device == null`, а не NRE. UART регистрируется на `sector + 1`, если передан `output`.

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

**`Disassembler`** — `public static class`:

```csharp
public static class Disassembler
{
    extension(CompilationResult res)
    {
        public DisassemblyResult Disassemble(int? codeSize = null);
    }
}
```

`compilation.Disassemble()` — весь поток как код; `compilation.Disassemble(codeSize: compilation.CodeSize)` — с секцией данных.

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
public static void   Format(StringBuilder sb, DecodedDataChunk chunk);  // только StringBuilder-форма
```

### Отладчик

**`DebugStopReason`** — `enum`:

```csharp
public enum DebugStopReason
{
    None = 0,          // отладчик ещё не останавливался
    StepComplete,      // выполнена одна инструкция (Step/StepOver/StepOut)
    Breakpoint,        // сработала точка останова
    Completed,         // программа выполнила END
    Faulted,           // BiosStatus != EndProgramm и != Success
    Sleeping,          // HALT
    StepLimit,         // достигнут лимит в Continue
    UserStop,          // RequestStop()
    Error,             // внутренняя ошибка отладчика
}
```

**`DebugStop`** — `readonly record struct`:

```csharp
public readonly record struct DebugStop(
    DebugStopReason Reason,
    ulong InstructionPointer,
    long StepCount,
    ProcessorSnapshot Snapshot,
    ProcessorFault Fault)
{
    public bool IsTerminal { get; }   // Completed || Faulted || Error
    public override string ToString();
}
```

**`CallStackFrame`** — `readonly record struct`:

```csharp
public readonly record struct CallStackFrame(
    int Index,
    ulong ReturnAddress,
    ulong StackAddress);
```

**`Debugger`** — `sealed class : IDisposable`:

```csharp
public sealed class Debugger : IDisposable
{
    public Debugger(DeviceContext context);

    public DeviceContext Context { get; }
    public bool IsActive { get; }
    public DebugStop? LastStop { get; }
    public IReadOnlyCollection<ulong> Breakpoints { get; }
    public event Action<DebugStop>? Stopped;

    public void Load(ReadOnlySpan<byte> program, ulong loadAddress = 0, ulong startAddress = 0);

    public DebugStop Step();
    public DebugStop StepOver();
    public DebugStop StepOut();
    public DebugStop Continue(long? stepLimit = null);
    public DebugStop ContinueTo(ulong address, long? stepLimit = null);
    public DebugStop RunToEnd(long? stepLimit = null);

    public void RequestStop();           // безопасно из другого потока
    public void Wake();                  // снять с HALT (OpCode.WAKE)

    public void AddBreakpoint(ulong address);
    public bool RemoveBreakpoint(ulong address);
    public void ClearBreakpoints();
    public bool HasBreakpoint(ulong address);

    public SnapshotRegisters Registers { get; }
    public ulong InstructionPointer { get; }

    public byte[] ReadRam(ulong start, int length);
    public void   CopyRamTo(Span<byte> destination, ulong start);
    public byte   ReadByte(ulong address);
    public ushort ReadUInt16(ulong address);
    public uint   ReadUInt32(ulong address);
    public ulong  ReadUInt64(ulong address);
    public char   ReadChar(ulong address);
    public string ReadCString(ulong address, int maxLength = 1024);

    public string CurrentInstruction { get; }
    public string DisassembleAt(ulong address);
    public string DisassembleProgram();

    public IReadOnlyList<CallStackFrame> GetCallStack(int maxDepth = 16);

    public void Dispose();
}
```

Поведение:

- **`Load`** — `ctx.LoadProgram` + `ctx.RunStepMode(startAddress)`; после вызова `IsActive == true`, но ни одна инструкция ещё не выполнена. `Debugger` одноразовый на один `Load` — повторный вызов бросит `InvalidOperationException` (защита от утечки step-режима; для нового прогона — новый `Debugger`).
- **`Step`** — одна инструкция через `ctx.SingleStep(onStep)`; результат — `DebugStop` со снимком процессора из callback'а (`ProcessorSnapshot` из VM — с корректным `IsSleeping`).
- **`StepOver`** — если текущая инструкция — `CALL`/`CALL_IND`, доходит до адреса возврата (`ip + 4` для `CALL_IND`, `AlignUp(ip + 4, 8) + 8` для `CALL`), иначе ведёт себя как `Step`.
- **`StepOut`** — крутит `Step`, пока `rSP` не станет больше значения на момент вызова. Best-effort: если функция использует `PUSH`/`POP` для временных значений, семантика ломается — см. [Что исправить](#что-исправить), п. 12.
- **`Continue`** — крутит `SingleStep` до `Breakpoint`/`Completed`/`Faulted`/`Sleeping`/`StepLimit`/`UserStop`. На старте проверяет `_skipBreakpointOnce` (см. [Особенная информация](#особенная-информация)), чтобы `Continue` → breakpoint → `Continue` не зацикливался.
- **`ContinueTo(addr)`** — временно добавляет breakpoint на `addr`, вызывает `Continue`, снимает breakpoint. Если на `addr` уже был пользовательский breakpoint — он сохраняется.
- **`RunToEnd`** — `Continue` с временно очищенным набором breakpoints (для «догнать до конца, игнорируя точки»).
- **`RequestStop`** — единственный метод, безопасный из другого потока: `volatile bool _stopRequested`. Циклы `Continue`/`StepOver`/`StepOut` проверяют его на каждой итерации.
- **`Wake`** — `ctx.Resume()`; кладёт `OpCode.WAKE` в очередь. Нужен, если отладчик остановился на `HALT` и вы хотите продолжить без `Stop`/повторного `Load`.
- **`ReadRam`/`CopyRamTo`** — через `ctx.NativeReadOnlyBuffer`; бросают `ArgumentOutOfRangeException` при выходе за границы и `ObjectDisposedException` после `Dispose`. В отличие от `DeviceContextExtensions.GetRamAsNewArray`, **не** бросают `InvalidOperationException` при `IsRunning` — отладчик обычно читает RAM именно во время останова.
- **`ReadUInt16/32/64`** — little-endian через `BinaryPrimitives` (соответствует контракту `MemoryBus`).
- **`ReadCString`** — читает байты до `0`; `maxLength` (по умолчанию 1024) защищает от чтения всей RAM, если строка не терминирована.
- **`CurrentInstruction`/`DisassembleAt`/`DisassembleProgram`** — используют `StringDisassembler.Disassemble`; формат — как `objdump`. `DisassembleProgram` дизассемблирует диапазон `[startAddress, loadAddress + program.Length)`, зафиксированный в `Load`.
- **`GetCallStack`** — best-effort: сканирует стек (`[rSP, RamSize)`) на 8-байтные значения в диапазоне `[ProgramStart, ProgramEnd)`, выровненные по 4. Возможны ложные срабатывания (значения локальных переменных). `maxDepth` ограничивает глубину (по умолчанию 16). См. [Особенная информация](#особенная-информация).
- **`Stopped`** — событие, срабатывает после каждого `DebugStop` (включая финальный). Удобно для логирования/визуализации.

### I/O-устройства

**`PortCharEncoding`**: `enum { BytePerChar, Utf8, Utf16 }`.

**`OutputPortLayout`** — раскладка 8-байтного окна:

```csharp
public const ulong WindowSize = 8;
public const ulong Data   = 0;   // запись: байт на вывод; чтение: 0
public const ulong Status = 1;   // 0 — готово, 1 — есть необработанные данные
public const ulong Flush  = 2;   // запись 1 — сбросить буфер
```

**`SynchronousIOStream : PortControlBase`**:

```csharp
public SynchronousIOStream(TextWriter writer, PortCharEncoding encoding);
```

`WritePort(Data, value)` → `_writer.Write(...)` + `Flush()`. `WritePort(Flush, _)` → `_writer.Flush()`. `ReadPort(_)` → 0. `WakeProcessor()` → `_writer.Flush()`.

**`NumericIOStream : PortControlBase`** — синхронный, пишет **числовые коды** символов:

```csharp
public NumericIOStream(TextWriter writer, PortCharEncoding encoding, bool hex = false);
```

`BytePerChar` → `EmitNumber(value)` (0..255). `Utf8`/`Utf16` → `EmitNumber(codePoint)`. Формат: `"42 "` или `"0x2A "`.

**`QueuedIOStream : PortControlBase`** — асинхронный через `Channel<byte>`:

```csharp
public QueuedIOStream(TextWriter writer, PortCharEncoding encoding);
```

`WritePort(Data, value)` → `Channel.Writer.TryWrite(value)` + `Interlocked.Increment`. `ReadPort(Status)` → `1` если pending, `0` иначе. `WritePort(Flush, _)` → polling до 2 сек. `Dispose` → `Complete` + `_drainTask.Wait(5s)`.

**`PortBusExtensions`**:

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

extension(OptimizationId id)
{
    public OptimizationId WithASTNodePrefix();   // префикс "astnode-opt."
}

public static readonly OptimizationId FunctionInlined;      // astnode-opt.inline-func
public static readonly OptimizationId ConstantPropagated;   // astnode-opt.const-propagate
public static readonly OptimizationId ConstantFolded;       // astnode-opt.const-fold
public static readonly OptimizationId NodeRemoved;          // astnode-opt.remove-node
public static readonly OptimizationId VariableRemoved;      // astnode-opt.remove-variable-unused
```

Стандартные AST-правила (`AddStdRules`):
1. `PropagateConstantsRule` — `IdentifierNode` → `NumberNode` если переменная объявлена с константой и не переприсваивается (нигде в функции).
2. `FoldConstantsRule` — `Number op Number` → `Number`, включая унарные `-`/`!`/`~` и рекурсивно по всем выражениям (`FunctionCallNode`, `ArrayAccessNode`, `MemberAccessNode`, `AddressOfNode`, `DereferenceNode`).
3. `RemoveUnusedVariablesRule` — убирает `VariableNode`, которые нигде не читаются и никуда не присваиваются (присваивание считается использованием), только если initializer side-effect-free. Side-effect-free: `NumberNode`, `IdentifierNode`, `BinaryOpNode`, `UnaryOpNode`, а также `null`.
4. `RemoveUnreachableCodeRule` — убирает код после `return`; `if(false)` заменяет содержимым `else` (или удаляет); `if(true)` заменяет содержимым `then`; `while(false)` и `for(...; false; ...)` удаляет. Рекурсивно по вложенным блокам.
5. `InlineSmallVoidFunctionsRule` — инлайнит `void`-функции с `CountStatements(body) ≤ 10` (считая вложенные), без прямой self-recursion (`!ContainsCallTo(body, name)`). Взаимная рекурсия двух кандидатов (`A` вызывает `B`, `B` вызывает `A`) **не** отсеивается — возможна дублирующая экспансия, но не бесконечная (один проход). Тела `main` не инлайнятся.
6. `RemoveUnreachableCodeRule` — ещё раз (после инлайна). См. [Что исправить](#что-исправить), п. 9.

**`PeepholeOptimizerExtension`**:

```csharp
extension(VmPeepholeOptimizer peephole)
{
    public VmPeepholeOptimizer AddStdRules();
}

extension(OptimizationId id)
{
    public OptimizationId WithPeepholePrefix();    // префикс "peephole."
    public OptimizationId WithRule();              // суффикс "-rule"
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
2. `RemovePushPopPairRule` — `PUSH rX; POP rX` (ни `PUSH`, ни `POP` не сразу после метки).
3. `RemoveSwapMovPairRule` — `MOV rA, rB; MOV rB, rA` (не сразу после метки). См. примечание в [Что исправить](#что-исправить), п. 15: при типичной семантике `MOV dst, src` паттерн не является no-op, правило сохраняется для обратной совместимости и должно быть проверено на реальной семантике `OpCode.MOV`.
4. `RemoveJumpToNextLabelRule` — `JMP label; label:` → удалить `JMP`.
5. `ConstantFoldingRule` — `LDI rA, X; LDI rB, Y; OP rA, rB` → `LDI opInstr.FirstReg, X op Y` (без меток между `ldi1`, `ldi2`, `op`). Поддерживаемые опкоды: `ADD`, `SUB`, `MULT_INT`, `DIV`, `AND`, `OR`, `XOR`, `SHR`. Допускается «обратный» порядок регистров (`ldi1.FirstReg == op.SecondReg && ldi2.FirstReg == op.FirstReg`). При `DIV` с `right == 0` правило **no-op** (не трогает инструкции).
6. `RemoveMultiEndPairRule` — `END; END` → один `END` (цепочки из >2 `END` схлопываются последовательно, потому что `newIndex == index`).

**`ReplaceSafeMemAccessWithUnsafeRule`** — **не входит** в `AddStdRules`, добавляется вручную:

```csharp
public sealed class ReplaceSafeMemAccessWithUnsafeRule : IPeepholeRule
{
    public RamSize SizeRam { get; set; } = RamSize.B128;   // дефолт
}
```

Заменяет `LOAD`/`STORE`/`LOAD_IND`/`STORE_IND` на `*_UNSAFE`-варианты, если:
- адрес известен: для `LOAD`/`STORE` — `Immediate`, для `LOAD_IND`/`STORE_IND` — предшествующая `LDI` в регистр-адрес (`instr.SecondReg`);
- `address + dataSize ≤ SizeRam.Bytes`;
- `address % dataSize == 0` (выравнивание по размеру данных; `dataSize` из `OpCodeSize`).

Дефолт `RamSize.B128` (128 байт) — **заниженный**, правило сработает только на самых маленьких адресах, если `SizeRam` не выставлен под реальный размер RAM у `Device`. См. [Что исправить](#что-исправить).

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

`SaveFileInclude`/`SaveFileProject` используют `proj.FileService.Combine(paths.IncludePath/ProjectPath, localPath)` и `SaveFile`. Без проверки `localPath` на `..` — см. [Что исправить](#что-исправить).

**`VMHostRunExtensions`**:

```csharp
public sealed record RunOutcome(
    DeviceContext? Device,
    int? DeviceId,
    CompilationResult Compilation,
    long StepCount,
    bool Success,
    IReadOnlyList<string> Diagnostics,
    ProcessorFault FaultInfo);

extension(VmHost host)
{
    public RunOutcome RunAndKeep(
        string source, SourceLanguage lang,
        RamSize? ram = null, bool optimize = false,
        string deviceName = "run", uint sector = 0,
        TextWriter? output = null,
        PortCharEncoding encoding = PortCharEncoding.Utf16);

    public bool RunAndCleanup(
        string source, SourceLanguage lang,
        RamSize? ram = null, bool optimize = false, uint sector = 0);

    public RunOutcome RunOnce(
        string source, SourceLanguage lang,
        RamSize? ram = null, bool optimize = false, uint sector = 0);
}
```

- `RunAndKeep` — компилирует `source`, регистрирует VM (`DeviceSpec` с `RamSize.MB16` по умолчанию, если `ram == null`) на `sector`, грузит, запускает. Если передан `output != null`, дополнительно регистрирует `QueuedIOStream(output, encoding)` как устройство с именем `"{deviceName}-console"` на `sector + 1`; при ошибке регистрации UART VM откатывается. `Success` = `fault.Status == BiosStatus.EndProgramm` (а не «факт запуска»). Все операции — через safe-API (`TryRegisterDevice`, `TryGetDeviceContext`, `TryLoadProgram`): при провале возвращается `RunOutcome` с `Device == null`, `DeviceId == null`, `Success == false` и диагностикой в `Diagnostics`, без NRE и без исключений.
- `RunAndCleanup` — `RunAndKeep` + `UnregisterDevice`. Возвращает `Success`.
- `RunOnce` — `RunAndKeep` + `UnregisterDevice`, возвращает `RunOutcome` с `Device = null, DeviceId = null`.

**`LoggingFaultPolicyMinimal`**:

```csharp
public class LoggingFaultPolicyMinimal(TextWriter output) : IProcessorFaultPolicy
{
    public bool ShouldContinue(in ProcessorFault fault);
    // пишет строку в output, возвращает false (остановить симуляцию)
}
```

### Встроенные библиотеки

**`Libraries`** — `static class` со строковыми константами (namespace `Nantuc.VmApplication.Extensions.StdLibraries`):

- `heap_nanasm` — `func_HeapGet`, `func_HeapSet`, `func_GetSP`, `func_Load64/32/16/8`, `func_Store64/32/16/8`.
- `alloc_minic` — first-fit аллокатор с split и coalescing. Раскладка блока: header (size | FREE_BIT) + payload + footer. `MIN_BLOCK = 24`, `HDR_FTR = 16`.
- `io_nanasm` — `func_In`, `func_Out`.
- `print_minic` — `print8`, `print16`, `write8`, `write16`, `flush` через `io_nanasm`.
- `imath_minic` — `imin`/`imax`/`umin`/`umax`, `iclamp`/`uclamp`, `iabs`, `igcd`/`ugcd`, `ipow`, `uisqrt`, `idiv_floor`/`idiv_ceil`/`udiv_round_up`.
- `bitops_minic` — `upopcount`, `uclz`/`uctz`, `rotl64`/`rotr64`, `urev64`, `unext_pow2`.
- `ovf_minic` — `add_ovf_i32`/`sub_ovf_i32`/`mul_ovf_i32`, `add_ovf_u64`/`sub_ovf_u64`/`mul_ovf_u64`.

`imath_minic`, `bitops_minic` и `ovf_minic` не требуют `#include` других библиотек — независимы. Подробнее — в [STDLIBRARIES.ru.md](STDLIBRARIES.ru.md).

**Важно:** баг `FunctionGenerator.GenerateFunction` (несимметричный push/pop callee-saved регистров, калечивший `alloc_minic` при 3-4 локалах) **исправлен**. Поэтому workaround «держать состояние в глобалах» больше не обязателен: в следующей итерации `alloc_minic` можно (и стоит) переписать с локалами. Текущая версия кода всё ещё держит `heapFreeList`/`heapCur`/`heapStart`/`FREE_BIT`/`MIN_BLOCK`/`HDR_FTR` в глобалах как легаси. Подробнее — в [STDLIBRARIES.ru.md](STDLIBRARIES.ru.md).

### RAM-утилиты

**`DeviceContextExtensions`** — extension на `DeviceContext`. Все методы читают RAM через `NativeReadOnlyBuffer`; ни один **не** использует `unsafe`/`AsRawDevice` (только `IReadOnlyNativeBuffer`). `<AllowUnsafeBlocks>` не требуется.

```csharp
extension(DeviceContext ctx)
{
    // ===== Массивы =====

    public byte[] GetRamAsNewArray(nuint start, nuint length);
    public byte[] GetRamAsNewArray(nuint start);              // от start до конца RAM
    public byte[] GetAllRamAsNewArray();                      // вся RAM

    // ===== Span / Memory / byte[] =====

    public void CopyRamTo(Span<byte> destination, nuint start);
    public void CopyRamTo(Span<byte> destination, nuint start, nuint length);
    public void CopyRamTo(byte[] destination, int destinationIndex, nuint start, int length);
    public void CopyRamTo(Memory<byte> destination, nuint start);

    // ===== Try* (без исключений) =====

    public bool TryCopyRamTo(Span<byte> destination, nuint start);

    // ===== Типизированные чтения (unmanaged) =====

    public T ReadRam<T>(nuint byteOffset) where T : unmanaged;
    public void CopyRamTo<T>(Span<T> destination, nuint byteOffset) where T : unmanaged;

    // ===== Поток — для больших объёмов без одной огромной аллокации =====

    public void CopyRamTo(Stream stream, nuint start, nuint length, int bufferSize = 81920);
}
```

Поведение:

- **Все методы, кроме `TryCopyRamTo`**, бросают `InvalidOperationException`, если `ctx.IsRunning`. `TryCopyRamTo` в этом случае возвращает `false`. Это согласуется с моделью `DeviceContext`: во время `IsRunning` буфер реестра-visible, и синхронное чтение дало бы race-y данные.
- **Границы** проверяются через `ArgumentOutOfRangeException.ThrowIfGreaterThan(start, bufferLength)` и `(length, bufferLength - start)`. Для `GetRamAsNewArray(start, length)` дополнительно `length <= int.MaxValue` (нельзя выделить массив больше).
- **`CopyRamTo(Span<byte>, start, length)`** копирует ровно `length` байт, даже если `destination.Length` больше — это удобно для буферов «с запасом».
- **`CopyRamTo(byte[], destinationIndex, start, length)`** — convenience-обёртка над `destination.AsSpan(destinationIndex, length)`; проверяет `ArgumentNullException`/`ArgumentOutOfRangeException` для `destinationIndex`/`length`.
- **`CopyRamTo(Stream, start, length, bufferSize = 81920)`** — читает RAM чанками по `bufferSize` и пишет в `stream.Write(...)`. Границы `[start, start + length)` проверяются один раз до цикла; за пределы RAM не выйдет.
- **`ReadRam<T>` / `CopyRamTo<T>`** — `MemoryMarshal.Read<T>` / `MemoryMarshal.AsBytes(...)` над временным byte-буфером, заполненным по-байтово. Никакого `unsafe` — только `MemoryMarshal` и `System.Runtime.CompilerServices.Unsafe.SizeOf<T>()` (последний не требует `<AllowUnsafeBlocks>`).
- **Копирование по-байтовое** (`for (int i = 0; i < destination.Length; i++) destination[i] = buf[(nuint)i + start];`) — медленнее `Buffer.MemoryCopy`, но не требует `unsafe`. Для больших объёмов лучше `CopyRamTo(Stream, ...)` (он хотя бы не аллоцирует один огромный массив).
- **`CopyRamTo<T>` на `Span<T>` с `start` в байтах** (не в элементах `T`): `CopyRamTo<T>(Span<T>, nuint)` берёт `MemoryMarshal.AsBytes(destination)` — смещение `byteOffset` в байтах RAM.

### Bytecode-утилиты

**`BytecodeExtension`** — `public static class` в namespace `Nantuc.VmApplication.Extensions.Bytecode`. Статический разбор `CompilationResult.Program`, не трогает `DeviceContext`/`VmEmulator`. Полезно для проверки недоверенного кода и для верификации `ReplaceSafeMemAccessWithUnsafeRule`.

```csharp
public static class BytecodeExtension
{
    public enum OpCodeSafety { Safe, HasUnsafe, Unknown }

    public readonly record struct UnsafeOpCodeLocation(
        OpCode OpCode,
        int    Offset,   // смещение в Program (байтовый массив)
        ulong  Ip);      // виртуальный адрес инструкции

    extension(CompilationResult res)
    {
        public OpCodeSafety GetOpCodeSafety();
        public IReadOnlyList<UnsafeOpCodeLocation> GetUnsafeOpCodes();
        public IReadOnlyList<UnsafeOpCodeLocation> GetUnsafeOpCodes(out bool fullyScanned);
    }

    public static bool IsUnsafeOpcode(OpCode opCode);
}
```

Поведение:

- **`GetOpCodeSafety()`**:
  - `Unknown` — если `!res.Success`, `res.CodeSize` (или `Program.Length`, если `CodeSize` == null) `< 0`, `> Program.Length`, или встретилась усечённая инструкция (`pos + 4 > Program.Length`, либо для `HasNeed64IntData`-опкода `nextPos + 8 > Program.Length`).
  - `HasUnsafe` — если найден хотя бы один `*_UNSAFE` опкод.
  - `Safe` — если программа просканирована до `actualCodeSize` и unsafe-опкодов нет.
- **`GetUnsafeOpCodes()`** — возвращает список всех `*_UNSAFE` опкодов с их `OpCode`, `Offset` (в `Program`) и `Ip` (виртуальный адрес = `StartAdress + pos`). При ошибках/усечении — возвращает то, что успело накопиться; полный список гарантирован, только если `fullyScanned == true`.
- **`GetUnsafeOpCodes(out bool fullyScanned)`** — вариант с флагом: `fullyScanned == false` означает «разбор остановился на усечённой/битой программе, список может быть неполным». `!res.Success` → `fullyScanned = false`, пустой список.
- **`IsUnsafeOpcode`** — `true` для `LOAD_UNSAFE`, `STORE_UNSAFE`, `STORE_IND_UNSAFE`, `LOAD_IND_UNSAFE`. Тот же набор, что и у `ReplaceSafeMemAccessWithUnsafeRule`.

Разбор учитывает длину инструкции: 4 байта для обычной, плюс паддинг до `AlignUp(8)` плюс 8 байт immediate для опкодов с `HasNeed64IntData` (например, `LDI`, `SYSCALL`, `CALL`). Это соответствует §3.2 [`bytecode.md`](../docs/bytecode.md).

### `DiagnosticsCompilerExtension`

Расширение над `IReadOnlyDictionary<ErrorCode, string>`:

```csharp
extension(IReadOnlyDictionary<ErrorCode, string> templates)
{
    public void ValidateAllCodesCovered();
    public bool TryValidateAllCodesCovered(out ErrorCode code);
}
```

Используется для валидации пользовательских провайдеров сообщений (`DefaultErrorMessageProvider.Templates`). По changelog 0.7.2 эти методы помечены как удалённые — файл в коде пока присутствует, см. [Что исправить](#что-исправить), п.3.

## Примеры для API

**Дизассемблирование результата компиляции:**

```csharp
using Nantuc.VmApplication;
using Nantuc.VmApplication.Extensions.Disassembler;

using var host = VmHostFactory.CreateDefault();
var sources = host.Project.GetSourceFiles();
var comp = host.Project.Compile(sources, 0, optimize: false);

var disasm = comp.Disassemble(codeSize: comp.CodeSize);
foreach (var inst in disasm.Instructions)
    Console.WriteLine($"{inst.Address:X8}: {inst.RawInstruction:X8}  {inst.OpCode}");

foreach (var chunk in disasm.DataChunks)
    Console.WriteLine($"{chunk.Address:X8}: {chunk.Value:X16}  \"{chunk.DecodedString}\"");

Console.WriteLine(comp.DisassembleToString());
```

**Пошаговая отладка с точками останова:**

```csharp
using Nantuc.Kernel.Common;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Extensions.Debugging;
using Nantuc.VmApplication.Project;

using var host = VmHostFactory.CreateDefault();

var sources = host.Project.GetSourceFiles();
var comp = host.Project.Compile(sources, baseAddress: 0, optimize: false);
if (!comp.Success) { foreach (var e in comp.Errors!) Console.Error.WriteLine(e); return; }

var state = host.Emulator.TryRegisterDevice(
    DeviceSpec.Create(ramSize: RamSize.MB16, name: "dbg"), sector: 0, out int vmId);
if (state != IdState.Valid) return;
if (!host.Emulator.TryGetDeviceContext(vmId, out var vm)) return;

using var dbg = new Debugger(vm);
dbg.Load(comp.Program!, loadAddress: 0, startAddress: 0);

dbg.AddBreakpoint(0x00);           // остановиться на входе в main
dbg.Stopped += s => Console.WriteLine($"STOP: {s}");

// Шаг с обходом CALL
var stop = dbg.StepOver();
Console.WriteLine($"after step-over: {stop}");
Console.WriteLine($"  at: {dbg.CurrentInstruction}");

// Продолжить до breakpoint'а, но не больше 100k шагов
stop = dbg.Continue(stepLimit: 100_000);
if (stop.Reason == DebugStopReason.Breakpoint)
    Console.WriteLine($"  hit bp at 0x{stop.InstructionPointer:X}");

// Продолжить до конца, игнорируя все breakpoints
var final = dbg.RunToEnd();
Console.WriteLine($"final: {final.Reason} after {final.StepCount} steps");

// Инспекция RAM и регистров
var regs = dbg.Registers;
Console.WriteLine($"r0={regs[RegType.r0]}, sp=0x{regs[RegType.rSP]:X}");

byte[] head = dbg.ReadRam(0, 32);
ulong counter = dbg.ReadUInt64(0x100);
Console.WriteLine($"counter={counter}");

foreach (var frame in dbg.GetCallStack())
    Console.WriteLine($"#{frame.Index} return=0x{frame.ReturnAddress:X8}");
```

**Прервать длинный прогон из другого потока:**

```csharp
using Nantuc.VmApplication.Extensions.Debugging;

using var dbg = new Debugger(vm);
dbg.Load(program, 0, 0);

var runner = Task.Run(() => dbg.Continue());     // может крутиться долго
Thread.Sleep(200);
dbg.RequestStop();                                // volatile bool, безопасно
var stop = runner.Result;                         // Reason == UserStop
```

> `RequestStop` — единственный потокобезопасный метод `Debugger`; остальные (`Step`/`Continue`/`Load`) должны вызываться из одного потока-координатора.

**Останов на HALT → продолжить:**

```csharp
var stop = dbg.Continue();
if (stop.Reason == DebugStopReason.Sleeping)
{
    // VM в HALT, ждёт WAKE_INT от устройства
    // … обработка сна снаружи …
    dbg.Wake();                                    // Resume() → OpCode.WAKE в очередь
    stop = dbg.Continue();
}
```

**Продолжить до конкретной функции (по её адресу):**

```csharp
dbg.ContinueTo(address: 0x200);                    // временный breakpoint на 0x200
// после — 0x200 в dbg.Breakpoints уже нет
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

**Запуск с выводом в консоль через UART:**

```csharp
var outcome = host.RunAndKeep(source, SourceLanguage.C, output: Console.Out);
// UART зарегистрирован на sector + 1, кодировка по умолчанию — Utf16.
// Чтобы получить Utf8 — передайте encoding: PortCharEncoding.Utf8.
```

**Регистрация на другом секторе:**

```csharp
var outcome = host.RunAndKeep(source, SourceLanguage.C, sector: 5);
// UART (если output != null) займёт sector 6.
```

**Сохранение исходников в проект:**

```csharp
using Nantuc.VmApplication.Extensions.StdLibraries;

host.Project.SaveFileProject("main.c", "int main() { return 0; }");
host.Project.SaveFileInclude("heap.nanasm", Libraries.heap_nanasm);
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

**Проверка недоверенного кода перед запуском:**

```csharp
using Nantuc.VmApplication.Extensions.Bytecode;

var comp = host.Project.Compile("int main() { return 0; }", 0, optimize: false);
if (!comp.Success) { /* … */ }

switch (comp.GetOpCodeSafety())
{
    case OpCodeSafety.Safe:
        // все обращения к памяти — *_UNSAFE, потенциально небезопасны
        break;
    case OpCodeSafety.HasUnsafe:
        foreach (var loc in comp.GetUnsafeOpCodes())
            Console.Error.WriteLine($"unsafe {loc.OpCode} at 0x{loc.Ip:X} (offset {loc.Offset})");
        break;
    case OpCodeSafety.Unknown:
        Console.Error.WriteLine("program is truncated or not compiled");
        break;
}
```

> `Safe` означает «нет `*_UNSAFE` опкодов», а не «безопасно». Обычные `LOAD`/`STORE` тоже могут обращаться к произвольным адресам — на это влияет только `SizeRam` в `ReplaceSafeMemAccessWithUnsafeRule` и границы `Device`. `HasUnsafe`, наоборот, означает, что код **точно** содержит небезопасные обращения — их наличие нужно либо принять (если это доверенный код), либо запретить (если это недоверенный код).

**Проверка результата `ReplaceSafeMemAccessWithUnsafeRule`:**

```csharp
host.Project.Optimizator.VMPeepholeOptimizer.AddStdRules();
// … собрать IL с peephole …
var comp = il.Build();

if (comp.GetOpCodeSafety() != OpCodeSafety.Safe)
    foreach (var loc in comp.GetUnsafeOpCodes())
        Console.Error.WriteLine($"missed at 0x{loc.Ip:X}: {loc.OpCode}");
```

## Приближенность модуля к пользователю

Средняя-высокая. Это опциональный слой поверх основного API. Если вы пишете «просто запустить и посмотреть» — подключайте `Nantuc.VmApplication.Extensions` и используйте `RunAndKeep`/`RunOnce`/`AddStdRules`. Если вы пишете IDE или отладчик — используйте `Debugger`/`Disassembler`/`StringDisassembler`/`InstructionFormatter`. Если пишете игру — `SynchronousIOStream`/`QueuedIOStream` для UART.

**Что стоит делать:**
- Подключать `AddStdRules()` в `VmHostFactory.CreateDefault`-сценарии для компиляции с оптимизациями.
- Использовать `QueuedIOStream` для неблокирующего вывода (игровой цикл), `SynchronousIOStream` — для простых случаев.
- Для отладки — `DisassembleToString()` (текст) или `Disassemble()` (структура).
- Для сохранения RAM — `GetRamAsNewArray` (снимок) и `CopyRamTo` (в готовый `Span<byte>`).
- Выставлять `ReplaceSafeMemAccessWithUnsafeRule.SizeRam` под реальный размер RAM у `Device`.
- `SaveFileProject`/`SaveFileInclude` — с проверкой `localPath` на отсутствие `..`.
- Своим оптимизационным правилам — присваивать `OptimizationId` через `WithPeepholePrefix()` / `WithRule()` / `WithASTNodePrefix()`, чтобы префикс совпадал со встроенными.
- Для отладки — `Debugger` поверх `DeviceContext`: `dbg.Load(program)` → `dbg.Step()`/`StepOver()`/`StepOut()`/`Continue()`, `dbg.AddBreakpoint(addr)`, `dbg.CurrentInstruction`, `dbg.GetCallStack()`. Для останова длинного прогона — `dbg.RequestStop()` из другого потока.
- Для отладки в IDE — подписаться на `dbg.Stopped`, чтобы обновлять панель регистров/стека/RAM на каждом останове.
- Для «догнать до конца» (например, CI-прогон) — `dbg.RunToEnd(stepLimit: N)`, не `Continue` (последний застрянет на первой точке останова).
- Для типовых целочисленных операций и проверок переполнения — `imath.minic`, `bitops.minic`, `ovf.minic` вместо ручных реализаций.
- Для недоверенного кода — проверять `comp.GetOpCodeSafety()` и/или перечислять `comp.GetUnsafeOpCodes()` до загрузки VM; это дешёвый статический чек (`*_UNSAFE`-опкоды), не заменяющий sandbox.
- Для вывода из VM в консоль/файл без ручной регистрации UART — `host.RunAndKeep(source, lang, output: writer)` (UART будет зарегистрирован на `sector + 1`, кодировка по умолчанию — `Utf16`).

**Что не стоит делать:**
- Вызывать `RunAndKeep` несколько раз на одном `VmHost` с тем же `sector` — сектор будет занят. Используйте `sector:` явно или `RunOnce`.
- Вызывать `RunAndKeep` несколько раз на одном `VmHost` с `output != null` без `UnregisterDevice` — сектор `sector + 1` тоже будет занят UART'ом.
- Использовать `RunAndKeep` для недоверенного кода — он регистрирует VM на указанном секторе без sandbox'а.
- Полагаться на `AddStdRules` для peephole — `ReplaceSafeMemAccessWithUnsafeRule` **не** добавляется автоматически.
- Ожидать, что `RunOutcome.Success` отражает только факт запуска — `Success == (fault.Status == BiosStatus.EndProgramm)`, т.е. программа действительно дошла до `END`. Runtime-фолты видны и в `Success`, и в `FaultInfo`.
- Использовать `Libraries.alloc_minic` как «эталон» — там всё ещё глобалы как легаси (workaround больше не нужен, но код пока не упрощён), и в `free` есть баг (см. [Что исправить](#что-исправить), п. 14).
- Вызывать `Step`/`Continue`/`Load` из разных потоков на одном `Debugger` — не потокобезопасно (кроме `RequestStop`). Модель — та же, что у `DeviceContext`.
- Ожидать точного стека вызовов от `GetCallStack()` — VM не хранит frame pointer, восстановление эвристическое; возможны ложные срабатывания.
- Полагаться на `stop.Snapshot.IsSleeping` в `Continue` — флаг сна приходит только из `_stepSnapshot` (после `SingleStep`); при останове через `RequestStop` он может быть `false`, даже если VM в `HALT`. Для `HALT` надёжнее `stop.Reason == Sleeping`.
- Использовать один `Debugger` для нескольких `Load` — после завершения программы (или ручного `Dispose`) создавайте новый экземпляр. Повторный `Load` бросит `InvalidOperationException` (защита от утечки step-режима).
- Ожидать от `Debugger.ReadRam` защиту от `IsRunning` (как у `DeviceContextExtensions.GetRamAsNewArray`) — отладчик читает RAM во время останова; если вызвать `ReadRam` из другого потока во время `Continue`, данные будут race-y.
- Считать `GetOpCodeSafety() == Safe` гарантией памяти-безопасности. `Safe` — это «нет `*_UNSAFE`», а не «нет произвольных адресов»: обычные `LOAD`/`STORE` тоже пишут по адресу из регистра. Для реальной защиты — комбинация `ReplaceSafeMemAccessWithUnsafeRule`, `SizeRam` = фактический размер RAM и границы `Device`/`MemoryBus`. `GetOpCodeSafety` — это сигнал «есть ли `*_UNSAFE`», а не «безопасен ли код».

## Особенная информация

### Двойной проход по data-секции

И `Disassembler.Disassemble`, и `StringDisassembler.Disassemble` делят поток на code и data по `codeSize`. Если `codeSize == 0`, весь поток — data. Если `codeSize == size`, data-секции нет. Если последняя инструкция с `data64` выходит за `codeSize` (паддинг + 8 байт), `pos` после цикла будет `> codeSize`, и data-цикл начнётся с `pos`, а не с `codeSize`. Первые `pos - codeSize` байт data-секции фактически являются хвостом последней инструкции и не будут показаны как data. `bytesConsumed` в `DisassemblyResult` всё равно равен `pos`.

### Отладчик: `_skipBreakpointOnce` и повторный `Continue`

`Continue` на старте проверяет два условия:
1. `_skipBreakpointOnce == rIP` → сбрасывает флаг и **не** останавливается на текущем адресе (это тот же breakpoint, что сработал в прошлый раз).
2. `HasBreakpoint(rIP)` → возвращает `DebugStopReason.Breakpoint` без шага.

`StepOneOrFinish` после каждого `SingleStep` выставляет `_skipBreakpointOnce = newIp`, если на новом `rIP` есть breakpoint. Это даёт корректную семантику:

```
Load → Continue (hit bp at 0x00) → Continue (skip 0x00, идёт дальше)
```

Без `_skipBreakpointOnce` `Continue` → breakpoint → `Continue` крутился бы бесконечно на том же `rIP`.

`ContinueTo(addr)` использует ту же логику: добавляет `addr` в `_breakpoints`, вызывает `Continue`, снимает breakpoint. Если `addr` уже был пользовательским breakpoint'ом — он сохраняется после вызова.

### Отладчик: `ProcessorSnapshot` в `DebugStop`

В `Step`/`StepOver`/`StepOut`/`Continue` `ProcessorSnapshot` берётся из `_stepSnapshot` (`SingleStep(onStep)` callback), поэтому:
- `IsSleeping` корректен (VM выставляет `_isSleeping` до вызова callback'а);
- `IsRunning` корректен (VM сбрасывает его до вызова callback'а);
- `RawInstruction` — слово по **текущему** `rIP` (после шага).

В `Continue`-ветке, где останов происходит до шага (initial-IP check для breakpoint'а), `Snapshot` строится через `BuildSnapshotFromRegisters` — там `IsSleeping == false` всегда, потому что `DeviceContext` не отдаёт флаг сна. Для детекции `HALT` надёжнее `stop.Reason == Sleeping` (это приходит только из `_stepSnapshot`).

### Отладчик: `GetCallStack` — эвристика, не точный стек

VM не хранит frame pointer и не ведёт отдельный call stack. `GetCallStack` сканирует `[rSP, RamSize)` на 8-байтные значения в диапазоне `[ProgramStart, ProgramEnd)`, выровненные по 4. Это находит **адреса возврата**, положенные `CALL` (см. [`bytecode.md` §5.6](../docs/bytecode.md#56-управление-потоком)), но:

- **Ложные срабатывания** — локальные `ulong`, значения которых случайно попали в диапазон кода.
- **Пропуски** — если `CALL` был indirect (`CALL_IND`), адрес возврата тоже на стеке; но если программа использует свой ручной call stack (как `print_minic`/`alloc_minic`), `rSP` может указывать не туда.
- **Порядок** — `frames[0]` — самый верхний (ближайший к текущему `rSP`), `frames[N]` — самый глубокий.

Для точного стека нужен frame pointer (в VM его нет) или отдельный стек вызовов в `Processor` (не реализован). В отладчике — только best-effort.

### Отладчик: `StepOver` и адрес возврата

Для `CALL` (immediate) длина инструкции — 12 байт (4 + 4 паддинга + 8 immediate), поэтому адрес возврата — `AlignUp(ip + 4, 8) + 8`. Для `CALL_IND` (indirect) длина — 4 байта, адрес возврата — `ip + 4`. Это соответствует §3.2 и §5.6 [`bytecode.md`](../docs/bytecode.md). Ошибка здесь привела бы к «проваливанию» в вызываемую функцию.

### `Libraries.alloc_minic`

Раскладка блока:
```
+0..7    : size | FREE_BIT(бит0)        — заголовок
+8..15   : next_free (только для free)  — либо начало payload
+16..sz-8: продолжение payload / unused
+sz-8..sz: size | FREE_BIT(бит0)        — футер (для слияния влево)
```
`payload` начинается с `+8`. Минимальный блок — 24 байта. `HDR_FTR = 16` (8 hdr + 8 ftr) прибавляется к выровненному `payload`'у в `alloc`: `need = ((size + 7) & ~7) + HDR_FTR`, но не меньше `MIN_BLOCK`. При освобождении: `blkMark(b, size, 1)` → `mergeRight(b)` → `mergeLeft(b)` → `blkPush(b)`. При аллокации: first-fit по `heapFreeList`, при достаточном размере — `split` (если `csz - need >= MIN_BLOCK`), иначе `blkMark(curr, csz, 0)` (без split); иначе bump через `GetSP()`.

**Известный баг в текущей версии:** в начале `free` стоит `if (blkFree(b)) return;` **до** объявления `ulong b = ptr - 8;` — обращение к необъявленной переменной. См. [Что исправить](#что-исправить), п. 14.

**Историческая справка:** раньше `alloc_minic` держал всё состояние в глобалах из-за бага `FunctionGenerator.GenerateFunction` в компиляторе (несимметричный push/pop callee-saved регистров при 3-4 локалах). Баг исправлен — workaround больше не нужен, но текущий код ещё не мигрирован на локалы. Детали — в [STDLIBRARIES.ru.md](STDLIBRARIES.ru.md).

### `Libraries.imath_minic`, `Libraries.bitops_minic`, `Libraries.ovf_minic`

Три независимые библиотеки поверх стандартных операторов MiniC — без `#include` и без ассемблерных обёрток:

- `imath_minic` — `imin`/`imax`/`umin`/`umax`, `iclamp`/`uclamp`, `iabs`, `igcd`/`ugcd`, `ipow`, `uisqrt`, `idiv_floor`/`idiv_ceil`/`udiv_round_up`.
- `bitops_minic` — `upopcount` (Kernighan), `uclz`/`uctz` (`0..64`, `64` для нуля), `rotl64`/`rotr64` (`n & 63`), `urev64`, `unext_pow2` (`0` — переполнение для `x > 2^63`).
- `ovf_minic` — `add_ovf_i32`/`sub_ovf_i32`/`mul_ovf_i32` (для signed 32-bit; `INT_MAX = 2147483647`, `INT_MIN = -2147483647 - 1`), `add_ovf_u64`/`sub_ovf_u64`/`mul_ovf_u64` (для unsigned 64-bit). Все возвращают `1` при переполнении и `0` — иначе.

Подробности — в [STDLIBRARIES.ru.md](STDLIBRARIES.ru.md).

### `PortBusExtensions.GetBaseAddress`

`(ulong)sector * bus.PortsOnDevice` — вычисляет абсолютный адрес начала сектора в port-пространстве. `sector` сначала приводится к `ulong`, переполнения нет.

### `HostExtensions` и `sector`

`RunAndKeep`/`RunOnce`/`RunAndCleanup` принимают `sector` (по умолчанию `0`). Если `sector` занят другим устройством, `TryRegisterDevice` вернёт не-`Valid` `IdState`, и `RunOutcome` будет с `Success == false` и диагностикой `"register vm: {state}"` в `Diagnostics` — NRE больше нет (используется safe-API). Если передан `output != null`, UART регистрируется на `sector + 1`; при провале регистрации UART'а VM откатывается через `UnregisterDevice` и `RunOutcome` возвращается с `Success == false`, `Device == null`, диагностикой `"register uart: {state}"`. `RunOnce` и `RunAndCleanup` освобождают **только VM** (`UnregisterDevice(vmId)`), UART остаётся зарегистрированным — это потенциальная утечка, если `output != null` и вызывать `RunOnce`/`RunAndCleanup` многократно с тем же `sector`. См. [Что исправить](#что-исправить), п. 16.

### `RunOutcome.FaultInfo` и `Success`

`FaultInfo` — это `ProcessorFault` из `ctx.Run(new LaunchOptions(StartAddress: 0))`. `Success` в `RunAndKeep` — это `fault.Status == BiosStatus.EndProgramm`, а не «факт запуска» и не «успех компиляции». При провале компиляции/регистрации/загрузки `RunOutcome.Success == false`, `Device == null`, `FaultInfo == default(ProcessorFault)`, а причина — в `Diagnostics`. Важно: `_faultData` в `Processor` не сбрасывается в `LaunchProgramm` — второй `Run` на том же `Device` может вернуть fault от первого, если первый был падением.

### `BytecodeExtension`: усечённая программа и `fullyScanned`

`GetOpCodeSafety`/`GetUnsafeOpCodes` идут по `Program` до `actualCodeSize = CodeSize ?? Program.Length`, но **границы** проверяют по `Program.Length` (а не по `actualCodeSize`):
- `pos + 4 > Program.Length` → разбор останавливается (для `GetOpCodeSafety` — `Unknown`, для `GetUnsafeOpCodes` — `fullyScanned = false`);
- для опкодов с `HasNeed64IntData`: `nextPos + 8 > Program.Length` → то же самое.

Причина: `Program` — это `byte[]`, и усечение означает, что либо `Program` битый, либо `CodeSize` не соответствует `Program`. В первом случае `actualCodeSize > size` уже отсеивается как `Unknown`. Во втором — `CodeSize < Program.Length` (нормально: в хвосте `Program` может быть data-секция; `BytecodeExtension` её не сканирует, потому что `actualCodeSize` ограничивает цикл, но паддинг/усечение на границе всё равно проверяется по `Program.Length`).

`UnsafeOpCodeLocation.Offset` — смещение в `Program` (байтовый массив `CompilationResult.Program`), **не** в секции кода. Для `Program`, совпадающего с code-секцией (`CodeSize == Program.Length`), `Offset` совпадает с адресом относительно начала программы.

### Ограничения платформ

- `net10.0`, `<AllowUnsafeBlocks>` **не** включён.
- `Thread.Sleep(1)` в `QueuedIOStream.Flush` — в WASM либо no-op, либо `PlatformNotSupportedException`. Для WASM используйте `SynchronousIOStream`.
- `Channel<byte>` и `Task.Run(DrainAsync)` — работают в WASM, но фоновые задачи могут не выполниться до явного `await`.
- `ImmutableArray<T>` — работает в AOT/WASM, но каждая мутация через `CreateBuilder` аллоцирует. Для больших программ (10k+ инструкций) `DisassemblyResult` может занимать много памяти. Для стриминга — `StringDisassembler.Disassemble`.
- `Guid.NewGuid().GetHashCode()` в `InlineSmallVoidFunctionsRule` — стабильно в пределах процесса, но не между процессами. Для детерминированной сборки — проблема.
- `Debugger` — `SingleStep` в WASM работает (одна инструкция — синхронно), но `Continue`/`StepOver`/`StepOut`/`GetCallStack` — синхронные циклы, блокируют текущий поток. В browser-WASM (single-threaded) — блокировка UI. Для IDE в браузере — выносить в Web Worker или использовать `Task.Run` с `RequestStop`.

## Что исправить

6. **[API] `RunAndCleanup` и `RunOnce` не дают выбрать `deviceName` и не принимают `output`/`encoding`.**  
   Файл: `Host/HostExtensions.cs`. `RunAndKeep` принимает `deviceName`, `output`, `encoding`; `RunAndCleanup`/`RunOnce` — нет (всегда `"run"`, без UART). Это несимметрично и мешает при отладке и при прогоне с выводом.  
   Фикс: добавить те же параметры в `RunAndCleanup`/`RunOnce` и прокинуть их в `RunAndKeep`.

7. **[API/XML] `SaveFileInclude`/`SaveFileProject` — path traversal.**  
   Файл: `Host/ProjectExtension.cs`. `localPath` не проверяется на `..` и абсолютные пути.  
   Фикс: валидировать `localPath` (`Path.IsPathRooted` + отсутствие `..` в сегментах) или задокументировать, что вызов — trusted input.

8. **[XML] `ReplaceSafeMemAccessWithUnsafeRule.SizeRam` — дефолт занижен.**  
   Файл: `Optimizators/IRAsmRules/ReplaceSafeMemAccessWithUnsafeRule.cs`. `RamSize.B128` — не «безопасный» дефолт, а минимальный; правило молча no-op, если пользователь не выставит размер.  
   Фикс: либо дефолт на `RamSize` из `Device` (но правило не знает про `Device`), либо `<remarks>` с явным «you must set `SizeRam` to the device RAM size, otherwise the rule is a no-op».

9. **[API] `AddStdRules` для AST содержит `RemoveUnreachableCodeRule` дважды.**  
   Файл: `Optimizators/AstOptimizerExtension.cs`. Дублирование намеренное (после инлайна), но неочевидное.  
   Фикс: `<remarks>` на `AddStdRules` с пояснением, зачем второй проход, либо вынести в отдельный композитный метод `AddStdRulesWithCleanup()`.

10. **[API] `Debugger` — одноразовый на один `Load`.**  
    Файл: `Debugging/Debugger.cs`. Повторный `Load` бросит `InvalidOperationException`, чтобы не утекал step-режим (`ctx.RunStepMode` без `EndStepMode`).  
    Фикс: либо `Reset()` (снимает step-режим, очищает breakpoints, но сохраняет `Context`), либо явный `Unload()`/`Reload(program)`. Сейчас — новый экземпляр на каждый прогон.

11. **[API] `Debugger.ReadRam` не защищён от `IsRunning`.**  
    Файл: `Debugging/Debugger.cs`. В отличие от `DeviceContextExtensions.GetRamAsNewArray`/`CopyRamTo` (которые бросают `InvalidOperationException` при `ctx.IsRunning`), отладчик читает RAM **во время останова** — это его нормальный сценарий. Но если пользователь вызовет `ReadRam` из другого потока во время `Continue`, данные будут race-y.  
    Фикс: либо проверять `_isStepping` (флаг «сейчас идёт `SingleStep`»), либо задокументировать в `<remarks>` (сейчас — только в README).

12. **[API/XML] `Debugger.StepOut` — эвристика по `rSP`.**  
    Файл: `Debugging/Debugger.cs`. Семантика «`rSP` стал больше, чем был» ломается, если функция использует `PUSH`/`POP` для временных значений (типично для `print_minic`/`alloc_minic`). В таких случаях `StepOut` может выйти из функции раньше, чем ожидалось.  
    Фикс: либо документировать ограничение, либо добавить «глубину вызова» через `GetCallStack` (счётчик кадров) вместо `rSP` — но это дороже.

13. **[API] `Debugger.GetCallStack` — best-effort, ложные срабатывания.**  
    Файл: `Debugging/Debugger.cs`. VM не хранит frame pointer, стек восстанавливается сканированием `[rSP, RamSize)` на 8-байтные значения в диапазоне кода. Локальные `ulong`, случайно попавшие в диапазон, дадут ложные кадры.  
    Фикс: либо `/// <remarks>` с явным предупреждением (сейчас — в README), либо реализовать frame pointer в `Processor` (breaking change для байткода).

14. **[API] `alloc_minic.free` — обращение к `b` до его объявления.**  
    Файл: `StdLibraries/Libraries.cs`. В начале `free(ulong ptr)` стоит `if (blkFree(b)) return;`, но `b` объявляется ниже (`ulong b = ptr - 8;`). Это баг — обращение к необъявленной переменной.  
    Фикс: убрать лишнюю строку `if (blkFree(b)) return;` (двойное освобождение и так не поддерживается, а проверка `if (ptr == 0) return;` уже есть), либо перенести её **после** объявления `b`: `ulong b = ptr - 8; if (blkFree(b)) return;`.

15. **[API/семантика] `RemoveSwapMovPairRule` удаляет `MOV rA, rB; MOV rB, rA`, предполагая «нетто-эффект нулевой».**  
    Файл: `Optimizators/IRAsmRules/RemoveSwapMovPairRule.cs`. Для типичной семантики `MOV dst, src` (`dst = src`) этот паттерн **не** является no-op: после `MOV A, B` имеем `A = old B`, после `MOV B, A` — `B = old B`; итог `A = old B, B = old B`. Правило корректно только если `OpCode.MOV` в VM имеет семантику swap (крайне нетипично) или если автор правила опирался на иную конвенцию.  
    Фикс: (а) уточнить семантику `OpCode.MOV` в `docs/bytecode.md`; (б) либо убрать правило из `AddStdRules`, либо переписать его под swap (`XCHG`/`SWAP`), если такой опкод есть; (в) как минимум — добавить `<remarks>` с явным предупреждением.

16. **[API] `RunOnce`/`RunAndCleanup` не освобождают UART, зарегистрированный на `sector + 1`.**  
    Файл: `Host/HostExtensions.cs`. При `output != null` `RunAndKeep` регистрирует `QueuedIOStream` на `sector + 1`; `RunOnce`/`RunAndCleanup` освобождают только VM (`UnregisterDevice(vmId)`), но не UART. Повторный вызов с тем же `sector` и `output != null` упадёт на регистрации UART.  
    Фикс: `RunAndKeep` должен возвращать `UartDeviceId` в `RunOutcome` (или в отдельном out-параметре), а `RunOnce`/`RunAndCleanup` — освобождать и его. Как минимум — документировать, что при `output != null` `sector + 1` тоже занимается.