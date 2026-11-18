﻿# Nantuc.Compiler — README

> Компилятор MiniC -> байткод Nantuc: лексер, парсер, AST, кодогенератор с регистровой раскраской, ассемблер (два backend'а — прямой и IR) и инфраструктура оптимизаций.
> [← Корень](../README.ru.md), [Common](../../src/Nantuc.Common/README.ru.md), [Contracts](../../src/Nantuc.Contracts/README.ru.md), [Kernel](../../src/Nantuc.Kernel/README.ru.md)

## Содержание
- [Об этом модуле](#об-этом-модуле)
- [Дерево локального проекта](#дерево-локального-проекта)
- [API](#api)
- [Примеры для API](#примеры-для-api)
- [Приближенность модуля к пользователю](#приближенность-модуля-к-пользователю)
- [Особенная информация](#особенная-информация)
  - [Каталог AST-узлов](#каталог-ast-узлов)
  - [Переполнение узких целых типов](#переполнение-узких-целых-типов)
  - [Константность (`const`) — семантика и ограничения](#константность-const--семантика-и-ограничения)
  - [Ассемблер NANASM: секции данных](#ассемблер-nanasm-секции-данных)
  - [Формат инструкции и `InstructionEncoder`](#формат-инструкции-и-instructionencoder)
  - [Диагностика: `DiagnosticData` и AOT](#диагностика-diagnosticdata-и-aot)
- [Что исправить](#что-исправить)

## Об этом модуле

`Nantuc.Compiler` — это frontend и backend компилятора платформы Nantuc. Frontend принимает исходник на MiniC (подмножество C: целочисленные типы, `struct`, указатели, массивы, `sizeof`, `asm`-блоки, **`const`-квалификаторы**, **overflow-семантика узких целых**), строит AST и генерирует байткод через двухступенчатую схему: кодогенератор -> ассемблер -> 32-битные слова ISA. Backend — двухуровневый: либо прямая запись в `MemoryStream` (`Assembler`), либо построение списка IR-инструкций (`IRAssembler`) с возможностью IR-оптимизаций.

Модуль зависит от `Common` (`OpCode`/`RegType`/`OpCodeSize`/`InstructionDecoder`), `Contracts` (`OptimizationId`/`OptimizationLogEntry`/`IReadOnlyLogOptimization`) и `Diagnostics` (`ErrorReporter`/`ErrorCode`, **`DiagnosticData`**). Сам по себе `unsafe`-кода не содержит — `stackalloc` только в `Assembler.Align8` (0–7 байт, WASM-safe).

Ключевые роли модуля:
- Frontend: `Lexer` (`Nantuc.Compiler.C.Lexer`), `Parser` (partial, 6 файлов, `Nantuc.Compiler.C.Parser`), **`Nantuc.Compiler.C.AST`** — 28 узлов AST в отдельных файлах, `MiniCLanguageDefinition` (`Nantuc.Compiler.C`), **`TokenTypeExtension`** (`Nantuc.Compiler.C.Lexer`).
- Кодогенератор: `ExpressionGenerator` (partial, 12 файлов, `Nantuc.Compiler.C.CodeGenerator.Expression`), `StatementGenerator` (partial, 6 файлов + `StatementTypeResolver`, `Nantuc.Compiler.C.CodeGenerator.Statement`), `FunctionGenerator` (partial, 5 файлов, `Nantuc.Compiler.C.CodeGenerator.FunctionGen`), `RegisterAllocator` + `GreedyGraphColorer` + `VariableAccessAnalyzer` + `RegisterAllocationResult` (`Nantuc.Compiler.C.CodeGenerator.RegistersAlloc`), `FunctionContext` + `SaveDepthEstimator` + `VariableLayoutBuilder` (`Nantuc.Compiler.C.CodeGenerator.FunctionCtx`), `VariableAccessor`, `GlobalMemoryManager`, `CodeGenUtils`, `VarLocation`, `GlobalInfo` (`Nantuc.Compiler.C.CodeGenerator`), **overflow-касты через `CASTZ`/`CASTS`**.
- Ассемблер: `AssemblerBase` (абстракция, `Nantuc.Compiler.ASM.Base`), `Assembler`/`AsmLanguageDefinition` (`Nantuc.Compiler.ASM`), **`AssemblerParser` — partial из 5 файлов** (`Nantuc.Compiler.ASM.Parser`), **`IRAssembler` — partial из 3 файлов** (`Nantuc.Compiler.ASM.IR.Emitеer`), `AsmExtensions`/`InstructionEncoder` (`Nantuc.Compiler.ASM.Utils`), IR-элементы — `AsmItem`/`AsmLabel`/`AsmData`/`AsmInstruction`/`IOptimizerIRRule`/`IROptimizer` (`Nantuc.Compiler.ASM.IR`).
  **`AssemblerParser` поддерживает секции `.data`/`.bss`/`.text`/`.code` и директивы данных** (`.byte`, `.word`, `.dword`, `.quad`, `.ascii`, `.asciz`, `.zero`).
- Оптимизации: `AstOptimizer`, `IROptimizer` — каркас с интерфейсами `IAstOptimizationRule`/`IOptimizerIRRule` (`Nantuc.Compiler.C.Optimizations` / `Nantuc.Compiler.ASM.IR`); **конкретных правил в этой партии нет**.
- Раскладка структур: `StructLayout`, `FieldInfo` (`Nantuc.Compiler.C`) — выравнивание полей по 8 байт, `Resolve` с итеративным разрешением зависимостей.
- Вспомогательное: **`WordRegexBuilder`** (`Nantuc.Compiler`) — единая точка построения `\b(...)\b`-регексов для `AsmLanguageDefinition` и `MiniCLanguageDefinition`.

> **XML-документация фрагментарна.** Полностью документированы `AssemblerParser` (шапка класса — но не синхронизирована с новыми опкодами, см. п. 20), `Assembler` (частично), `IOptimizerIRRule`, `IAstOptimizationRule`, `InstructionEncoder` (формат + регионы). Без `///` — вся AST-иерархия (28 типов в `C/AST/`, только inline `//`-комментарии в шапке файла), `Lexer`, **`Parser` и все его partial-файлы** (`Parser.Declarations.cs`/`.Expressions.cs`/`.Literals.cs`/`.Statements.cs`/`.TopLevel.cs`), **`ExpressionGenerator` и все его partial-файлы** (11 partial'ов в `CodeGenerator/Expression/`), **`StatementGenerator` и все его partial-файлы** (5 partial'ов + `StatementTypeResolver.cs` в `CodeGenerator/Statement/`), **`FunctionGenerator` и все его partial-файлы** (4 partial'а в `CodeGenerator/FunctionGen/`), **`FunctionContext`/`SaveDepthEstimator`/`VariableLayoutBuilder`** (`CodeGenerator/FunctionCtx/`), **`RegisterAllocator`/`GreedyGraphColorer`/`VariableAccessAnalyzer`** (`CodeGenerator/RegistersAlloc/`), `CodeGenUtils`, `GlobalMemoryManager`, `VariableAccessor`, **`IRAssembler` и его partial'ы** (`IRAssembler.Layout.cs`/`IRAssembler.Emit.cs`), **`AssemblerParser` и его partial'ы** (`AssemblerParser.Lexing.cs`/`.Instructions.cs`/`.DataSection.cs`/`.Numbers.cs`), `IROptimizer`, `AsmExtensions`, **`WordRegexBuilder`**. Подробности и дефекты — в разделе [Что исправить](#что-исправить).

## Дерево локального проекта

```
Nantuc.Compiler/
├── Nantuc.Compiler.csproj            # net10.0, refs Common+Contracts+Diagnostics
├── README.md
├── README.ru.md
├── WordRegexBuilder.cs               # Nantuc.Compiler — \b(...)\b regex builder (Asm + MiniC)
│
├── ASM/                              # Backend (namespace Nantuc.Compiler.ASM)
│   ├── AsmLanguageDefinition.cs      # Mnemonics/Registers (FrozenSet); regex'ы без Compiled
│   ├── Assembler.cs                  # прямая запись в MemoryStream + патч меток
│   │
│   ├── Base/                         # Nantuc.Compiler.ASM.Base
│   │   └── AssemblerBase.cs          # BaseAddress/ErrorReporter/SectionSize/DataSectionStart
│   │
│   ├── Parser/                       # Nantuc.Compiler.ASM.Parser
│   │   ├── AssemblerParser.cs        # поля, Assemble/Build, PendingData, [GeneratedRegex]
│   │   ├── AssemblerParser.Lexing.cs # ProcessLines/StripComments/секции/FlushPendingData;
│   │   │                             # NormalizeSizeExtension (GeneratedRegex, fast-path)
│   │   ├── AssemblerParser.Instructions.cs # ParseInstruction/SplitMnemonicAndSize/DispatchInstruction;
│   │   │                             # FormatR/FormatU/FormatIWithSize/FormatLDI/FormatJumpType/
│   │   │                             # FormatLOAD_STORE/FormatLOAD_IND_STORE_IND
│   │   ├── AssemblerParser.DataSection.cs # TryProcessDataDirective; TryAddIntegerData/StringData;
│   │   │                             # AddPendingData
│   │   └── AssemblerParser.Numbers.cs # ParseReg; TryParseSize; TryParseLong; ParseNumber; IsNumber
│   │
│   ├── IR/                           # Nantuc.Compiler.ASM.IR
│   │   ├── AsmItem.cs                # базовый IR-элемент
│   │   ├── AsmInstruction.cs         # авто-декодирует поля через InstructionDecoder
│   │   ├── AsmLabel.cs               # метка (Position)
│   │   ├── AsmData.cs                # данные с выравниванием (alignment)
│   │   ├── IOptimizerIRRule.cs
│   │   ├── IROptimizer.cs
│   │   │
│   │   └── Emitеer/                  # Nantuc.Compiler.ASM.IR.Emitеer
│   │       ├── IRAssembler.cs        # Emit*/MarkLabel/GetItems/Build (entry point)
│   │       ├── IRAssembler.Layout.cs # проход 1: ComputeLabelAddresses; AdvanceInstructionOffset
│   │       └── IRAssembler.Emit.cs   # проход 2: EmitItem/EmitInstructionItem/EmitDataItem/PadTo
│   │
│   └── Utils/                        # Nantuc.Compiler.ASM.Utils
│       ├── AsmExtensions.cs          # Emit* + EmitInstI на AssemblerBase
│       └── InstructionEncoder.cs     # Encode/Decode (вкл. CASTZ/CASTS)
│                                     # + extension-блоки на RegType/OpCode/OpCodeSize
│
└── C/                                # Frontend (namespace Nantuc.Compiler.C)
    ├── MiniCLanguageDefinition.cs    # Nantuc.Compiler.C — Keywords/Operators (FrozenSet)
    ├── StructLayout.cs               # Nantuc.Compiler.C — StructLayout (Dictionary), FieldInfo
    │
    ├── Lexer/                        # Nantuc.Compiler.C.Lexer
    │   ├── Lexer.cs                  # Tokenize(), позиционирование
    │   ├── Lexer.Trivia.cs           # пробелы и комментарии
    │   ├── Lexer.Literals.cs         # идентификаторы, числа, строки, символы
    │   ├── Lexer.Operators.cs        # операторы (табличный разбор)
    │   ├── Token.cs                  # Token (Type/Value/Line/Column)
    │   ├── TokenType.cs              # enum TokenType (+ Arrow)
    │   └── TokenTypeExtension.cs     # extension(TokenType).AsText — switch без рефлексии
    │
    ├── Parser/                       # Nantuc.Compiler.C.Parser
    │   ├── Parser.cs                 # partial class Parser — поля, Parse(), Advance(), Expect()
    │   ├── Parser.TopLevel.cs        # struct / #include / extern / global / function
    │   ├── Parser.Declarations.cs    # ParseDeclSpec/ParseDeclarators/ParseVariableTail/ParseFunction
    │   ├── Parser.Expressions.cs     # ParseAssignment..ParsePrimary (13 уровней приоритета)
    │   ├── Parser.Literals.cs        # Number/Char/String/Hex — литералы
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
    │   ├── CodeGenUtils.cs           # размеры/выравнивание + IsSignedType + EmitCastToType
    │   ├── GlobalInfo.cs             # + IsSigned
    │   ├── GlobalMemoryManager.cs    # реестр глобалов, IsSigned вычисляется
    │   ├── VariableAccessor.cs       # load/store с канонизацией через CASTZ/CASTS
    │   ├── VarLocation.cs            # + IsSigned
    │   │
    │   ├── FunctionCtx/              # Nantuc.Compiler.C.CodeGenerator.FunctionCtx
    │   │   ├── FunctionContext.cs    # контекст + ReturnSize/ReturnIsSigned/ReturnIsVoid
    │   │   ├── SaveDepthEstimator.cs # оценка ExpressionSaveDepth по AST (чистая функция)
    │   │   └── VariableLayoutBuilder.cs # VarInfo -> VarLocation (стек/регистр)
    │   │
    │   ├── FunctionGen/              # Nantuc.Compiler.C.CodeGenerator.FunctionGen
    │   │   ├── FunctionGenerator.cs           # оркестратор
    │   │   ├── FunctionGenerator.DataSection.cs # эмиссия данных (глобалы + pseudo-params)
    │   │   ├── FunctionGenerator.Prologue.cs  # пролог + маршалинг параметров
    │   │   ├── FunctionGenerator.Epilogue.cs  # эпилог
    │   │   ├── ConstantEvaluator.cs           # свёртка констант в инициализаторах
    │   │   └── LocalVarCollector.cs           # обход AST для сбора локальных переменных
    │   │
    │   ├── RegistersAlloc/           # Nantuc.Compiler.C.CodeGenerator.RegistersAlloc
    │   │   ├── RegisterAllocator.cs           # оркестратор (public API)
    │   │   ├── RegisterAllocator.Interference.cs # граф интерференции
    │   │   ├── GreedyGraphColorer.cs          # жадная раскраска (Welsh–Powell)
    │   │   ├── VariableAccessAnalyzer.cs      # AST-walker'ы (def/use, address-taken)
    │   │   └── RegisterAllocationResult.cs    # VarToRegister + UsedRegisters
    │   │
    │   ├── Expression/               # Nantuc.Compiler.C.CodeGenerator.Expression
    │   │   ├── ExpressionGenerator.cs            # корень + GenerateExpression(expr[, targetReg])
    │   │   ├── ExpressionGenerator.Arrays.cs     # ArrayAccess/StoreR0ToArrayElement
    │   │   ├── ExpressionGenerator.Assignment.cs # GenerateAssignmentExpr + CheckConstTarget
    │   │   ├── ExpressionGenerator.Binary.cs     # GenerateBinaryOp + logical/compare
    │   │   ├── ExpressionGenerator.Calls.cs      # FunctionCall/IndirectCall + call-site касты
    │   │   ├── ExpressionGenerator.Conditions.cs # GenerateCondition (&&/||, сравнения, bool-контекст)
    │   │   ├── ExpressionGenerator.Helpers.cs    # SaveRegToStack/RestoreRegFromStack
    │   │   ├── ExpressionGenerator.Increment.cs  # GenerateIncrement + const-проверки
    │   │   ├── ExpressionGenerator.MemberAccess.cs # GenerateMemberAddress + arrow/dot
    │   │   ├── ExpressionGenerator.PointerOps.cs # ApplyPointerArithmeticScaling + AddressOf/Dereference
    │   │   ├── ExpressionGenerator.Sizeof.cs     # SizeofType/SizeofExpr
    │   │   └── ExpressionGenerator.Unary.cs      # GenerateUnaryOp (-, !, ~)
    │   │
    │   └── Statement/                # Nantuc.Compiler.C.CodeGenerator.Statement
    │       ├── StatementGenerator.cs             # корень + switch по узлам
    │       ├── StatementGenerator.Assignments.cs # GenerateAssignment (4 lvalue-ветки)
    │       ├── StatementGenerator.CompoundAssignments.cs # x op= / arr[i] op= / *p op= / obj.f op=
    │       ├── StatementGenerator.ControlFlow.cs # If/While/DoWhile/For/Break/Continue/Return
    │       ├── StatementGenerator.Declarations.cs # объявления локальных + init
    │       ├── StatementGenerator.InlineAsm.cs   # InlineAsmNode -> new AssemblerParser()
    │       └── StatementTypeResolver.cs          # IsArrayElemSigned/IsPointedSigned/IsFieldSigned
    │
    └── Optimizations/                # Nantuc.Compiler.C.Optimizations
        ├── AstOptimizer.cs
        ├── IAstOptimizationRule.cs
        └── ILogOptimization.cs       # контракт лога оптимизации (+ OptimizationResult)
```

Замечания по организации:
- Frontend (`C/*`) знает про байткод через `OpCode`/`RegType`/`OpCodeSize`, но не про `Assembler`/`IRAssembler` — кодогенератор работает через `AssemblerBase`-extension-методы из `Utils/AsmExtensions.cs`/`Utils/InstructionEncoder.cs`.
- **AST-узлы вынесены в отдельный namespace `Nantuc.Compiler.C.AST` и физически разложены по файлам в `C/AST/`** (по одному узлу на файл, кроме базового `ASTNode.cs`).
- **`Lexer` разложен по папке `C/Lexer/`** (`Nantuc.Compiler.C.Lexer`): `Lexer` + 3 partial'а + `Token` + `TokenType` + `TokenTypeExtension`.
- **`Parser` — partial class из 6 файлов в `C/Parser/`** (`Nantuc.Compiler.C.Parser`). Публичный API один — `Parse()`; partial-разбиение только для читаемости.
- **`ExpressionGenerator` — partial class из 12 файлов в `CodeGenerator/Expression/`** (`Nantuc.Compiler.C.CodeGenerator.Expression`). **`StatementGenerator` — partial class из 6 файлов в `CodeGenerator/Statement/`** (`Nantuc.Compiler.C.CodeGenerator.Statement`) + `StatementTypeResolver.cs`. Разбиение сделано для XML-документации и навигации; API-методы не изменились.
- **`FunctionGenerator` — partial class из 5 файлов в `CodeGenerator/FunctionGen/`** (`Nantuc.Compiler.C.CodeGenerator.FunctionGen`): оркестратор + DataSection + Prologue + Epilogue + `ConstantEvaluator` + `LocalVarCollector`.
- **`FunctionContext`/`SaveDepthEstimator`/`VariableLayoutBuilder` — в `CodeGenerator/FunctionCtx/`** (`Nantuc.Compiler.C.CodeGenerator.FunctionCtx`).
- **`RegisterAllocator`/`RegisterAllocator.Interference`/`GreedyGraphColorer`/`VariableAccessAnalyzer`/`RegisterAllocationResult` — в `CodeGenerator/RegistersAlloc/`** (`Nantuc.Compiler.C.CodeGenerator.RegistersAlloc`).
- Два backend'а: `Assembler` (прямой, `MemoryStream` + `BinaryWriter`) и `IRAssembler` (список `AsmItem`). `AssemblerParser` работает с любым через `AssemblerBase`.
- **`AssemblerParser` — partial class из 5 файлов в `ASM/Parser/`** (`Nantuc.Compiler.ASM.Parser`): `AssemblerParser.cs` (поля, `Assemble`/`Build`, `PendingData`, `[GeneratedRegex]`), `.Lexing.cs` (строки/комментарии/секции + `NormalizeSizeExtension` + `FlushPendingData`), `.Instructions.cs` (разбор мнемоник + `Format*` хелперы), `.DataSection.cs` (директивы данных), `.Numbers.cs` (регистры/числа). Публичный API — `Assemble(...)`/`Build(...)`.
- **`IRAssembler` — partial class из 3 файлов в `ASM/IR/Emitеer/`** (`Nantuc.Compiler.ASM.IR.Emitеer`): `IRAssembler.cs` (Emit*/MarkLabel/GetItems/Build), `.Layout.cs` (проход 1: `ComputeLabelAddresses`), `.Emit.cs` (проход 2: `EmitItem`/`EmitInstructionItem`/`EmitDataItem`/`PadTo`/`ResolveJumpTarget`).
- Оптимизации: `AstOptimizer` (AST-level) и `IROptimizer` (IR-level) — оба каркасы, ждут конкретных правил.
- **`TokenTypeExtension.AsText`** — единственное место, где `TokenType` превращается в строку; используется везде в диагностике вместо `Enum.ToString()` (AOT-safe, без рефлексии, без боксинга).
- **`WordRegexBuilder`** (`Nantuc.Compiler`) — единая точка построения `\b(...)\b`-паттернов; используется `AsmLanguageDefinition` и `MiniCLanguageDefinition`, чтобы формы не разъезжались.
- **`AsmLanguageDefinition.Mnemonics`/`Registers`** — источник истины для NANASM-скриптов, но **не синхронизирован с XML-шапкой `AssemblerParser`** (см. п. 20).

## API

### Общая картина

| Тип | Namespace | Вид | Назначение |
|---|---|---|---|
| `ASTNode` + 28 наследников | `C.AST` | `abstract class` / `class` | Иерархия AST. Полный каталог — в разделе [Каталог AST-узлов](#каталог-ast-узлов) |
| `Lexer` | `C.Lexer` | `partial class` | Токенизация; `Token`, `TokenType`; **3-символьные операторы `<<=`/`>>=`** |
| `TokenTypeExtension` | `C.Lexer` | `static class` | `extension(TokenType).AsText` — ручной switch, AOT-safe |
| `Parser` | `C.Parser` | `partial class` (6 файлов) | Recursive descent -> `ProgramNode`; **`ParseDeclSpec` для `const`**, **`ParseFunction(..., isReturnPointer)`** |
| `MiniCLanguageDefinition` | `C` | `static partial class` | `Keywords`/`Operators` (`FrozenSet`); `const` в `Keywords` |
| `StructLayout` / `FieldInfo` | `C` | `class` / `record` | Раскладка структур; `_fields` — `Dictionary<string, FieldInfo>` |
| `WordRegexBuilder` | `Nantuc.Compiler` | `static class` | `Build(FrozenSet<string>)` -> `\b(...)\b` regex (AOT-safe, без `Compiled`) |
| `CodeGenUtils` | `C.CodeGenerator` | `static class` | `GetSizeForType`/`GetSizeInBytes`/`GetAlignment`/`IsPrimitiveType`/`IsSignedType`; extension `EmitCastToType`, `EmitMultiplyByConstant` |
| `VariableAccessor` | `C.CodeGenerator` | `class` | `LoadToRegister`/`StoreFromRegister` с **`EmitCastToType`** после LOAD и после MOV в home-регистр |
| `GlobalMemoryManager` | `C.CodeGenerator` | `class` | `Allocate`/`Contains`/`GetInfo`/`GetLabel`; **`IsSigned` вычисляется в `Allocate`** |
| `GlobalInfo` | `C.CodeGenerator` | `class` | Метаданные глобала; **`IsSigned`** |
| `VarLocation` | `C.CodeGenerator` | `class` | Метаданные переменной; **`IsSigned`** |
| `FunctionContext` | `C.CodeGenerator.FunctionCtx` | `class` | `Create(...)` -> `VarMap`, `TotalLocalSize`, `SaveAreaSize`; **`ReturnSize`/`ReturnIsSigned`/`ReturnIsVoid`**, **`VarLocation.IsSigned`** |
| `SaveDepthEstimator` | `C.CodeGenerator.FunctionCtx` | `static class` | `Estimate(ASTNode)` — максимальная глубина `ExpressionSaveDepth` |
| `VariableLayoutBuilder` | `C.CodeGenerator.FunctionCtx` | `internal static class` | `VarInfo` -> `VarLocation` (регистр или стек); вычисляет `TypeSize`/`IsSigned`/`StructTypeName` |
| `ExpressionGenerator` | `C.CodeGenerator.Expression` | `partial class` (12 файлов) | Генерация выражений; **`GenerateAssignmentExpr` с 4 lvalue-ветками**, **`CheckConstTarget`** (internal), **overflow-касты** |
| `StatementGenerator` | `C.CodeGenerator.Statement` | `partial class` (6 файлов) | Генерация операторов; **вызывает `_exprGen.CheckConstTarget`**; **overflow-касты на всех lvalue** |
| `StatementTypeResolver` | `C.CodeGenerator.Statement` | `static class` | `IsArrayElemSigned`/`IsPointedSigned`/`IsFieldSigned` — вынесено из `StatementGenerator` |
| `FunctionGenerator` | `C.CodeGenerator.FunctionGen` | `partial class` (5 файлов) | `Generate(ProgramNode)`, пролог/эпилог; **каст аргументов r0..r3 и pseudo-globals** |
| `ConstantEvaluator` | `C.CodeGenerator.FunctionGen` | `internal static class` | `Evaluate(ASTNode, structTable, er)` — свёртка констант для data-секции |
| `LocalVarCollector` | `C.CodeGenerator.FunctionGen` | `internal static class` | `Collect(BlockNode)` -> `Dictionary<string, VariableNode>` |
| `RegisterAllocator` | `C.CodeGenerator.RegistersAlloc` | `static partial class` (2 файла) | `AllocateRegisters(...)` -> `RegisterAllocationResult`; `CollectAddressTakenVars` (через `VariableAccessAnalyzer`) |
| `GreedyGraphColorer` | `C.CodeGenerator.RegistersAlloc` | `internal static class` | `Color(graph, availableRegs)` — жадная раскраска по убыванию степени |
| `VariableAccessAnalyzer` | `C.CodeGenerator.RegistersAlloc` | `internal static class` | `CollectAddressTakenVars` (проход 1) + `CollectVarEvents` (проход 2) |
| `RegisterAllocationResult` | `C.CodeGenerator.RegistersAlloc` | `class` | `VarToRegister` (`Dictionary<string, RegType?>`) + `UsedRegisters` (`HashSet<RegType>`) |
| `AstOptimizer` | `C.Optimizations` | `class` | `Add`/`Insert`/`Remove`/`Optimize(ProgramNode)` -> `IEnumerable<IReadOnlyLogOptimization>` |
| `IAstOptimizationRule` / `ILogOptimization` | `C.Optimizations` | `interface` | Контракт правила / контракт лога |
| `OptimizationResult` | `C.Optimizations` | `class : ILogOptimization` | Простая реализация лога |
| `AssemblerBase` | `ASM.Base` | `abstract class` | `Build`/`EmitInstruction`/`EmitInstruction64`/`EmitInstruction64WithLabel`/`EmitJump`/`EmitJumpToAbsolute`/`MarkLabel`/`EmitData`; свойства `BaseAddress`/`ErrorReporter`/`CodeSectionSize`/`DataSectionStart`/`DataSectionSize` |
| `Assembler` | `ASM` | `class : AssemblerBase` | Прямая запись; `_patches`; `DataSection*`/`CodeSectionSize` в `Build()` |
| `AssemblerParser` | `ASM.Parser` | `partial class` (5 файлов) | `Assemble(code, baseAddress, ErrorReporter)`, `Build(code, asm)`, `Assemble(code, asm, allowDataSection)`; секции `.data`/`.bss`/`.text`/`.code`; **`FormatIWithSize` для `CASTZ`/`CASTS`**; `PendingData`; `[GeneratedRegex]` для `NormalizeSizeExtension` |
| `AsmLanguageDefinition` | `ASM` | `static class` | `Mnemonics`/`Registers` (`FrozenSet`); regex'ы без `Compiled` |
| `IRAssembler` | `ASM.IR.Emitеer` | `partial class : AssemblerBase` (3 файла) | `GetItems()`, двухпроходный `Build()`, padding-`AsmData`; `stackalloc byte[7]` в `Build()` |
| `AsmItem`/`AsmLabel`/`AsmData`/`AsmInstruction` | `ASM.IR` | `class` | IR-элементы (каждый в своём файле); `AsmInstruction` авто-декодирует поля через `InstructionDecoder` |
| `IOptimizerIRRule` | `ASM.IR` | `interface` | `TryOptimize(items, index, passCount, out newIndex)`, `GetLogs()`, `Clear()` |
| `IROptimizer` | `ASM.IR` | `class` | `Optimize(items, maxPasses = 5)` — до неподвижной точки; `Clear()` на входе |
| `InstructionEncoder` | `ASM.Utils` | `static class` | `Encode`/`EncodeR`/`EncodeI`/`EncodeU`/`EncodeJ` + `EncodeLDI`/…; **`Decode(uint)`** с case'ами для `CASTZ`/`CASTS`; extension-блоки |
| `AsmExtensions` | `ASM.Utils` | `static class` | `EmitMov`/…/`EmitInstI`/`EmitInstR`/`EmitInstU`/`EmitInstJ`/`EmitInstWithData` |

### Frontend

**`Lexer(source, ErrorReporter)`** (`Nantuc.Compiler.C.Lexer`) — `Tokenize() -> List<Token>`. Поддерживает `//`/`/* */`, hex (`0x`), char/string-литералы, `->` как отдельный `TokenType.Arrow`. **3-символьные операторы `<<=`/`>>=` распознаются в `ReadOperator` до проверки на 2-символьные** — иначе `x >>= 1` режется на `>>` + `=` и падает в `Parser.ParseAssignment`. `Peek(offset = 1)` — единственный lookahead. Разложен по `C/Lexer/` (`Lexer.cs` + `.Trivia.cs` + `.Literals.cs` + `.Operators.cs` + `Token.cs` + `TokenType.cs` + `TokenTypeExtension.cs`).

**`Parser(tokens, ErrorReporter)`** (`Nantuc.Compiler.C.Parser`) — `partial class` из 6 файлов в `C/Parser/`. Публичный контракт — `Parse() -> ProgramNode` (namespace `Nantuc.Compiler.C.AST`); остальные методы — `private`/`internal`, распределены по partial'ам:
- `Parser.cs` — корень (конструктор, поля, `Parse()`, `Advance()`, `Expect()`).
- `Parser.TopLevel.cs` — `Parse()`: `struct`, `#include`, `extern`, функции, глобалы; `_structNames` пополняется по мере встречи.
- `Parser.Declarations.cs` — `ParseDeclSpec`/`ParseDeclarators`/`ParseVariableTail`/`ParseFunction(type, name, isReturnPointer)`.
- `Parser.Expressions.cs` — `ParseAssignment` -> `ParseLogicalOr` -> `ParseLogicalAnd` -> `ParseBitwiseOr`/`Xor`/`And` -> `ParseEquality` -> `ParseRelational` -> `ParseShift` -> `ParseAdditive` -> `ParseMultiplicative` -> `ParseUnary` -> `ParsePrimary`.
- `Parser.Statements.cs` — `ParseBlock`/`ParseBody`/`ParseIf`/`ParseWhile`/`ParseDoWhile`/`ParseFor`/`ParseReturn`/`ParseBreak`/`ParseContinue`/`ParseInlineAsm`.
- `Parser.Literals.cs` — `ParseStringToNodes`/`ParseCharLiteral`/`ParseHexChar`.

`Parse` обрабатывает:
- `struct Name { ... };` — `_structNames` пополняется по мере встречи, поддерживаются и `Name`, и `struct Name`.
- `#include "path"` — добавляется в `IncludesList`.
- `extern <type> <name>(<params>);` — `FunctionNode` с `IsExternal = true`, `Body = null!`.
- Функции и глобальные переменные через `ParseDeclSpec`/`ParseDeclarators`.
- `asm { ... }` — `InlineAsmNode` с текстом.
- Постфиксные `++`/`--`, `.`/`->`, `[i]`, `(args)` — в цикле `ParsePrimary` (в `Parser.Expressions.cs`).
- `sizeof` — в `ParseUnary`, включая `sizeof(const T)` и `sizeof(const T*)`.
- `const`-квалификаторы — `ParseDeclSpec`.
- **`ParseFunction(type, name, isReturnPointer)` — `isReturnPointer` пробрасывается в `FunctionNode.ReturnIsPointer`** (нужно для overflow-каста `return`).
- **Касты к примитивам** - выражения вида `(byte)300` округлит его до `44`, ограничения: `*(int*)` еще не поддерживается, привидения к пользовательским структурам также не поддерживаются

**AST-иерархия (`Nantuc.Compiler.C.AST`)** — 28 классов, разложены по файлам в `C/AST/`. Базовый `ASTNode` — единственное абстрактное свойство `NameNode` (`=> nameof(XxxNode)`). `AssignmentNode` содержит `LValue`, `CompoundOp`, `IndexExpr`. `VariableNode` содержит `IsPointer`/`PointedType`/`IsArray`/`ArraySize`/`ArrayInitializer` + `IsConst`/`IsPointedConst`. `ParameterNode` + `IsConst`/`IsPointedConst`. **`FunctionNode` — `LocalSize`, `IsExternal`, `ReturnIsPointer`**.

**`TokenTypeExtension`** (`Nantuc.Compiler.C.Lexer`) — `extension(TokenType).AsText` возвращает читаемое имя через ручной `switch`, без `Enum.ToString()`. Используется во всех сообщениях парсера — **AOT-safe, без рефлексии, без боксинга**.

**`MiniCLanguageDefinition`** (`Nantuc.Compiler.C`) — `Keywords`/`Operators` (`FrozenSet`), `const` в `Keywords`. **`GetKeywordRegex()` — dead code** (заменён на `WordRegexBuilder`).

**`WordRegexBuilder`** (`Nantuc.Compiler`) — `Build(FrozenSet<string>, RegexOptions)` -> `new($@"\b({string.Join("|", words)})\b", options)`. Единая точка для `AsmLanguageDefinition` и `MiniCLanguageDefinition`; **намеренно без `[GeneratedRegex]`**, чтобы не хардкодить паттерн (см. историю drift'а в `MiniCLanguageDefinition`).

### Кодогенератор

**`FunctionGenerator.Generate(ProgramNode)`** (`Nantuc.Compiler.C.CodeGenerator.FunctionGen`) — последовательность:
1. `GlobalMemoryManager` + `Allocate` для всех `GlobalVarNodes`, `IsConst`/`IsPointedConst`/`IsSigned` вычисляются автоматически.
2. `_functionTable` заполняется из `FunctionNodes`.
3. `MarkLabel("__global_init")` + `GenerateGlobalInit` — инициализация глобалов (только скаляры, структуры пропускаются).
4. `EmitJmp("func_main")`.
5. `GenerateFunction` для каждой не-external функции.
6. `EmitData` для глобалов (с `ArrayInitializer`).
7. `EmitData` для pseudo-globals параметров 4+.

**`FunctionContext.Create`** (`Nantuc.Compiler.C.CodeGenerator.FunctionCtx`) — вычисляет:
- `VarMap`: `VarLocation` для каждого параметра и локальной переменной, включая **`IsSigned`** (для non-pointer, non-struct) — через `VariableLayoutBuilder.Build`.
- `TotalLocalSize` — суммарный размер стековых переменных, выровненный по 8.
- `SaveAreaSize = (SaveDepthEstimator.Estimate(body) + 1) * 8`.
- `UsedRegisters` — регистры из `RegisterAllocator`.
- **`ReturnSize`/`ReturnIsSigned`/`ReturnIsVoid`** — для каста `return` в `GenerateReturn`.

**`SaveDepthEstimator`** (`Nantuc.Compiler.C.CodeGenerator.FunctionCtx`) — чистая функция `Estimate(ASTNode?) -> int`; по каждому узлу возвращает оценку максимальной глубины `ExpressionSaveDepth`. Каждый `SaveSlot` в switch соответствует одному одновременно занятому save-слоту (см. `ExpressionGenerator.SaveRegToStack`). Легко тестируется отдельно (см. `SaveDepthEstimatorTests`).

**`VariableLayoutBuilder`** (`Nantuc.Compiler.C.CodeGenerator.FunctionCtx`) — `internal static class`. `Build(VarInfo, stackOffset, allocation, structTable, er, out consumedBytes)` -> `VarLocation`. Логика: если переменная — скаляр/не массив/не структура и `RegisterAllocator` дал регистр -> `MakeRegisterLocation`; иначе -> `MakeStackLocation` (с `TypeSize`/`StructTypeName`/`IsSigned`).

**`RegisterAllocator.AllocateRegisters`** (`Nantuc.Compiler.C.CodeGenerator.RegistersAlloc`) — жадная раскраска:
- Резервирует `r0..r4`, `r27`, `rFL`, `rSP`, `rHP`, `rIP` — **22 доступных** (`r5..r26`; комментарий в коде «23 регистра» — неверен, см. п. 14).
- **Исключает переменные, чей адрес берётся (`&x`)** — `VariableAccessAnalyzer.CollectAddressTakenVars` до раскраски.
- Строит граф по `[firstDef, lastUse]` в `RegisterAllocator.Interference.cs`, порядок раскраски — по убыванию степени (`GreedyGraphColorer`).
- Возвращает `RegisterAllocationResult` (`VarToRegister` + `UsedRegisters`).

**`VariableAccessAnalyzer`** (`Nantuc.Compiler.C.CodeGenerator.RegistersAlloc`) — два независимых прохода: `CollectAddressTakenVars` (имена `x`, к которым применён `&x`) и `CollectVarEvents` (позиции def/use в линейном обходе). Важно: проходы НЕ объединены — по-разному обходят `AssignmentNode` (см. п. 14).

**`ExpressionGenerator`** — `partial class` из 12 файлов (namespace `Nantuc.Compiler.C.CodeGenerator.Expression`, папка `Expression/`). Публичный API один — `GenerateExpression(expr)` (в `r0`) и `GenerateExpression(expr, targetReg)`; внутри распределено по partial'ам:
- `ExpressionGenerator.cs` — корень + `GenerateExpression(expr)` / `GenerateExpression(expr, targetReg)`.
- `ExpressionGenerator.Assignment.cs` — `GenerateAssignmentExpr(assign, targetReg)` — **assignment-as-expression** (результат = новое значение). Покрывает:
  - `arr[i] = value` (через `StoreR0ToArrayElement`)
  - `*ptr = value` (через `GenerateExpression(operand, r0)` + `STORE_IND`)
  - `obj.field = value` (через `GenerateMemberAddress(member, r0)` + `STORE_IND`)
  - `x = value` (через `StoreFromRegister`)
  - compound: `arr[i] op= value`, `*p op= value`, `obj.f op= value`, `x op= value` — через `GenerateCompoundAssignmentExpr` -> `GenerateCompoundIndirectExpr` / `GenerateCompoundIdentExpr`.
  - **`CheckConstTarget` (internal) в начале** — единая точка проверки `const`.
  - **`EmitCastToType` после каждой записи** — overflow для узких типов.
- `ExpressionGenerator.Binary.cs` — `GenerateBinaryOp`:
  - Сравнения — `Cmp` + прыжки -> `1`/`0`.
  - `&&`/`||` — `GenerateLogicalAndOr`, результат в `r0`, потом `Mov(targetReg, r0)`.
  - Свёртка констант: `NumberNode op NumberNode`.
  - Оптимизация `targetReg == левый операнд`: правый в `r1`/`r2`, `ApplyPointerArithmeticScaling` для `+`/`-` с указателями.
  - Указательная арифметика: `ptr ± int` — масштабирование `int` на `sizeof(*ptr)`; `int + ptr` — то же. `ptr - ptr` не масштабируется.
- `ExpressionGenerator.PointerOps.cs` — `GenerateAddressOf` + `GenerateDereference` + `ApplyPointerArithmeticScaling`.
- `ExpressionGenerator.Arrays.cs` — `ArrayAccessNode` + `StoreR0ToArrayElement`.
- `ExpressionGenerator.Calls.cs` — `FunctionCallNode`/`IndirectCallNode` + call-site касты аргументов к типу параметра.
- `ExpressionGenerator.Conditions.cs` — `GenerateCondition` (раскрытие `&&`/`||`, сравнения, сравнение с нулём).
- `ExpressionGenerator.Helpers.cs` — `SaveRegToStack`/`RestoreRegFromStack`; `ExpressionSaveDepth`, слот = `TotalLocalSize + ExpressionSaveDepth * 8`, адрес через `ADDR_SCRATCH_REG_EN = r27`.
- `ExpressionGenerator.Increment.cs` — **`GenerateIncrement`** — `++`/`--` для `Identifier`/`ArrayAccess`/`Dereference`/`MemberAccess`. В начале — `const`-проверки. После `Inc`/`Dec` и после load — `EmitCastToType`.
- `ExpressionGenerator.MemberAccess.cs` — `GenerateMemberAddress` + `.`/`->`.
- `ExpressionGenerator.Sizeof.cs` — `SizeofTypeNode`/`SizeofExprNode`.
- `ExpressionGenerator.Unary.cs` — `GenerateUnaryOp` (для `-`, `!`, `~`).

**`StatementGenerator`** — `partial class` из 6 файлов + `StatementTypeResolver` (namespace `Nantuc.Compiler.C.CodeGenerator.Statement`, папка `Statement/`). Публичный API — `GenerateBlock(BlockNode)`; внутри распределено по partial'ам:
- `StatementGenerator.cs` — корень + switch по узлам, `_loopStack`.
- `StatementGenerator.Assignments.cs` — `GenerateAssignment` (4 lvalue-ветки); **в начале вызывает `_exprGen.CheckConstTarget`**.
- `StatementGenerator.CompoundAssignments.cs` — `GenerateCompoundAssignment` -> `GenerateCompoundArrayElement` / `GenerateCompoundDeref` / `GenerateCompoundMember` / `GenerateCompoundIdent`; **overflow-касты на всех lvalue**.
- `StatementGenerator.ControlFlow.cs` — `GenerateIf`/`GenerateWhile`/`GenerateFor`/`GenerateDoWhile`/`GenerateReturn` (с `EmitCastToType` к типу возврата)/`GenerateBreak`/`GenerateContinue`.
- `StatementGenerator.Declarations.cs` — объявления локальных + инициализация.
- `StatementGenerator.InlineAsm.cs` — `InlineAsmNode` -> `new AssemblerParser().Assemble(...)`.
- `StatementTypeResolver.cs` — `IsArrayElemSigned`/`IsPointedSigned`/`IsFieldSigned` (вынесено из `StatementGenerator` для читаемости).

### Ассемблер

**`AssemblerBase`** (`Nantuc.Compiler.ASM.Base`) — абстракция эмиттера. Публичные свойства:
- `BaseAddress` — адрес для патча прыжков.
- `ErrorReporter` — публичное readonly-поле.
- `CodeSectionSize` / `DataSectionStart` / `DataSectionSize` — `{ get; protected set; }`, заполняются в `Build()`.

**`Assembler`** (`Nantuc.Compiler.ASM`) — прямая запись в `MemoryStream`:
- `EmitInstruction(uint)` — 4 байта.
- `EmitInstruction64(uint, ulong)` — 4 + `Align8()` + 8.
- `EmitInstruction64WithLabel(uint, string)` / `EmitJump(uint, string)` — 4 + `Align8()` + placeholder, патч в `Build()`.
- `EmitJumpToAbsolute(uint, ulong)` — 4 + `Align8()` + 8, без патча.
- `EmitData(string, byte[])` — `Align8()`, `MarkLabel`, запись.
- `Build()` — фиксирует `DataSectionStart`/`DataSectionSize`/`CodeSectionSize`, патчит `_patches`.
- `Align8()` — `stackalloc byte[pad]` (0..7 байт).

**`AssemblerParser`** (`Nantuc.Compiler.ASM.Parser`) — `partial class` из 5 файлов (`AssemblerParser.cs` + `.Lexing.cs` + `.Instructions.cs` + `.DataSection.cs` + `.Numbers.cs`). Текстовый ассемблер. **Полный набор инструкций:**
- Без операндов: `NOP`, `END`, `RET`, `HALT`, `WAKE`.
- R-формат (2 регистра): `MOV`, `MOD`, `ADD`, `SUB`, `AND`, `OR`, `XOR`, `IN`, `OUT`, `SHR`, `SHL`, `DIV`, `MULT_INT`, `CMP`, `TEST`.
- U-формат (1 регистр): `JMP_IND`, `CALL_IND`, `WAKE_INT`, `INC`, `DEC`, `NOT`, `PUSH`, `POP`.
- **I+size-формат (1 регистр + `DataSize`): `CASTZ[.S8|.S16|.S32|.S64]`, `CASTS[...]`** — новый `FormatIWithSize`, эмитит `EmitInstI`.
- `LDI reg, const|label` — константа (`0x...` hex) или метка.
- `LOAD[.size] reg, addr` / `LOAD_UNSAFE ...`, `STORE[.size] reg, addr` / `STORE_UNSAFE ...`.
- `LOAD_IND[.size] regDst, regAddr` / `STORE_IND[.size] regSrc, regAddr` (+unsafe).
- `JMP`/`JZ`/`JNZ`/`JG`/`JL`/`CALL` — метка или абсолютный адрес.

**Разложение по partial'ам:**
- `AssemblerParser.cs` — поля (`_asm`, `_allowDataSection`, `_pendingData`, `_inDataSection`, `_pendingLabel`, `_currentLine`, `_opMap`, `_regMap`), конструктор, `Assemble(...)` (3 перегрузки), `Build(...)`, `PendingData` (record), `[GeneratedRegex]`-метод `GetRexExtensionAsm()`.
- `AssemblerParser.Lexing.cs` — `ProcessLines`, `StripComments`, `ProcessSectionDirective`, `ProcessDataLine`, `ProcessCodeLine`, `GetSectionDirective`, `NormalizeSizeExtension`, `FlushPendingData`.
- `AssemblerParser.Instructions.cs` — `ParseInstruction`, `SplitMnemonicAndSize`, `DispatchInstruction`, `FormatR`/`FormatU`/`FormatIWithSize`/`FormatLDI`/`FormatJumpType`/`FormatLOAD_STORE`/`FormatLOAD_IND_STORE_IND`, `RequireOperands`.
- `AssemblerParser.DataSection.cs` — `TryProcessDataDirective`, `TryAddIntegerData`, `TryAddStringData`, `AddPendingData`.
- `AssemblerParser.Numbers.cs` — `ParseReg`, `TryParseSize`, `TryParseLong`, `ParseNumber`, `IsNumber`.

**Нормализация размера:** `NormalizeSizeExtension` чинит `LOAD . S8` -> `LOAD.S8` через `GetRexExtensionAsm().Replace(instruction, "$1.$2")` (AOT-safe, `[GeneratedRegex]` на `(\w)\s*\.\s*(\w)`). Есть fast-path: если нет `' . '` или пробела вокруг `.` — возвращает строку без аллокаций.

**Состояние парсера — instance-level:** `_asm`, `_allowDataSection`, `_pendingData`, `_inDataSection`, `_pendingLabel`, `_currentLine`, `_opMap`, `_regMap` мутируются в `Assemble`. **Не потокобезопасен** (см. п. 13). `_pendingData` накапливает `PendingData` и сбрасывается через `FlushPendingData()` в конце `Assemble` (или при переходе из `.data`/`.bss` обратно в `.text`).

**`AsmLanguageDefinition`** (`Nantuc.Compiler.ASM`) — `Mnemonics`/`Registers` (`FrozenSet`), regex'ы без `Compiled`. Использует `WordRegexBuilder`. `_regMapLex`/`_opMapLex` — обычные `Dictionary`, **плюс** их `FrozenDictionary`-копии `_regMap`/`_opMap` (см. п. 11).

**`IRAssembler`** (`Nantuc.Compiler.ASM.IR.Emitеer`) — `partial class` из 3 файлов (`IRAssembler.cs` + `.Layout.cs` + `.Emit.cs`), список `AsmItem`:
- `IRAssembler.cs`:
  - `EmitInstruction64(uint, ulong)` валидирует `HasNeed64IntData(op)`.
  - `EmitJump(uint, string)` -> `AsmInstruction` с `TargetLabel`.
  - `EmitJumpToAbsolute(uint, ulong)` -> `AsmInstruction(..., isAbsolute: true)`.
  - `EmitData(string, byte[])` -> padding-`AsmData(alignment: 8)` + `MarkLabel` + `AsmData(data, alignment: 1)`. Пустой `AsmData` с `alignment: 8` — разделитель/выравнивание.
  - `MarkLabel` — дубли через `HashSet<string>`; при коллизии — `Asm_DuplicateLabel` с позицией существующей метки (ищется линейно).
  - `Build()` — двухпроходный: `labelAddresses = ComputeLabelAddresses()`, потом `EmitItem` для каждого элемента; `stackalloc byte[7]` padding для выравнивания.
  - `GetItems()` — **внутренний** `List<AsmItem>`, мутации видны `Build()`.
- `IRAssembler.Layout.cs` — **проход 1**: `ComputeLabelAddresses()` — `labels`, `DataSectionStart`, `DataSectionSize`, `CodeSectionSize`. `AdvanceInstructionOffset(instr, offset)` — единая offset-логика для инструкций; `Align(offset, alignment)` — extension на `ulong`.
- `IRAssembler.Emit.cs` — **проход 2**: `EmitItem`, `EmitInstructionItem` (4 байта кода, `PadTo(8)`, 8 байт данных для `HasImmediate`/`IsJump`), `EmitDataItem` (`PadTo(alignment)`, bytes), `PadTo` (использует `padding[..pad]`), `ResolveJumpTarget` (`Asm_UndefinedLabel` если нет метки).

> **Важно:** `ComputeLabelAddresses` (Layout) и `EmitItem` (Emit) обязаны считать `offset` **идентично**. Обе реализации опираются на общий `Align(...)` (`offset.AlignUp(alignment)`).

**`InstructionEncoder`** (`Nantuc.Compiler.ASM.Utils`) — кодирование 32-битного слова:
- `Encode`/`EncodeR`/`EncodeI`/`EncodeU`/`EncodeJ` — форматы.
- Специализированные: `EncodeLDI`/`EncodeLOAD`/`EncodeSTORE`/`EncodeLOAD_IND`/`EncodeSTORE_IND`/`EncodeCALL`/`EncodeRET`/`EncodeHALT`/`EncodeWAKE`/`EncodeEND`/`EncodeNOP`.
- **`Decode(uint)`** — покрывает все опкоды, включая `LOAD_UNSAFE`/`STORE_UNSAFE`/`LOAD_IND_UNSAFE`/`STORE_IND_UNSAFE`/`CMP`/`TEST`/`WAKE_INT`/`CASTZ`/`CASTS` (`CASTZ.S8 r0`, `CASTS.S32 r1`).
- Extension-блоки: `OpCode.ToUint()`/`OpName()`, `RegType.ToUint()`/`RegName()`, `OpCodeSize.ToUint()`/`SizeName()`.
- Extension-блок на `BinaryWriter`: `WriteInstruction(uint)`, `Align8()`, `WriteInstructionWithData64(uint, ulong)`. **В `Assembler` не используется** (свои методы), но доступен для кастомных backend'ов.

**`AsmExtensions`** (`Nantuc.Compiler.ASM.Utils`) — extension-блок на `AssemblerBase`. Кодогенератор работает только через эти методы:
- R-формат: `EmitMov`, `EmitMult`, `EmitAdd`, `EmitSub`, `EmitMultInt`, `EmitDiv`, `EmitAnd`, `EmitOr`, `EmitXOr`, `EmitShl`, `EmitShr`, `EmitMod`, `EmitCmp`.
- U-формат: `EmitPush`, `EmitPop`, `EmitNot`, `EmitInc`, `EmitDec`.
- Специальные: `EmitLdi(ulong)`, `EmitLdi(string)`, `EmitLoadInd`, `EmitStoreInd`, `EmitLoad`, `EmitStore` (2 overload'а).
- Прыжки: `EmitJmp`, `EmitJz`, `EmitJnz`, `EmitJl`, `EmitJg`, `EmitCall`; `EmitInstJAbsolute`.
- Без операндов: `EmitEnd`, `EmitRet`, `EmitNop`, `EmitHalt`, `EmitWake`.
- Общие: `EmitInst`, `EmitInstU`, `EmitInstR`, **`EmitInstI`** (используется для `CASTZ`/`CASTS`), `EmitInstJ`, `EmitInstJAbsolute`, `EmitInstWithData`.

**`CodeGenUtils.EmitCastToType(reg, size, isSigned)`** (`Nantuc.Compiler.C.CodeGenerator`) — extension на `AssemblerBase`:
- `size == S64` -> no-op;
- `isSigned == true` -> `EmitInstI(OpCode.CASTS, reg, size)`;
- `isSigned == false` -> `EmitInstI(OpCode.CASTZ, reg, size)`.

### Оптимизации

**`AstOptimizer`** (`Nantuc.Compiler.C.Optimizations`) — `Add`/`Insert`/`Remove`/`RemoveAt`, `Count`, `GetType(i)`. `Optimize(program)` возвращает `IEnumerable<IReadOnlyLogOptimization>` — по одному на правило. Правил в этой партии нет.

**`IROptimizer`** (`Nantuc.Compiler.ASM.IR`) — `Optimize(items, maxPasses = 5)` — до неподвижной точки. Пропускает `AsmLabel`, применяет первое подходящее правило. `Clear()` на всех правилах в начале.

## Примеры для API

**Полный пайплайн:**

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

**Overflow узких типов:**

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
int main() { int a[3]; int x = (a[1] = 25) + 5; ... }     // пишет в a[1], не a[0]
```

**`const`:**

```c
const int LIMIT = 100;
int * const p = &a;              // p нельзя переприсвоить, *p можно
const int *q = &a;               // q можно, *q нельзя
const int arr[3] = { 1, 2, 3 };  // arr[i] нельзя

x = 6;              // ❌ CodeGen_AssignmentToConst (1316)
x++;                // ❌ CodeGen_IncrementOfConst  (1317)
(x = 6) + 1;        // ❌ тоже — через GenerateAssignmentExpr
arr[0] = 10;        // ❌
*p = 10;            // ❌ (если p = const int*)
p = &b;             // ❌ (если p = int* const)
```

**Прямой ассемблер + NANASM:**

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
        CASTS.S32 r1            ; знаковое расширение
        END
    """, baseAddress: 0x1000, ErrorReporter);
```

**Дизассемблирование:**

```csharp
InstructionEncoder.Decode(raw);   // "ADD r1, r2" / "CASTS.S32 r0" / "UNKNOWN" больше нет
```

## Приближенность модуля к пользователю

Средняя. Пользователь MiniC работает через `VmApplication.Compile(source)`, автор языка/расширений — здесь.

- Автор нового синтаксиса — `Lexer`/`Parser` (partials в `Parser/*.cs`)/`ASTNode` (`C/AST/`).
- Автор оптимизаций — `IAstOptimizationRule`/`IOptimizerIRRule`.
- Автор нового backend'а — `AssemblerBase`.
- Автор нового target'а ISA — `InstructionEncoder` + `AsmExtensions`.
- Автор NANASM-скриптов — `AssemblerParser` с секциями `.data`/`.bss`/`.text`/`.code` (partials в `ASM/Parser/`).

**Что стоит делать:**
- `IRAssembler` для production-пайплайна; `Assembler` — для тестов.
- Пользовательские оптимизации — через `Add` в `AstOptimizer`/`IROptimizer`; правила идемпотентны (`Clear()` на входе).
- Для inline-asm — `AsmLanguageDefinition.Mnemonics`/`Registers` как источник истины.
- **`const`** — использовать как гарантию неизменяемости в API и внутренних данных.
- **Overflow** — писать в узкие типы (byte/char/short/int/uint/ushort) как в обычном C; касты вставляет кодогенератор.
- `InstructionEncoder.Decode(uint)` — для отладки.
- **Добавление нового выражения** — новый partial `ExpressionGenerator.Xxx.cs` в `CodeGenerator/Expression/`; **нового оператора** — новый partial `StatementGenerator.Xxx.cs` в `CodeGenerator/Statement/`. API-методы не меняются, `Parser`/`StatementGenerator.cs` только диспетчеризуют.
- **Добавление новой директивы данных** — в `AssemblerParser.DataSection.cs` (один case в `TryProcessDataDirective`); **нового формата операндов** — в `AssemblerParser.Instructions.cs` (`Format*` + case в `DispatchInstruction`).

**Что не стоит делать:**
- Полагаться на `FunctionNode.Body` у `extern` — там `null!`.
- Полагаться на `RegisterAllocator` для >22 одновременно живых скаляров.
- Использовать `MiniCLanguageDefinition.GetKeywordRegex()` — dead code (заменён на `WordRegexBuilder`).
- `.data` в inline-asm — запрещено.
- `const` на полях `struct` — не поддержано.
- Один `AssemblerParser` на несколько потоков — не потокобезопасен.
- **Полагаться на compile-time свёртку `const`** — его нет; `const int N = 10; int arr[N];` — ошибка парсинга.
- **Добавлять `AsmData` с `alignment > 8`** — сломает `Padding`-буфер в `IRAssembler.Build()` (см. п. 21).

## Особенная информация

### Каталог AST-узлов

Все узлы в **`Nantuc.Compiler.C.AST`**, файлы в `C/AST/`. Базовый `ASTNode` — только `NameNode`. `Parser.Parse()` возвращает `ProgramNode`.

**Корневые / декларации**

| Узел | Что содержит | Где создаётся |
|---|---|---|
| `ProgramNode` | `GlobalVarNodes`, `FunctionNodes`, `StructNodes`, `IncludesList` | `Parser.TopLevel.cs` -> `Parse()` |
| `StructDeclNode` | `Name`, `Fields: List<(type, name)>` | `Parser.TopLevel.cs`, ветка `struct Name { ... }` |
| `FunctionNode` | `Name`, `ReturnType`, `Parameters`, `Body`, `IsExternal`, `LocalSize`, **`ReturnIsPointer`** | `Parser.Declarations.cs` -> `ParseFunction`, `Parser.TopLevel.cs` -> `extern`-ветка |
| `ParameterNode` | `Type`, `Name`, `IsPointer`, `PointedType`, `IsConst`, `IsPointedConst` | `Parser.Declarations.cs` -> `ParseFunction` |
| `VariableNode` | `Type`, `Name`, `Initializer`, `ArrayInitializer`, `IsArray`, `ArraySize`, `IsPointer`, `PointedType`, `StructTypeName`, `IsConst`, `IsPointedConst` | `Parser.Declarations.cs` -> `ParseVariableTail` |

**Операторы / statements**

| Узел | Что содержит | Где создаётся |
|---|---|---|
| `BlockNode` | `Statements` | `Parser.Statements.cs` -> `ParseBlock`, `ParseBody` |
| `IfNode` | `Condition`, `ThenBlock`, `ElseBlock?` | `Parser.Statements.cs` -> `ParseIf` |
| `WhileNode` | `Condition`, `Body` | `Parser.Statements.cs` -> `ParseWhile` |
| `DoWhileNode` | `Body`, `Condition` | `Parser.Statements.cs` -> `ParseDoWhile` |
| `ForNode` | `Init`, `Condition`, `Increment`, `Body` | `Parser.Statements.cs` -> `ParseFor` |
| `ReturnNode` | `Value?` | `Parser.Statements.cs` -> `ParseReturn` |
| `BreakNode` / `ContinueNode` | — | `Parser.Statements.cs` -> `ParseBreak` / `ParseContinue` |
| `InlineAsmNode` | `AsmCode: string` | `Parser.Statements.cs` -> `ParseInlineAsm` |

**Выражения**

| Узел | Что содержит | Где создаётся |
|---|---|---|
| `NumberNode` | `Value: long` | `Parser.Expressions.cs` -> `ParseNumberAtom` (из `ParsePrimary`) |
| `IdentifierNode` | `Name` | `Parser.Expressions.cs` -> `ParseIdentifierAtom` |
| `AssignmentNode` | `Name`, `IndexExpr?`, `LValue`, `CompoundOp?`, `Value` | `Parser.Expressions.cs` -> `ParseAssignment` |
| `BinaryOpNode` | `Operator`, `Left`, `Right` | `Parser.Expressions.cs` -> `ParseLogicalOr`/`And`, `ParseBitwiseOr`/`Xor`/`And`, `ParseEquality`, `ParseRelational`, `ParseShift`, `ParseAdditive`, `ParseMultiplicative`, `ParseComma` |
| `UnaryOpNode` | `Operator`, `Operand` | `Parser.Expressions.cs` -> `ParseUnary` |
| `IncrementNode` | `Target`, `Delta`, `IsPrefix` | `Parser.Expressions.cs` -> `ParseUnary` (префикс), `ParsePrimary` (постфикс) |
| `FunctionCallNode` | `Name`, `Arguments` | `Parser.Expressions.cs` -> `ParseIdentifierAtom` |
| `IndirectCallNode` | `Callee`, `Arguments` | `Parser.Expressions.cs` -> `ParsePostfixIndirectCall` |
| `ArrayAccessNode` | `ArrayName`, `Index` | `Parser.Expressions.cs` -> `ParseIdentifierAtom` |
| `AddressOfNode` / `DereferenceNode` | `Operand` | `Parser.Expressions.cs` -> `ParseUnary` |
| `MemberAccessNode` | `Object`, `FieldName`, `IsArrow` | `Parser.Expressions.cs` -> `ParsePostfixMemberAndIncrement` |
| `SizeofTypeNode` / `SizeofExprNode` | `TypeName`/`IsPointer`, `Operand` | `Parser.Expressions.cs` -> `ParseUnary` |

Итого 28 наследников. **Кто ходит по AST в кодогенераторе:** `ExpressionGenerator` (все partial'ы), `StatementGenerator` (все partial'ы + `StatementTypeResolver`), `SaveDepthEstimator`, `RegisterAllocator` (`VariableAccessAnalyzer.CollectVarEvents`, `CollectAddressTakenVars`), `LocalVarCollector.Collect`, `ConstantEvaluator`. При добавлении узла — обновите все эти места.

### Переполнение узких целых типов

Кодогенератор эмитит `CASTZ`/`CASTS` (опкоды 43/44) после записи в переменную/регистр/поле/элемент массива/return — чтобы значение в регистре всегда было в канонической форме своего типа.

**Канонические формы:**

| Тип | `OpCodeSize` | Signed | Диапазон |
|---|---|---|---|
| `byte` | S8 | нет | `0..255` (верхние 56 бит — нули) |
| `char` | **S16** | да | `−32768..32767`, sign-extended |
| `short` | S16 | да | `−32768..32767` |
| `ushort` | S16 | нет | `0..65535` |
| `int` | S32 | да | `−2³¹..2³¹−1`, sign-extended |
| `uint` | S32 | нет | `0..2³²−1` |
| `long` | S64 | да | как есть |
| `ulong` | S64 | нет | как есть |

> **`char` в MiniC — 16-битный знаковый (`S16`)**, не 8-битный как в стандартном C. Следствия: `sizeof(char) == 2`; `char c = 120; c += 100;` -> `220` (влезает в int16); `char c = 32767; c += 1;` -> `−32768` (обёртка).

**Где вставляются касты (`Nantuc.Compiler.C.CodeGenerator`, по partial'ам):**

| Событие | Каст | Partial |
|---|---|---|
| `LOAD[.size]` из памяти | `LOAD` zero-extends -> **`CASTS.S<size>`** для signed-типов | `VariableAccessor` |
| `STORE[.size]` в память | `STORE` сам усекает — каст не нужен | `VariableAccessor` |
| `x = value`, `x op= value`, `x++` (register) | после операции — `CASTZ`/`CASTS` в home-регистре | `VariableAccessor`, `Assignment.cs`, `Increment.cs`, `CompoundAssignments.cs` |
| `(x = value)`, `(x op= value)`, `x++` в выражении | после операции — `CASTZ`/`CASTS` в `targetReg` | `Assignment.cs`, `Increment.cs` |
| `arr[i] = value`, `*p = value`, `obj.f = value` | после load и после операции — `CASTZ`/`CASTS` | `Arrays.cs`, `MemberAccess.cs`, `CompoundAssignments.cs` |
| `arr[i]++`, `(*p)++`, `(obj.f)++` | после load и после `INC`/`DEC` — `CASTZ`/`CASTS` | `Increment.cs` |
| Call-site (аргумент -> `r0..r3`) | `CASTZ`/`CASTS` к типу параметра | `Calls.cs`, `FunctionGenerator` |
| Prologue (параметр 4+ из pseudo-global) | после `LOAD` — `CASTZ`/`CASTS` | `FunctionGenerator` |
| `RETURN` (значение в `r0`) | перед `JMP epilogue` / `END` — `CASTZ`/`CASTS` к типу возврата (non-pointer scalar) | `ControlFlow.cs` (`GenerateReturn`) |

**Что НЕ кастуется:**
- Промежуточный результат арифметики — C-семантика: `byte + byte` даёт `int`; усечение только на следующей записи.
- `long`/`ulong` — `S64`, `EmitCastToType` no-op.
- Указатели и структуры.

**Реализация:**
- `CodeGenUtils.IsSignedType(type)` — `char/short/int/long` -> `true`.
- `CodeGenUtils.EmitCastToType(reg, size, isSigned)` — extension на `AssemblerBase`.
- `VarLocation.IsSigned` / `GlobalInfo.IsSigned` — вычисляются в `VariableLayoutBuilder.Build` / `GlobalMemoryManager.Allocate`.
- `VariableAccessor.LoadToRegister` — `CASTS` после `LOAD`; `StoreFromRegister` — каст home-регистра.

### Константность (`const`) — семантика и ограничения

`const` в MiniC — **compile-time гарантия неизменяемости** (не .rodata, не constant folding). Значение хранится обычным образом; запрет на запись диагностируется на этапе кодогенерации.

**Поддерживаемые формы**

| Декларация | Что запрещено | Поля `VariableNode` |
|---|---|---|
| `const int x` | `x = ...; x++; x += ...;` | `IsConst = true` |
| `int * const p` | `p = ...; p++; p += ...;` (можно `*p = ...`) | `IsConst = true`, `IsPointedConst = false` |
| `const int *p` | `*p = ...; (*p)++; p[i] = ...` (можно `p = ...`) | `IsConst = false`, `IsPointedConst = true` |
| `const int * const p` | и то, и другое | оба `true` |
| `const int arr[N]` | `arr[i] = ...; arr[i]++` | `IsPointedConst = true`, `IsConst = false` |
| `void f(const int x)` | `x = ...` | `ParameterNode.IsConst = true` |
| `void f(const int *p)` | `*p = ...` | `ParameterNode.IsPointedConst = true` |

**Синтаксис:** `Parser.Declarations.cs` -> `ParseDeclSpec` -> `(type, isPointer, pointedType, prefixConst, ptrConst)`; `[const] type`, `type const`, `*`, `[* const]`.

**Проверки в кодогенераторе:**
- `ExpressionGenerator.CheckConstTarget(assign)` — **internal, единая точка** (в `ExpressionGenerator.Assignment.cs`); вызывается из `GenerateAssignmentExpr` (expression-context) и из `StatementGenerator.GenerateAssignment` (`StatementGenerator.Assignments.cs`, statement-context).
  - `assign.IndexExpr != null` -> `IsPointedConst(name)`;
  - `assign.LValue is DereferenceNode` -> `IsPointedConst(ptrName)`;
  - `assign.LValue is MemberAccessNode` -> пока пропуск (TODO, п. 1);
  - простое имя -> `IsConst(name)`.
- `ExpressionGenerator.GenerateIncrement` (`ExpressionGenerator.Increment.cs`) — `IsConst`/`IsPointedConst` для `Identifier`/`ArrayAccess`/`Dereference`.

**Новые `ErrorCode`:**
```csharp
CodeGen_AssignmentToConst = 1316,   // "Cannot assign to const '{0}'"
CodeGen_IncrementOfConst  = 1317,   // "Cannot increment/decrement const '{0}'"
```

**Не поддерживается:**
1. `const` на полях `struct` — `CheckConstTarget` для `MemberAccessNode` делает `return` (TODO).
2. `const` на call-site — `const int*` -> `int*` не диагностируется.
3. Constant folding — `const int N; int arr[N];` — ошибка парсинга.
4. `const` в `extern`-объявлениях.
5. `&x` от `const x` даёт обычный `int*`.

**С регистрами:** `RegisterAllocator` не исключает `const` из кандидатов — запись блокируется на уровне кодогенерации, до инструкции `Store`/`Inc`/`Dec` не доходит. `&x` — `CollectAddressTakenVars` гарантирует, что такие переменные живут в памяти.

### WASM / кроссплатформенность

| Аспект | Статус | Комментарий |
|---|---|---|
| Сборка в WASM | ✅ | Нет `unsafe`, `DllImport`, `LibraryImport`. `stackalloc` только в `Assembler.Align8` и `IRAssembler.Build` (0–7 байт). |
| `NativeMemory` | ✅ | Не используется. |
| `stackalloc` / `InlineArray` | ✅ | `stackalloc byte[pad]` (0..7 байт) в `Assembler.Align8`; `stackalloc byte[7]` в `IRAssembler.Build`. |
| `Interlocked` / `Volatile` | ✅ | Не используются. |
| `System.Collections.Frozen` | ✅ | `FrozenSet` + **`FrozenDictionary`** (`_regMap`/`_opMap`). |
| `[GeneratedRegex]` | ✅ | `AssemblerParser.GetRexExtensionAsm()` — единственный regex в горячем пути. |
| `Regex` без `Compiled` | ✅ | `AsmLanguageDefinition._mnemonicsRegex`/`regex`, `WordRegexBuilder.Build` — без `Compiled`. |
| `Enum.ToString()` | ⚠️ | **Убран из диагностики** — заменён на `TokenTypeExtension.AsText` / `OpCode.OpName()`. |
| `DiagnosticData` | ✅ | Готовая строка при создании, без боксинга и рефлексии. |
| `AsmInstruction(uint, ulong, bool)` | ⚠️ | `TargetLabel = absoluteTarget.ToString()` — fallback, мёртвая ветка (п. 7). |
| Trimming | ⚠️ | `using System.Security.Cryptography;` в одном из файлов `C/AST/` — не используется, но sloppy. |
| AOT | ⚠️ | Основной риск — `RegexOptions.Compiled`. |
| `switch` по `OpCode`/`TokenType` | ✅ | Jump-table, AOT-friendly. |

**Итог:** собирается под WASM и AOT, если убрать `RegexOptions.Compiled`.

### Пайплайн компиляции

```
source (MiniC)
  │
  ▼
Lexer.Tokenize() -> List<Token>                  // <<=, >>= — 3-символьные
  │
  ▼
Parser.Parse() -> ProgramNode                     // C.AST; 28 узлов
  │  (ParseDeclSpec: [const] Type [* [const]]; ParseFunction: isReturnPointer)
  │  (partials: Parser.cs / .TopLevel.cs / .Declarations.cs /
  │             .Expressions.cs / .Statements.cs / .Literals.cs)
  │
  ├── StructLayout.Resolve(program.StructNodes)
  │
  ▼
FunctionGenerator.Generate(program)               // C.CodeGenerator.FunctionGen
  ├── GlobalMemoryManager.Allocate(...) — IsSigned вычисляется
  ├── GenerateGlobalInit (только скаляры)
  ├── EmitJmp("func_main")
  ├── foreach func in FunctionNodes:
  │     ├── LocalVarCollector.Collect(func.Body)
  │     ├── FunctionContext.Create(...) -> VarMap + IsSigned + ReturnSize/ReturnIsSigned
  │     ├── RegisterAllocator.AllocateRegisters(...) — &var -> стек
  │     ├── пролог: push savedRegs, sub rSP, stackAlloc, params -> registers
  │     ├── StatementGenerator.GenerateBlock(func.Body)
  │     │   (partials: StatementGenerator.cs / .Assignments.cs /
  │     │             .CompoundAssignments.cs / .ControlFlow.cs /
  │     │             .Declarations.cs / .InlineAsm.cs)
  │     └── эпилог: add rSP, pop savedRegs, ret / END для main
  └── EmitData для глобалов и pseudo-globals
  │
  ▼
AssemblerBase (Assembler | IRAssembler)
  │  Assembles при Build():
  │  - IRAssembler: проход 1 (ComputeLabelAddresses) -> проход 2 (EmitItem)
  │  - Assembler:   прямая запись + патч _patches
  │  - DataSectionStart / DataSectionSize / CodeSectionSize
  │
  ▼
byte[] (32-битные слова ISA + 8-выровненные данные)
```

### Формат инструкции и `InstructionEncoder`

32-битное слово ISA:

```
Bits  0-7   : OpCode         (8 бит, до 256 опкодов)
Bits  8-12  : Reg1           (5 бит, целевой/основной регистр)
Bits 13-17  : Reg2           (5 бит, второй регистр-источник)
Bits 18-19  : OpCodeSize     (2 бита: 0=S8, 1=S16, 2=S32, 3=S64)
Bits 20-31  : не используется
```

**Форматы:**

| Формат | Метод | Примеры | Операнды |
|---|---|---|---|
| R | `EncodeR(op, regDst, regSrc)` | `MOV`, `ADD`, `SUB`, `AND`, `OR`, `XOR`, `MUL`, `DIV`, `MOD`, `SHL`, `SHR`, `CMP`, `TEST`, `IN`, `OUT` | 2 регистра |
| R+size | `Encode(op, regDst, regSrc, size)` | `LOAD_IND`, `STORE_IND` (+unsafe) | 2 регистра + размер |
| I | `EncodeI(op, reg, size)` | `LDI`, `LOAD`, `STORE` (+unsafe), **`CASTZ`, `CASTS`** | 1 регистр + размер (+64-битный immediate после выравнивания для LDI/LOAD/STORE) |
| U | `EncodeU(op, reg)` | `INC`, `DEC`, `NOT`, `PUSH`, `POP`, `CALL_IND`, `JMP_IND`, `WAKE_INT` | 1 регистр |
| J | `EncodeJ(op)` | `JMP`, `JZ`, `JNZ`, `JG`, `JL`, `CALL` | 64-битный адрес после выравнивания |
| — | `EncodeEND/NOP/RET/HALT/WAKE` | — | без операндов |

**64-битные операнды:** после инструкции — `Align8()`, затем 8 байт little-endian.

**`Decode(uint)`** — обратное разложение (для отладки):
```csharp
InstructionEncoder.Decode(raw);   // "ADD r1, r2" / "LOAD.S32 r0, [data64]" / "CASTS.S16 r5"
```

### Ассемблер NANASM: секции данных

**Директивы секций**

| Директива | Действие |
|---|---|
| `.data`, `.bss`, `.section .data`, `.section .bss` | открыть секцию данных |
| `.text`, `.code`, `.section .text`, `.section .code` | закрыть секцию данных |

`.bss` семантически совпадает с `.data`.

**Директивы данных** (только внутри `.data`/`.bss`)

| Директива | Алиас | Ширина | Пример | Результат |
|---|---|---|---|---|
| `.byte` | `db` | 1 | `.byte 1, 2, 3` | `[01 02 03]` |
| `.byte` | — | 1 | `.byte "abc"` | `[61 62 63]` |
| `.word` | `dw` | 2 | `.word 0x1234` | `[34 12]` |
| `.dword` | `dd` | 4 | `.dword 0xDEADBEEF` | `[EF BE DE DE]` |
| `.quad` | `dq` | 8 | `.quad 0x...` | 8 байт little-endian |
| `.ascii` | — | 1 | `.ascii "Hi"` | `[48 69]` |
| `.asciz` | `.string` | 1 (+NUL) | `.asciz "Hi"` | `[48 69 00]` |
| `.zero` | `.space` | N | `.zero 256` | 256 нулевых байт |

**Ограничения**

| Ограничение | Причина | Что делать |
|---|---|---|
| `.data` запрещён в inline-asm | `allowDataSection: false` | Вынесите `.data` в NANASM-файл |
| `.quad <label>` не поддержан | Нет патча меток в `EmitData` | `.quad 0` + инициализация в `__global_init` |
| `.align N` не поддержан | `EmitData` всегда выравнивает на 8 | Полагайтесь на авто-8 |
| `;` в строке обрезается | `Split(['\n', ';'])` в `ProcessLines` | Не используйте `;` в `.ascii`/`.asciz` |
| Только little-endian | Платформа Nantuc | — |

### Диагностика: `DiagnosticData` и AOT

Аргументы диагностики — `ReadOnlySpan<DiagnosticData>` (не `object[]`):

```csharp
public readonly record struct DiagnosticData
{
    public string Text { get; }
}
```

Что это даёт для AOT:
- Нет боксинга `int`/`char`/enum.
- Нет виртуального `Object.ToString()` — строка уже готова.
- `Enum.ToString()` в диагностике убран (`TokenTypeExtension.AsText` / `OpCode.OpName()`).
- `Type.Name` в диагностику не передаётся.

**Итог:** `DiagnosticData` полностью AOT-safe.

### `Assembler` vs `IRAssembler`

| | `Assembler` | `IRAssembler` |
|---|---|---|
| Хранение | `MemoryStream` + `BinaryWriter` | `List<AsmItem>` |
| Патч меток | `_patches` в `Build()` | `labelAddresses` в первом проходе (`ComputeLabelAddresses`) |
| IR | ❌ | ✅ |
| Проверка `HasNeed64IntData` | ❌ (только при `EmitInstruction64`, `EmitInstruction64WithLabel` не проверяет — п. 12) | ✅ при `EmitInstruction64` (тоже не при `EmitInstruction64WithLabel`) |
| Валидация дублей меток | ✅ | ✅ |
| Двухпроходный Build | ❌ | ✅ (`Layout` -> `Emit`) |
| `.data` через `AssemblerParser` | ✅ | ✅ |
| `EmitData` | `Align8()` + `MarkLabel` + bytes | padding-`AsmData(alignment: 8)` + `MarkLabel` + `AsmData(data, alignment: 1)` |
| Padding-буфер | `stackalloc byte[pad]` (0..7) per call | `stackalloc byte[7]` один на `Build()` (лимит alignment ≤ 8, п. 21) |
| `GetItems()` мутируемый | — | ✅ (внутренний `List<AsmItem>`) |

## Что исправить

По убыванию важности. Нумерация сохранена (пропуски 3/5/6 — историческая).

### `const` — неполнота семантики

1. **[CONST] `CheckConstTarget` для `MemberAccessNode` — TODO.**
   Файл: `ExpressionGenerator.Assignment.cs` (`CheckConstTarget`): `if (assign.LValue is MemberAccessNode) return;   // TODO`.
   **Следствие:** `struct Point p; p.x = 5` не проверяется, даже если поле объявлено как `const int x` (что пока невозможно — `const` на полях структуры тоже не поддержан).
   **Фикс:** расширить `FieldInfo` полями `IsConst`/`IsPointedConst`, пробросить через `StructLayout`, проверять в `CheckConstTarget`.

2. **[CONST] `const`-квалификатор в `extern`-объявлениях — не парсится.**
   Файл: `Parser.TopLevel.cs`, блок `extern`. Пропускает `*`, но не `const`.
   **Фикс:** симметрично `ParseDeclSpec` (из `Parser.Declarations.cs`).

### API / Дефекты

4. **[API] `StatementGenerator.GenerateAssignment` — case 4 (direct assign) — dead code.**
   Файл: `StatementGenerator.Assignments.cs`.
   Условие `assign.LValue == null` никогда не выполняется: `Parser.ParseAssignment` для `IdentifierNode` создаёт `new AssignmentNode(...) { LValue = left, ... }` — `LValue` всегда установлен.
   **Фикс:** заменить на `assign.LValue is IdentifierNode` или `assign.Name != null && assign.IndexExpr == null && assign.CompoundOp == null`.

7. **[API] `AsmInstruction(uint, ulong, bool)` — fallback через `ToString()`.**
   Файл: `ASM/IR/AsmInstruction.cs`.
   `TargetLabel = absoluteTarget.ToString()` — мёртвая ветка (`IRAssembler.EmitJumpToAbsolute` всегда `isAbsolute: true`).
   **Фикс:** убрать `isAbsolute` из конструктора, разделить по типу аргумента.

8. **[API] `ExpressionGenerator.GenerateBinaryOp` — `&&`/`||` в `targetReg` через `Mov`.**
   Файл: `ExpressionGenerator.Binary.cs`.
   **Фикс:** `GenerateLogicalAndOr(binop, targetReg)`.

9. **[API] `AssemblerParser.TryAddIntegerData` — `.quad <label>` пока не поддержан.**
   Файл: `ASM/Parser/AssemblerParser.DataSection.cs`.
   **Фикс:** механизм патча по метке в `EmitData`/`Build()`.

10. **[API] `AssemblerParser` — `;` внутри строки обрезается.**
    Файл: `ASM/Parser/AssemblerParser.Lexing.cs` (`ProcessLines`: `code.Split(['\n', ';'])`).
    **Фикс:** вынести разбор строк из общего `Split` (разбивать по `\n`, а `;` обрабатывать только вне литералов) или добавить hex-escape.

11. **[API] `AssemblerParser` — двойные словари `_regMapLex`/`_opMapLex` + `_regMap`/`_opMap`.**
    Файл: `AsmLanguageDefinition.cs`.
    `_regMapLex`/`_opMapLex` — dead после статического конструктора.
    **Фикс:** инициализировать `FrozenDictionary` напрямую.

12. **[API] `IRAssembler.EmitInstruction64WithLabel` не проверяет `HasNeed64IntData`.**
    Файл: `ASM/IR/Emitеer/IRAssembler.cs`.
    **Фикс:** вынести проверку в общий хелпер `Validate64BitOp(op)`.

13. **[API] `AssemblerParser` не потокобезопасен.**
    Файл: `ASM/Parser/AssemblerParser.cs`. Поля instance-level (`_asm`, `_allowDataSection`, `_pendingData`, `_inDataSection`, `_pendingLabel`, `_currentLine`, `_opMap`, `_regMap`). `StatementGenerator.GenerateInlineAsm` (`StatementGenerator.InlineAsm.cs`) создаёт `new AssemblerParser()` на каждый `asm` — но контракт не задокументирован.
    **Фикс:** `[ThreadStatic]`/`lock`, либо явно задокументировать.

14. **[API] `RegisterAllocator.BuildInterferenceGraph` — линейные live-диапазоны, не учитывают циклы.**
    Файл: `RegisterAllocator.Interference.cs` + `VariableAccessAnalyzer.CollectVarEvents`.
    **Фикс:** задокументировать как «best effort» или реализовать реальный live-range analysis.

15. **[API] `FunctionGenerator.GenerateGlobalInit` — инициализация структур не поддерживается молча.**
    Файл: `FunctionGenerator.cs`.
    `if (CodeGenUtils.IsStructType(global.Type, _structTable)) continue;` — глобальная структура с `Initializer` игнорируется.
    **Фикс:** поддержать или бросать `NotSupported`.

### Дом

16. **[Дом] AST разложен по файлам `C/AST/`, но без «карты» в шапке папки.**
    `CodeGenerator` уже разложен по `Expression/`/`Statement/`/`FunctionCtx/`/`FunctionGen/`/`RegistersAlloc/`, а AST — нет.
    **Фикс:** добавить `C/AST/README.md` или `#region`-карту.

### XML

17. **[XML] AST-иерархия — без `///`.**
    Включая `VariableNode.IsConst`/`IsPointedConst`, `ParameterNode.IsConst`/`IsPointedConst`, `FunctionNode.ReturnIsPointer`.

18. **[XML] `Parser` (все 6 partial'ов) и `ExpressionGenerator`/`StatementGenerator` (все partial'ы) — без `///`.**
    Особенно `CheckConstTarget` (`ExpressionGenerator.Assignment.cs`), `GenerateIncrement` (`ExpressionGenerator.Increment.cs`), `GenerateAssignmentExpr` (`ExpressionGenerator.Assignment.cs`). Split на partial'ы сделан именно для читаемости — логичное место добавить `///` в шапку каждого partial'а.

19. **[XML] `CodeGenUtils` — без `///`.**
    `IsSignedType`, `EmitCastToType`, `GetAlignment(type, structTable)` (жёсткое `8` для структур).

20. **[XML] `AssemblerParser` — шапка класса не перечисляет `.data`, `allowDataSection`, `CASTZ`/`CASTS`.**
    Файл: `ASM/Parser/AssemblerParser.cs`. Шапка класса упоминает только базовые форматы; partial'ы `.Lexing.cs`/`.Instructions.cs`/`.DataSection.cs`/`.Numbers.cs` вообще без `///`.

### Новые (выявлены при разбиении на partial'ы)

21. **[API] `IRAssembler` — padding-буфер фиксированного размера 7 байт.**
    Файл: `ASM/IR/Emitеer/IRAssembler.cs` (`Build()`: `Span<byte> padding = stackalloc byte[7]`), `ASM/IR/Emitеer/IRAssembler.Emit.cs` (`PadTo` использует `padding[..pad]`).
    При добавлении `AsmData` с `alignment > 8` (или другого `AdvanceInstructionOffset` с выравниванием > 8) — `IndexOutOfRangeException`. Сейчас все вызовы `EmitData` используют 1 или 8, а инструкции — 8, так что лимит соблюдается неявно.
    **Фикс:** либо увеличить буфер до `maxAlignment` (например, 32), либо сделать `maxAlignment` константой и явно её документировать; либо генерировать padding динамически.

22. **[API] `AssemblerParser.FormatLOAD_STORE` — не поддерживает метки (только числовой адрес).**
    Файл: `ASM/Parser/AssemblerParser.Instructions.cs`.
    ```csharp
    ulong addr = ParseNumber(tokens[2]);   // "msg" -> FormatException
    ```
    Пример из README (`LOAD.S8 r1, msg`) сейчас не работает: `msg` не парсится как число. Симметрично `FormatLDI`/`FormatJumpType` уже поддерживают метки.
    **Фикс:** добавить `IsNumber`-ветку + `EmitInstWithLabel`/`EmitInstruction64WithLabel` (или отдельный метод), либо задокументировать ограничение и поправить пример.

23. **[API] `AssemblerParser.TryAddStringData` — не поддерживает `\xNN` escape.**
    Файл: `ASM/Parser/AssemblerParser.DataSection.cs`. Неизвестные escape'ы обрабатываются как `(byte)inner[i]` (silently) — в отличие от `Parser.Literals.cs` (`Parser_InvalidCharLiteral`). Расхождение между C-строкой и NANASM-строкой.
    **Фикс:** унифицировать escape-обработку; для симметрии с `Parser.Literals` поддержать `\xNN`.

24. **[API] `AssemblerParser.ParseNumber` бросает на невалидном вводе, а `IsNumber` возвращает `false` — рассинхронизация контракта.**
    Файл: `ASM/Parser/AssemblerParser.Numbers.cs`.
    `FormatLDI`/`FormatJumpType` проверяют `IsNumber` до `ParseNumber`, но `FormatLOAD_STORE` — нет. Ошибки парсинга адреса приходят как `FormatException`/`OverflowException` из `ulong.Parse`/`Convert.ToUInt64`, а не как `Asm_InvalidInstruction`.
    **Фикс:** `TryParseNumber(string, out ulong)` + ветка в `FormatLOAD_STORE` (см. п. 22).

25. **[API] `IRAssembler.ResolveJumpTarget` — `ErrorCode.Asm_UndefinedLabel` с позицией 0.**
    Файл: `ASM/IR/Emitеer/IRAssembler.Emit.cs`.
    ```csharp
    ErrorReporter.Throw(ErrorCode.Asm_UndefinedLabel, instr.TargetLabel!, 0);
    ```
    Второй аргумент — не позиция токена, а хардкод `0`; диагностика теряет контекст.
    **Фикс:** либо передавать реальную позицию (если есть), либо сделать параметр опциональным (`long? position = null`).
