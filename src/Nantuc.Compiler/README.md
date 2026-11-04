# Nantuc.Compiler — README

> MiniC → Nantuc bytecode compiler: lexer, parser, AST, code generator with register coloring, assembler (two backends — direct and IR) and optimization infrastructure.
> `[← Root](../README.md) · [Common](../../src/Nantuc.Common/README.md) · [Contracts](../../src/Nantuc.Contracts/README.md) · [Kernel](../../src/Nantuc.Kernel/README.md)`

## Table of contents
- [About this module](#about-this-module)
- [Local project tree](#local-project-tree)
- [API](#api)
- [API examples](#api-examples)
- [How close the module is to the user](#how-close-the-module-is-to-the-user)
- [Special information](#special-information)
  - [Constness (`const`) — semantics and limitations](#constness-const--semantics-and-limitations) **[NEW]**
  - [NANASM assembler: data sections](#nanasm-assembler-data-sections)
  - [Diagnostics: `DiagnosticData` and AOT](#diagnostics-diagnosticdata-and-aot) **[NEW]**
- [What to fix](#what-to-fix)

## About this module

`Nantuc.Compiler` is the frontend and backend of the Nantuc platform compiler. The frontend accepts MiniC source (a subset of C: integer types, `struct`, pointers, arrays, `sizeof`, `asm` blocks, **`const` qualifiers** **[NEW]**), builds an AST and generates bytecode through a two-stage scheme: code generator → assembler → 32-bit ISA words. The backend is two-level: either direct writing to a `MemoryStream` (`Assembler`) or building a list of IR instructions (`ILAssembler`) with peephole optimization capability.

The module depends on `Common` (`OpCode`/`RegType`/`OpCodeSize`/`InstructionDecoder`), `Contracts` (`OptimizationId`/`OptimizationLogEntry`/`IReadOnlyLogOptimization`) and `Diagnostics` (`ThrowCompiler`/`ErrorCode`, **`DiagnosticData`** **[NEW]**). It contains no `unsafe` code itself — `stackalloc` only in `Assembler.Align8` (0–7 bytes, WASM-safe).

Key roles of the module:
- Frontend: `Lexer`, `Parser`, `ASTNode` hierarchy (28 types), `MiniCLanguageDefinition`, **`TokenTypeExtension`** **[NEW]**.
- Code generator: `ExpressionGenerator`, `StatementGenerator`, `FunctionGenerator`, `RegisterAllocator` (greedy coloring of the interference graph), `FunctionContext`, `VariableAccessor`.
- Assembler: `AssemblerBase` (abstraction), `Assembler` (direct), `ILAssembler` (IR), `AssemblerParser`, `InstructionEncoder`, `AsmExtensions`.
  **`AssemblerParser` supports `.data`/`.text` sections and data directives** (`.byte`, `.word`, `.dword`, `.quad`, `.ascii`, `.asciz`, `.zero`).
- Optimizations: `AstOptimizer`, `PeepholeOptimizer` — a framework with `IAstOptimizationRule`/`IPeepholeRule` interfaces; **there are no concrete rules in this batch**.
- Struct layout: `StructLayout`, `FieldInfo` — field alignment to 8 bytes, `Resolve` with iterative dependency resolution.

> **XML documentation is fragmentary.** `AssemblerParser` (class header), `Assembler` (partially), `IPeepholeRule`, `IAstOptimizationRule` are fully documented. Without `///` — the entire AST hierarchy (28 types, only inline `//` comments in the file header), `Lexer`, `Parser`, `ExpressionGenerator`, `StatementGenerator`, `FunctionGenerator`, `RegisterAllocator`, `CodeGenUtils`, `GlobalMemoryManager`, `VariableAccessor`, `AsmLanguageDefinition`. Details and defects — in the [What to fix](#what-to-fix) section.

## Local project tree

Namespaces by layer, files flat within them:

```
Nantuc.Compiler/
├── Nantuc.Compiler.csproj            # net10.0, refs Common+Contracts+Diagnostics
│
├── C/                                 # Frontend (namespace Nantuc.Compiler.C)
│   ├── ASTNode.cs                     # ASTNode + 28 descendants
│   ├── Lexer.cs                       # Token, TokenType, Lexer
│   ├── Parser.cs                      # Parser (recursive descent)
│   ├── MiniCLanguageDefinition.cs     # Keywords/Operators/Punctuation (FrozenSet)
│   ├── StructLayout.cs                # StructLayout, FieldInfo
│   ├── TokenTypeExtension.cs          # TokenType.AsText — switch without reflection, AOT-safe [NEW]
│   │
│   ├── CodeGenerator/                 # namespace Nantuc.Compiler.C.CodeGenerator
│   │   ├── CodeGenUtils.cs            # static size/alignment utilities
│   │   ├── ExpressionGenerator.cs     # expression generation + const checks [CONST]
│   │   ├── FunctionContext.cs         # function context + save depth analysis + const flags [CONST]
│   │   ├── FunctionGenerator.cs       # prologue/epilogue, function traversal + const → GlobalMemoryManager [CONST]
│   │   ├── GlobalInfo.cs              # DTO for globals + IsConst/IsPointedConst [CONST]
│   │   ├── GlobalMemoryManager.cs     # global registry + labels + const flags [CONST]
│   │   ├── RegisterAllocator.cs       # greedy graph coloring + CollectAddressTakenVars [FIX]
│   │   ├── StatementGenerator.cs      # statement generation + CheckConstTarget [CONST]
│   │   ├── VarLocation.cs             # VarLocation + RegisterAllocationResult + const flags [CONST]
│   │   └── VariableAccessor.cs        # variable load/store + IsConst/IsPointedConst [CONST]
│   │
│   └── Optimizators/                  # namespace Nantuc.Compiler.C.Optimizators
│       ├── AstOptimizer.cs            # AstOptimizer + OptimizationResult
│       ├── IAstOptimizationRule.cs
│       └── ILogOptimization.cs
│
└── ASM/                               # Backend (namespace Nantuc.Compiler.ASM)
    ├── Assembler.cs                    # direct writing to MemoryStream
    ├── AssemblerParser.cs              # text assembler → AssemblerBase
    │                                   # + AsmLanguageDefinition (Mnemonics/Registers)
    │                                   # + .data / .text support (buffering, EmitData at the end)
    │
    ├── Base/
    │   └── AssemblerBase.cs            # emitter abstraction
    │
    ├── IR/
    │   ├── AsmInstruction.cs           # AsmItem, AsmLabel, AsmData, AsmInstruction
    │   ├── ILAssembler.cs              # IR collector, two-pass Build()
    │   ├── IPeepholeRule.cs
    │   └── PeepholeOptimizer.cs
    │
    └── Utils/
        └── InstructionEncoder.cs       # Encode*, extension blocks on RegType/OpCode/OpCodeSize
                                        # + AsmExtensions (Emit* on AssemblerBase)
```

Organization notes:
- The frontend (`C/*`) knows about bytecode through `OpCode`/`RegType`/`OpCodeSize`, but not about `Assembler`/`ILAssembler` — the code generator works through `AssemblerBase` extension methods from `Utils/InstructionEncoder.cs`.
- Two backends: `Assembler` (direct, `MemoryStream` + `BinaryWriter`) and `ILAssembler` (list of `AsmItem`). `AssemblerParser` works with either through `AssemblerBase`.
- Optimizations: `AstOptimizer` (AST-level) and `PeepholeOptimizer` (IR-level) — both frameworks, awaiting concrete rules.
- **`TokenTypeExtension.AsText`** — the only place where `TokenType` is turned into a string; used everywhere in diagnostics instead of `Enum.ToString()` (AOT-safe, no reflection, no boxing).

## API

### Overall picture

| Type | Namespace | Kind | Purpose |
|---|---|---|---|
| `ASTNode` + 28 descendants | `C` | `abstract class` / `class` | AST hierarchy: `ProgramNode`, `FunctionNode`, `BlockNode`, `VariableNode`, `AssignmentNode`, `BinaryOpNode`, `IfNode`, `WhileNode`, `ForNode`, `ReturnNode`, `FunctionCallNode`, `ArrayAccessNode`, `AddressOfNode`, `DereferenceNode`, `InlineAsmNode`, `StructDeclNode`, `MemberAccessNode`, `BreakNode`, `ContinueNode`, `DoWhileNode`, `IncrementNode`, `SizeofTypeNode`, `SizeofExprNode` |
| `Lexer` | `C` | `class` | Tokenization; `Token`, `TokenType` |
| `Parser` | `C` | `class` | Recursive descent → `ProgramNode`; **`ParseDeclSpec` for `const` qualifiers** [CONST] |
| `MiniCLanguageDefinition` | `C` | `static partial class` | `Keywords`/`Operators`/`Punctuation` (`FrozenSet`); **`const` in `Keywords`** [CONST] |
| `StructLayout` / `FieldInfo` | `C` | `class` / `record` | Struct layout, `Resolve` with iterative resolution |
| `TokenTypeExtension` | `C` | `static class` | **`extension(TokenType).AsText`** — manual switch, AOT-safe [NEW] |
| `CodeGenUtils` | `C.CodeGenerator` | `static class` | `GetSizeForType`/`GetSizeInBytes`/`GetAlignment`/`IsPrimitiveType`/`EmitMultiplyByConstant` |
| `ExpressionGenerator` | `C.CodeGenerator` | `class` | Expression generation (2 `GenerateExpression` overloads); **`const` checks in `GenerateIncrement`** [CONST] |
| `StatementGenerator` | `C.CodeGenerator` | `class` | Statement generation (`GenerateBlock`, `GenerateIf`, ...); **`CheckConstTarget` at the start of `GenerateAssignment`** [CONST] |
| `FunctionGenerator` | `C.CodeGenerator` | `class` | Entry point: `Generate(ProgramNode)`, prologue/epilogue, `CollectLocalVars`; **passes `IsConst`/`IsPointedConst` to `GlobalMemoryManager.Allocate`** [CONST] |
| `FunctionContext` | `C.CodeGenerator` | `class` | `Create(FunctionNode, ...)` → `VarMap`, `TotalLocalSize`, `SaveAreaSize` (via `ComputeMaxBinaryDepth`); **`IsConst`/`IsPointedConst` in `VarLocation`** [CONST] |
| `RegisterAllocator` | `C.CodeGenerator` | `static class` | `AllocateRegisters(...)` → `RegisterAllocationResult` (greedy coloring); **`CollectAddressTakenVars`** [FIX] |
| `VariableAccessor` | `C.CodeGenerator` | `class` | `LoadToRegister`/`StoreFromRegister`/`LoadAddressToRegister`/`TryGetRegister`; **`IsConst`/`IsPointedConst`** [CONST] |
| `GlobalMemoryManager` | `C.CodeGenerator` | `class` | `Allocate`/`Contains`/`GetInfo`/`GetLabel`; **`isConst`/`isPointedConst` in `Allocate`** [CONST] |
| `VarLocation` / `RegisterAllocationResult` | `C.CodeGenerator` | `class` | Variable metadata; **`IsConst`/`IsPointedConst`** [CONST] |
| `AstOptimizer` | `C.Optimizators` | `class` | `Add`/`Insert`/`Remove`/`Optimize(ProgramNode)` → `IEnumerable<IReadOnlyLogOptimization>` |
| `IAstOptimizationRule` / `ILogOptimization` | `C.Optimizators` | `interface` | Rule contract / log contract |
| `OptimizationResult` | `C.Optimizators` | `class : ILogOptimization` | Simple log implementation |
| `AssemblerBase` | `ASM.Base` | `abstract class` | `EmitInstruction`/`EmitInstruction64`/`EmitJump`/`MarkLabel`/`EmitData`/`Build` |
| `Assembler` | `ASM` | `class : AssemblerBase` | Direct writing; `_patches` for labels |
| `AssemblerParser` | `ASM` | `partial class` | Text → bytes; `Assemble(code, baseAddress, ThrowCompiler)`; `.data`/`.text` sections |
| `AsmLanguageDefinition` | `ASM` | `static class` | `Mnemonics`/`Registers` (`FrozenSet`), `GetMnemonicRegex`/`GetRegisterRegex` |
| `ILAssembler` | `ASM.IR` | `class : AssemblerBase` | IR collector: `GetItems()`, two-pass `Build()` |
| `AsmItem`/`AsmLabel`/`AsmData`/`AsmInstruction` | `ASM.IR` | `class` | IR elements; `AsmInstruction` parses `raw` via `InstructionDecoder` |
| `IPeepholeRule` | `ASM.IR` | `interface` | `TryOptimize(items, index, passCount, out newIndex)` |
| `PeepholeOptimizer` | `ASM.IR` | `class` | `Optimize(items, maxPasses)` to a fixed point |
| `InstructionEncoder` | `ASM.Utils` | `static class` | `Encode`/`EncodeR`/`EncodeI`/`EncodeU`/`EncodeJ`, `Decode`, extension blocks |
| `AsmExtensions` | `ASM.Utils` | `static class` | `EmitMov`/`EmitAdd`/`EmitLdi`/`EmitJmp`/`EmitLoad`/`EmitStore`/… on `AssemblerBase` |

### Frontend

**`Lexer(source, ThrowCompiler)`** — `Tokenize() → List<Token>`. Supports `//`/`/* */`, hex (`0x`), char/string literals, `->` as a separate `TokenType.Arrow`. Operators — from `MiniCLanguageDefinition.Operators`; punctuation — from `Punctuation`. `Peek(offset = 1)` — the only lookahead; depth > 1 is not supported.

**`Parser(tokens, ThrowCompiler)`** — `Parse() → ProgramNode`. Handles:
- `struct Name { ... };` — `_structNames` is filled as encountered; both `Name` and `struct Name` are supported.
- `#include "path"` — added to `IncludesList`.
- `extern <type> <name>(<params>);` — `FunctionNode` with `IsExternal = true`, `Body = null!` (nobody checks afterwards).
- Functions and global variables via `ParseDeclSpec`/`ParseDeclarators`.
- `asm { ... }` — inline asm, `InlineAsmNode` with text.
- Operator precedence: `ParseAssignment` → `ParseLogicalOr` → `ParseLogicalAnd` → `ParseBitwiseOr` → `ParseBitwiseXor` → `ParseBitwiseAnd` → `ParseEquality` → `ParseRelational` → `ParseShift` → `ParseAdditive` → `ParseMultiplicative` → `ParseUnary` → `ParsePrimary`.
- Postfix `++`/`--`, `.`/`->`, `[i]`, `(args)` — in the `ParsePrimary` loop.
- **`sizeof` — in `ParseUnary`, including `sizeof(const T)` and `sizeof(const T*)`** [CONST].
- **`const` qualifiers — `ParseDeclSpec`** [CONST] (see the separate section).

**AST hierarchy** — 28 classes. `AssignmentNode` contains `LValue`, `CompoundOp` (`+=`, `<<=`, etc.), `IndexExpr` (for `arr[i] = x`). `VariableNode` contains `IsPointer`/`PointedType`/`IsArray`/`ArraySize`/`ArrayInitializer` **+ `IsConst`/`IsPointedConst`** [CONST]. `ParameterNode` **+ `IsConst`/`IsPointedConst`** [CONST]. `FunctionNode` — `LocalSize`, `IsExternal`.

**`TokenTypeExtension`** [NEW] — `extension(TokenType).AsText` returns a readable name (`"Identifier"`, `"Keyword"`, …) via a manual `switch`, without `Enum.ToString()`. Used in all parser messages (`Parser_ExpectedToken`, `Parser_UnexpectedToken`) — **AOT-safe, no reflection, no boxing**.

### Code generator

**`FunctionGenerator.Generate(ProgramNode)`** — sequence:
1. `GlobalMemoryManager` + `Allocate` for all `GlobalVarNodes`, **`IsConst`/`IsPointedConst` are passed** [CONST].
2. `_functionTable` is filled from `FunctionNodes`.
3. `MarkLabel("__global_init")` + `GenerateGlobalInit` — global initialization (scalars only, structs are skipped).
4. `EmitJmp("func_main")` if `main` exists.
5. `GenerateFunction` for each non-external function.
6. `EmitData` for globals (with `ArrayInitializer`).
7. `EmitData` for pseudo-globals of parameters 4+ (8 bytes each).

**`FunctionContext.Create`** — computes:
- `VarMap`: `VarLocation` for each parameter and local variable.
- `TotalLocalSize` — total size of stack variables, aligned to 8.
- `SaveAreaSize = (ComputeMaxBinaryDepth(body) + 1) * 8` — the size of the "save area" for intermediate values in binary operations.
- `UsedRegisters` — registers occupied by variables (from `RegisterAllocator`).
- **`VarLocation.IsConst`/`IsPointedConst` — passed from `VariableNode`/`ParameterNode`** [CONST].

**`RegisterAllocator.AllocateRegisters`** — greedy coloring of the interference graph:
- Reserves `r0..r4`, `r27`, `rFL`, `rSP`, `rHP`, `rIP` — **22 available** (`r5..r26`; the comment "23 registers" is incorrect, `r27` is reserved).
- **Excludes variables whose address is taken (`&x`) — they must live in memory. `CollectAddressTakenVars` recursively traverses the AST and collects the names of such variables BEFORE coloring.** [FIX]
- Builds the graph by "live ranges" (`[firstDef, lastUse]`) from a linear AST traversal via `CollectVarEvents`. If ranges intersect — an edge.
- Coloring order — by descending degree (Welsh-Powell heuristic).
- Candidates — scalars only, not arrays, not structs, not pointers to structs.

**`ExpressionGenerator.GenerateExpression(expr)`** (without `targetReg`) — always generates into `r0`. The second overload `(expr, targetReg)`:
- If `targetReg == r0` — delegates to the first.
- Otherwise — a `switch` on type; for unknown — `GenerateExpression(expr); EmitMov(targetReg, r0)`.

Specifics:
- `BinaryOpNode` with `IsComparisonOperator` — `Cmp` + jumps → `1`/`0` into `targetReg`.
- `&&`/`||` — `GenerateLogicalAndOr`, result in `r0`, then `Mov(targetReg, r0)`.
- Constant folding: `NumberNode op NumberNode` → `Ldi(targetReg, result)`.
- Optimization `targetReg == left operand`: right operand in `r1`/`r2`, `ApplyPointerArithmeticScaling` for `+`/`-` with pointers, `EmitBinaryOperation`.
- Pointer arithmetic: `ptr + int` — scaling `int` by `sizeof(*ptr)`; `int + ptr` (for `+`) — same. `ptr - ptr` is not scaled (byte difference, no division).
- **`GenerateIncrement` performs `const` checks at the start (see the `const` section)** [CONST].

**`SaveRegToStack`/`RestoreRegFromStack`** — use `ExpressionSaveDepth` (incremented on `Save`, decremented on `Restore`). Slot: `TotalLocalSize + ExpressionSaveDepth * 8`. Address via `ADDR_SCRATCH_REG_EN = r27`, `Ldi(slot) + Add(rSP)`.

**`StatementGenerator`** — `GenerateBlock`, `GenerateIf`, `GenerateWhile`, `GenerateFor`, `GenerateDoWhile`, `GenerateReturn`, `GenerateBreak`, `GenerateContinue`. For loops — `_loopStack` (`(breakLabel, continueLabel)`), `while` → `continue = startLabel`, `for` → `continue = continueLabel` (before increment), `do-while` → `continue = continueLabel` (before condition).
- **`GenerateAssignment` calls `CheckConstTarget` at the very beginning — a single point for `x = ...`, `x += ...`, `arr[i] = ...`, `*p = ...`** [CONST].

### Assembler

**`Assembler`** — direct writing to `MemoryStream`/`BinaryWriter`:
- `EmitInstruction(uint)` — 4 bytes.
- `EmitInstruction64(uint, ulong)` — 4 bytes + `Align8()` + 8 bytes.
- `EmitJump(uint, string)` — 4 bytes + `Align8()` + placeholder, patched in `Build()`.
- `EmitInstruction64WithLabel(uint, string)` — like jump, but for non-jump opcodes (e.g., `LDI` with a label).
- `EmitData(string, byte[])` — `Align8()`, `MarkLabel`, write.
- `Build()` — patches all `_patches` with absolute addresses (`_baseAddress + targetPos`), returns `byte[]`.

**`ILAssembler`** — list of `AsmItem`:
- `EmitInstruction64(uint, ulong)` checks `HasNeed64IntData(op)` — if not, throws `Asm_InvalidInstruction`.
- `EmitJump(uint, string)` → `AsmInstruction` with `TargetLabel`.
- `EmitData` adds an empty `AsmData(alignment: 8)` (for alignment before the label) + `MarkLabel` + `AsmData(data, alignment: 1)`.
- `Build()` — two-pass: first computes label addresses (`labelAddresses`), then writes bytes, patches jumps.

**`AssemblerParser`** — text assembler. See [NANASM assembler: data sections](#nanasm-assembler-data-sections).

**`InstructionEncoder`** — 32-bit word encoding:
- `Encode(opcode, regDst, regSrc, sizeCode)` — R-format.
- `EncodeI(opcode, reg, sizeCode)` — I-format (`LDI`/`LOAD`/`STORE`).
- `EncodeU(opcode, reg)` — U-format (`INC`/`DEC`/`NOT`/`PUSH`/`POP`/`JMP_IND`/`CALL_IND`).
- `EncodeJ(opcode)` — J-format (always `S64`).
- `Decode(uint)` — reverse decomposition for debugging.
- Extension blocks on `OpCode` (`ToUint`/`OpName`), `RegType` (`ToUint`/`RegName`), `OpCodeSize` (`ToUint`/`SizeName`).

**`AsmExtensions`** — extension block on `AssemblerBase` with `Emit*` methods. The code generator works only through these methods.

### Optimizations

**`AstOptimizer`** — `Add`/`Insert`/`Remove`/`RemoveAt`, `Count`, `GetType(i)`. `Optimize(program)` returns `IEnumerable<IReadOnlyLogOptimization>` — one per rule. There are no rules in this batch.

**`PeepholeOptimizer`** — `Optimize(items, maxPasses = 5)` — loop to a fixed point. Skips `AsmLabel`, applies the first matching rule, `i = nextIndex`.

## API examples

**Full pipeline: text → bytes:**

```csharp
using Nantuc.Compiler.C;
using Nantuc.Compiler.C.CodeGenerator;
using Nantuc.Compiler.C.Optimizators;
using Nantuc.Compiler.ASM.IR;
using Nantuc.Common;
using Nantuc.Diagnostics;

var throwCompiler = new ThrowCompiler();
string source = """
    const int LIMIT = 10;
    int main() {
        int x = 42;
        return x < LIMIT ? x : LIMIT;
    }
    """;

var tokens = new Lexer(source, throwCompiler).Tokenize();
var program = new Parser(tokens, throwCompiler).Parse();
new AstOptimizer().Optimize(program);

var structTable = StructLayout.Resolve(program.StructNodes, throwCompiler);
var asm = new ILAssembler(baseAddress: 0x1000, throwCompiler);
new FunctionGenerator(asm, structTable, throwCompiler).Generate(program);

var items = asm.GetItems();
new PeepholeOptimizer().Optimize(items);

byte[] bytecode = asm.Build();
```

**`const` — what now compiles:**

```csharp
const int LIMIT = 100;
int * const p = &a;              // p cannot be reassigned, *p can be changed
const int *q = &a;               // q can be reassigned, *q cannot be changed
const int * const r = &a;        // neither
const int arr[3] = { 1, 2, 3 };  // arr[i] cannot be changed
void f(const int x);             // x cannot be changed in the body
void g(const int *p);            // *p cannot be changed in the body

const int x = 5;
x = 6;              // ❌ CodeGen_AssignmentToConst (1316)
x++;                // ❌ CodeGen_IncrementOfConst  (1317)
x += 1;             // ❌ CodeGen_AssignmentToConst
arr[0] = 10;        // ❌ CodeGen_AssignmentToConst
*p = 10;            // ❌ CodeGen_AssignmentToConst (if p = const int*)
p = &b;             // ❌ CodeGen_AssignmentToConst (if p = int* const)
```

**Direct assembler without AST:**

```csharp
var parser = new AssemblerParser();
byte[] bytecode = parser.Assemble("""
    LDI r0, 42
    LDI r1, 10
    ADD r0, r1
    END
    """, baseAddress: 0x0, throwCompiler);
```

**NANASM with a data section — full example:**

```csharp
var parser = new AssemblerParser();
byte[] bytecode = parser.Assemble("""
    .data
    msg:        .asciz "Hello, NANASM!\n"
    counter:    .quad 0
    table:      .byte 1, 2, 3, 4, 5
    big_buf:    .zero 64

    .text
    _start:
        LDI r0, msg
        LOAD.S8 r1, msg
        LDI r2, 42
        STORE.S64 r2, counter
        END
    """, baseAddress: 0x1000, throwCompiler);
```

**NANASM with `.data` via explicit `allowDataSection`:**

```csharp
var asm = new Assembler(baseAddress: 0x0, throwCompiler);
var parser = new AssemblerParser();
parser.Assemble("""
    .data
    v:   .dword 0xDEADBEEF
    .text
    _start:
        LOAD.S32 r0, v
        END
    """, asm, allowDataSection: true);
byte[] bytes = asm.Build();
```

**Inline asm in MiniC — `.data` forbidden (by default):**

```csharp
// StatementGenerator.GenerateInlineAsm → AssemblerParser.Assemble(code, _asm, allowDataSection: false)
// So in MiniC you cannot:
//   asm { .data msg: .asciz "x" }  → NotSupported
```

## How close the module is to the user

Medium. The MiniC user works with the compiler through `VmApplication` (e.g., `VmApplication.Compile(source)`), not directly with `Nantuc.Compiler`. But the language/extension author works exactly here.

- Author of new syntax — `Lexer`/`Parser`/`ASTNode`.
- Author of optimizations — `IAstOptimizationRule`/`IPeepholeRule`.
- Author of a new backend — `AssemblerBase` (implement `Build`, `EmitInstruction64`, etc.).
- Author of a new ISA target — `InstructionEncoder` + `AsmExtensions`.
- Author of NANASM scripts — `AssemblerParser` with `.data`/`.text` sections.

**What you should do:**
- Use `ILAssembler` for the production pipeline (two-pass, supports peephole); `Assembler` — only for tests/debugging.
- Custom optimizations — via `Add` in `AstOptimizer`/`PeepholeOptimizer`; rules must be idempotent (`Clear()` is called at the start of each `Optimize`).
- For inline asm use `AsmLanguageDefinition.Mnemonics`/`Registers` as the source of truth.
- For static data in NANASM — `.data` + directives (`.asciz`, `.zero`, `.quad`, …).
- **`const` — use as a guarantee of immutability in APIs and internal data** (see the `const` section).

**What you should not do:**
- Rely on `FunctionNode.Body` for `extern` functions — it is `null!`, nobody checks it.
- Rely on `RegisterAllocator` for large functions (>22 simultaneously live scalars) — greedy coloring will spill to the stack.
- Use `MiniCLanguageDefinition.GetKeywordRegex()` — dead code, may throw on WASM AOT.
- Use `.data` in inline asm — forbidden.
- **Use `const` on `struct` fields — not supported** (see the `const` section).

## Special information

### WASM / cross-platform

| Aspect | Status | Comment |
|---|---|---|
| Building for WASM | ✅ | No `unsafe`, `DllImport`, `LibraryImport`. `stackalloc` only in `Assembler.Align8` (0–7 bytes, WASM-safe). |
| `NativeMemory` | ✅ | Not used. |
| `stackalloc` / `InlineArray` | ✅ | `stackalloc byte[pad]` in `Assembler.Align8` — bounded 0..7 bytes, WASM-safe. |
| `Interlocked` / `Volatile` | ✅ | Not used. |
| `System.Collections.Frozen` | ✅ | `FrozenSet` in `MiniCLanguageDefinition` and `AsmLanguageDefinition`. AOT-friendly. |
| `RegexOptions.None` | ✅ | generated via `[GeneratedRegex(KeywordPattern, RegexOptions.None)]` |
| `[GeneratedRegex]` | ✅ | `AssemblerParser.GetRexExtensionAsm()` — AOT-safe. |
| `Regex` without `Compiled` | ✅ | `AsmLanguageDefinition._mnemonicsRegex`/`regex` — `RegexOptions.IgnoreCase` without `Compiled`, works in AOT. |
| `Enum.ToString()` | ⚠️ | **Removed from diagnostics** — replaced by `TokenTypeExtension.AsText` and `OpCode.OpName()` (manual switches) [FIX]. In other places (`ErrorCode` as a key) — does not leak. |
| `DiagnosticData` | ✅ | **The string representation of the argument is built at creation, no `string.Format(object[])` with boxing, no reflection.** [NEW] |
| Trimming | ⚠️ | `using System.Security.Cryptography;` in `ASTNode.cs` — unused, but may pull in crypto assemblies; trimming will cope, but it's sloppy. |
| AOT | ⚠️ | The main risk is `RegexOptions.Compiled`. Everything else (switch patterns, `readonly record struct DiagnosticData`, switch expressions) — AOT-friendly. |
| `switch` on `OpCode`/`TokenType` | ✅ | Compiles to a jump table, AOT-friendly. |

**Bottom line:** the compiler builds for WASM and AOT **if you remove `RegexOptions.Compiled`**. In its current form, `MiniCLanguageDefinition` is an AOT blocker on browser-wasm.

### Compilation pipeline

```
source (MiniC)
  │
  ▼
Lexer.Tokenize() → List<Token>
  │
  ▼
Parser.Parse() → ProgramNode
  │  (ParseDeclSpec → [const] Type [* [const]] → IsConst/IsPointedConst)
  │
  ├── StructLayout.Resolve(program.StructNodes)
  │
  ▼
FunctionGenerator.Generate(program)
  ├── GlobalMemoryManager.Allocate(...) — with IsConst/IsPointedConst
  ├── GenerateGlobalInit (scalars only)
  ├── EmitJmp("func_main")
  ├── foreach func in FunctionNodes:
  │     ├── CollectLocalVars(func.Body)
  │     ├── FunctionContext.Create(...) → VarMap + const flags
  │     ├── RegisterAllocator.AllocateRegisters(...) — &var → stack
  │     ├── prologue: push savedRegs, sub rSP, stackAlloc, params → registers
  │     ├── StatementGenerator.GenerateBlock(func.Body)
  │     └── epilogue: add rSP, pop savedRegs, ret (or END for main)
  └── EmitData for globals and pseudo-globals
  │
  ▼
AssemblerBase (Assembler | ILAssembler)
  │
  ▼
byte[] (32-bit ISA words)
```

> **NANASM as a separate stream:** `AssemblerParser.Assemble(...)` buffers the `.data` section and calls `_asm.EmitData(...)` at the end.

### Constness (`const`) — semantics and limitations **[NEW]**

`const` in MiniC is a **compile-time immutability guarantee** (not .rodata, not constant folding). The value is stored in the usual way; the prohibition on writing is diagnosed at the code generation stage.

**Supported forms**

| Declaration | What is forbidden | `VariableNode` fields |
|---|---|---|
| `const int x` | `x = ...; x++; x += ...;` | `IsConst = true` |
| `int * const p` | `p = ...; p++; p += ...;` (`*p = ...` allowed) | `IsConst = true`, `IsPointedConst = false` |
| `const int *p` | `*p = ...; (*p)++; p[i] = ...` (`p = ...` allowed) | `IsConst = false`, `IsPointedConst = true` |
| `const int * const p` | both | both `true` |
| `const int arr[N]` | `arr[i] = ...; arr[i]++` | `IsPointedConst = true` (elements), `IsConst = false` |
| `void f(const int x)` | `x = ...` in the body | `ParameterNode.IsConst = true` |
| `void f(const int *p)` | `*p = ...` in the body | `ParameterNode.IsPointedConst = true` |

**Parsing syntax**

`Parser.ParseDeclSpec` → `(type, isPointer, pointedType, prefixConst, ptrConst)`:
1. `[const] type` — prefixConst;
2. `type const` — equivalent (prefixConst);
3. `*` — isPointer;
4. `[* const]` — ptrConst.

`ParseVariableTail` (scalar):
```csharp
IsConst        = isPointer ? ptrConst : prefixConst,
IsPointedConst = isPointer && prefixConst
```

`ParseVariableTail` (array):
```csharp
IsConst        = ptrConst,        // exotic: int * const arr[]
IsPointedConst = prefixConst      // const int arr[] → elements immutable
```

`ParseFunction` (parameter) — the same formula as in the scalar branch.

**Checks in the code generator**

- `StatementGenerator.CheckConstTarget(assign)` — called **at the start** of `GenerateAssignment` (before compound dispatch):
  - `assign.IndexExpr != null` → `IsPointedConst(name)`
  - `assign.LValue is DereferenceNode` → `IsPointedConst(ptrName)`
  - `assign.LValue is MemberAccessNode` → skipped for now (see limitations)
  - simple name → `IsConst(name)`
- `ExpressionGenerator.GenerateIncrement(node, targetReg)` — at the start:
  - `IdentifierNode` → `IsConst`
  - `ArrayAccessNode` → `IsPointedConst(name)`
  - `DereferenceNode` → `IsPointedConst(ptrName)`

**New `ErrorCode`s**

```csharp
CodeGen_AssignmentToConst = 1316,   // "Cannot assign to const '{0}'"
CodeGen_IncrementOfConst  = 1317,   // "Cannot increment/decrement const '{0}'"
```

Both templates are registered in `DefaultErrorMessageProvider` (`Nantuc.Diagnostics`).

**What is NOT supported (deliberately)**

1. **`const` on `struct` fields** — would require extending `FieldInfo`/`StructLayout` and checking `obj.field = ...` / `p->field = ...`. Currently `CheckConstTarget` for `MemberAccessNode` does `return` (TODO comment in the code).
2. **`const` at call-site** — `const int*` → `int*` is not diagnosed (no argument type system).
3. **Constant folding** — `const` does not make a variable a compile-time constant. `for (int i = 0; i < N; i++)` with `const int N` is not optimized.
4. **`const` in `extern` declarations** — the `if (Current.Value == "extern")` block in `Parser.Parse` skips only `*`, not `const`. If needed — symmetrically.
5. **`&x` from `const x`** — yields a regular `int*`; protection works only if the user explicitly declared `const int *p = &x`.

**Interaction with registers**

`RegisterAllocator` **does not exclude `const` variables from register candidates** — this is safe because writes to them are blocked at the `StatementGenerator`/`ExpressionGenerator` level (no `Store`/`Inc`/`Dec` instruction reaches the register of a `const` variable). The address of a `const` variable to which `&` is applied is resolved via `CollectAddressTakenVars` — also without problems.

### NANASM assembler: data sections

`AssemblerParser` can parse `.data`/`.text` sections and data directives. This is needed so that NANASM scripts can describe static data (strings, arrays, buffers) without manually generating `.byte`.

**Section directives**

| Directive | Action |
|---|---|
| `.data`, `.bss`, `.section .data`, `.section .bss` | open the data section |
| `.text`, `.code`, `.section .text`, `.section .code` | close the data section (then — regular code) |

**Data directives** (only inside `.data`)

| Directive | Alias | Width | Example | Result |
|---|---|---|---|---|
| `.byte` | `db` | 1 | `.byte 1, 2, 3` | `[01 02 03]` |
| `.byte` | — | 1 | `.byte "abc"` | `[61 62 63]` |
| `.word` | `dw` | 2 | `.word 0x1234, 0x5678` | `[34 12 78 56]` |
| `.dword` | `dd` | 4 | `.dword 0xDEADBEEF` | `[EF BE AD DE]` |
| `.quad` | `dq` | 8 | `.quad 0x1122334455667788` | 8 bytes little-endian |
| `.ascii` | — | 1 | `.ascii "Hi"` | `[48 69]` |
| `.asciz` | `.string` | 1 (+NUL) | `.asciz "Hi"` | `[48 69 00]` |
| `.zero` | `.space` | N | `.zero 256` | 256 zero bytes |

**Limitations**

| Limitation | Reason | What to do |
|---|---|---|
| `.data` forbidden in MiniC inline asm | `allowDataSection: false` by default | Move `.data` to a separate NANASM file |
| `.quad <label>` not supported | The `EmitData` patch mechanism cannot handle label operands | `.quad 0` + initialization in `__global_init` |
| `.align N` not supported | `EmitData` always aligns to 8 | Rely on automatic 8-byte alignment |
| `;` inside a string is truncated | `Split(['\n', ';'])` | Do not use `;` in `.ascii`/`.asciz` |
| Little-endian only for now | Nantuc platform | — |

### Diagnostics: `DiagnosticData` and AOT **[NEW]**

Diagnostic arguments are now passed as `ReadOnlySpan<DiagnosticData>`, not `ReadOnlySpan<object>`:

```csharp
public readonly record struct DiagnosticData
{
    public string Text { get; }
    // ...
}
```

What this gives for AOT:

- **No boxing** of `int`/`char`/enum when packing into `object`.
- **No virtual `Object.ToString()`** at format time — the string is already ready.
- **`Enum.ToString()` in diagnostics is removed** — all enums are converted via `TokenTypeExtension.AsText` / `OpCode.OpName()` (manual switches, zero reflection).
- **`Type.Name` is not passed to diagnostics** — the caller passes a ready string.

Remaining reflection-adjacent places in the BCL (not in this module):
- `ErrorCode` as a key in `FrozenDictionary` — does not leak into `Enum.ToString()`.
- `DiagnosticData.Of<T>(T?)` — if `T` has not overridden `ToString()`, the BCL may read metadata. Use only for known types.

**Bottom line:** `DiagnosticData` is fully AOT-safe for all current `ThrowCompiler.Throw` calls.

### `Assembler` vs `ILAssembler`

| | `Assembler` | `ILAssembler` |
|---|---|---|
| Storage | `MemoryStream` + `BinaryWriter` | `List<AsmItem>` |
| Label patching | `_patches` in `Build()` | `labelAddresses` in the first pass of `Build()` |
| Peephole | ❌ | ✅ |
| `HasNeed64IntData` check | ❌ | ✅ |
| Duplicate label validation | ✅ | ✅ |
| Two-pass Build | ❌ | ✅ |
| `.data` via `AssemblerParser` | ✅ | ✅ |

## What to fix

In descending order of importance. `[WASM]` — risks for browser-WASM/AOT; `[API]` — public surface; `[CONST]` — incompleteness of const semantics; `[Defect]` — behavior that does not match expectations/XML; `[Housekeeping]` — code organization; `[XML]` — documentation.

### `const` — incompleteness of semantics **[CONST]** **[NEW]**

3. **[CONST] Expression-context `const` violation is not diagnosed.**  
   File: `ExpressionGenerator.GenerateAssignmentExpr`.  
   ```csharp
   private void GenerateAssignmentExpr(AssignmentNode assign, RegType targetReg)
   {
       GenerateExpression(assign.Value, targetReg);
       _varAccessor.StoreFromRegister(assign.Name, targetReg);   // ← no CheckConstTarget
   }
   ```
   `StatementGenerator.CheckConstTarget` is called **only** for statement-level `AssignmentNode`. But `AssignmentNode` can also appear in an expression: `(x = 5) + 1`, `(x += 1) * 2`. In this case:
   - `x` may be `const` → the check is skipped → the write occurs.
   - For `MemberAccessNode`/`DereferenceNode` in `assign.Name == null!` → `StoreFromRegister` throws `UndefinedVariable` (this is item 4 below).
   
   **Fix:** in `GenerateAssignmentExpr`, either delegate to `StatementGenerator.GenerateAssignment`, or duplicate `CheckConstTarget` (the former is better — it will also eliminate the NRE for `MemberAccessNode`).

4. **[CONST] `CheckConstTarget` for `MemberAccessNode` — TODO.**  
   File: `StatementGenerator.CheckConstTarget`: `if (assign.LValue is MemberAccessNode) return;   // TODO`.  
   **Consequence:** `struct Point p; p.x = 5` is not checked, even if the field is declared as `const int x` (which is currently impossible — `const` on struct fields is also not supported).  
   **Fix:** extend `FieldInfo` with `IsConst`/`IsPointedConst` fields, thread them through `StructLayout`, check in `CheckConstTarget`.

5. **[CONST] `const` qualifier in `extern` declarations — not parsed.**  
   File: `Parser.Parse`, block `if (Current.Value == "extern")`. Skips `*`, but not `const`.  
   **Fix:** mirror `ParseDeclSpec`.

### API / Defects

6. **[API] `RegisterAllocator` — the comment "23 registers" is incorrect.**  
   File: `RegisterAllocator.cs`, `GetAvailableRegisters()`: `return regs;   // r5..r27 — 23 registers for variables`. In `ReservedRegs`, `RegType.r27` is explicitly listed, so there are **22** available (`r5..r26`).  
   Fix: correct the comment.

7. **[API] `StatementGenerator.GenerateAssignment` — case 4 (direct assign) — dead code.**  
   File: `StatementGenerator.cs`, `case 4`: condition `assign.LValue == null`. But `Parser.ParseAssignment` for `IdentifierNode` creates `new AssignmentNode(id.Name, right) { LValue = left, ... }` — `LValue` is **always** set. The condition never executes.  
   Fix: remove the `assign.LValue == null` check; check `assign.LValue is IdentifierNode` or `assign.Name != null && assign.IndexExpr == null && assign.CompoundOp == null`.

8. **[API] `RegisterAllocator.BuildInterferenceGraph` — linear live ranges, does not account for loops.**  
   File: `RegisterAllocator.cs`. `CollectVarEvents` numbers positions in the AST traversal; a variable defined in a loop but not used afterwards will get the range `[defPos, defPos]`.  
   Fix: either document as "best effort", or implement real live-range analysis.

9. **[API] `FunctionGenerator.GenerateGlobalInit` — struct initialization is silently unsupported.**  
   File: `FunctionGenerator.cs`: `if (CodeGenUtils.IsStructType(global.Type, _structTable)) continue;`. A global struct with an `Initializer` (`Point origin = {0, 0};`) will be ignored.  
   Fix: support it or throw `NotSupported`.

10. **[API] `AsmInstruction(uint, ulong, bool)` — fallback via `ToString()`.**  
    File: `AsmInstruction.cs`: `TargetLabel = absoluteTarget.ToString();`. If `isAbsolute: false`, `Build()` will not find the label → `Asm_UndefinedLabel`.  
    Fix: remove the fallback, always `Immediate = absoluteTarget`.

11. **[API] `ExpressionGenerator.GenerateBinaryOp` — `&&`/`||` into `targetReg` via `Mov`.**  
    File: `ExpressionGenerator.cs`: `GenerateLogicalAndOr(binop); if (targetReg != r0) EmitMov(targetReg, r0);`. Extra instructions for chains.  
    Fix: `GenerateLogicalAndOr(binop, targetReg)`.

12. **[API] `AssemblerParser.TryAddIntegerData` — `.quad <label>` is not supported yet.**  
    Fix: a label patch mechanism in `EmitData`/`Build()`.

13. **[API] `AssemblerParser.TryAddStringData` — `;` inside a string is truncated.**  
    Fix: move string parsing out of the common `Split`, or add hex escaping (`\x3B`).

### Housekeeping

14. **[Housekeeping] `Nantuc.Compiler.C.Optimizators` — namespace with a typo.**  
    Files: `AstOptimizer.cs`, `IAstOptimizationRule.cs`, `ILogOptimization.cs`. Should be `Optimizers` (or `Optimizations`). Public surface.  
    Fix: rename (breaking change) or leave with `<remarks>`.

15. **[Housekeeping] `AsmLanguageDefinition` and `MiniCLanguageDefinition` — parallel dictionaries.**  
    Regex helpers in `MiniCLanguageDefinition` — dead code.  
    Fix: move the common part into a single `LanguageDefinition` module, remove the dead code.

16. **[Housekeeping] `FieldInfo` — `record` with `Name`/`Type`/`Offset`/`Size`.**  
    `GetField` uses `List.Find` — O(n).  
    Fix: `Dictionary<string, FieldInfo>` in `StructLayout`.

### XML

17. **[XML] AST hierarchy — no `///`.**  
    Including **`VariableNode.IsConst`/`IsPointedConst` and `ParameterNode.IsConst`/`IsPointedConst`** — new fields without documentation.  
    Fix: `///` on key nodes, mark nullable fields.

18. **[XML] `ExpressionGenerator`, `StatementGenerator`, `FunctionGenerator` — no `///`.**  
    Especially **`CheckConstTarget`** — the single `const` check point for assignment, and **`GenerateIncrement`** — for `++`/`--`.  
    Fix: `<summary>` on public methods; for `CheckConstTarget` — `<remarks>` listing the checked forms.

19. **[XML] `CodeGenUtils` — no `///`.**  
    `GetAlignment(type, structTable)` for structs returns a hardcoded `8` — not described.  
    Fix: `///` on each method.

20. **[XML] `AssemblerParser` — the class header does not list `.data` and does not mention `allowDataSection`.**  
    Fix: extend `<summary>`/`<remarks>`.

### What has already been done (for history)

- **`const` qualifiers** (`const int`, `int * const`, `const int *`, `const int * const`, `const int arr[]`, `const` parameters, `sizeof(const T)`):
  - `Lexer`/`MiniCLanguageDefinition` — `const` in `Keywords`.
  - `Parser.ParseDeclSpec` — parsing `[const] type [* [const]]` → `(type, isPointer, pointedType, prefixConst, ptrConst)`.
  - `Parser.ParseVariableTail`/`ParseDeclarators`/`ParseFunction` — passing flags to `VariableNode`/`ParameterNode`.
  - `VariableNode`/`ParameterNode`/`VarLocation`/`GlobalInfo` — `IsConst`/`IsPointedConst` fields.
  - `FunctionContext.Create`/`FunctionGenerator.Generate`/`GlobalMemoryManager.Allocate` — flag propagation.
  - `VariableAccessor.IsConst`/`IsPointedConst`.
  - `StatementGenerator.CheckConstTarget` — at the start of `GenerateAssignment`.
  - `ExpressionGenerator.GenerateIncrement` — checks for `Identifier`/`ArrayAccess`/`Dereference`.
  - `ErrorCode.CodeGen_AssignmentToConst = 1316`, `CodeGen_IncrementOfConst = 1317` + templates in `DefaultErrorMessageProvider`.

- **`TokenTypeExtension.AsText`** — manual switch instead of `Enum.ToString()`, AOT-safe, used in all parser messages.

- **`RegisterAllocator.CollectAddressTakenVars`** — variables whose address is taken (`&x`) are excluded from register candidates before coloring. Fixes `Cannot take address of register variable 'a'` for `&local`.

- **`Parser.ParseUnary`** — restored the `sizeof` block (was lost when adding `const`); added support for `sizeof(const T)` and `sizeof(const T*)`.

- **`Parser.ParseVariableTail` (scalar)** — fixed the formula: `IsConst = isPointer ? ptrConst : prefixConst` (previously `IsConst = ptrConst`, which is why `const int x` was not caught).

- **`DiagnosticData`** — unification of diagnostic arguments via `ReadOnlySpan<DiagnosticData>` (no boxing, no reflection, AOT-safe). Merge of `overrides` over standard templates in `DefaultErrorMessageProvider`.

- `InstructionEncoder` — extension blocks on `OpCode`/`RegType`/`OpCodeSize` + `AsmExtensions` on `AssemblerBase`.

- `ILAssembler` — two-pass `Build()` with precomputed `labelAddresses`, peephole support.

- `AssemblerParser.GetRexExtensionAsm()` — `[GeneratedRegex]` (AOT-safe), normalization of `LOAD . S8` → `LOAD.S8`.

- `AssemblerParser` — `.data`/`.text` sections + data directives; data is buffered in `_pendingData` and merged via `EmitData` at the end.

- `StructLayout.Resolve` — iterative dependency resolution.