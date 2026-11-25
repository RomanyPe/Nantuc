# Nantuc.Compiler — README

> Compiler MiniC -> Nantuc bytecode: lexer, parser, AST, code generator with register coloring, assembler (two backends — direct and IR) and optimization infrastructure.
> [← Root](../README.md), [Common](../../src/Nantuc.Common/README.md), [Contracts](../../src/Nantuc.Contracts/README.md), [Kernel](../../src/Nantuc.Kernel/README.md)

## Contents
- [About this module](#about-this-module)
- [Local project tree](#local-project-tree)
- [API](#api)
- [Examples for the API](#examples-for-the-api)
- [Proximity of the module to the user](#proximity-of-the-module-to-the-user)
- [Special information](#special-information)
  - [Catalog of AST nodes](#catalog-of-ast-nodes)
  - [Overflow of narrow integer types](#overflow-of-narrow-integer-types)
  - [Constness (`const`) — semantics and limitations](#constness-const--semantics-and-limitations)
  - [NANASM assembler: data sections](#nanaasm-assembler-data-sections)
  - [Instruction format and `InstructionEncoder`](#instruction-format-and-instructionencoder)
  - [Diagnostics: `DiagnosticData` and AOT](#diagnostics-diagnosticdata-and-aot)
- [What to fix](#what-to-fix)

## About this module

`Nantuc.Compiler` is the frontend and backend of the Nantuc platform compiler. The frontend accepts source in MiniC (a subset of C: integer types, `struct`, pointers, arrays, `sizeof`, `asm` blocks, **`const` qualifiers**, **overflow semantics of narrow integers**), builds an AST and generates bytecode through a two-stage scheme: code generator -> assembler -> 32-bit ISA words. The backend is two-tier: either direct writing to a `MemoryStream` (`Assembler`), or building a list of IR instructions (`IRAssembler`) with the possibility of IR optimizations.

The module depends on `Common` (`OpCode`/`RegType`/`OpCodeSize`/`InstructionDecoder`), `Contracts` (`OptimizationId`/`OptimizationLogEntry`/`IReadOnlyLogOptimization`) and `Diagnostics` (`ErrorReporter`/`ErrorCode`, **`DiagnosticData`**). It contains no `unsafe` code itself — `stackalloc` only in `Assembler.Align8` (0–7 bytes, WASM-safe).

Key roles of the module:
- Frontend: `Lexer` (`Nantuc.Compiler.C.Lexer`), `Parser` (partial, 6 files, `Nantuc.Compiler.C.Parser`), **`Nantuc.Compiler.C.AST`** — 28 AST nodes in separate files, `MiniCLanguageDefinition` (`Nantuc.Compiler.C`), **`TokenTypeExtension`** (`Nantuc.Compiler.C.Lexer`).
- Code generator: `ExpressionGenerator` (partial, 12 files, `Nantuc.Compiler.C.CodeGenerator.Expression`), `StatementGenerator` (partial, 6 files + `StatementTypeResolver`, `Nantuc.Compiler.C.CodeGenerator.Statement`), `FunctionGenerator` (partial, 5 files, `Nantuc.Compiler.C.CodeGenerator.FunctionGen`), `RegisterAllocator` + `GreedyGraphColorer` + `VariableAccessAnalyzer` + `RegisterAllocationResult` (`Nantuc.Compiler.C.CodeGenerator.RegistersAlloc`), `FunctionContext` + `SaveDepthEstimator` + `VariableLayoutBuilder` (`Nantuc.Compiler.C.CodeGenerator.FunctionCtx`), `VariableAccessor`, `GlobalMemoryManager`, `CodeGenUtils`, `VarLocation`, `GlobalInfo` (`Nantuc.Compiler.C.CodeGenerator`), **overflow casts via `CASTZ`/`CASTS`**.
- Assembler: `AssemblerBase` (abstraction, `Nantuc.Compiler.ASM.Base`), `Assembler`/`AsmLanguageDefinition` (`Nantuc.Compiler.ASM`), **`AssemblerParser` — partial of 5 files** (`Nantuc.Compiler.ASM.Parser`), **`IRAssembler` — partial of 3 files** (`Nantuc.Compiler.ASM.IR.Emitеer`), `AsmExtensions`/`InstructionEncoder` (`Nantuc.Compiler.ASM.Utils`), IR elements — `AsmItem`/`AsmLabel`/`AsmData`/`AsmInstruction`/`IOptimizerIRRule`/`IROptimizer` (`Nantuc.Compiler.ASM.IR`).
  **`AssemblerParser` supports `.data`/`.bss`/`.text`/`.code` sections and data directives** (`.byte`, `.word`, `.dword`, `.quad`, `.ascii`, `.asciz`, `.zero`).
- Optimizations: `AstOptimizer`, `IROptimizer` — framework with interfaces `IAstOptimizationRule`/`IOptimizerIRRule` (`Nantuc.Compiler.C.Optimizations` / `Nantuc.Compiler.ASM.IR`); **there are no concrete rules in this batch**.
- Struct layout: `StructLayout`, `FieldInfo` (`Nantuc.Compiler.C`) — field alignment by 8 bytes, `Resolve` with iterative dependency resolution.
- Auxiliary: **`WordRegexBuilder`** (`Nantuc.Compiler`) — a single point for building `\b(...)\b` regexes for `AsmLanguageDefinition` and `MiniCLanguageDefinition`.

> **XML documentation is fragmentary.** Fully documented: `AssemblerParser` (class header — but not synchronized with the new opcodes, see item 20), `Assembler` (partially), `IOptimizerIRRule`, `IAstOptimizationRule`, `InstructionEncoder` (format + regions). Without `///` — the entire AST hierarchy (28 types in `C/AST/`, only inline `//` comments in the file header), `Lexer`, **`Parser` and all its partial files** (`Parser.Declarations.cs`/`.Expressions.cs`/`.Literals.cs`/`.Statements.cs`/`.TopLevel.cs`), **`ExpressionGenerator` and all its partial files** (11 partials in `CodeGenerator/Expression/`), **`StatementGenerator` and all its partial files** (5 partials + `StatementTypeResolver.cs` in `CodeGenerator/Statement/`), **`FunctionGenerator` and all its partial files** (4 partials in `CodeGenerator/FunctionGen/`), **`FunctionContext`/`SaveDepthEstimator`/`VariableLayoutBuilder`** (`CodeGenerator/FunctionCtx/`), **`RegisterAllocator`/`GreedyGraphColorer`/`VariableAccessAnalyzer`** (`CodeGenerator/RegistersAlloc/`), `CodeGenUtils`, `GlobalMemoryManager`, `VariableAccessor`, **`IRAssembler` and its partials** (`IRAssembler.Layout.cs`/`IRAssembler.Emit.cs`), **`AssemblerParser` and its partials** (`AssemblerParser.Lexing.cs`/`.Instructions.cs`/`.DataSection.cs`/`.Numbers.cs`), `IROptimizer`, `AsmExtensions`, **`WordRegexBuilder`**. Details and defects — in the section [What to fix](#what-to-fix).

## Local project tree

```
Nantuc.Compiler/
├── Nantuc.Compiler.csproj            # net10.0, refs Common+Contracts+Diagnostics
├── README.md
├── README.ru.md
├── WordRegexBuilder.cs               # Nantuc.Compiler — \b(...)\b regex builder (Asm + MiniC)
│
├── ASM/                              # Backend (namespace Nantuc.Compiler.ASM)
│   ├── AsmLanguageDefinition.cs      # Mnemonics/Registers (FrozenSet); regexes without Compiled
│   ├── Assembler.cs                  # direct writing to MemoryStream + label patching
│   │
│   ├── Base/                         # Nantuc.Compiler.ASM.Base
│   │   └── AssemblerBase.cs          # BaseAddress/ErrorReporter/SectionSize/DataSectionStart
│   │
│   ├── Parser/                       # Nantuc.Compiler.ASM.Parser
│   │   ├── AssemblerParser.cs        # fields, Assemble/Build, PendingData, [GeneratedRegex]
│   │   ├── AssemblerParser.Lexing.cs # ProcessLines/StripComments/sections/FlushPendingData;
│   │   │                             # NormalizeSizeExtension (GeneratedRegex, fast-path)
│   │   ├── AssemblerParser.Instructions.cs # ParseInstruction/SplitMnemonicAndSize/DispatchInstruction;
│   │   │                             # FormatR/FormatU/FormatIWithSize/FormatLDI/FormatJumpType/
│   │   │                             # FormatLOAD_STORE/FormatLOAD_IND_STORE_IND
│   │   ├── AssemblerParser.DataSection.cs # TryProcessDataDirective; TryAddIntegerData/StringData;
│   │   │                             # AddPendingData
│   │   └── AssemblerParser.Numbers.cs # ParseReg; TryParseSize; TryParseLong; ParseNumber; IsNumber
│   │
│   ├── IR/                           # Nantuc.Compiler.ASM.IR
│   │   ├── AsmItem.cs                # base IR element
│   │   ├── AsmInstruction.cs         # auto-decodes fields via InstructionDecoder
│   │   ├── AsmLabel.cs               # label (Position)
│   │   ├── AsmData.cs                # data with alignment (alignment)
│   │   ├── IOptimizerIRRule.cs
│   │   ├── IROptimizer.cs
│   │   │
│   │   └── Emitеer/                  # Nantuc.Compiler.ASM.IR.Emitеer
│   │       ├── IRAssembler.cs        # Emit*/MarkLabel/GetItems/Build (entry point)
│   │       ├── IRAssembler.Layout.cs # pass 1: ComputeLabelAddresses; AdvanceInstructionOffset
│   │       └── IRAssembler.Emit.cs   # pass 2: EmitItem/EmitInstructionItem/EmitDataItem/PadTo
│   │
│   └── Utils/                        # Nantuc.Compiler.ASM.Utils
│       ├── AsmExtensions.cs          # Emit* + EmitInstI on AssemblerBase
│       └── InstructionEncoder.cs     # Encode/Decode (incl. CASTZ/CASTS)
│                                     # + extension blocks on RegType/OpCode/OpCodeSize
│
└── C/                                # Frontend (namespace Nantuc.Compiler.C)
    ├── MiniCLanguageDefinition.cs    # Nantuc.Compiler.C — Keywords/Operators (FrozenSet)
    ├── StructLayout.cs               # Nantuc.Compiler.C — StructLayout (Dictionary), FieldInfo
    │
    ├── Lexer/                        # Nantuc.Compiler.C.Lexer
    │   ├── Lexer.cs                  # Tokenize(), positioning
    │   ├── Lexer.Trivia.cs           # whitespace and comments
    │   ├── Lexer.Literals.cs         # identifiers, numbers, strings, characters
    │   ├── Lexer.Operators.cs        # operators (table-driven parsing)
    │   ├── Token.cs                  # Token (Type/Value/Line/Column)
    │   ├── TokenType.cs              # enum TokenType (+ Arrow)
    │   └── TokenTypeExtension.cs     # extension(TokenType).AsText — switch without reflection
    │
    ├── Parser/                       # Nantuc.Compiler.C.Parser
    │   ├── Parser.cs                 # partial class Parser — fields, Parse(), Advance(), Expect()
    │   ├── Parser.TopLevel.cs        # struct / #include / extern / global / function
    │   ├── Parser.Declarations.cs    # ParseDeclSpec/ParseDeclarators/ParseVariableTail/ParseFunction
    │   ├── Parser.Expressions.cs     # ParseAssignment..ParsePrimary (13 precedence levels)
    │   ├── Parser.Literals.cs        # Number/Char/String/Hex — literals
    │   └── Parser.Statements.cs      # ParseBlock/If/While/DoWhile/For/Return/Break/Continue/Asm
    │
    ├── AST/                          # Nantuc.Compiler.C.AST
    │   ├── ASTNode.cs
    │   ├── ProgramNode.cs
    │   ├── StructDeclNode.cs
    │   ├── FunctionNode.cs           # + ReturnIsPointer
    │   ├── ParameterNode.cs          # + IsConst/IsPointedConst
    │   ├── VariableNode.cs           # + IsConst/IsPointedConst
    │   ├── BlockNode.cs
    │   ├── IfNode.cs / WhileNode.cs / DoWhileNode.cs / ForNode.cs
    │   ├── ReturnNode.cs / BreakNode.cs / ContinueNode.cs / InlineAsmNode.cs
    │   ├── NumberNode.cs / IdentifierNode.cs
    │   ├── AssignmentNode.cs         # Name, IndexExpr, LValue, CompoundOp
    │   ├── BinaryOpNode.cs / UnaryOpNode.cs / IncrementNode.cs
    │   ├── FunctionCallNode.cs / IndirectCallNode.cs
    │   ├── ArrayAccessNode.cs / AddressOfNode.cs / DereferenceNode.cs
    │   ├── MemberAccessNode.cs       # IsArrow
    │   ├── SizeofTypeNode.cs / SizeofExprNode.cs
    │   └── CastNode.cs / CastNode.cs
    │
    ├── CodeGenerator/                # Nantuc.Compiler.C.CodeGenerator
    │   ├── CodeGenUtils.cs           # sizes/alignment + IsSignedType + EmitCastToType
    │   ├── GlobalInfo.cs             # + IsSigned
    │   ├── GlobalMemoryManager.cs    # registry of globals, IsSigned is computed
    │   ├── VariableAccessor.cs       # load/store with canonicalization via CASTZ/CASTS
    │   ├── VarLocation.cs            # + IsSigned
    │   │
    │   ├── FunctionCtx/              # Nantuc.Compiler.C.CodeGenerator.FunctionCtx
    │   │   ├── FunctionContext.cs    # context + ReturnSize/ReturnIsSigned/ReturnIsVoid
    │   │   ├── SaveDepthEstimator.cs # estimation of ExpressionSaveDepth from AST (pure function)
    │   │   └── VariableLayoutBuilder.cs # VarInfo -> VarLocation (stack/register)
    │   │
    │   ├── FunctionGen/              # Nantuc.Compiler.C.CodeGenerator.FunctionGen
    │   │   ├── FunctionGenerator.cs           # orchestrator
    │   │   ├── FunctionGenerator.DataSection.cs # data emission (globals + pseudo-params)
    │   │   ├── FunctionGenerator.Prologue.cs  # prologue + parameter marshaling
    │   │   ├── FunctionGenerator.Epilogue.cs  # epilogue
    │   │   ├── ConstantEvaluator.cs           # constant folding in initializers
    │   │   └── LocalVarCollector.cs           # AST walk for collecting local variables
    │   │
    │   ├── RegistersAlloc/           # Nantuc.Compiler.C.CodeGenerator.RegistersAlloc
    │   │   ├── RegisterAllocator.cs           # orchestrator (public API)
    │   │   ├── RegisterAllocator.Interference.cs # interference graph
    │   │   ├── GreedyGraphColorer.cs          # greedy coloring (Welsh–Powell)
    │   │   ├── VariableAccessAnalyzer.cs      # AST walkers (def/use, address-taken)
    │   │   └── RegisterAllocationResult.cs    # VarToRegister + UsedRegisters
    │   │
    │   ├── Expression/               # Nantuc.Compiler.C.CodeGenerator.Expression
    │   │   ├── ExpressionGenerator.cs            # root + GenerateExpression(expr[, targetReg])
    │   │   ├── ExpressionGenerator.Arrays.cs     # ArrayAccess/StoreR0ToArrayElement
    │   │   ├── ExpressionGenerator.Assignment.cs # GenerateAssignmentExpr + CheckConstTarget
    │   │   ├── ExpressionGenerator.Binary.cs     # GenerateBinaryOp + logical/compare
    │   │   ├── ExpressionGenerator.Calls.cs      # FunctionCall/IndirectCall + call-site casts
    │   │   ├── ExpressionGenerator.Conditions.cs # GenerateCondition (&&/||, comparisons, bool-context)
    │   │   ├── ExpressionGenerator.Helpers.cs    # SaveRegToStack/RestoreRegFromStack
    │   │   ├── ExpressionGenerator.Increment.cs  # GenerateIncrement + const checks
    │   │   ├── ExpressionGenerator.MemberAccess.cs # GenerateMemberAddress + arrow/dot
    │   │   ├── ExpressionGenerator.PointerOps.cs # ApplyPointerArithmeticScaling + AddressOf/Dereference
    │   │   ├── ExpressionGenerator.Sizeof.cs     # SizeofType/SizeofExpr
    │   │   └── ExpressionGenerator.Unary.cs      # GenerateUnaryOp (-, !, ~)
    │   │
    │   └── Statement/                # Nantuc.Compiler.C.CodeGenerator.Statement
    │       ├── StatementGenerator.cs             # root + switch over nodes
    │       ├── StatementGenerator.Assignments.cs # GenerateAssignment (4 lvalue branches)
    │       ├── StatementGenerator.CompoundAssignments.cs # x op= / arr[i] op= / *p op= / obj.f op=
    │       ├── StatementGenerator.ControlFlow.cs # If/While/DoWhile/For/Break/Continue/Return
    │       ├── StatementGenerator.Declarations.cs # local declarations + init
    │       ├── StatementGenerator.InlineAsm.cs   # InlineAsmNode -> new AssemblerParser()
    │       └── StatementTypeResolver.cs          # IsArrayElemSigned/IsPointedSigned/IsFieldSigned
    │
    └── Optimizations/                # Nantuc.Compiler.C.Optimizations
        ├── AstOptimizer.cs
        ├── IAstOptimizationRule.cs
        └── ILogOptimization.cs       # optimization log contract (+ OptimizationResult)
```

Notes on organization:
- The frontend (`C/*`) knows about bytecode through `OpCode`/`RegType`/`OpCodeSize`, but not about `Assembler`/`IRAssembler` — the code generator works through `AssemblerBase`-extension methods from `Utils/AsmExtensions.cs`/`Utils/InstructionEncoder.cs`.
- **AST nodes are placed in a separate namespace `Nantuc.Compiler.C.AST` and physically laid out in files in `C/AST/`** (one node per file, except the base `ASTNode.cs`).
- **`Lexer` is laid out in the `C/Lexer/` folder** (`Nantuc.Compiler.C.Lexer`): `Lexer` + 3 partials + `Token` + `TokenType` + `TokenTypeExtension`.
- **`Parser` is a partial class of 6 files in `C/Parser/`** (`Nantuc.Compiler.C.Parser`). The public API is one — `Parse()`; partial splitting is only for readability.
- **`ExpressionGenerator` is a partial class of 12 files in `CodeGenerator/Expression/`** (`Nantuc.Compiler.C.CodeGenerator.Expression`). **`StatementGenerator` is a partial class of 6 files in `CodeGenerator/Statement/`** (`Nantuc.Compiler.C.CodeGenerator.Statement`) + `StatementTypeResolver.cs`. The split was done for XML documentation and navigation; API methods did not change.
- **`FunctionGenerator` is a partial class of 5 files in `CodeGenerator/FunctionGen/`** (`Nantuc.Compiler.C.CodeGenerator.FunctionGen`): orchestrator + DataSection + Prologue + Epilogue + `ConstantEvaluator` + `LocalVarCollector`.
- **`FunctionContext`/`SaveDepthEstimator`/`VariableLayoutBuilder` are in `CodeGenerator/FunctionCtx/`** (`Nantuc.Compiler.C.CodeGenerator.FunctionCtx`).
- **`RegisterAllocator`/`RegisterAllocator.Interference`/`GreedyGraphColorer`/`VariableAccessAnalyzer`/`RegisterAllocationResult` are in `CodeGenerator/RegistersAlloc/`** (`Nantuc.Compiler.C.CodeGenerator.RegistersAlloc`).
- Two backends: `Assembler` (direct, `MemoryStream` + `BinaryWriter`) and `IRAssembler` (list of `AsmItem`). `AssemblerParser` works with either via `AssemblerBase`.
- **`AssemblerParser` is a partial class of 5 files in `ASM/Parser/`** (`Nantuc.Compiler.ASM.Parser`): `AssemblerParser.cs` (fields, `Assemble`/`Build`, `PendingData`, `[GeneratedRegex]`), `.Lexing.cs` (lines/comments/sections + `NormalizeSizeExtension` + `FlushPendingData`), `.Instructions.cs` (mnemonic parsing + `Format*` helpers), `.DataSection.cs` (data directives), `.Numbers.cs` (registers/numbers). Public API — `Assemble(...)`/`Build(...)`.
- **`IRAssembler` is a partial class of 3 files in `ASM/IR/Emitеer/`** (`Nantuc.Compiler.ASM.IR.Emitеer`): `IRAssembler.cs` (Emit*/MarkLabel/GetItems/Build), `.Layout.cs` (pass 1: `ComputeLabelAddresses`), `.Emit.cs` (pass 2: `EmitItem`/`EmitInstructionItem`/`EmitDataItem`/`PadTo`/`ResolveJumpTarget`).
- Optimizations: `AstOptimizer` (AST-level) and `IROptimizer` (IR-level) — both are frameworks awaiting concrete rules.
- **`TokenTypeExtension.AsText`** — the only place where `TokenType` is converted to a string; used everywhere in diagnostics instead of `Enum.ToString()` (AOT-safe, no reflection, no boxing).
- **`WordRegexBuilder`** (`Nantuc.Compiler`) — a single point for building `\b(...)\b` patterns; used by `AsmLanguageDefinition` and `MiniCLanguageDefinition` so that the shapes do not diverge.
- **`AsmLanguageDefinition.Mnemonics`/`Registers`** — the source of truth for NANASM scripts, but **not synchronized with the XML header of `AssemblerParser`** (see item 20).

## API

### General overview

| Type | Namespace | Kind | Purpose |
|---|---|---|---|
| `ASTNode` + 28 descendants | `C.AST` | `abstract class` / `class` | AST hierarchy. Full catalog — in the section [Catalog of AST nodes](#catalog-of-ast-nodes) |
| `Lexer` | `C.Lexer` | `partial class` | Tokenization; `Token`, `TokenType`; **3-character operators `<<=`/`>>=`** |
| `TokenTypeExtension` | `C.Lexer` | `static class` | `extension(TokenType).AsText` — manual switch, AOT-safe |
| `Parser` | `C.Parser` | `partial class` (6 files) | Recursive descent -> `ProgramNode`; **`ParseDeclSpec` for `const`**, **`ParseFunction(..., isReturnPointer)`** |
| `MiniCLanguageDefinition` | `C` | `static partial class` | `Keywords`/`Operators` (`FrozenSet`); `const` in `Keywords` |
| `StructLayout` / `FieldInfo` | `C` | `class` / `record` | Struct layout; `_fields` — `Dictionary<string, FieldInfo>` |
| `WordRegexBuilder` | `Nantuc.Compiler` | `static class` | `Build(FrozenSet<string>)` -> `\b(...)\b` regex (AOT-safe, without `Compiled`) |
| `CodeGenUtils` | `C.CodeGenerator` | `static class` | `GetSizeForType`/`GetSizeInBytes`/`IsPrimitiveType`/`IsSignedType`; extension `EmitCastToType`, `EmitMultiplyByConstant` |
| `VariableAccessor` | `C.CodeGenerator` | `class` | `LoadToRegister`/`StoreFromRegister` with **`EmitCastToType`** after LOAD and after MOV into the home register |
| `GlobalMemoryManager` | `C.CodeGenerator` | `class` | `Allocate`/`Contains`/`GetInfo`/`GetLabel`; **`IsSigned` is computed in `Allocate`** |
| `GlobalInfo` | `C.CodeGenerator` | `class` | Metadata of a global; **`IsSigned`** |
| `VarLocation` | `C.CodeGenerator` | `class` | Metadata of a variable; **`IsSigned`** |
| `FunctionContext` | `C.CodeGenerator.FunctionCtx` | `class` | `Create(...)` -> `VarMap`, `TotalLocalSize`, `SaveAreaSize`; **`ReturnSize`/`ReturnIsSigned`/`ReturnIsVoid`**, **`VarLocation.IsSigned`** |
| `SaveDepthEstimator` | `C.CodeGenerator.FunctionCtx` | `static class` | `Estimate(ASTNode)` — maximum depth of `ExpressionSaveDepth` |
| `VariableLayoutBuilder` | `C.CodeGenerator.FunctionCtx` | `internal static class` | `VarInfo` -> `VarLocation` (register or stack); computes `TypeSize`/`IsSigned`/`StructTypeName` |
| `ExpressionGenerator` | `C.CodeGenerator.Expression` | `partial class` (12 files) | Expression generation; **`GenerateAssignmentExpr` with 4 lvalue branches**, **`CheckConstTarget`** (internal), **overflow casts** |
| `StatementGenerator` | `C.CodeGenerator.Statement` | `partial class` (6 files) | Statement generation; **calls `_exprGen.CheckConstTarget`**; **overflow casts on all lvalues** |
| `StatementTypeResolver` | `C.CodeGenerator.Statement` | `static class` | `IsArrayElemSigned`/`IsPointedSigned`/`IsFieldSigned` — extracted from `StatementGenerator` |
| `FunctionGenerator` | `C.CodeGenerator.FunctionGen` | `partial class` (5 files) | `Generate(ProgramNode)`, prologue/epilogue; **cast of arguments r0..r3 and pseudo-globals** |
| `ConstantEvaluator` | `C.CodeGenerator.FunctionGen` | `internal static class` | `Evaluate(ASTNode, structTable, er)` — constant folding for the data section |
| `LocalVarCollector` | `C.CodeGenerator.FunctionGen` | `internal static class` | `Collect(BlockNode)` -> `Dictionary<string, VariableNode>` |
| `RegisterAllocator` | `C.CodeGenerator.RegistersAlloc` | `static partial class` (2 files) | `AllocateRegisters(...)` -> `RegisterAllocationResult`; `CollectAddressTakenVars` (via `VariableAccessAnalyzer`) |
| `GreedyGraphColorer` | `C.CodeGenerator.RegistersAlloc` | `internal static class` | `Color(graph, availableRegs)` — greedy coloring by descending degree |
| `VariableAccessAnalyzer` | `C.CodeGenerator.RegistersAlloc` | `internal static class` | `CollectAddressTakenVars` (pass 1) + `CollectVarEvents` (pass 2) |
| `RegisterAllocationResult` | `C.CodeGenerator.RegistersAlloc` | `class` | `VarToRegister` (`Dictionary<string, RegType?>`) + `UsedRegisters` (`HashSet<RegType>`) |
| `AstOptimizer` | `C.Optimizations` | `class` | `Add`/`Insert`/`Remove`/`Optimize(ProgramNode)` -> `IEnumerable<IReadOnlyLogOptimization>` |
| `IAstOptimizationRule` / `ILogOptimization` | `C.Optimizations` | `interface` | Rule contract / log contract |
| `OptimizationResult` | `C.Optimizations` | `class : ILogOptimization` | Simple log implementation |
| `AssemblerBase` | `ASM.Base` | `abstract class` | `Build`/`EmitInstruction`/`EmitInstruction64`/`EmitInstruction64WithLabel`/`EmitJump`/`EmitJumpToAbsolute`/`MarkLabel`/`EmitData`; properties `BaseAddress`/`ErrorReporter`/`CodeSectionSize`/`DataSectionStart`/`DataSectionSize` |
| `Assembler` | `ASM` | `class : AssemblerBase` | Direct writing; `_patches`; `DataSection*`/`CodeSectionSize` in `Build()` |
| `AssemblerParser` | `ASM.Parser` | `partial class` (5 files) | `Assemble(code, baseAddress, ErrorReporter)`, `Build(code, asm)`, `Assemble(code, asm, allowDataSection)`; `.data`/`.bss`/`.text`/`.code` sections; **`FormatIWithSize` for `CASTZ`/`CASTS`**; `PendingData`; `[GeneratedRegex]` for `NormalizeSizeExtension` |
| `AsmLanguageDefinition` | `ASM` | `static class` | `Mnemonics`/`Registers` (`FrozenSet`); regexes without `Compiled` |
| `IRAssembler` | `ASM.IR.Emitеer` | `partial class : AssemblerBase` (3 files) | `GetItems()`, two-pass `Build()`, padding-`AsmData`; `stackalloc byte[7]` in `Build()` |
| `AsmItem`/`AsmLabel`/`AsmData`/`AsmInstruction` | `ASM.IR` | `class` | IR elements (each in its own file); `AsmInstruction` auto-decodes fields via `InstructionDecoder` |
| `IOptimizerIRRule` | `ASM.IR` | `interface` | `TryOptimize(items, index, passCount, out newIndex)`, `GetLogs()`, `Clear()` |
| `IROptimizer` | `ASM.IR` | `class` | `Optimize(items, maxPasses = 5)` — to a fixed point; `Clear()` on entry |
| `InstructionEncoder` | `ASM.Utils` | `static class` | `Encode`/`EncodeR`/`EncodeI`/`EncodeU`/`EncodeJ` + `EncodeLDI`/…; **`Decode(uint)`** with cases for `CASTZ`/`CASTS`; extension blocks |
| `AsmExtensions` | `ASM.Utils` | `static class` | `EmitMov`/…/`EmitInstI`/`EmitInstR`/`EmitInstU`/`EmitInstJ`/`EmitInstWithData` |

### Frontend

**`Lexer(source, ErrorReporter)`** (`Nantuc.Compiler.C.Lexer`) — `Tokenize() -> List<Token>`. Supports `//`/`/* */`, hex (`0x`), char/string literals, `->` as a separate `TokenType.Arrow`. **3-character operators `<<=`/`>>=` are recognized in `ReadOperator` before the check for 2-character ones** — otherwise `x >>= 1` is split into `>>` + `=` and fails in `Parser.ParseAssignment`. `Peek(offset = 1)` — the only lookahead. Laid out in `C/Lexer/` (`Lexer.cs` + `.Trivia.cs` + `.Literals.cs` + `.Operators.cs` + `Token.cs` + `TokenType.cs` + `TokenTypeExtension.cs`).

**`Parser(tokens, ErrorReporter)`** (`Nantuc.Compiler.C.Parser`) — `partial class` of 6 files in `C/Parser/`. Public contract — `Parse() -> ProgramNode` (namespace `Nantuc.Compiler.C.AST`); the rest of the methods are `private`/`internal`, distributed across partials:
- `Parser.cs` — root (constructor, fields, `Parse()`, `Advance()`, `Expect()`).
- `Parser.TopLevel.cs` — `Parse()`: `struct`, `#include`, `extern`, functions, globals; `_structNames` is populated as encountered.
- `Parser.Declarations.cs` — `ParseDeclSpec`/`ParseDeclarators`/`ParseVariableTail`/`ParseFunction(type, name, isReturnPointer)`.
- `Parser.Expressions.cs` — `ParseAssignment` -> `ParseLogicalOr` -> `ParseLogicalAnd` -> `ParseBitwiseOr`/`Xor`/`And` -> `ParseEquality` -> `ParseRelational` -> `ParseShift` -> `ParseAdditive` -> `ParseMultiplicative` -> `ParseUnary` -> `ParsePrimary`.
- `Parser.Statements.cs` — `ParseBlock`/`ParseBody`/`ParseIf`/`ParseWhile`/`ParseDoWhile`/`ParseFor`/`ParseReturn`/`ParseBreak`/`ParseContinue`/`ParseInlineAsm`.
- `Parser.Literals.cs` — `ParseStringToNodes`/`ParseCharLiteral`/`ParseHexChar`.

`Parse` handles:
- `struct Name { ... };` — `_structNames` is populated as encountered, both `Name` and `struct Name` are supported.
- `#include "path"` — added to `IncludesList`.
- `extern <type> <name>(<params>);` — `FunctionNode` with `IsExternal = true`, `Body = null!`.
- Functions and global variables via `ParseDeclSpec`/`ParseDeclarators`.
- `asm { ... }` — `InlineAsmNode` with the text.
- Postfix `++`/`--`, `.`/`->`, `[i]`, `(args)` — in the `ParsePrimary` loop (in `Parser.Expressions.cs`).
- `sizeof` — in `ParseUnary`, including `sizeof(const T)` and `sizeof(const T*)`.
- `const` qualifiers — `ParseDeclSpec`.
- **`ParseFunction(type, name, isReturnPointer)` — `isReturnPointer` is passed to `FunctionNode.ReturnIsPointer`** (needed for the overflow cast of `return`).
- **Primitive casts** - expressions like `(byte)300` will truncate it to `44`, limitations: `*(int*)` is not supported yet, casts to user-defined structs are also not supported

**AST hierarchy (`Nantuc.Compiler.C.AST`)** — 28 classes, laid out in files in `C/AST/`. The base `ASTNode` — the only abstract property `NameNode` (`=> nameof(XxxNode)`). `AssignmentNode` contains `LValue`, `CompoundOp`, `IndexExpr`. `VariableNode` contains `IsPointer`/`PointedType`/`IsArray`/`ArraySize`/`ArrayInitializer` + `IsConst`/`IsPointedConst`. `ParameterNode` + `IsConst`/`IsPointedConst`. **`FunctionNode` — `LocalSize`, `IsExternal`, `ReturnIsPointer`**.

**`TokenTypeExtension`** (`Nantuc.Compiler.C.Lexer`) — `extension(TokenType).AsText` returns a readable name via a manual `switch`, without `Enum.ToString()`. Used in all parser messages — **AOT-safe, no reflection, no boxing**.

**`MiniCLanguageDefinition`** (`Nantuc.Compiler.C`) — `Keywords`/`Operators` (`FrozenSet`), `const` in `Keywords`. **`GetKeywordRegex()` — dead code** (replaced by `WordRegexBuilder`).

**`WordRegexBuilder`** (`Nantuc.Compiler`) — `Build(FrozenSet<string>, RegexOptions)` -> `new($@"\b({string.Join("|", words)})\b", options)`. A single point for `AsmLanguageDefinition` and `MiniCLanguageDefinition`; **intentionally without `[GeneratedRegex]`** so as not to hardcode the pattern (see the drift history in `MiniCLanguageDefinition`).

### Code generator

**`FunctionGenerator.Generate(ProgramNode)`** (`Nantuc.Compiler.C.CodeGenerator.FunctionGen`) — sequence:
1. `GlobalMemoryManager` + `Allocate` for all `GlobalVarNodes`, `IsConst`/`IsPointedConst`/`IsSigned` computed automatically.
2. `_functionTable` is populated from `FunctionNodes`.
3. `MarkLabel("__global_init")` + `GenerateGlobalInit` — initialization of globals (scalars only, structs skipped).
4. `EmitJmp("func_main")`.
5. `GenerateFunction` for each non-external function.
6. `EmitData` for globals (with `ArrayInitializer`).
7. `EmitData` for pseudo-globals of parameters 4+.

**`FunctionContext.Create`** (`Nantuc.Compiler.C.CodeGenerator.FunctionCtx`) — computes:
- `VarMap`: `VarLocation` for each parameter and local variable, including **`IsSigned`** (for non-pointer, non-struct) — via `VariableLayoutBuilder.Build`.
- `TotalLocalSize` — total size of stack variables, aligned to 8.
- `SaveAreaSize = (SaveDepthEstimator.Estimate(body) + 1) * 8`.
- `UsedRegisters` — registers from `RegisterAllocator`.
- **`ReturnSize`/`ReturnIsSigned`/`ReturnIsVoid`** — for casting `return` in `GenerateReturn`.

**`SaveDepthEstimator`** (`Nantuc.Compiler.C.CodeGenerator.FunctionCtx`) — a pure function `Estimate(ASTNode?) -> int`; for each node returns an estimate of the maximum depth of `ExpressionSaveDepth`. Each `SaveSlot` in the switch corresponds to one simultaneously occupied save slot (see `ExpressionGenerator.SaveRegToStack`). Easily tested separately (see `SaveDepthEstimatorTests`).

**`VariableLayoutBuilder`** (`Nantuc.Compiler.C.CodeGenerator.FunctionCtx`) — `internal static class`. `Build(VarInfo, stackOffset, allocation, structTable, er, out consumedBytes)` -> `VarLocation`. Logic: if the variable is a scalar/not an array/not a struct and `RegisterAllocator` gave a register -> `MakeRegisterLocation`; otherwise -> `MakeStackLocation` (with `TypeSize`/`StructTypeName`/`IsSigned`).

**`RegisterAllocator.AllocateRegisters`** (`Nantuc.Compiler.C.CodeGenerator.RegistersAlloc`) — greedy coloring:
- Reserves `r0..r4`, `r27`, `rFL`, `rSP`, `rHP`, `rIP` — **22 available** (`r5..r26`; the comment in the code "23 registers" is incorrect, see item 14).
- **Excludes variables whose address is taken (`&x`)** — `VariableAccessAnalyzer.CollectAddressTakenVars` before coloring.
- Builds the graph by `[firstDef, lastUse]` in `RegisterAllocator.Interference.cs`, coloring order — by descending degree (`GreedyGraphColorer`).
- Returns `RegisterAllocationResult` (`VarToRegister` + `UsedRegisters`).

**`VariableAccessAnalyzer`** (`Nantuc.Compiler.C.CodeGenerator.RegistersAlloc`) — two independent passes: `CollectAddressTakenVars` (names `x` to which `&x` is applied) and `CollectVarEvents` (positions of def/use in a linear walk). Important: the passes are NOT merged — they traverse `AssignmentNode` differently (see item 14).

**`ExpressionGenerator`** — `partial class` of 12 files (namespace `Nantuc.Compiler.C.CodeGenerator.Expression`, folder `Expression/`). Public API is one — `GenerateExpression(expr)` (into `r0`) and `GenerateExpression(expr, targetReg)`; internally distributed across partials:
- `ExpressionGenerator.cs` — root + `GenerateExpression(expr)` / `GenerateExpression(expr, targetReg)`.
- `ExpressionGenerator.Assignment.cs` — `GenerateAssignmentExpr(assign, targetReg)` — **assignment-as-expression** (result = new value). Covers:
  - `arr[i] = value` (via `StoreR0ToArrayElement`)
  - `*ptr = value` (via `GenerateExpression(operand, r0)` + `STORE_IND`)
  - `obj.field = value` (via `GenerateMemberAddress(member, r0)` + `STORE_IND`)
  - `x = value` (via `StoreFromRegister`)
  - compound: `arr[i] op= value`, `*p op= value`, `obj.f op= value`, `x op= value` — via `GenerateCompoundAssignmentExpr` -> `GenerateCompoundIndirectExpr` / `GenerateCompoundIdentExpr`.
  - **`CheckConstTarget` (internal) at the start** — a single point for checking `const`.
  - **`EmitCastToType` after each write** — overflow for narrow types.
- `ExpressionGenerator.Binary.cs` — `GenerateBinaryOp`:
  - Comparisons — `Cmp` + jumps -> `1`/`0`.
  - `&&`/`||` — `GenerateLogicalAndOr`, result in `r0`, then `Mov(targetReg, r0)`.
  - Constant folding: `NumberNode op NumberNode`.
  - Optimization `targetReg == left operand`: right into `r1`/`r2`, `ApplyPointerArithmeticScaling` for `+`/`-` with pointers.
  - Pointer arithmetic: `ptr ± int` — scaling of `int` by `sizeof(*ptr)`; `int + ptr` — the same. `ptr - ptr` is not scaled.
- `ExpressionGenerator.PointerOps.cs` — `GenerateAddressOf` + `GenerateDereference` + `ApplyPointerArithmeticScaling`.
- `ExpressionGenerator.Arrays.cs` — `ArrayAccessNode` + `StoreR0ToArrayElement`.
- `ExpressionGenerator.Calls.cs` — `FunctionCallNode`/`IndirectCallNode` + call-site casts of arguments to the parameter type.
- `ExpressionGenerator.Conditions.cs` — `GenerateCondition` (expansion of `&&`/`||`, comparisons, comparison with zero).
- `ExpressionGenerator.Helpers.cs` — `SaveRegToStack`/`RestoreRegFromStack`; `ExpressionSaveDepth`, slot = `TotalLocalSize + ExpressionSaveDepth * 8`, address via `ADDR_SCRATCH_REG_EN = r27`.
- `ExpressionGenerator.Increment.cs` — **`GenerateIncrement`** — `++`/`--` for `Identifier`/`ArrayAccess`/`Dereference`/`MemberAccess`. At the start — `const` checks. After `Inc`/`Dec` and after load — `EmitCastToType`.
- `ExpressionGenerator.MemberAccess.cs` — `GenerateMemberAddress` + `.`/`->`.
- `ExpressionGenerator.Sizeof.cs` — `SizeofTypeNode`/`SizeofExprNode`.
- `ExpressionGenerator.Unary.cs` — `GenerateUnaryOp` (for `-`, `!`, `~`).

**`StatementGenerator`** — `partial class` of 6 files + `StatementTypeResolver` (namespace `Nantuc.Compiler.C.CodeGenerator.Statement`, folder `Statement/`). Public API — `GenerateBlock(BlockNode)`; internally distributed across partials:
- `StatementGenerator.cs` — root + switch over nodes, `_loopStack`.
- `StatementGenerator.Assignments.cs` — `GenerateAssignment` (4 lvalue branches); **at the start calls `_exprGen.CheckConstTarget`**.
- `StatementGenerator.CompoundAssignments.cs` — `GenerateCompoundAssignment` -> `GenerateCompoundArrayElement` / `GenerateCompoundDeref` / `GenerateCompoundMember` / `GenerateCompoundIdent`; **overflow casts on all lvalues**.
- `StatementGenerator.ControlFlow.cs` — `GenerateIf`/`GenerateWhile`/`GenerateFor`/`GenerateDoWhile`/`GenerateReturn` (with `EmitCastToType` to the return type)/`GenerateBreak`/`GenerateContinue`.
- `StatementGenerator.Declarations.cs` — local declarations + initialization.
- `StatementGenerator.InlineAsm.cs` — `InlineAsmNode` -> `new AssemblerParser().Assemble(...)`.
- `StatementTypeResolver.cs` — `IsArrayElemSigned`/`IsPointedSigned`/`IsFieldSigned` (extracted from `StatementGenerator` for readability).

### Assembler

**`AssemblerBase`** (`Nantuc.Compiler.ASM.Base`) — emitter abstraction. Public properties:
- `BaseAddress` — address for patching jumps.
- `ErrorReporter` — public readonly field.
- `CodeSectionSize` / `DataSectionStart` / `DataSectionSize` — `{ get; protected set; }`, filled in `Build()`.

**`Assembler`** (`Nantuc.Compiler.ASM`) — direct writing to `MemoryStream`:
- `EmitInstruction(uint)` — 4 bytes.
- `EmitInstruction64(uint, ulong)` — 4 + `Align8()` + 8.
- `EmitInstruction64WithLabel(uint, string)` / `EmitJump(uint, string)` — 4 + `Align8()` + placeholder, patched in `Build()`.
- `EmitJumpToAbsolute(uint, ulong)` — 4 + `Align8()` + 8, no patching.
- `EmitData(string, byte[])` — `Align8()`, `MarkLabel`, write.
- `Build()` — records `DataSectionStart`/`DataSectionSize`/`CodeSectionSize`, patches `_patches`.
- `Align8()` — `stackalloc byte[pad]` (0..7 bytes).

**`AssemblerParser`** (`Nantuc.Compiler.ASM.Parser`) — `partial class` of 5 files (`AssemblerParser.cs` + `.Lexing.cs` + `.Instructions.cs` + `.DataSection.cs` + `.Numbers.cs`). Text assembler. **Full instruction set:**
- No operands: `NOP`, `END`, `RET`, `HALT`, `WAKE`.
- R-format (2 registers): `MOV`, `MOD`, `ADD`, `SUB`, `AND`, `OR`, `XOR`, `IN`, `OUT`, `SHR`, `SHL`, `DIV`, `MULT_INT`, `CMP`, `TEST`.
- U-format (1 register): `JMP_IND`, `CALL_IND`, `WAKE_INT`, `INC`, `DEC`, `NOT`, `PUSH`, `POP`.
- **I+size-format (1 register + `DataSize`): `CASTZ[.S8|.S16|.S32|.S64]`, `CASTS[...]`** — new `FormatIWithSize`, emits `EmitInstI`.
- `LDI reg, const|label` — constant (`0x...` hex) or label.
- `LOAD[.size] reg, addr` / `LOAD_UNSAFE ...`, `STORE[.size] reg, addr` / `STORE_UNSAFE ...`.
- `LOAD_IND[.size] regDst, regAddr` / `STORE_IND[.size] regSrc, regAddr` (+unsafe).
- `JMP`/`JZ`/`JNZ`/`JG`/`JL`/`CALL` — label or absolute address.

**Layout across partials:**
- `AssemblerParser.cs` — fields (`_asm`, `_allowDataSection`, `_pendingData`, `_inDataSection`, `_pendingLabel`, `_currentLine`, `_opMap`, `_regMap`), constructor, `Assemble(...)` (3 overloads), `Build(...)`, `PendingData` (record), `[GeneratedRegex]` method `GetRexExtensionAsm()`.
- `AssemblerParser.Lexing.cs` — `ProcessLines`, `StripComments`, `ProcessSectionDirective`, `ProcessDataLine`, `ProcessCodeLine`, `GetSectionDirective`, `NormalizeSizeExtension`, `FlushPendingData`.
- `AssemblerParser.Instructions.cs` — `ParseInstruction`, `SplitMnemonicAndSize`, `DispatchInstruction`, `FormatR`/`FormatU`/`FormatIWithSize`/`FormatLDI`/`FormatJumpType`/`FormatLOAD_STORE`/`FormatLOAD_IND_STORE_IND`, `RequireOperands`.
- `AssemblerParser.DataSection.cs` — `TryProcessDataDirective`, `TryAddIntegerData`, `TryAddStringData`, `AddPendingData`.
- `AssemblerParser.Numbers.cs` — `ParseReg`, `TryParseSize`, `TryParseLong`, `ParseNumber`, `IsNumber`.

**Size normalization:** `NormalizeSizeExtension` fixes `LOAD . S8` -> `LOAD.S8` via `GetRexExtensionAsm().Replace(instruction, "$1.$2")` (AOT-safe, `[GeneratedRegex]` on `(\w)\s*\.\s*(\w)`). There is a fast-path: if there is no `' . '` or spaces around `.` — returns the string without allocations.

**Parser state is instance-level:** `_asm`, `_allowDataSection`, `_pendingData`, `_inDataSection`, `_pendingLabel`, `_currentLine`, `_opMap`, `_regMap` are mutated in `Assemble`. **Not thread-safe** (see item 13). `_pendingData` accumulates `PendingData` and is flushed via `FlushPendingData()` at the end of `Assemble` (or when transitioning from `.data`/`.bss` back to `.text`).

**`AsmLanguageDefinition`** (`Nantuc.Compiler.ASM`) — `Mnemonics`/`Registers` (`FrozenSet`), regexes without `Compiled`. Uses `WordRegexBuilder`. `_regMapLex`/`_opMapLex` — ordinary `Dictionary`, **plus** their `FrozenDictionary` copies `_regMap`/`_opMap` (see item 11).

**`IRAssembler`** (`Nantuc.Compiler.ASM.IR.Emitеer`) — `partial class` of 3 files (`IRAssembler.cs` + `.Layout.cs` + `.Emit.cs`), list of `AsmItem`:
- `IRAssembler.cs`:
  - `EmitInstruction64(uint, ulong)` validates `HasNeed64IntData(op)`.
  - `EmitJump(uint, string)` -> `AsmInstruction` with `TargetLabel`.
  - `EmitJumpToAbsolute(uint, ulong)` -> `AsmInstruction(..., isAbsolute: true)`.
  - `EmitData(string, byte[])` -> padding-`AsmData(alignment: 8)` + `MarkLabel` + `AsmData(data, alignment: 1)`. Empty `AsmData` with `alignment: 8` — separator/alignment.
  - `MarkLabel` — duplicates via `HashSet<string>`; on collision — `Asm_DuplicateLabel` with the position of the existing label (searched linearly).
  - `Build()` — two-pass: `labelAddresses = ComputeLabelAddresses()`, then `EmitItem` for each element; `stackalloc byte[7]` padding for alignment.
  - `GetItems()` — **internal** `List<AsmItem>`, mutations visible to `Build()`.
- `IRAssembler.Layout.cs` — **pass 1**: `ComputeLabelAddresses()` — `labels`, `DataSectionStart`, `DataSectionSize`, `CodeSectionSize`. `AdvanceInstructionOffset(instr, offset)` — single offset logic for instructions; `Align(offset, alignment)` — extension on `ulong`.
- `IRAssembler.Emit.cs` — **pass 2**: `EmitItem`, `EmitInstructionItem` (4 bytes of code, `PadTo(8)`, 8 bytes of data for `HasImmediate`/`IsJump`), `EmitDataItem` (`PadTo(alignment)`, bytes), `PadTo` (uses `padding[..pad]`), `ResolveJumpTarget` (`Asm_UndefinedLabel` if no label).

> **Important:** `ComputeLabelAddresses` (Layout) and `EmitItem` (Emit) must compute `offset` **identically**. Both implementations rely on the common `Align(...)` (`offset.AlignUp(alignment)`).

**`InstructionEncoder`** (`Nantuc.Compiler.ASM.Utils`) — encoding of a 32-bit word:
- `Encode`/`EncodeR`/`EncodeI`/`EncodeU`/`EncodeJ` — formats.
- Specialized: `EncodeLDI`/`EncodeLOAD`/`EncodeSTORE`/`EncodeLOAD_IND`/`EncodeSTORE_IND`/`EncodeCALL`/`EncodeRET`/`EncodeHALT`/`EncodeWAKE`/`EncodeEND`/`EncodeNOP`.
- **`Decode(uint)`** — covers all opcodes, including `LOAD_UNSAFE`/`STORE_UNSAFE`/`LOAD_IND_UNSAFE`/`STORE_IND_UNSAFE`/`CMP`/`TEST`/`WAKE_INT`/`CASTZ`/`CASTS` (`CASTZ.S8 r0`, `CASTS.S32 r1`).
- Extension blocks: `OpCode.ToUint()`/`AsText`, `RegType.ToUint()`/`AsText`, `OpCodeSize.ToUint()`/`AsText`.
- Extension block on `BinaryWriter`: `WriteInstruction(uint)`, `Align8()`, `WriteInstructionWithData64(uint, ulong)`. **Not used in `Assembler`** (it has its own methods), but available for custom backends.

**`AsmExtensions`** (`Nantuc.Compiler.ASM.Utils`) — extension block on `AssemblerBase`. The code generator works only through these methods:
- R-format: `EmitMov`, `EmitMult`, `EmitAdd`, `EmitSub`, `EmitMultInt`, `EmitDiv`, `EmitAnd`, `EmitOr`, `EmitXOr`, `EmitShl`, `EmitShr`, `EmitMod`, `EmitCmp`.
- U-format: `EmitPush`, `EmitPop`, `EmitNot`, `EmitInc`, `EmitDec`.
- Special: `EmitLdi(ulong)`, `EmitLdi(string)`, `EmitLoadInd`, `EmitStoreInd`, `EmitLoad`, `EmitStore` (2 overloads).
- Jumps: `EmitJmp`, `EmitJz`, `EmitJnz`, `EmitJl`, `EmitJg`, `EmitCall`; `EmitInstJAbsolute`.
- No operands: `EmitEnd`, `EmitRet`, `EmitNop`, `EmitHalt`, `EmitWake`.
- General: `EmitInst`, `EmitInstU`, `EmitInstR`, **`EmitInstI`** (used for `CASTZ`/`CASTS`), `EmitInstJ`, `EmitInstJAbsolute`, `EmitInstWithData`.

**`CodeGenUtils.EmitCastToType(reg, size, isSigned)`** (`Nantuc.Compiler.C.CodeGenerator`) — extension on `AssemblerBase`:
- `size == S64` -> no-op;
- `isSigned == true` -> `EmitInstI(OpCode.CASTS, reg, size)`;
- `isSigned == false` -> `EmitInstI(OpCode.CASTZ, reg, size)`.

### Optimizations

**`AstOptimizer`** (`Nantuc.Compiler.C.Optimizations`) — `Add`/`Insert`/`Remove`/`RemoveAt`, `Count`, `GetType(i)`. `Optimize(program)` returns `IEnumerable<IReadOnlyLogOptimization>` — one per rule. There are no rules in this batch.

**`IROptimizer`** (`Nantuc.Compiler.ASM.IR`) — `Optimize(items, maxPasses = 5)` — to a fixed point. Skips `AsmLabel`, applies the first matching rule. `Clear()` on all rules at the start.

## Examples for the API

**Full pipeline:**

```csharp
using Nantuc.Compiler.C.Lexer;
using Nantuc.Compiler.C.Parser;
using Nantuc.Compiler.C.AST;
using Nantuc.Compiler.C.CodeGenerator.FunctionGen;
using Nantuc.Compiler.C.Optimizations;
using Nantuc.Compiler.ASM.IR;
using Nantuc.Common;
using Nantuc.Diagnostics;

var ErrorReporter = new ErrorReporter();
string source = """
    const int LIMIT = 10;
    int main() {
        int x = 42;
        if (x < LIMIT) {
            return x;
        }
        return LIMIT;
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

Medium. The MiniC user works through `VmApplication.Compile(source)`; the language/extension author — here.

- Author of new syntax — `Lexer`/`Parser` (partials in `Parser/*.cs`)/`ASTNode` (`C/AST/`).
- Author of optimizations — `IAstOptimizationRule`/`IOptimizerIRRule`.
- Author of a new backend — `AssemblerBase`.
- Author of a new ISA target — `InstructionEncoder` + `AsmExtensions`.
- Author of NANASM scripts — `AssemblerParser` with `.data`/`.bss`/`.text`/`.code` sections (partials in `ASM/Parser/`).

**What should be done:**
- `IRAssembler` for the production pipeline; `Assembler` — for tests.
- User optimizations — via `Add` in `AstOptimizer`/`IROptimizer`; rules are idempotent (`Clear()` on entry).
- For inline-asm — `AsmLanguageDefinition.Mnemonics`/`Registers` as the source of truth.
- **`const`** — use as a guarantee of immutability in the API and internal data.
- **Overflow** — write to narrow types (byte/char/short/int/uint/ushort) as in normal C; casts are inserted by the code generator.
- `InstructionEncoder.Decode(uint)` — for debugging.
- **Adding a new expression** — a new partial `ExpressionGenerator.Xxx.cs` in `CodeGenerator/Expression/`; **a new operator** — a new partial `StatementGenerator.Xxx.cs` in `CodeGenerator/Statement/`. API methods do not change, `Parser`/`StatementGenerator.cs` only dispatch.
- **Adding a new data directive** — in `AssemblerParser.DataSection.cs` (one case in `TryProcessDataDirective`); **a new operand format** — in `AssemblerParser.Instructions.cs` (`Format*` + case in `DispatchInstruction`).

**What should not be done:**
- Rely on `FunctionNode.Body` for `extern` — it is `null!`.
- Rely on `RegisterAllocator` for >22 simultaneously live scalars.
- Use `MiniCLanguageDefinition.GetKeywordRegex()` — dead code (replaced by `WordRegexBuilder`).
- `.data` in inline-asm — forbidden.
- `const` on `struct` fields — not supported.
- One `AssemblerParser` for multiple threads — not thread-safe.
- **Rely on compile-time folding of `const`** — it does not exist; `const int N = 10; int arr[N];` — parsing error.
- **Add `AsmData` with `alignment > 8`** — will break the `Padding` buffer in `IRAssembler.Build()` (see item 21).

## Special information

### Catalog of AST nodes

All nodes are in **`Nantuc.Compiler.C.AST`**, files in `C/AST/`. The base `ASTNode` — only `NameNode`. `Parser.Parse()` returns `ProgramNode`.

**Root / declarations**

| Node | Contains | Created in |
|---|---|---|
| `ProgramNode` | `GlobalVarNodes`, `FunctionNodes`, `StructNodes`, `IncludesList` | `Parser.TopLevel.cs` -> `Parse()` |
| `StructDeclNode` | `Name`, `Fields: List<(type, name)>` | `Parser.TopLevel.cs`, branch `struct Name { ... }` |
| `FunctionNode` | `Name`, `ReturnType`, `Parameters`, `Body`, `IsExternal`, `LocalSize`, **`ReturnIsPointer`** | `Parser.Declarations.cs` -> `ParseFunction`, `Parser.TopLevel.cs` -> `extern` branch |
| `ParameterNode` | `Type`, `Name`, `IsPointer`, `PointedType`, `IsConst`, `IsPointedConst` | `Parser.Declarations.cs` -> `ParseFunction` |
| `VariableNode` | `Type`, `Name`, `Initializer`, `ArrayInitializer`, `IsArray`, `ArraySize`, `IsPointer`, `PointedType`, `StructTypeName`, `IsConst`, `IsPointedConst` | `Parser.Declarations.cs` -> `ParseVariableTail` |

**Statements**

| Node | Contains | Created in |
|---|---|---|
| `BlockNode` | `Statements` | `Parser.Statements.cs` -> `ParseBlock`, `ParseBody` |
| `IfNode` | `Condition`, `ThenBlock`, `ElseBlock?` | `Parser.Statements.cs` -> `ParseIf` |
| `WhileNode` | `Condition`, `Body` | `Parser.Statements.cs` -> `ParseWhile` |
| `DoWhileNode` | `Body`, `Condition` | `Parser.Statements.cs` -> `ParseDoWhile` |
| `ForNode` | `Init`, `Condition`, `Increment`, `Body` | `Parser.Statements.cs` -> `ParseFor` |
| `ReturnNode` | `Value?` | `Parser.Statements.cs` -> `ParseReturn` |
| `BreakNode` / `ContinueNode` | — | `Parser.Statements.cs` -> `ParseBreak` / `ParseContinue` |
| `InlineAsmNode` | `AsmCode: string` | `Parser.Statements.cs` -> `ParseInlineAsm` |

**Expressions**

| Node | Contains | Created in |
|---|---|---|
| `NumberNode` | `Value: long` | `Parser.Expressions.cs` -> `ParseNumberAtom` (from `ParsePrimary`) |
| `IdentifierNode` | `Name` | `Parser.Expressions.cs` -> `ParseIdentifierAtom` |
| `AssignmentNode` | `Name`, `IndexExpr?`, `LValue`, `CompoundOp?`, `Value` | `Parser.Expressions.cs` -> `ParseAssignment` |
| `BinaryOpNode` | `Operator`, `Left`, `Right` | `Parser.Expressions.cs` -> `ParseLogicalOr`/`And`, `ParseBitwiseOr`/`Xor`/`And`, `ParseEquality`, `ParseRelational`, `ParseShift`, `ParseAdditive`, `ParseMultiplicative`, `ParseComma` |
| `UnaryOpNode` | `Operator`, `Operand` | `Parser.Expressions.cs` -> `ParseUnary` |
| `IncrementNode` | `Target`, `Delta`, `IsPrefix` | `Parser.Expressions.cs` -> `ParseUnary` (prefix), `ParsePrimary` (postfix) |
| `FunctionCallNode` | `Name`, `Arguments` | `Parser.Expressions.cs` -> `ParseIdentifierAtom` |
| `IndirectCallNode` | `Callee`, `Arguments` | `Parser.Expressions.cs` -> `ParsePostfixIndirectCall` |
| `ArrayAccessNode` | `ArrayName`, `Index` | `Parser.Expressions.cs` -> `ParseIdentifierAtom` |
| `AddressOfNode` / `DereferenceNode` | `Operand` | `Parser.Expressions.cs` -> `ParseUnary` |
| `MemberAccessNode` | `Object`, `FieldName`, `IsArrow` | `Parser.Expressions.cs` -> `ParsePostfixMemberAndIncrement` |
| `SizeofTypeNode` / `SizeofExprNode` | `TypeName`/`IsPointer`, `Operand` | `Parser.Expressions.cs` -> `ParseUnary` |

Total 28 descendants. **Who walks the AST in the code generator:** `ExpressionGenerator` (all partials), `StatementGenerator` (all partials + `StatementTypeResolver`), `SaveDepthEstimator`, `RegisterAllocator` (`VariableAccessAnalyzer.CollectVarEvents`, `CollectAddressTakenVars`), `LocalVarCollector.Collect`, `ConstantEvaluator`. When adding a node — update all these places.

### Overflow of narrow integer types

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
| `long` | S64 | yes | as-is |
| `ulong` | S64 | no | as-is |

> **`char` in MiniC is a 16-bit signed (`S16`)**, not 8-bit as in standard C. Consequences: `sizeof(char) == 2`; `char c = 120; c += 100;` -> `220` (fits in int16); `char c = 32767; c += 1;` -> `−32768` (wrap).

**Where casts are inserted (`Nantuc.Compiler.C.CodeGenerator`, by partial):**

| Event | Cast | Partial |
|---|---|---|
| `LOAD[.size]` from memory | `LOAD` zero-extends -> **`CASTS.S<size>`** for signed types | `VariableAccessor` |
| `STORE[.size]` to memory | `STORE` truncates itself — no cast needed | `VariableAccessor` |
| `x = value`, `x op= value`, `x++` (register) | after the operation — `CASTZ`/`CASTS` in the home register | `VariableAccessor`, `Assignment.cs`, `Increment.cs`, `CompoundAssignments.cs` |
| `(x = value)`, `(x op= value)`, `x++` in an expression | after the operation — `CASTZ`/`CASTS` in `targetReg` | `Assignment.cs`, `Increment.cs` |
| `arr[i] = value`, `*p = value`, `obj.f = value` | after load and after the operation — `CASTZ`/`CASTS` | `Arrays.cs`, `MemberAccess.cs`, `CompoundAssignments.cs` |
| `arr[i]++`, `(*p)++`, `(obj.f)++` | after load and after `INC`/`DEC` — `CASTZ`/`CASTS` | `Increment.cs` |
| Call-site (argument -> `r0..r3`) | `CASTZ`/`CASTS` to the parameter type | `Calls.cs`, `FunctionGenerator` |
| Prologue (parameter 4+ from pseudo-global) | after `LOAD` — `CASTZ`/`CASTS` | `FunctionGenerator` |
| `RETURN` (value in `r0`) | before `JMP epilogue` / `END` — `CASTZ`/`CASTS` to the return type (non-pointer scalar) | `ControlFlow.cs` (`GenerateReturn`) |

**What is NOT cast:**
- Intermediate arithmetic result — C semantics: `byte + byte` gives `int`; truncation only on the next write.
- `long`/`ulong` — `S64`, `EmitCastToType` no-op.
- Pointers and structs.

**Implementation:**
- `CodeGenUtils.IsSignedType(type)` — `char/short/int/long` -> `true`.
- `CodeGenUtils.EmitCastToType(reg, size, isSigned)` — extension on `AssemblerBase`.
- `VarLocation.IsSigned` / `GlobalInfo.IsSigned` — computed in `VariableLayoutBuilder.Build` / `GlobalMemoryManager.Allocate`.
- `VariableAccessor.LoadToRegister` — `CASTS` after `LOAD`; `StoreFromRegister` — cast of the home register.

### Constness (`const`) — semantics and limitations

`const` in MiniC — a **compile-time guarantee of immutability** (not .rodata, not constant folding). The value is stored in the usual way; the write prohibition is diagnosed at the code generation stage.

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

**Syntax:** `Parser.Declarations.cs` -> `ParseDeclSpec` -> `(type, isPointer, pointedType, prefixConst, ptrConst)`; `[const] type`, `type const`, `*`, `[* const]`.

**Checks in the code generator:**
- `ExpressionGenerator.CheckConstTarget(assign)` — **internal, single point** (in `ExpressionGenerator.Assignment.cs`); called from `GenerateAssignmentExpr` (expression-context) and from `StatementGenerator.GenerateAssignment` (`StatementGenerator.Assignments.cs`, statement-context).
  - `assign.IndexExpr != null` -> `IsPointedConst(name)`;
  - `assign.LValue is DereferenceNode` -> `IsPointedConst(ptrName)`;
  - `assign.LValue is MemberAccessNode` -> skip for now (TODO, item 1);
  - simple name -> `IsConst(name)`.
- `ExpressionGenerator.GenerateIncrement` (`ExpressionGenerator.Increment.cs`) — `IsConst`/`IsPointedConst` for `Identifier`/`ArrayAccess`/`Dereference`.

**New `ErrorCode`s:**
```csharp
CodeGen_AssignmentToConst = 1316,   // "Cannot assign to const '{0}'"
CodeGen_IncrementOfConst  = 1317,   // "Cannot increment/decrement const '{0}'"
```

**Not supported:**
1. `const` on `struct` fields — `CheckConstTarget` for `MemberAccessNode` does `return` (TODO).
2. `const` at the call-site — `const int*` -> `int*` is not diagnosed.
3. Constant folding — `const int N; int arr[N];` — parsing error.
4. `const` in `extern` declarations.
5. `&x` from `const x` gives an ordinary `int*`.

**With registers:** `RegisterAllocator` does not exclude `const` from candidates — writing is blocked at the code generation level, the `Store`/`Inc`/`Dec` instruction is never reached. `&x` — `CollectAddressTakenVars` guarantees that such variables live in memory.

### WASM / cross-platform

| Aspect | Status | Comment |
|---|---|---|
| Build in WASM | - | No `unsafe`, `DllImport`, `LibraryImport`. `stackalloc` only in `Assembler.Align8` and `IRAssembler.Build` (0–7 bytes). |
| `NativeMemory` | - | Not used. |
| `stackalloc` / `InlineArray` | - | `stackalloc byte[pad]` (0..7 bytes) in `Assembler.Align8`; `stackalloc byte[7]` in `IRAssembler.Build`. |
| `Interlocked` / `Volatile` | - | Not used. |
| `System.Collections.Frozen` | - | `FrozenSet` + **`FrozenDictionary`** (`_regMap`/`_opMap`). |
| `[GeneratedRegex]` | - | `AssemblerParser.GetRexExtensionAsm()` — the only regex in the hot path. |
| `Regex` without `Compiled` | - | `AsmLanguageDefinition._mnemonicsRegex`/`regex`, `WordRegexBuilder.Build` — without `Compiled`. |
| `Enum.ToString()` | !!️ | **Removed from diagnostics** — replaced by `TokenTypeExtension.AsText` / `OpCode.AsText`. |
| `DiagnosticData` | - | Ready string on creation, no boxing and no reflection. |
| `AsmInstruction(uint, ulong, bool)` | !!️ | `TargetLabel = absoluteTarget.ToString()` — fallback, dead branch (item 7). |
| Trimming | !!️ | `using System.Security.Cryptography;` in one of the `C/AST/` files — not used, but sloppy. |
| AOT | !!️ | Main risk — `RegexOptions.Compiled`. |
| `switch` on `OpCode`/`TokenType` | - | Jump-table, AOT-friendly. |

**Result:** builds under WASM and AOT if `RegexOptions.Compiled` is removed.

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
  │  (partials: Parser.cs / .TopLevel.cs / .Declarations.cs /
  │             .Expressions.cs / .Statements.cs / .Literals.cs)
  │
  ├── StructLayout.Resolve(program.StructNodes)
  │
  ▼
FunctionGenerator.Generate(program)               // C.CodeGenerator.FunctionGen
  ├── GlobalMemoryManager.Allocate(...) — IsSigned computed
  ├── GenerateGlobalInit (scalars only)
  ├── EmitJmp("func_main")
  ├── foreach func in FunctionNodes:
  │     ├── LocalVarCollector.Collect(func.Body)
  │     ├── FunctionContext.Create(...) -> VarMap + IsSigned + ReturnSize/ReturnIsSigned
  │     ├── RegisterAllocator.AllocateRegisters(...) — &var -> stack
  │     ├── prologue: push savedRegs, sub rSP, stackAlloc, params -> registers
  │     ├── StatementGenerator.GenerateBlock(func.Body)
  │     │   (partials: StatementGenerator.cs / .Assignments.cs /
  │     │             .CompoundAssignments.cs / .ControlFlow.cs /
  │     │             .Declarations.cs / .InlineAsm.cs)
  │     └── epilogue: add rSP, pop savedRegs, ret / END for main
  └── EmitData for globals and pseudo-globals
  │
  ▼
AssemblerBase (Assembler | IRAssembler)
  │  Assembles in Build():
  │  - IRAssembler: pass 1 (ComputeLabelAddresses) -> pass 2 (EmitItem)
  │  - Assembler:   direct write + patch _patches
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
Bits 20-31  : not used
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
| `.dword` | `dd` | 4 | `.dword 0xDEADBEEF` | `[EF BE DE DE]` |
| `.quad` | `dq` | 8 | `.quad 0x...` | 8 bytes little-endian |
| `.ascii` | — | 1 | `.ascii "Hi"` | `[48 69]` |
| `.asciz` | `.string` | 1 (+NUL) | `.asciz "Hi"` | `[48 69 00]` |
| `.zero` | `.space` | N | `.zero 256` | 256 zero bytes |

**Limitations**

| Limitation | Reason | What to do |
|---|---|---|
| `.data` forbidden in inline-asm | `allowDataSection: false` | Move `.data` to a NANASM file |
| `.quad <label>` not supported | No label patching in `EmitData` | `.quad 0` + initialization in `__global_init` |
| `.align N` not supported | `EmitData` always aligns to 8 | Rely on auto-8 |
| `;` in a line is truncated | `Split(['\n', ';'])` in `ProcessLines` | Do not use `;` in `.ascii`/`.asciz` |
| Little-endian only | Nantuc platform | — |

### Diagnostics: `DiagnosticData` and AOT

Diagnostics arguments — `ReadOnlySpan<DiagnosticData>` (not `object[]`):

```csharp
public readonly record struct DiagnosticData
{
    public string Text { get; }
}
```

What this gives for AOT:
- No boxing of `int`/`char`/enum.
- No virtual `Object.ToString()` — the string is already ready.
- `Enum.ToString()` removed from diagnostics (`TokenTypeExtension.AsText` / `OpCode.AsText`).
- `Type.Name` is not passed to diagnostics.

**Result:** `DiagnosticData` is fully AOT-safe.

### `Assembler` vs `IRAssembler`

| | `Assembler` | `IRAssembler` |
|---|---|---|
| Storage | `MemoryStream` + `BinaryWriter` | `List<AsmItem>` |
| Label patching | `_patches` in `Build()` | `labelAddresses` in the first pass (`ComputeLabelAddresses`) |
| IR | ❌ | - |
| `HasNeed64IntData` check | ❌ (only on `EmitInstruction64`, `EmitInstruction64WithLabel` does not check — item 12) | - on `EmitInstruction64` (also not on `EmitInstruction64WithLabel`) |
| Duplicate label validation | - | - |
| Two-pass Build | ❌ | - (`Layout` -> `Emit`) |
| `.data` via `AssemblerParser` | - | - |
| `EmitData` | `Align8()` + `MarkLabel` + bytes | padding-`AsmData(alignment: 8)` + `MarkLabel` + `AsmData(data, alignment: 1)` |
| Padding buffer | `stackalloc byte[pad]` (0..7) per call | `stackalloc byte[7]` once per `Build()` (alignment limit ≤ 8, item 21) |
| `GetItems()` mutable | — | - (internal `List<AsmItem>`) |

## What to fix

In descending order of importance. Numbering preserved (gaps 3/5/6 — historical).

### `const` — incompleteness of semantics

1. **[CONST] `CheckConstTarget` for `MemberAccessNode` — TODO.**
   File: `ExpressionGenerator.Assignment.cs` (`CheckConstTarget`): `if (assign.LValue is MemberAccessNode) return;   // TODO`.
   **Consequence:** `struct Point p; p.x = 5` is not checked, even if the field is declared as `const int x` (which is not yet possible — `const` on struct fields is also not supported).
   **Fix:** extend `FieldInfo` with `IsConst`/`IsPointedConst`, thread through `StructLayout`, check in `CheckConstTarget`.

2. **[CONST] `const` qualifier in `extern` declarations — not parsed.**
   File: `Parser.TopLevel.cs`, `extern` block. Skips `*`, but not `const`.
   **Fix:** symmetrically to `ParseDeclSpec` (from `Parser.Declarations.cs`).

### API / Defects

4. **[API] `StatementGenerator.GenerateAssignment` — case 4 (direct assign) — dead code.**
   File: `StatementGenerator.Assignments.cs`.
   The condition `assign.LValue == null` is never satisfied: `Parser.ParseAssignment` for `IdentifierNode` creates `new AssignmentNode(...) { LValue = left, ... }` — `LValue` is always set.
   **Fix:** replace with `assign.LValue is IdentifierNode` or `assign.Name != null && assign.IndexExpr == null && assign.CompoundOp == null`.

7. **[API] `AsmInstruction(uint, ulong, bool)` — fallback via `ToString()`.**
   File: `ASM/IR/AsmInstruction.cs`.
   `TargetLabel = absoluteTarget.ToString()` — dead branch (`IRAssembler.EmitJumpToAbsolute` always `isAbsolute: true`).
   **Fix:** remove `isAbsolute` from the constructor, split by argument type.

8. **[API] `ExpressionGenerator.GenerateBinaryOp` — `&&`/`||` into `targetReg` via `Mov`.**
   File: `ExpressionGenerator.Binary.cs`.
   **Fix:** `GenerateLogicalAndOr(binop, targetReg)`.

9. **[API] `AssemblerParser.TryAddIntegerData` — `.quad <label>` not yet supported.**
   File: `ASM/Parser/AssemblerParser.DataSection.cs`.
   **Fix:** label patching mechanism in `EmitData`/`Build()`.

10. **[API] `AssemblerParser` — `;` inside a string is truncated.**
    File: `ASM/Parser/AssemblerParser.Lexing.cs` (`ProcessLines`: `code.Split(['\n', ';'])`).
    **Fix:** extract string parsing from the common `Split` (split by `\n`, handle `;` only outside literals) or add hex-escape.

11. **[API] `AssemblerParser` — duplicate dictionaries `_regMapLex`/`_opMapLex` + `_regMap`/`_opMap`.**
    File: `AsmLanguageDefinition.cs`.
    `_regMapLex`/`_opMapLex` — dead after the static constructor.
    **Fix:** initialize `FrozenDictionary` directly.

12. **[API] `IRAssembler.EmitInstruction64WithLabel` does not check `HasNeed64IntData`.**
    File: `ASM/IR/Emitеer/IRAssembler.cs`.
    **Fix:** extract the check into a common helper `Validate64BitOp(op)`.

13. **[API] `AssemblerParser` is not thread-safe.**
    File: `ASM/Parser/AssemblerParser.cs`. Instance-level fields (`_asm`, `_allowDataSection`, `_pendingData`, `_inDataSection`, `_pendingLabel`, `_currentLine`, `_opMap`, `_regMap`). `StatementGenerator.GenerateInlineAsm` (`StatementGenerator.InlineAsm.cs`) creates `new AssemblerParser()` for each `asm` — but the contract is not documented.
    **Fix:** `[ThreadStatic]`/`lock`, or explicitly document.

14. **[API] `RegisterAllocator.BuildInterferenceGraph` — linear live ranges, do not account for loops.**
    File: `RegisterAllocator.Interference.cs` + `VariableAccessAnalyzer.CollectVarEvents`.
    **Fix:** document as "best effort" or implement real live-range analysis.

15. **[API] `FunctionGenerator.GenerateGlobalInit` — struct initialization is silently unsupported.**
    File: `FunctionGenerator.cs`.
    `if (CodeGenUtils.IsStructType(global.Type, _structTable)) continue;` — a global struct with an `Initializer` is ignored.
    **Fix:** support or throw `NotSupported`.

### Home

16. **[Home] AST is laid out in files in `C/AST/`, but without a "map" in the folder header.**
    `CodeGenerator` is already laid out in `Expression/`/`Statement/`/`FunctionCtx/`/`FunctionGen/`/`RegistersAlloc/`, but AST is not.
    **Fix:** add `C/AST/README.md` or a `#region` map.

### XML

17. **[XML] AST hierarchy — without `///`.**
    Including `VariableNode.IsConst`/`IsPointedConst`, `ParameterNode.IsConst`/`IsPointedConst`, `FunctionNode.ReturnIsPointer`.

18. **[XML] `Parser` (all 6 partials) and `ExpressionGenerator`/`StatementGenerator` (all partials) — without `///`.**
    Especially `CheckConstTarget` (`ExpressionGenerator.Assignment.cs`), `GenerateIncrement` (`ExpressionGenerator.Increment.cs`), `GenerateAssignmentExpr` (`ExpressionGenerator.Assignment.cs`). The split into partials was done precisely for readability — a logical place to add `///` in the header of each partial.

19. **[XML] `CodeGenUtils` — without `///`.**
    `IsSignedType`, `EmitCastToType`, `GetAlignment(type, structTable)` (hardcoded `8` for structs).

20. **[XML] `AssemblerParser` — class header does not list `.data`, `allowDataSection`, `CASTZ`/`CASTS`.**
    File: `ASM/Parser/AssemblerParser.cs`. The class header mentions only basic formats; the partials `.Lexing.cs`/`.Instructions.cs`/`.DataSection.cs`/`.Numbers.cs` have no `///` at all.

### New (identified when splitting into partials)

21. **[API] `IRAssembler` — fixed-size padding buffer of 7 bytes.**
    File: `ASM/IR/Emitеer/IRAssembler.cs` (`Build()`: `Span<byte> padding = stackalloc byte[7]`), `ASM/IR/Emitеer/IRAssembler.Emit.cs` (`PadTo` uses `padding[..pad]`).
    When adding `AsmData` with `alignment > 8` (or another `AdvanceInstructionOffset` with alignment > 8) — `IndexOutOfRangeException`. Currently all `EmitData` calls use 1 or 8, and instructions use 8, so the limit is implicitly respected.
    **Fix:** either increase the buffer to `maxAlignment` (e.g., 32), or make `maxAlignment` a constant and explicitly document it; or generate padding dynamically.

22. **[API] `AssemblerParser.FormatLOAD_STORE` — does not support labels (numeric address only).**
    File: `ASM/Parser/AssemblerParser.Instructions.cs`.
    ```csharp
    ulong addr = ParseNumber(tokens[2]);   // "msg" -> FormatException
    ```
    The example from the README (`LOAD.S8 r1, msg`) does not work right now: `msg` is not parsed as a number. Symmetrically, `FormatLDI`/`FormatJumpType` already support labels.
    **Fix:** add an `IsNumber` branch + `EmitInstWithLabel`/`EmitInstruction64WithLabel` (or a separate method), or document the limitation and fix the example.

23. **[API] `AssemblerParser.TryAddStringData` — does not support `\xNN` escape.**
    File: `ASM/Parser/AssemblerParser.DataSection.cs`. Unknown escapes are treated as `(byte)inner[i]` (silently) — unlike `Parser.Literals.cs` (`Parser_InvalidCharLiteral`). Discrepancy between the C string and the NANASM string.
    **Fix:** unify escape handling; for symmetry with `Parser.Literals` support `\xNN`.

24. **[API] `AssemblerParser.ParseNumber` throws on invalid input, while `IsNumber` returns `false` — contract desynchronization.**
    File: `ASM/Parser/AssemblerParser.Numbers.cs`.
    `FormatLDI`/`FormatJumpType` check `IsNumber` before `ParseNumber`, but `FormatLOAD_STORE` does not. Address parsing errors arrive as `FormatException`/`OverflowException` from `ulong.Parse`/`Convert.ToUInt64`, not as `Asm_InvalidInstruction`.
    **Fix:** `TryParseNumber(string, out ulong)` + branch in `FormatLOAD_STORE` (see item 22).

25. **[API] `IRAssembler.ResolveJumpTarget` — `ErrorCode.Asm_UndefinedLabel` with position 0.**
    File: `ASM/IR/Emitеer/IRAssembler.Emit.cs`.
    ```csharp
    ErrorReporter.Throw(ErrorCode.Asm_UndefinedLabel, instr.TargetLabel!, 0);
    ```
    The second argument is not a token position, but a hardcoded `0`; the diagnostics lose context.
    **Fix:** either pass the real position (if available), or make the parameter optional (`long? position = null`).