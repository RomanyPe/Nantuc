# Changelog
## [0.7.2] - 2026-10-30

### Добавлено:
- Компилятор распознает `const`. Значение хранится обычным образом, но любая попытка присвоить/инкрементить диагностируется на этапе кодогенерации.
- Новые `ErrorCode`:
	- `CodeGen_AssignmentToConst = 1316`
	- `CodeGen_IncrementOfConst = 1317`
- Добавлен класс `TokenTypeExtension` чтобы без рефлексии получать названия `TokenType`, с `extension` свойством `AsText`
- Новая структура в `Kernel.Common` - `DiagnosticData`, содержащая свойство `string Text`, а также `implicit operator` и статичные конструкторы
- Добавлены новые поля к `VariableNode`, `ParameterNode`, `GlobalInfo`, `VarLocation`
- Добавлены тесты на `const`
- Добавлена тесты отрицательной ветки компиляции - тоесть проверка что компиляция упадет, позже будут добавлены подобные проверки при надобности

### Изменения:
- все `AST` узлы перенесены в отдельную папку и разделены на отдельные файлы
- Поправлен `DefaultErrorMessageProvider`, в конструкторе он мержит 2 словаря в 1, также добавлены соответсвующие новые `ErrorCode`
- Изменена сигнатура `IErrorMessageProvider.GetMessage`, а также`ThrowCompiler.Throw`, теперь вместо `ReadOnlySpan<object> args` он имеет `ReadOnlySpan<DiagnosticData> args`
- Более AoT - frendly 
- `RegisterAllocationResult` вынесен в отдельный файл


## [0.7.1] - 2026-10-30

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