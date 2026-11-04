# Коды ошибок компилятора

> Справочник по `ErrorCode` (`Nantuc.Kernel.Diagnostics`).
> Числовые значения — **стабильный контракт**: попадают в логи, телеметрию,
> JSON и тесты. Существующие значения менять нельзя, новые — только
> в конец своей сотни.
>
> `[← Корень](../README.md) · [Kernel.Diagnostics](../Nantuc.Kernel.Diagnostics/README.md)`

## Содержание

- [Соглашения](#соглашения)
- [Диапазоны](#диапазоны)
- [Общие коды (1000–1099)](#общие-коды-10001099)
- [Лексер (1100–1199)](#лексер-11001199)
- [Парсер (1200–1299)](#парсер-12001299)
- [Генератор кода (1300–1399)](#генератор-кода-13001399)
- [Ассемблер (1400–1499)](#ассемблер-14001499)
- [Как добавить новый код](#как-добавить-новый-код)
- [Как переопределить текст](#как-переопределить-текст)
- [Примеры](#примеры)

## Соглашения

1. **Числа стабильны.** `NumericCode` из `CodeException` — часть контракта.
   Если код попал в релиз, его число менять нельзя никогда.
2. **Один код — один шаблон.** Каждый `ErrorCode` обязан иметь запись
   в `DefaultErrorMessageProvider.templates`. Инвариант проверяется тестом
   `DefaultTemplates_HasEntryForEveryDefinedCode`.
3. **Плейсхолдеры — `string.Format`.** Шаблон получает `{0}`, `{1}`, … из
   `DiagnosticData`. Число плейсхолдеров должно совпадать с числом аргументов
   на месте вызова; компилятор этого не проверяет.
4. **Никакой рефлексии.** Тексты — константы в `FrozenDictionary`. Это
   AOT/trimming-совместимо и работает в WASM.
5. **Локализация** — через `IErrorMessageProvider` (см.
   [Как переопределить текст](#как-переопределить-текст)). Встроенный набор —
   только английский.

## Диапазоны

| Диапазон | Подсистема | Кол-во | Статус |
|---|---|---|---|
| 1000–1099 | Общие | 2 | стабильно |
| 1100–1199 | Лексер | 3 | стабильно |
| 1200–1299 | Парсер | 9 | стабильно |
| 1300–1399 | Генератор кода (CodeGen) | 18 | стабильно |
| 1400–1499 | Ассемблер | 5 | стабильно |
| 1500+ | — | 0 | свободно |

Всего определено **37** кодов.

---

## Общие коды (1000–1099)

| Код | Значение | Шаблон | Когда возникает |
|---|---:|---|---|
| `NotSupported` | 1000 | `Not supported: {0}` | Операция/функция недоступна в текущей конфигурации. Используется редко — в основном для фич, отключённых флагом сборки. |
| `NotImplemented` | 1001 | `Feature not implemented: {0}` | Функциональность ещё не реализована. Обычно — заглушки в незаконченных ветках. |

---

## Лексер (1100–1199)

| Код | Значение | Шаблон | Когда возникает |
|---|---:|---|---|
| `Lexer_UnexpectedCharacter` | 1100 | `Unexpected character '{0}' at line {1}, column {2}` | Символ не может начать ни один токен. Плейсхолдеры: символ, строка, столбец. |
| `Lexer_InvalidNumber` | 1101 | `Invalid number literal '{0}' at {1}:{2}` | Неверный формат или переполнение числового литерала. |
| `Lexer_UnterminatedString` | 1102 | `Unterminated string starting at {0}:{1}` | Строковый литерал не закрыт до конца строки/файла. |

---

## Парсер (1200–1299)

| Код | Значение | Шаблон | Когда возникает |
|---|---:|---|---|
| `Parser_UnknownType` | 1200 | `Unknown type '{0}'` | Неизвестный тип в объявлении или выражении. |
| `Parser_ExpectedToken` | 1201 | `Expected {0}, got {1} at {2}:{3}` | Ожидался конкретный токен, получен другой. Плейсхолдеры: ожидаемый, полученный, строка, столбец. |
| `Parser_UnexpectedToken` | 1202 | `Unexpected token: {0}` | Токен неожиданен в данной грамматической позиции. |
| `Parser_InvalidAssignment` | 1203 | `Invalid assignment target` | LHS присваивания — не lvalue, либо типы несовместимы. |
| `Parser_ArraySizeNotConstant` | 1204 | `Array size must be constant` | Размер массива не сводится к константному выражению. |
| `Parser_UnknownDirective` | 1205 | `Unknown preprocessor directive: #{0}` | Директива препроцессора/ассемблера не распознана. |
| `Parser_StructNotDefined` | 1206 | `Struct '{0}' is not defined` | Использована необъявленная структура. |
| `Parser_InvalidCharLiteral` | 1207 | `Char literal '{0}' is invalid` | Символьный литерал пуст или слишком длинный. |
| `Parser_IncludeAmbiguous` | 1208 | `The file {0} exists in both {1} and {2}; please select a specific file.` | `include` нашёл несколько кандидатов с одинаковым именем. Плейсхолдеры: файл, путь 1, путь 2. |
| `Parser_IncludeNotFound` | 1209 | `The file {0} not found in \"{1}\" and \"{2}\"` | `include` не найдет в файлах проекта и папке `include` |

> **Особенность:** `Parser_IncludeAmbiguous` — единственный шаблон с тремя
> плейсхолдерами пути. При переопределении не потеряйте третий.

---

## Генератор кода (1300–1399)

| Код | Значение | Шаблон | Когда возникает |
|---|---:|---|---|
| `CodeGen_UnknownTypeSize` | 1300 | `Unknown type '{0}' for memory size` | Не удалось определить размер типа при генерации. |
| `CodeGen_UnknownOpCodeSize` | 1301 | `Unknown opcode size: {0}` | Неизвестный код размера операнда. |
| `CodeGen_UnknownStructType` | 1302 | `Unknown struct type '{0}'` | Неизвестный тип структуры. |
| `CodeGen_UnknownField` | 1303 | `Field '{0}' not found in struct '{1}'` | Обращение к несуществующему полю. |
| `CodeGen_NonStructAccess` | 1304 | `'{0}' is not a struct or pointer to struct` | Доступ к полю у не-структуры. |
| `CodeGen_InvalidDotAccess` | 1305 | `Dot access only for simple variables` | Доступ через точку к недопустимому члену. |
| `CodeGen_UndefinedVariable` | 1306 | `Undefined variable '{0}'` | Использование необъявленной переменной. |
| `CodeGen_CannotGetRegisterAddress` | 1307 | `Cannot take address of register variable '{0}'` | Попытка взять адрес регистровой переменной. |
| `CodeGen_NonIndexableType` | 1308 | `'{0}' is not an array or pointer` | Индексация не-массива и не-указателя. |
| `CodeGen_UnknownFunction` | 1309 | `Function '{0}' not found` | Вызов неизвестной функции. |
| `CodeGen_ArgumentCountMismatch` | 1310 | `Argument count mismatch for function '{0}'` | Число аргументов не совпадает с сигнатурой. |
| `CodeGen_ParameterNotInRegister` | 1311 | `Parameter '{0}' not allocated to a register` | Параметр должен быть в регистре, но не размещён. |
| `CodeGen_UnknownPointedType` | 1312 | `Cannot determine pointed type for dereference` | Тип по указателю не определён. |
| `CodeGen_DirectStructLoadStore` | 1313 | `Direct load/store of struct variable '{0}' is not supported` | Прямая загрузка/сохранение структуры без доступа к полям. |
| `CodeGen_BreakOutsideLoop` | 1314 | `Operator "break" outside loop` | `break` вне цикла. |
| `CodeGen_ContinueOutsideLoop` | 1315 | `Operator "continue" outside loop` | `continue` вне цикла. |
| `CodeGen_AssignmentToConst` | 1316 | `Cannot assign to const '{0}'` | Присваивание `const`-сущности (`=`, `+=`, `*p =`, `arr[i] =`). См. раздел «Константность» в README компилятора. |
| `CodeGen_IncrementOfConst` | 1317 | `Cannot increment/decrement const '{0}'` | `++`/`--` у `const` (префикс и постфикс, `*p`, `arr[i]`). |

> **Группа `const`-correctness** (1316–1317) добавлена в 0.7.2. Если
> добавляете новый `const`-код — только `1318+`, не переиспользуйте 1316/1317.

---

## Ассемблер (1400–1499)

| Код | Значение | Шаблон | Когда возникает |
|---|---:|---|---|
| `Asm_UndefinedLabel` | 1400 | `Undefined label '{0}' at position '{1}'` | Ссылка на неопределённую метку. |
| `Asm_DuplicateLabel` | 1401 | `Label '{0}' already defined at position {1}` | Метка объявлена повторно. |
| `Asm_InvalidInstruction` | 1402 | `Invalid instruction syntax: {0}` | Неверные операнды или формат инструкции. |
| `Asm_UnknownRegister` | 1403 | `Unknown register '{0}'` | Регистр не распознан. |
| `Asm_UnknownMnemonic` | 1404 | `Unknown mnemonic '{0}'` | Мнемоника не распознана. |

> **Замечание по `Asm_UndefinedLabel`:** шаблон оборачивает `{1}` в кавычки
> (`at position '{1}'`), в отличие от `Asm_DuplicateLabel` (`at position {1}`).
> Скорее всего, опечатка. Пока не исправлено — тест
> `NumericContract_IsStable` фиксирует число, но **не** текст; если будете
> править шаблон, добавьте регресс-тест на точную строку.

---

## Как добавить новый код

1. **Выберите число** в конце соответствующей сотни. Никогда не переиспользуйте
   удалённые числа — они могли попасть в чьи-то логи.
2. **Добавьте в `ErrorCode.cs`** в конец своей группы, с `///`-описанием
   и `<remarks>`, если семантика неочевидна.
3. **Добавьте в `DefaultErrorMessageProvider.templates`** запись
   `{ ErrorCode.YourCode, "Your template with {0}" }`.
4. **Добавьте `[InlineData]` в `NumericContract_IsStable`** — фиксирует число.
5. **Добавьте `[InlineData]` в `GetMessage_*`-тест** — фиксирует текст
   (иначе случайная правка шаблона проскочит).
6. **Обновите эту таблицу** и `docs/METRICS.md`, если там считается число кодов.

Инварианты, которые проверяются тестами автоматически:

- `DefaultTemplates_HasEntryForEveryDefinedCode` — каждый `ErrorCode` есть в словаре.
- `GetMessage_EveryDefinedCode_DoesNotThrow` — ни один код не падает без аргументов.
- `NumericContract_IsStable` — захардкоженные числа не поехали.

## Как переопределить текст

```csharp
using System.Collections.Frozen;
using Nantuc.Kernel.Diagnostics;

var overrides = new Dictionary<ErrorCode, string>
{
    [ErrorCode.Parser_UnknownType] = "Неизвестный тип «{0}»",
    [ErrorCode.CodeGen_AssignmentToConst] = "Нельзя присвоить const-переменной «{0}»",
}.ToFrozenDictionary();

var provider = new DefaultErrorMessageProvider(overrides);
var thrower = new ThrowCompiler(provider);

// Дальше — как обычно:
thrower.Throw(ErrorCode.Parser_UnknownType, (DiagnosticData)"Foo");
// → CodeException: "Неизвестный тип «Foo»"
```

Семантика — **мерж поверх дефолтов**, не замена: коды, которых нет в
`overrides`, продолжают работать из стандартного набора. Это регресс-тест
`Overrides_MergeWithDefaults_Regression`.

`IErrorMessageProvider.Default` — синглтон со стандартным набором. Для
глобальной локализации подмените провайдер на уровне `ThrowCompiler`
(создайте новый экземпляр) — `Default` не мутируется.

## Примеры

**Базовое использование:**

```csharp
var thrower = new ThrowCompiler(provider: null); // → Default

// Один аргумент
thrower.Throw(ErrorCode.Parser_UnknownType, (DiagnosticData)"Foo");
// CodeException: "Unknown type 'Foo'"

// Несколько аргументов, смешанные типы
thrower.Throw(
    ErrorCode.Parser_ExpectedToken,
    (DiagnosticData)"';'",
    (DiagnosticData)"'}'",
    (DiagnosticData)10,
    (DiagnosticData)5);
// CodeException: "Expected ';', got '}' at 10:5"
```

**В выражении (generic-форма):**

```csharp
// Не нужна отдельная ветка throw
string? value = null;
return value ?? thrower.Throw<string>(ErrorCode.NotSupported, (DiagnosticData)"missing");
```

**Ловля и разбор:**

```csharp
try
{
    compiler.Compile(source);
}
catch (CodeException ex)
{
    Console.Error.WriteLine($"[{ex.NumericCode}] {ex.ErrorCode}: {ex.Message}");
    // [1200] Parser_UnknownType: Unknown type 'Foo'
}
```

**Culture-invariance:** числа в `DiagnosticData` форматируются через
`CultureInfo.InvariantCulture`. Это проверяется тестом
`ImplicitConversions_AreCultureIndependent` — сообщения в логах/JSON не
зависят от текущей культуры потока.

## Ссылки

- [`ErrorCode.cs`](../Nantuc.Kernel.Diagnostics/ErrorCode.cs) — определения.
- [`DefaultErrorMessageProvider.cs`](../Nantuc.Kernel.Diagnostics/DefaultErrorMessageProvider.cs) — шаблоны.
- [`DiagnosticData.cs`](../Nantuc.Kernel.Diagnostics/DiagnosticData.cs) — аргументы.
- [`ThrowCompiler.cs`](../Nantuc.Kernel.Diagnostics/ThrowCompiler.cs) — фасад.
- [`CodeException.cs`](../Nantuc.Kernel.Diagnostics/CodeException.cs) — исключение.
- [`Nantuc.Kernel.Diagnostics/README.md`](../Nantuc.Kernel.Diagnostics/README.md) — обзор модуля.