# Compiler Error Codes

> Reference for `ErrorCode` (`Nantuc.Kernel.Diagnostics`).
> Numeric values are a **stable contract**: they appear in logs, telemetry,
> JSON, and tests. Existing values must not be changed; new ones may only be
> added at the end of their hundred.
>
> `[← Root](../README.md) · [Kernel.Diagnostics](../../src/Nantuc.Kernel.Diagnostics/README.md)`

## Table of contents

- [Conventions](#conventions)
- [Ranges](#ranges)
- [General codes (1000–1099)](#general-codes-10001099)
- [Lexer (1100–1199)](#lexer-11001199)
- [Parser (1200–1299)](#parser-12001299)
- [Code generator (1300–1399)](#code-generator-13001399)
- [Assembler (1400–1499)](#assembler-14001499)
- [How to add a new code](#how-to-add-a-new-code)
- [How to override text](#how-to-override-text)
- [Examples](#examples)

## Conventions

1. **Numbers are stable.** `NumericCode` from `CodeException` is part of the contract.
   Once a code has shipped in a release, its number must never be changed.
2. **One code — one template.** Every `ErrorCode` must have an entry
   in `DefaultErrorMessageProvider.templates`. This invariant is checked by the test
   `DefaultTemplates_HasEntryForEveryDefinedCode`.
3. **Placeholders — `string.Format`.** The template receives `{0}`, `{1}`, … from
   `DiagnosticData`. The number of placeholders must match the number of arguments
   at the call site; the compiler does not check this.
4. **No reflection.** Texts are constants in a `FrozenDictionary`. This is
   AOT/trimming-compatible and works in WASM.
5. **Localization** — via `IErrorMessageProvider` (see
   [How to override text](#how-to-override-text)). The built-in set is
   English only.

## Ranges

| Range | Subsystem | Count | Status |
|---|---|---|---|
| 1000–1099 | General | 2 | stable |
| 1100–1199 | Lexer | 3 | stable |
| 1200–1299 | Parser | 9 | stable |
| 1300–1399 | Code generator (CodeGen) | 18 | stable |
| 1400–1499 | Assembler | 5 | stable |
| 1500+ | — | 0 | free |

**38** codes are defined in total.

---

## General codes (1000–1099)

| Code | Value | Template | When it occurs |
|---|---:|---|---|
| `NotSupported` | 1000 | `Not supported: {0}` | The operation/function is unavailable in the current configuration. Used rarely — mainly for features disabled by a build flag. |
| `NotImplemented` | 1001 | `Feature not implemented: {0}` | The functionality is not implemented yet. Usually — stubs in unfinished branches. |

---

## Lexer (1100–1199)

| Code | Value | Template | When it occurs |
|---|---:|---|---|
| `Lexer_UnexpectedCharacter` | 1100 | `Unexpected character '{0}' at line {1}, column {2}` | The character cannot start any token. Placeholders: character, line, column. |
| `Lexer_InvalidNumber` | 1101 | `Invalid number literal '{0}' at {1}:{2}` | Invalid format or overflow of a numeric literal. |
| `Lexer_UnterminatedString` | 1102 | `Unterminated string starting at {0}:{1}` | A string literal is not closed before the end of the line/file. |

---

## Parser (1200–1299)

| Code | Value | Template | When it occurs |
|---|---:|---|---|
| `Parser_UnknownType` | 1200 | `Unknown type '{0}'` | Unknown type in a declaration or expression. |
| `Parser_ExpectedToken` | 1201 | `Expected {0}, got {1} at {2}:{3}` | A specific token was expected, but another was received. Placeholders: expected, received, line, column. |
| `Parser_UnexpectedToken` | 1202 | `Unexpected token: {0}` | The token is unexpected in this grammatical position. |
| `Parser_InvalidAssignment` | 1203 | `Invalid assignment target` | The LHS of an assignment is not an lvalue, or the types are incompatible. |
| `Parser_ArraySizeNotConstant` | 1204 | `Array size must be constant` | The array size does not reduce to a constant expression. |
| `Parser_UnknownDirective` | 1205 | `Unknown preprocessor directive: #{0}` | A preprocessor/assembler directive is not recognized. |
| `Parser_StructNotDefined` | 1206 | `Struct '{0}' is not defined` | An undeclared struct is used. |
| `Parser_InvalidCharLiteral` | 1207 | `Char literal '{0}' is invalid` | The character literal is empty or too long. |
| `Parser_IncludeAmbiguous` | 1208 | `The file {0} exists in both {1} and {2}; please select a specific file.` | `include` found multiple candidates with the same name. Placeholders: file, path 1, path 2. |
| `Parser_IncludeNotFound` | 1209 | `The file {0} not found in "{1}" and "{2}"` | The `include` was not found in the project files or the `include` folder |

---

## Code generator (1300–1399)

| Code | Value | Template | When it occurs |
|---|---:|---|---|
| `CodeGen_UnknownTypeSize` | 1300 | `Unknown type '{0}' for memory size` | Failed to determine the type size during generation. |
| `CodeGen_UnknownOpCodeSize` | 1301 | `Unknown opcode size: {0}` | Unknown operand size code. |
| `CodeGen_UnknownStructType` | 1302 | `Unknown struct type '{0}'` | Unknown struct type. |
| `CodeGen_UnknownField` | 1303 | `Field '{0}' not found in struct '{1}'` | Access to a nonexistent field. |
| `CodeGen_NonStructAccess` | 1304 | `'{0}' is not a struct or pointer to struct` | Field access on a non-struct. |
| `CodeGen_InvalidDotAccess` | 1305 | `Dot access only for simple variables` | Dot access to an invalid member. |
| `CodeGen_UndefinedVariable` | 1306 | `Undefined variable '{0}'` | Use of an undeclared variable. |
| `CodeGen_CannotGetRegisterAddress` | 1307 | `Cannot take address of register variable '{0}'` | Attempt to take the address of a register variable. |
| `CodeGen_NonIndexableType` | 1308 | `'{0}' is not an array or pointer` | Indexing a non-array and non-pointer. |
| `CodeGen_UnknownFunction` | 1309 | `Function '{0}' not found` | Call to an unknown function. |
| `CodeGen_ArgumentCountMismatch` | 1310 | `Argument count mismatch for function '{0}'` | The number of arguments does not match the signature. |
| `CodeGen_ParameterNotInRegister` | 1311 | `Parameter '{0}' not allocated to a register` | The parameter must be in a register but is not allocated. |
| `CodeGen_UnknownPointedType` | 1312 | `Cannot determine pointed type for dereference` | The pointed-to type is not determined. |
| `CodeGen_DirectStructLoadStore` | 1313 | `Direct load/store of struct variable '{0}' is not supported` | Direct load/store of a struct without accessing fields. |
| `CodeGen_BreakOutsideLoop` | 1314 | `Operator "break" outside loop` | `break` outside a loop. |
| `CodeGen_ContinueOutsideLoop` | 1315 | `Operator "continue" outside loop` | `continue` outside a loop. |
| `CodeGen_AssignmentToConst` | 1316 | `Cannot assign to const '{0}'` | Assignment to a `const` entity (`=`, `+=`, `*p =`, `arr[i] =`). See the "Constness" section in the compiler README. |
| `CodeGen_IncrementOfConst` | 1317 | `Cannot increment/decrement const '{0}'` | `++`/`--` on a `const` (prefix and postfix, `*p`, `arr[i]`). |

> The **`const`-correctness group** (1316–1317) was added in 0.7.2. If
> you add a new `const` code, use only `1318+`; do not reuse 1316/1317.

---

## Assembler (1400–1499)

| Code | Value | Template | When it occurs |
|---|---:|---|---|
| `Asm_UndefinedLabel` | 1400 | `Undefined label '{0}' at position '{1}'` | Reference to an undefined label. |
| `Asm_DuplicateLabel` | 1401 | `Label '{0}' already defined at position {1}` | A label is declared more than once. |
| `Asm_InvalidInstruction` | 1402 | `Invalid instruction syntax: {0}` | Invalid operands or instruction format. |
| `Asm_UnknownRegister` | 1403 | `Unknown register '{0}'` | The register is not recognized. |
| `Asm_UnknownMnemonic` | 1404 | `Unknown mnemonic '{0}'` | The mnemonic is not recognized. |

> **Note on `Asm_UndefinedLabel`:** the template wraps `{1}` in quotes
> (`at position '{1}'`), unlike `Asm_DuplicateLabel` (`at position {1}`).
> This is most likely a typo. It has not been fixed yet — the test
> `NumericContract_IsStable` pins the number but **not** the text; if you
> change the template, add a regression test for the exact string.

---

## How to add a new code

1. **Choose a number** at the end of the corresponding hundred. Never reuse
   deleted numbers — they may have made it into someone's logs.
2. **Add it to `ErrorCode.cs`** at the end of its group, with a `///` description
   and `<remarks>` if the semantics are non-obvious.
3. **Add an entry to `DefaultErrorMessageProvider.templates`**:
   `{ ErrorCode.YourCode, "Your template with {0}" }`.
4. **Add `[InlineData]` to `NumericContract_IsStable`** — this pins the number.
5. **Add `[InlineData]` to the `GetMessage_*` test** — this pins the text
   (otherwise an accidental template edit will slip through).
6. **Update this table** and `docs/METRICS.md` if it counts the number of codes.

Invariants checked automatically by tests:

- `DefaultTemplates_HasEntryForEveryDefinedCode` — every `ErrorCode` is in the dictionary.
- `GetMessage_EveryDefinedCode_DoesNotThrow` — no code throws without arguments.
- `NumericContract_IsStable` — the hardcoded numbers have not moved.

## How to override text

```csharp
using System.Collections.Frozen;
using Nantuc.Kernel.Diagnostics;

var overrides = new Dictionary<ErrorCode, string>
{
    [ErrorCode.Parser_UnknownType] = "Unknown type «{0}»",
    [ErrorCode.CodeGen_AssignmentToConst] = "Cannot assign to const variable «{0}»",
}.ToFrozenDictionary();

var provider = new DefaultErrorMessageProvider(overrides);
var thrower = new ThrowCompiler(provider);

// Then — as usual:
thrower.Throw(ErrorCode.Parser_UnknownType, (DiagnosticData)"Foo");
// → CodeException: "Unknown type «Foo»"
```

The semantics are a **merge over the defaults**, not a replacement: codes that are not in
`overrides` continue to work from the standard set. This is the regression test
`Overrides_MergeWithDefaults_Regression`.

`IErrorMessageProvider.Default` is a singleton with the standard set. For
global localization, replace the provider at the `ThrowCompiler` level
(create a new instance) — `Default` is not mutated.

## Examples

**Basic usage:**

```csharp
var thrower = new ThrowCompiler(provider: null); // → Default

// One argument
thrower.Throw(ErrorCode.Parser_UnknownType, (DiagnosticData)"Foo");
// CodeException: "Unknown type 'Foo'"

// Multiple arguments, mixed types
thrower.Throw(
    ErrorCode.Parser_ExpectedToken,
    (DiagnosticData)"';'",
    (DiagnosticData)"'}'",
    (DiagnosticData)10,
    (DiagnosticData)5);
// CodeException: "Expected ';', got '}' at 10:5"
```

**In an expression (generic form):**

```csharp
// No separate throw branch needed
string? value = null;
return value ?? thrower.Throw<string>(ErrorCode.NotSupported, (DiagnosticData)"missing");
```

**Catching and parsing:**

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

**Culture-invariance:** numbers in `DiagnosticData` are formatted via
`CultureInfo.InvariantCulture`. This is checked by the test
`ImplicitConversions_AreCultureIndependent` — messages in logs/JSON do not
depend on the current thread culture.

## Links

- [`ErrorCode.cs`](../../src/Nantuc.Kernel.Diagnostics/ErrorCode.cs) — definitions.
- [`DefaultErrorMessageProvider.cs`](../../src/Nantuc.Kernel.Diagnostics/DefaultErrorMessageProvider.cs) — templates.
- [`DiagnosticData.cs`](../../src/Nantuc.Kernel.Diagnostics/DiagnosticData.cs) — arguments.
- [`ThrowCompiler.cs`](../../src/Nantuc.Kernel.Diagnostics/ThrowCompiler.cs) — facade.
- [`CodeException.cs`](../../src/Nantuc.Kernel.Diagnostics/CodeException.cs) — exception.
- [`Nantuc.Kernel.Diagnostics/README.md`](../../src/Nantuc.Kernel.Diagnostics/README.md) — module overview.
