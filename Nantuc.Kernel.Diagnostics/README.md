# Nantuc.Kernel.Diagnostics — README

> Общий модуль диагностики: коды ошибок, шаблоны сообщений, `ThrowCompiler` и исключение `CodeException`.  
> Выделен из ядра, чтобы авторы компиляторов и расширений могли использовать единый механизм ошибок и подменять провайдер сообщений, не трогая `Nantuc.Kernel`.  
> `[← Корень](../README.md) · [Common](../Nantuc.Kernel.Common/README.md) · [Contracts](../Nantuc.Kernel.Contracts/README.md) · [Kernel](../Nantuc.Kernel/README.md)`

## Содержание
- [Об этом модуле](#об-этом-модуле)
- [Дерево локального проекта](#дерево-локального-проекта)
- [API](#api)
- [Примеры для API](#примеры-для-api)
- [Приближенность модуля к пользователю](#приближенность-модуля-к-пользователю)
- [Рекомендации](#рекомендации)
- [Особенная информация](#особенная-информация)

## Об этом модуле

`Nantuc.Kernel.Diagnostics` — это **автономный модуль диагностики**, который обслуживает компилятор MiniC, ассемблер NANASM и потенциально любые другие backend'ы/расширения. Несмотря на `Kernel` в имени namespace, **этот проект не зависит ни от одного другого проекта Nantuc** — в `.csproj` нет ни одного `ProjectReference`. Это сделано намеренно: автору компилятора не нужно тянуть ядро (`Processor`, `MemoryBus`, нативные буферы, `unsafe`), чтобы просто бросать осмысленные ошибки с кодами.

Ключевые роли:
- **`ErrorCode`** — единый enum кодов ошибок, сгруппированных по подсистемам (Lexer, Parser, CodeGen, Asm).
- **`IErrorMessageProvider`** — точка расширения для локализации и кастомизации сообщений; содержит `static Default`.
- **`DefaultErrorMessageProvider`** — встроенный провайдер с английскими шаблонами; поддерживает `overrides` и `TryGetTemplate` для наследников.
- **`ThrowCompiler`** — статический «бросатель» ошибок + extension-методы валидации покрытия.
- **`CodeException`** — `Exception` с `ErrorCode` и `NumericCode`.

Философия модуля: **код ошибки — это стабильный контракт** (число, не строка), **сообщение — это сменный ресурс** (шаблон, не хардкод). Это позволяет:
- Тестировать компилятор по `ErrorCode`, не парся строки.
- Локализовать сообщения без пересборки ядра.
- Давать конечному пользователю русский/английский/любой язык через свой `IErrorMessageProvider`.

## Дерево локального проекта

Все типы — в namespace `Nantuc.Kernel.Diagnostics`, структура плоская:

```
Nantuc.Kernel.Diagnostics/
├── Nantuc.Kernel.Diagnostics.csproj   # net10.0, без ProjectReference
├── CodeException.cs                   # class : Exception
├── DefaultErrorMessageProvider.cs     # FrozenDictionary шаблонов + overrides + TryGetTemplate
├── ErrorCode.cs                       # enum ErrorCode
├── IErrorMessageProvider.cs           # interface + static Default
└── ThrowCompiler.cs                   # static: Throw/Throw<T> + extension-валидаторы
```

## API

### Общая картина

| Тип | Вид | Default-реализация | Назначение |
|---|---|---|---|
| `ErrorCode` | `enum` | — | Стабильные числовые коды ошибок |
| `IErrorMessageProvider` | `interface` | **да, через `static Default`** | Форматирование сообщения по коду и аргументам |
| `DefaultErrorMessageProvider` | `class` | — | Встроенные английские шаблоны + overrides + `TryGetTemplate` |
| `ThrowCompiler` | `static class` | — | Бросание `CodeException`, валидация покрытия (extension) |
| `CodeException` | `class : Exception` | — | Исключение с `ErrorCode` и `NumericCode` |

**Default-реализация интерфейса есть только у `IErrorMessageProvider`** — через `static IErrorMessageProvider Default { get; } = new DefaultErrorMessageProvider();`. Это согласуется с `IProcessorFaultPolicy.Default` в `Kernel.Contracts` и даёт единый паттерн «где живёт default» — на самом интерфейсе, а не на `ThrowCompiler`.

### `ErrorCode` — единый enum

Сгруппирован по подсистемам, числовые диапазоны:

| Диапазон | Подсистема | Коды |
|---|---|---|
| 1000–1001 | Общие | `NotSupported`, `NotImplemented` |
| 1100–1102 | Лексер | `Lexer_UnexpectedCharacter`, `Lexer_InvalidNumber`, `Lexer_UnterminatedString` |
| 1200–1207 | Парсер | `Parser_UnknownType`, `Parser_ExpectedToken`, `Parser_UnexpectedToken`, `Parser_InvalidAssignment`, `Parser_ArraySizeNotConstant`, `Parser_UnknownDirective`, `Parser_StructNotDefined`, `Parser_InvalidCharLiteral` |
| 1300–1315 | CodeGen | `CodeGen_UnknownTypeSize` … `CodeGen_DirectStructLoadStore` (1313), `CodeGen_BreakOutsideLoop` (1314), `CodeGen_ContinueOutsideLoop` (1315) |
| 1400–1404 | Asm | `Asm_UndefinedLabel`, `Asm_DuplicateLabel`, `Asm_InvalidInstruction`, `Asm_UnknownRegister`, `Asm_UnknownMnemonic` |

Правило «код ошибки — это контракт»: **не меняйте числа существующих кодов** после релиза. Тесты компилятора и внешние тулы будут опираться на них. Новые коды — только с новым номером.

### `IErrorMessageProvider` — точка расширения

```csharp
public interface IErrorMessageProvider
{
    static IErrorMessageProvider Default { get; } = new DefaultErrorMessageProvider();
    string GetMessage(ErrorCode code, params ReadOnlySpan<object> args);
}
```

- **`static Default`** — единая точка «что такое default-провайдер». `ThrowCompiler` инициализируется через него, а не создаёт `new DefaultErrorMessageProvider()` у себя. Если понадобится заменить default глобально — менять здесь.
- `params ReadOnlySpan<object>` — C# 13+ (`params` для `ReadOnlySpan`). Для `object` (reference type) нулевой набор аргументов → `ReadOnlySpan<object>.Empty`, **без аллокаций**; 1+ аргумент — аллокация `object[]` (ref-type нельзя `stackalloc`). Для пути ошибки это ок.
- Контракт **не требует** бросать из `GetMessage` для неизвестного кода — это решает реализация. `DefaultErrorMessageProvider` бросает `InvalidOperationException`, но кастомный провайдер вправе вернуть `$"Error {(int)code}"`.

### `DefaultErrorMessageProvider` — встроенные шаблоны

```csharp
public class DefaultErrorMessageProvider(Dictionary<ErrorCode, string>? overrides = null)
    : IErrorMessageProvider
{
    public static FrozenDictionary<ErrorCode, string> DefaultTemplates { get; }
    public IReadOnlyDictionary<ErrorCode, string> Templates { get; }

    protected virtual string? TryGetTemplate(ErrorCode code);
    public string GetMessage(ErrorCode code, params ReadOnlySpan<object> args);
}
```

- Шаблоны по умолчанию хранятся в `FrozenDictionary<ErrorCode, string>` — read-only после инициализации, быстрый lookup. `DefaultTemplates` отдаёт именно `FrozenDictionary` (не `IReadOnlyDictionary` поверх `Dictionary`), так что привести обратно к мутабельному `Dictionary` нельзя.
- `Templates` — `IReadOnlyDictionary` (внутри `ReadOnlyDictionary<,>`), актуальные шаблоны **этого экземпляра**. Если передан `overrides` — это `overrides.AsReadOnly()`, иначе `templates.AsReadOnly()`.
- `TryGetTemplate` — **`protected virtual`** точка расширения для наследников. `GetMessage` **действительно вызывает** `TryGetTemplate` (а не дублирует lookup), поэтому override работает:
  ```csharp
  var template = TryGetTemplate(code)
      ?? throw new InvalidOperationException($"No message template for error code {code}");
  return string.Format(template, args);
  ```
- `overrides` — **заменяет** словарь, а не мержит. См. [Рекомендации](#рекомендации) — это всё ещё не «overrides» в привычном смысле.
- `string.Format(template, args)` — .NET 9+ overload с `params ReadOnlySpan<object?>`, без промежуточного массива (если `args` уже span).
- Сообщения — на английском. Локализация — через замену `IErrorMessageProvider` (см. примеры), не через редактирование этого класса.

Примеры шаблонов (порядок аргументов важен, если пишете свой провайдер):

| Код | Шаблон | Аргументы |
|---|---|---|
| `Lexer_UnexpectedCharacter` | `"Unexpected character '{0}' at line {1}, column {2}"` | `char`, `int line`, `int col` |
| `Parser_ExpectedToken` | `"Expected {0}, got {1} at {2}:{3}"` | `string expected`, `string got`, `int line`, `int col` |
| `CodeGen_UnknownField` | `"Field '{0}' not found in struct '{1}'"` | `string field`, `string struct` |
| `Asm_DuplicateLabel` | `"Label '{0}' already defined at position {1}"` | `string label`, `int pos` |
| `CodeGen_BreakOutsideLoop` | `"Operator \"break\" outside loop"` | — (нет аргументов) |

> Порядок аргументов по-прежнему **не задокументирован нигде, кроме кода**. Для авторов собственных провайдеров это главный недостаток. См. [Рекомендации](#рекомендации).

### `ThrowCompiler` — статический фасад

```csharp
public static class ThrowCompiler
{
    public static IErrorMessageProvider ErrorMessageProvider { get; set; }

    [DoesNotReturn] public static void Throw(ErrorCode code, params ReadOnlySpan<object> args);
    [DoesNotReturn] public static T    Throw<T>(ErrorCode code, params ReadOnlySpan<object> args);

    extension(IReadOnlyDictionary<ErrorCode, string> templates)
    {
        public void ValidateAllCodesCovered();
        public bool TryValidateAllCodesCovered(out ErrorCode code);
    }
}
```

- `ErrorMessageProvider` — **глобальная точка подмены**. Инициализируется `IErrorMessageProvider.Default`. Сеттер защищён от `null` через `?? throw new ArgumentNullException`. Внутри используется `field`-keyword (C# 14) — см. [Особенная информация](#особенная-информация) про `<LangVersion>`.
- `Throw(code, args)` — бросает `CodeException(code, provider.GetMessage(code, args))`. `[DoesNotReturn]` + `[MethodImpl(NoInlining)]` — JIT не инлайнит бросающий путь в горячие методы.
- `Throw<T>(code, args)` — то же, но возвращает `T`, чтобы использовать в выражениях: `return x ?? ThrowCompiler.Throw<Foo>(code)`. `[DoesNotReturn]` гарантирует, что компилятор не будет ругаться на «возврат без значения».
- `ValidateAllCodesCovered()` — **extension-метод** (C# 14 `extension`-блок) на `IReadOnlyDictionary<ErrorCode, string>`. Бросает `InvalidOperationException` при первом пропуске. Один `foreach`, без двойной итерации.
- `TryValidateAllCodesCovered(out ErrorCode code)` — non-throwing версия, **тоже extension**. Один `foreach`, `out`-параметр `code` корректно отделён от переменной цикла `c` (CS0136 исправлен).
- `_errorCodesValidateTable` — `Enum.GetValues<ErrorCode>()`, кэшируется один раз на процесс.

> **Breaking change для вызывающих**: в предыдущей версии `ValidateAllCodesCovered` был static, теперь — extension. `ThrowHelper.ValidateAllCodesCovered(dict)` → `dict.ValidateAllCodesCovered()`. Если модуль уже кто-то использует — оставьте static-алиас на одну версию с `[Obsolete]`.

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
- **Primary constructor** (C# 12+) — лаконично, но нет конструктора с `innerException`. См. [Рекомендации](#рекомендации).
- **Нет `[Serializable]`/`ISerializable`** — в современном .NET это уже не требуется (binary formatter deprecated), но если планируете межпроцессную передачу — учтите.

## Примеры для API

**Базовое бросание:**

```csharp
using Nantuc.Kernel.Diagnostics;

if (current is null)
    ThrowCompiler.Throw(ErrorCode.Lexer_UnexpectedCharacter, c, line, col);
// компилятор знает, что дальше код недостижим (DoesNotReturn)
```

**Использование в выражении (`Throw<T>`):**

```csharp
Foo foo = MaybeGet() ?? ThrowCompiler.Throw<Foo>(
    ErrorCode.CodeGen_UndefinedVariable, variableName);
```

**Ловля по коду:**

```csharp
try
{
    Parse(source);
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
            // …
        }.ToFrozenDictionary();

    public string GetMessage(ErrorCode code, params ReadOnlySpan<object> args)
        => _t.TryGetValue(code, out var t)
            ? string.Format(t, args)
            : $"Ошибка {(int)code}";
}

ThrowCompiler.ErrorMessageProvider = new RussianMessageProvider();
// дальше все ThrowCompiler.Throw(...) отдают русский текст
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

ThrowCompiler.ErrorMessageProvider = new MyProvider();
```

**Валидация покрытия (в тестах или на старте):**

```csharp
// extension-метод
var templates = new Dictionary<ErrorCode, string> { /* … */ };
templates.ValidateAllCodesCovered();   // упадёт, если чего-то не хватает

if (!templates.TryValidateAllCodesCovered(out var missing))
    Console.WriteLine($"Нет шаблона для {missing}");

// готовые шаблоны тоже можно проверить
DefaultErrorMessageProvider.DefaultTemplates.ValidateAllCodesCovered();
```

**`overrides` — учтите семантику «заменить», не «дополнить»:**

```csharp
// Так вы потеряете ВСЕ встроенные шаблоны, кроме двух:
var p1 = new DefaultErrorMessageProvider(new()
{
    [ErrorCode.Lexer_UnexpectedCharacter] = "Custom 1",
    [ErrorCode.Parser_ExpectedToken]      = "Custom 2",
});
// p1.GetMessage(ErrorCode.CodeGen_UndefinedVariable, "x") → InvalidOperationException

// Если нужно «дополнить» — берите DefaultTemplates за основу:
var merged = new Dictionary<ErrorCode, string>(DefaultErrorMessageProvider.DefaultTemplates)
{
    [ErrorCode.Lexer_UnexpectedCharacter] = "Custom 1",
};
var p2 = new DefaultErrorMessageProvider(merged);
```

## Приближенность модуля к пользователю

**Средняя.** Для конечного пользователя (того, кто пишет на MiniC/NANASM) модуль **не виден вообще** — ошибки приходят как `CodeException` с готовым сообщением. Для **автора компилятора/ассемблера/расширения** — это основной инструмент диагностики.

- **Пользователь языка**: видит `ex.Message` в консоли. Если язык локализован через `ThrowCompiler.ErrorMessageProvider` — видит на своём языке. Импортировать этот модуль ему **не нужно**.
- **Автор компилятора** (MiniC, NANASM, сторонние): использует `ThrowCompiler.Throw` + `ErrorCode`. Может подменить `IErrorMessageProvider`.
- **Автор хост-приложения** (IDE, REPL, CI): ловит `CodeException`, разбирает `ErrorCode`/`NumericCode`. Может установить свой провайдер, чтобы сообщения соответствовали его UX.
- **Автор ядра** — **не должен** использовать этот модуль для внутренних ошибок. Ядро работает с `BiosStatus`/`ProcessorFault` (`Kernel.Common`), а не с `CodeException`. `CodeException` — для ошибок **компиляции/ассемблирования**, не для runtime-fault'ов.

**Что стоит делать:**
- Бросать `ThrowCompiler.Throw` из frontend'а (Lexer/Parser/CodeGen) и assembler'а, а не `throw new Exception(...)`: это даёт стабильный `ErrorCode` для тестов.
- Проверять покрытие в CI: `templates.ValidateAllCodesCovered()` в тесте — гарантия, что новый `ErrorCode` получит шаблон.
- Устанавливать `ThrowCompiler.ErrorMessageProvider` **один раз** на старте приложения, до первого `Throw`.
- В тестах ловить `CodeException` и проверять `ex.ErrorCode`, а не `ex.Message` — сообщения могут локализоваться.
- Использовать `ThrowCompiler.Throw<T>` в выражениях (`??`, тернарниках) — это его основное назначение.

**Что не стоит делать:**
- Использовать `ThrowCompiler` в `Kernel`/`Contracts`/`Common` — там своя модель ошибок (`BiosStatus`).
- Менять `ErrorMessageProvider` в рантайме (между потоками) — это глобальный mutable static; race condition. Устанавливайте на старте.
- Кидать `CodeException` вручную (`new CodeException(...)`) — используйте `ThrowCompiler.Throw`, чтобы сообщение шло через провайдер.
- Передавать `overrides` в `DefaultErrorMessageProvider` и ждать «мержа» — сейчас это «замена». См. [Рекомендации](#рекомендации).
- Полагаться на порядок аргументов без документации — сейчас порядок не задокументирован (см. ниже).

## Особенная информация

### Модель ошибок: `ErrorCode` vs `BiosStatus`

Важно не путать два разных механизма ошибок в платформе:

| Механизм | Модуль | Когда | Что значит |
|---|---|---|---|
| `ErrorCode` + `CodeException` | `Kernel.Diagnostics` | **компиляция/ассемблирование** | «ваш исходник не компилируется» |
| `BiosStatus` + `ProcessorFault` | `Kernel.Common` | **исполнение байткода** | «ваша программа упала в рантайме» |

`Kernel.Diagnostics` **не используется** ядром (`Kernel`/`Contracts`/`Common`). Если вы пишете runtime-код и хотите сообщить об ошибке — используйте `BiosStatus`/`ProcessorFault`, а не `CodeException`. `CodeException` — это «ошибка компилятора», а не «ошибка VM».

### Стабильность кодов

- Числа `ErrorCode` — **wire-format**. Не меняйте их после релиза.
- `NumericCode` — для логов, CI-парсинга, внешних тулов. Если у вас есть внешняя система, которая парсит `error 1300: …` — она опирается на эти числа.
- Удаление `ErrorCode`-значения — breaking change. Помечайте `[Obsolete]`, но не удаляйте, пока не выйдет major.
- Шаблоны в `DefaultErrorMessageProvider.templates` — тоже контракт (порядок аргументов). Если меняете шаблон — либо сохраняйте порядок аргументов, либо документируйте как breaking.
- **Диапазоны по подсистемам** — соглашение, а не инвариант. `1000` — общие, `1100` — Lexer, `1200` — Parser, `1300` — CodeGen, `1400` — Asm. Свободные номера внутри диапазона можно занимать; переходить на чужой диапазон — нет.

### Глобальное состояние `ThrowCompiler.ErrorMessageProvider`

Это **mutable static** — единственная точка на весь процесс. Следствия:

- **Не потокобезопасно при смене.** Если один поток читает `ErrorMessageProvider`, а другой пишет — data race. Устанавливайте провайдер **до** запуска потоков (на старте приложения).
- **Влияет на все компиляторы в процессе.** Если у вас в одном процессе и MiniC, и NANASM, и сторонний плагин — все они получат один провайдер. Для мультиязычного хостинга (например, IDE с переключением языка) это ограничение: провайдер придётся менять при смене локали (с теми же race-рисками).
- **Тесты могут «протекать».** Если один тест установил свой провайдер, а другой ожидает `Default` — второй упадёт. В тестах либо восстанавливайте провайдер в `finally`/`IDisposable`, либо запускайте в отдельном `AssemblyLoadContext`/процессе.

Если понадобится изоляция — альтернатива: сделать `ThrowCompiler` инстансным (`class ThrowCompiler { IErrorMessageProvider Provider { get; } ... }`) и передавать его в компилятор. Но это уже архитектурное изменение.

### `IErrorMessageProvider.Default` — почему `static` на интерфейсе

```csharp
static IErrorMessageProvider Default { get; } = new DefaultErrorMessageProvider();
```

Это C# 11+ static abstract/virtual-член интерфейса с реализацией по умолчанию. Он не виртуален для реализаций (`Default` — это `static`, а не `static abstract`), поэтому `MyProvider.Default` вернёт **тот же** `DefaultErrorMessageProvider`, а не `MyProvider`. Это ожидаемо: `Default` — «что считается дефолтом в платформе», а не «дефолт для этого типа». Если хотите, чтобы наследники могли переопределять — сделайте `static abstract` (но тогда все реализации обязаны его определить) или вынесите в отдельный `ErrorProviderDefaults` static-класс.

### `params ReadOnlySpan<object>` — особенности

- Требует **C# 13+** (для `params` на `ReadOnlySpan<T>`) и `net9.0+` (для `string.Format(string, params ReadOnlySpan<object?>)`). На `net10.0` — работает.
- Для `object` (reference type): нулевой набор аргументов → `ReadOnlySpan<object>.Empty`, **без аллокаций**. Один и более → аллокация `object[]` (ref-type нельзя `stackalloc`). Для пути ошибки это ок.
- **Boxing value-types**: `ThrowCompiler.Throw(code, 42, line)` упакует `42` и `line` в `object` — две аллокации. Для ошибки неважно, но если модуль когда-нибудь начнут использовать для «горячих» warning'ов — учитывайте.
- Если у вызывающего **уже есть** `ReadOnlySpan<object>` — он может передать его напрямую, без обёртки. Это единственный сценарий, где `params ReadOnlySpan` реально экономит.

### Ограничения платформ

- **`net10.0`.** `params ReadOnlySpan<object>`, `FrozenDictionary`, `ReadOnlyDictionary`, `[DoesNotReturn]`, primary constructors, `extension`-блоки, `field`-keyword — всё есть. На `net8.0`/`net9.0` часть не соберётся.
- **`field`-keyword** (C# 14) может требовать `<LangVersion>preview</LangVersion>` в зависимости от версии SDK. Проверьте сборку в чистом окружении.
- **`unsafe` отсутствует** — модуль безопасен для WASM/AOT и окружений с запретом `unsafe`. Это его сильная сторона.
- **`FrozenDictionary`** — в AOT работает, но инициализация чуть дороже, чем у обычного `Dictionary`. Один раз на процесс — норм.
- **`Enum.GetValues<ErrorCode>()`** — в AOT без `[DynamicDependency]` может быть вырезан триммером, если вы используете `ValidateAllCodesCovered`. Для `net10.0` с `PublishTrimmed=true` проверьте, что `ErrorCode` не стримится (обычно не стримится, но добавьте `[DynamicDependency(DynamicallyAccessedMemberTypes.PublicFields, typeof(ErrorCode))]` на `ThrowCompiler`, если хотите спать спокойно).
- **`string.Format`** с `ReadOnlySpan<object>` в AOT — работает, но `IFormattable`-форматирование аргументов идёт через reflection. Для ошибок это ок.
- **Локализация** — `DefaultErrorMessageProvider` не использует `CultureInfo.CurrentUICulture`; сообщения всегда на английском. Если нужна локализация через `ResourceManager` — реализуйте свой `IErrorMessageProvider` и установите его.

### Что этот модуль **не** делает

- Не форматирует ошибки в JSON/структурированный вид — только строка (`GetMessage`). Для структурированных логов используйте `ErrorCode`/`NumericCode` напрямую.
- Не содержит severity-уровней (Error/Warning/Info) — только ошибки. Если понадобятся предупреждения компилятора, это будет отдельный механизм (возможно, `WarningCode` или расширение `ErrorCode` с флагом).
- Не содержит source-позиций (файл, строка, столбец) как полей — они передаются аргументами в шаблон. Для IDE/CI, которым нужны структурированные позиции, придётся либо расширять `CodeException` (добавить `SourceSpan`), либо парсить сообщение. См. п. 2 Рекомендаций.
- Не знает про `IProcessorFaultPolicy`/`BiosStatus` — это runtime-модель, отдельно.
- Не зависит от других проектов Nantuc — можно подключать в любой .NET-проект как «Nantuc-style diagnostics».

Правило «куда положить тип»: **код ошибки/исключение/провайдер сообщений → `Kernel.Diagnostics`; runtime-статус → `Kernel.Common`; контракт → `Kernel.Contracts`; реализация исполнения → `Kernel`.**