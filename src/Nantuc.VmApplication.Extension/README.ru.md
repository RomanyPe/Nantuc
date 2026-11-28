Вот перевод предоставленного файла `README.md` на русский язык с сохранением структуры, форматирования и технических терминов (имена классов, методов и типов данных оставлены на английском языке для соответствия коду).

---

# Nantuc.VmApplication.Extensions — README

Расширения для основного API: дизассемблер, отладчик, устройства ввода-вывода, асинхронное устройство, стандартные оптимизации (AST и IR), утилиты компиляции "в одну строку", встроенные библиотеки MiniC/NANASM.

Зависит от `[Nantuc.VmApplication]` (транзитивно от `Diagnostics`, `Kernel`, `Contracts`, `Common`; напрямую из `Common` использует `OpCode`, `AlignUp`, `InstructionDecoder`).

[← Корень](../README.md), [VmApplication](../../src/Nantuc.VmApplication/README.md), [Diagnostics](../../src/Nantuc.Diagnostics/README.md), [Kernel](../../src/Nantuc.Kernel/README.md), [Common](../../src/Nantuc.Kernel.Common/README.md), [Contracts](../../src/Nantuc.Kernel.Contracts/README.md), [Diagnostics](../../src/Nantuc.Kernel.Diagnostics/README.md), [STDLIBRARIES](STDLIBRARIES.md)

