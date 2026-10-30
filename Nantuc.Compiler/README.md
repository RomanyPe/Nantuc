﻿﻿# Nantuc.Compiler — README

> Компилятор MiniC → байткод Nantuc: лексер, парсер, AST, кодогенератор с регистровой раскраской, ассемблер (два backend'а — прямой и IR) и инфраструктура оптимизаций.
> `[← Корень](../README.md) · [Common](../Nantuc.Kernel.Common/README.md) · [Contracts](../Nantuc.Kernel.Contracts/README.md) · [Kernel](../Nantuc.Kernel/README.md)`

## Содержание
- [Об этом модуле](#об-этом-модуле)
- [Дерево локального проекта](#дерево-локального-проекта)
- [API](#api)
- [Примеры для API](#примеры-для-api)
- [Приближенность модуля к пользователю](#приближенность-модуля-к-пользователю)
- [Особенная информация](#особенная-информация)
  - [Константность (`const`) — семантика и ограничения](#константность-const--семантика-и-ограничения) **[NEW]**
  - [Ассемблер NANASM: секции данных](#ассемблер-nanasm-секции-данных)
  - [Диагностика: `DiagnosticData` и AOT](#диагностика-diagnosticdata-и-aot) **[NEW]**
- [Что исправить](#что-исправить)

## Об этом модуле

`Nantuc.Compiler` — это frontend и backend компилятора платформы Nantuc. Frontend принимает исходник на MiniC (подмножество C: целочисленные типы, `struct`, указатели, массивы, `sizeof`, `asm`-блоки, **`const`-квалификаторы** **[NEW]**), строит AST и генерирует байткод через двухступенчатую схему: кодогенератор → ассемблер → 32-битные слова ISA. Backend — двухуровневый: либо прямая запись в `MemoryStream` (`Assembler`), либо построение списка IR-инструкций (`ILAssembler`) с возможностью peephole-оптимизаций.

Модуль зависит от `Common` (`OpCode`/`RegType`/`OpCodeSize`/`InstructionDecoder`), `Contracts` (`OptimizationId`/`OptimizationLogEntry`/`IReadOnlyLogOptimization`) и `Diagnostics` (`ThrowCompiler`/`ErrorCode`, **`DiagnosticData`** **[NEW]**). Сам по себе `unsafe`-кода не содержит — `stackalloc` только в `Assembler.Align8` (0–7 байт, WASM-safe).

Ключевые роли модуля:
- Frontend: `Lexer`, `Parser`, `ASTNode`-иерархия (28 типов), `MiniCLanguageDefinition`, **`TokenTypeExtension`** **[NEW]**.
- Кодогенератор: `ExpressionGenerator`, `StatementGenerator`, `FunctionGenerator`, `RegisterAllocator` (жадная раскраска интерференционного графа), `FunctionContext`, `VariableAccessor`.
- Ассемблер: `AssemblerBase` (абстракция), `Assembler` (прямой), `ILAssembler` (IR), `AssemblerParser`, `InstructionEncoder`, `AsmExtensions`.
  **`AssemblerParser` поддерживает секции `.data`/`.text` и директивы данных** (`.byte`, `.word`, `.dword`, `.quad`, `.ascii`, `.asciz`, `.zero`).
- Оптимизации: `AstOptimizer`, `PeepholeOptimizer` — каркас с интерфейсами `IAstOptimizationRule`/`IPeepholeRule`; **конкретных правил в этой партии нет**.
- Раскладка структур: `StructLayout`, `FieldInfo` — выравнивание полей по 8 байт, `Resolve` с итеративным разрешением зависимостей.

> **XML-документация фрагментарна.** Полностью документированы `AssemblerParser` (шапка класса), `Assembler` (частично), `IPeepholeRule`, `IAstOptimizationRule`. Без `///` — вся AST-иерархия (28 типов, только inline `//`-комментарии в шапке файла), `Lexer`, `Parser`, `ExpressionGenerator`, `StatementGenerator`, `FunctionGenerator`, `RegisterAllocator`, `CodeGenUtils`, `GlobalMemoryManager`, `VariableAccessor`, `AsmLanguageDefinition`. Подробности и дефекты — в разделе [Что исправить](#что-исправить).

## Дерево локального проекта

Namespace-ы по слоям, файлы плоские внутри них:

```
Nantuc.Compiler/
├── Nantuc.Compiler.csproj            # net10.0, refs Common+Contracts+Diagnostics
│
├── C/                                 # Frontend (namespace Nantuc.Compiler.C)
│   ├── ASTNode.cs                     # ASTNode + 28 наследников
│   ├── Lexer.cs                       # Token, TokenType, Lexer
│   ├── Parser.cs                      # Parser (recursive descent)
│   ├── MiniCLanguageDefinition.cs     # Keywords/Operators/Punctuation (FrozenSet)
│   ├── StructLayout.cs                # StructLayout, FieldInfo
│   ├── TokenTypeExtension.cs          # TokenType.AsText — switch без рефлексии, AOT-safe [NEW]
│   │
│   ├── CodeGenerator/                 # namespace Nantuc.Compiler.C.CodeGenerator
│   │   ├── CodeGenUtils.cs            # статические утилиты размеров/выравнивания
│   │   ├── ExpressionGenerator.cs     # генерация выражений + проверки const [CONST]
│   │   ├── FunctionContext.cs         # контекст функции + анализ глубины сохранений + const-флаги [CONST]
│   │   ├── FunctionGenerator.cs       # пролог/эпилог, обход функций + const → GlobalMemoryManager [CONST]
│   │   ├── GlobalInfo.cs              # DTO для глобалов + IsConst/IsPointedConst [CONST]
│   │   ├── GlobalMemoryManager.cs     # реестр глобалов + метки + const-флаги [CONST]
│   │   ├── RegisterAllocator.cs       # жадная раскраска графа + CollectAddressTakenVars [FIX]
│   │   ├── StatementGenerator.cs      # генерация операторов + CheckConstTarget [CONST]
│   │   ├── VarLocation.cs             # VarLocation + RegisterAllocationResult + const-флаги [CONST]
│   │   └── VariableAccessor.cs        # load/store переменных + IsConst/IsPointedConst [CONST]
│   │
│   └── Optimizators/                  # namespace Nantuc.Compiler.C.Optimizators
│       ├── AstOptimizer.cs            # AstOptimizer + OptimizationResult
│       ├── IAstOptimizationRule.cs
│       └── ILogOptimization.cs
│
└── ASM/                               # Backend (namespace Nantuc.Compiler.ASM)
    ├── Assembler.cs                    # прямая запись в MemoryStream
    ├── AssemblerParser.cs              # текстовый ассемблер → AssemblerBase
    │                                   # + AsmLanguageDefinition (Mnemonics/Registers)
    │                                   # + поддержка .data / .text (буферизация, EmitData в конце)
    │
    ├── Base/
    │   └── AssemblerBase.cs            # абстракция эмиттера
    │
    ├── IR/
    │   ├── AsmInstruction.cs           # AsmItem, AsmLabel, AsmData, AsmInstruction
    │   ├── ILAssembler.cs              # IR-сборщик, двухпроходный Build()
    │   ├── IPeepholeRule.cs
    │   └── PeepholeOptimizer.cs
    │
    └── Utils/
        └── InstructionEncoder.cs       # Encode*, extension-блоки на RegType/OpCode/OpCodeSize
                                        # + AsmExtensions (Emit* поверх AssemblerBase)
```

Замечания по организации:
- Frontend (`C/*`) знает про байткод через `OpCode`/`RegType`/`OpCodeSize`, но не про `Assembler`/`ILAssembler` — кодогенератор работает через `AssemblerBase`-extension-методы из `Utils/InstructionEncoder.cs`.
- Два backend'а: `Assembler` (прямой, `MemoryStream` + `BinaryWriter`) и `ILAssembler` (список `AsmItem`). `AssemblerParser` работает с любым через `AssemblerBase`.
- Оптимизации: `AstOptimizer` (AST-level) и `PeepholeOptimizer` (IR-level) — оба каркасы, ждут конкретных правил.
- **`TokenTypeExtension.AsText`** — единственное место, где `TokenType` превращается в строку; используется везде в диагностике вместо `Enum.ToString()` (AOT-safe, без рефлексии, без боксинга).

## API

### Общая картина

| Тип | Namespace | Вид | Назначение |
|---|---|---|---|
| `ASTNode` + 28 наследников | `C` | `abstract class` / `class` | Иерархия AST: `ProgramNode`, `FunctionNode`, `BlockNode`, `VariableNode`, `AssignmentNode`, `BinaryOpNode`, `IfNode`, `WhileNode`, `ForNode`, `ReturnNode`, `FunctionCallNode`, `ArrayAccessNode`, `AddressOfNode`, `DereferenceNode`, `InlineAsmNode`, `StructDeclNode`, `MemberAccessNode`, `BreakNode`, `ContinueNode`, `DoWhileNode`, `IncrementNode`, `SizeofTypeNode`, `SizeofExprNode` |
| `Lexer` | `C` | `class` | Токенизация; `Token`, `TokenType` |
| `Parser` | `C` | `class` | Recursive descent → `ProgramNode`; **`ParseDeclSpec` для `const`-квалификаторов** [CONST] |
| `MiniCLanguageDefinition` | `C` | `static partial class` | `Keywords`/`Operators`/`Punctuation` (`FrozenSet`); **`const` в `Keywords`** [CONST] |
| `StructLayout` / `FieldInfo` | `C` | `class` / `record` | Раскладка структур, `Resolve` с итеративным разрешением |
| `TokenTypeExtension` | `C` | `static class` | **`extension(TokenType).AsText`** — ручной switch, AOT-safe [NEW] |
| `CodeGenUtils` | `C.CodeGenerator` | `static class` | `GetSizeForType`/`GetSizeInBytes`/`GetAlignment`/`IsPrimitiveType`/`EmitMultiplyByConstant` |
| `ExpressionGenerator` | `C.CodeGenerator` | `class` | Генерация выражений (2 перегрузки `GenerateExpression`); **проверки `const` в `GenerateIncrement`** [CONST] |
| `StatementGenerator` | `C.CodeGenerator` | `class` | Генерация операторов (`GenerateBlock`, `GenerateIf`, ...); **`CheckConstTarget` в начале `GenerateAssignment`** [CONST] |
| `FunctionGenerator` | `C.CodeGenerator` | `class` | Точка входа: `Generate(ProgramNode)`, пролог/эпилог, `CollectLocalVars`; **пробрасывает `IsConst`/`IsPointedConst` в `GlobalMemoryManager.Allocate`** [CONST] |
| `FunctionContext` | `C.CodeGenerator` | `class` | `Create(FunctionNode, ...)` → `VarMap`, `TotalLocalSize`, `SaveAreaSize` (через `ComputeMaxBinaryDepth`); **`IsConst`/`IsPointedConst` в `VarLocation`** [CONST] |
| `RegisterAllocator` | `C.CodeGenerator` | `static class` | `AllocateRegisters(...)` → `RegisterAllocationResult` (жадная раскраска); **`CollectAddressTakenVars`** [FIX] |
| `VariableAccessor` | `C.CodeGenerator` | `class` | `LoadToRegister`/`StoreFromRegister`/`LoadAddressToRegister`/`TryGetRegister`; **`IsConst`/`IsPointedConst`** [CONST] |
| `GlobalMemoryManager` | `C.CodeGenerator` | `class` | `Allocate`/`Contains`/`GetInfo`/`GetLabel`; **`isConst`/`isPointedConst` в `Allocate`** [CONST] |
| `VarLocation` / `RegisterAllocationResult` | `C.CodeGenerator` | `class` | Метаданные переменной; **`IsConst`/`IsPointedConst`** [CONST] |
| `AstOptimizer` | `C.Optimizators` | `class` | `Add`/`Insert`/`Remove`/`Optimize(ProgramNode)` → `IEnumerable<IReadOnlyLogOptimization>` |
| `IAstOptimizationRule` / `ILogOptimization` | `C.Optimizators` | `interface` | Контракт правила / контракт лога |
| `OptimizationResult` | `C.Optimizators` | `class : ILogOptimization` | Простая реализация лога |
| `AssemblerBase` | `ASM.Base` | `abstract class` | `EmitInstruction`/`EmitInstruction64`/`EmitJump`/`MarkLabel`/`EmitData`/`Build` |
| `Assembler` | `ASM` | `class : AssemblerBase` | Прямая запись; `_patches` для меток |
| `AssemblerParser` | `ASM` | `partial class` | Текст → байты; `Assemble(code, baseAddress, ThrowCompiler)`; секции `.data`/`.text` |
| `AsmLanguageDefinition` | `ASM` | `static class` | `Mnemonics`/`Registers` (`FrozenSet`), `GetMnemonicRegex`/`GetRegisterRegex` |
| `ILAssembler` | `ASM.IR` | `class : AssemblerBase` | IR-сборщик: `GetItems()`, двухпроходный `Build()` |
| `AsmItem`/`AsmLabel`/`AsmData`/`AsmInstruction` | `ASM.IR` | `class` | IR-элементы; `AsmInstruction` разбирает `raw` через `InstructionDecoder` |
| `IPeepholeRule` | `ASM.IR` | `interface` | `TryOptimize(items, index, passCount, out newIndex)` |
| `PeepholeOptimizer` | `ASM.IR` | `class` | `Optimize(items, maxPasses)` до неподвижной точки |
| `InstructionEncoder` | `ASM.Utils` | `static class` | `Encode`/`EncodeR`/`EncodeI`/`EncodeU`/`EncodeJ`, `Decode`, extension-блоки |
| `AsmExtensions` | `ASM.Utils` | `static class` | `EmitMov`/`EmitAdd`/`EmitLdi`/`EmitJmp`/`EmitLoad`/`EmitStore`/… на `AssemblerBase` |

### Frontend

**`Lexer(source, ThrowCompiler)`** — `Tokenize() → List<Token>`. Поддерживает `//`/`/* */`, hex (`0x`), char/string-литералы, `->` как отдельный `TokenType.Arrow`. Операторы — из `MiniCLanguageDefinition.Operators`; пунктуация — из `Punctuation`. `Peek(offset = 1)` — единственный lookahead, depth > 1 не поддерживается.

**`Parser(tokens, ThrowCompiler)`** — `Parse() → ProgramNode`. Обрабатывает:
- `struct Name { ... };` — `_structNames` заполняется по мере встречи, поддерживаются и `Name` и `struct Name`.
- `#include "path"` — добавляется в `IncludesList`.
- `extern <type> <name>(<params>);` — `FunctionNode` с `IsExternal = true`, `Body = null!` (потом никто не проверяет).
- Функции и глобальные переменные через `ParseDeclSpec`/`ParseDeclarators`.
- `asm { ... }` — inline asm, `InlineAsmNode` с текстом.
- Приоритеты операторов: `ParseAssignment` → `ParseLogicalOr` → `ParseLogicalAnd` → `ParseBitwiseOr` → `ParseBitwiseXor` → `ParseBitwiseAnd` → `ParseEquality` → `ParseRelational` → `ParseShift` → `ParseAdditive` → `ParseMultiplicative` → `ParseUnary` → `ParsePrimary`.
- Постфиксные `++`/`--`, `.`/`->`, `[i]`, `(args)` — в цикле `ParsePrimary`.
- **`sizeof` — в `ParseUnary`, включая `sizeof(const T)` и `sizeof(const T*)`** [CONST].
- **`const`-квалификаторы — `ParseDeclSpec`** [CONST] (см. отдельный раздел).

**AST-иерархия** — 28 классов. `AssignmentNode` содержит `LValue`, `CompoundOp` (`+=`, `<<=` и т.д.), `IndexExpr` (для `arr[i] = x`). `VariableNode` содержит `IsPointer`/`PointedType`/`IsArray`/`ArraySize`/`ArrayInitializer` **+ `IsConst`/`IsPointedConst`** [CONST]. `ParameterNode` **+ `IsConst`/`IsPointedConst`** [CONST]. `FunctionNode` — `LocalSize`, `IsExternal`.

**`TokenTypeExtension`** [NEW] — `extension(TokenType).AsText` возвращает читаемое имя (`"Identifier"`, `"Keyword"`, …) через ручной `switch`, без `Enum.ToString()`. Используется во всех сообщениях парсера (`Parser_ExpectedToken`, `Parser_UnexpectedToken`) — **AOT-safe, без рефлексии, без боксинга**.

### Кодогенератор

**`FunctionGenerator.Generate(ProgramNode)`** — последовательность:
1. `GlobalMemoryManager` + `Allocate` для всех `GlobalVarNodes`, **передаются `IsConst`/`IsPointedConst`** [CONST].
2. `_functionTable` заполняется из `FunctionNodes`.
3. `MarkLabel("__global_init")` + `GenerateGlobalInit` — инициализация глобалов (только скаляры, структуры пропускаются).
4. `EmitJmp("func_main")` если `main` есть.
5. `GenerateFunction` для каждой не-external функции.
6. `EmitData` для глобалов (с `ArrayInitializer`).
7. `EmitData` для pseudo-globals параметров 4+ (по 8 байт каждый).

**`FunctionContext.Create`** — вычисляет:
- `VarMap`: `VarLocation` для каждого параметра и локальной переменной.
- `TotalLocalSize` — суммарный размер стековых переменных, выровненный по 8.
- `SaveAreaSize = (ComputeMaxBinaryDepth(body) + 1) * 8` — размер «save area» для промежуточных значений при бинарных операциях.
- `UsedRegisters` — регистры, занятые переменными (из `RegisterAllocator`).
- **`VarLocation.IsConst`/`IsPointedConst` — пробрасываются из `VariableNode`/`ParameterNode`** [CONST].

**`RegisterAllocator.AllocateRegisters`** — жадная раскраска интерференционного графа:
- Резервирует `r0..r4`, `r27`, `rFL`, `rSP`, `rHP`, `rIP` — **22 доступных** (`r5..r26`; комментарий «23 регистра» — неверен, `r27` зарезервирован).
- **Исключает переменные, чей адрес берётся (`&x`) — они обязаны жить в памяти. `CollectAddressTakenVars` рекурсивно обходит AST и собирает имена таких переменных ДО раскраски.** [FIX]
- Строит граф по «живым диапазонам» (`[firstDef, lastUse]`) из линейного обхода AST через `CollectVarEvents`. Если диапазоны пересекаются — ребро.
- Порядок раскраски — по убыванию степени (эвристика Welsh-Powell).
- Кандидаты — только скаляры, не массивы, не структуры, не указатели на структуры.

**`ExpressionGenerator.GenerateExpression(expr)`** (без `targetReg`) — всегда генерирует в `r0`. Вторая перегрузка `(expr, targetReg)`:
- Если `targetReg == r0` — делегирует первой.
- Иначе — `switch` по типу, для неизвестных — `GenerateExpression(expr); EmitMov(targetReg, r0)`.

Специфика:
- `BinaryOpNode` с `IsComparisonOperator` — `Cmp` + прыжки → `1`/`0` в `targetReg`.
- `&&`/`||` — `GenerateLogicalAndOr`, результат в `r0`, потом `Mov(targetReg, r0)`.
- Свёртка констант: `NumberNode op NumberNode` → `Ldi(targetReg, result)`.
- Оптимизация `targetReg == левый операнд`: правый операнд в `r1`/`r2`, `ApplyPointerArithmeticScaling` для `+`/`-` с указателями, `EmitBinaryOperation`.
- Указательная арифметика: `ptr + int` — масштабирование `int` на `sizeof(*ptr)`; `int + ptr` (для `+`) — то же. `ptr - ptr` не масштабируется (разница в байтах, без деления).
- **`GenerateIncrement` в начале делает `const`-проверки (см. раздел `const`)** [CONST].

**`SaveRegToStack`/`RestoreRegFromStack`** — используют `ExpressionSaveDepth` (увеличивается на `Save`, уменьшается на `Restore`). Слот: `TotalLocalSize + ExpressionSaveDepth * 8`. Адрес через `ADDR_SCRATCH_REG_EN = r27`, `Ldi(slot) + Add(rSP)`.

**`StatementGenerator`** — `GenerateBlock`, `GenerateIf`, `GenerateWhile`, `GenerateFor`, `GenerateDoWhile`, `GenerateReturn`, `GenerateBreak`, `GenerateContinue`. Для циклов — `_loopStack` (`(breakLabel, continueLabel)`), `while` → `continue = startLabel`, `for` → `continue = continueLabel` (перед increment), `do-while` → `continue = continueLabel` (перед condition).
- **`GenerateAssignment` в самом начале вызывает `CheckConstTarget` — единая точка для `x = ...`, `x += ...`, `arr[i] = ...`, `*p = ...`** [CONST].

### Ассемблер

**`Assembler`** — прямая запись в `MemoryStream`/`BinaryWriter`:
- `EmitInstruction(uint)` — 4 байта.
- `EmitInstruction64(uint, ulong)` — 4 байта + `Align8()` + 8 байт.
- `EmitJump(uint, string)` — 4 байта + `Align8()` + placeholder, патч в `Build()`.
- `EmitInstruction64WithLabel(uint, string)` — как jump, но для не-jump опкодов (например, `LDI` с меткой).
- `EmitData(string, byte[])` — `Align8()`, `MarkLabel`, запись.
- `Build()` — патчит все `_patches` абсолютными адресами (`_baseAddress + targetPos`), возвращает `byte[]`.

**`ILAssembler`** — список `AsmItem`:
- `EmitInstruction64(uint, ulong)` проверяет `HasNeed64IntData(op)` — если нет, бросает `Asm_InvalidInstruction`.
- `EmitJump(uint, string)` → `AsmInstruction` с `TargetLabel`.
- `EmitData` добавляет пустой `AsmData(alignment: 8)` (для выравнивания перед меткой) + `MarkLabel` + `AsmData(data, alignment: 1)`.
- `Build()` — двухпроходный: сначала вычисляет адреса меток (`labelAddresses`), потом пишет байты, патча прыжки.

**`AssemblerParser`** — текстовый ассемблер. См. [Ассемблер NANASM: секции данных](#ассемблер-nanasm-секции-данных).

**`InstructionEncoder`** — кодирование 32-битного слова:
- `Encode(opcode, regDst, regSrc, sizeCode)` — R-формат.
- `EncodeI(opcode, reg, sizeCode)` — I-формат (`LDI`/`LOAD`/`STORE`).
- `EncodeU(opcode, reg)` — U-формат (`INC`/`DEC`/`NOT`/`PUSH`/`POP`/`JMP_IND`/`CALL_IND`).
- `EncodeJ(opcode)` — J-формат (всегда `S64`).
- `Decode(uint)` — обратное разложение для отладки.
- Extension-блоки на `OpCode` (`ToUint`/`OpName`), `RegType` (`ToUint`/`RegName`), `OpCodeSize` (`ToUint`/`SizeName`).

**`AsmExtensions`** — extension-блок на `AssemblerBase` с `Emit*`-методами. Кодогенератор работает только через эти методы.

### Оптимизации

**`AstOptimizer`** — `Add`/`Insert`/`Remove`/`RemoveAt`, `Count`, `GetType(i)`. `Optimize(program)` возвращает `IEnumerable<IReadOnlyLogOptimization>` — по одному на правило. Правил в этой партии нет.

**`PeepholeOptimizer`** — `Optimize(items, maxPasses = 5)` — цикл до неподвижной точки. Пропускает `AsmLabel`, применяет первое подходящее правило, `i = nextIndex`.

## Примеры для API

**Полный пайплайн: текст → байты:**

```csharp
using Nantuc.Compiler.C;
using Nantuc.Compiler.C.CodeGenerator;
using Nantuc.Compiler.C.Optimizators;
using Nantuc.Compiler.ASM.IR;
using Nantuc.Kernel.Common;
using Nantuc.Kernel.Diagnostics;

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

**`const` — что теперь компилируется:**

```csharp
const int LIMIT = 100;
int * const p = &a;              // p нельзя переприсвоить, *p можно менять
const int *q = &a;               // q можно переприсвоить, *q нельзя менять
const int * const r = &a;        // ни то, ни другое
const int arr[3] = { 1, 2, 3 };  // arr[i] нельзя менять
void f(const int x);             // x нельзя менять в теле
void g(const int *p);            // *p нельзя менять в теле

const int x = 5;
x = 6;              // ❌ CodeGen_AssignmentToConst (1316)
x++;                // ❌ CodeGen_IncrementOfConst  (1317)
x += 1;             // ❌ CodeGen_AssignmentToConst
arr[0] = 10;        // ❌ CodeGen_AssignmentToConst
*p = 10;            // ❌ CodeGen_AssignmentToConst (если p = const int*)
p = &b;             // ❌ CodeGen_AssignmentToConst (если p = int* const)
```

**Прямой ассемблер без AST:**

```csharp
var parser = new AssemblerParser();
byte[] bytecode = parser.Assemble("""
    LDI r0, 42
    LDI r1, 10
    ADD r0, r1
    END
    """, baseAddress: 0x0, throwCompiler);
```

**NANASM с секцией данных — полный пример:**

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

**NANASM с `.data` через explicit `allowDataSection`:**

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

**Inline-asm в MiniC — `.data` запрещён (по умолчанию):**

```csharp
// StatementGenerator.GenerateInlineAsm → AssemblerParser.Assemble(code, _asm, allowDataSection: false)
// Значит, в MiniC нельзя:
//   asm { .data msg: .asciz "x" }  → NotSupported
```

## Приближенность модуля к пользователю

Средняя. Пользователь MiniC работает с компилятором через `VmApplication` (например, `VmApplication.Compile(source)`), а не напрямую с `Nantuc.Compiler`. Но автор языка/расширений работает именно здесь.

- Автор нового синтаксиса — `Lexer`/`Parser`/`ASTNode`.
- Автор оптимизаций — `IAstOptimizationRule`/`IPeepholeRule`.
- Автор нового backend'а — `AssemblerBase` (реализовать `Build`, `EmitInstruction64` и т.д.).
- Автор нового target'а ISA — `InstructionEncoder` + `AsmExtensions`.
- Автор NANASM-скриптов — `AssemblerParser` с секциями `.data`/`.text`.

**Что стоит делать:**
- Использовать `ILAssembler` для production-пайплайна (двухпроходный, поддерживает peephole), `Assembler` — только для тестов/отладки.
- Пользовательские оптимизации — через `Add` в `AstOptimizer`/`PeepholeOptimizer`; правила должны быть идемпотентны (`Clear()` вызывается в начале каждого `Optimize`).
- Для inline-asm использовать `AsmLanguageDefinition.Mnemonics`/`Registers` как источник истины.
- Для статических данных в NANASM — `.data` + директивы (`.asciz`, `.zero`, `.quad`, …).
- **`const` — использовать как гарантию неизменяемости в API и внутренних данных** (см. раздел `const`).

**Что не стоит делать:**
- Полагаться на `FunctionNode.Body` у `extern`-функций — там `null!`, никто не проверяет.
- Полагаться на `RegisterAllocator` для больших функций (>22 одновременно живых скаляров) — жадная раскраска даст стек.
- Использовать `MiniCLanguageDefinition.GetKeywordRegex()` — dead code, может бросить на WASM AOT.
- Использовать `.data` в inline-asm — запрещено.
- **Использовать `const` на полях `struct` — не поддержано** (см. раздел `const`).

## Особенная информация

### WASM / кроссплатформенность

| Аспект | Статус | Комментарий |
|---|---|---|
| Сборка в WASM | ✅ | Нет `unsafe`, `DllImport`, `LibraryImport`. `stackalloc` только в `Assembler.Align8` (0–7 байт, WASM-safe). |
| `NativeMemory` | ✅ | Не используется. |
| `stackalloc` / `InlineArray` | ✅ | `stackalloc byte[pad]` в `Assembler.Align8` — bounded 0..7 байт, WASM-safe. |
| `Interlocked` / `Volatile` | ✅ | Не используются. |
| `System.Collections.Frozen` | ✅ | `FrozenSet` в `MiniCLanguageDefinition` и `AsmLanguageDefinition`. AOT-friendly. |
| `RegexOptions.Compiled` | ❌ | `MiniCLanguageDefinition._keywordRegex = new(GetKeywordPattern(), RegexOptions.Compiled);` — **бросает `PlatformNotSupportedException` на browser-wasm AOT**. Также regex — dead code. |
| `[GeneratedRegex]` | ✅ | `AssemblerParser.GetRexExtensionAsm()` — AOT-safe. |
| `Regex` без `Compiled` | ✅ | `AsmLanguageDefinition._mnemonicsRegex`/`regex` — `RegexOptions.IgnoreCase` без `Compiled`, работает в AOT. |
| `Enum.ToString()` | ⚠️ | **Убран из диагностики** — заменён на `TokenTypeExtension.AsText` и `OpCode.OpName()` (ручные switch) [FIX]. В остальных местах (`ErrorCode` как ключ) — не течёт. |
| `DiagnosticData` | ✅ | **Строковое представление аргумента строится при создании, никакого `string.Format(object[])` с боксингом, никакой рефлексии.** [NEW] |
| Trimming | ⚠️ | `using System.Security.Cryptography;` в `ASTNode.cs` — не используется, но может подтянуть crypto-сборки; trimming справится, но sloppy. |
| AOT | ⚠️ | Основной риск — `RegexOptions.Compiled`. Всё остальное (switch-паттерны, `readonly record struct DiagnosticData`, switch expressions) — AOT-friendly. |
| `switch` по `OpCode`/`TokenType` | ✅ | Компилируется в jump-table, AOT-friendly. |

**Итог:** компилятор собирается под WASM и AOT, **если убрать `RegexOptions.Compiled`**. В текущем виде `MiniCLanguageDefinition` — AOT-blocker на browser-wasm.

### Пайплайн компиляции

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
  ├── GlobalMemoryManager.Allocate(...) — с IsConst/IsPointedConst
  ├── GenerateGlobalInit (только скаляры)
  ├── EmitJmp("func_main")
  ├── foreach func in FunctionNodes:
  │     ├── CollectLocalVars(func.Body)
  │     ├── FunctionContext.Create(...) → VarMap + const-флаги
  │     ├── RegisterAllocator.AllocateRegisters(...) — &var → стек
  │     ├── пролог: push savedRegs, sub rSP, stackAlloc, params → registers
  │     ├── StatementGenerator.GenerateBlock(func.Body)
  │     └── эпилог: add rSP, pop savedRegs, ret (или END для main)
  └── EmitData для глобалов и pseudo-globals
  │
  ▼
AssemblerBase (Assembler | ILAssembler)
  │
  ▼
byte[] (32-битные слова ISA)
```

> **NANASM отдельным потоком:** `AssemblerParser.Assemble(...)` буферизует `.data`-секцию и вызывает `_asm.EmitData(...)` в конце.

### Константность (`const`) — семантика и ограничения **[NEW]**

`const` в MiniC — **compile-time гарантия неизменяемости** (не .rodata, не constant folding). Значение хранится обычным образом; запрет на запись диагностируется на этапе кодогенерации.

**Поддерживаемые формы**

| Декларация | Что запрещено | Поля `VariableNode` |
|---|---|---|
| `const int x` | `x = ...; x++; x += ...;` | `IsConst = true` |
| `int * const p` | `p = ...; p++; p += ...;` (можно `*p = ...`) | `IsConst = true`, `IsPointedConst = false` |
| `const int *p` | `*p = ...; (*p)++; p[i] = ...` (можно `p = ...`) | `IsConst = false`, `IsPointedConst = true` |
| `const int * const p` | и то, и другое | оба `true` |
| `const int arr[N]` | `arr[i] = ...; arr[i]++` | `IsPointedConst = true` (элементы), `IsConst = false` |
| `void f(const int x)` | `x = ...` в теле | `ParameterNode.IsConst = true` |
| `void f(const int *p)` | `*p = ...` в теле | `ParameterNode.IsPointedConst = true` |

**Синтаксис парсинга**

`Parser.ParseDeclSpec` → `(type, isPointer, pointedType, prefixConst, ptrConst)`:
1. `[const] type` — prefixConst;
2. `type const` — эквивалентно (prefixConst);
3. `*` — isPointer;
4. `[* const]` — ptrConst.

`ParseVariableTail` (скаляр):
```csharp
IsConst        = isPointer ? ptrConst : prefixConst,
IsPointedConst = isPointer && prefixConst
```

`ParseVariableTail` (массив):
```csharp
IsConst        = ptrConst,        // экзотика: int * const arr[]
IsPointedConst = prefixConst      // const int arr[] → элементы неизменяемы
```

`ParseFunction` (параметр) — та же формула, что и в скалярной ветке.

**Проверки в кодогенераторе**

- `StatementGenerator.CheckConstTarget(assign)` — вызывается **в начале** `GenerateAssignment` (до compound-диспетчеризации):
  - `assign.IndexExpr != null` → `IsPointedConst(name)`
  - `assign.LValue is DereferenceNode` → `IsPointedConst(ptrName)`
  - `assign.LValue is MemberAccessNode` → пока пропуск (см. ограничения)
  - простое имя → `IsConst(name)`
- `ExpressionGenerator.GenerateIncrement(node, targetReg)` — в начале:
  - `IdentifierNode` → `IsConst`
  - `ArrayAccessNode` → `IsPointedConst(name)`
  - `DereferenceNode` → `IsPointedConst(ptrName)`

**Новые `ErrorCode`**

```csharp
CodeGen_AssignmentToConst = 1316,   // "Cannot assign to const '{0}'"
CodeGen_IncrementOfConst  = 1317,   // "Cannot increment/decrement const '{0}'"
```

Оба шаблона зарегистрированы в `DefaultErrorMessageProvider` (`Nantuc.Kernel.Diagnostics`).

**Что НЕ поддерживается (осознанно)**

1. **`const` на полях `struct`** — потребует расширить `FieldInfo`/`StructLayout` и проверять `obj.field = ...` / `p->field = ...`. Сейчас `CheckConstTarget` для `MemberAccessNode` делает `return` (TODO-комментарий в коде).
2. **`const` на call-site** — `const int*` → `int*` не диагностируется (нет системы типов аргументов).
3. **Constant folding** — `const` не делает переменную compile-time константой. `for (int i = 0; i < N; i++)` с `const int N` не оптимизируется.
4. **`const` в `extern`-объявлениях** — блок `if (Current.Value == "extern")` в `Parser.Parse` пропускает только `*`, не `const`. При необходимости — симметрично.
5. **`&x` от `const x`** — даёт обычный `int*`; защита работает только если пользователь объявил `const int *p = &x` явно.

**Взаимодействие с регистрами**

`RegisterAllocator` **не исключает `const`-переменные из кандидатов в регистры** — это безопасно, потому что запись в них блокируется на уровне `StatementGenerator`/`ExpressionGenerator` (никакая инструкция `Store`/`Inc`/`Dec` до регистра `const`-переменной не доходит). Адрес `const`-переменной, к которой применён `&`, разрешён через `CollectAddressTakenVars` — тоже без проблем.

### Ассемблер NANASM: секции данных

`AssemblerParser` умеет разбирать секции `.data`/`.text` и директивы данных. Это нужно, чтобы NANASM-скрипты могли описывать статические данные (строки, массивы, буферы) без ручной генерации `.byte`.

**Директивы секций**

| Директива | Действие |
|---|---|
| `.data`, `.bss`, `.section .data`, `.section .bss` | открыть секцию данных |
| `.text`, `.code`, `.section .text`, `.section .code` | закрыть секцию данных (дальше — обычный код) |

**Директивы данных** (только внутри `.data`)

| Директива | Алиас | Ширина | Пример | Результат |
|---|---|---|---|---|
| `.byte` | `db` | 1 | `.byte 1, 2, 3` | `[01 02 03]` |
| `.byte` | — | 1 | `.byte "abc"` | `[61 62 63]` |
| `.word` | `dw` | 2 | `.word 0x1234, 0x5678` | `[34 12 78 56]` |
| `.dword` | `dd` | 4 | `.dword 0xDEADBEEF` | `[EF BE AD DE]` |
| `.quad` | `dq` | 8 | `.quad 0x1122334455667788` | 8 байт little-endian |
| `.ascii` | — | 1 | `.ascii "Hi"` | `[48 69]` |
| `.asciz` | `.string` | 1 (+NUL) | `.asciz "Hi"` | `[48 69 00]` |
| `.zero` | `.space` | N | `.zero 256` | 256 нулевых байт |

**Ограничения**

| Ограничение | Причина | Что делать |
|---|---|---|
| `.data` запрещён в inline-asm MiniC | `allowDataSection: false` по умолчанию | Вынесите `.data` в отдельный NANASM-файл |
| `.quad <label>` не поддерживается | Патч-механизм `EmitData` не умеет метки-операнды | `.quad 0` + инициализация в `__global_init` |
| `.align N` не поддержан | `EmitData` всегда выравнивает на 8 | Полагайтесь на авто-выравнивание 8 |
| `;` внутри строки обрезается | `Split(['\n', ';'])` | Не используйте `;` в `.ascii`/`.asciz` |
| Пока только little-endian | Платформа Nantuc | — |

### Диагностика: `DiagnosticData` и AOT **[NEW]**

Аргументы диагностики теперь передаются как `ReadOnlySpan<DiagnosticData>`, а не `ReadOnlySpan<object>`:

```csharp
public readonly record struct DiagnosticData
{
    public string Text { get; }
    // ...
}
```

Что это даёт для AOT:

- **Нет боксинга** `int`/`char`/enum при упаковке в `object`.
- **Нет виртуального `Object.ToString()`** на формат-времени — строка уже готова.
- **`Enum.ToString()` в диагностике убран** — все enum'ы конвертируются через `TokenTypeExtension.AsText` / `OpCode.OpName()` (ручные switch, ноль рефлексии).
- **`Type.Name` в диагностику не передаётся** — вызывающий передаёт готовую строку.

Остающиеся reflection-adjacent места в BCL (не в этом модуле):
- `ErrorCode` как ключ в `FrozenDictionary` — не течёт в `Enum.ToString()`.
- `DiagnosticData.Of<T>(T?)` — если `T` не переопределил `ToString()`, BCL может читать метаданные. Использовать только для известных типов.

**Итог:** `DiagnosticData` полностью AOT-safe для всех текущих вызовов `ThrowCompiler.Throw`.

### `Assembler` vs `ILAssembler`

| | `Assembler` | `ILAssembler` |
|---|---|---|
| Хранение | `MemoryStream` + `BinaryWriter` | `List<AsmItem>` |
| Патч меток | `_patches` в `Build()` | `labelAddresses` в первом проходе `Build()` |
| Peephole | ❌ | ✅ |
| Проверка `HasNeed64IntData` | ❌ | ✅ |
| Валидация дублей меток | ✅ | ✅ |
| Двухпроходный Build | ❌ | ✅ |
| `.data` через `AssemblerParser` | ✅ | ✅ |

## Что исправить

По убыванию важности. `[WASM]` — риски для browser-WASM/AOT; `[API]` — публичная поверхность; `[CONST]` — неполнота const-семантики; `[Дефект]` — поведение, не соответствующее ожиданиям/XML; `[Дом]` — организация кода; `[XML]` — документация.

### `const` — неполнота семантики **[CONST]** **[NEW]**

3. **[CONST] Expression-context нарушение `const` не диагностируется.**  
   Файл: `ExpressionGenerator.GenerateAssignmentExpr`.  
   ```csharp
   private void GenerateAssignmentExpr(AssignmentNode assign, RegType targetReg)
   {
       GenerateExpression(assign.Value, targetReg);
       _varAccessor.StoreFromRegister(assign.Name, targetReg);   // ← нет CheckConstTarget
   }
   ```
   `StatementGenerator.CheckConstTarget` вызывается **только** для statement-level `AssignmentNode`. Но `AssignmentNode` можно получить и в выражении: `(x = 5) + 1`, `(x += 1) * 2`. В этом случае:
   - `x` может быть `const` → проверка пропускается → запись произойдёт.
   - Для `MemberAccessNode`/`DereferenceNode` в `assign.Name == null!` → `StoreFromRegister` бросит `UndefinedVariable` (это уже пункт 4 ниже).
   
   **Фикс:** в `GenerateAssignmentExpr` либо делегировать в `StatementGenerator.GenerateAssignment`, либо продублировать `CheckConstTarget` (лучше первое — устранит и NRE для `MemberAccessNode`).

4. **[CONST] `CheckConstTarget` для `MemberAccessNode` — TODO.**  
   Файл: `StatementGenerator.CheckConstTarget`: `if (assign.LValue is MemberAccessNode) return;   // TODO`.  
   **Следствие:** `struct Point p; p.x = 5` не проверяется, даже если поле объявлено как `const int x` (что пока невозможно — `const` на полях структуры тоже не поддержан).  
   **Фикс:** расширить `FieldInfo` полями `IsConst`/`IsPointedConst`, пробросить через `StructLayout`, проверять в `CheckConstTarget`.

5. **[CONST] `const`-квалификатор в `extern`-объявлениях — не парсится.**  
   Файл: `Parser.Parse`, блок `if (Current.Value == "extern")`. Пропускает `*`, но не `const`.  
   **Фикс:** симметрично `ParseDeclSpec`.

### API / Дефекты

6. **[API] `RegisterAllocator` — комментарий «23 регистра» неверен.**  
   Файл: `RegisterAllocator.cs`, `GetAvailableRegisters()`: `return regs;   // r5..r27 — 23 регистра под переменные`. В `ReservedRegs` явно указан `RegType.r27`, то есть доступных — **22** (`r5..r26`).  
   Фикс: исправить комментарий.

7. **[API] `StatementGenerator.GenerateAssignment` — case 4 (direct assign) — dead code.**  
   Файл: `StatementGenerator.cs`, `case 4`: условие `assign.LValue == null`. Но `Parser.ParseAssignment` для `IdentifierNode` создаёт `new AssignmentNode(id.Name, right) { LValue = left, ... }` — `LValue` **всегда** установлен. Условие никогда не выполняется.  
   Фикс: убрать проверку `assign.LValue == null`; проверять `assign.LValue is IdentifierNode` или `assign.Name != null && assign.IndexExpr == null && assign.CompoundOp == null`.

8. **[API] `RegisterAllocator.BuildInterferenceGraph` — линейные live-диапазоны, не учитывают циклы.**  
   Файл: `RegisterAllocator.cs`. `CollectVarEvents` нумерует позиции в AST-обходе; переменная, определённая в цикле, но не используемая после, получит диапазон `[defPos, defPos]`.  
   Фикс: либо задокументировать как «best effort», либо реализовать реальный live-range analysis.

9. **[API] `FunctionGenerator.GenerateGlobalInit` — инициализация структур не поддерживается молча.**  
   Файл: `FunctionGenerator.cs`: `if (CodeGenUtils.IsStructType(global.Type, _structTable)) continue;`. Глобальная структура с `Initializer` (`Point origin = {0, 0};`) проигнорируется.  
   Фикс: поддержать или бросать `NotSupported`.

10. **[API] `AsmInstruction(uint, ulong, bool)` — fallback через `ToString()`.**  
    Файл: `AsmInstruction.cs`: `TargetLabel = absoluteTarget.ToString();`. Если `isAbsolute: false`, `Build()` не найдёт метку → `Asm_UndefinedLabel`.  
    Фикс: убрать fallback, всегда `Immediate = absoluteTarget`.

11. **[API] `ExpressionGenerator.GenerateBinaryOp` — `&&`/`||` в `targetReg` через `Mov`.**  
    Файл: `ExpressionGenerator.cs`: `GenerateLogicalAndOr(binop); if (targetReg != r0) EmitMov(targetReg, r0);`. Лишние инструкции для цепочек.  
    Фикс: `GenerateLogicalAndOr(binop, targetReg)`.

12. **[API] `AssemblerParser.TryAddIntegerData` — `.quad <label>` пока не поддержан.**  
    Фикс: механизм патча по метке в `EmitData`/`Build()`.

13. **[API] `AssemblerParser.TryAddStringData` — `;` внутри строки обрезается.**  
    Фикс: вынести разбор строк из общего `Split` или добавить hex-escape (`\x3B`).

### Дом

14. **[Дом] `Nantuc.Compiler.C.Optimizators` — namespace с опечаткой.**  
    Файлы: `AstOptimizer.cs`, `IAstOptimizationRule.cs`, `ILogOptimization.cs`. Должно быть `Optimizers` (или `Optimizations`). Публичная поверхность.  
    Фикс: переименовать (breaking change) или оставить с `<remarks>`.

15. **[Дом] `AsmLanguageDefinition` и `MiniCLanguageDefinition` — параллельные словари.**  
    Regex-хелперы в `MiniCLanguageDefinition` — dead code.  
    Фикс: вынести общее в один `LanguageDefinition`-модуль, dead code убрать.

16. **[Дом] `FieldInfo` — `record` с `Name`/`Type`/`Offset`/`Size`.**  
    `GetField` использует `List.Find` — O(n).  
    Фикс: `Dictionary<string, FieldInfo>` в `StructLayout`.

### XML

17. **[XML] AST-иерархия — без `///`.**  
    Включая **`VariableNode.IsConst`/`IsPointedConst` и `ParameterNode.IsConst`/`IsPointedConst`** — новые поля без документации.  
    Фикс: `///` на ключевые узлы, отметить nullable-поля.

18. **[XML] `ExpressionGenerator`, `StatementGenerator`, `FunctionGenerator` — без `///`.**  
    Особенно **`CheckConstTarget`** — единственная точка проверки `const` для assignment, и **`GenerateIncrement`** — для `++`/`--`.  
    Фикс: `<summary>` на публичных методах; для `CheckConstTarget` — `<remarks>` с перечислением проверяемых форм.

19. **[XML] `CodeGenUtils` — без `///`.**  
    `GetAlignment(type, structTable)` для структур возвращает жёстко `8` — не описано.  
    Фикс: `///` на каждый метод.

20. **[XML] `AssemblerParser` — шапка класса не перечисляет `.data` и не упоминает `allowDataSection`.**  
    Фикс: расширить `<summary>`/`<remarks>`.

### Что уже сделано (для истории)

- **`const`-квалификаторы** (`const int`, `int * const`, `const int *`, `const int * const`, `const int arr[]`, `const`-параметры, `sizeof(const T)`):
  - `Lexer`/`MiniCLanguageDefinition` — `const` в `Keywords`.
  - `Parser.ParseDeclSpec` — разбор `[const] type [* [const]]` → `(type, isPointer, pointedType, prefixConst, ptrConst)`.
  - `Parser.ParseVariableTail`/`ParseDeclarators`/`ParseFunction` — прокидывание флагов в `VariableNode`/`ParameterNode`.
  - `VariableNode`/`ParameterNode`/`VarLocation`/`GlobalInfo` — поля `IsConst`/`IsPointedConst`.
  - `FunctionContext.Create`/`FunctionGenerator.Generate`/`GlobalMemoryManager.Allocate` — проброс флагов.
  - `VariableAccessor.IsConst`/`IsPointedConst`.
  - `StatementGenerator.CheckConstTarget` — в начале `GenerateAssignment`.
  - `ExpressionGenerator.GenerateIncrement` — проверки для `Identifier`/`ArrayAccess`/`Dereference`.
  - `ErrorCode.CodeGen_AssignmentToConst = 1316`, `CodeGen_IncrementOfConst = 1317` + шаблоны в `DefaultErrorMessageProvider`.

- **`TokenTypeExtension.AsText`** — ручной switch вместо `Enum.ToString()`, AOT-safe, используется во всех сообщениях парсера.

- **`RegisterAllocator.CollectAddressTakenVars`** — переменные, чей адрес берётся (`&x`), исключаются из кандидатов на регистр до раскраски. Чинит `Cannot take address of register variable 'a'` для `&local`.

- **`Parser.ParseUnary`** — восстановлен блок `sizeof` (был потерян при добавлении `const`); добавлена поддержка `sizeof(const T)` и `sizeof(const T*)`.

- **`Parser.ParseVariableTail` (скаляр)** — исправлена формула: `IsConst = isPointer ? ptrConst : prefixConst` (было `IsConst = ptrConst`, из-за чего `const int x` не ловился).

- **`DiagnosticData`** — унификация аргументов диагностики через `ReadOnlySpan<DiagnosticData>` (без боксинга, без рефлексии, AOT-safe). Мерж `overrides` поверх стандартных шаблонов в `DefaultErrorMessageProvider`.

- `InstructionEncoder` — extension-блоки на `OpCode`/`RegType`/`OpCodeSize` + `AsmExtensions` на `AssemblerBase`.

- `ILAssembler` — двухпроходный `Build()` с предварительным вычислением `labelAddresses`, поддержка peephole.

- `AssemblerParser.GetRexExtensionAsm()` — `[GeneratedRegex]` (AOT-safe), нормализация `LOAD . S8` → `LOAD.S8`.

- `AssemblerParser` — секции `.data`/`.text` + директивы данных; данные буферизуются в `_pendingData` и сливаются через `EmitData` в конце.

- `StructLayout.Resolve` — итеративное разрешение зависимостей.