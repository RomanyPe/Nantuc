# Nantuc.Compiler — README

> Компилятор MiniC -> байткод Nantuc: лексер, парсер, AST, кодогенератор с регистровой раскраской, ассемблер (два backend'а — прямой и IR) и инфраструктура оптимизаций.
> `[← Корень](../README.ru.md) · [Common](../../src/Nantuc.Common/README.ru.md) · [Contracts](../../src/Nantuc.Contracts/README.ru.md) · [Kernel](../../src/Nantuc.Kernel/README.ru.md)`

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
- Frontend: `Lexer`, `Parser` (partial, 6 файлов), **`Nantuc.Compiler.C.AST`** — 28 узлов AST в отдельных файлах, `MiniCLanguageDefinition`, **`TokenTypeExtension`**.
- Кодогенератор: `ExpressionGenerator` (partial, 12 файлов), `StatementGenerator` (partial, 6 файлов + `StatementTypeResolver`), `FunctionGenerator`, `RegisterAllocator` (жадная раскраска интерференционного графа), `FunctionContext`, `VariableAccessor`, **overflow-касты через `CASTZ`/`CASTS`**.
- Ассемблер: `AssemblerBase` (абстракция), `Assembler` (прямой), `IRAssembler` (IR), `AssemblerParser`, `InstructionEncoder`, `AsmExtensions`, `AsmLanguageDefinition`.
  **`AssemblerParser` поддерживает секции `.data`/`.bss`/`.text`/`.code` и директивы данных** (`.byte`, `.word`, `.dword`, `.quad`, `.ascii`, `.asciz`, `.zero`).
- Оптимизации: `AstOptimizer`, `IROptimizer` — каркас с интерфейсами `IAstOptimizationRule`/`IOptimizerIRRule`; **конкретных правил в этой партии нет**.
- Раскладка структур: `StructLayout`, `FieldInfo` — выравнивание полей по 8 байт, `Resolve` с итеративным разрешением зависимостей.

> **XML-документация фрагментарна.** Полностью документированы `AssemblerParser` (шапка класса — но не синхронизирована с новыми опкодами, см. п. 20), `Assembler` (частично), `IOptimizerIRRule`, `IAstOptimizationRule`, `InstructionEncoder` (формат + регионы). Без `///` — вся AST-иерархия (28 типов в `C/AST/`, только inline `//`-комментарии в шапке файла), `Lexer`, **`Parser` и все его partial-файлы** (`Parser.Declarations.cs`/`.Expressions.cs`/`.Literals.cs`/`.Statements.cs`/`.TopLevel.cs`), **`ExpressionGenerator` и все его partial-файлы** (11 partial'ов в `CodeGenerator/Expression/`), **`StatementGenerator` и все его partial-файлы** (5 partial'ов + `StatementTypeResolver.cs` в `CodeGenerator/Statement/`), `FunctionGenerator`, `RegisterAllocator`, `CodeGenUtils`, `GlobalMemoryManager`, `VariableAccessor`, `IRAssembler`, `IROptimizer`, `AsmExtensions`. Подробности и дефекты — в разделе [Что исправить](#что-исправить).

## Дерево локального проекта

```
Nantuc.Compiler/
├── Nantuc.Compiler.csproj            # net10.0, refs Common+Contracts+Diagnostics
├── README.md
├── README.ru.md
│
├── ASM/                              # Backend (namespace Nantuc.Compiler.ASM)
│   ├── AsmLanguageDefinition.cs      # Mnemonics/Registers (FrozenSet); regex'ы без Compiled
│   ├── Assembler.cs                  # прямая запись в MemoryStream + патч меток
│   ├── AssemblerParser.cs            # текст -> AssemblerBase; .data/.bss/.text/.code;
│   │                                 # директивы данных; NormalizeSizeExtension
│   │                                 # _opMapLex + CASTZ/CASTS; FormatIWithSize
│   │
│   ├── Base/
│   │   └── AssemblerBase.cs          # BaseAddress/ErrorReporter/SectionSize/DataSectionStart
│   │
│   ├── IR/
│   │   ├── AsmItem.cs                # базовый IR-элемент
│   │   ├── AsmInstruction.cs         # авто-декодирует поля через InstructionDecoder
│   │   ├── AsmLabel.cs               # метка (Position)
│   │   ├── AsmData.cs                # данные с выравниванием (alignment)
│   │   ├── IRAssembler.cs
│   │   ├── IOptimizerIRRule.cs
│   │   └── IROptimizer.cs
│   │
│   └── Utils/
│       ├── AsmExtensions.cs          # Emit* + EmitInstI на AssemblerBase
│       └── InstructionEncoder.cs     # Encode/Decode (вкл. CASTZ/CASTS)
│                                     # + extension-блоки на RegType/OpCode/OpCodeSize
│
└── C/                                # Frontend (namespace Nantuc.Compiler.C)
    ├── Lexer.cs                      # Token, TokenType, Lexer + 3-символьные операторы (<<=, >>=)
    ├── MiniCLanguageDefinition.cs    # Keywords/Operators (FrozenSet)
    ├── StructLayout.cs               # StructLayout (Dictionary<string, FieldInfo>), FieldInfo
    ├── TokenTypeExtension.cs         # TokenType.AsText — switch без рефлексии, AOT-safe
    │
    ├── Parser.cs                     # partial class Parser — корень + Parse()
    ├── Parser.Declarations.cs        # ParseDeclSpec/ParseDeclarators/ParseVariableTail
    ├── Parser.Expressions.cs         # ParseAssignment..ParsePrimary (13 уровней приоритета)
    ├── Parser.Literals.cs            # Number/Char/String/Hex — литералы
    ├── Parser.Statements.cs          # ParseBlock/If/While/DoWhile/For/Return/Break/Continue/Asm
    ├── Parser.TopLevel.cs            # Parse() целиком: struct, #include, extern, funcs, глобалы
    │
    ├── AST/                          # namespace Nantuc.Compiler.C.AST
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
    │   └── SizeofTypeNode.cs / SizeofExprNode.cs
    │
    ├── CodeGenerator/                # namespace Nantuc.Compiler.C.CodeGenerator
    │   ├── CodeGenUtils.cs           # размеры/выравнивание + IsSignedType + EmitCastToType
    │   ├── FunctionContext.cs        # контекст + ReturnSize/ReturnIsSigned/ReturnIsVoid
    │   ├── FunctionGenerator.cs      # пролог/эпилог, касты аргументов и return
    │   ├── GlobalInfo.cs             # + IsSigned
    │   ├── GlobalMemoryManager.cs    # реестр глобалов, IsSigned вычисляется
    │   ├── RegisterAllocationResult.cs # UsedRegisters/UsedSaveRegs
    │   ├── RegisterAllocator.cs      # жадная раскраска + CollectAddressTakenVars
    │   ├── VariableAccessor.cs       # load/store с канонизацией через CASTZ/CASTS
    │   ├── VarLocation.cs            # + IsSigned
    │   │
    │   ├── Expression/               # partial ExpressionGenerator — по темам
    │   │   ├── ExpressionGenerator.cs            # корень + GenerateExpression(expr[, targetReg])
    │   │   ├── ExpressionGenerator.Arrays.cs     # ArrayAccess/StoreR0ToArrayElement
    │   │   ├── ExpressionGenerator.Assignment.cs # GenerateAssignmentExpr + CheckConstTarget
    │   │   ├── ExpressionGenerator.Binary.cs     # GenerateBinaryOp + logical/compare
    │   │   ├── ExpressionGenerator.Calls.cs      # FunctionCall/IndirectCall + call-site касты
    │   │   ├── ExpressionGenerator.Conditions.cs # тернарник, bool-контекст
    │   │   ├── ExpressionGenerator.Helpers.cs    # SaveRegToStack/RestoreRegFromStack
    │   │   ├── ExpressionGenerator.Increment.cs  # GenerateIncrement + const-проверки
    │   │   ├── ExpressionGenerator.MemberAccess.cs # GenerateMemberAddress + arrow/dot
    │   │   ├── ExpressionGenerator.PointerOps.cs # ApplyPointerArithmeticScaling
    │   │   ├── ExpressionGenerator.Sizeof.cs     # SizeofType/SizeofExpr
    │   │   └── ExpressionGenerator.Unary.cs      # Unary/AddressOf/Dereference
    │   │
    │   └── Statement/                # partial StatementGenerator — по темам
    │       ├── StatementGenerator.cs             # корень + switch по узлам
    │       ├── StatementGenerator.Assignments.cs # GenerateAssignment (4 lvalue-ветки)
    │       ├── StatementGenerator.CompoundAssignments.cs # x op= / arr[i] op= / *p op= / obj.f op=
    │       ├── StatementGenerator.ControlFlow.cs # If/While/DoWhile/For/Break/Continue/Return
    │       ├── StatementGenerator.Declarations.cs # объявления локальных + init
    │       ├── StatementGenerator.InlineAsm.cs   # InlineAsmNode -> new AssemblerParser()
    │       └── StatementTypeResolver.cs          # IsArrayElemSigned/IsPointedSigned/IsFieldSigned
    │
    └── Optimizations/                # namespace Nantuc.Compiler.C.Optimizations
        ├── AstOptimizer.cs
        ├── IAstOptimizationRule.cs
        └── ILogOptimization.cs       # контракт лога оптимизации (+ OptimizationResult)
```

Замечания по организации:
- Frontend (`C/*` и `C/AST/*`) знает про байткод через `OpCode`/`RegType`/`OpCodeSize`, но не про `Assembler`/`IRAssembler` — кодогенератор работает через `AssemblerBase`-extension-методы из `Utils/AsmExtensions.cs`/`Utils/InstructionEncoder.cs`.
- **AST-узлы вынесены в отдельный namespace `Nantuc.Compiler.C.AST` и физически разложены по файлам в `C/AST/` (по одному узлу на файл, кроме базового `ASTNode.cs`)**.
- **`Parser` — partial class из 6 файлов** (`Parser.cs` + `Parser.Declarations.cs`/`.Expressions.cs`/`.Literals.cs`/`.Statements.cs`/`.TopLevel.cs`). Публичный API один — `Parse()`; partial-разбиение только для читаемости.
- **`ExpressionGenerator` — partial class из 12 файлов** в `CodeGenerator/Expression/` — по темам (Arrays/Assignment/Binary/Calls/Conditions/Helpers/Increment/MemberAccess/PointerOps/Sizeof/Unary). **`StatementGenerator` — partial class из 6 файлов** в `CodeGenerator/Statement/` (Assignments/CompoundAssignments/ControlFlow/Declarations/InlineAsm) + `StatementTypeResolver.cs`. Разбиение сделано для XML-документации и навигации; API-методы не изменились.
- Два backend'а: `Assembler` (прямой, `MemoryStream` + `BinaryWriter`) и `IRAssembler` (список `AsmItem`). `AssemblerParser` работает с любым через `AssemblerBase`.
- Оптимизации: `AstOptimizer` (AST-level) и `IROptimizer` (IR-level) — оба каркасы, ждут конкретных правил.
- **`TokenTypeExtension.AsText`** — единственное место, где `TokenType` превращается в строку; используется везде в диагностике вместо `Enum.ToString()` (AOT-safe, без рефлексии, без боксинга).
- **`AsmLanguageDefinition.Mnemonics`/`Registers`** — источник истины для NANASM-скриптов, но **не синхронизирован с XML-шапкой `AssemblerParser`** (см. п. 20).

## API

### Общая картина

| Тип | Namespace | Вид | Назначение |
|---|---|---|---|
| `ASTNode` + 28 наследников | `C.AST` | `abstract class` / `class` | Иерархия AST. Полный каталог — в разделе [Каталог AST-узлов](#каталог-ast-узлов) |
| `Lexer` | `C` | `class` | Токенизация; `Token`, `TokenType`; **3-символьные операторы `<<=`/`>>=`** |
| `Parser` | `C` | `partial class` (6 файлов) | Recursive descent -> `ProgramNode`; **`ParseDeclSpec` для `const`**, **`ParseFunction(..., isReturnPointer)`** |
| `MiniCLanguageDefinition` | `C` | `static partial class` | `Keywords`/`Operators` (`FrozenSet`); `const` в `Keywords` |
| `StructLayout` / `FieldInfo` | `C` | `class` / `record` | Раскладка структур; `_fields` — `Dictionary<string, FieldInfo>` |
| `TokenTypeExtension` | `C` | `static class` | `extension(TokenType).AsText` — ручной switch, AOT-safe |
| `CodeGenUtils` | `C.CodeGenerator` | `static class` | `GetSizeForType`/`GetSizeInBytes`/`GetAlignment`/`IsPrimitiveType`/`IsSignedType`; extension `EmitCastToType`, `EmitMultiplyByConstant` |
| `ExpressionGenerator` | `C.CodeGenerator` | `partial class` (12 файлов, `Expression/`) | Генерация выражений; **`GenerateAssignmentExpr` с 4 lvalue-ветками**, **`CheckConstTarget`** (internal), **overflow-касты** |
| `StatementGenerator` | `C.CodeGenerator` | `partial class` (6 файлов, `Statement/`) | Генерация операторов; **вызывает `_exprGen.CheckConstTarget`**; **overflow-касты на всех lvalue** |
| `StatementTypeResolver` | `C.CodeGenerator` | `static class` | `IsArrayElemSigned`/`IsPointedSigned`/`IsFieldSigned` — вынесено из `StatementGenerator` |
| `FunctionGenerator` | `C.CodeGenerator` | `class` | `Generate(ProgramNode)`, пролог/эпилог; **каст аргументов r0..r3 и pseudo-globals** |
| `FunctionContext` | `C.CodeGenerator` | `class` | `Create(...)` -> `VarMap`, `TotalLocalSize`, `SaveAreaSize`; **`ReturnSize`/`ReturnIsSigned`/`ReturnIsVoid`**, **`VarLocation.IsSigned`** |
| `RegisterAllocator` | `C.CodeGenerator` | `static class` | `AllocateRegisters(...)` -> `RegisterAllocationResult`; `CollectAddressTakenVars` |
| `RegisterAllocationResult` | `C.CodeGenerator` | `class` | `UsedRegisters`, `UsedSaveRegs` — результат раскраски |
| `VariableAccessor` | `C.CodeGenerator` | `class` | `LoadToRegister`/`StoreFromRegister` с **`EmitCastToType`** после LOAD и после MOV в home-регистр |
| `GlobalMemoryManager` | `C.CodeGenerator` | `class` | `Allocate`/`Contains`/`GetInfo`/`GetLabel`; **`IsSigned` вычисляется в `Allocate`** |
| `VarLocation` | `C.CodeGenerator` | `class` | Метаданные переменной; **`IsSigned`** |
| `AstOptimizer` | `C.Optimizations` | `class` | `Add`/`Insert`/`Remove`/`Optimize(ProgramNode)` -> `IEnumerable<IReadOnlyLogOptimization>` |
| `IAstOptimizationRule` / `ILogOptimization` | `C.Optimizations` | `interface` | Контракт правила / контракт лога |
| `OptimizationResult` | `C.Optimizations` | `class : ILogOptimization` | Простая реализация лога |
| `AssemblerBase` | `ASM.Base` | `abstract class` | `Build`/`EmitInstruction`/`EmitInstruction64`/`EmitInstruction64WithLabel`/`EmitJump`/`EmitJumpToAbsolute`/`MarkLabel`/`EmitData`; свойства `BaseAddress`/`ErrorReporter`/`CodeSectionSize`/`DataSectionStart`/`DataSectionSize` |
| `Assembler` | `ASM` | `class : AssemblerBase` | Прямая запись; `_patches`; `DataSection*`/`CodeSectionSize` в `Build()` |
| `AssemblerParser` | `ASM` | `partial class` | `Assemble(code, baseAddress, ErrorReporter)`, `Build(code, asm)`, `Assemble(code, asm, allowDataSection)`; секции `.data`/`.bss`/`.text`/`.code`; **`FormatIWithSize` для `CASTZ`/`CASTS`** |
| `AsmLanguageDefinition` | `ASM` | `static class` | `Mnemonics`/`Registers` (`FrozenSet`); regex'ы без `Compiled` |
| `IRAssembler` | `ASM.IR` | `class : AssemblerBase` | `GetItems()`, двухпроходный `Build()`, padding-`AsmData` |
| `AsmItem`/`AsmLabel`/`AsmData`/`AsmInstruction` | `ASM.IR` | `class` | IR-элементы (каждый в своём файле); `AsmInstruction` авто-декодирует поля через `InstructionDecoder` |
| `IOptimizerIRRule` | `ASM.IR` | `interface` | `TryOptimize(items, index, passCount, out newIndex)`, `GetLogs()`, `Clear()` |
| `IROptimizer` | `ASM.IR` | `class` | `Optimize(items, maxPasses = 5)` — до неподвижной точки; `Clear()` на входе |
| `InstructionEncoder` | `ASM.Utils` | `static class` | `Encode`/`EncodeR`/`EncodeI`/`EncodeU`/`EncodeJ` + `EncodeLDI`/…; **`Decode(uint)`** с case'ами для `CASTZ`/`CASTS`; extension-блоки |
| `AsmExtensions` | `ASM.Utils` | `static class` | `EmitMov`/…/`EmitInstI`/`EmitInstR`/`EmitInstU`/`EmitInstJ`/`EmitInstWithData` |

### Frontend

**`Lexer(source, ErrorReporter)`** — `Tokenize() -> List<Token>`. Поддерживает `//`/`/* */`, hex (`0x`), char/string-литералы, `->` как отдельный `TokenType.Arrow`. **3-символьные операторы `<<=`/`>>=` распознаются в `ReadOperator` до проверки на 2-символьные** — иначе `x >>= 1` режется на `>>` + `=` и падает в `Parser.ParseAssignment`. `Peek(offset = 1)` — единственный lookahead.

**`Parser(tokens, ErrorReporter)`** — `partial class` из 6 файлов. Публичный контракт — `Parse() -> ProgramNode` (namespace `Nantuc.Compiler.C.AST`); остальные методы — `private`/`internal`, распределены по partial'ам:
- `Parser.cs` — корень (конструктор, поля, `Parse()`).
- `Parser.TopLevel.cs` — `Parse()`: `struct`, `#include`, `extern`, функции, глобалы; `_structNames` пополняется по мере встречи.
- `Parser.Declarations.cs` — `ParseDeclSpec`/`ParseDeclarators`/`ParseVariableTail`/`ParseFunction(type, name, isReturnPointer)`.
- `Parser.Expressions.cs` — `ParseAssignment` -> `ParseLogicalOr` -> `ParseLogicalAnd` -> `ParseBitwiseOr`/`Xor`/`And` -> `ParseEquality` -> `ParseRelational` -> `ParseShift` -> `ParseAdditive` -> `ParseMultiplicative` -> `ParseUnary` -> `ParsePrimary`.
- `Parser.Statements.cs` — `ParseBlock`/`ParseBody`/`ParseIf`/`ParseWhile`/`ParseDoWhile`/`ParseFor`/`ParseReturn`/`ParseBreak`/`ParseContinue`/`ParseInlineAsm`.
- `Parser.Literals.cs` — `Number` (десятичные, `0x` hex, char/string).

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

**AST-иерархия (`Nantuc.Compiler.C.AST`)** — 28 классов, разложены по файлам в `C/AST/`. Базовый `ASTNode` — единственное абстрактное свойство `NameNode` (`=> nameof(XxxNode)`). `AssignmentNode` содержит `LValue`, `CompoundOp`, `IndexExpr`. `VariableNode` содержит `IsPointer`/`PointedType`/`IsArray`/`ArraySize`/`ArrayInitializer` + `IsConst`/`IsPointedConst`. `ParameterNode` + `IsConst`/`IsPointedConst`. **`FunctionNode` — `LocalSize`, `IsExternal`, `ReturnIsPointer`**.

**`TokenTypeExtension`** — `extension(TokenType).AsText` возвращает читаемое имя через ручной `switch`, без `Enum.ToString()`. Используется во всех сообщениях парсера — **AOT-safe, без рефлексии, без боксинга**.

### Кодогенератор

**`FunctionGenerator.Generate(ProgramNode)`** — последовательность:
1. `GlobalMemoryManager` + `Allocate` для всех `GlobalVarNodes`, `IsConst`/`IsPointedConst`/`IsSigned` вычисляются автоматически.
2. `_functionTable` заполняется из `FunctionNodes`.
3. `MarkLabel("__global_init")` + `GenerateGlobalInit` — инициализация глобалов (только скаляры, структуры пропускаются).
4. `EmitJmp("func_main")`.
5. `GenerateFunction` для каждой не-external функции.
6. `EmitData` для глобалов (с `ArrayInitializer`).
7. `EmitData` для pseudo-globals параметров 4+.

**`FunctionContext.Create`** — вычисляет:
- `VarMap`: `VarLocation` для каждого параметра и локальной переменной, включая **`IsSigned`** (для non-pointer, non-struct).
- `TotalLocalSize` — суммарный размер стековых переменных, выровненный по 8.
- `SaveAreaSize = (ComputeMaxBinaryDepth(body) + 1) * 8`.
- `UsedRegisters` — регистры из `RegisterAllocator`.
- **`ReturnSize`/`ReturnIsSigned`/`ReturnIsVoid`** — для каста `return` в `GenerateReturn`.

**`RegisterAllocator.AllocateRegisters`** — жадная раскраска:
- Резервирует `r0..r4`, `r27`, `rFL`, `rSP`, `rHP`, `rIP` — **22 доступных** (`r5..r26`; комментарий в коде «23 регистра» — неверен, см. п. 14).
- **Исключает переменные, чей адрес берётся (`&x`)** — `CollectAddressTakenVars` до раскраски.
- Строит граф по `[firstDef, lastUse]`, порядок раскраски — по убыванию степени.
- Возвращает `RegisterAllocationResult` (`UsedRegisters`/`UsedSaveRegs`).

**`ExpressionGenerator`** — `partial class` из 12 файлов (namespace `Nantuc.Compiler.C.CodeGenerator`, папка `Expression/`). Публичный API один — `GenerateExpression(expr)` (в `r0`) и `GenerateExpression(expr, targetReg)`; внутри распределено по partial'ам:
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
- `ExpressionGenerator.PointerOps.cs` — `ApplyPointerArithmeticScaling`.
- `ExpressionGenerator.Arrays.cs` — `ArrayAccessNode` + `StoreR0ToArrayElement`.
- `ExpressionGenerator.Calls.cs` — `FunctionCallNode`/`IndirectCallNode` + call-site касты аргументов к типу параметра.
- `ExpressionGenerator.Conditions.cs` — тернарник и приведение к bool-контексту.
- `ExpressionGenerator.Helpers.cs` — `SaveRegToStack`/`RestoreRegFromStack`; `ExpressionSaveDepth`, слот = `TotalLocalSize + ExpressionSaveDepth * 8`, адрес через `ADDR_SCRATCH_REG_EN = r27`.
- `ExpressionGenerator.Increment.cs` — **`GenerateIncrement`** — `++`/`--` для `Identifier`/`ArrayAccess`/`Dereference`/`MemberAccess`. В начале — `const`-проверки. После `Inc`/`Dec` и после load — `EmitCastToType`.
- `ExpressionGenerator.MemberAccess.cs` — `GenerateMemberAddress` + `.`/`->`.
- `ExpressionGenerator.Sizeof.cs` — `SizeofTypeNode`/`SizeofExprNode`.
- `ExpressionGenerator.Unary.cs` — `UnaryOpNode`/`AddressOfNode`/`DereferenceNode`.

**`StatementGenerator`** — `partial class` из 6 файлов + `StatementTypeResolver` (namespace `Nantuc.Compiler.C.CodeGenerator`, папка `Statement/`). Публичный API — `GenerateBlock(BlockNode)`; внутри распределено по partial'ам:
- `StatementGenerator.cs` — корень + switch по узлам, `_loopStack`.
- `StatementGenerator.Assignments.cs` — `GenerateAssignment` (4 lvalue-ветки); **в начале вызывает `_exprGen.CheckConstTarget`**.
- `StatementGenerator.CompoundAssignments.cs` — `GenerateCompoundAssignment` -> `GenerateCompoundArrayElement` / `GenerateCompoundDeref` / `GenerateCompoundMember` / `GenerateCompoundIdent`; **overflow-касты на всех lvalue**.
- `StatementGenerator.ControlFlow.cs` — `GenerateIf`/`GenerateWhile`/`GenerateFor`/`GenerateDoWhile`/`GenerateReturn` (с `EmitCastToType` к типу возврата)/`GenerateBreak`/`GenerateContinue`.
- `StatementGenerator.Declarations.cs` — объявления локальных + инициализация.
- `StatementGenerator.InlineAsm.cs` — `InlineAsmNode` -> `new AssemblerParser().Assemble(...)`.
- `StatementTypeResolver.cs` — `IsArrayElemSigned`/`IsPointedSigned`/`IsFieldSigned` (вынесено из `StatementGenerator` для читаемости).

### Ассемблер

**`AssemblerBase`** — абстракция эмиттера. Публичные свойства:
- `BaseAddress` — адрес для патча прыжков.
- `ErrorReporter` — публичное readonly-поле.
- `CodeSectionSize` / `DataSectionStart` / `DataSectionSize` — `{ get; protected set; }`, заполняются в `Build()`.

**`Assembler`** — прямая запись в `MemoryStream`:
- `EmitInstruction(uint)` — 4 байта.
- `EmitInstruction64(uint, ulong)` — 4 + `Align8()` + 8.
- `EmitInstruction64WithLabel(uint, string)` / `EmitJump(uint, string)` — 4 + `Align8()` + placeholder, патч в `Build()`.
- `EmitJumpToAbsolute(uint, ulong)` — 4 + `Align8()` + 8, без патча.
- `EmitData(string, byte[])` — `Align8()`, `MarkLabel`, запись.
- `Build()` — фиксирует `DataSectionStart`/`DataSectionSize`/`CodeSectionSize`, патчит `_patches`.
- `Align8()` — `stackalloc byte[pad]` (0..7 байт).

**`AssemblerParser`** — текстовый ассемблер. **Полный набор инструкций:**
- Без операндов: `NOP`, `END`, `RET`, `HALT`, `WAKE`.
- R-формат (2 регистра): `MOV`, `MOD`, `ADD`, `SUB`, `AND`, `OR`, `XOR`, `IN`, `OUT`, `SHR`, `SHL`, `DIV`, `MULT_INT`, `CMP`, `TEST`.
- U-формат (1 регистр): `JMP_IND`, `CALL_IND`, `WAKE_INT`, `INC`, `DEC`, `NOT`, `PUSH`, `POP`.
- **I+size-формат (1 регистр + `DataSize`): `CASTZ[.S8|.S16|.S32|.S64]`, `CASTS[...]`** — новый `FormatIWithSize`, эмитит `EmitInstI`.
- `LDI reg, const|label` — константа (`0x...` hex) или метка.
- `LOAD[.size] reg, addr` / `LOAD_UNSAFE ...`, `STORE[.size] reg, addr` / `STORE_UNSAFE ...`.
- `LOAD_IND[.size] regDst, regAddr` / `STORE_IND[.size] regSrc, regAddr` (+unsafe).
- `JMP`/`JZ`/`JNZ`/`JG`/`JL`/`CALL` — метка или абсолютный адрес.

**Нормализация размера:** `NormalizeSizeExtension` чинит `LOAD . S8` -> `LOAD.S8` через `[GeneratedRegex(@"(\w)\s*\.\s*(\w)")]` (AOT-safe). Есть fast-path: если нет `' . '` — возвращает строку без аллокаций.

**Состояние парсера — instance-level:** `_asm`, `_allowDataSection`, `_pendingData`, `_inDataSection`, `_pendingLabel`, `_currentLine` мутируются в `Assemble`. **Не потокобезопасен** (см. п. 13).

**`AsmLanguageDefinition`** — `Mnemonics`/`Registers` (`FrozenSet`), regex'ы без `Compiled`. `_regMapLex`/`_opMapLex` — обычные `Dictionary`, **плюс** их `FrozenDictionary`-копии `_regMap`/`_opMap` (см. п. 11).

**`IRAssembler`** — список `AsmItem`:
- `EmitInstruction64(uint, ulong)` валидирует `HasNeed64IntData(op)`.
- `EmitJump(uint, string)` -> `AsmInstruction` с `TargetLabel`.
- `EmitJumpToAbsolute(uint, ulong)` -> `AsmInstruction(..., isAbsolute: true)`.
- `EmitData(string, byte[])` -> padding-`AsmData(alignment: 8)` + `MarkLabel` + `AsmData(data, alignment: 1)`.
- `MarkLabel` — дубли через `HashSet<string>`; при коллизии — `Asm_DuplicateLabel` с позицией.
- `Build()` — двухпроходный: `labelAddresses`, потом байты и патч прыжков; `emptyPadding = new byte[7]`.
- `GetItems()` — **внутренний** `List<AsmItem>`, мутации видны `Build()`.

**`InstructionEncoder`** — кодирование 32-битного слова:
- `Encode`/`EncodeR`/`EncodeI`/`EncodeU`/`EncodeJ` — форматы.
- Специализированные: `EncodeLDI`/`EncodeLOAD`/`EncodeSTORE`/`EncodeLOAD_IND`/`EncodeSTORE_IND`/`EncodeCALL`/`EncodeRET`/`EncodeHALT`/`EncodeWAKE`/`EncodeEND`/`EncodeNOP`.
- **`Decode(uint)`** — покрывает все опкоды, включая `LOAD_UNSAFE`/`STORE_UNSAFE`/`LOAD_IND_UNSAFE`/`STORE_IND_UNSAFE`/`CMP`/`TEST`/`WAKE_INT`/`CASTZ`/`CASTS` (`CASTZ.S8 r0`, `CASTS.S32 r1`).
- Extension-блоки: `OpCode.ToUint()`/`OpName()`, `RegType.ToUint()`/`RegName()`, `OpCodeSize.ToUint()`/`SizeName()`.
- Extension-блок на `BinaryWriter`: `WriteInstruction(uint)`, `Align8()`, `WriteInstructionWithData64(uint, ulong)`. **В `Assembler` не используется** (свои методы), но доступен для кастомных backend'ов.

**`AsmExtensions`** — extension-блок на `AssemblerBase`. Кодогенератор работает только через эти методы:
- R-формат: `EmitMov`, `EmitMult`, `EmitAdd`, `EmitSub`, `EmitMultInt`, `EmitDiv`, `EmitAnd`, `EmitOr`, `EmitXOr`, `EmitShl`, `EmitShr`, `EmitMod`, `EmitCmp`.
- U-формат: `EmitPush`, `EmitPop`, `EmitNot`, `EmitInc`, `EmitDec`.
- Специальные: `EmitLdi(ulong)`, `EmitLdi(string)`, `EmitLoadInd`, `EmitStoreInd`, `EmitLoad`, `EmitStore` (2 overload'а).
- Прыжки: `EmitJmp`, `EmitJz`, `EmitJnz`, `EmitJl`, `EmitJg`, `EmitCall`; `EmitInstJAbsolute`.
- Без операндов: `EmitEnd`, `EmitRet`, `EmitNop`, `EmitHalt`, `EmitWake`.
- Общие: `EmitInst`, `EmitInstU`, `EmitInstR`, **`EmitInstI`** (используется для `CASTZ`/`CASTS`), `EmitInstJ`, `EmitInstJAbsolute`, `EmitInstWithData`.

**`CodeGenUtils.EmitCastToType(reg, size, isSigned)`** — extension на `AssemblerBase`:
- `size == S64` -> no-op;
- `isSigned == true` -> `EmitInstI(OpCode.CASTS, reg, size)`;
- `isSigned == false` -> `EmitInstI(OpCode.CASTZ, reg, size)`.

### Оптимизации

**`AstOptimizer`** — `Add`/`Insert`/`Remove`/`RemoveAt`, `Count`, `GetType(i)`. `Optimize(program)` возвращает `IEnumerable<IReadOnlyLogOptimization>` — по одному на правило. Правил в этой партии нет.

**`IROptimizer`** — `Optimize(items, maxPasses = 5)` — до неподвижной точки. Пропускает `AsmLabel`, применяет первое подходящее правило. `Clear()` на всех правилах в начале.

## Примеры для API

**Полный пайплайн:**

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

- Автор нового синтаксиса — `Lexer`/`Parser` (partials в `Parser.*.cs`)/`ASTNode` (`C/AST/`).
- Автор оптимизаций — `IAstOptimizationRule`/`IOptimizerIRRule`.
- Автор нового backend'а — `AssemblerBase`.
- Автор нового target'а ISA — `InstructionEncoder` + `AsmExtensions`.
- Автор NANASM-скриптов — `AssemblerParser` с секциями `.data`/`.bss`/`.text`/`.code`.

**Что стоит делать:**
- `IRAssembler` для production-пайплайна; `Assembler` — для тестов.
- Пользовательские оптимизации — через `Add` в `AstOptimizer`/`IROptimizer`; правила идемпотентны (`Clear()` на входе).
- Для inline-asm — `AsmLanguageDefinition.Mnemonics`/`Registers` как источник истины.
- **`const`** — использовать как гарантию неизменяемости в API и внутренних данных.
- **Overflow** — писать в узкие типы (byte/char/short/int/uint/ushort) как в обычном C; касты вставляет кодогенератор.
- `InstructionEncoder.Decode(uint)` — для отладки.
- **Добавление нового выражения** — новый partial `ExpressionGenerator.Xxx.cs` в `CodeGenerator/Expression/`; **нового оператора** — новый partial `StatementGenerator.Xxx.cs` в `CodeGenerator/Statement/`. API-методы не меняются, `Parser`/`StatementGenerator.cs` только диспетчеризуют.

**Что не стоит делать:**
- Полагаться на `FunctionNode.Body` у `extern` — там `null!`.
- Полагаться на `RegisterAllocator` для >22 одновременно живых скаляров.
- Использовать `MiniCLanguageDefinition.GetKeywordRegex()` — dead code.
- `.data` в inline-asm — запрещено.
- `const` на полях `struct` — не поддержано.
- Один `AssemblerParser` на несколько потоков — не потокобезопасен.
- **Полагаться на compile-time свёртку `const`** — его нет; `const int N = 10; int arr[N];` — ошибка парсинга.

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
| `NumberNode` | `Value: long` | `Parser.Literals.cs` -> `ParseNumber` (из `ParsePrimary`) |
| `IdentifierNode` | `Name` | `Parser.Expressions.cs` -> `ParsePrimary` |
| `AssignmentNode` | `Name`, `IndexExpr?`, `LValue`, `CompoundOp?`, `Value` | `Parser.Expressions.cs` -> `ParseAssignment` |
| `BinaryOpNode` | `Operator`, `Left`, `Right` | `Parser.Expressions.cs` -> `ParseLogicalOr`/`And`, `ParseBitwiseOr`/`Xor`/`And`, `ParseEquality`, `ParseRelational`, `ParseShift`, `ParseAdditive`, `ParseMultiplicative`, `ParseComma` |
| `UnaryOpNode` | `Operator`, `Operand` | `Parser.Expressions.cs` -> `ParseUnary` |
| `IncrementNode` | `Target`, `Delta`, `IsPrefix` | `Parser.Expressions.cs` -> `ParseUnary` (префикс), `ParsePrimary` (постфикс) |
| `FunctionCallNode` | `Name`, `Arguments` | `Parser.Expressions.cs` -> `ParsePrimary` |
| `IndirectCallNode` | `Callee`, `Arguments` | `Parser.Expressions.cs` -> `ParsePrimary` (постфиксный вызов) |
| `ArrayAccessNode` | `ArrayName`, `Index` | `Parser.Expressions.cs` -> `ParsePrimary` |
| `AddressOfNode` / `DereferenceNode` | `Operand` | `Parser.Expressions.cs` -> `ParseUnary` |
| `MemberAccessNode` | `Object`, `FieldName`, `IsArrow` | `Parser.Expressions.cs` -> `ParsePrimary` |
| `SizeofTypeNode` / `SizeofExprNode` | `TypeName`/`IsPointer`, `Operand` | `Parser.Expressions.cs` -> `ParseUnary` |

Итого 28 наследников. **Кто ходит по AST в кодогенераторе:** `ExpressionGenerator` (все partial'ы), `StatementGenerator` (все partial'ы + `StatementTypeResolver`), `FunctionContext.ComputeMaxBinaryDepth`, `RegisterAllocator` (`CollectVarEvents`, `CollectAddressTakenVars`), `FunctionGenerator.CollectLocalVars`. При добавлении узла — обновите все эти места.

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
- `VarLocation.IsSigned` / `GlobalInfo.IsSigned` — вычисляются в `FunctionContext.Create` / `GlobalMemoryManager.Allocate`.
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
| Сборка в WASM | ✅ | Нет `unsafe`, `DllImport`, `LibraryImport`. `stackalloc` только в `Assembler.Align8` (0–7 байт). |
| `NativeMemory` | ✅ | Не используется. |
| `stackalloc` / `InlineArray` | ✅ | `stackalloc byte[pad]` (0..7 байт) в `Assembler.Align8`. |
| `Interlocked` / `Volatile` | ✅ | Не используются. |
| `System.Collections.Frozen` | ✅ | `FrozenSet` + **`FrozenDictionary`** (`_regMap`/`_opMap`). |
| `[GeneratedRegex]` | ✅ | `AssemblerParser.GetRexExtensionAsm()` — единственный regex в горячем пути. |
| `Regex` без `Compiled` | ✅ | `AsmLanguageDefinition._mnemonicsRegex`/`regex` — без `Compiled`. |
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
FunctionGenerator.Generate(program)
  ├── GlobalMemoryManager.Allocate(...) — IsSigned вычисляется
  ├── GenerateGlobalInit (только скаляры)
  ├── EmitJmp("func_main")
  ├── foreach func in FunctionNodes:
  │     ├── CollectLocalVars(func.Body)
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
  │  - патч меток
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
| `;` в строке обрезается | `Split(['\n', ';'])` | Не используйте `;` в `.ascii`/`.asciz` |
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
| Патч меток | `_patches` в `Build()` | `labelAddresses` в первом проходе |
| IR | ❌ | ✅ |
| Проверка `HasNeed64IntData` | ❌ (только при `EmitInstruction64`, `EmitInstruction64WithLabel` не проверяет — п. 12) | ✅ при `EmitInstruction64` (тоже не при `EmitInstruction64WithLabel`) |
| Валидация дублей меток | ✅ | ✅ |
| Двухпроходный Build | ❌ | ✅ |
| `.data` через `AssemblerParser` | ✅ | ✅ |
| `EmitData` | `Align8()` + `MarkLabel` + bytes | padding-`AsmData` + `MarkLabel` + `AsmData(data)` |
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
   Файл: `AssemblerParser.cs`.  
   **Фикс:** механизм патча по метке в `EmitData`/`Build()`.

10. **[API] `AssemblerParser.TryAddStringData` — `;` внутри строки обрезается.**  
    Файл: `AssemblerParser.cs`.  
    **Фикс:** вынести разбор строк из общего `Split` или добавить hex-escape.

11. **[API] `AssemblerParser` — двойные словари `_regMapLex`/`_opMapLex` + `_regMap`/`_opMap`.**  
    Файл: `AsmLanguageDefinition.cs`.  
    `_regMapLex`/`_opMapLex` — dead после статического конструктора.  
    **Фикс:** инициализировать `FrozenDictionary` напрямую.

12. **[API] `IRAssembler.EmitInstruction64WithLabel` не проверяет `HasNeed64IntData`.**  
    Файл: `ASM/IR/IRAssembler.cs`.  
    **Фикс:** вынести проверку в общий хелпер `Validate64BitOp(op)`.

13. **[API] `AssemblerParser` не потокобезопасен.**  
    Файл: `AssemblerParser.cs`. Поля instance-level. `StatementGenerator.GenerateInlineAsm` (`StatementGenerator.InlineAsm.cs`) создаёт `new AssemblerParser()` на каждый `asm` — но контракт не задокументирован.  
    **Фикс:** `[ThreadStatic]`/`lock`, либо явно задокументировать.

14. **[API] `RegisterAllocator.BuildInterferenceGraph` — линейные live-диапазоны, не учитывают циклы.**  
    Файл: `RegisterAllocator.cs`.  
    **Фикс:** задокументировать как «best effort» или реализовать реальный live-range analysis.

15. **[API] `FunctionGenerator.GenerateGlobalInit` — инициализация структур не поддерживается молча.**  
    Файл: `FunctionGenerator.cs`.  
    `if (CodeGenUtils.IsStructType(global.Type, _structTable)) continue;` — глобальная структура с `Initializer` игнорируется.  
    **Фикс:** поддержать или бросать `NotSupported`.

### Дом

16. **[Дом] AST разложен по файлам `C/AST/`, но без «карты» в шапке папки.**  
    `CodeGenerator` уже разложен по `Expression/`/`Statement/`, а AST — нет.  
    **Фикс:** добавить `C/AST/README.md` или `#region`-карту.

### XML

17. **[XML] AST-иерархия — без `///`.**  
    Включая `VariableNode.IsConst`/`IsPointedConst`, `ParameterNode.IsConst`/`IsPointedConst`, `FunctionNode.ReturnIsPointer`.

18. **[XML] `Parser` (все 6 partial'ов) и `ExpressionGenerator`/`StatementGenerator` (все partial'ы) — без `///`.**  
    Особенно `CheckConstTarget` (`ExpressionGenerator.Assignment.cs`), `GenerateIncrement` (`ExpressionGenerator.Increment.cs`), `GenerateAssignmentExpr` (`ExpressionGenerator.Assignment.cs`). Split на partial'ы сделан именно для читаемости — логичное место добавить `///` в шапку каждого partial'а.

19. **[XML] `CodeGenUtils` — без `///`.**  
    `IsSignedType`, `EmitCastToType`, `GetAlignment(type, structTable)` (жёсткое `8` для структур).

20. **[XML] `AssemblerParser` — шапка класса не перечисляет `.data`, `allowDataSection`, `CASTZ`/`CASTS`.**