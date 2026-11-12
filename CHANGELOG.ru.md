# Changelog

Все заметные изменения в этом проекте документируются в этом файле.

Формат основан на [Keep a Changelog](https://keepachangelog.com/ru/1.1.0/).

Порядок разделов внутри версии:
**Добавлено → Изменено → Устарело → Удалено → Исправлено → Документация**.

Краткие метки:
- **BREAKING** — обратно несовместимое изменение; может сломать пользовательский код при обновлении.

## [0.13.1] - 2026-11-12

### Исправлено
- баг изза которого `a < b << c;` не распарсивался (оставался `<< c;`)

## [0.13.0] - 2026-11-12

### Добавлено 
- новый метод в интерфейс `IProcessorPolicy` - `bool AllowUnsafeOpCodes { get; }` - разрешение использовать процессором небезопасные опкоды изза проблем с безопасностью
- сами блоки с условиями для *_UNSAFE инструкций
- новая структура `readonly record struct DiagnosticResult(string Message, ErrorCode ErrorCode)` для диагностики ошибок

### Изменено
- **BREAKING:** имя `IProcessorFaultPolicy` => `IProcessorPolicy`
- **BREAKING:** изменены свойства `Errors` для результатов компиляции, они теперь возвращают не только готовую строку но и по возможности `ErrorCode` - если не могут вернуть конкретный то возвращают `ErrorCode.NotImplemented`
- исправлен баг из за которого инструкции `LOAD_UNSAFE` и `STORE_UNSAFE` заменялись на свои безопасные варианты

### Документация
- синхронизирована документация с кодом

## [0.12.0] - 2026-11-10

### Изменено
- **BREAKING:** переименование класса `ILAssembler` -> `IRAssembler`
- **BREAKING:** переименование типов `Peephole*` -> `IR*`
- рефакторинг класса `RamFileService`, теперь он лучше эмулирует реальную фс, но все еще считается Mock классом

### Добавлено
- класс `VmCompilerOptimizatorExtension` - позволяет вытащить из `VmCompilerOptimizator` IR и AST оптимизаторы из слоя `Nantuc.Compiler`
- добавлены флаги дамба ОЗУ и регистров для CLI команды `run`
- `try - finally` блок для `DeviceContext.Run` и `DeviceContext.DebugRun` чтобы корректно возвращаться из запущенного состояния в незапущенное

## [0.11.0] - 2026-11-09 

### Добавлено
- `ROADMAP.md`
- бенчмарки сравнения `Nantuc MiniC` и `Nantuc VM` с другими языками и их скоростью вычислений

## [0.10.0] - 2026-11-08

### Добавлено
- новые методы для копирования ОЗУ `Nantuc VM`
- новый класс для статичного анализа байт кода - `Bytecode/BytecodeExtension.cs`
- у всех `NuGet` пакетов теперь есть свои `README.md` файлы

### Изменено
- **BREAKING:** смена имени класса `CompilerExtension` -> `DiagnosticsCompilerExtension`
- **BREAKING:** смена имени `namespace` `Compiler` -> `Diagnostics`

### Документация
- добавлены пункты о `Bytecode/BytecodeExtension.cs`
- синхронизированы пункты о `Diagnostics/DiagnosticsCompilerExtension.cs`
- синхронизированы пункты о `DeviceCtx/DeviceContextExtensions.cs`

## [0.9.0] - 2026-11-08

### Добавлено
- добавлен новый модуль `Nantu.CLI`
- добавлен новые тесты `Nantuc.CLI.Tests`

### Документация
- Обновлен корневой `README.md` и его локализация
- Добавлен новый `README.md` для `Nantuc.CLI`

## [0.8.3] - 2026-11-05

### Добавлено
- новые std библиотеки в `Nantuc.VmApplication.Extensions.StdLibraries.Libraries`: `imath`, `bitops`, `ovf`
- добавлен тест на аллокатор из `std`
 
### Изменено
- **BREAKING:** интерфейс `ITimerSimulationResult` заменен на `readonly record struct TimeSimulationResult(TimeSpan Elapsed, long Steps)` все места с старой сематикой заменены на новую

### Документация
- Обновлен корневой `README.md` и его локализация, он стал меньше

## [0.8.2] - 2026-11-04

### Изменено
- Репозиторий разделён на `src/`, `tests/`, `samples/`, `benchmarks/`.
- Solution-файл пересобран под новую раскладку.
- Обновлены относительные пути в `ProjectReference` после переезда проектов.
  
## [0.8.1] - 2026-11-04

### Изменено
- Настроена упаковка и публикация NuGet-пакетов через GitHub Actions.
- Изменений в коде нет.

## [0.8.0] - 2026-11-04

### Добавлено
- новый `ErrorCode` - `Parser_IncludeNotFound = 1209`,
- добавлены соотсветсвующие записи в `error-codes.md`
- добавлен соотсветсвующий элемент в `DefaultErrorMessageProvider`
- добавлены новые кейсы в [`Nantuc.Benchmarks/SourceCasesMiniC.cs`](Nantuc.Benchmarks/SourceCasesMiniC.cs)
- исходные файлы для бенчмарков находятся в отдельном файле [`Nantuc.Benchmarks/SourceCasesMiniC.cs`](Nantuc.Benchmarks/SourceCasesMiniC.cs)
- добавлен новый проект [`Nantuc.Benchmarks.BenchmarkDotNet`](Nantuc.Benchmarks.BenchmarkDotNet) - проект использующий фреймворк `BenchmarkDotNet` 

### Изменено
- **BREAKING:** все `namespace` вида `Nantuc.Kernel.*` и соотсветсвующие проекты изменены, убраны `*.Kernel.*` вставки. Для всех кроме `Nantuc.Kernel`

## [0.7.4] - 2026-11-01

### Добавлено
- **Отладчик** в `Nantuc.VmApplication.Extensions.Debugging`:
	- `Debugger` — пошаговое исполнение поверх `DeviceContext`: `Step`/`StepOver`/`StepOut`/`Continue`/`ContinueTo`/`RunToEnd`, точки останова (`AddBreakpoint`/`RemoveBreakpoint`/`ClearBreakpoints`/`HasBreakpoint`), `RequestStop()` (единственный потокобезопасный метод — `volatile bool`), `Wake()` из `HALT` (через `ctx.Resume()`), чтение RAM (`ReadRam`/`CopyRamTo`/`ReadByte`/`ReadUInt16`/`ReadUInt32`/`ReadUInt64`/`ReadChar`/`ReadCString`), дизассемблирование (`CurrentInstruction`/`DisassembleAt`/`DisassembleProgram` через `StringDisassembler`), best-effort стек вызовов (`GetCallStack`), событие `Stopped`. Не использует `AsRawDevice`/`unsafe` — только публичный API `DeviceContext` (`RunStepMode`/`SingleStep`/`GetRegistersSnapshot`/`NativeReadOnlyBuffer`).
	- `DebugStop` (`readonly record struct`) — `Reason` + `InstructionPointer` + `StepCount` + `ProcessorSnapshot` + `ProcessorFault`; свойство `IsTerminal` (`Completed || Faulted || Error`).
	- `DebugStopReason` (`enum`) — `None`/`StepComplete`/`Breakpoint`/`Completed`/`Faulted`/`Sleeping`/`StepLimit`/`UserStop`/`Error`.
	- `CallStackFrame` (`readonly record struct`) — `Index` + `ReturnAddress` + `StackAddress` (best-effort: сканирование `[rSP, RamSize)` на 8-байтные значения в диапазоне кода; VM не хранит frame pointer).
- Новые свойства у `ProcessorSnapshot` (нужны отладчику для `DebugStop.Snapshot` и `CurrentInstruction`):
	- `uint RawInstruction { get; }` — сырое слово инструкции по текущему `rIP`
	- `ulong DataByInstruction { get; }` — 64-битный immediate, пришедший с инструкцией
	- `BiosStatus CurrentBiosStatus { get; }` — статус на момент снимка
	- `ulong DataFault { get; }` — `DataByInstruction`, если `CurrentBiosStatus != Success`, иначе `0`
	- `OpCode OpCode { get; }` — декодированный опкод из `RawInstruction`
	- `RegType FirstRegister { get; }` — декодированный `Reg1`
	- `RegType SecondRegister { get; }` — декодированный `Reg2`
	- `OpCodeSize DataSizeCode { get; }` — декодированный `OpCodeSize` (имеет смысл для `LOAD`/`STORE`/`*_IND`)
- добавлен файл [`docs/error-code.md`](docs/error-code.md) с таблицей «код → аргументы → шаблон»
- добавлен не точный перевод для документации, русская документация остается просто добавляется `*.ru.*` перед `*.md`


### Изменено
- **BREAKING:** `ProcessorReader` изменено название класса на `ProcessorSnapshot`
- **BREAKING:** `INativeReadOnlyBuffer` изменено название интерфейса на `IReadOnlyNativeBuffer`
- Получение `ProcessorSnapshot` из `Processor`, изменено внутреннее получение в соответствии с новыми свойствами (`RawInstruction` читается из `rIP`, `OpCode`/`FirstRegister`/`SecondRegister`/`DataSizeCode` — через `InstructionDecoder.DecodeRawInstToEnums`, `DataByInstruction` — из `_lastFaultData`/immediate, `DataFault` — только при `CurrentBiosStatus != Success`)
- Кеширование и пометка `IsDirty` для `ManagerDevices`, значение `Count` кешируется, если произошли изменения то система пересчитывает все снова


### Исправлено
- закрыты все TODO в README модулей:
	- `Kernel.Common` — сломанные `</summary` в `DevicePortSize.B64/B128/B256`, устаревший `<remarks>` на `InstructionDecoder.HasNeed64IntData`
	- `Kernel.Contracts` — `ProcessorSnapshot.GetArr` бросает `ArgumentException` при `span.Length != 32` (было — `IndexOutOfRangeException`/молчаливые нули), XML на `IReadOnlyLogOptimization.GetLogs`
	- `Kernel.Diagnostics` — XML на `IErrorMessageProvider.GetMessage`/`TryGetTemplate`
	- `Kernel` — `Device.Dispose` освобождает `_wakeSignal` (было — утечка)
- `Device.Dispose` теперь освобождает `_wakeSignal`
- рассинхронизация в README файлах

### Удалено
- класс `BenchmarkOptimizer` из модуля `Nantuc.Benchmarks`


## [0.7.3] - 2026-10-31

### Добавлено
- Методы `Pause()` и `Resume()` для `DeviceContext` — просто заставляют процессор уснуть.
- Файл [`docs/bytecode.md`](docs/bytecode.md) — спецификация байткода.
- Файл [`docs/METRICS.md`](docs/METRICS.md) — метрики кода.
- Компилятор различает получение указателя на функцию и её вызов; поправлены массивы указателей.

### Изменено
- **BREAKING:** `ProcessorSnapshot` теперь `readonly struct` (было: `IProcessorSnapshot` — `interface`).
- `onStep` из `DebugRun` вызывается до `_processor.Step()` — благодаря этому мы получаем текущий `ProcessorSnapshot`, а не состояние следующей инструкции.
- Мелкие правки по коду.
- Тесты на работу компилятора разделены на более мелкие файлы.

### Документация
- **VmApplication README:** исправлены разделы «API и конструкторы», «Callback’и и результаты», «Типы и возвращаемые значения», «Файлы, загрузка и документация».
- **Корневой README:** метрики кода перенесены в `docs/METRICS.md`.
- Мелкие правки документации.

## [0.7.2] - 2026-10-30

### Добавлено
- Компилятор распознаёт `const` (см. раздел «Константность» в README компилятора).
- Новые `ErrorCode`: `CodeGen_AssignmentToConst = 1316`, `CodeGen_IncrementOfConst = 1317`.
- `TokenTypeExtension.AsText` — без рефлексии (AOT-safe).
- `DiagnosticData` (`Nantuc.Kernel.Diagnostics`) — типизированный аргумент диагностики.
- Новые поля в `VariableNode`, `ParameterNode`, `GlobalInfo`, `VarLocation` (`IsConst`/`IsPointedConst`).
- Тесты на `const` (позитив + негатив — `RunCompile_ConstViolation`).

### Изменено
- **BREAKING:** `ThrowCompiler` — instance-класс (было: `static`).
- **BREAKING:** `IErrorMessageProvider.GetMessage` и `ThrowCompiler.Throw` принимают `ReadOnlySpan<DiagnosticData>` (было: `object`).
- `DefaultErrorMessageProvider` мержит `overrides` поверх стандартных шаблонов (было: замена).
- AST-узлы разнесены по отдельным файлам в `C/AST/`.
- `RegisterAllocationResult` вынесен в отдельный файл.
- Более AOT-friendly: `Enum.ToString()` убран из диагностики, `DiagnosticData` без боксинга.

### Удалено
- **BREAKING:** `ValidateAllCodesCovered` / `TryValidateAllCodesCovered`

### Документация
- **Compiler README:** новые разделы «Константность (`const`)» и «Диагностика: `DiagnosticData`»; обновлены дерево, API-таблица, пайплайн, «Что исправить», «Что сделано».
- **Kernel.Diagnostics README:** новый раздел про `DiagnosticData` и AOT; `ThrowCompiler` описан как instance; обновлены `ErrorCode`, `DefaultErrorMessageProvider`, примеры.

## [0.7.1] - 2026-10-30

### Добавлено
- `IdStateExtension` — класс расширения для конвертации `int` в `IdState`.
- Новый элемент `HostStopped` в `BiosStatus` — отправляется процессору при вызове `Stop()` у `Device` или `DeviceContext`.

### Изменено
- **Песочница:** частично реализована политика песочницы на хосте; Nantuc VM из коробки работает только в полностью изолированной песочнице — может только отправлять данные и получать их по запросу `IN`/`OUT`. Определение «песочница» в корневом README скорректировано.
- `ResetMemoryRam` теперь передаёт в `ThrowIfRunning()` сообщение `"Cannot clear RAM while device is running. Call Stop() first."` вместо дефолтного «Cannot launch program while device is running…».

### Исправлено
- `InstructionDecode.HasNeed64IntData(Opcode opcode)` ранее игнорировал `LOAD_UNSAFE` и `STORE_UNSAFE` — теперь они занесены в таблицу.
- Исправлена и дополнена XML-документация для класса `VmHostProject`.

### Документация
- Добавлена более подробная XML-документация к `AlignmentExtensions`.
- **Корневой README:** метрики кода изменены на актуальные.

## [0.7.0] - 2026-10-29

### Добавлено
- Компилятор NANASM различает сегменты `.data` и `.code`.
- `Nantuc.VmApplication.Unsafe` — новые классы для получения объектов `Kernel`.
- Полная поддержка WASM и AOT Native.
- Новые тесты:
  - проверка, что ID, приходящий при неверных данных, невалиден;
  - проверка, что `PortControllerBase` безопасен при двойном `Dispose`;
  - проверка, что при удалении устройства из системы вызывается его `Dispose` и устройство удаляется из системы.

### Изменено
- Начат Changelog; версия проекта зафиксирована как 0.7.0.

### Исправлено
- `HaltWakeTests` (`DebugRun_ShouldStopAfterStepLimit`, `ReleaseRun_ShouldStopAfterStepLimit`): исправлена ошибка, при которой свойство возвращало `StepCount` на 1 больше ожидаемого.

### Документация
- Соответствующие изменения в README затронутых разделов.

> Changelog ведётся с 2026-10-29; до этой даты история доступна только в коммитах.