## Содержание
- [Об этом модуле](#об-этом-модуле)
- [Структура локального проекта](#структура-локального-проекта)
- [API](#api)
- [Примеры использования API](#примеры-использования-api)
- [Ориентированность модуля на пользователя](#ориентированность-модуля-на-пользователя)
- [Что было исправлено в этой версии](#что-было-исправлено-в-этой-версии)
- [Особые примечания](#особые-примечания)
- [Что необходимо исправить](#что-необходимо-исправить)

---

## Об этом модуле

`Nantuc.VmApplication.Extensions` — это "готовые решения" ("батарейки") для основного API. Здесь нет новых абстракций ядра, только расширения поверх существующих: `DeviceContext`, `AsyncDeviceContext`, `VmHost`, `VmHostProject`, `VmEmulator`, `IPortController`. Вы можете не подключать этот модуль — основной API работает и без него. Но с ним вы получаете:

- **Дизассемблер** (`Disassembler` / `StringDisassembler` / `InstructionFormatter`): разбирает `CompilationResult.Program` на `DecodedInstruction[]` + `DecodedDataChunk[]` и текстовое представление (как `objdump`).
- **Отладчик** (`Debugger`): пошаговое выполнение поверх `DeviceContext` — `Step` / `StepOver` / `StepOut` / `Continue` / `ContinueTo` / `RunToEnd`, точки останова, попытка восстановления стека вызовов (best-effort), чтение оперативной памяти (`ReadUInt16/32/64`, `ReadCString`), дизассемблирование текущей инструкции через `StringDisassembler`. Не использует `AsRawDevice` или `unsafe` — только публичный API `DeviceContext` / `ProcessorSnapshot`.
- **Устройства ввода-вывода** (`NumericIOStream`, `SynchronousIOStream`, `QueuedIOStream`): вывод байтов/UTF-8/UTF-16 из ВМ в `TextWriter`. Плюс `PortCharEncoding` и `OutputPortLayout` (макет 8-байтового окна).
- **Асинхронное устройство** (`AsyncDevice`, `AsyncDeviceContext`, `AsyncDeviceExtension`, `ReusableWakeSignal`): устройство ВМ с собственной оперативной памятью, процессором и блоком портов ввода-вывода; асинхронные `Run` / `DebugRun` / `Step` / `StepDebug`, поддержка HALT/пробуждения, режимы Step/Continuous, вспомогательные методы регистрации и утилиты для работы с памятью. Пространство имен — `Nantuc.VmApplication.Extensions.AsyncDeviceExtensions`.
- **Стандартные оптимизации**: правила AST (`FoldConstants`, `PropagateConstants`, `RemoveUnusedVariables`, `RemoveUnreachableCode`, `InlineSmallVoidFunctions`) и правила IR (`ConstantFolding`, `RemoveNoopMov`, `RemovePushPopPair`, `RemoveSwapMovPair`, `RemoveJumpToNextLabel`, `RemoveMultiEnd`).
- **Утилиты компиляции** (`ProjectExtension`, `VMHostRunExtensions`): `host.Project.CompileToIR("int main(){}", 0, true, SourceLanguage.MiniC)` и `host.RunAndKeep(...)`, `host.RunOnce(...)`, `host.RunAndCleanup(...)` — одна строка вместо конвейера — плюс их асинхронные аналоги `host.AsyncRunAndKeep(...)`, `host.AsyncRunOnce(...)`, `host.AsyncRunAndCleanup(...)`, построенные на базе `AsyncDeviceContext`. Все варианты могут регистрировать UART через `QueuedIOStream`, если передан параметр `output`.
- **Встроенные библиотеки** (`Libraries`): строковые константы `heap_nanasm`, `alloc_minic`, `io_nanasm`, `print_minic`, `imath_minic`, `bitops_minic`, `ovf_minic` для `#include`. Подробности см. в [STDLIBRARIES.md](STDLIBRARIES.md).
- **Утилиты для работы с оперативной памятью** (`DeviceContextExtensions`, плюс тот же набор для `AsyncDeviceContext`): `GetRamAsNewArray` / `GetAllRamAsNewArray`, `CopyRamTo` в `Span<byte>` / `byte[]` / `Memory<byte>` / `Stream`, `ReadRam<T>` / `CopyRamTo<T>` для `unmanaged` типов, `TryCopyRamTo` без исключений — чтение памяти **остановленной** ВМ (все, кроме `TryCopyRamTo`, выбрасывают `InvalidOperationException`, если `ctx.IsRunning`).
- **Утилиты для работы с байт-кодом** (`BytecodeExtension`): `GetOpCodeSafety` / `GetUnsafeOpCodes` — статический анализ `CompilationResult.Program` на наличие `*_UNSAFE` опкодов (`OpCodeSafety.Safe` / `HasUnsafe` / `Unknown`) и их локализация (`UnsafeOpCodeLocation(OpCode, Offset, Ip)`). Полезно для проверки ненадежного кода перед запуском и для верификации результата работы `ReplaceSafeMemAccessWithUnsafeRule`.

Ключевая идея заключается в том, чтобы не изменять контракт `Nantuc.VmApplication`, а расширять его. Все правила реализуют `IAstOptimizationRule` / `IOptimizerIRRule` из компилятора, все устройства ввода-вывода наследуют `PortControlBase` из `VmApplication`, отладчик работает через `DeviceContext.RunStepMode` / `StepDebug` / `GetRegistersSnapshot` / `NativeReadOnlyBuffer`, а `AsyncDevice` реализует `IPortController`, владея собственными `MemoryBus` / `Processor`. Никаких новых интерфейсов в `Kernel.Contracts`.

---

## Структура локального проекта

Пространства имен совпадают с папками:
```text
Nantuc.VmApplication.Extensions/
 ├── Nantuc.VmApplication.Extensions.csproj   # net10.0, ref -> VmApplication
 ├── AsyncDevice/
 │   ├── AsyncDevice.cs                        # AsyncDevice : IPortController — собственная RAM + Processor + блок портов, асинхронные Run/DebugRun/Step
 │   ├── AsyncDeviceContext.cs                 # AsyncDeviceContext — режимы Step/Continuous/None, запуск/шаг/RAM/порты
 │   ├── AsyncDeviceExtension.cs               # расширения для VmEmulator/ManagerDevices/AsyncDeviceContext + утилиты RAM
 │   └── ReusableWakeSignal.cs                 # сигнал пробуждения IValueTaskSource<bool> для HALT/sleep (внутренняя поддержка)
 ├── Bytecode/
 │   └── BytecodeExtension.cs                  # расширение для CompilationResult: GetOpCodeSafety / GetUnsafeOpCodes + OpCodeSafety + UnsafeOpCodeLocation + IsUnsafeOpcode
 ├── Diagnostics/
 │   └── DiagnosticsCompilerExtension.cs       # расширение для IReadOnlyDictionary<ErrorCode,string> (ValidateAllCodesCovered / TryValidateAllCodesCovered) — см. "Что необходимо исправить", пункт 3
 ├── Debugging/
 │   ├── Debugger.cs                           # пошаговый отладчик на базе DeviceContext: Step/StepOver/StepOut/Continue/ContinueTo/RunToEnd/точки останова/RAM/дизассемблер/стек
 │   ├── DebugStop.cs                          # readonly record struct: Reason + IP + StepCount + ProcessorSnapshot + ProcessorFault
 │   ├── DebugStopReason.cs                    # enum: None/StepComplete/Breakpoint/Completed/Faulted/Sleeping/StepLimit/UserStop/Error
 │   └── CallStackFrame.cs                     # readonly record struct: Index + ReturnAddress + StackAddress (попытка восстановления)
 ├── DeviceCtx/
 │   └── DeviceContextExtensions.cs            # GetRamAsNewArray, CopyRamTo
 ├── Disassembler/
 │   ├── DecodedDataChunk.cs                   # record struct (Address, Value, Bytes, DecodedString, ByteLength)
 │   ├── DecodedInstruction.cs                 # record struct (Address, RawInstruction, OpCode, Reg1, Reg2, DataSize, Data64?, ByteLength)
 │   ├── Disassembler.cs                       # public static: extension(res).Disassemble(int?) -> DisassemblyResult
 │   ├── DisassemblyResult.cs                  # sealed: Instructions, DataChunks, BaseAddress, CodeSize, TotalSize, BytesConsumed, HasDataSection
 │   ├── InstructionFormatter.cs               # static: Format(DisassemblyResult | DecodedInstruction); Format(StringBuilder, ...)
 │   └── StringDisassembler.cs                 # static: Disassemble(ReadOnlySpan<byte>, int, ulong) -> string; extension(res).DisassembleToString()
 ├── Emulator/IO/
 │   ├── NumericIOStream.cs                    # синхронный: записывает числовые коды символов
 │   ├── OutputPortLayout.cs                   # const: WindowSize=8, Data=0, Status=1, Flush=2
 │   ├── PortCharEncoding.cs                   # enum: BytePerChar | Utf8 | Utf16
 │   ├── QueuedIOStream.cs                     # асинхронный: Channel<byte> + фоновый сброс (drain)
 │   └── SynchronousIOStream.cs                # синхронный: записывает символы
 ├── Host/
 │   ├── HostExtensions.cs                     # RunOutcome + AsyncRunOutcome + RunAndKeep / RunAndCleanup / RunOnce + AsyncRunAndKeep / AsyncRunAndCleanup / AsyncRunOnce (+ UART через QueuedIOStream, если output != null)
 │   └── ProjectExtension.cs                   # Compile/CompileToIR из строки/строк + SaveFileInclude/SaveFileProject
 ├── StdLibraries/
 │   └── Libraries.cs                          # static class Libraries: heap_nanasm, alloc_minic, io_nanasm, print_minic, imath_minic, bitops_minic, ovf_minic
 └── Optimizations/
     ├── AstOptimizerExtension.cs              # extension(VmAstOptimizer).AddStdRules() + WithASTNodePrefix + константы OptimizationId
     ├── IROptimizerExtension.cs               # extension(VmIROptimizer).AddStdRules() + WithIRPrefix / WithRule + константы OptimizationId
     ├── IRLog.cs                              # IRLog : IRogOptimization — форматирование логов оптимизации
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

**Организационные заметки:**
- Папка и пространство имен — `StdLibraries`, файл и публичный класс — `Libraries`. Исторически папка называлась `Librares`, а в старых документах было `Librare` — обе опечатки исправлены. Пространство имен `Nantuc.VmApplication.Extensions.StdLibraries`, класс `Libraries`.
- `AstOptimizerExtension.cs` и `IROptimizerExtension.cs` содержат не только методы расширения, но и константы `OptimizationId`. Они являются частью публичного контракта: по этим ID вы ищете логи в `OptimizationResultLog`.
- `IRLog.cs` — отдельный файл (пространство имен `Nantuc.VmApplication.Extensions.Optimizations`), выделенный из `IROptimizerExtension.cs`.
- `Diagnostics/DiagnosticsCompilerExtension.cs` — расширение для `IReadOnlyDictionary<ErrorCode, string>` (`ValidateAllCodesCovered`/`TryValidateAllCodesCovered`). Согласно changelog 0.7.2 эти методы помечены как удаленные; файл все еще присутствует в коде — см. [Что необходимо исправить](#что-необходимо-исправить), пункт 3.
- `Debugging/` — новый слой поверх `DeviceContext`, не изменяет `Nantuc.VmApplication`. Отладчик не является потокобезопасным (кроме `RequestStop`), что соответствует модели `DeviceContext`. Стек вызовов восстанавливается эвристически — ВМ не хранит указатель кадра (frame pointer). Подробности в [Особые примечания](#особые-примечания).
- `AsyncDevice/` — пространство имен `Nantuc.VmApplication.Extensions.AsyncDeviceExtensions`. `AsyncDevice` реализует `IPortController`, владеет `MemoryBus` и `Processor`, и регистрируется как обычное устройство. `AsyncDeviceContext` зеркально отражает модель запуска/шага `DeviceContext` с явными режимами `Step` / `Continuous` / `None`. `AsyncDeviceExtension` добавляет вспомогательные методы регистрации и утилиты для RAM. `ReusableWakeSignal` является публичным только как внутренняя поддержка ожидания HALT/sleep — это не основной API.
- `Bytecode/` — статический анализ `CompilationResult.Program`, не затрагивает `DeviceContext` / `VmEmulator`. `IsUnsafeOpcode` использует тот же набор, что и `ReplaceSafeMemAccessWithUnsafeRule`, поэтому модуль можно использовать для проверки результата работы IR-правила (после применения все `LOAD` / `STORE` / `LOAD_IND` / `STORE_IND` заменяются на `*_UNSAFE`).
- `Host/HostExtensions.cs` — синхронные `RunAndKeep` / `RunOnce` / `RunAndCleanup` принимают `deviceName` / `sector` / `output` / `encoding` и используют только безопасный API (`TryRegisterDevice`, `TryGetDeviceContext`, `TryLoadProgram`), без `!` — за исключением одного безусловного `UnregisterDevice(uartId!.Value)` в конце `RunAndKeep`, что является ошибкой, когда `output == null` (см. [Что необходимо исправить](#что-необходимо-исправить), пункт 19). Асинхронные `AsyncRunAndKeep` / `AsyncRunOnce` / `AsyncRunAndCleanup` зеркально отражают синхронный API поверх `AsyncDeviceContext`, но в настоящее время вообще не отменяют регистрацию UART (см. пункт 20).

---

## API

### Дизассемблер
*(Описание структур `DecodedInstruction`, `DecodedDataChunk`, `DisassemblyResult`, `Disassembler`, `StringDisassembler`, `InstructionFormatter` оставлено без изменений в сигнатурах, переведены только пояснения)*
- `compilation.Disassemble()` — весь поток как код; `compilation.Disassemble(codeSize: compilation.CodeSize)` — с секцией данных.
- Формат: `XXXXXXXX: XXXXXXXX MNEMONIC [data = 0x...]` — как в `objdump`. Для секции данных: `XXXXXXXX: XX XX ... .data 0xXXXXXXXXXXXXXXXX "string"`.

### Отладчик
- `DebugStopReason` — `enum`: `None` (отладчик еще не останавливался), `StepComplete` (выполнена одна инструкция), `Breakpoint` (сработала точка останова), `Completed` (программа выполнила END), `Faulted` (BiosStatus != EndProgram && != Success), `Sleeping` (HALT), `StepLimit` (достигнут лимит шагов в Continue), `UserStop` (RequestStop()), `Error` (внутренняя ошибка отладчика).
- `DebugStop` — `readonly record struct`: содержит `Reason`, `InstructionPointer`, `StepCount`, `Snapshot`, `Fault`. Свойство `IsTerminal` возвращает `true`, если причина `Completed`, `Faulted` или `Error`.
- `CallStackFrame` — `readonly record struct`: `Index`, `ReturnAddress`, `StackAddress`.
- `Debugger` — `sealed class : IDisposable`.
  - **Поведение**:
    - `Load` — вызывает `ctx.LoadProgram` + `ctx.RunStepMode(startAddress)`; после вызова `IsActive == true`, но ни одна инструкция еще не выполнена. `Debugger` одноразовый для одного `Load` — повторный вызов выбрасывает `InvalidOperationException` (защита от утечки режима шага; для нового запуска нужен новый `Debugger`).
    - `Step` — одна инструкция через `ctx.StepDebug(onStep)`; результат — `DebugStop` со снимком процессора из колбэка.
    - `StepOver` — если текущая инструкция `CALL` / `CALL_IND`, выполняется до адреса возврата (`ip + 4` для `CALL_IND`, `AlignUp(ip + 4, 8) + 8` для `CALL`); иначе ведет себя как `Step`.
    - `StepOut` — выполняет `Step`, пока `rSP` не станет больше значения на момент вызова. Best-effort: если функция использует `PUSH`/`POP` для временных значений, семантика нарушается (см. [Что необходимо исправить](#что-необходимо-исправить), пункт 11).
    - `Continue` — выполняет `StepDebug` до `Breakpoint` / `Completed` / `Faulted` / `Sleeping` / `StepLimit` / `UserStop`. В начале проверяет `_skipBreakpointOnce`, чтобы `Continue` -> точка останова -> `Continue` не зацикливался.
    - `ContinueTo(addr)` — временно добавляет точку останова по адресу `addr`, вызывает `Continue`, удаляет точку. Если по `addr` уже была пользовательская точка, она сохраняется.
    - `RunToEnd` — `Continue` с временно очищенным набором точек останова (чтобы "дойти до конца, игнорируя точки").
    - `RequestStop` — единственный метод, безопасный для вызова из другого потока: `volatile bool _stopRequested`. Циклы `Continue`/`StepOver`/`StepOut` проверяют его на каждой итерации.
    - `Wake` — `ctx.Resume()`; помещает `OpCode.WAKE` в очередь. Нужно, если отладчик остановился на `HALT`, и вы хотите продолжить без `Stop`/повторного `Load`.
    - `ReadRam` / `CopyRamTo` — через `ctx.NativeReadOnlyBuffer`; выбрасывают `ArgumentOutOfRangeException` при выходе за границы и `ObjectDisposedException` после `Dispose`. В отличие от `DeviceContextExtensions.GetRamAsNewArray`, они **не** выбрасывают `InvalidOperationException`, когда `IsRunning` — отладчик обычно читает память именно во время остановки.
    - `ReadUInt16/32/64` — little-endian через `BinaryPrimitives` (соответствует контракту `MemoryBus`).
    - `ReadCString` — читает байты до `0`; `maxLength` (по умолчанию 1024) защищает от чтения всей RAM, если строка не завершена.
    - `CurrentInstruction` / `DisassembleAt` / `DisassembleProgram` — используют `StringDisassembler.Disassemble`; формат как в `objdump`. `DisassembleProgram` дизассемблирует диапазон `[startAddress, loadAddress + program.Length)`, зафиксированный в `Load`.
    - `GetCallStack` — best-effort: сканирует стек (`[rSP, RamSize)`) на наличие 8-байтовых значений в диапазоне `[ProgramStart, ProgramEnd)`, выровненных по 4. Возможны ложные срабатывания (значения локальных переменных). `maxDepth` ограничивает глубину (по умолчанию 16).
    - `Stopped` — событие, срабатывающее после каждого `DebugStop` (включая финальный). Удобно для логирования/визуализации.

### Асинхронное устройство
Пространство имен: `Nantuc.VmApplication.Extensions.AsyncDeviceExtensions`.
- `AsyncDevice` — `sealed class : IPortController`:
  - **Поведение**:
    - Собственная RAM/CPU/порты — `AsyncDevice` создает собственный `MemoryBus` (`RamSize`) и `Processor` на общем `PortBus`; `MaxRamSize` — длина RAM, `NativeReadOnlyBuffer` — представление RAM только для чтения. Реализует `IPortController`, поэтому регистрируется как обычное устройство.
    - Не является потокобезопасным — `Run`, `DebugRun`, `Step`, `BatchStep`, `StepDebug`, `BatchStepDebug`, `LoadProgram` и `BreakPointerLaunchDevice` не должны вызываться конкурентно. `Stop`, `WakeProcessor` и `EnqueueExternalCommand` — безопасные межпоточные методы.
    - `LoadProgram` — выравнивает `loadAddress` до 8, проверяет `loadAddress + program.Length <= RAM`, загружает через `MemoryBus.LoadProgram`, затем инициализирует указатель кучи через `InitRegHP(AlignUp(loadAddress + length, 8))`. Выбрасывает `InvalidOperationException`, если программа не помещается. `TryLoadProgram` возвращает `false` в этом случае (и если устройство уничтожено).
    - `Run` — блокирующий асинхронный цикл через `await Step()`. С `limit` каждая итерация проверяет `StepCount >= limit` и ставит в очередь `BiosStatus.InfinityLoop`; отмена ставит в очередь `BiosStatus.HostStopped` и выполняет один дополнительный `Step()`, чтобы процессор мог наблюдать статус остановки. Возвращает `ProcessorFault` из `GetDiagnosticData()`.
    - `DebugRun` — тот же цикл с необязательной `delay` (мс) между шагами и колбэком `onStep`. Текущая реализация вызывает `onStep` перед каждым `Step()` (снимок до шага), а также перед проверкой лимита шагов.
    - `Step` — выполняет один шаг процессора. Если процессор спит (`HALT`), ожидает `ReusableWakeSignal.WaitAsync(_processorPolicy.SleepWaitTimeout)`; иначе, если все еще работает, инкрементирует `StepCount`. Каждые `0xFFFF` шагов происходит yield через `Task.Yield()`.
    - `BreakPointerLaunchDevice` — сбрасывает процессор и запускает его по адресу `start ?? 0` (выровненному до 8) без запуска цикла; полезно для ручного `Step`/`StepDebug`. XML-комментарий упоминает "конец RAM" для `null`, но текущий код использует `0`.
    - `Stop` — ставит в очередь `BiosStatus.HostStopped`, устанавливает сигнал пробуждения и отменяет текущий CTS `Run`/`DebugRun`, если он есть. Безопасно из другого потока.
    - Порты — `ReadPort` возвращает `0` для недопустимого смещения; `WritePort` игнорирует недопустимое смещение.
    - `WakeProcessor` / `EnqueueExternalCommand` — ставят в очередь внешнюю команду (`WAKE` или произвольную сырую инструкцию) и устанавливают сигнал пробуждения.
    - `Dispose` — идемпотентен: `Stop()`, уничтожение RAM и сигнала пробуждения.
- `AsyncDeviceContext` — `public class`:
  - **Поведение**:
    - Режимы — `AsyncDeviceContext` управляет одним устройством с внутренним режимом `Step` / `Continuous` / `None`. `Run` / `DebugRun` устанавливают `Continuous` и сбрасывают в `None` в `finally`; `RunStepMode` устанавливает `Step`; `Stop` / `EndStepMode` сбрасывают в `None`. Конфликтующий режим выбрасывает `InvalidOperationException`.
    - `Run` / `DebugRun` — используют `LaunchOptions.Default` / `LaunchOptionsDebug.Default`, если `opt == null`; выбрасывают исключение, если устройство уже работает или установлен другой режим.
    - `RunStepMode` — инициализирует устройство для ручного пошагового выполнения через `BreakPointerLaunchDevice`; не выполняет инструкцию.
    - `StepDebug` / `BatchStepDebug` — требуют режима `Step`; делегируют вызов `AsyncDevice.StepDebug` / `BatchStepDebug`. Как и в `AsyncDevice`, колбэк вызывается перед шагом в текущей реализации.
    - `Stop` — вызывает `_device.Stop()`; если контекст в режиме `Step`, дополнительно вызывает `await _device.StepDebug(null)`, чтобы процессор мог наблюдать статус остановки, затем сбрасывает режим в `None`.
    - `LoadProgram` / `ResetMemoryRam` — выбрасывают `InvalidOperationException`, если устройство работает. `TryLoadProgram` возвращает `false` со строкой `error`, если устройство работает или выход за границы.
    - `Pause` / `Resume` — ставят в очередь `OpCode.HALT` / `OpCode.WAKE` через `EnqueueExternalCommand`.
    - Не является потокобезопасным — режим контекста не защищен блокировкой; используйте его из одного координирующего потока, как и в случае с `DeviceContext`.
- `AsyncDeviceExtension` — `public static class`:
  - **Поведение**:
    - `RegisterAsyncDevice` — создает `AsyncDevice` из `spec.RamSize` / `spec.ProcessorFaultPolicy` и регистрирует его через `VmEmulator.RegisterDevice` (вариант с выбрасыванием исключения). `TryRegisterAsyncDevice` возвращает `IdState` и `id`.
    - `TryGetAsyncDeviceContext` / `GetAsyncDeviceContext` — оборачивают существующее `AsyncDevice`, зарегистрированное в эмуляторе, в новый `AsyncDeviceContext`.
    - Утилиты RAM — зеркально отражают `DeviceContextExtensions`: все методы, кроме `TryCopyRamTo`, выбрасывают `InvalidOperationException`, когда `ctx.IsRunning`; `TryCopyRamTo` возвращает `false` в этом случае. Границы проверяются с помощью `ArgumentOutOfRangeException`; копирование идет байт за байтом через `NativeReadOnlyBuffer`; типизированное чтение использует `MemoryMarshal` и не требует `unsafe`.
- `ReusableWakeSignal` — `public sealed class : IValueTaskSource<bool>, IDisposable`:
  - **Поведение**:
    - `Set()` пока никто не ожидает, фиксирует сигнал; следующий `WaitAsync` немедленно возвращает `true` и потребляет фиксацию.
    - `WaitAsync(timeout)` возвращает `false` по таймауту.
    - Повторный вход в `WaitAsync` не поддерживается: если ожидание уже выполняется, другой `WaitAsync` немедленно возвращает `true`.
    - Продолжения выполняются асинхронно (`RunContinuationsAsynchronously = true`).
    - Используется `AsyncDevice.Step()`, когда процессор переходит в состояние `HALT`/сна.

### Устройства ввода-вывода
- `PortCharEncoding`: `enum { BytePerChar, Utf8, Utf16 }`.
- `OutputPortLayout` — макет 8-байтового окна: `WindowSize = 8`, `Data = 0` (запись: байт для вывода; чтение: 0), `Status = 1` (0 — готов, 1 — есть необработанные данные), `Flush = 2` (запись 1 — сбросить буфер).
- `SynchronousIOStream : PortControlBase`: `WritePort(Data, value)` -> `_writer.Write(...)` + `Flush()`. `WritePort(Flush, _)` -> `_writer.Flush()`. `ReadPort(_)` -> 0. `WakeProcessor()` -> `_writer.Flush()`.
- `NumericIOStream : PortControlBase` — синхронный, записывает числовые коды символов. Формат: `"42 "` или `"0x2A "`.
- `QueuedIOStream : PortControlBase` — асинхронный через `Channel<byte>`. `WritePort(Data, value)` -> `Channel.Writer.TryWrite(value)` + `Interlocked.Increment`. `ReadPort(Status)` -> `1`, если есть ожидающие данные, иначе `0`. `WritePort(Flush, _)` — опрос до 2 сек. `Dispose` -> `Complete` + `_drainTask.Wait(5s)`.

### Оптимизации
- `AstOptimizerExtension`: `AddStdRules()` добавляет стандартные правила AST:
  - `PropagateConstantsRule` — `IdentifierNode` -> `NumberNode`, если переменная объявлена как константа и не переназначается.
  - `FoldConstantsRule` — `Number op Number` -> `Number`, включая унарные `-` / `!` / `~` и рекурсивно по всем выражениям.
  - `RemoveUnusedVariablesRule` — удаляет `VariableNode`, которые никогда не читаются и не назначаются, только если инициализатор не имеет побочных эффектов.
  - `RemoveUnreachableCodeRule` — удаляет код после `return`; `if(false)` заменяется содержимым `else` (или удаляется); `if(true)` заменяется содержимым `then`; `while(false)` и `for(...; false; ...)` удаляются. Рекурсивно по вложенным блокам.
  - `InlineSmallVoidFunctionsRule` — встраивает `void` функции с `CountStatements(body) ≤ 10` (с учетом вложенных), без прямой саморекурсии. Тела `main` не встраиваются.
  - `RemoveUnreachableCodeRule` — еще раз (после встраивания). См. [Что необходимо исправить](#что-необходимо-исправить), пункт 8.
- `IROptimizerExtension`: `AddStdRules()` добавляет стандартные правила IR:
  - `RemoveNoopMovRule` — `MOV rX, rX` (не сразу после метки).
  - `RemovePushPopPairRule` — `PUSH rX; POP rX` (ни `PUSH`, ни `POP` не сразу после метки).
  - `RemoveSwapMovPairRule` — `MOV rA, rB; MOV rB, rA` (не сразу после метки). См. примечание в [Что необходимо исправить](#что-необходимо-исправить), пункт 14.
  - `RemoveJumpToNextLabelRule` — `JMP label; label:` -> удалить `JMP`.
  - `ConstantFoldingRule` — `LDI rA, X; LDI rB, Y; OP rA, rB` -> `LDI opInstr.FirstReg, X op Y` (без меток между инструкциями). Поддерживаемые опкоды: `ADD`, `SUB`, `MULT_INT`, `DIV`, `AND`, `OR`, `XOR`, `SHR`. При `DIV` и `right == 0` правило является no-op.
  - `RemoveMultiEndPairRule` — `END; END` -> один `END` (цепочки >2 `END` сворачиваются последовательно).
  - `ReplaceSafeMemAccessWithUnsafeRule` — не является частью `AddStdRules`, добавляется вручную. Заменяет `LOAD`/`STORE`/`LOAD_IND`/`STORE_IND` на `*_UNSAFE` варианты, если адрес известен, `address + dataSize ≤ SizeRam.Bytes` и `address % dataSize == 0`. Значение по умолчанию `RamSize.B128` занижено — правило срабатывает только для самых маленьких адресов, если `SizeRam` не установлен в фактический размер RAM устройства.
- `IRLog`: класс для форматирования логов оптимизации.

### Утилиты компиляции и запуска
- `ProjectExtension`: методы `CompileToIR`, `Compile`, `SaveFileInclude`, `SaveFileProject`. `localPath` не проверяется на `..` — см. [Что необходимо исправить](#что-необходимо-исправить).
- `VMHostRunExtensions`: предоставляет записи `RunOutcome` и `AsyncRunOutcome`, а также методы расширения для `VmHost`:
  - Синхронные: `RunAndKeep`, `RunAndCleanup`, `RunOnce`.
  - Асинхронные: `AsyncRunAndKeep`, `AsyncRunAndCleanup`, `AsyncRunOnce`.
  - **Синхронное поведение**: `RunAndKeep` компилирует, регистрирует ВМ, загружает и запускает. Если передан `output != null`, дополнительно регистрирует `QueuedIOStream` как устройство. **Важная текущая ошибка**: в конце `RunAndKeep` безусловно вызывает `host.Emulator.UnregisterDevice(uartId!.Value)` — когда `output == null`, `uartId` равен `null`, и это выбрасывает `InvalidOperationException`. См. [Что необходимо исправить](#что-необходимо-исправить), пункт 19.
  - **Асинхронное поведение**: `AsyncRunAndKeep` создает ВМ через `TryRegisterAsyncDevice` и запускает через `await ctx.Run(...)`. **Несоответствие**: в отличие от синхронного `RunAndKeep`, он **не** отменяет регистрацию UART перед возвратом — UART остается зарегистрированным при успехе и при ранних возвратах после регистрации UART. См. пункт 20.

### Встроенные библиотеки
- `Libraries` — `static class` со строковыми константами (пространство имен `Nantuc.VmApplication.Extensions.StdLibraries`):
  - `heap_nanasm` — функции работы с кучей.
  - `alloc_minic` — аллокатор first-fit с разделением и объединением.
  - `io_nanasm` — `func_In`, `func_Out`.
  - `print_minic` — функции печати через `io_nanasm`.
  - `imath_minic` — математические функции (`imin`, `imax`, `igcd`, `ipow`, `uisqrt` и т.д.).
  - `bitops_minic` — битовые операции (`upopcount`, `uclz`, `rotl64`, `unext_pow2` и т.д.).
  - `ovf_minic` — проверки переполнения для 32-битных знаковых и 64-битных беззнаковых операций.
  - **Важно**: ошибка `FunctionGenerator.GenerateFunction` (асимметричные push/pop сохраняемых регистров, которая ломала `alloc_minic` с 3-4 локальными переменными) исправлена. Поэтому обходной путь "хранить состояние в глобальных переменных" больше не нужен: в следующей итерации `alloc_minic` можно (и нужно) переписать с использованием локальных переменных. Текущий код все еще хранит состояние в глобальных переменных как наследие.

### Утилиты для работы с оперативной памятью (RAM)
- `DeviceContextExtensions` — расширение для `DeviceContext`. Все методы читают RAM через `NativeReadOnlyBuffer`; ни один не использует `unsafe`/`AsRawDevice`.
  - Все методы, кроме `TryCopyRamTo`, выбрасывают `InvalidOperationException`, если `ctx.IsRunning`. `TryCopyRamTo` возвращает `false` в этом случае.
  - Границы проверяются через `ArgumentOutOfRangeException`.
  - `CopyRamTo(Stream, ...)` читает RAM чанками по `bufferSize` и записывает в поток.
  - `ReadRam<T>` / `CopyRamTo<T>` используют `MemoryMarshal` и не требуют `<AllowUnsafeBlocks>`.
  - Тот же набор утилит для RAM доступен для `AsyncDeviceContext` через `AsyncDeviceExtension`.

### Утилиты для работы с байт-кодом
- `BytecodeExtension` — статический анализ `CompilationResult.Program`, не затрагивает `DeviceContext`/`VmEmulator`.
  - `GetOpCodeSafety()`: возвращает `Unknown` (если программа не скомпилирована или обрезана), `HasUnsafe` (если найден хотя бы один `*_UNSAFE` опкод) или `Safe` (если программа просканирована до `actualCodeSize` и небезопасных опкодов нет).
  - `GetUnsafeOpCodes()` — возвращает список всех `*_UNSAFE` опкодов с их `OpCode`, `Offset` (в байтовом массиве `Program`) и `Ip` (виртуальный адрес).
  - `IsUnsafeOpcode` — `true` для `LOAD_UNSAFE`, `STORE_UNSAFE`, `STORE_IND_UNSAFE`, `LOAD_IND_UNSAFE`.

### DiagnosticsCompilerExtension
- Расширение для `IReadOnlyDictionary<ErrorCode, string>`: `ValidateAllCodesCovered()` и `TryValidateAllCodesCovered()`. Используется для проверки пользовательских провайдеров сообщений. Согласно changelog 0.7.2 эти методы помечены как удаленные, но файл все еще присутствует в коде.

---

## Примеры использования API

*(Код оставлен без изменений, переведены только комментарии и пояснительный текст)*

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
// ... инициализация host и comp ...
using var dbg = new Debugger(vm);
dbg.Load(comp.Program!, loadAddress: 0, startAddress: 0);
dbg.AddBreakpoint(0x00);           // остановка на входе в main
dbg.Stopped += s => Console.WriteLine($"STOP: {s}");
var stop = dbg.StepOver();
Console.WriteLine($"after step-over: {stop}");
Console.WriteLine($"  at: {dbg.CurrentInstruction}");
stop = dbg.Continue(stepLimit: 100_000);
if (stop.Reason == DebugStopReason.Breakpoint)
    Console.WriteLine($"  hit bp at 0x{stop.InstructionPointer:X}");
var final = dbg.RunToEnd();
Console.WriteLine($"final: {final.Reason} after {final.StepCount} steps");
// ... чтение регистров и памяти ...
```

**Прерывание долгого выполнения из другого потока:**
```csharp
using var dbg = new Debugger(vm);
dbg.Load(program, 0, 0);
var runner = Task.Run(() => dbg.Continue());     // может выполняться долго
Thread.Sleep(200);
dbg.RequestStop();                                // volatile bool, безопасно
var stop = runner.Result;                         // Reason == UserStop
```
`RequestStop` — единственный потокобезопасный метод `Debugger`; остальные (`Step`/`Continue`/`Load`) должны вызываться из одного координирующего потока.

**Остановка HALT -> продолжение:**
```csharp
var stop = dbg.Continue();
if (stop.Reason == DebugStopReason.Sleeping)
{
    dbg.Wake();                                    // Resume() -> OpCode.WAKE в очереди
    stop = dbg.Continue();
}
```

**Продолжение до конкретной функции (по ее адресу):**
```csharp
dbg.ContinueTo(address: 0x200);                    // временная точка останова на 0x200
// после этого 0x200 больше нет в dbg.Breakpoints
```

**Запуск в одну строку с диагностикой (синхронно):**
```csharp
using var host = VmHostFactory.CreateDefault();
bool ok = host.RunAndCleanup("int main() { return 0; }", SourceLanguage.MiniC);
var outcome = host.RunAndKeep("""
    int main() {
        int x = 42;
        return x;
    }
    """, SourceLanguage.MiniC);
if (outcome.Success)
{
    var regs = outcome.Device!.GetRegistersSnapshot();
    Console.WriteLine($"r0={regs[RegType.r0]}, steps={outcome.StepCount}");
    if (outcome.FaultInfo.Status != BiosStatus.EndProgram)
        Console.Error.WriteLine($"VM fault: {outcome.FaultInfo.Status} " +
                                $"at IP=0x{outcome.FaultInfo.InstructionPointer:X}");
}
```

**Запуск с выводом в консоль через UART (синхронно):**
```csharp
var outcome = host.RunAndKeep(source, SourceLanguage.MiniC, output: Console.Out);
// UART регистрируется на sector + 1, кодировка по умолчанию — Utf16.
// Для Utf8 — передайте encoding: PortCharEncoding.Utf8.
```

**Асинхронный запуск в одну строку с диагностикой:**
```csharp
using var host = VmHostFactory.CreateDefault();
bool ok = await host.AsyncRunAndCleanup(
    "int main() { return 0; }", SourceLanguage.MiniC);
var outcome = await host.AsyncRunAndKeep("""
    int main() {
        int x = 42;
        return x;
    }
    """, SourceLanguage.MiniC);
// ... проверка outcome ...
// Долгий запуск с отменой:
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
var fault = await host.AsyncRunAndKeep(source, SourceLanguage.MiniC, token: cts.Token);
```

**Асинхронное устройство: регистрация, загрузка, прямой запуск (низкий уровень):**
```csharp
// ... инициализация host и comp ...
var state = host.Emulator.TryRegisterAsyncDevice(
    DeviceSpec.Create(ramSize: RamSize.MB16, name: "async"), sector: 0, out int id);
if (state != IdState.Valid) return;
if (!host.Emulator.TryGetAsyncDeviceContext(id, out var actx)) return;
actx.LoadProgram(comp.Program!, loadAddress: 0);
var fault = await actx.Run(new LaunchOptions(StartAddress: 0));
Console.WriteLine($"status={fault.Status}, ip=0x{fault.InstructionPointer:X}, steps={actx.StepCount}");
```

**Асинхронное устройство: ручной пошаговый режим:**
```csharp
actx.LoadProgram(comp.Program!, 0);
actx.RunStepMode(0);                              // подготовка режима Step, инструкция еще не выполнена
await actx.StepDebug(snapshot =>
{
    // Текущая реализация: колбэк вызывается ДО шага.
    Console.WriteLine($"before step: ip=0x{snapshot.Registers[RegType.rIP]:X}");
});
await actx.BatchStepDebug(count: 10);
await actx.EndStepMode();
```

**Асинхронное устройство: HALT / Resume:**
```csharp
actx.Pause();                                     // поставить в очередь OpCode.HALT
// … устройство спит …
actx.Resume();                                    // поставить в очередь OpCode.WAKE
```

**Асинхронное устройство: снимок RAM:**
```csharp
if (!actx.IsRunning)
{
    byte[] ram = actx.GetRamAsNewArray(0, 256);
    ulong value = actx.ReadRam<ulong>(0x100);
    Console.WriteLine($"value={value}");
}
```

**Устройство вывода ввода-вывода (UTF-8):**
```csharp
using var host = VmHostFactory.CreateDefault();
var uart = new SynchronousIOStream(Console.Out, PortCharEncoding.Utf8);
host.Emulator.RegisterDevice(uart, name: "UART0", sector: 1);
```

**Очереди ввода-вывода — не блокирует ЦП:**
```csharp
var writer = new StreamWriter("output.txt");
var uart = new QueuedIOStream(writer, PortCharEncoding.Utf8);
host.Emulator.RegisterDevice(uart, name: "UART0", sector: 1);
```

**Стандартные оптимизации AST:**
```csharp
host.Project.Optimizator.VMAstOptimizer.AddStdRules();
var comp = host.Project.CompileToIR(sources, 0, optimize: true).Build();
if (comp.OptimizationResultLog is { } logs)
    foreach (var (id, log) in logs.Logs)
        foreach (var e in log.GetLogs())
            Console.WriteLine($"[{id}] pass {e.Pass}: {e.Message}");
```

**Стандартные оптимизации IR:**
```csharp
host.Project.Optimizator.VMIROptimizer.AddStdRules();
var il = host.Project.CompileToIR(sources, 0, optimize: false);
il = host.Project.OptimizationIR(il, IROptimizationCount: 5);
var comp = il.Build();
```

**Сохранение исходников в проект:**
```csharp
using Nantuc.VmApplication.Extensions.StdLibraries;
host.Project.SaveFileProject("main.c", "int main() { return 0; }");
host.Project.SaveFileInclude("heap.nanasm", Libraries.heap_nanasm);
```

**Чтение RAM после запуска (синхронно):**
```csharp
var outcome = host.RunAndKeep("int main() { int x = 42; return x; }", SourceLanguage.MiniC);
if (outcome.Success)
{
    var ram = outcome.Device!.GetRamAsNewArray(0, 256);
}
```

**Чтение RAM после запуска (асинхронно):**
```csharp
var outcome = await host.AsyncRunAndKeep("int main() { int x = 42; return x; }", SourceLanguage.MiniC);
if (outcome.Success)
{
    var ram = outcome.Device!.GetRamAsNewArray(0, 256);
}
```

**Проверка ненадежного кода перед запуском:**
```csharp
var comp = host.Project.Compile("int main() { return 0; }", 0, optimize: false);
if (!comp.Success) { /* … */ }
switch (comp.GetOpCodeSafety())
{
    case OpCodeSafety.Safe:
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
`Safe` означает "нет `*_UNSAFE` опкодов", а не "безопасно". Обычные `LOAD` / `STORE` также могут обращаться к произвольным адресам. `HasUnsafe`, напротив, означает, что код точно содержит небезопасные обращения.

**Проверка результата работы `ReplaceSafeMemAccessWithUnsafeRule`:**
```csharp
host.Project.Optimizator.VMIROptimizer.AddStdRules();
// … сборка IR с оптимизациями …
var comp = il.Build();
if (comp.GetOpCodeSafety() != OpCodeSafety.Safe)
    foreach (var loc in comp.GetUnsafeOpCodes())
        Console.Error.WriteLine($"missed at 0x{loc.Ip:X}: {loc.OpCode}");
```

---

## Ориентированность модуля на пользователя

Средне-высокая. Это необязательный слой поверх основного API.
- Если вы просто хотите "запустить и посмотреть" — подключите `Nantuc.VmApplication.Extensions` и используйте `RunAndKeep` / `RunOnce` / `AsyncRunAndKeep` / `AddStdRules`.
- Если вы пишете IDE или отладчик — используйте `Debugger` / `Disassembler` / `StringDisassembler` / `InstructionFormatter`.
- Если вы пишете игру — `SynchronousIOStream` / `QueuedIOStream` для UART.
- Если вам нужно устройство с собственной RAM/CPU или асинхронное управление запуском/шагами — используйте `AsyncDeviceContext` или однострочники `AsyncRun*`.

**Что вам следует делать:**
- Подключайте `AddStdRules()` в сценариях типа `VmHostFactory.CreateDefault` для компиляции с оптимизациями.
- Используйте `QueuedIOStream` для неблокирующего вывода (игровой цикл), `SynchronousIOStream` — для простых случаев.
- Для отладки — `DisassembleToString()` (текст) или `Disassemble()` (структура).
- Для экономии RAM — `GetRamAsNewArray` (снимок) и `CopyRamTo` (в подготовленный `Span<byte>`).
- Устанавливайте `ReplaceSafeMemAccessWithUnsafeRule.SizeRam` в фактический размер RAM устройства.
- Используйте `SaveFileProject`/`SaveFileInclude` с проверкой, что `localPath` не содержит `..`.
- Для собственных правил оптимизации — назначайте `OptimizationId` через `WithIRPrefix()` / `WithRule()` / `WithASTNodePrefix()`, чтобы префикс совпадал со встроенными.
- Для отладки — `Debugger` поверх `DeviceContext`: `dbg.Load(program)` -> `dbg.Step()` / `StepOver()` / `StepOut()` / `Continue()`, `dbg.AddBreakpoint(addr)`, `dbg.CurrentInstruction`, `dbg.GetCallStack()`. Чтобы остановить долгий запуск — `dbg.RequestStop()` из другого потока.
- Для отладки в IDE — подпишитесь на `dbg.Stopped`, чтобы обновлять панель регистров/стека/RAM при каждой остановке.
- Для "запуска до конца" (например, в CI) — используйте `dbg.RunToEnd(stepLimit: N)`, а не `Continue` (последний застрянет на первой точке останова).
- Для типичных целочисленных операций и проверок переполнения — используйте `imath_minic`, `bitops_minic`, `ovf_minic` вместо рукописных реализаций.
- Для ненадежного кода — проверяйте `comp.GetOpCodeSafety()` и/или перечисляйте `comp.GetUnsafeOpCodes()` перед загрузкой ВМ; это дешевая статическая проверка (`*_UNSAFE` опкоды), но не замена песочнице.
- Для вывода из ВМ в консоль/файл без ручной регистрации UART — `host.RunAndKeep(source, lang, output: writer)` (UART будет зарегистрирован на `sector + 1`, кодировка по умолчанию — `Utf16`).
- Для устройства с собственной RAM/процессором — `host.Emulator.TryRegisterAsyncDevice(...)` / `TryGetAsyncDeviceContext(...)`, затем `AsyncDeviceContext.Run` / `DebugRun` / `RunStepMode`; или однострочники `host.AsyncRunAndKeep` / `host.AsyncRunOnce` / `host.AsyncRunAndCleanup`.
- Для асинхронного ручного пошагового выполнения — `actx.RunStepMode(start)`, затем `await actx.StepDebug(...)` / `BatchStepDebug(...)`, и `await actx.EndStepMode()` по завершении.
- Для управления HALT/sleep на асинхронном устройстве — `actx.Pause()` / `actx.Resume()` или `device.WakeProcessor()`.
- Для чтения RAM остановленного асинхронного устройства — те же расширения `GetRamAsNewArray` / `CopyRamTo` / `ReadRam<T>` для `AsyncDeviceContext`.

**Чего вам не следует делать:**
- Вызывать `RunAndKeep` несколько раз на одном `VmHost` с одним и тем же `sector` — сектор будет занят. Используйте `sector:` явно или `RunOnce`.
- Вызывать `RunAndKeep`/`AsyncRunAndKeep` несколько раз на одном `VmHost` с `output != null` без `UnregisterDevice` — сектор `sector + 1` также будет занят UART.
- Вызывать синхронные `RunAndKeep` / `RunOnce` / `RunAndCleanup` с `output: null` в текущей сборке — это выбрасывает `InvalidOperationException` в конце (`uartId!.Value`). См. [Что необходимо исправить](#что-необходимо-исправить), пункт 19. В качестве обходного пути передавайте `TextWriter`, пока ошибка не будет исправлена.
- Многократно вызывать асинхронные `AsyncRunAndCleanup`/`AsyncRunOnce` с `output != null` на одном `sector` — UART не освобождается, и `sector + 1` будет "утекать". См. пункт 20.
- Использовать `RunAndKeep` для ненадежного кода — он регистрирует ВМ на указанном секторе без песочницы.
- Полагаться на `AddStdRules` для IR — `ReplaceSafeMemAccessWithUnsafeRule` не добавляется автоматически.
- Ожидать, что `RunOutcome.Success` / `AsyncRunOutcome.Success` отражают только факт запуска — `Success == (fault.Status == BiosStatus.EndProgram)`. Ошибки времени выполнения видны как в `Success`, так и в `FaultInfo`.
- Ожидать, что UART, зарегистрированный через `RunAndKeep`, останется доступным после возврата — текущая реализация отменяет его регистрацию перед возвратом.
- Использовать `Libraries.alloc_minic` как "эталон" — он все еще использует глобальные переменные как наследие, и в `free` есть ошибка (см. [Что необходимо исправить](#что-необходимо-исправить), пункт 13).
- Вызывать `Step`/`Continue`/`Load` из разных потоков для одного `Debugger` — он не является потокобезопасным (кроме `RequestStop`).
- Ожидать точного стека вызовов от `GetCallStack()` — ВМ не хранит указатель кадра, восстановление эвристическое; возможны ложные срабатывания.
- Полагаться на `stop.Snapshot.IsSleeping` в `Continue` — флаг сна приходит только из `_stepSnapshot` (после `StepDebug`); при остановке через `RequestStop` он может быть `false`, даже если ВМ в `HALT`. Для обнаружения `HALT` более надежен `stop.Reason == Sleeping`.
- Использовать один `Debugger` для нескольких `Load` — повторный `Load` выбрасывает `InvalidOperationException`.
- Считать `GetOpCodeSafety() == Safe` гарантией безопасности памяти. `Safe` означает "нет `*_UNSAFE`", а не "нет произвольных адресов".
- Вызывать методы `AsyncDeviceContext` из нескольких потоков — режим контекста не является потокобезопасным. Безопасны из другого потока только `AsyncDevice.Stop`, `WakeProcessor` и `EnqueueExternalCommand`.
- Ожидать, что `AsyncDevice.StepDebug` / `BatchStepDebug` / `DebugRun` вызовут `onStep` **после** шага — текущая реализация вызывает его **до** шага (снимок до выполнения).
- Полагать, что `BreakPointerLaunchDevice(start: null)` означает "конец RAM" — текущая реализация использует `start ?? 0`; передавайте явный начальный адрес.
- Вызывать `AsyncDeviceContext.LoadProgram` / `ResetMemoryRam`, пока устройство работает — они выбрасывают `InvalidOperationException`; сначала вызовите `await actx.Stop()`.

---

## Что было исправлено в этой версии

1. `RunAndCleanup`/`RunOnce` (синхронные) теперь принимают `deviceName`, `output`, `encoding` и передают их в `RunAndKeep` — предыдущая асимметрия API (старый пункт 6 из "Что необходимо исправить") устранена.
2. Добавлены `AsyncRunOutcome` и асинхронный конвейер: `AsyncRunAndKeep` / `AsyncRunAndCleanup` / `AsyncRunOnce` на базе `AsyncDeviceContext`.
3. Добавлен модуль `AsyncDevice` (`AsyncDevice`, `AsyncDeviceContext`, `AsyncDeviceExtension`, `ReusableWakeSignal`) — устройство ВМ с собственной RAM/CPU, асинхронным шагом/запуском и утилитами для RAM. См. новый раздел API "Асинхронное устройство".

Оставшиеся проблемы после этого изменения отслеживаются в разделе [Что необходимо исправить](#что-необходимо-исправить); новые из них — это пункты 19 (`uartId!.Value` в синхронном `RunAndKeep`) и 20 (утечка асинхронного UART / несогласованный жизненный цикл UART).

---

## Особые примечания

**Двойной проход по секции данных**
И `Disassembler.Disassemble`, и `StringDisassembler.Disassemble` разделяют поток на код и данные по `codeSize`. Если `codeSize == 0`, весь поток считается данными. Если `codeSize == size`, секции данных нет. Если последняя инструкция с `data64` выходит за пределы `codeSize` (padding + 8 байт), `pos` после цикла будет `> codeSize`, и цикл данных начнется с `pos`, а не с `codeSize`. Первые `pos - codeSize` байт секции данных на самом деле являются хвостом последней инструкции и не будут показаны как данные. `bytesConsumed` в `DisassemblyResult` все равно будет равен `pos`.

**Отладчик: `_skipBreakpointOnce` и повторный `Continue`**
`Continue` в начале проверяет два условия:
1. `_skipBreakpointOnce == rIP` -> очищает флаг и не останавливается на текущем адресе (это та же точка останова, которая сработала в последний раз).
2. `HasBreakpoint(rIP)` -> возвращает `DebugStopReason.Breakpoint` без выполнения шага.
`StepOneOrFinish` после каждого `StepDebug` устанавливает `_skipBreakpointOnce = newIp`, если по новому `rIP` есть точка останова. Это дает корректную семантику:
Load -> Continue (сработала точка на 0x00) -> Continue (пропускает 0x00, продолжает).
Без `_skipBreakpointOnce`, `Continue` -> точка останова -> `Continue` зациклился бы навсегда на одном `rIP`.
`ContinueTo(addr)` использует ту же логику: добавляет `addr` в `_breakpoints`, вызывает `Continue`, удаляет точку. Если `addr` уже был пользовательской точкой — она сохраняется после вызова.

**Отладчик: `ProcessorSnapshot` в `DebugStop`**
В `Step`/`StepOver`/`StepOut`/`Continue` `ProcessorSnapshot` берется из `_stepSnapshot` (колбэк `StepDebug(onStep)`), поэтому:
- `IsSleeping` корректен (ВМ устанавливает `_isSleeping` перед вызовом колбэка);
- `IsRunning` корректен (ВМ очищает его перед вызовом колбэка);
- `RawInstruction` — слово по текущему `rIP` (после шага).
В ветви `Continue`, где остановка происходит до шага (проверка начального IP на точку останова), `Snapshot` строится через `BuildSnapshotFromRegisters` — там `IsSleeping == false` всегда, потому что `DeviceContext` не предоставляет флаг сна. Для обнаружения `HALT` более надежен `stop.Reason == Sleeping` (это приходит только из `_stepSnapshot`).

**Отладчик: `GetCallStack` — эвристика, а не точный стек**
ВМ не хранит указатель кадра и не ведет отдельный стек вызовов. `GetCallStack` сканирует `[rSP, RamSize)` на наличие 8-байтовых значений в диапазоне `[ProgramStart, ProgramEnd)`, выровненных по 4. Это находит адреса возврата, помещенные в стек командой `CALL`, но:
- Ложные срабатывания — локальные `ulong`, значения которых случайно попадают в диапазон кода.
- Пропуски — если `CALL` был косвенным (`CALL_IND`), адрес возврата также в стеке; но если программа использует собственный ручной стек вызовов (как `print_minic`/`alloc_minic`), `rSP` может указывать в другое место.
- Порядок — `frames[0]` — самый верхний (ближайший к текущему `rSP`), `frames[N]` — самый глубокий.
Точный стек требует указателя кадра (которого у ВМ нет) или отдельного стека вызовов в `Processor` (не реализовано). В отладчике — только best-effort.

**Отладчик: `StepOver` и адрес возврата**
Для `CALL` (непосредственный) длина инструкции составляет 12 байт (4 + 4 padding + 8 immediate), поэтому адрес возврата — `AlignUp(ip + 4, 8) + 8`. Для `CALL_IND` (косвенный) длина 4 байта, адрес возврата — `ip + 4`. Это соответствует §3.2 и §5.6 документа `bytecode.md`. Ошибка здесь привела бы к "проваливанию" в вызываемую функцию.

**Асинхронное устройство: режимы, колбэки до шага, HALT**
- `AsyncDeviceContext` имеет явный `Mode` (`Step` / `Continuous` / `None`): `Run` / `DebugRun` устанавливают `Continuous` и сбрасывают в `None` в `finally`; `RunStepMode` устанавливает `Step`; `Stop` / `EndStepMode` сбрасывают в `None`. Конфликтующий режим выбрасывает `InvalidOperationException`.
- `AsyncDevice` сам по себе не является потокобезопасным; безопасны из другого потока только `Stop`, `WakeProcessor` и `EnqueueExternalCommand`. Контекст также не является потокобезопасным — используйте один координирующий поток.
- `AsyncDevice.StepDebug` / `BatchStepDebug` и `DebugRun` вызывают `onStep` **до** `Step()` в текущей реализации. Снимок, следовательно, делается до выполнения инструкции. `DebugRun` также вызывает `onStep` перед проверкой лимита шагов. Для просмотра после шага вызовите `await actx.Step()`, а затем `actx.GetRegistersSnapshot()`.
- HALT/sleep: `AsyncDevice.Step()` вызывает `_processor.Step()`; если процессор спит, он ожидает `ReusableWakeSignal.WaitAsync(_processorPolicy.SleepWaitTimeout)`. `ReusableWakeSignal` фиксирует `Set()`, когда никто не ожидает; следующий `WaitAsync` немедленно возвращает `true`. Повторный вход в `WaitAsync` немедленно возвращает `true`. Пробуждение через `WakeProcessor()`, `Resume()` или `Stop()`.
- `BreakPointerLaunchDevice(ulong? start, Action? onLaunch)` сбрасывает процессор и запускает его по адресу `start ?? 0` (выровненному до 8). XML-комментарий говорит, что `null` означает "конец RAM", но текущий код использует `0`; передавайте явный начальный адрес.
- `AsyncDeviceContext.Stop()` в режиме `Step` выполняет `_device.Stop()`, а затем `await _device.StepDebug(null)`, чтобы процессор мог наблюдать `BiosStatus.HostStopped`; если процессор уже не работает, `StepDebug` не делает ничего.

**`Libraries.alloc_minic`**
Макет блока:
```text
+0..7    : size | FREE_BIT(бит 0)       — заголовок
+8..15   : next_free (только для свободных) — или начало полезной нагрузки
+16..sz-8: продолжение полезной нагрузки / неиспользуемое
+sz-8..sz: size | FREE_BIT(бит 0)       — нижний колонтитул (footer, для объединения слева)
```
`payload` начинается с `+8`. Минимальный блок — 24 байта. `HDR_FTR = 16` (8 заголовок + 8 footer) добавляется к выровненному `payload` в `alloc`: `need = ((size + 7) & ~7) + HDR_FTR`, но не менее `MIN_BLOCK`. При освобождении: `blkMark(b, size, 1)` -> `mergeRight(b)` -> `mergeLeft(b)` -> `blkPush(b)`. При выделении: first-fit по `heapFreeList`, если размер достаточен — `split` (если `csz - need >= MIN_BLOCK`), иначе `blkMark(curr, csz, 0)` (без разделения); иначе выделение через `GetSP()`.
**Известная ошибка в текущей версии**: в начале `free` есть строка `if (blkFree(b)) return;` **до** объявления `ulong b = ptr - 8;` — ссылка на необъявленную переменную. См. [Что необходимо исправить](#что-необходимо-исправить), пункт 13.
**Историческая справка**: ранее `alloc_minic` хранил все состояние в глобальных переменных из-за ошибки `FunctionGenerator.GenerateFunction` в компиляторе (асимметричные push/pop сохраняемых регистров с 3-4 локальными переменными). Ошибка исправлена — обходной путь больше не нужен, но текущий код еще не мигрирован на локальные переменные.

**`Libraries.imath_minic`, `Libraries.bitops_minic`, `Libraries.ovf_minic`**
Три независимые библиотеки на базе стандартных операторов MiniC — без `#include` и без оберток на ассемблере:
- `imath_minic` — `imin` / `imax` / `umin` / `umax`, `iclamp` / `uclamp`, `iabs`, `igcd` / `ugcd`, `ipow`, `uisqrt`, `idiv_floor` / `idiv_ceil` / `udiv_round_up`.
- `bitops_minic` — `upopcount` (Керниган), `uclz` / `uctz` (`0..64`, `64` для нуля), `rotl64` / `rotr64` (`n & 63`), `urev64`, `unext_pow2` (`0` — переполнение для `x > 2^63`).
- `ovf_minic` — `add_ovf_i32` / `sub_ovf_i32` / `mul_ovf_i32` (для знаковых 32-битных; `INT_MAX = 2147483647`, `INT_MIN = -2147483647 - 1`), `add_ovf_u64` / `sub_ovf_u64` / `mul_ovf_u64` (для беззнаковых 64-битных). Все возвращают `1` при переполнении и `0` в противном случае.

**`HostExtensions` и `sector`**
`RunAndKeep` / `RunOnce` / `RunAndCleanup` и их асинхронные аналоги принимают `sector` (по умолчанию `0`). Если `sector` занят другим устройством, `TryRegisterDevice` / `TryRegisterAsyncDevice` возвращает `IdState`, отличный от `Valid`, и результат имеет `Success == false` с диагностикой `"register vm: {state}"` в `Diagnostics` — без NRE (используется безопасный API). Если `output != null`, UART регистрируется на `sector + 1`; при ошибке регистрации UART ВМ откатывается через `UnregisterDevice`, и возвращается результат с `Success == false`, `Device == null`, диагностикой `"register uart: {state}"`.
Жизненный цикл UART в настоящее время несогласован между синхронными и асинхронными методами:
- Синхронный `RunAndKeep` безусловно отменяет регистрацию UART **перед возвратом** через `host.Emulator.UnregisterDevice(uartId!.Value)`. С одной стороны, это предотвращает утечку слота `sector + 1` при повторных вызовах; с другой стороны, это выбрасывает `InvalidOperationException`, когда `output == null` (`uartId` равен `null`). Кроме того, успешный "keep" не оставляет UART доступным в дальнейшем. См. пункт 19.
- Синхронные `RunAndCleanup`/`RunOnce` освобождают только ВМ — UART уже освобожден `RunAndKeep`, поэтому это корректно для пути успеха, но подвержено той же ошибке `uartId!.Value`, когда `output == null`.
- Асинхронный `AsyncRunAndKeep` вообще не отменяет регистрацию UART — он остается зарегистрированным (что соответствует семантике "keep"), но `AsyncRunOnce`/`AsyncRunAndCleanup` отменяют регистрацию только ВМ, поэтому UART "утекает" при повторных вызовах на одном `sector`. См. пункт 20.
Пути раннего возврата при ошибках внутри `RunAndKeep` (регистрация, контекст, загрузка) освобождают и ВМ, и (если уже зарегистрирован) UART через `if (uartId is int u) host.Emulator.UnregisterDevice(u)` — то же самое верно для `AsyncRunAndKeep`. Неверен только финальный безусловный вызов в синхронном `RunAndKeep`.

**`RunOutcome.FaultInfo` и `Success`**
`FaultInfo` — это `ProcessorFault` из `ctx.Run(new LaunchOptions(StartAddress: 0))` (синхронно) или `await ctx.Run(new LaunchOptions(StartAddress: 0), token)` (асинхронно). `Success` равен `fault.Status == BiosStatus.EndProgram`, а не "факт запуска" и не "успех компиляции". При ошибке компиляции/регистрации/загрузки `Success == false`, `Device == null`, `FaultInfo == default(ProcessorFault)`, а причина указана в `Diagnostics`. **Важно**: `_faultData` в `Processor` не сбрасывается в `LaunchProgramm` — второй `Run` на том же `Device` может вернуть ошибку от первого запуска, если первый завершился сбоем.

**`BytecodeExtension`: обрезанная программа и `fullyScanned`**
`GetOpCodeSafety`/`GetUnsafeOpCodes` итерируют по `Program` до `actualCodeSize = CodeSize ?? Program.Length`, но границы проверяются относительно `Program.Length` (а не `actualCodeSize`):
- `pos + 4 > Program.Length` -> парсинг останавливается (для `GetOpCodeSafety` — `Unknown`, для `GetUnsafeOpCodes` — `fullyScanned = false`);
- для опкодов с `HasNeed64IntData`: `nextPos + 8 > Program.Length` -> то же самое.
Причина: `Program` — это `byte[]`, и обрезка означает, что либо `Program` поврежден, либо `CodeSize` не соответствует `Program`. В первом случае `actualCodeSize > size` уже отфильтровано как `Unknown`. Во втором — `CodeSize < Program.Length` (нормальная ситуация: хвост `Program` может содержать секцию данных; `BytecodeExtension` не сканирует ее, так как `actualCodeSize` ограничивает цикл, но padding/обрезка на границе все равно проверяется относительно `Program.Length`).
`UnsafeOpCodeLocation.Offset` — это смещение в `Program` (байтовый массив `CompilationResult.Program`), а не в секции кода. Для `Program`, который совпадает с секцией кода (`CodeSize == Program.Length`), `Offset` совпадает с адресом относительно начала программы.

**Ограничения платформы**
- `net10.0`, `<AllowUnsafeBlocks>` не включен.
- `Thread.Sleep(1)` в `QueuedIOStream.Flush` — в WASM это либо no-op, либо `PlatformNotSupportedException`. Для WASM используйте `SynchronousIOStream`.
- `Channel<byte>` и `Task.Run(DrainAsync)` — работают в WASM, но фоновые задачи могут не выполняться до явного `await`.
- `ImmutableArray<T>` — работает в AOT/WASM, но каждая мутация через `CreateBuilder` выделяет память. Для больших программ (10k+ инструкций) `DisassemblyResult` может занимать много памяти. Для потоковой обработки используйте `StringDisassembler.Disassemble`.
- `Guid.NewGuid().GetHashCode()` в `InlineSmallVoidFunctionsRule` — стабилен в пределах процесса, но не между процессами. Проблема для детерминированных сборок.
- `Debugger` — `StepDebug` работает в WASM (одна инструкция — синхронно), но `Continue` / `StepOver` / `StepOut` / `GetCallStack` — это синхронные циклы, блокирующие текущий поток. В браузерном WASM (однопоточном) это блокирует UI. Для IDE в браузере — перенесите в Web Worker или используйте `Task.Run` с `RequestStop`.
- `AsyncDevice` — асинхронные циклы используют `ValueTask`, `Task.Yield()` и `ReusableWakeSignal`; `Run` / `DebugRun` / `Step` ожидаемы (awaitable), но само устройство/контекст не являются потокобезопасными. В браузерном WASM длинные циклы `Run` все равно занимают единственный поток между `await`; используйте `Stop`/отмену и явные точки `await`.

---

## Что необходимо исправить

1. **[API/XML] `SaveFileInclude` / `SaveFileProject` — обход пути (path traversal).**
   - **Файл:** `Host/ProjectExtension.cs`. `localPath` не проверяется на наличие `..` и абсолютных путей.
   - **Исправление:** валидировать `localPath` (`Path.IsPathRooted` + отсутствие `..` в сегментах) или задокументировать, что вызов предполагает доверенный ввод.

2. **[XML] `ReplaceSafeMemAccessWithUnsafeRule.SizeRam` — заниженное значение по умолчанию.**
   - **Файл:** `Optimizations/IRAsmRules/ReplaceSafeMemAccessWithUnsafeRule.cs`. `RamSize.B128` — это не "безопасный" default, а минимум; правило молча не срабатывает (no-op), если пользователь не установит размер.
   - **Исправление:** либо использовать `RamSize` из `Device` по умолчанию (но правило не знает о `Device`), либо добавить `<remarks>` с явным указанием: "вы должны установить `SizeRam` в размер RAM устройства, иначе правило не сработает".

3. **[API] `AddStdRules` для AST содержит `RemoveUnreachableCodeRule` дважды.**
   - **Файл:** `Optimizations/AstOptimizerExtension.cs`. Дублирование намеренное (после встраивания), но неочевидное.
   - **Исправление:** добавить `<remarks>` к `AddStdRules`, объясняющий, зачем нужен второй проход, или выделить это в отдельный составной метод `AddStdRulesWithCleanup()`.

4. **[API] `Debugger` — одноразовый для одного `Load`.**
   - **Файл:** `Debugging/Debugger.cs`. Повторный `Load` выбрасывает `InvalidOperationException`, чтобы режим шага не "утекал" (`ctx.RunStepMode` без `EndStepMode`).
   - **Исправление:** либо добавить `Reset()` (очищает режим шага, очищает точки останова, но сохраняет `Context`), либо явный `Unload()` / `Reload(program)`. Пока что — новый экземпляр на каждый запуск.

5. **[API] `Debugger.ReadRam` не защищен от `IsRunning`.**
   - **Файл:** `Debugging/Debugger.cs`. В отличие от `DeviceContextExtensions.GetRamAsNewArray` / `CopyRamTo` (которые выбрасывают `InvalidOperationException`, когда `ctx.IsRunning`), отладчик читает RAM **во время остановки** — это его нормальный сценарий. Но если пользователь вызовет `ReadRam` из другого потока во время `Continue`, данные будут содержать гонки (race-y).
   - **Исправление:** либо проверять `_isStepping` (флаг "выполняется `StepDebug`"), либо явно задокументировать это в `<remarks>` (сейчас только в README).

6. **[API/XML] `Debugger.StepOut` — эвристика на основе `rSP`.**
   - **Файл:** `Debugging/Debugger.cs`. Семантика "`rSP` стал больше, чем был" нарушается, если функция использует `PUSH`/`POP` для временных значений (типично для `print_minic` / `alloc_minic`). В таких случаях `StepOut` может выйти из функции раньше ожидаемого.
   - **Исправление:** либо задокументировать ограничение, либо добавить "глубину вызова" через `GetCallStack` (количество кадров) вместо `rSP` — но это более затратно.

7. **[API] `Debugger.GetCallStack` — best-effort, возможны ложные срабатывания.**
   - **Файл:** `Debugging/Debugger.cs`. ВМ не хранит указатель кадра; стек восстанавливается сканированием `[rSP, RamSize)` на наличие 8-байтовых значений в диапазоне кода. Локальные `ulong`, случайно попавшие в этот диапазон, создадут ложные кадры.
   - **Исправление:** либо `<remarks>` с явным предупреждением (сейчас только в README), либо реализация указателя кадра в `Processor` (критическое изменение для байт-кода).

8. **[API] `alloc_minic.free` — ссылка на `b` до ее объявления.**
   - **Файл:** `StdLibraries/Libraries.cs`. В начале `free(ulong ptr)` есть строка `if (blkFree(b)) return;`, но `b` объявляется ниже (`ulong b = ptr - 8;`). Это ошибка — ссылка на необъявленную переменную.
   - **Исправление:** удалить лишнюю строку `if (blkFree(b)) return;` (двойное освобождение все равно не поддерживается, и проверка `if (ptr == 0) return;` уже есть), либо переместить ее **после** объявления `b`: `ulong b = ptr - 8; if (blkFree(b)) return;`.

9. **[API/семантика] `RemoveSwapMovPairRule` удаляет `MOV rA, rB; MOV rB, rA`, предполагая "нулевой суммарный эффект".**
   - **Файл:** `Optimizations/IRAsmRules/RemoveSwapMovPairRule.cs`. При типичной семантике `MOV dst, src` (`dst = src`) этот паттерн **не** является no-op: после `MOV A, B` имеем `A = old B`, после `MOV B, A` — `B = old B`; итоговый результат: `A = old B, B = old B`. Правило корректно только если `OpCode.MOV` в ВМ имеет семантику обмена (крайне необычно) или если автор правила опирался на другую конвенцию.
   - **Исправление:** (a) прояснить семантику `OpCode.MOV` в `docs/bytecode.md`; (b) либо удалить правило из `AddStdRules`, либо переписать его для обмена (`XCHG` / `SWAP`), если такой опкод существует; (c) как минимум — добавить `<remarks>` с явным предупреждением.

10. **[API/XML] `AsyncDevice.StepDebug` / `BatchStepDebug` / `DebugRun` вызывают `onStep` до шага.**
    - **Файл:** `AsyncDevice/AsyncDevice.cs`. Колбэк получает `_processor.CurrentProcessorSnapshot` **до** `await Step()`, поэтому снимок делается до выполнения инструкции. Это легко неправильно истолковать как снимок после шага (особенно по сравнению со слоем `Debugger`).
    - **Исправление:** либо переместить вызов колбэка после `await Step()` (если предполагается семантика после шага), либо сохранить текущее поведение, но явно задокументировать его в XML `<remarks>`; в README это уже отмечено.

11. **[XML] `AsyncDevice.BreakPointerLaunchDevice` — `start: null` не означает "конец RAM".**
    - **Файл:** `AsyncDevice/AsyncDevice.cs`. XML-комментарий гласит, что `null` запускает с конца RAM, но реализация использует `GlobalInit(onLaunch, start ?? 0)`.
    - **Исправление:** либо изменить реализацию на использование фактического адреса конца RAM, либо исправить XML-комментарий, указав, что `null` означает `0`.

12. **[API] `AsyncRunAndKeep` / `AsyncRunOnce` / `AsyncRunAndCleanup` — нет ранней проверки отмены, и не все пути ошибок симметричны синхронному варианту.**
    - **Файл:** `Host/HostExtensions.cs`. Асинхронные варианты не проверяют `token` перед запуском конвейера; шаг компиляции синхронен и не может быть отменен; `AsyncRunAndKeep` работает на `AsyncDeviceContext`, поэтому потокобезопасность ограничена контекстом устройства (один координирующий поток). Отмена во время `Run` учитывается циклом процессора, но отмена шага компиляции невозможна.
    - **Исправление:** вызывать `token.ThrowIfCancellationRequested()` в начале `AsyncRunAndKeep` (и на входе в обертки `AsyncRunAndCleanup` / `AsyncRunOnce`), и задокументировать однопоточную модель `AsyncDeviceContext` в `<remarks>`.

14. **[API] Несогласованный жизненный цикл UART между синхронными и асинхронными вариантами `Run*`.**
    - **Файл:** `Host/HostExtensions.cs`. Синхронный `RunAndKeep` отменяет регистрацию UART перед возвратом; асинхронный `AsyncRunAndKeep` — нет. В результате:
      - синхронный: `RunAndKeep` с `output != null` не оставляет UART доступным (что неожиданно для метода с "keep" в названии), но `RunOnce`/`RunAndCleanup` не вызывают утечек;
      - асинхронный: `AsyncRunAndKeep` сохраняет UART, что соответствует названию "keep", но `AsyncRunOnce` / `AsyncRunAndCleanup` отменяют регистрацию только ВМ — слот `sector + 1` "утекает" при повторных вызовах с одним и тем же `sector` и `output != null`.
    - **Исправление:** выбрать одну модель и применить ее как к синхронным, так и к асинхронным методам. Например: `RunAndKeep` / `AsyncRunAndKeep` возвращают id UART (например, `UartDeviceId` в `RunOutcome` / `AsyncRunOutcome` или через отдельный `out` / возвращаемое значение) и **не** отменяют его регистрацию; `RunOnce` / `RunAndCleanup` и их асинхронные аналоги освобождают и ВМ, и UART. Это даст семантику "keep" для методов `*AndKeep` и отсутствие утечек для методов `*Once` / `*AndCleanup`. Как минимум, задокументировать выбранное поведение.