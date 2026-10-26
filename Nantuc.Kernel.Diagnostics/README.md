# Nantuc.Kernel.Diagnostics — README

> Общий модуль диагностики: коды ошибок, шаблоны сообщений, `ThrowCompiler` и исключение `CodeException`.  
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
- [Что исправить](#что-исправить)

## Об этом модуле

`Nantuc.Kernel.Diagnostics` — автономный модуль диагностики, который обслуживает компилятор MiniC, ассемблер NANASM и потенциально любые другие backend'ы/расширения. Несмотря на `Kernel` в имени namespace, этот проект не зависит ни от одного другого проекта Nantuc — в `.csproj` нет ни одного `ProjectReference`. Это сделано намеренно: автору компилятора не нужно тянуть ядро (`Processor`, `MemoryBus`, нативные буферы, unsafe), чтобы просто бросать осмысленные ошибки с кодами.

Ключевые роли:
- `ErrorCode` — единый enum кодов ошибок, сгруппированных по подсистемам (Lexer, Parser, CodeGen, Asm).
- `IErrorMessageProvider` — точка расширения для локализации и кастомизации сообщений; содержит `static Default`.
- `DefaultErrorMessageProvider` — встроенный провайдер с английскими шаблонами; поддерживает `overrides` и `TryGetTemplate` для наследников.
- `ThrowCompiler` — статический «бросатель» ошибок + extension-методы валидации покрытия.
- `CodeException` — `Exception` с `ErrorCode` и `NumericCode`.

Философия модуля: код ошибки — это стабильный контракт (число, не строка), сообщение — это сменный ресурс (шаблон, не хардкод). Это позволяет:
- Тестировать компилятор по `ErrorCode`, не парся строки.
- Локализовать сообщения без пересборки ядра.
- Давать конечному пользователю русский/английский/любой язык через свой `IErrorMessageProvider`.

> **XML-документация в работе.** Часть типов уже документирована (`OptimizationId` в `Contracts`), `Nantuc.Kernel.Diagnostics` — пока без `///` на публичных членах (`ErrorCode`, `ThrowCompiler`, `CodeException`, `DefaultErrorMessageProvider`). Это осознанно: приоритет — стабилизировать форму API, потом — наполнить `///`. В разделе [Что исправить](#что-исправить) — список того, что стоит задокументировать в первую очередь (по убыванию риска неправильного прочтения).

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

- `static Default` — единая точка «что такое default-провайдер». `ThrowCompiler` инициализируется через него, а не создаёт `new DefaultErrorMessageProvider()` у себя.
- `params ReadOnlySpan<object>` — C# 13+ (`params` для `ReadOnlySpan`). Для `object` (reference type) нулевой набор аргументов → `ReadOnlySpan<object>.Empty`, без аллокаций; 1+ аргумент — аллокация `object[]`. Для пути ошибки это ок.
- Контракт не требует бросать из `GetMessage` для неизвестного кода — это решает реализация. `DefaultErrorMessageProvider` бросает `InvalidOperationException`, но кастомный провайдер вправе вернуть `$"Error {(int)code}"`.

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

- Шаблоны по умолчанию — `FrozenDictionary<ErrorCode, string>`, read-only после инициализации. `DefaultTemplates` отдаёт именно `FrozenDictionary`, так что привести обратно к мутабельному `Dictionary` нельзя.
- `Templates` — `IReadOnlyDictionary` (внутри `ReadOnlyDictionary<,>`), актуальные шаблоны **этого экземпляра**.
- `TryGetTemplate` — `protected virtual` точка расширения. `GetMessage` действительно вызывает `TryGetTemplate` (а не дублирует lookup), поэтому override работает.
- `overrides` — **заменяет** словарь, а не мержит (см. [Что исправить, п. 2](#что-исправить)).
- `string.Format(template, args)` — .NET 9+ overload с `params ReadOnlySpan<object?>`, без промежуточного массива (если `args` уже span).
- Сообщения — на английском. Локализация — через замену `IErrorMessageProvider`, не через редактирование этого класса.

Примеры шаблонов (порядок аргументов важен, если пишете свой провайдер):

| Код | Шаблон | Аргументы |
|---|---|---|
| `Lexer_UnexpectedCharacter` | `"Unexpected character '{0}' at line {1}, column {2}"` | `char`, `int line`, `int col` |
| `Parser_ExpectedToken` | `"Expected {0}, got {1} at {2}:{3}"` | `string expected`, `string got`, `int line`, `int col` |
| `CodeGen_UnknownField` | `"Field '{0}' not found in struct '{1}'"` | `string field`, `string struct` |
| `Asm_DuplicateLabel` | `"Label '{0}' already defined at position {1}"` | `string label`, `int pos` |
| `CodeGen_BreakOutsideLoop` | `"Operator \"break\" outside loop"` | — (нет аргументов) |

Порядок аргументов не задокументирован нигде, кроме кода (`DefaultErrorMessageProvider.templates`). Для авторов собственных провайдеров это главный недостаток — см. [Что исправить, п. 3](#что-исправить).

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

- `ErrorMessageProvider` — глобальная точка подмены. Инициализируется `IErrorMessageProvider.Default`. Сеттер защищён от `null` через `?? throw new ArgumentNullException`. Внутри используется `field`-keyword (C# 14) — см. [Особенная информация](#особенная-информация).
- `Throw(code, args)` — бросает `CodeException(code, provider.GetMessage(code, args))`. `[DoesNotReturn]` + `[MethodImpl(NoInlining)]` — JIT не инлайнит бросающий путь в горячие методы.
- `Throw<T>(code, args)` — то же, но возвращает `T`, чтобы использовать в выражениях: `return x ?? ThrowCompiler.Throw<Foo>(code)`. `[DoesNotReturn]` гарантирует, что компилятор не будет ругаться на «возврат без значения».
- `ValidateAllCodesCovered()` — extension-метод (C# 14 `extension`-блок) на `IReadOnlyDictionary<ErrorCode, string>`. Бросает `InvalidOperationException` при первом пропуске. Один `foreach`, без двойной итерации.
- `TryValidateAllCodesCovered(out ErrorCode code)` — non-throwing версия, тоже extension. Один `foreach`, `out`-параметр `code` корректно отделён от переменной цикла `c` (CS0136 исправлен).
- `_errorCodesValidateTable` — `Enum.GetValues<ErrorCode>()`, кэшируется один раз на процесс.

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
- Primary constructor (C# 12+) — лаконично, но нет конструктора с `innerException`. См. [Что исправить, п. 4](#что-исправить).
- Нет `[Serializable]`/`ISerializable` — в современном .NET это уже не требуется (binary formatter deprecated), но если планируете межпроцессную передачу — учтите.

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
templates.ValidateAllCodesCovered();

if (!templates.TryValidateAllCodesCovered(out var missing))
    Console.WriteLine($"Нет шаблона для {missing}");

DefaultErrorMessageProvider.DefaultTemplates.ValidateAllCodesCovered();
```

**`overrides` — учтите семантику «заменить», не «дополнить»:**

```csharp
// ⚠ Так вы потеряете ВСЕ встроенные шаблоны, кроме двух:
var p1 = new DefaultErrorMessageProvider(new()
{
    [ErrorCode.Lexer_UnexpectedCharacter] = "Custom 1",
    [ErrorCode.Parser_ExpectedToken]      = "Custom 2",
});
// p1.GetMessage(ErrorCode.CodeGen_UndefinedVariable, "x") → InvalidOperationException

// ✅ Если нужно «дополнить» — берите DefaultTemplates за основу:
var merged = new Dictionary<ErrorCode, string>(DefaultErrorMessageProvider.DefaultTemplates)
{
    [ErrorCode.Lexer_UnexpectedCharacter] = "Custom 1",
};
var p2 = new DefaultErrorMessageProvider(merged);
```

## Приближенность модуля к пользователю

Средняя. Для конечного пользователя (того, кто пишет на MiniC/NANASM) модуль не виден — ошибки приходят как `CodeException` с готовым сообщением. Для автора компилятора/ассемблера/расширения — это основной инструмент диагностики.

- Пользователь языка: видит `ex.Message` в консоли. Если язык локализован через `ThrowCompiler.ErrorMessageProvider` — видит на своём языке. Импортировать этот модуль ему не нужно.
- Автор компилятора (MiniC, NANASM, сторонние): использует `ThrowCompiler.Throw` + `ErrorCode`. Может подменить `IErrorMessageProvider`.
- Автор хост-приложения (IDE, REPL, CI): ловит `CodeException`, разбирает `ErrorCode`/`NumericCode`. Может установить свой провайдер, чтобы сообщения соответствовали его UX.
- Автор ядра — не должен использовать этот модуль для внутренних ошибок. Ядро работает с `BiosStatus`/`ProcessorFault` (`Kernel.Common`), а не с `CodeException`. `CodeException` — для ошибок компиляции/ассемблирования, не для runtime-fault'ов.

**Что стоит делать:**
- Бросать `ThrowCompiler.Throw` из frontend'а (Lexer/Parser/CodeGen) и assembler'а, а не `throw new Exception(...)`: это даёт стабильный `ErrorCode` для тестов.
- Проверять покрытие в CI: `templates.ValidateAllCodesCovered()` в тесте — гарантия, что новый `ErrorCode` получит шаблон.
- Устанавливать `ThrowCompiler.ErrorMessageProvider` один раз на старте приложения, до первого `Throw`.
- В тестах ловить `CodeException` и проверять `ex.ErrorCode`, а не `ex.Message` — сообщения могут локализоваться.
- Использовать `ThrowCompiler.Throw<T>` в выражениях (`??`, тернарниках) — это его основное назначение.

**Что не стоит делать:**
- Использовать `ThrowCompiler` в `Kernel`/`Contracts`/`Common` — там своя модель ошибок (`BiosStatus`).
- Менять `ErrorMessageProvider` в рантайме (между потоками) — это глобальный mutable static; race condition. Устанавливайте на старте.
- Кидать `CodeException` вручную (`new CodeException(...)`) — используйте `ThrowCompiler.Throw`, чтобы сообщение шло через провайдер.
- Передавать `overrides` в `DefaultErrorMessageProvider` и ждать «мержа» — сейчас это «замена».
- Полагаться на порядок аргументов без документации — сейчас порядок не задокументирован (см. ниже).

## Что исправлено в этой версии

По сравнению с прошлой фиксацией `Nantuc.Kernel.Diagnostics`:

- **CS0136 в `TryValidateAllCodesCovered`** — `out ErrorCode code` и лямбда `code => ...` больше не конфликтуют. Теперь один `foreach (var c in _errorCodesValidateTable)` с `code = c`. Модуль собирается.
- **`CodeExpection` / `CodeExepction` → `CodeException`.** Публичный контракт теперь читается правильно.
- **`TryGetTemplate` вызывается из `GetMessage`** (раньше был dead code). Override в наследниках теперь действительно работает.
- **`ThrowMiniC` → `ThrowCompiler`** (переименование класса) и **`ThrowMiniC` → `Throw`/`Throw<T>`** (переименование методов). Имена больше не привязаны к MiniC.
- **`ValidateAllCodesCovered` / `TryValidateAllCodesCovered`** — оба extension-метода в одном `extension`-блоке, двойная итерация (`Any` + `First`) убрана.
- **`ThrowCompiler.ErrorMessageProvider`** — null-check через `?? throw new ArgumentNullException`. Раньше можно было установить `null` и получить NRE в первом же `Throw`.
- **`DefaultErrorMessageProvider.DefaultTemplates`** — `FrozenDictionary`, а не `IReadOnlyDictionary` поверх мутабельного `Dictionary`. Кастовать обратно и изменить встроенные шаблоны нельзя.
- **`ErrorCode` диапазоны** — `CodeGen_BreakOutsideLoop`/`CodeGen_ContinueOutsideLoop` перенесены в 1314/1315 (правильный диапазон CodeGen). `Asm` больше не «загрязнён» чужими кодами.

## Особенная информация

### Модель ошибок: `ErrorCode` vs `BiosStatus`

Важно не путать два разных механизма ошибок в платформе:

| Механизм | Модуль | Когда | Что значит |
|---|---|---|---|
| `ErrorCode` + `CodeException` | `Kernel.Diagnostics` | компиляция/ассемблирование | «ваш исходник не компилируется» |
| `BiosStatus` + `ProcessorFault` | `Kernel.Common` | исполнение байткода | «ваша программа упала в рантайме» |

`Kernel.Diagnostics` не используется ядром (`Kernel`/`Contracts`/`Common`). Если вы пишете runtime-код и хотите сообщить об ошибке — используйте `BiosStatus`/`ProcessorFault`, а не `CodeException`.

### Стабильность кодов

- Числа `ErrorCode` — wire-format. Не меняйте их после релиза.
- `NumericCode` — для логов, CI-парсинга, внешних тулов. Если у вас есть внешняя система, которая парсит `error 1300: …` — она опирается на эти числа.
- Удаление `ErrorCode`-значения — breaking change. Помечайте `[Obsolete]`, но не удаляйте, пока не выйдет major.
- Шаблоны в `DefaultErrorMessageProvider.templates` — тоже контракт (порядок аргументов). Если меняете шаблон — либо сохраняйте порядок аргументов, либо документируйте как breaking.
- Диапазоны по подсистемам — соглашение, а не инвариант: `1000` — общие, `1100` — Lexer, `1200` — Parser, `1300` — CodeGen, `1400` — Asm. Свободные номера внутри диапазона можно занимать; переходить на чужой диапазон — нет.

### Глобальное состояние `ThrowCompiler.ErrorMessageProvider`

Это mutable static — единственная точка на весь процесс. Следствия:

- Не потокобезопасно при смене. Если один поток читает `ErrorMessageProvider`, а другой пишет — data race. Устанавливайте провайдер до запуска потоков (на старте приложения).
- Влияет на все компиляторы в процессе. Если у вас в одном процессе и MiniC, и NANASM, и сторонний плагин — все они получат один провайдер. Для мультиязычного хостинга (например, IDE с переключением языка) это ограничение.
- Тесты могут «протекать». Если один тест установил свой провайдер, а другой ожидает `Default` — второй упадёт. В тестах либо восстанавливайте провайдер в `finally`/`IDisposable`, либо запускайте в отдельном `AssemblyLoadContext`/процессе.

### `IErrorMessageProvider.Default` — почему `static` на интерфейсе

```csharp
static IErrorMessageProvider Default { get; } = new DefaultErrorMessageProvider();
```

Это C# 11+ static-член интерфейса с реализацией по умолчанию. Он не виртуален для реализаций (`Default` — это `static`, а не `static abstract`), поэтому `MyProvider.Default` вернёт тот же `DefaultErrorMessageProvider`, а не `MyProvider`. Это ожидаемо: `Default` — «что считается дефолтом в платформе», а не «дефолт для этого типа».

### `params ReadOnlySpan<object>` — особенности

- Требует C# 13+ (для `params` на `ReadOnlySpan<T>`) и `net9.0+` (для `string.Format(string, params ReadOnlySpan<object?>)`). На `net10.0` — работает.
- Для `object` (reference type): нулевой набор аргументов → `ReadOnlySpan<object>.Empty`, без аллокаций. Один и более → аллокация `object[]`. Для пути ошибки это ок.
- Boxing value-types: `ThrowCompiler.Throw(code, 42, line)` упакует `42` и `line` в `object` — две аллокации. Для ошибки неважно, но если модуль когда-нибудь начнут использовать для «горячих» warning'ов — учитывайте.
- Если у вызывающего уже есть `ReadOnlySpan<object>` — он может передать его напрямую, без обёртки.

### `field`-keyword в `ThrowCompiler.ErrorMessageProvider`

```csharp
public static IErrorMessageProvider ErrorMessageProvider
{
    get; set => field = value ?? throw new ArgumentNullException(nameof(value)); 
} = IErrorMessageProvider.Default;
```

`field` — C# 14. Может требовать `<LangVersion>preview</LangVersion>` в зависимости от версии SDK. Если у вас собирается без него — ок. Если нет — либо добавьте `preview`, либо используйте явное поле (см. [Что исправить, п. 5](#что-исправить)).

### Ограничения платформ

- `net10.0`. `params ReadOnlySpan<object>`, `FrozenDictionary`, `ReadOnlyDictionary`, `[DoesNotReturn]`, primary constructors, `extension`-блоки, `field`-keyword — всё есть. На `net8.0`/`net9.0` часть не соберётся.
- `unsafe` отсутствует — модуль безопасен для WASM/AOT и окружений с запретом `unsafe`.
- `FrozenDictionary` — в AOT работает, но инициализация чуть дороже, чем у обычного `Dictionary`. Один раз на процесс — норм.
- `Enum.GetValues<ErrorCode>()` — в AOT без `[DynamicDependency]` может быть вырезан триммером, если вы используете `ValidateAllCodesCovered`. Для `net10.0` с `PublishTrimmed=true` проверьте, что `ErrorCode` не стримится.
- `string.Format` с `ReadOnlySpan<object>` в AOT — работает, но `IFormattable`-форматирование аргументов идёт через reflection. Для ошибок это ок.
- Локализация — `DefaultErrorMessageProvider` не использует `CultureInfo.CurrentUICulture`; сообщения всегда на английском. Если нужна локализация через `ResourceManager` — реализуйте свой `IErrorMessageProvider`.

### Что этот модуль не делает

- Не форматирует ошибки в JSON/структурированный вид — только строка (`GetMessage`). Для структурированных логов используйте `ErrorCode`/`NumericCode` напрямую.
- Не содержит severity-уровней (Error/Warning/Info) — только ошибки. Если понадобятся предупреждения компилятора, это будет отдельный механизм.
- Не содержит source-позиций (файл, строка, столбец) как полей — они передаются аргументами в шаблон. Для IDE/CI, которым нужны структурированные позиции, придётся либо расширять `CodeException` (добавить `SourceSpan`), либо парсить сообщение. См. [Что исправить, п. 4](#что-исправить).
- Не знает про `IProcessorFaultPolicy`/`BiosStatus` — это runtime-модель, отдельно.
- Не зависит от других проектов Nantuc — можно подключать в любой .NET-проект как «Nantuc-style diagnostics».

Правило «куда положить тип»: код ошибки/исключение/провайдер сообщений → `Kernel.Diagnostics`; runtime-статус → `Kernel.Common`; контракт → `Kernel.Contracts`; реализация исполнения → `Kernel`.

## Что исправить

По убыванию важности. Ссылки на файлы и методы. Пункты, помеченные **[XML]**, — приоритет для XML-документации (сейчас `///` отсутствует; вы отметили, что работа идёт, поэтому это не «баги», а приоритеты).

1. **`overrides` в `DefaultErrorMessageProvider` заменяет, а не мержит.**  
   Файл: `DefaultErrorMessageProvider.cs`, поле `_templates`:
   ```csharp
   private readonly ReadOnlyDictionary<ErrorCode, string> _templates =
       overrides != null ? overrides.AsReadOnly() : templates.AsReadOnly();
   ```
   Если пользователь передал словарь с одним сообщением, он теряет все 30+ встроенных. `ValidateAllCodesCovered` на таком «override»-словаре упадёт на первом же коде, которого в нём нет — а он и не должен быть.  
   Фикс: мержить поверх встроенных:
   ```csharp
   private static ReadOnlyDictionary<ErrorCode, string> Merge(Dictionary<ErrorCode, string>? overrides)
   {
       if (overrides is null) return templates.AsReadOnly();
       var merged = new Dictionary<ErrorCode, string>(templates);
       foreach (var (k, v) in overrides) merged[k] = v;
       return merged.AsReadOnly();
   }
   ```
   Если намеренно хотите «replace» — переименуйте параметр в `replaceAll`/`templates` и задокументируйте. Сейчас имя вводит в заблуждение (это уже отмечено в [Приближенность](#приближенность-модуля-к-пользователю) и в примерах).

2. **`CodeException` без `innerException`.**  
   Файл: `CodeException.cs`, primary constructor `(ErrorCode errorCode, string message)`.  
   Нет способа обернуть `IOException`/`FormatException`/`CodeException` из вложенного вызова в `CodeException`. Кодогенератор и парсер часто ловят низкоуровневые исключения и хотят обернуть их в диагностику с кодом. Primary constructor мешает — добавьте вторичный:
   ```csharp
   public CodeException(ErrorCode errorCode, string message, Exception inner)
       : base(message, inner) { ErrorCode = errorCode; }
   ```
   И, возможно, `ThrowCompiler.Throw(code, inner, args)`.

3. **Порядок аргументов шаблонов нигде не задокументирован. [XML]**  
   Файл: `DefaultErrorMessageProvider.cs`, `templates` — единственный источник. Автор своего провайдера вынужден читать исходник, чтобы узнать, что `Lexer_UnexpectedCharacter` ждёт `(char, int line, int col)`.  
   Фикс: либо `docs/error-codes.md` с таблицей «код → аргументы → шаблон», либо комментарии `// {0}=char, {1}=line, {2}=col` рядом с каждым шаблоном, либо `///` на каждом члене `ErrorCode` с описанием смысла (даже без порядка аргументов — уже лучше, чем ничего).

4. **`ErrorCode` — нет XML-документации. [XML]**  
   Файл: `ErrorCode.cs`. `NotSupported` vs `NotImplemented` — в чём разница? `Lexer_InvalidNumber` vs `Parser_UnknownType` — понятно по именам, но не по контракту. `CodeGen_*` — 16 кодов, из них `CodeGen_InvalidDotAccess` и `CodeGen_NonStructAccess` легко перепутать.  
   Фикс: `/// <summary>` на каждый член (или хотя бы на группы через `<remarks>` на enum). Это самый высокий приоритет среди XML-документации после п. 3.

5. **`field`-keyword в `ThrowCompiler.ErrorMessageProvider` может требовать `<LangVersion>preview</LangVersion>`.**  
   Файл: `ThrowCompiler.cs`, свойство `ErrorMessageProvider`. В `.csproj` нет `LangVersion`. Если у вас собирается без него — ок. Если нет — либо добавьте `preview`, либо используйте явное поле:
   ```csharp
   private static IErrorMessageProvider _provider = IErrorMessageProvider.Default;
   public static IErrorMessageProvider ErrorMessageProvider
   {
       get => _provider;
       set => _provider = value ?? throw new ArgumentNullException(nameof(value));
   }
   ```
   Второй вариант ещё и читается проще.

6. **`Throw<T>` — `[DoesNotReturn]` на generic-версии не работает для value-type в выражениях.**  
   Файл: `ThrowCompiler.cs`, `Throw<T>(...)`.  
   `[DoesNotReturn]` корректен, но компилятор не выведет non-null из него для `T`, и `x ?? Throw<int>(...)` не скомпилируется (нельзя `??` на `int`). Это не баг, но пользователи могут удивиться.  
   Рекомендация: либо ограничить `where T : class`, либо задокументировать в `///`, что generic-версия — для reference-type сценариев.

7. **`ThrowCompiler` — имя грамматически странное («throw compiler»?). [XML]**  
   Файл: `ThrowCompiler.cs`. Лучше `ThrowDiagnostics` (совпадает с именем модуля), `Diagnostics.Throw(...)` или просто `Throw`. Но это вкусовщина; если имя уже используется в компиляторе — оставьте, добавьте `[Obsolete]`-алиас, когда/если переименуете.

8. **`IErrorMessageProvider`, `ThrowCompiler`, `CodeException`, `DefaultErrorMessageProvider` — без `///`. [XML]**  
   Файлы: `IErrorMessageProvider.cs`, `ThrowCompiler.cs`, `CodeException.cs`, `DefaultErrorMessageProvider.cs`.  
   Публичные контракты, которые читают авторы компиляторов и IDE. `///` на `Throw`/`Throw<T>`/`ValidateAllCodesCovered`/`TryValidateAllCodesCovered`/`GetMessage`/`TryGetTemplate`/`CodeException.ErrorCode`/`NumericCode`.  
   Рекомендация: включить `GenerateDocumentationFile=true` в `.csproj` и добить пропуски.

9. **Валидация покрытия не выполняется автоматически при старте.**  
   Файл: `DefaultErrorMessageProvider.cs`, статический конструктор отсутствует. При добавлении нового кода в `ErrorCode` и забывании шаблона ошибка вылезет в рантайме (`InvalidOperationException` из `GetMessage`), а не в CI.  
   Рекомендация: тест `AllErrorCodes_HaveTemplates` в `Nantuc.Kernel.Diagnostics.Tests` (самый безопасный вариант) или `static DefaultErrorMessageProvider() { templates.ValidateAllCodesCovered(); }` (упадёт при первом использовании, но до этого — тишина). Тест предпочтительнее.

10. **`DefaultTemplates` — `FrozenDictionary`, extension-методы объявлены на `IReadOnlyDictionary<ErrorCode, string>`.**  
    Файл: `ThrowCompiler.cs`, extension на `IReadOnlyDictionary<ErrorCode, string>`.  
    `FrozenDictionary` реализует `IReadOnlyDictionary`, так что `DefaultErrorMessageProvider.DefaultTemplates.ValidateAllCodesCovered()` компилируется. Но если когда-нибудь поменяете `DefaultTemplates` на конкретный тип без `IReadOnlyDictionary` — extension «отвалится». Не критично, просто держите в голове.

11. **`IErrorMessageProvider.GetMessage` — контракт для неизвестного кода не задокументирован. [XML]**  
    Файл: `IErrorMessageProvider.cs`. `DefaultErrorMessageProvider` бросает `InvalidOperationException`, но кастомный провайдер вправе вернуть `$"Error {(int)code}"`. Без `///` читатель не знает, что обязательно, а что — рекомендация.  
    Рекомендация: `/// <remarks>` на `GetMessage`: «Implementation may throw for unknown codes, or return a fallback string; contract does not require either».

12. **`ValidateAllCodesCovered` / `TryValidateAllCodesCovered` — стали extension-методами.**  
    Файл: `ThrowCompiler.cs`, `extension(IReadOnlyDictionary<ErrorCode, string> templates)`.  
    В предыдущей версии это были `static`-методы `ThrowHelper.ValidateAllCodesCovered(dict)`. Теперь вызов `dict.ValidateAllCodesCovered()`. Это breaking change для тех, кто уже вызывал. Если модуль ещё не в релизе — норм; если что-то внешнее использует — оставьте static-алиас на одну версию с `[Obsolete]`.

13. **`CodeException` — нет `SourceSpan`/`FilePath`/`Line`/`Column` как полей. [XML]**  
    Файл: `CodeException.cs`. Позиции передаются аргументами в шаблон, но не хранятся в исключении. Для IDE/CI, которым нужны структурированные позиции, придётся парсить `Message`.  
    Рекомендация: либо добавить `public string? FilePath { get; }`, `public int? Line { get; }`, `public int? Column { get; }` (breaking change для конструктора — но можно через отдельный конструктор с опциональными параметрами), либо задокументировать в `///` на `CodeException`, что позиции — только в `Message` (формат зависит от провайдера).

14. **Тестов нет.**  
    Самый дешёвый и ценный набор:
    - `DefaultErrorMessageProvider`: каждый `ErrorCode` из `Enum.GetValues<ErrorCode>()` → `GetMessage` не падает (это же и есть п. 9).
    - `TryValidateAllCodesCovered`: пустой словарь → `false` + `out` не `default`; полный → `true`.
    - `ValidateAllCodesCovered`: пустой → `InvalidOperationException` с правильным кодом в сообщении.
    - `ThrowCompiler.Throw`: бросает `CodeException` с ожидаемым `ErrorCode`/`Message`.
    - `ThrowCompiler.Throw<T>`: компилируется в выражении (`return x ?? ThrowCompiler.Throw<Foo>(...)`).
    - `ThrowCompiler.ErrorMessageProvider = null` → `ArgumentNullException`.
    - Подмена провайдера работает: установить свой, проверить, что `Message` из него; восстановить в `finally`.
    - `TryGetTemplate` override работает: наследник `DefaultErrorMessageProvider` с override → `GetMessage` использует его.
    - `CodeException.NumericCode == (int)ErrorCode`.
    - `overrides`-поведение (когда исправите п. 1 — «мерж»; сейчас — «replace»).