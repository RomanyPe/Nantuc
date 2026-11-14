# Nantuc.Compiler — README

> MiniC -> Nantuc bytecode compiler: lexer, parser, AST, code generator with register coloring, assembler (two backends — direct and IR), and optimization infrastructure.
> `[← Root](../README.md) · [Common](../../src/Nantuc.Common/README.md) · [Contracts](../../src/Nantuc.Contracts/README.md) · [Kernel](../../src/Nantuc.Kernel/README.md)`

## Table of Contents
- [About this module](#about-this-module)
- [Local project tree](#local-project-tree)
- [API](#api)
- [API examples](#api-examples)
- [Proximity of the module to the user](#proximity-of-the-module-to-the-user)
- [Special information](#special-information)
  - [AST node catalog](#ast-node-catalog)
  - [Overflow of narrow integer types](#overflow-of-narrow-integer-types) **[NEW]**
  - [Constness (`const`) — semantics and limitations](#constness-const--semantics-and-limitations)
  - [NANASM assembler: data sections](#nanasm-assembler-data-sections)
  - [Instruction format and `InstructionEncoder`](#instruction-format-and-instructionencoder)
  - [Diagnostics: `DiagnosticData` and AOT](#diagnostics-diagnosticdata-and-aot)
- [What to fix](#what-to-fix)

## About this module

`Nantuc.Compiler` is the frontend and backend of the Nantuc platform compiler. The frontend accepts MiniC source (a subset of C: integer types, `struct`, pointers, arrays, `sizeof`, `asm` blocks, **`const` qualifiers**, **overflow semantics of narrow integers**), builds an AST, and generates bytecode via a two-stage scheme: code generator -> assembler -> 32-bit ISA words. The backend is two-level: either direct writing to a `MemoryStream` (`Assembler`), or building a list of IR instructions (`IRAssembler`) with the possibility of IR optimizations.

The module depends on `Common` (`OpCode`/`RegType`/`OpCodeSize`/`InstructionDecoder`), `Contracts` (`OptimizationId`/`OptimizationLogEntry`/`IReadOnlyLogOptimization`), and `Diagnostics` (`ErrorReporter`/`ErrorCode`, **`DiagnosticData`**). It contains no `unsafe` code itself — `stackalloc` only in `Assembler.Align8` (0–7 bytes, WASM-safe).

Key roles of the module:
- Frontend: `Lexer`, `Parser`, **`Nantuc.Compiler.C.AST`** — 28 AST nodes in separate files, `MiniCLanguageDefinition`, **`TokenTypeExtension`**.
- Code generator: `ExpressionGenerator`, `StatementGenerator`, `FunctionGenerator`, `RegisterAllocator` (greedy interference-graph coloring), `FunctionContext`, `VariableAccessor`, **overflow casts via `CASTZ`/`CASTS`**.
- Assembler: `AssemblerBase` (abstraction), `Assembler` (direct), `IRAssembler` (IR), `AssemblerParser`, `InstructionEncoder`, `AsmExtensions`, `AsmLanguageDefinition`.
  **`AssemblerParser` supports `.data`/`.bss`/`.text`/`.code` sections and data directives** (`.byte`, `.word`, `.dword`, `.quad`, `.ascii`, `.asciz`, `.zero`).
- Optimizations: `AstOptimizer`, `IROptimizer` — a framework with `IAstOptimizationRule`/`IOptimizerIRRule` interfaces; **there are no concrete rules in this batch**.
- Struct layout: `StructLayout`, `FieldInfo` — field alignment to 8 bytes, `Resolve` with iterative dependency resolution.

> **XML documentation is fragmentary.** Fully documented: `AssemblerParser` (class header — but not synchronized with new opcodes, see item 25), `Assembler` (partially), `IOptimizerIRRule`, `IAstOptimizationRule`, `InstructionEncoder` (format + regions). Without `///` — the entire AST hierarchy (28 types in `C/AST/`, only inline `//` comments in the file header), `Lexer`, `Parser`, `ExpressionGenerator`, `StatementGenerator`, `FunctionGenerator`, `RegisterAllocator`, `CodeGenUtils`, `GlobalMemoryManager`, `VariableAccessor`, `IRAssembler`, `IROptimizer`, `AsmExtensions`. Details and defects — in the [What to fix](#what-to-fix) section.

## Local project tree

```
Nantuc.Compiler/
├── Nantuc.Compiler.csproj            # net10.0, refs Common+Contracts+Diagnostics
│
├── C/                                 # Frontend (namespace Nantuc.Compiler.C)
│   ├── Lexer.cs                       # Token, TokenType, Lexer + 3-character operators (<<=, >>=)
│   ├── Parser.cs                      # Parser (recursive descent); ParseFunction(..., isReturnPointer)
│   ├── MiniCLanguageDefinition.cs     # Keywords/Operators (FrozenSet)
│   ├── StructLayout.cs                # StructLayout (Dictionary<string, FieldInfo>), FieldInfo
│   ├── TokenTypeExtension.cs          # TokenType.AsText — switch without reflection, AOT-safe
│   │
│   ├── AST/                           # namespace Nantuc.Compiler.C.AST
│   │   ├── ASTNode.cs
│   │   ├── ProgramNode.cs
│   │   ├── StructDeclNode.cs
│   │   ├── FunctionNode.cs            # + ReturnIsPointer
│   │   ├── ParameterNode.cs           # + IsConst/IsPointedConst
│   │   ├── VariableNode.cs            # + IsConst/IsPointedConst
│   │   ├── BlockNode.cs
│   │   ├── IfNode.cs / WhileNode.cs / DoWhileNode.cs / ForNode.cs
│   │   ├── ReturnNode.cs / BreakNode.cs / ContinueNode.cs / InlineAsmNode.cs
│   │   ├── NumberNode.cs / IdentifierNode.cs
│   │   ├── AssignmentNode.cs          # Name, IndexExpr, LValue, CompoundOp
│   │   ├── BinaryOpNode.cs / UnaryOpNode.cs / IncrementNode.cs
│   │   ├── FunctionCallNode.cs / IndirectCallNode.cs
│   │   ├── ArrayAccessNode.cs / AddressOfNode.cs / DereferenceNode.cs
│   │   ├── MemberAccessNode.cs        # IsArrow
│   │   └── SizeofTypeNode.cs / SizeofExprNode.cs
│   │
│   ├── CodeGenerator/                 # namespace Nantuc.Compiler.C.CodeGenerator
│   │   ├── CodeGenUtils.cs            # size/alignment utilities + IsSignedType + EmitCastToType
│   │   ├── ExpressionGenerator.cs     # expressions + CheckConstTarget + overflow casts
│   │   ├── FunctionContext.cs         # context + ReturnSize/ReturnIsSigned/ReturnIsVoid
│   │   ├── FunctionGenerator.cs       # prologue/epilogue, argument and return casts
│   │   ├── GlobalInfo.cs              # + IsSigned
│   │   ├── GlobalMemoryManager.cs     # global registry, IsSigned is computed
│   │   ├── RegisterAllocator.cs       # greedy coloring + CollectAddressTakenVars
│   │   ├── StatementGenerator.cs      # statements + overflow casts on all lvalues
│   │   ├── VarLocation.cs             # + IsSigned
│   │   └── VariableAccessor.cs        # load/store with canonicalization via CASTZ/CASTS
│   │
│   └── Optimizations/                  # namespace Nantuc.Compiler.C.Optimizations
│       ├── AstOptimizer.cs
│       ├── IAstOptimizationRule.cs
│       └── IRogOptimization.cs
│
└── ASM/                               # Backend (namespace Nantuc.Compiler.ASM)
    ├── Assembler.cs                    # direct writing to MemoryStream + label patching
    ├── AssemblerParser.cs              # text -> AssemblerBase; .data/.bss/.text/.code;
    │                                   # data directives; NormalizeSizeExtension
    │                                   # _opMapLex + CASTZ/CASTS; FormatIWithSize
    │
    ├── Base/
    │   └── AssemblerBase.cs            # BaseAddress/ErrorReporter/SectionSize/DataSectionStart
    │
    ├── IR/
    │   ├── AsmInstruction.cs
    │   ├── IRAssembler.cs
    │   ├── IOptimizerIRRule.cs
    │   └── IROptimizer.cs
    │
    └── Utils/
        └── InstructionEncoder.cs       # Encode/Decode (incl. CASTZ/CASTS)
                                        # + extension blocks on RegType/OpCode/OpCodeSize
                                        # + AsmExtensions (Emit* + EmitInstI)
```

Organization notes:
- The frontend (`C/*` and `C/AST/*`) knows about bytecode via `OpCode`/`RegType`/`OpCodeSize`, but not about `Assembler`/`IRAssembler` — the code generator works through `AssemblerBase` extension methods from `Utils/InstructionEncoder.cs`.
- **AST nodes are moved into a separate namespace `Nantuc.Compiler.C.AST` and physically split into files in `C/AST/` (one node per file, except the base `ASTNode.cs`)**.
- Two backends: `Assembler` (direct, `MemoryStream` + `BinaryWriter`) and `IRAssembler` (list of `AsmItem`). `AssemblerParser` works with either through `AssemblerBase`.
- Optimizations: `AstOptimizer` (AST-level) and `IROptimizer` (IR-level) — both frameworks, awaiting concrete rules.
- **`TokenTypeExtension.AsText`** — the only place where `TokenType` is turned into a string; used everywhere in diagnostics instead of `Enum.ToString()` (AOT-safe, no reflection, no boxing).
- **`AsmLanguageDefinition.Mnemonics`/`Registers`** — the source of truth for NANASM scripts, but **not synchronized with the XML header of `AssemblerParser`** (see item 22).

## API

### General picture

| Type | Namespace | Kind | Purpose |
|---|---|---|---|
| `ASTNode` + 28 descendants | `C.AST` | `abstract class` / `class` | AST hierarchy. Full catalog — in the [AST node catalog](#ast-node-catalog) section |
| `Lexer` | `C` | `class` | Tokenization; `Token`, `TokenType`; **3-character operators `<<=`/`>>=`** |
| `Parser` | `C` | `class` | Recursive descent -> `ProgramNode`; **`ParseDeclSpec` for `const`**, **`ParseFunction(..., isReturnPointer)`** |
| `MiniCLanguageDefinition` | `C` | `static partial class` | `Keywords`/`Operators` (`FrozenSet`); `const` in `Keywords` |
| `StructLayout` / `FieldInfo` | `C` | `class` / `record` | Struct layout; `_fields` — `Dictionary<string, FieldInfo>` |
| `TokenTypeExtension` | `C` | `static class` | `extension(TokenType).AsText` — manual switch, AOT-safe |
| `CodeGenUtils` | `C.CodeGenerator` | `static class` | `GetSizeForType`/`GetSizeInBytes`/`GetAlignment`/`IsPrimitiveType`/`IsSignedType`; extension `EmitCastToType`, `EmitMultiplyByConstant` |
| `ExpressionGenerator` | `C.CodeGenerator` | `class` | Expression generation; **`GenerateAssignmentExpr` with 4 lvalue branches**, **`CheckConstTarget`** (internal), **overflow casts** |
| `StatementGenerator` | `C.CodeGenerator` | `class` | Statement generation; **calls `_exprGen.CheckConstTarget`**; **overflow casts on all lvalues** |
| `FunctionGenerator` | `C.CodeGenerator` | `class` | `Generate(ProgramNode)`, prologue/epilogue; **cast of arguments r0..r3 and pseudo-globals** |
| `FunctionContext` | `C.CodeGenerator` | `class` | `Create(...)` -> `VarMap`, `TotalLocalSize`, `SaveAreaSize`; **`ReturnSize`/`ReturnIsSigned`/`ReturnIsVoid`**, **`VarLocation.IsSigned`** |
| `RegisterAllocator` | `C.CodeGenerator` | `static class` | `AllocateRegisters(...)` -> `RegisterAllocationResult`; `CollectAddressTakenVars` |
| `VariableAccessor` | `C.CodeGenerator` | `class` | `LoadToRegister`/`StoreFromRegister` with **`EmitCastToType`** after LOAD and after MOV into the home register |
| `GlobalMemoryManager` | `C.CodeGenerator` | `class` | `Allocate`/`Contains`/`GetInfo`/`GetLabel`; **`IsSigned` is computed in `Allocate`** |
| `VarLocation` / `RegisterAllocationResult` | `C.CodeGenerator` | `class` | Variable metadata; **`IsSigned`** |
| `AstOptimizer` | `C.Optimizations` | `class` | `Add`/`Insert`/`Remove`/`Optimize(ProgramNode)` -> `IEnumerable<IReadOnlyLogOptimization>` |
| `IAstOptimizationRule` / `IRogOptimization` | `C.Optimizations` | `interface` | Rule contract / log contract |
| `OptimizationResult` | `C.Optimizations` | `class : IRogOptimization` | Simple log implementation |
| `AssemblerBase` | `ASM.Base` | `abstract class` | `Build`/`EmitInstruction`/`EmitInstruction64`/`EmitInstruction64WithLabel`/`EmitJump`/`EmitJumpToAbsolute`/`MarkLabel`/`EmitData`; properties `BaseAddress`/`ErrorReporter`/`CodeSectionSize`/`DataSectionStart`/`DataSectionSize` |
| `Assembler` | `ASM` | `class : AssemblerBase` | Direct writing; `_patches`; `DataSection*`/`CodeSectionSize` in `Build()` |
| `AssemblerParser` | `ASM` | `partial class` | `Assemble(code, baseAddress, ErrorReporter)`, `Build(code, asm)`, `Assemble(code, asm, allowDataSection)`; `.data`/`.bss`/`.text`/`.code` sections; **`FormatIWithSize` for `CASTZ`/`CASTS`** |
| `AsmLanguageDefinition` | `ASM` | `static class` | `Mnemonics`/`Registers` (`FrozenSet`); regexes without `Compiled` |
| `IRAssembler` | `ASM.IR` | `class : AssemblerBase` | `GetItems()`, two-pass `Build()`, padding `AsmData` |
| `AsmItem`/`AsmLabel`/`AsmData`/`AsmInstruction` | `ASM.IR` | `class` | IR elements; `AsmInstruction` auto-decodes fields via `InstructionDecoder` |
| `IOptimizerIRRule` | `ASM.IR` | `interface` | `TryOptimize(items, index, passCount, out newIndex)`, `GetLogs()`, `Clear()` |
| `IROptimizer` | `ASM.IR` | `class` | `Optimize(items, maxPasses = 5)` — until fixed point; `Clear()` on entry |
| `InstructionEncoder` | `ASM.Utils` | `static class` | `Encode`/`EncodeR`/`EncodeI`/`EncodeU`/`EncodeJ` + `EncodeLDI`/…; **`Decode(uint)`** with cases for `CASTZ`/`CASTS`; extension blocks |
| `AsmExtensions` | `ASM.Utils` | `static class` | `EmitMov`/…/`EmitInstI`/`EmitInstR`/`EmitInstU`/`EmitInstJ`/`EmitInstWithData` |

### Frontend

**`Lexer(source, ErrorReporter)`** — `Tokenize() -> List<Token>`. Supports `//`/`/* */`, hex (`0x`), char/string literals, `->` as a separate `TokenType.Arrow`. **3-character operators `<<=`/`>>=` are recognized in `ReadOperator` before checking for 2-character ones** — otherwise `x >>= 1` is split into `>>` + `=` and fails in `Parser.ParseAssignment`. `Peek(offset = 1)` — the only lookahead.

**`Parser(tokens, ErrorReporter)`** — `Parse() -> ProgramNode` (namespace `Nantuc.Compiler.C.AST`). Handles:
- `struct Name { ... };` — `_structNames` is populated as they are encountered; both `Name` and `struct Name` are supported.
- `#include "path"` — added to `IncludesList`.
- `extern <type> <name>(<params>);` — `FunctionNode` with `IsExternal = true`, `Body = null!`.
- Functions and global variables via `ParseDeclSpec`/`ParseDeclarators`.
- `asm { ... }` — `InlineAsmNode` with the text.
- Precedence: `ParseAssignment` -> `ParseLogicalOr` -> `ParseLogicalAnd` -> `ParseBitwiseOr` -> `ParseBitwiseXor` -> `ParseBitwiseAnd` -> `ParseEquality` -> `ParseRelational` -> `ParseShift` -> `ParseAdditive` -> `ParseMultiplicative` -> `ParseUnary` -> `ParsePrimary`.
- Postfix `++`/`--`, `.`/`->`, `[i]`, `(args)` — in the `ParsePrimary` loop.
- `sizeof` — in `ParseUnary`, including `sizeof(const T)` and `sizeof(const T*)`.
- `const` qualifiers — `ParseDeclSpec`.
- **`ParseFunction(type, name, isReturnPointer)` — `isReturnPointer` is passed into `FunctionNode.ReturnIsPointer`** (needed for the overflow cast of `return`).

**AST hierarchy (`Nantuc.Compiler.C.AST`)** — 28 classes, split into files in `C/AST/`. The base `ASTNode` — the only abstract property is `NameNode` (`=> nameof(XxxNode)`). `AssignmentNode` contains `LValue`, `CompoundOp`, `IndexExpr`. `VariableNode` contains `IsPointer`/`PointedType`/`IsArray`/`ArraySize`/`ArrayInitializer` + `IsConst`/`IsPointedConst`. `ParameterNode` + `IsConst`/`IsPointedConst`. **`FunctionNode` — `LocalSize`, `IsExternal`, `ReturnIsPointer`**.

**`TokenTypeExtension`** — `extension(TokenType).AsText` returns a readable name via a manual `switch`, without `Enum.ToString()`. Used in all parser messages — **AOT-safe, no reflection, no boxing**.

### Code generator

**`FunctionGenerator.Generate(ProgramNode)`** — sequence:
1. `GlobalMemoryManager` + `Allocate` for all `GlobalVarNodes`; `IsConst`/`IsPointedConst`/`IsSigned` are computed automatically.
2. `_functionTable` is filled from `FunctionNodes`.
3. `MarkLabel("__global_init")` + `GenerateGlobalInit` — initialization of globals (scalars only, structs are skipped).
4. `EmitJmp("func_main")`.
5. `GenerateFunction` for each non-external function.
6. `EmitData` for globals (with `ArrayInitializer`).
7. `EmitData` for pseudo-globals of parameters 4+.

**`FunctionContext.Create`** — computes:
- `VarMap`: `VarLocation` for each parameter and local variable, including **`IsSigned`** (for non-pointer, non-struct).
- `TotalLocalSize` — total size of stack variables, aligned to 8.
- `SaveAreaSize = (ComputeMaxBinaryDepth(body) + 1) * 8`.
- `UsedRegisters` — registers from `RegisterAllocator`.
- **`ReturnSize`/`ReturnIsSigned`/`ReturnIsVoid`** — for the `return` cast in `GenerateReturn`.

**`RegisterAllocator.AllocateRegisters`** — greedy coloring:
- Reserves `r0..r4`, `r27`, `rFL`, `rSP`, `rHP`, `rIP` — **22 available** (`r5..r26`; the comment in the code “23 registers” is incorrect, see item 6).
- **Excludes variables whose address is taken (`&x`)** — `CollectAddressTakenVars` before coloring.
- Builds the graph by `[firstDef, lastUse]`; coloring order is by descending degree.

**`ExpressionGenerator.GenerateExpression(expr)`** (without `targetReg`) — always into `r0`. The second overload `(expr, targetReg)`:
- `targetReg == r0` — delegates to the first.
- Otherwise `switch` by type; unknown — `GenerateExpression(expr); EmitMov(targetReg, r0)`.

**`GenerateAssignmentExpr(assign, targetReg)`** — **assignment-as-expression** (result = new value). Covers:
- `arr[i] = value` (via `StoreR0ToArrayElement`)
- `*ptr = value` (via `GenerateExpression(operand, r0)` + `STORE_IND`)
- `obj.field = value` (via `GenerateMemberAddress(member, r0)` + `STORE_IND`)
- `x = value` (via `StoreFromRegister`)
- compound: `arr[i] op= value`, `*p op= value`, `obj.f op= value`, `x op= value` — via `GenerateCompoundAssignmentExpr` -> `GenerateCompoundIndirectExpr` / `GenerateCompoundIdentExpr`.
- **`CheckConstTarget` at the beginning** — the single point of `const` checking.
- **`EmitCastToType` after each write** — overflow for narrow types.

Specifics of `GenerateBinaryOp`:
- Comparisons — `Cmp` + jumps -> `1`/`0`.
- `&&`/`||` — `GenerateLogicalAndOr`, result in `r0`, then `Mov(targetReg, r0)`.
- Constant folding: `NumberNode op NumberNode`.
- Optimization `targetReg == left operand`: right into `r1`/`r2`, `ApplyPointerArithmeticScaling` for `+`/`-` with pointers.
- Pointer arithmetic: `ptr ± int` — scaling `int` by `sizeof(*ptr)`; `int + ptr` — the same. `ptr - ptr` is not scaled.

**`GenerateIncrement`** — `++`/`--` for `Identifier`/`ArrayAccess`/`Dereference`/`MemberAccess`. At the beginning — `const` checks. After `Inc`/`Dec` and after load — `EmitCastToType`.

**`SaveRegToStack`/`RestoreRegFromStack`** — `ExpressionSaveDepth`, slot = `TotalLocalSize + ExpressionSaveDepth * 8`, address via `ADDR_SCRATCH_REG_EN = r27`.

**`StatementGenerator`** — `GenerateBlock`, `GenerateIf`, `GenerateWhile`, `GenerateFor`, `GenerateDoWhile`, `GenerateReturn` (with `EmitCastToType` to the return type), `GenerateBreak`, `GenerateContinue`. For loops — `_loopStack`.
- **`GenerateAssignment` calls `_exprGen.CheckConstTarget` at the beginning**.
- **`GenerateCompound*` — `GenerateCompoundArrayElement` / `GenerateCompoundDeref` / `GenerateCompoundMember`** with `IsArrayElemSigned`/`IsPointedSigned`/`IsFieldSigned`.

### Assembler

**`AssemblerBase`** — emitter abstraction. Public properties:
- `BaseAddress` — address for jump patching.
- `ErrorReporter` — public readonly field.
- `CodeSectionSize` / `DataSectionStart` / `DataSectionSize` — `{ get; protected set; }`, filled in `Build()`.

**`Assembler`** — direct writing to `MemoryStream`:
- `EmitInstruction(uint)` — 4 bytes.
- `EmitInstruction64(uint, ulong)` — 4 + `Align8()` + 8.
- `EmitInstruction64WithLabel(uint, string)` / `EmitJump(uint, string)` — 4 + `Align8()` + placeholder, patch in `Build()`.
- `EmitJumpToAbsolute(uint, ulong)` — 4 + `Align8()` + 8, no patch.
- `EmitData(string, byte[])` — `Align8()`, `MarkLabel`, write.
- `Build()` — fixes `DataSectionStart`/`DataSectionSize`/`CodeSectionSize`, patches `_patches`.
- `Align8()` — `stackalloc byte[pad]` (0..7 bytes).

**`AssemblerParser`** — text assembler. **Full instruction set:**
- No operands: `NOP`, `END`, `RET`, `HALT`, `WAKE`.
- R-format (2 registers): `MOV`, `MOD`, `ADD`, `SUB`, `AND`, `OR`, `XOR`, `IN`, `OUT`, `SHR`, `SHL`, `DIV`, `MULT_INT`, `CMP`, `TEST`.
- U-format (1 register): `JMP_IND`, `CALL_IND`, `WAKE_INT`, `INC`, `DEC`, `NOT`, `PUSH`, `POP`.
- **I+size format (1 register + `DataSize`): `CASTZ[.S8|.S16|.S32|.S64]`, `CASTS[...]`** — new `FormatIWithSize`, emits `EmitInstI`.
- `LDI reg, const|label` — constant (`0x...` hex) or label.
- `LOAD[.size] reg, addr` / `LOAD_UNSAFE ...`, `STORE[.size] reg, addr` / `STORE_UNSAFE ...`.
- `LOAD_IND[.size] regDst, regAddr` / `STORE_IND[.size] regSrc, regAddr` (+unsafe).
- `JMP`/`JZ`/`JNZ`/`JG`/`JL`/`CALL` — label or absolute address.

**Size normalization:** `NormalizeSizeExtension` fixes `LOAD . S8` -> `LOAD.S8` via `[GeneratedRegex(@"(\w)\s*\.\s*(\w)")]` (AOT-safe). There is a fast path: if there is no `' . '`, it returns the string without allocations.

**Parser state is instance-level:** `_asm`, `_allowDataSection`, `_pendingData`, `_inDataSection`, `_pendingLabel`, `_currentLine` are mutated in `Assemble`. **Not thread-safe** (see item 16).

**`AsmLanguageDefinition`** — `Mnemonics`/`Registers` (`FrozenSet`), regexes without `Compiled`. `_regMapLex`/`_opMapLex` — regular `Dictionary`, **plus** their `FrozenDictionary` copies `_regMap`/`_opMap` (see item 14).

**`IRAssembler`** — list of `AsmItem`:
- `EmitInstruction64(uint, ulong)` validates `HasNeed64IntData(op)`.
- `EmitJump(uint, string)` -> `AsmInstruction` with `TargetLabel`.
- `EmitJumpToAbsolute(uint, ulong)` -> `AsmInstruction(..., isAbsolute: true)`.
- `EmitData(string, byte[])` -> padding `AsmData(alignment: 8)` + `MarkLabel` + `AsmData(data, alignment: 1)`.
- `MarkLabel` — duplicates via `HashSet<string>`; on collision — `Asm_DuplicateLabel` with position.
- `Build()` — two-pass: `labelAddresses`, then bytes and jump patching; `emptyPadding = new byte[7]`.
- `GetItems()` — **internal** `List<AsmItem>`, mutations are visible to `Build()`.

**`InstructionEncoder`** — encoding of a 32-bit word:
- `Encode`/`EncodeR`/`EncodeI`/`EncodeU`/`EncodeJ` — formats.
- Specialized: `EncodeLDI`/`EncodeLOAD`/`EncodeSTORE`/`EncodeLOAD_IND`/`EncodeSTORE_IND`/`EncodeCALL`/`EncodeRET`/`EncodeHALT`/`EncodeWAKE`/`EncodeEND`/`EncodeNOP`.
- **`Decode(uint)`** — covers all opcodes, including `LOAD_UNSAFE`/`STORE_UNSAFE`/`LOAD_IND_UNSAFE`/`STORE_IND_UNSAFE`/`CMP`/`TEST`/`WAKE_INT`/`CASTZ`/`CASTS` (`CASTZ.S8 r0`, `CASTS.S32 r1`).
- Extension blocks: `OpCode.ToUint()`/`OpName()`, `RegType.ToUint()`/`RegName()`, `OpCodeSize.ToUint()`/`SizeName()`.
- Extension block on `BinaryWriter`: `WriteInstruction(uint)`, `Align8()`, `WriteInstructionWithData64(uint, ulong)`. **Not used in `Assembler`** (it has its own methods), but available for custom backends.

**`AsmExtensions`** — extension block on `AssemblerBase`. The code generator works only through these methods:
- R-format: `EmitMov`, `EmitMult`, `EmitAdd`, `EmitSub`, `EmitMultInt`, `EmitDiv`, `EmitAnd`, `EmitOr`, `EmitXOr`, `EmitShl`, `EmitShr`, `EmitMod`, `EmitCmp`.
- U-format: `EmitPush`, `EmitPop`, `EmitNot`, `EmitInc`, `EmitDec`.
- Special: `EmitLdi(ulong)`, `EmitLdi(string)`, `EmitLoadInd`, `EmitStoreInd`, `EmitLoad`, `EmitStore` (2 overloads).
- Jumps: `EmitJmp`, `EmitJz`, `EmitJnz`, `EmitJl`, `EmitJg`, `EmitCall`; `EmitInstJAbsolute`.
- No operands: `EmitEnd`, `EmitRet`, `EmitNop`, `EmitHalt`, `EmitWake`.
- General: `EmitInst`, `EmitInstU`, `EmitInstR`, **`EmitInstI`** (used for `CASTZ`/`CASTS`), `EmitInstJ`, `EmitInstJAbsolute`, `EmitInstWithData`.

**`CodeGenUtils.EmitCastToType(reg, size, isSigned)`** — extension on `AssemblerBase`:
- `size == S64` -> no-op;
- `isSigned == true` -> `EmitInstI(OpCode.CASTS, reg, size)`;
- `isSigned == false` -> `EmitInstI(OpCode.CASTZ, reg, size)`.

### Optimizations

**`AstOptimizer`** — `Add`/`Insert`/`Remove`/`RemoveAt`, `Count`, `GetType(i)`. `Optimize(program)` returns `IEnumerable<IReadOnlyLogOptimization>` — one per rule. There are no rules in this batch.

**`IROptimizer`** — `Optimize(items, maxPasses = 5)` — until fixed point. Skips `AsmLabel`, applies the first matching rule. `Clear()` on all rules at the beginning.

## API examples

**Full pipeline:**

```csharp
using Nantuc.Compiler.C;
using Nantuc.Compiler.C.AST;
using Nantuc.Compiler.C.CodeGenerator;
using Nantuc.Compiler.C.Optimizations;
using Nantuc.Compiler.ASM.IR;
using Nantuc.Common;
using Nantuc.Diagnostics;

var ErrorReporter = new ErrorReporter();
string source = """
    const int LIMIT = 10;
    int main() {
        int x = 42;
        return x < LIMIT ? x : LIMIT;
    }
    """;

var tokens = new Lexer(source, ErrorReporter).Tokenize();
var program = new Parser(tokens, ErrorReporter).Parse();
new AstOptimizer().Optimize(program);

var structTable = StructLayout.Resolve(program.StructNodes, ErrorReporter);
var asm = new IRAssembler(baseAddress: 0x1000, ErrorReporter);
new FunctionGenerator(asm, structTable, ErrorReporter).Generate(program);

var items = asm.GetItems();
new IROptimizer().Optimize(items);

byte[] bytecode = asm.Build();
Console.WriteLine($"code: {asm.CodeSectionSize}, data: {asm.DataSectionSize} @ {asm.DataSectionStart}");
```

**Overflow of narrow types:**

```c
int main() { byte b = 250; b += 10; return b; }            // 4
int main() { byte b = 0; b--; return b; }                 // 255
int main() { char c = 32767; c += 1; return c; }          // -32768 (char = S16)
int main() { short s = 30000; s += 10000; return s; }     // -25536
int main() { ushort u = 65535; u += 1; return u; }        // 0
int main() { int i = 0x7FFFFFFF; i += 1; return i; }      // INT_MIN
byte f() { return 300; }                                  // 44
int g(byte x) { return x; } g(300);                       // x = 44
byte a[2]; a[0] = 250; a[0] += 10;                        // a[0] == 4
```

**Assignment-as-expression:**

```c
int main() { int x = 10; return (x >>= 1) * 3; }          // 15 (x = 5)
int main() { int x; return (x = 5) + 1; }                 // 6
int main() { int x; y = (x = 3) + 1; return x + y; }      // 7
int main() { int a[3]; int x = (a[1] = 25) + 5; ... }     // writes to a[1], not a[0]
```

**`const`:**

```c
const int LIMIT = 100;
int * const p = &a;              // p cannot be reassigned, *p can
const int *q = &a;               // q can, *q cannot
const int arr[3] = { 1, 2, 3 };  // arr[i] cannot

x = 6;              // ❌ CodeGen_AssignmentToConst (1316)
x++;                // ❌ CodeGen_IncrementOfConst  (1317)
(x = 6) + 1;        // ❌ too — via GenerateAssignmentExpr
arr[0] = 10;        // ❌
*p = 10;            // ❌ (if p = const int*)
p = &b;             // ❌ (if p = int* const)
```

**Direct assembler + NANASM:**

```csharp
var parser = new AssemblerParser();
byte[] bytecode = parser.Assemble("""
    .data
    msg:   .asciz "Hello, NANASM!\n"
    n:     .quad 0
    .text
    _start:
        LDI r0, msg
        LOAD.S8 r1, msg
        CASTS.S32 r1            ; sign extension
        END
    """, baseAddress: 0x1000, ErrorReporter);
```

**Disassembly:**

```csharp
InstructionEncoder.Decode(raw);   // "ADD r1, r2" / "CASTS.S32 r0" / no more "UNKNOWN"
```

## Proximity of the module to the user

Medium. A MiniC user works through `VmApplication.Compile(source)`; a language/extension author works here.

- Author of new syntax — `Lexer`/`Parser`/`ASTNode` (`C/AST/`).
- Author of optimizations — `IAstOptimizationRule`/`IOptimizerIRRule`.
- Author of a new backend — `AssemblerBase`.
- Author of a new ISA target — `InstructionEncoder` + `AsmExtensions`.
- Author of NANASM scripts — `AssemblerParser` with `.data`/`.bss`/`.text`/`.code` sections.

**What you should do:**
- Use `IRAssembler` for the production pipeline; `Assembler` — for tests.
- Custom optimizations — via `Add` in `AstOptimizer`/`IROptimizer`; rules are idempotent (`Clear()` on entry).
- For inline asm — `AsmLanguageDefinition.Mnemonics`/`Registers` as the source of truth.
- **`const`** — use as an immutability guarantee in APIs and internal data.
- **Overflow** — write to narrow types (byte/char/short/int/uint/ushort) as in normal C; casts are inserted by the code generator.
- `InstructionEncoder.Decode(uint)` — for debugging.

**What you should not do:**
- Rely on `FunctionNode.Body` for `extern` — it is `null!`.
- Rely on `RegisterAllocator` for >22 simultaneously live scalars.
- Use `MiniCLanguageDefinition.GetKeywordRegex()` — dead code.
- `.data` in inline asm — forbidden.
- `const` on `struct` fields — not supported.
- One `AssemblerParser` across multiple threads — not thread-safe.
- **Rely on compile-time folding of `const`** — it does not exist; `const int N = 10; int arr[N];` — parse error.

## Special information

### AST node catalog

All nodes are in **`Nantuc.Compiler.C.AST`**, files in `C/AST/`. The base `ASTNode` — only `NameNode`. `Parser.Parse()` returns `ProgramNode`.

**Root / declarations**

| Node | Contains | Created in |
|---|---|---|
| `ProgramNode` | `GlobalVarNodes`, `FunctionNodes`, `StructNodes`, `IncludesList` | `Parse()` |
| `StructDeclNode` | `Name`, `Fields: List<(type, name)>` | `struct Name { ... }` branch |
| `FunctionNode` | `Name`, `ReturnType`, `Parameters`, `Body`, `IsExternal`, `LocalSize`, **`ReturnIsPointer`** | `ParseFunction`, `extern` branch |
| `ParameterNode` | `Type`, `Name`, `IsPointer`, `PointedType`, `IsConst`, `IsPointedConst` | `ParseFunction` |
| `VariableNode` | `Type`, `Name`, `Initializer`, `ArrayInitializer`, `IsArray`, `ArraySize`, `IsPointer`, `PointedType`, `StructTypeName`, `IsConst`, `IsPointedConst` | `ParseVariableTail` |

**Statements**

| Node | Contains | Created in |
|---|---|---|
| `BlockNode` | `Statements` | `ParseBlock`, `ParseBody` |
| `IfNode` | `Condition`, `ThenBlock`, `ElseBlock?` | `ParseIf` |
| `WhileNode` | `Condition`, `Body` | `ParseWhile` |
| `DoWhileNode` | `Body`, `Condition` | `ParseDoWhile` |
| `ForNode` | `Init`, `Condition`, `Increment`, `Body` | `ParseFor` |
| `ReturnNode` | `Value?` | `ParseReturn` |
| `BreakNode` / `ContinueNode` | — | `ParseBreak` / `ParseContinue` |
| `InlineAsmNode` | `AsmCode: string` | `ParseInlineAsm` |

**Expressions**

| Node | Contains | Created in |
|---|---|---|
| `NumberNode` | `Value: long` | `ParsePrimary` |
| `IdentifierNode` | `Name` | `ParsePrimary` |
| `AssignmentNode` | `Name`, `IndexExpr?`, `LValue`, `CompoundOp?`, `Value` | `ParseAssignment` |
| `BinaryOpNode` | `Operator`, `Left`, `Right` | `ParseLogicalOr`/`And`, `ParseBitwiseOr`/`Xor`/`And`, `ParseEquality`, `ParseRelational`, `ParseShift`, `ParseAdditive`, `ParseMultiplicative`, `ParseComma` |
| `UnaryOpNode` | `Operator`, `Operand` | `ParseUnary` |
| `IncrementNode` | `Target`, `Delta`, `IsPrefix` | `ParseUnary` (prefix), `ParsePrimary` (postfix) |
| `FunctionCallNode` | `Name`, `Arguments` | `ParsePrimary` |
| `IndirectCallNode` | `Callee`, `Arguments` | `ParsePrimary` (postfix call) |
| `ArrayAccessNode` | `ArrayName`, `Index` | `ParsePrimary` |
| `AddressOfNode` / `DereferenceNode` | `Operand` | `ParseUnary` |
| `MemberAccessNode` | `Object`, `FieldName`, `IsArrow` | `ParsePrimary` |
| `SizeofTypeNode` / `SizeofExprNode` | `TypeName`/`IsPointer`, `Operand` | `ParseUnary` |

Total 28 descendants. **Who walks the AST in the code generator:** `ExpressionGenerator`, `StatementGenerator`, `FunctionContext.ComputeMaxBinaryDepth`, `RegisterAllocator` (`CollectVarEvents`, `CollectAddressTakenVars`), `FunctionGenerator.CollectLocalVars`. When adding a node — update all these places.

### Overflow of narrow integer types **[NEW]**

The code generator emits `CASTZ`/`CASTS` (opcodes 43/44) after writing to a variable/register/field/array element/return — so that the value in the register is always in the canonical form of its type.

**Canonical forms:**

| Type | `OpCodeSize` | Signed | Range |
|---|---|---|---|
| `byte` | S8 | no | `0..255` (upper 56 bits — zeros) |
| `char` | **S16** | yes | `−32768..32767`, sign-extended |
| `short` | S16 | yes | `−32768..32767` |
| `ushort` | S16 | no | `0..65535` |
| `int` | S32 | yes | `−2³¹..2³¹−1`, sign-extended |
| `uint` | S32 | no | `0..2³²−1` |
| `long` | S64 | yes | as is |
| `ulong` | S64 | no | as is |

> **`char` in MiniC is 16-bit signed (`S16`)**, not 8-bit as in standard C. Consequences: `sizeof(char) == 2`; `char c = 120; c += 100;` -> `220` (fits in int16); `char c = 32767; c += 1;` -> `−32768` (wrap).

**Where casts are inserted (`Nantuc.Compiler.C.CodeGenerator`):**

| Event | Cast |
|---|---|
| `LOAD[.size]` from memory | `LOAD` zero-extends -> **`CASTS.S<size>`** for signed types |
| `STORE[.size]` to memory | `STORE` itself truncates — cast not needed |
| `x = value`, `x op= value`, `x++` (register) | after the operation — `CASTZ`/`CASTS` in the home register |
| `(x = value)`, `(x op= value)`, `x++` in an expression | after the operation — `CASTZ`/`CASTS` in `targetReg` |
| `arr[i] = value`, `*p = value`, `obj.f = value` | after load and after the operation — `CASTZ`/`CASTS` |
| `arr[i]++`, `(*p)++`, `(obj.f)++` | after load and after `INC`/`DEC` — `CASTZ`/`CASTS` |
| Call-site (argument -> `r0..r3`) | `CASTZ`/`CASTS` to the parameter type |
| Prologue (parameter 4+ from pseudo-global) | after `LOAD` — `CASTZ`/`CASTS` |
| `RETURN` (value in `r0`) | before `JMP epilogue` / `END` — `CASTZ`/`CASTS` to the return type (non-pointer scalar) |

**What is NOT cast:**
- Intermediate arithmetic result — C semantics: `byte + byte` yields `int`; truncation only on the next write.
- `long`/`ulong` — `S64`, `EmitCastToType` no-op.
- Pointers and structs.

**Implementation:**
- `CodeGenUtils.IsSignedType(type)` — `char/short/int/long` -> `true`.
- `CodeGenUtils.EmitCastToType(reg, size, isSigned)` — extension on `AssemblerBase`.
- `VarLocation.IsSigned` / `GlobalInfo.IsSigned` — computed in `FunctionContext.Create` / `GlobalMemoryManager.Allocate`.
- `VariableAccessor.LoadToRegister` — `CASTS` after `LOAD`; `StoreFromRegister` — cast of the home register.

### Constness (`const`) — semantics and limitations

`const` in MiniC is a **compile-time immutability guarantee** (not .rodata, not constant folding). The value is stored normally; the write prohibition is diagnosed at the code generation stage.

**Supported forms**

| Declaration | What is forbidden | `VariableNode` fields |
|---|---|---|
| `const int x` | `x = ...; x++; x += ...;` | `IsConst = true` |
| `int * const p` | `p = ...; p++; p += ...;` (`*p = ...` allowed) | `IsConst = true`, `IsPointedConst = false` |
| `const int *p` | `*p = ...; (*p)++; p[i] = ...` (`p = ...` allowed) | `IsConst = false`, `IsPointedConst = true` |
| `const int * const p` | both | both `true` |
| `const int arr[N]` | `arr[i] = ...; arr[i]++` | `IsPointedConst = true`, `IsConst = false` |
| `void f(const int x)` | `x = ...` | `ParameterNode.IsConst = true` |
| `void f(const int *p)` | `*p = ...` | `ParameterNode.IsPointedConst = true` |

**Syntax:** `Parser.ParseDeclSpec` -> `(type, isPointer, pointedType, prefixConst, ptrConst)`; `[const] type`, `type const`, `*`, `[* const]`.

**Checks in the code generator:**
- `ExpressionGenerator.CheckConstTarget(assign)` — **internal, single point**; called from `GenerateAssignmentExpr` (expression context) and from `StatementGenerator.GenerateAssignment` (statement context).
  - `assign.IndexExpr != null` -> `IsPointedConst(name)`;
  - `assign.LValue is DereferenceNode` -> `IsPointedConst(ptrName)`;
  - `assign.LValue is MemberAccessNode` -> currently skipped (TODO, item 4);
  - simple name -> `IsConst(name)`.
- `ExpressionGenerator.GenerateIncrement` — `IsConst`/`IsPointedConst` for `Identifier`/`ArrayAccess`/`Dereference`.

**New `ErrorCode`s:**
```csharp
CodeGen_AssignmentToConst = 1316,   // "Cannot assign to const '{0}'"
CodeGen_IncrementOfConst  = 1317,   // "Cannot increment/decrement const '{0}'"
```

**Not supported:**
1. `const` on `struct` fields — `CheckConstTarget` for `MemberAccessNode` does `return` (TODO).
2. `const` at call-site — `const int*` -> `int*` is not diagnosed.
3. Constant folding — `const int N; int arr[N];` — parse error.
4. `const` in `extern` declarations.
5. `&x` from `const x` yields a regular `int*`.

**With registers:** `RegisterAllocator` does not exclude `const` from candidates — writing is blocked at the code generation level, before the `Store`/`Inc`/`Dec` instruction. `&x` — `CollectAddressTakenVars` ensures such variables live in memory.

### WASM / cross-platform

| Aspect | Status | Comment |
|---|---|---|
| Build to WASM | ✅ | No `unsafe`, `DllImport`, `LibraryImport`. `stackalloc` only in `Assembler.Align8` (0–7 bytes). |
| `NativeMemory` | ✅ | Not used. |
| `stackalloc` / `InlineArray` | ✅ | `stackalloc byte[pad]` (0..7 bytes) in `Assembler.Align8`. |
| `Interlocked` / `Volatile` | ✅ | Not used. |
| `System.Collections.Frozen` | ✅ | `FrozenSet` + **`FrozenDictionary`** (`_regMap`/`_opMap`). |
| `[GeneratedRegex]` | ✅ | `AssemblerParser.GetRexExtensionAsm()` — the only regex in the hot path. |
| `Regex` without `Compiled` | ✅ | `AsmLanguageDefinition._mnemonicsRegex`/`regex` — without `Compiled`. |
| `Enum.ToString()` | ⚠️ | **Removed from diagnostics** — replaced with `TokenTypeExtension.AsText` / `OpCode.OpName()`. |
| `DiagnosticData` | ✅ | Ready string at creation, no boxing or reflection. |
| `AsmInstruction(uint, ulong, bool)` | ⚠️ | `TargetLabel = absoluteTarget.ToString()` — fallback, dead branch (item 10). |
| Trimming | ⚠️ | `using System.Security.Cryptography;` in one of the `C/AST/` files — unused, but sloppy. |
| AOT | ⚠️ | Main risk — `RegexOptions.Compiled`. |
| `switch` on `OpCode`/`TokenType` | ✅ | Jump-table, AOT-friendly. |

**Bottom line:** builds for WASM and AOT if `RegexOptions.Compiled` is removed.

### Compilation pipeline

```
source (MiniC)
  │
  ▼
Lexer.Tokenize() -> List<Token>                  // <<=, >>= — 3-character
  │
  ▼
Parser.Parse() -> ProgramNode                     // C.AST; 28 nodes
  │  (ParseDeclSpec: [const] Type [* [const]]; ParseFunction: isReturnPointer)
  │
  ├── StructLayout.Resolve(program.StructNodes)
  │
  ▼
FunctionGenerator.Generate(program)
  ├── GlobalMemoryManager.Allocate(...) — IsSigned is computed
  ├── GenerateGlobalInit (scalars only)
  ├── EmitJmp("func_main")
  ├── foreach func in FunctionNodes:
  │     ├── CollectLocalVars(func.Body)
  │     ├── FunctionContext.Create(...) -> VarMap + IsSigned + ReturnSize/ReturnIsSigned
  │     ├── RegisterAllocator.AllocateRegisters(...) — &var -> stack
  │     ├── prologue: push savedRegs, sub rSP, stackAlloc, params -> registers
  │     ├── StatementGenerator.GenerateBlock(func.Body)
  │     └── epilogue: add rSP, pop savedRegs, ret / END for main
  └── EmitData for globals and pseudo-globals
  │
  ▼
AssemblerBase (Assembler | IRAssembler)
  │  Assembles in Build():
  │  - label patching
  │  - DataSectionStart / DataSectionSize / CodeSectionSize
  │
  ▼
byte[] (32-bit ISA words + 8-aligned data)
```

### Instruction format and `InstructionEncoder`

32-bit ISA word:

```
Bits  0-7   : OpCode         (8 bits, up to 256 opcodes)
Bits  8-12  : Reg1           (5 bits, destination/main register)
Bits 13-17  : Reg2           (5 bits, second source register)
Bits 18-19  : OpCodeSize     (2 bits: 0=S8, 1=S16, 2=S32, 3=S64)
Bits 20-31  : unused
```

**Formats:**

| Format | Method | Examples | Operands |
|---|---|---|---|
| R | `EncodeR(op, regDst, regSrc)` | `MOV`, `ADD`, `SUB`, `AND`, `OR`, `XOR`, `MUL`, `DIV`, `MOD`, `SHL`, `SHR`, `CMP`, `TEST`, `IN`, `OUT` | 2 registers |
| R+size | `Encode(op, regDst, regSrc, size)` | `LOAD_IND`, `STORE_IND` (+unsafe) | 2 registers + size |
| I | `EncodeI(op, reg, size)` | `LDI`, `LOAD`, `STORE` (+unsafe), **`CASTZ`, `CASTS`** | 1 register + size (+64-bit immediate after alignment for LDI/LOAD/STORE) |
| U | `EncodeU(op, reg)` | `INC`, `DEC`, `NOT`, `PUSH`, `POP`, `CALL_IND`, `JMP_IND`, `WAKE_INT` | 1 register |
| J | `EncodeJ(op)` | `JMP`, `JZ`, `JNZ`, `JG`, `JL`, `CALL` | 64-bit address after alignment |
| — | `EncodeEND/NOP/RET/HALT/WAKE` | — | no operands |

**64-bit operands:** after the instruction — `Align8()`, then 8 bytes little-endian.

**`Decode(uint)`** — reverse decomposition (for debugging):
```csharp
InstructionEncoder.Decode(raw);   // "ADD r1, r2" / "LOAD.S32 r0, [data64]" / "CASTS.S16 r5"
```

### NANASM assembler: data sections

**Section directives**

| Directive | Action |
|---|---|
| `.data`, `.bss`, `.section .data`, `.section .bss` | open the data section |
| `.text`, `.code`, `.section .text`, `.section .code` | close the data section |

`.bss` is semantically identical to `.data`.

**Data directives** (only inside `.data`/`.bss`)

| Directive | Alias | Width | Example | Result |
|---|---|---|---|---|
| `.byte` | `db` | 1 | `.byte 1, 2, 3` | `[01 02 03]` |
| `.byte` | — | 1 | `.byte "abc"` | `[61 62 63]` |
| `.word` | `dw` | 2 | `.word 0x1234` | `[34 12]` |
| `.dword` | `dd` | 4 | `.dword 0xDEADBEEF` | `[EF BE AD DE]` |
| `.quad` | `dq` | 8 | `.quad 0x...` | 8 bytes little-endian |
| `.ascii` | — | 1 | `.ascii "Hi"` | `[48 69]` |
| `.asciz` | `.string` | 1 (+NUL) | `.asciz "Hi"` | `[48 69 00]` |
| `.zero` | `.space` | N | `.zero 256` | 256 zero bytes |

**Limitations**

| Limitation | Reason | What to do |
|---|---|---|
| `.data` forbidden in inline asm | `allowDataSection: false` | Move `.data` into a NANASM file |
| `.quad <label>` not supported | No label patching in `EmitData` | `.quad 0` + initialization in `__global_init` |
| `.align N` not supported | `EmitData` always aligns to 8 | Rely on auto-8 |
| `;` in a string is truncated | `Split(['\n', ';'])` | Do not use `;` in `.ascii`/`.asciz` |
| Little-endian only | Nantuc platform | — |

### Diagnostics: `DiagnosticData` and AOT

Diagnostic arguments — `ReadOnlySpan<DiagnosticData>` (not `object[]`):

```csharp
public readonly record struct DiagnosticData
{
    public string Text { get; }
}
```

What this gives for AOT:
- No boxing of `int`/`char`/enum.
- No virtual `Object.ToString()` — the string is already ready.
- `Enum.ToString()` is removed from diagnostics (`TokenTypeExtension.AsText` / `OpCode.OpName()`).
- `Type.Name` is not passed to diagnostics.

**Bottom line:** `DiagnosticData` is fully AOT-safe.

### `Assembler` vs `IRAssembler`

| | `Assembler` | `IRAssembler` |
|---|---|---|
| Storage | `MemoryStream` + `BinaryWriter` | `List<AsmItem>` |
| Label patching | `_patches` in `Build()` | `labelAddresses` in the first pass |
| IR | ❌ | ✅ |
| `HasNeed64IntData` check | ❌ (only in `EmitInstruction64`; `EmitInstruction64WithLabel` does not check — item 15) | ✅ in `EmitInstruction64` (also not in `EmitInstruction64WithLabel`) |
| Duplicate label validation | ✅ | ✅ |
| Two-pass Build | ❌ | ✅ |
| `.data` via `AssemblerParser` | ✅ | ✅ |
| `EmitData` | `Align8()` + `MarkLabel` + bytes | padding `AsmData` + `MarkLabel` + `AsmData(data)` |
| `GetItems()` mutable | — | ✅ (internal `List<AsmItem>`) |

## What to fix

In descending order of importance.

### `const` — incomplete semantics

1. **[CONST] `CheckConstTarget` for `MemberAccessNode` — TODO.**  
   File: `ExpressionGenerator.CheckConstTarget`: `if (assign.LValue is MemberAccessNode) return;   // TODO`.  
   **Consequence:** `struct Point p; p.x = 5` is not checked, even if the field is declared as `const int x` (which is currently impossible — `const` on struct fields is also not supported).  
   **Fix:** extend `FieldInfo` with `IsConst`/`IsPointedConst` fields, pass through `StructLayout`, check in `CheckConstTarget`.

2. **[CONST] `const` qualifier in `extern` declarations — not parsed.**  
   File: `Parser.Parse`, `extern` block. Skips `*`, but not `const`.  
   **Fix:** symmetrically to `ParseDeclSpec`.

### API / Defects

4. **[API] `StatementGenerator.GenerateAssignment` — case 4 (direct assign) — dead code.**  
   The condition `assign.LValue == null` never holds: `Parser.ParseAssignment` for `IdentifierNode` creates `new AssignmentNode(...) { LValue = left, ... }` — `LValue` is always set.  
   **Fix:** replace with `assign.LValue is IdentifierNode` or `assign.Name != null && assign.IndexExpr == null && assign.CompoundOp == null`.

7. **[API] `AsmInstruction(uint, ulong, bool)` — fallback via `ToString()`.**  
   `TargetLabel = absoluteTarget.ToString()` — dead branch (`IRAssembler.EmitJumpToAbsolute` always `isAbsolute: true`).  
   **Fix:** remove `isAbsolute` from the constructor, split by argument type.

8. **[API] `ExpressionGenerator.GenerateBinaryOp` — `&&`/`||` into `targetReg` via `Mov`.**  
   **Fix:** `GenerateLogicalAndOr(binop, targetReg)`.

9. **[API] `AssemblerParser.TryAddIntegerData` — `.quad <label>` is not supported yet.**  
   **Fix:** label patching mechanism in `EmitData`/`Build()`.

10. **[API] `AssemblerParser.TryAddStringData` — `;` inside a string is truncated.**  
    **Fix:** move string parsing out of the common `Split` or add hex escape.

11. **[API] `AssemblerParser` — double dictionaries `_regMapLex`/`_opMapLex` + `_regMap`/`_opMap`.**  
    `_regMapLex`/`_opMapLex` are dead after the static constructor.  
    **Fix:** initialize `FrozenDictionary` directly.

12. **[API] `IRAssembler.EmitInstruction64WithLabel` does not check `HasNeed64IntData`.**  
    **Fix:** move the check into a common helper `Validate64BitOp(op)`.

13. **[API] `AssemblerParser` is not thread-safe.**  
    Fields are instance-level. `StatementGenerator.GenerateInlineAsm` creates `new AssemblerParser()` for each `asm` — but the contract is not documented.  
    **Fix:** `[ThreadStatic]`/`lock`, or explicitly document.

14. **[API] `RegisterAllocator.BuildInterferenceGraph` — linear live ranges, does not account for loops.**  
    **Fix:** document as “best effort” or implement real live-range analysis.

15. **[API] `FunctionGenerator.GenerateGlobalInit` — initialization of structs is silently unsupported.**  
    `if (CodeGenUtils.IsStructType(global.Type, _structTable)) continue;` — a global struct with an `Initializer` is ignored.  
    **Fix:** support it or throw `NotSupported`.

### Housekeeping

16. **[Housekeeping] AST is split into files in `C/AST/`, but without a “map” in the folder header.**  
    **Fix:** add `C/AST/README.md` or a `#region` map.

### XML

17. **[XML] AST hierarchy — without `///`.**  
    Including `VariableNode.IsConst`/`IsPointedConst`, `ParameterNode.IsConst`/`IsPointedConst`, `FunctionNode.ReturnIsPointer`.

18. **[XML] `ExpressionGenerator`, `StatementGenerator`, `FunctionGenerator` — without `///`.**  
    Especially `CheckConstTarget`, `GenerateIncrement`, `GenerateAssignmentExpr`.

19. **[XML] `CodeGenUtils` — without `///`.**  
    `IsSignedType`, `EmitCastToType`, `GetAlignment(type, structTable)` (hardcoded `8` for structs).

20. **[XML] `AssemblerParser` — class header does not list `.data`, `allowDataSection`, `CASTZ`/`CASTS`.**
