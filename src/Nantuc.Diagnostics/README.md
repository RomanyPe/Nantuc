# Nantuc.Diagnostics — README

> General diagnostics module: error codes, message templates, `ThrowCompiler`, `CodeException`, and typed diagnostic arguments `DiagnosticData`.
> Extracted from the kernel so that compiler and extension authors can use a unified error mechanism and replace the message provider without touching `Nantuc.Kernel`.
> `[← Root](../README.md) · [Common](../../src/Nantuc.Common/README.md) · [Contracts](../../src/Nantuc.Contracts/README.md) · [Kernel](../../src/Nantuc.Kernel/README.md) · [Compiler](../../src/Nantuc.Compiler/README.md)`

## Table of contents
- [About this module](#about-this-module)
- [Local project tree](#local-project-tree)
- [API](#api)
- [API examples](#api-examples)
- [How close the module is to the user](#how-close-the-module-is-to-the-user)
- [What was fixed in this version](#what-was-fixed-in-this-version)
- [Special information](#special-information)
  - [`DiagnosticData` and AOT](#diagnosticdata-and-aot) **[NEW]**
- [What to fix](#what-to-fix)

## About this module

`Nantuc.Diagnostics` is a standalone diagnostics module that serves the MiniC compiler, the NANASM assembler, and potentially any other backends/extensions. Despite `Kernel` in the namespace name, this project does not depend on any other Nantuc project — there is not a single `ProjectReference` in the `.csproj`. This is intentional: a compiler author should not have to pull in the kernel (`Processor`, `MemoryBus`, native buffers, unsafe) just to throw meaningful errors with codes.

Key roles:
- `ErrorCode` — a single enum of error codes, grouped by subsystem (Lexer, Parser, CodeGen, Asm).
- `IErrorMessageProvider` — an extension point for localization and message customization; contains `static Default`.
- `DefaultErrorMessageProvider` — built-in provider with English templates; `overrides` are **merged** on top of the standard templates.
- `DiagnosticData` — **typed diagnostic argument** (replaces `object`); AOT-safe, no reflection, no boxing. **[NEW]**
- `ThrowCompiler` — an **instance class** (not static) for throwing errors; stores `ErrorMessageProvider` as an instance field, no global mutable state. **[BREAKING]**
- `CodeException` — an `Exception` with `ErrorCode` and `NumericCode`.

The module's philosophy: an error code is a stable contract (a number, not a string), a message is a replaceable resource (a template, not hardcoded), and arguments are typed `DiagnosticData` (not `object`). This enables:
- Testing the compiler by `ErrorCode` without parsing strings.
- Localizing messages without rebuilding the 
- Giving the end user Russian/English/any language through their own `IErrorMessageProvider`.
- Guaranteeing AOT compatibility at the level of diagnostic arguments (no reflection, no boxing of enums).

> **XML documentation has been expanded.** `ErrorCode` (every member), `ThrowCompiler` (class and methods), `DiagnosticData` (partially), `DefaultErrorMessageProvider` (class and public members), `CodeException` (class and members) are fully documented. Without `///` — `IErrorMessageProvider.GetMessage` (only `<summary>`), `TryGetTemplate`. Details — in [What to fix](#what-to-fix).

## Local project tree

All types are in the namespace `Nantuc.Diagnostics`, the structure is flat:

```
Nantuc.Diagnostics/
├── Nantuc.Diagnostics.csproj   # net10.0, no ProjectReference
├── CodeException.cs                   # class : Exception, with ErrorCode/NumericCode
├── DefaultErrorMessageProvider.cs     # FrozenDictionary of templates + overrides (merge) + TryGetTemplate
├── DiagnosticData.cs                  # readonly record struct — typed diagnostic argument [NEW]
├── ErrorCode.cs                       # enum ErrorCode (ranges 1000/1100/1200/1300/1400)
├── IErrorMessageProvider.cs           # interface + static Default
└── ThrowCompiler.cs                   # class (instance): Throw/Throw<T> + ErrorMessageProvider
```

## API

### Overall picture

| Type | Kind | Default implementation | Purpose |
|---|---|---|---|
| `ErrorCode` | `enum` | — | Stable numeric error codes |
| `IErrorMessageProvider` | `interface` | **yes, via `static Default`** | Formatting a message by code and typed arguments |
| `DefaultErrorMessageProvider` | `class` | — | Built-in English templates + `overrides` (merge) + `TryGetTemplate` |
| **`DiagnosticData`** | `readonly record struct` | — | **Typed diagnostic argument (replaces `object`)** **[NEW]** |
| **`ThrowCompiler`** | **`class`** (not `static`) **[BREAKING]** | — | Instance facade for throwing `CodeException` |
| `CodeException` | `class : Exception` | — | Exception with `ErrorCode` and `NumericCode` |

**A default implementation of the interface exists only for `IErrorMessageProvider`** — via `static IErrorMessageProvider Default { get; } = new DefaultErrorMessageProvider();`. This is consistent with `IProcessorPolicy.Default` in `Contracts` and provides a unified pattern for "where the default lives" — on the interface itself, not on `ThrowCompiler`.

### `ErrorCode` — a single enum

Grouped by subsystem, numeric ranges:

| Range | Subsystem | Codes |
|---|---|---|
| 1000–1001 | General | `NotSupported`, `NotImplemented` |
| 1100–1102 | Lexer | `Lexer_UnexpectedCharacter`, `Lexer_InvalidNumber`, `Lexer_UnterminatedString` |
| 1200–1208 | Parser | `Parser_UnknownType`, `Parser_ExpectedToken`, `Parser_UnexpectedToken`, `Parser_InvalidAssignment`, `Parser_ArraySizeNotConstant`, `Parser_UnknownDirective`, `Parser_StructNotDefined`, `Parser_InvalidCharLiteral`, `Parser_IncludeAmbiguous` |
| 1300–1317 | CodeGen | `CodeGen_UnknownTypeSize` (1300) … `CodeGen_DirectStructLoadStore` (1313), `CodeGen_BreakOutsideLoop` (1314), `CodeGen_ContinueOutsideLoop` (1315), **`CodeGen_AssignmentToConst` (1316)**, **`CodeGen_IncrementOfConst` (1317)** **[NEW]** |
| 1400–1404 | Asm | `Asm_UndefinedLabel`, `Asm_DuplicateLabel`, `Asm_InvalidInstruction`, `Asm_UnknownRegister`, `Asm_UnknownMnemonic` |

Rule "an error code is a contract": **do not change the numbers of existing codes** after release. Compiler tests and external tools will rely on them. New codes — only with a new number.

### `IErrorMessageProvider` — extension point

```csharp
public interface IErrorMessageProvider
{
    static IErrorMessageProvider Default { get; } = new DefaultErrorMessageProvider();
    string GetMessage(ErrorCode code, params ReadOnlySpan<DiagnosticData> args);  // [BREAKING]
}
```

- `static Default` — a single point for "what the default provider is". `ThrowCompiler` is initialized through it, rather than creating `new DefaultErrorMessageProvider()` itself.
- `params ReadOnlySpan<DiagnosticData>` — C# 13+ (`params` for `ReadOnlySpan`). For `DiagnosticData` (a value type), an empty set → `ReadOnlySpan<DiagnosticData>.Empty`, no allocations; 1+ arguments — allocation of `DiagnosticData[]` (not `object[]`, no boxing). **[NEW]**
- The contract does not require throwing from `GetMessage` for an unknown code — the implementation decides. `DefaultErrorMessageProvider` throws `InvalidOperationException`, but a custom provider is free to return `$"Error {(int)code}"`.

### `DiagnosticData` — typed diagnostic argument **[NEW]**

```csharp
public readonly record struct DiagnosticData
{
    public string Text { get; }
    public DiagnosticData(string? text);
    public override string ToString() => Text;

    // Factories for types outside the implicit conversions
    public static DiagnosticData Of(bool value);
    public static DiagnosticData Of<T>(T? value) where T : IBinaryNumber<T> 
        => new(value?.ToString());
    public static DiagnosticData Of(string value) => new(value ?? string.Empty);

    // Implicit conversions for common types
    public static implicit operator DiagnosticData(string? text);
    public static implicit operator DiagnosticData(char value);
    public static implicit operator DiagnosticData(int value);
    public static implicit operator DiagnosticData(uint value);
    public static implicit operator DiagnosticData(long value);
    public static implicit operator DiagnosticData(ulong value);
}
```

- Stores a **ready-made string** (`Text`) built at creation time. No `string.Format(object[])`, no virtual `ToString()` at format time, no boxing of int/char/enum.
- **Implicit conversions** — for `string`, `char`, `int`, `uint`, `long`, `ulong`. Convenient: `ThrowCompiler.Throw(code, "x", 42)` works without explicit `new DiagnosticData(...)`.
- **Explicit factories** — `Of(bool)` (otherwise `bool` → `int` via implicit conversions would give `"0"`/`"1"`), `Of<T>(T?)` (fallback via `ToString()`, **only for your own types** — see [Special information](#diagnosticdata-and-aot)).
- **`implicit operator DiagnosticData(Enum)` is intentionally absent** — it would lead to `Enum.ToString()` (reflection-adjacent, AOT risk). For enums, use explicit switch helpers (e.g., `TokenTypeExtension.AsText` in the compiler). **[AOT]**

**Convention:** a `DiagnosticData` argument for an enum must arrive either as a `string` or as a `DiagnosticData` built without `Enum.ToString()`. For `char`/`int`/`uint`/`long`/`ulong`, the implicit operators produce `InvariantCulture.ToString()` — without reflection.

### `DefaultErrorMessageProvider` — built-in templates

```csharp
public class DefaultErrorMessageProvider(
    FrozenDictionary<ErrorCode, string>? overrides = null)  // [BREAKING] FrozenDictionary, not Dictionary
    : IErrorMessageProvider
{
    public static FrozenDictionary<ErrorCode, string> DefaultTemplates { get; }
    public IReadOnlyDictionary<ErrorCode, string> Templates { get; }

    protected virtual string? TryGetTemplate(ErrorCode code);
    public string GetMessage(ErrorCode code, params ReadOnlySpan<DiagnosticData> args);
}
```

- Default templates — a `FrozenDictionary<ErrorCode, string>`, read-only after initialization. `DefaultTemplates` returns exactly a `FrozenDictionary`, so it cannot be cast back to a mutable `Dictionary`.
- `Templates` — `IReadOnlyDictionary` (internally a `ReadOnlyDictionary<,>`), the actual templates **of this instance** (standard + `overrides`).
- `TryGetTemplate` — a `protected virtual` extension point. `GetMessage` actually calls `TryGetTemplate`, so overriding works.
- **`overrides` are now merged on top of the standard ones** (previously they replaced them). Matching keys are overwritten, the rest are inherited from the default set. `MergeTemplates` copies `templates` into a new `Dictionary`, then overlays `overrides`. **[FIX]**
- `string.Format(template, parts)` — where `parts` is a `string[]` built from `args[i].Text`. `string[]` is covariant with `object[]`, so `string.Format` receives the same array without a copy. No boxing: the elements are already strings.
- Messages are in English. Localization — by replacing `IErrorMessageProvider`, not by editing this class.

Template examples:

| Code | Template | Arguments (`DiagnosticData`) |
|---|---|---|
| `Lexer_UnexpectedCharacter` | `"Unexpected character '{0}' at line {1}, column {2}"` | `char`, `int line`, `int col` |
| `Parser_ExpectedToken` | `"Expected {0}, got {1} at {2}:{3}"` | `string expected`, `string got`, `int line`, `int col` |
| `CodeGen_UnknownField` | `"Field '{0}' not found in struct '{1}'"` | `string field`, `string struct` |
| `Asm_DuplicateLabel` | `"Label '{0}' already defined at position {1}"` | `string label`, `int pos` |
| `CodeGen_BreakOutsideLoop` | `"Operator \"break\" outside loop"` | — |
| **`CodeGen_AssignmentToConst`** | **`"Cannot assign to const '{0}'"`** | **`string varName`** **[NEW]** |
| **`CodeGen_IncrementOfConst`** | **`"Cannot increment/decrement const '{0}'"`** | **`string varName`** **[NEW]** |

The argument order is not documented anywhere except the code (`DefaultErrorMessageProvider.templates`). See [What to fix, item 3](#what-to-fix).

### `ThrowCompiler` — instance facade **[BREAKING]**

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

- **Not a `static class`.** `ThrowCompiler` is now an instance class. Each compiler/assembler creates its own instance: `new ThrowCompiler(provider)` and passes it as a dependency.
- `ErrorMessageProvider` — an **instance field** (not a static property). There is no global mutable state. This eliminates the previous thread-safety issue with replacing the provider at runtime.
- **`provider == null`** → uses `IErrorMessageProvider.Default`.
- `Throw(code, args)` — throws `CodeException(code, provider.GetMessage(code, args))`. `[DoesNotReturn]` + `[MethodImpl(NoInlining)]` — the JIT does not inline the throwing path.
- `Throw<T>(code, args)` — same, but returns `T` so it can be used in expressions: `return x ?? thrower.Throw<Foo>(code)`. `[DoesNotReturn]` guarantees that the compiler will not complain about "not all code paths return a value".
- **Extension methods `ValidateAllCodesCovered`/`TryValidateAllCodesCovered` removed.** Coverage validation is now the job of tests (see [What to fix, item 9](#what-to-fix)). **[BREAKING]**

### `CodeException` — exception

```csharp
public class CodeException(ErrorCode errorCode, string message) : Exception(message)
{
    public ErrorCode ErrorCode { get; }
    public int       NumericCode => (int)ErrorCode;
}
```

- Inherits `Exception`, `Message` — the formatted message.
- `ErrorCode` — typed code for `catch`/logic. `NumericCode` — for logs and external systems.
- Primary constructor (C# 12+) — concise, but there is no constructor with `innerException`. See [What to fix, item 2](#what-to-fix).
- No `[Serializable]`/`ISerializable` — in modern .NET this is no longer required.

## API examples

**Creating an instance and basic throwing:**

```csharp
using Nantuc.Diagnostics;

// One instance per compiler/parser/assembler
var thrower = new ThrowCompiler(provider: null);   // → IErrorMessageProvider.Default

if (current is null)
    thrower.Throw(ErrorCode.Lexer_UnexpectedCharacter, c, line, col);
// the compiler knows the code after this is unreachable (DoesNotReturn)
```

**Use in an expression (`Throw<T>`):**

```csharp
Foo foo = MaybeGet() ?? thrower.Throw<Foo>(
    ErrorCode.CodeGen_UndefinedVariable, variableName);
```

**Catching by code:**

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

**Custom provider (Russian messages):**

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

**Inheriting from `DefaultErrorMessageProvider` (overriding a couple of templates):**

```csharp
public sealed class MyProvider : DefaultErrorMessageProvider
{
    // TryGetTemplate is actually called from GetMessage — the override works
    protected override string? TryGetTemplate(ErrorCode code) => code switch
    {
        ErrorCode.Parser_ExpectedToken => "Ожидался {0}, получен {1} ({2}:{3})",
        _ => base.TryGetTemplate(code),   // fallback to the built-in English
    };
}

var thrower = new ThrowCompiler(new MyProvider());
```

**`overrides` — now a merge, not a replacement:** **[FIX]**

```csharp
// Only targeted overrides — all other codes come from the default set
var p = new DefaultErrorMessageProvider(new Dictionary<ErrorCode, string>
{
    [ErrorCode.Lexer_UnexpectedCharacter] = "Custom 1",
    [ErrorCode.Parser_ExpectedToken]      = "Custom 2",
}.ToFrozenDictionary());

// Works for codes that are not in overrides too:
p.GetMessage(ErrorCode.CodeGen_UndefinedVariable, "x");
// → "Undefined variable 'x'" (from the standard templates)
```

**Provider isolation between instances:** **[BREAKING]**

```csharp
var throwerA = new ThrowCompiler(new RussianMessageProvider());
var throwerB = new ThrowCompiler(provider: null);   // → Default (English)

// No global state: A and B do not affect each other.
```

## How close the module is to the user

Medium. For the end user (the one who writes in MiniC/NANASM) the module is invisible — errors arrive as `CodeException` with a ready message. For the compiler/assembler/extension author, it is the main diagnostic tool.

- Language user: sees `ex.Message` in the console. If the language is localized via `thrower.ErrorMessageProvider` — sees it in their language. They do not need to import this module.
- Compiler author (MiniC, NANASM, third-party): creates `ThrowCompiler` with the desired provider and uses `thrower.Throw` + `ErrorCode`. Can replace the provider via the constructor.
- Host application author (IDE, REPL, CI): catches `CodeException`, parses `ErrorCode`/`NumericCode`. Can create a `ThrowCompiler` with their own provider per request/session.
- Kernel author — should not use this module for internal errors. The kernel works with `BiosStatus`/`ProcessorFault` (`Common`), not with `CodeException`.

**What you should do:**
- Throw via `thrower.Throw` from the frontend (Lexer/Parser/CodeGen) and assembler, not `throw new Exception(...)`: this gives a stable `ErrorCode` for tests.
- **Create `ThrowCompiler` once per compiler/session and pass it as a dependency** (constructor injection). The compiler already does this: `FunctionGenerator(asm, structTable, ThrowCompiler)`.
- In tests, catch `CodeException` and check `ex.ErrorCode`, not `ex.Message` — messages may be localized.
- Use `thrower.Throw<T>` in expressions (`??`, ternaries) — that is its primary purpose.
- Pass `DiagnosticData`-compatible types (`string`, `char`, `int`, `uint`, `long`, `ulong`) as arguments — the implicit conversions will work.

**What you should not do:**
- Use `ThrowCompiler` in `Kernel`/`Contracts`/`Common` — they have their own error model (`BiosStatus`).
- Rely on global mutable state for the provider — it no longer exists; create a new `ThrowCompiler` when needed.
- Throw `CodeException` manually (`new CodeException(...)`) — use `thrower.Throw` so the message goes through the provider.
- Pass `object` as arguments (boxing enums/structs) — use `DiagnosticData` to preserve AOT compatibility.
- Rely on `Enum.ToString()` for arguments — use explicit switch helpers (e.g., `TokenTypeExtension.AsText` in the compiler).

## What was fixed in this version

Compared to the previous snapshot of `Nantuc.Diagnostics`:

- **`ThrowCompiler` — instance, not `static`.** **[BREAKING]** Removed the global mutable `ErrorMessageProvider` static field. Now each compiler has its own provider; thread safety is by construction. `Throw`/`Throw<T>` are instance methods.
- **`DiagnosticData` — typed arguments instead of `object`.** **[BREAKING]** `GetMessage(ErrorCode, params ReadOnlySpan<DiagnosticData>)`. Removed boxing of `int`/`char`/enum, removed reflection at format time. The `DiagnosticData(string? text)` argument builds the string at creation time.
- **`implicit operator DiagnosticData(Enum)` is intentionally absent** — it would lead to `Enum.ToString()` (AOT risk). Enums must arrive as `string` or via explicit switch helpers.
- **`overrides` in `DefaultErrorMessageProvider` are now merged, not replacing.** **[FIX]** `MergeTemplates(templates, overrides)` copies the standard templates and overlays `overrides`. Previously, passing a single custom message cut off all built-in ones — fixed.
- **`overrides` accepts `FrozenDictionary<ErrorCode, string>` instead of `Dictionary`.** **[BREAKING]** A guarantee that the passed dictionary will not be mutated after construction.
- **`ErrorCode` received XML documentation for every member.** Fully, including `<remarks>` for `CodeGen_AssignmentToConst`/`CodeGen_IncrementOfConst` listing the triggers. **[FIX]**
- **New `ErrorCode.CodeGen_AssignmentToConst = 1316` and `CodeGen_IncrementOfConst = 1317`** — for `const` violations. With templates in `DefaultErrorMessageProvider`. **[NEW]**
- **`ValidateAllCodesCovered`/`TryValidateAllCodesCovered` removed.** **[BREAKING]** Coverage validation is the job of tests, not a runtime API. See [What to fix, item 9](#what-to-fix).
- **`ThrowCompiler` XML documentation added.** `<summary>`, `<param>`, `<remarks>`, `<exception>` on the class and both `Throw`/`Throw<T>`. **[FIX]**
- **`DefaultErrorMessageProvider` XML documentation added.** `<summary>` explicitly mentioning the merge, `<param>` on `overrides`. **[FIX]**
- **`CodeException` XML documentation added** (with typos `ошиибки`/`ввиде` — see [What to fix, item 14](#what-to-fix)).
- **`params ReadOnlySpan<DiagnosticData>`** — for `DiagnosticData` (a value type), an empty set → `ReadOnlySpan.Empty`, no allocations. The `string[]` for `string.Format` is covariant with `object[]`, no copy.

## Special information

### Error model: `ErrorCode` vs `BiosStatus`

| Mechanism | Module | When | What it means |
|---|---|---|---|
| `ErrorCode` + `CodeException` | `Diagnostics` | compilation/assembly | "your source does not compile" |
| `BiosStatus` + `ProcessorFault` | `Common` | bytecode execution | "your program crashed at runtime" |

`Diagnostics` is not used by the kernel (`Kernel`/`Contracts`/`Common`). If you write runtime code and want to report an error — use `BiosStatus`/`ProcessorFault`, not `CodeException`.

### Code stability

- `ErrorCode` numbers are a wire format. Do not change them after release.
- `NumericCode` — for logs, CI parsing, external tools.
- Removing an `ErrorCode` value is a breaking change. Mark it `[Obsolete]` but do not remove it until a major release.
- The templates in `DefaultErrorMessageProvider.templates` are also a contract (argument order). If you change a template — either preserve the argument order or document it as breaking.
- Ranges by subsystem are a convention: `1000` — general, `1100` — Lexer, `1200` — Parser, `1300` — CodeGen, `1400` — Asm. Free numbers within a range can be used; moving into someone else's range — not.

### `IErrorMessageProvider.Default` — why `static` on the interface

```csharp
static IErrorMessageProvider Default { get; } = new DefaultErrorMessageProvider();
```

This is a C# 11+ static interface member with a default implementation. It is not virtual for implementations (`Default` is `static`, not `static abstract`), so `MyProvider.Default` will return the same `DefaultErrorMessageProvider`, not `MyProvider`. This is expected: `Default` is "what is considered the default in the platform", not "the default for this type".

### `DiagnosticData` and AOT **[NEW]**

`DiagnosticData` is designed so that the entire diagnostic argument path is AOT-safe:

| Mechanism | AOT risk | What to do |
|---|---|---|
| `implicit operator DiagnosticData(string?)` | no | — |
| `implicit operator DiagnosticData(char)` | no (`char.ToString()` — BCL, no reflection) | — |
| `implicit operator DiagnosticData(int/uint/long/ulong)` | no (`IFormattable`/`ToString(InvariantCulture)`, no reflection) | — |
| `Of(bool)` | no | — |
| `Of<T>(T?)` | **yes**, if `T` is a reference type without a `ToString()` override → will call `Object.ToString()` → type metadata | Use only for known types (`Type`, `Enum`, your own classes with `ToString()`) |
| `implicit operator DiagnosticData(Enum)` | **does not exist intentionally** — would lead to `Enum.ToString()` (reflection-adjacent) | Pass the enum as a `string` via an explicit switch helper |
| `string.Format(template, string[])` | no (the elements are already strings, no `IFormattable` path) | — |
| `$"No message template for error code {code}"` in `GetMessage` | **yes**: `ErrorCode.ToString()` → reflection-adjacent | Only a fallback path for a forgotten template; for AOT purity — replace with `((int)code).ToString(InvariantCulture)` |

**Bottom line:** `DiagnosticData` is fully AOT-safe for all typical calls (`string`, `char`, `int`, `uint`, `long`, `ulong`, `bool`). For `Of<T>` and `Enum` — the convention "pass a ready-made string" applies.

### `params ReadOnlySpan<DiagnosticData>` — specifics

- Requires C# 13+ (for `params` on `ReadOnlySpan<T>`) and `net9.0+` (for `string.Format(string, params ReadOnlySpan<object?>)` — but here `string.Format(template, string[])` is used, which exists earlier). On `net10.0` — works.
- `DiagnosticData` is a `readonly record struct`, a value type: an empty set of arguments → `ReadOnlySpan<DiagnosticData>.Empty`, no allocations. One or more → allocation of `DiagnosticData[]` (not `object[]`, **no boxing** of int/char).
- `DiagnosticData` contains a `string Text` (reference), so the internal strings are boxed only on `new DiagnosticData(someInt.ToString())` — this is unavoidable for a string representation. But there is no boxing of the `int`/`char` itself into `object`.
- If the caller already has a `ReadOnlySpan<DiagnosticData>` — they can pass it directly, without a wrapper.

### Platform limitations

- `net10.0`. `params ReadOnlySpan<DiagnosticData>`, `FrozenDictionary`, `ReadOnlyDictionary`, `[DoesNotReturn]`, primary constructors, `readonly record struct` — all present. On `net8.0`/`net9.0` some parts will not build.
- No `unsafe` — the module is safe for WASM/AOT and environments where `unsafe` is prohibited.
- `FrozenDictionary` — works in AOT, but initialization is slightly more expensive than a regular `Dictionary`. Once per process — fine.
- `Enum.GetValues<ErrorCode>()` (was in the removed `ValidateAllCodesCovered`) — in AOT without `[DynamicDependency]` it may be trimmed away. It is not used now; if you bring back validation — keep this in mind.
- Localization — `DefaultErrorMessageProvider` does not use `CultureInfo.CurrentUICulture`; messages are always in English. If you need localization via `ResourceManager` — implement your own `IErrorMessageProvider`.
- **`ThrowCompiler` — instance:** for WASM/AOT this is even better than static mutable state: there is no global field that needs to be synchronized between requests.

### What this module does not do

- It does not format errors into JSON/structured form — only a string (`GetMessage`).
- It does not contain severity levels (Error/Warning/Info) — only errors.
- It does not contain source positions (file, line, column) as fields — they are passed as arguments to the template.
- It knows nothing about `IProcessorPolicy`/`BiosStatus` — that is the runtime model, separate.
- It does not depend on other Nantuc projects — it can be plugged into any .NET project as "Nantuc-style diagnostics".

Rule for "where to put a type": error code/exception/message provider/diagnostic argument → `Diagnostics`; runtime status → `Common`; contract → `Contracts`; execution implementation → `Kernel`.