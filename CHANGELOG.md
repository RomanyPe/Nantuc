# Changelog

## [0.7.2] - 2026-10-30

### Добавлено
- Компилятор распознаёт `const` (см. раздел «Константность» в README компилятора).
- Новые `ErrorCode`: `CodeGen_AssignmentToConst = 1316`, `CodeGen_IncrementOfConst = 1317`.
- `TokenTypeExtension.AsText` — без рефлексии (AOT-safe).
- `DiagnosticData` (`Nantuc.Kernel.Diagnostics`) — типизированный аргумент диагностики.
- Новые поля в `VariableNode`, `ParameterNode`, `GlobalInfo`, `VarLocation` (`IsConst`/`IsPointedConst`).
- Тесты на `const` (позитив + негатив — `RunCompile_ConstViolation`).

### Изменения
- **BREAKING:** `ThrowCompiler` — instance-класс (было `static`).
- **BREAKING:** `IErrorMessageProvider.GetMessage` и `ThrowCompiler.Throw` принимают `ReadOnlySpan<DiagnosticData>` (было `object`).
- **BREAKING:** `ValidateAllCodesCovered` / `TryValidateAllCodesCovered` удалены.
- `DefaultErrorMessageProvider` мержит `overrides` поверх стандартных шаблонов (было — замена).
- AST-узлы разнесены по отдельным файлам в `C/AST/`.
- `RegisterAllocationResult` вынесен в отдельный файл.
- Более AOT-friendly: `Enum.ToString()` убран из диагностики, `DiagnosticData` без боксинга.

### Документация
- **Compiler README:** новые разделы «Константность (`const`)» и «Диагностика: `DiagnosticData`»; обновлены дерево, API-таблица, пайплайн, «Что исправить», «Что сделано».
- **Kernel.Diagnostics README:** новый раздел про `DiagnosticData` и AOT; `ThrowCompiler` описан как instance; обновлены `ErrorCode`, `DefaultErrorMessageProvider`, примеры.## [0.7.1] - 2026-10-30
- **Корневой README:** изменеы метрики кода на соотсветсвующие.


### Добавлено:
- `IdStateExtension` класс расширения который может конвертировать `int` в `IdState` 
- Добавлен новый элемент `HostStopped` в `BiosStatus`, отправляется процессору при вызове `Stop()` у `Device` или `DeviceContext`
- Добавлена более подробная XML документация к `AlignmentExtensions`

### Изменения:
- Изменена документация в корневом README.md, определение "песочница" скорректрировано, частичная реализация политики песочница на хосте, Nantuc VM из коробки работает только в полностью изолированной песочнице, может только отправлять данные и получать их по запросу `IN` и `OUT`
- `ResetMemoryRam` вызывает `ThrowIfRunning()` с дефолтным `"Cannot launch program while device is running..."` — при попытке очистить RAM сообщение говорит «launch». `ResetMemoryRam` -> `ThrowIfRunning("Cannot clear RAM while device is running. Call Stop() first.")`.

### Исправления:
- `InstructionDecode.HasNeed64IntData(Opcode opcode)` ранее игнорировал `LOAD_UNSAFE` и `STORE_UNSAFE`, теперь они занесены в таблицу
- Исправлена и дополнена XML документация для класса `VmHostProject`


## [0.7.0] - 2026-10-29

### Добавлено:
- Компилятор NANASM умеет различать .data и .code сегменты
- `Nantuc.VmApplication.Unsafe` - директория обзавелась новыми классами для получения объектов `Kernel`
- начался вестись Changelog версия проекта зафиксирована как 0.7.0
- полная поддержка WASM и Aot Native
- написаны новые тесты:
	- проверка что id который приходит при не верных данных не валиден
	- проверка что `PortControllerBase` безопасен на двойной `Dispose`
	- проверка что при удалиние устройства из системы вызывается его `Dispose` и он удаляется из системы
- соответственные изменения README файлов затронутых разделов

### Исправления:
- `HaltWakeTests` для `DebugRun_ShouldStopAfterStepLimit` и `ReleaseRun_ShouldStopAfterStepLimit`- ранее была ошибка при которой свойство возвращало `StepCount` больше на 1 чем должно, исправлено

> Файл изменений начался вестись только 2026-10-29, до этого момента история только коммитами