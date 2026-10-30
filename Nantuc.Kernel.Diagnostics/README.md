# Nantuc.Kernel.Diagnostics — README

> Общий модуль диагностики: коды ошибок, шаблоны сообщений, `ThrowCompiler`, `CodeException` и типизированные аргументы диагностики `DiagnosticData`.
> Выделен из ядра, чтобы авторы компиляторов и расширений могли использовать единый механизм ошибок и подменять провайдер сообщений, не трогая `Nantuc.Kernel`.
> `[← Корень](../README.md) · [Common](../Nantuc.Kernel.Common/README.md) · [Contracts](../Nantuc.Kernel.Contracts/README.md) · [Kernel](../Nantuc.Kernel/README.md) · [Compiler](../Nantuc.Compiler/README.md)`

## Содержание
- [Об этом модуле](#об-этом-модуле)
- [Дерево локального проекта](#дерево-локального-проекта)
- [API](#api)
- [Примеры для API](#примеры-для-api)
- [Приближенность модуля к пользователю](#приближенность-модуля-к-пользователю)
- [Что исправлено в этой версии](#что-исправлено-в-этой-версии)
- [Особенная информация](#особенная-информация)
  - [`DiagnosticData` и AOT](#diagnosticdata-и-aot) **[NEW]**
- [Что исправить](#что-исправить)

## Об этом модуле

`Nantuc.Kernel.Diagnostics` — автономный модуль диагностики, который обслуживает компилятор MiniC, ассемблер NANASM и потенциально любые другие backend'ы/расширения. Несмотря на `Kernel` в имени namespace, этот проект не зависит ни от одного другого проекта Nantuc — в `.csproj` нет ни одного `ProjectReference`. Это сделано намеренно: автору компилятора не нужно тянуть ядро (`Processor`, `MemoryBus`, нативные буферы, unsafe), чтобы просто бросать осмысленные ошибки с кодами.

Ключевые роли:
- `ErrorCode` — единый enum кодов ошибок, сгруппированных по подсистемам (Lexer, Parser, CodeGen, Asm).
- `IErrorMessageProvider` — точка расширения для локализации и кастомизации сообщений; содержит `static Default`.
- `DefaultErrorMessageProvider` — встроенный провайдер с английскими шаблонами; `overrides` **мержатся** поверх стандартных шаблонов.
- `DiagnosticData` — **типизированный аргумент диагностики** (заменяет `object`); AOT-safe, без рефлексии и боксинга. **[NEW]**
- `ThrowCompiler` — **instance-класс** (не статический) для бросания ошибок; хранит `ErrorMessageProvider` как поле экземпляра, без глобального mutable state. **[BREAKING]**
- `CodeException` — `Exception` с `ErrorCode` и `NumericCode`.

Философия модуля: код ошибки — это стабильный контракт (число, не строка), сообщение — это сменный ресурс (шаблон, не хардкод), аргументы — типизированные `DiagnosticData` (не `object`). Это позволяет:
- Тестировать компилятор по `ErrorCode`, не парся строки.
- Локализовать сообщения без пересборки ядра.
- Давать конечному пользователю русский/английский/любой язык через свой `IErrorMessageProvider`.
- Гарантировать AOT-совместимость на уровне аргументов диагностики (без рефлексии, без боксинга enum'ов).

> **XML-документация расширена.** Полностью документированы `ErrorCode` (каждый член), `ThrowCompiler` (класс и методы), `DiagnosticData` (частично), `DefaultErrorMessageProvider` (класс и публичные члены), `CodeException` (класс и члены). Без `///` — `IErrorMessageProvider.GetMessage` (только `<summary>`), `TryGetTemplate`. Подробности — в [Что исправить](#что-исправить).

## Дерево локального проекта

Все типы — в namespace `Nantuc.Kernel.Diagnostics`, структура плоская:

```
Nantuc.Kernel.Diagnostics/
├── Nantuc.Kernel.Diagnostics.csproj   # net10.0, без ProjectReference
├── CodeException.cs                   # class : Exception, c ErrorCode/NumericCode
├── DefaultErrorMessageProvider.cs     # FrozenDictionary шаблонов + overrides (мерж) + TryGetTemplate
├── DiagnosticData.cs                  # readonly record struct — типизированный аргумент диагностики [NEW]
├── ErrorCode.cs                       # enum ErrorCode (диапазоны 1000/1100/1200/1300/1400)
├── IErrorMessageProvider.cs           # interface + static Default
└── ThrowCompiler.cs                   # class (instance): Throw/Throw<T> + ErrorMessageProvider
```

## API

### Общая картина

| Тип | Вид | Default-реализация | Назначение |
|---|---|---|---|
| `ErrorCode` | `enum` | — | Стабильные числовые коды ошибок |
| `IErrorMessageProvider` | `interface` | **да, через `static Default`** | Форматирование сообщения по коду и типизированным аргументам |
| `DefaultErrorMessageProvider` | `class` | — | Встроенные английские шаблоны + `overrides` (мерж) + `TryGetTemplate` |
| **`DiagnosticData`** | `readonly record struct` | — | **Типизированный аргумент диагностики (замена `object`)** **[NEW]** |
| **`ThrowCompiler`** | **`class`** (не `static`) **[BREAKING]** | — | Instance-фасад для бросания `CodeException` |
| `CodeException` | `class : Exception` | — | Исключение с `ErrorCode` и `NumericCode` |

**Default-реализация интерфейса есть только у `IErrorMessageProvider`** — через `static IErrorMessageProvider Default { get; } = new DefaultErrorMessageProvider();`. Это согласуется с `IProcessorFaultPolicy.Default` в `Kernel.Contracts` и даёт единый паттерн «где живёт default» — на самом интерфейсе, а не на `ThrowCompiler`.

### `ErrorCode` — единый enum

Сгруппирован по подсистемам, числовые диапазоны:

| Диапазон | Подсистема | Коды |
|---|---|---|
| 1000–1001 | Общие | `NotSupported`, `NotImplemented` |
| 1100–1102 | Лексер | `Lexer_UnexpectedCharacter`, `Lexer_InvalidNumber`, `Lexer_UnterminatedString` |
| 1200–1208 | Парсер | `Parser_UnknownType`, `Parser_ExpectedToken`, `Parser_UnexpectedToken`, `Parser_InvalidAssignment`, `Parser_ArraySizeNotConstant`, `Parser_UnknownDirective`, `Parser_StructNotDefined`, `Parser_InvalidCharLiteral`, `Parser_IncludeAmbiguous` |
| 1300–1317 | CodeGen | `CodeGen_UnknownTypeSize` (1300) … `CodeGen_DirectStructLoadStore` (1313), `CodeGen_BreakOutsideLoop` (1314), `CodeGen_ContinueOutsideLoop` (1315), **`CodeGen_AssignmentToConst` (1316)**, **`CodeGen_IncrementOfConst` (1317)** **[NEW]** |
| 1400–1404 | Asm | `Asm_UndefinedLabel`, `Asm_DuplicateLabel`, `Asm_InvalidInstruction`, `Asm_UnknownRegister`, `Asm_UnknownMnemonic` |

Правило «код ошибки — это контракт»: **не меняйте числа существующих кодов** после релиза. Тесты компилятора и внешние тулы будут опираться на них. Новые коды — только с новым номером.

### `IErrorMessageProvider` — точка расширения

```csharp
public interface IErrorMessageProvider
{
    static IErrorMessageProvider Default { get; } = new DefaultErrorMessageProvider();
    string GetMessage(ErrorCode code, params ReadOnlySpan<DiagnosticData> args);  // [BREAKING]
}
```

- `static Default` — единая точка «что такое default-провайдер». `ThrowCompiler` инициализируется через него, а не создаёт `new DefaultErrorMessageProvider()` у себя.
- `params ReadOnlySpan<DiagnosticData>` — C# 13+ (`params` для `ReadOnlySpan`). Для `DiagnosticData` (value type) нулевой набор → `ReadOnlySpan<DiagnosticData>.Empty`, без аллокаций; 1+ аргумент — аллокация `DiagnosticData[]` (не `object[]`, без боксинга). **[NEW]**
- Контракт не требует бросать из `GetMessage` для неизвестного кода — это решает реализация. `DefaultErrorMessageProvider` бросает `InvalidOperationException`, но кастомный провайдер вправе вернуть `$"Error {(int)code}"`.

### `DiagnosticData` — типизированный аргумент диагностики **[NEW]**

```csharp
public readonly record struct DiagnosticData
{
    public string Text { get; }
    public DiagnosticData(string? text);
    public override string ToString() => Text;

    // Фабрики для типов вне неявных конверсий
    public static DiagnosticData Of(bool value);
    public static DiagnosticData Of<T>(T? value);

    // Неявные конверсии для частых типов
    public static implicit operator DiagnosticData(string? text);
    public static implicit operator DiagnosticData(char value);
    public static implicit operator DiagnosticData(int value);
    public static implicit operator DiagnosticData(uint value);
    public static implicit operator DiagnosticData(long value);
    public static implicit operator DiagnosticData(ulong value);
}
```

- Хранит **готовую строку** (`Text`), построенную в момент создания. Никакого `string.Format(object[])`, никакого виртуального `ToString()` на формат-времени, никакого боксинга int/char/enum.
- **Неявные конверсии** — для `string`, `char`, `int`, `uint`, `long`, `ulong`. Удобно: `ThrowCompiler.Throw(code, "x", 42)` работает без явных `new DiagnosticData(...)`.
- **Явные фабрики** — `Of(bool)` (иначе `bool` → `int` через неявные конверсии был бы `"0"`/`"1"`), `Of<T>(T?)` (fallback через `ToString()`, **только для своих типов** — см. [Особенная информация](#diagnosticdata-и-aot)).
- **`implicit operator DiagnosticData(Enum)` отсутствует намеренно** — это привело бы к `Enum.ToString()` (reflection-adjacent, AOT-риск). Для enum'ов используйте явные switch-хелперы (например, `TokenTypeExtension.AsText` в компиляторе). **[AOT]**

**Соглашение:** аргумент `DiagnosticData` для enum'а должен приходить уже как `string` или как `DiagnosticData`, построенный без `Enum.ToString()`. Для `char`/`int`/`uint`/`long`/`ulong` неявные операторы дают `InvariantCulture.ToString()` — без рефлексии.

### `DefaultErrorMessageProvider` — встроенные шаблоны

```csharp
public class DefaultErrorMessageProvider(
    FrozenDictionary<ErrorCode, string>? overrides = null)  // [BREAKING] FrozenDictionary, не Dictionary
    : IErrorMessageProvider
{
    public static FrozenDictionary<ErrorCode, string> DefaultTemplates { get; }
    public IReadOnlyDictionary<ErrorCode, string> Templates { get; }

    protected virtual string? TryGetTemplate(ErrorCode code);
    public string GetMessage(ErrorCode code, params ReadOnlySpan<DiagnosticData> args);
}
```

- Шаблоны по умолчанию — `FrozenDictionary<ErrorCode, string>`, read-only после инициализации. `DefaultTemplates` отдаёт именно `FrozenDictionary`, так что привести обратно к мутабельному `Dictionary` нельзя.
- `Templates` — `IReadOnlyDictionary` (внутри `ReadOnlyDictionary<,>`), актуальные шаблоны **этого экземпляра** (стандартные + `overrides`).
- `TryGetTemplate` — `protected virtual` точка расширения. `GetMessage` действительно вызывает `TryGetTemplate`, поэтому override работает.
- **`overrides` теперь мержится поверх стандартных** (раньше — заменял). Совпадающие ключи перезаписываются, остальные наследуются из дефолтного набора. `MergeTemplates` копирует `templates` в новый `Dictionary`, потом накрывает `overrides`. **[FIX]**
- `string.Format(template, parts)` — где `parts` — `string[]`, построенный из `args[i].Text`. `string[]` ковариантен `object[]`, поэтому `string.Format` получает тот же массив без копии. Боксинг отсутствует: элементы уже строки.
- Сообщения — на английском. Локализация — через замену `IErrorMessageProvider`, не через редактирование этого класса.

Примеры шаблонов:

| Код | Шаблон | Аргументы (`DiagnosticData`) |
|---|---|---|
| `Lexer_UnexpectedCharacter` | `"Unexpected character '{0}' at line {1}, column {2}"` | `char`, `int line`, `int col` |
| `Parser_ExpectedToken` | `"Expected {0}, got {1} at {2}:{3}"` | `string expected`, `string got`, `int line`, `int col` |
| `CodeGen_UnknownField` | `"Field '{0}' not found in struct '{1}'"` | `string field`, `string struct` |
| `Asm_DuplicateLabel` | `"Label '{0}' already defined at position {1}"` | `string label`, `int pos` |
| `CodeGen_BreakOutsideLoop` | `"Operator \"break\" outside loop"` | — |
| **`CodeGen_AssignmentToConst`** | **`"Cannot assign to const '{0}'"`** | **`string varName`** **[NEW]** |
| **`CodeGen_IncrementOfConst`** | **`"Cannot increment/decrement const '{0}'"`** | **`string varName`** **[NEW]** |

Порядок аргументов не задокументирован нигде, кроме кода (`DefaultErrorMessageProvider.templates`). См. [Что исправить, п. 3](#что-исправить).

### `ThrowCompiler` — instance-фасад **[BREAKING]**

```csharp
public class ThrowCompiler(IErrorMessageProvider? provider)
{
    public IErrorMessageProvider ErrorMessageProvider = provider ?? IErrorMessageProvider.Default;   // instance field

    [DoesNotReturn] [MethodImpl(MethodImplOptions.NoInlining)]
    public void Throw(ErrorCode code, params ReadOnlySpan<DiagnosticData> args);

    [DoesNotReturn] [MethodImpl(MethodImplOptions.NoInlining)]
    public T Throw<T>(ErrorCode code, params ReadOnlySpan<DiagnosticData> args);
}
```

- **Не `static class`.** `ThrowCompiler` теперь instance-класс. Каждый компилятор/ассемблер создаёт свой экземпляр: `new ThrowCompiler(provider)` и передаёт его как зависимость.
- `ErrorMessageProvider` — **instance поле** (не статическое свойство). Глобального mutable state нет. Это устраняет прежнюю проблему с thread-safety при подмене провайдера в рантайме.
- **`provider == null`** → используется `IErrorMessageProvider.Default`.
- `Throw(code, args)` — бросает `CodeException(code, provider.GetMessage(code, args))`. `[DoesNotReturn]` + `[MethodImpl(NoInlining)]` — JIT не инлайнит бросающий путь.
- `Throw<T>(code, args)` — то же, но возвращает `T`, чтобы использовать в выражениях: `return x ?? thrower.Throw<Foo>(code)`. `[DoesNotReturn]` гарантирует, что компилятор не будет ругаться на «возврат без значения».
- **Extension-методы `ValidateAllCodesCovered`/`TryValidateAllCodesCovered` удалены.** Валидация покрытия теперь — задача тестов (см. [Что исправить, п. 9](#что-исправить)). **[BREAKING]**

### `CodeException` — исключение

```csharp
public class CodeException(ErrorCode errorCode, string message) : Exception(message)
{
    public ErrorCode ErrorCode { get; }
    public int       NumericCode => (int)ErrorCode;
}
```

- Наследует `Exception`, `Message` — отформатированное сообщение.
- `ErrorCode` — типизированный код для `catch`/логики. `NumericCode` — для логов и внешних систем.
- Primary constructor (C# 12+) — лаконично, но нет конструктора с `innerException`. См. [Что исправить, п. 2](#что-исправить).
- Нет `[Serializable]`/`ISerializable` — в современном .NET это уже не требуется.

## Примеры для API

**Создание экземпляра и базовое бросание:**

```csharp
using Nantuc.Kernel.Diagnostics;

// Один экземпляр на компилятор/парсер/ассемблер
var thrower = new ThrowCompiler(provider: null);   // → IErrorMessageProvider.Default

if (current is null)
    thrower.Throw(ErrorCode.Lexer_UnexpectedCharacter, c, line, col);
// компилятор знает, что дальше код недостижим (DoesNotReturn)
```

**Использование в выражении (`Throw<T>`):**

```csharp
Foo foo = MaybeGet() ?? thrower.Throw<Foo>(
    ErrorCode.CodeGen_UndefinedVariable, variableName);
```

**Ловля по коду:**

```csharp
try
{
    Parse(source, thrower);
}
catch (CodeException ex) when (ex.ErrorCode == ErrorCode.Parser_ExpectedToken)
{
    Console.WriteLine($"syntax: {ex.Message}");
}
catch (CodeException ex)
{
    Console.WriteLine($"error {ex.NumericCode}: {ex.Message}");
}
```

**Свой провайдер (русские сообщения):**

```csharp
public sealed class RussianMessageProvider : IErrorMessageProvider
{
    private static readonly FrozenDictionary<ErrorCode, string> _t =
        new Dictionary<ErrorCode, string>
        {
            [ErrorCode.Lexer_UnexpectedCharacter] = "Неожиданный символ '{0}' в строке {1}, столбец {2}",
            [ErrorCode.Parser_ExpectedToken]      = "Ожидался {0}, получен {1} в {2}:{3}",
            [ErrorCode.CodeGen_AssignmentToConst] = "Нельзя присвоить значение const-переменной '{0}'",
            // …
        }.ToFrozenDictionary();

    public string GetMessage(ErrorCode code, params ReadOnlySpan<DiagnosticData> args)
        => _t.TryGetValue(code, out var t)
            ? (args.IsEmpty ? t : string.Format(t, args.ToArray().Select(a => a.Text).ToArray()))
            : $"Ошибка {(int)code}";
}

var thrower = new ThrowCompiler(new RussianMessageProvider());
```

**Наследование от `DefaultErrorMessageProvider` (переопределение пары шаблонов):**

```csharp
public sealed class MyProvider : DefaultErrorMessageProvider
{
    // TryGetTemplate действительно вызывается из GetMessage — override работает
    protected override string? TryGetTemplate(ErrorCode code) => code switch
    {
        ErrorCode.Parser_ExpectedToken => "Ожидался {0}, получен {1} ({2}:{3})",
        _ => base.TryGetTemplate(code),   // fallback на встроенные английские
    };
}

var thrower = new ThrowCompiler(new MyProvider());
```

**`overrides` — теперь мерж, не замена:** **[FIX]**

```csharp
// Только точечные переопределения — все остальные коды берутся из дефолтного набора
var p = new DefaultErrorMessageProvider(new Dictionary<ErrorCode, string>
{
    [ErrorCode.Lexer_UnexpectedCharacter] = "Custom 1",
    [ErrorCode.Parser_ExpectedToken]      = "Custom 2",
}.ToFrozenDictionary());

// Работает и для кодов, которых нет в overrides:
p.GetMessage(ErrorCode.CodeGen_UndefinedVariable, "x");
// → "Undefined variable 'x'" (из стандартных шаблонов)
```

**Изоляция провайдеров между инстансами:** **[BREAKING]**

```csharp
var throwerA = new ThrowCompiler(new RussianMessageProvider());
var throwerB = new ThrowCompiler(provider: null);   // → Default (английский)

// Нет глобального состояния: A и B не влияют друг на друга.
```

## Приближенность модуля к пользователю

Средняя. Для конечного пользователя (того, кто пишет на MiniC/NANASM) модуль не виден — ошибки приходят как `CodeException` с готовым сообщением. Для автора компилятора/ассемблера/расширения — это основной инструмент диагностики.

- Пользователь языка: видит `ex.Message` в консоли. Если язык локализован через `thrower.ErrorMessageProvider` — видит на своём языке. Импортировать этот модуль ему не нужно.
- Автор компилятора (MiniC, NANASM, сторонние): создаёт `ThrowCompiler` с нужным провайдером и использует `thrower.Throw` + `ErrorCode`. Может подменить провайдер через конструктор.
- Автор хост-приложения (IDE, REPL, CI): ловит `CodeException`, разбирает `ErrorCode`/`NumericCode`. Может создать `ThrowCompiler` со своим провайдером для каждого запроса/сессии.
- Автор ядра — не должен использовать этот модуль для внутренних ошибок. Ядро работает с `BiosStatus`/`ProcessorFault` (`Kernel.Common`), а не с `CodeException`.

**Что стоит делать:**
- Бросать `thrower.Throw` из frontend'а (Lexer/Parser/CodeGen) и assembler'а, а не `throw new Exception(...)`: это даёт стабильный `ErrorCode` для тестов.
- **Создавать `ThrowCompiler` один раз на компилятор/сессию и передавать как зависимость** (constructor injection). Компилятор уже это делает: `FunctionGenerator(asm, structTable, ThrowCompiler)`.
- В тестах ловить `CodeException` и проверять `ex.ErrorCode`, а не `ex.Message` — сообщения могут локализоваться.
- Использовать `thrower.Throw<T>` в выражениях (`??`, тернарниках) — это его основное назначение.
- Передавать в аргументы `DiagnosticData`-совместимые типы (`string`, `char`, `int`, `uint`, `long`, `ulong`) — неявные конверсии сработают.

**Что не стоит делать:**
- Использовать `ThrowCompiler` в `Kernel`/`Contracts`/`Common` — там своя модель ошибок (`BiosStatus`).
- Полагаться на глобальный mutable state для провайдера — его больше нет; создавайте новый `ThrowCompiler` при необходимости.
- Кидать `CodeException` вручную (`new CodeException(...)`) — используйте `thrower.Throw`, чтобы сообщение шло через провайдер.
- Передавать в аргументы `object` (бокс enum'ов/структур) — используйте `DiagnosticData`, чтобы не потерять AOT-совместимость.
- Полагаться на `Enum.ToString()` для аргументов — используйте явные switch-хелперы (например, `TokenTypeExtension.AsText` в компиляторе).

## Что исправлено в этой версии

По сравнению с прошлой фиксацией `Nantuc.Kernel.Diagnostics`:

- **`ThrowCompiler` — instance, не `static`.** **[BREAKING]** Убран глобальный mutable `ErrorMessageProvider` static-поле. Теперь у каждого компилятора свой провайдер; thread-safety — по построению. `Throw`/`Throw<T>` — instance-методы.
- **`DiagnosticData` — типизированные аргументы вместо `object`.** **[BREAKING]** `GetMessage(ErrorCode, params ReadOnlySpan<DiagnosticData>)`. Убран boxing `int`/`char`/enum, убрана рефлексия на формат-времени. Аргумент `DiagnosticData(string? text)` строит строку при создании.
- **`implicit operator DiagnosticData(Enum)` отсутствует намеренно** — это привело бы к `Enum.ToString()` (AOT-риск). Enum'ы должны приходить как `string` или через явные switch-хелперы.
- **`overrides` в `DefaultErrorMessageProvider` теперь мержится, а не заменяет.** **[FIX]** `MergeTemplates(templates, overrides)` копирует стандартные шаблоны и накрывает их `overrides`. Раньше передача одного кастомного сообщения отрезала все встроенные — исправлено.
- **`overrides` принимает `FrozenDictionary<ErrorCode, string>`, а не `Dictionary`.** **[BREAKING]** Гарантия, что переданный словарь не мутируют после конструирования.
- **`ErrorCode` получил XML-документацию на каждый член.** Полностью, включая `<remarks>` для `CodeGen_AssignmentToConst`/`CodeGen_IncrementOfConst` с перечислением триггеров. **[FIX]**
- **Новые `ErrorCode.CodeGen_AssignmentToConst = 1316` и `CodeGen_IncrementOfConst = 1317`** — для `const`-нарушений. С шаблонами в `DefaultErrorMessageProvider`. **[NEW]**
- **`ValidateAllCodesCovered`/`TryValidateAllCodesCovered` удалены.** **[BREAKING]** Валидация покрытия — задача тестов, а не runtime-API. См. [Что исправить, п. 9](#что-исправить).
- **`ThrowCompiler` XML-документация добавлена.** `<summary>`, `<param>`, `<remarks>`, `<exception>` на классе и обоих `Throw`/`Throw<T>`. **[FIX]**
- **`DefaultErrorMessageProvider` XML-документация добавлена.** `<summary>` с явным упоминанием мержа, `<param>` на `overrides`. **[FIX]**
- **`CodeException` XML-документация добавлена** (с опечатками `ошиибки`/`ввиде` — см. [Что исправить, п. 14](#что-исправить)).
- **`params ReadOnlySpan<DiagnosticData>`** — для `DiagnosticData` (value type) нулевой набор → `ReadOnlySpan.Empty`, без аллокаций. `string[]` для `string.Format` ковариантен `object[]`, без копии.

## Особенная информация

### Модель ошибок: `ErrorCode` vs `BiosStatus`

| Механизм | Модуль | Когда | Что значит |
|---|---|---|---|
| `ErrorCode` + `CodeException` | `Kernel.Diagnostics` | компиляция/ассемблирование | «ваш исходник не компилируется» |
| `BiosStatus` + `ProcessorFault` | `Kernel.Common` | исполнение байткода | «ваша программа упала в рантайме» |

`Kernel.Diagnostics` не используется ядром (`Kernel`/`Contracts`/`Common`). Если вы пишете runtime-код и хотите сообщить об ошибке — используйте `BiosStatus`/`ProcessorFault`, а не `CodeException`.

### Стабильность кодов

- Числа `ErrorCode` — wire-format. Не меняйте их после релиза.
- `NumericCode` — для логов, CI-парсинга, внешних тулов.
- Удаление `ErrorCode`-значения — breaking change. Помечайте `[Obsolete]`, но не удаляйте, пока не выйдет major.
- Шаблоны в `DefaultErrorMessageProvider.templates` — тоже контракт (порядок аргументов). Если меняете шаблон — либо сохраняйте порядок аргументов, либо документируйте как breaking.
- Диапазоны по подсистемам — соглашение: `1000` — общие, `1100` — Lexer, `1200` — Parser, `1300` — CodeGen, `1400` — Asm. Свободные номера внутри диапазона можно занимать; переходить на чужой диапазон — нет.

### `IErrorMessageProvider.Default` — почему `static` на интерфейсе

```csharp
static IErrorMessageProvider Default { get; } = new DefaultErrorMessageProvider();
```

Это C# 11+ static-член интерфейса с реализацией по умолчанию. Он не виртуален для реализаций (`Default` — это `static`, а не `static abstract`), поэтому `MyProvider.Default` вернёт тот же `DefaultErrorMessageProvider`, а не `MyProvider`. Это ожидаемо: `Default` — «что считается дефолтом в платформе», а не «дефолт для этого типа».

### `DiagnosticData` и AOT **[NEW]**

`DiagnosticData` спроектирован так, чтобы весь путь аргументов диагностики был AOT-safe:

| Механизм | AOT-риск | Что делать |
|---|---|---|
| `implicit operator DiagnosticData(string?)` | нет | — |
| `implicit operator DiagnosticData(char)` | нет (`char.ToString()` — BCL, без рефлексии) | — |
| `implicit operator DiagnosticData(int/uint/long/ulong)` | нет (`IFormattable`/`ToString(InvariantCulture)`, без рефлексии) | — |
| `Of(bool)` | нет | — |
| `Of<T>(T?)` | **есть**, если `T` — reference type без `ToString()` override → вызовет `Object.ToString()` → метаданные типа | Использовать только для известных типов (`Type`, `Enum`, свои классы с `ToString()`) |
| `implicit operator DiagnosticData(Enum)` | **не существует намеренно** — привёл бы к `Enum.ToString()` (reflection-adjacent) | Передавать enum как `string` через явный switch-хелпер |
| `string.Format(template, string[])` | нет (элементы уже строки, нет `IFormattable`-пути) | — |
| `$"No message template for error code {code}"` в `GetMessage` | **есть**: `ErrorCode.ToString()` → reflection-adjacent | Только fallback-путь при забытом шаблоне; для AOT-чистоты — заменить на `((int)code).ToString(InvariantCulture)` |

**Итог:** `DiagnosticData` полностью AOT-safe для всех типовых вызовов (`string`, `char`, `int`, `uint`, `long`, `ulong`, `bool`). Для `Of<T>` и `Enum` — действует соглашение «передавать готовую строку».

### `params ReadOnlySpan<DiagnosticData>` — особенности

- Требует C# 13+ (для `params` на `ReadOnlySpan<T>`) и `net9.0+` (для `string.Format(string, params ReadOnlySpan<object?>)` — но здесь используется `string.Format(template, string[])`, который есть и раньше). На `net10.0` — работает.
- `DiagnosticData` — `readonly record struct`, value type: нулевой набор аргументов → `ReadOnlySpan<DiagnosticData>.Empty`, без аллокаций. Один и более → аллокация `DiagnosticData[]` (не `object[]`, **без боксинга** int/char).
- `DiagnosticData` содержит `string Text` (reference), так что внутренние строки боксятся только при `new DiagnosticData(someInt.ToString())` — это неизбежно для строкового представления. Но боксинга самого `int`/`char` в `object` — нет.
- Если у вызывающего уже есть `ReadOnlySpan<DiagnosticData>` — он может передать его напрямую, без обёртки.

### Ограничения платформ

- `net10.0`. `params ReadOnlySpan<DiagnosticData>`, `FrozenDictionary`, `ReadOnlyDictionary`, `[DoesNotReturn]`, primary constructors, `readonly record struct` — всё есть. На `net8.0`/`net9.0` часть не соберётся.
- `unsafe` отсутствует — модуль безопасен для WASM/AOT и окружений с запретом `unsafe`.
- `FrozenDictionary` — в AOT работает, но инициализация чуть дороже, чем у обычного `Dictionary`. Один раз на процесс — норм.
- `Enum.GetValues<ErrorCode>()` (был в удалённом `ValidateAllCodesCovered`) — в AOT без `[DynamicDependency]` может быть вырезан триммером. Сейчас не используется; если вернёте валидацию — учтите.
- Локализация — `DefaultErrorMessageProvider` не использует `CultureInfo.CurrentUICulture`; сообщения всегда на английском. Если нужна локализация через `ResourceManager` — реализуйте свой `IErrorMessageProvider`.
- **`ThrowCompiler` — instance:** для WASM/AOT это даже лучше, чем static mutable state: нет глобального поля, которое нужно синхронизировать между запросами.

### Что этот модуль не делает

- Не форматирует ошибки в JSON/структурированный вид — только строка (`GetMessage`).
- Не содержит severity-уровней (Error/Warning/Info) — только ошибки.
- Не содержит source-позиций (файл, строка, столбец) как полей — они передаются аргументами в шаблон.
- Не знает про `IProcessorFaultPolicy`/`BiosStatus` — это runtime-модель, отдельно.
- Не зависит от других проектов Nantuc — можно подключать в любой .NET-проект как «Nantuc-style diagnostics».

Правило «куда положить тип»: код ошибки/исключение/провайдер сообщений/аргумент диагностики → `Kernel.Diagnostics`; runtime-статус → `Kernel.Common`; контракт → `Kernel.Contracts`; реализация исполнения → `Kernel`.

## Что исправить

По убыванию важности.

1. **[XML] Порядок аргументов шаблонов нигде не задокументирован.**  
   Файл: `DefaultErrorMessageProvider.cs`, `templates` — единственный источник. Автор своего провайдера вынужден читать исходник, чтобы узнать, что `Lexer_UnexpectedCharacter` ждёт `(char, int line, int col)`.  
   Фикс: `docs/error-codes.md` с таблицей «код → аргументы → шаблон» или `// {0}=char, {1}=line, {2}=col` рядом с каждым шаблоном.

2. **`CodeException` без `innerException`.**  
   Файл: `CodeException.cs`, primary constructor `(ErrorCode errorCode, string message)`.  
   Нет способа обернуть `IOException`/`FormatException`/`CodeException` из вложенного вызова. Кодогенератор и парсер часто ловят низкоуровневые исключения и хотят обернуть их в диагностику с кодом.  
   Фикс: добавить `public CodeException(ErrorCode, string, Exception inner)` и, возможно, `ThrowCompiler.Throw(code, inner, args)`.

3. **[AOT] `$"No message template for error code {code}"` использует `Enum.ToString()`.**  
   Файл: `DefaultErrorMessageProvider.GetMessage`. Это fallback при забытом шаблоне. В reflection-free режиме вернёт число вместо имени.  
   Фикс: `$"No message template for error code {(int)code}"` — `int.ToString()` без рефлексии.

4. **[AOT] `DiagnosticData.Of<T>(T? value) => new(value?.ToString())` — reflection-adjacent для reference-type без `ToString()` override.**  
   Файл: `DiagnosticData.cs`. Если `T` — класс без override, BCL вызовет `Object.ToString()` → метаданные.  
   Фикс: либо ограничить `where T : IFormattable`, либо задокументировать в `///`, что `Of<T>` — для известных типов.

5. **[XML] `IErrorMessageProvider.GetMessage` — контракт для неизвестного кода не задокументирован.**  
   Файл: `IErrorMessageProvider.cs`. `DefaultErrorMessageProvider` бросает `InvalidOperationException`, но кастомный провайдер вправе вернуть `$"Error {(int)code}"`.  
   Фикс: `/// <remarks>` на `GetMessage`: «Implementation may throw for unknown codes, or return a fallback string».

6. **Валидация покрытия не выполняется автоматически при старте.**  
   Файл: `DefaultErrorMessageProvider.cs`, статический конструктор отсутствует. При добавлении нового кода в `ErrorCode` и забывании шаблона ошибка вылезет в рантайме (`InvalidOperationException` из `GetMessage`), а не в CI.  
   Рекомендация: тест `AllErrorCodes_HaveTemplates` в `Nantuc.Kernel.Diagnostics.Tests`. Сейчас extension-методы `ValidateAllCodesCovered`/`TryValidateAllCodesCovered` удалены — валидация только тестами.  
   Фикс: добавить тест (см. п. 9).

7. **`CodeException` — нет `SourceSpan`/`FilePath`/`Line`/`Column` как полей.**  
   Файл: `CodeException.cs`. Позиции передаются аргументами в шаблон, но не хранятся в исключении. Для IDE/CI, которым нужны структурированные позиции, придётся парсить `Message`.  
   Рекомендация: добавить опциональные `FilePath`/`Line`/`Column` через отдельный конструктор или задокументировать, что позиции — только в `Message`.

8. **[XML] `DefaultErrorMessageProvider.TryGetTemplate` — без `<param>`/`<returns>`.**  
   Файл: `DefaultErrorMessageProvider.cs`. Protected virtual точка расширения, но без описания контракта (что возвращает, для чего используется).  
   Фикс: `<param>`/`<returns>` + `<remarks>` о том, что `GetMessage` вызывает его.

9. **Тестов нет.**  
   Самый дешёвый и ценный набор:
   - `DefaultErrorMessageProvider`: каждый `ErrorCode` из `Enum.GetValues<ErrorCode>()` → `GetMessage` не падает.
   - `overrides` — мерж: `new DefaultErrorMessageProvider(frozenWithOne)` → `GetMessage` для кода не из `overrides` работает (регресс-тест на старый баг).
   - `ThrowCompiler.Throw`: бросает `CodeException` с ожидаемым `ErrorCode`/`Message`.
   - `ThrowCompiler.Throw<T>`: компилируется в выражении (`return x ?? thrower.Throw<Foo>(...)`).
   - `ThrowCompiler(provider: null)` → `ErrorMessageProvider == IErrorMessageProvider.Default`.
   - **Изоляция провайдеров:** два `ThrowCompiler` с разными провайдерами не влияют друг на друга.
   - `TryGetTemplate` override работает.
   - `CodeException.NumericCode == (int)ErrorCode`.
   - `DiagnosticData`: `implicit`-конверсии для `string`/`char`/`int`/`uint`/`long`/`ulong` дают ожидаемые `Text`; `Of(bool)` → `"true"`/`"false"`; `Of<T>(null)` → `""`.

10. **Опечатки в XML-документации `CodeException.cs`.**  
    Файл: `CodeException.cs`: `"сообщение с ошиибки"` (→ `ошибки`), `"Код ошибки ввиде <c>enum</c>"` (→ `в виде`).  
    Фикс: поправить.

11. **Опечатка в XML-документации `IErrorMessageProvider.cs`.**  
    Файл: `IErrorMessageProvider.cs`: `"Политика исплючений"` (→ `исключений`).  
    Фикс: поправить.

12. **`ThrowCompiler` — имя класса.**  
    Файл: `ThrowCompiler.cs`. «Throw compiler» читается странно. Лучше `Diagnostics.Throw(...)`, `ErrorThrower`, `ThrowHelper`. Но если имя уже используется в компиляторе — оставьте, добавьте `[Obsolete]`-алиас при переименовании.

13. **`DiagnosticData` не имеет `implicit operator DiagnosticData(bool)`.**  
    Файл: `DiagnosticData.cs`. `Of(bool)` — есть, implicit — нет. Это сделано намеренно (bool через `int` был бы `"0"`/`"1"`), но в коде вызова приходится писать `DiagnosticData.Of(true)` вместо `true`.  
    Рекомендация: оставить как есть, добавить `<remarks>` в `Of(bool)` с объяснением, почему нет implicit.