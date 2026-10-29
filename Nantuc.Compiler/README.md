﻿# Nantuc.Compiler — README

> Компилятор MiniC → байткод Nantuc: лексер, парсер, AST, кодогенератор с регистровой раскраской, ассемблер (два backend'а — прямой и IR) и инфраструктура оптимизаций.
> `[← Корень](../README.md) · [Common](../Nantuc.Kernel.Common/README.md) · [Contracts](../Nantuc.Kernel.Contracts/README.md) · [Kernel](../Nantuc.Kernel/README.md)`

## Содержание
- [Об этом модуле](#об-этом-модуле)
- [Дерево локального проекта](#дерево-локального-проекта)
- [API](#api)
- [Примеры для API](#примеры-для-api)
- [Приближенность модуля к пользователю](#приближенность-модуля-к-пользователю)
- [Особенная информация](#особенная-информация)
  - [Ассемблер NANASM: секции данных](#ассемблер-nanasm-секции-данных) **[NEW]**
- [Что исправить](#что-исправить)

## Об этом модуле

`Nantuc.Compiler` — это frontend и backend компилятора платформы Nantuc. Frontend принимает исходник на MiniC (подмножество C: целочисленные типы, `struct`, указатели, массивы, `sizeof`, `asm`-блоки), строит AST и генерирует байткод через двухступенчатую схему: кодогенератор → ассемблер → 32-битные слова ISA. Backend — двухуровневый: либо прямая запись в `MemoryStream` (`Assembler`), либо построение списка IR-инструкций (`ILAssembler`) с возможностью peephole-оптимизаций.

Модуль зависит от `Common` (`OpCode`/`RegType`/`OpCodeSize`/`InstructionDecoder`), `Contracts` (`OptimizationId`/`OptimizationLogEntry`/`IReadOnlyLogOptimization`) и `Diagnostics` (`ThrowCompiler`/`ErrorCode`). Сам по себе `unsafe`-кода не содержит — `stackalloc` только в `Assembler.Align8` (0–7 байт, WASM-safe).

Ключевые роли модуля:
- Frontend: `Lexer`, `Parser`, `ASTNode`-иерархия (28 типов), `MiniCLanguageDefinition`.
- Кодогенератор: `ExpressionGenerator`, `StatementGenerator`, `FunctionGenerator`, `RegisterAllocator` (жадная раскраска интерференционного графа), `FunctionContext`, `VariableAccessor`.
- Ассемблер: `AssemblerBase` (абстракция), `Assembler` (прямой), `ILAssembler` (IR), `AssemblerParser`, `InstructionEncoder`, `AsmExtensions`.
  **`AssemblerParser` поддерживает секции `.data`/`.text` и директивы данных** (`.byte`, `.word`, `.dword`, `.quad`, `.ascii`, `.asciz`, `.zero`) — см. [Ассемблер NANASM: секции данных](#ассемблер-nanasm-секции-данных). **[NEW]**
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
│   │
│   ├── CodeGenerator/                 # namespace Nantuc.Compiler.C.CodeGenerator
│   │   ├── CodeGenUtils.cs            # статические утилиты размеров/выравнивания
│   │   ├── ExpressionGenerator.cs     # генерация выражений
│   │   ├── FunctionContext.cs         # контекст функции + анализ глубины сохранений
│   │   ├── FunctionGenerator.cs       # пролог/эпилог, обход функций
│   │   ├── GlobalInfo.cs              # DTO для глобалов
│   │   ├── GlobalMemoryManager.cs     # реестр глобалов + метки
│   │   ├── RegisterAllocator.cs       # жадная раскраска графа
│   │   ├── StatementGenerator.cs      # генерация операторов
│   │   ├── VarLocation.cs             # VarLocation + RegisterAllocationResult
│   │   └── VariableAccessor.cs        # load/store переменных
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
    │                                   # + поддержка .data / .text (буферизация, EmitData в конце) [NEW]
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

## API

### Общая картина

| Тип | Namespace | Вид | Назначение |
|---|---|---|---|
| `ASTNode` + 28 наследников | `C` | `abstract class` / `class` | Иерархия AST: `ProgramNode`, `FunctionNode`, `BlockNode`, `VariableNode`, `AssignmentNode`, `BinaryOpNode`, `IfNode`, `WhileNode`, `ForNode`, `ReturnNode`, `FunctionCallNode`, `ArrayAccessNode`, `AddressOfNode`, `DereferenceNode`, `InlineAsmNode`, `StructDeclNode`, `MemberAccessNode`, `BreakNode`, `ContinueNode`, `DoWhileNode`, `IncrementNode`, `SizeofTypeNode`, `SizeofExprNode` |
| `Lexer` | `C` | `class` | Токенизация; `Token`, `TokenType` |
| `Parser` | `C` | `class` | Recursive descent → `ProgramNode` |
| `MiniCLanguageDefinition` | `C` | `static partial class` | `Keywords`/`Operators`/`Punctuation` (`FrozenSet`); regex-хелперы |
| `StructLayout` / `FieldInfo` | `C` | `class` / `record` | Раскладка структур, `Resolve` с итеративным разрешением |
| `CodeGenUtils` | `C.CodeGenerator` | `static class` | `GetSizeForType`/`GetSizeInBytes`/`GetAlignment`/`IsPrimitiveType`/`EmitMultiplyByConstant` |
| `ExpressionGenerator` | `C.CodeGenerator` | `class` | Генерация выражений (2 перегрузки `GenerateExpression`) |
| `StatementGenerator` | `C.CodeGenerator` | `class` | Генерация операторов (`GenerateBlock`, `GenerateIf`, `GenerateWhile`, `GenerateFor`, `GenerateDoWhile`, `GenerateReturn`, `GenerateBreak`, `GenerateContinue`) |
| `FunctionGenerator` | `C.CodeGenerator` | `class` | Точка входа: `Generate(ProgramNode)`, пролог/эпилог, `CollectLocalVars` |
| `FunctionContext` | `C.CodeGenerator` | `class` | `Create(FunctionNode, ...)` → `VarMap`, `TotalLocalSize`, `SaveAreaSize` (через `ComputeMaxBinaryDepth`) |
| `RegisterAllocator` | `C.CodeGenerator` | `static class` | `AllocateRegisters(...)` → `RegisterAllocationResult` (жадная раскраска) |
| `VariableAccessor` | `C.CodeGenerator` | `class` | `LoadToRegister`/`StoreFromRegister`/`LoadAddressToRegister`/`TryGetRegister` |
| `GlobalMemoryManager` | `C.CodeGenerator` | `class` | `Allocate`/`Contains`/`GetInfo`/`GetLabel` |
| `VarLocation` / `RegisterAllocationResult` | `C.CodeGenerator` | `class` | Метаданные переменной / результат раскраски |
| `AstOptimizer` | `C.Optimizators` | `class` | `Add`/`Insert`/`Remove`/`Optimize(ProgramNode)` → `IEnumerable<IReadOnlyLogOptimization>` |
| `IAstOptimizationRule` / `ILogOptimization` | `C.Optimizators` | `interface` | Контракт правила / контракт лога |
| `OptimizationResult` | `C.Optimizators` | `class : ILogOptimization` | Простая реализация лога |
| `AssemblerBase` | `ASM.Base` | `abstract class` | `EmitInstruction`/`EmitInstruction64`/`EmitJump`/`MarkLabel`/`EmitData`/`Build` |
| `Assembler` | `ASM` | `class : AssemblerBase` | Прямая запись; `_patches` для меток |
| `AssemblerParser` | `ASM` | `partial class` | Текст → байты; `Assemble(code, baseAddress, ThrowCompiler)`; секции `.data`/`.text` **[UPD]** |
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
- Функции и глобальные переменные через `ParseType`/`ParseDeclarators`.
- `asm { ... }` — inline asm, `InlineAsmNode` с текстом.
- Приоритеты операторов: `ParseAssignment` → `ParseLogicalOr` → `ParseLogicalAnd` → `ParseBitwiseOr` → `ParseBitwiseXor` → `ParseBitwiseAnd` → `ParseEquality` → `ParseRelational` → `ParseShift` → `ParseAdditive` → `ParseMultiplicative` → `ParseUnary` → `ParsePrimary`.
- Постфиксные `++`/`--`, `.`/`->`, `[i]`, `(args)` — в цикле `ParsePrimary`.

**AST-иерархия** — 28 классов. `AssignmentNode` содержит `LValue`, `CompoundOp` (`+=`, `<<=` и т.д.), `IndexExpr` (для `arr[i] = x`). `VariableNode` содержит `IsPointer`/`PointedType`/`IsArray`/`ArraySize`/`ArrayInitializer`. `FunctionNode` — `LocalSize`, `IsExternal`.

### Кодогенератор

**`FunctionGenerator.Generate(ProgramNode)`** — последовательность:
1. `GlobalMemoryManager` + `Allocate` для всех `GlobalVarNodes`.
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

`ComputeMaxBinaryDepth` — рекурсивный обход AST, оценивающий максимальную глубину одновременных сохранений. Для `BinaryOpNode` — `1 + max(depth(left), depth(right))`; для `If` — `max(1+depth(cond), max(depth(then), depth(else)))`; для `FunctionCallNode` — `min(argCount, 4) + max(depth(args))`.

**`RegisterAllocator.AllocateRegisters`** — жадная раскраска интерференционного графа:
- Резервирует `r0..r4`, `r27`, `rFL`, `rSP`, `rHP`, `rIP` — **22 доступных** (`r5..r26`; комментарий «23 регистра» — неверен, `r27` зарезервирован).
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

**`SaveRegToStack`/`RestoreRegFromStack`** — используют `ExpressionSaveDepth` (увеличивается на `Save`, уменьшается на `Restore`). Слот: `TotalLocalSize + ExpressionSaveDepth * 8`. Адрес через `ADDR_SCRATCH_REG_EN = r27`, `Ldi(slot) + Add(rSP)`.

**`StatementGenerator`** — `GenerateBlock`, `GenerateIf`, `GenerateWhile`, `GenerateFor`, `GenerateDoWhile`, `GenerateReturn`, `GenerateBreak`, `GenerateContinue`. Для циклов — `_loopStack` (`(breakLabel, continueLabel)`), `while` → `continue = startLabel`, `for` → `continue = continueLabel` (перед increment), `do-while` → `continue = continueLabel` (перед condition).

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

**`AssemblerParser`** — текстовый ассемблер. **[UPD]**
- Публичные точки входа:
  - `byte[] Assemble(string code, ulong baseAddress, ThrowCompiler)`
  - `byte[] Build(string code, AssemblerBase asm)`
  - `void Assemble(string code, AssemblerBase asm, bool allowDataSection = false)`
- `allowDataSection = false` по умолчанию — **`.data` запрещён** (используется из `StatementGenerator.GenerateInlineAsm`). Standalone-точки (`Assemble(code, baseAddress, …)`, `Build(code, asm)`) включают секции данных явно.
- `ProcessLines` — split по `\n`/`;`, обработка меток `label:`, `label: instr`, `label: .quad …`, комментарии `;`/`//`.
- Директивы секций: `.data`, `.bss`, `.section .data`, `.section .bss` → открыть; `.text`, `.code`, `.section .text`, `.section .code` → закрыть.
- Директивы данных (только в `.data`): `.byte`/`db`, `.word`/`dw`, `.dword`/`dd`, `.quad`/`dq`, `.ascii`, `.asciz`/`.string`, `.zero`/`.space`.
- **Буферизация**: данные копятся в `_pendingData` и сливаются в `_asm.EmitData(...)` в конце `Assemble(...)`. Это гарантирует, что `DataSectionStart`/`DataSectionSize` в `Build()` указывают на хвост потока — как если бы их вызвали из MiniC-кодогенератора.
- `ParseInstruction` — mnemonic, опциональный `.S8`/`.S16`/`.S32`/`.S64` через `GetRexExtensionAsm()` (нормализует `LOAD . S8` → `LOAD.S8`).
- Роутинг по `baseMnemonic` → `FormatR`/`FormatU`/`FormatLDI`/`FormatLOAD__STORE`/`FormateLOAD_IND__STORE_IND`/`FormatJumpType`.
- `_opMap`/`_regMap` — `FrozenDictionary` для O(1)-lookup.

**`InstructionEncoder`** — кодирование 32-битного слова:
- `Encode(opcode, regDst, regSrc, sizeCode)` — R-формат.
- `EncodeI(opcode, reg, sizeCode)` — I-формат (`LDI`/`LOAD`/`STORE`).
- `EncodeU(opcode, reg)` — U-формат (`INC`/`DEC`/`NOT`/`PUSH`/`POP`/`JMP_IND`/`CALL_IND`).
- `EncodeJ(opcode)` — J-формат (всегда `S64`).
- `Decode(uint)` — обратное разложение для отладки.
- Extension-блоки на `OpCode` (`ToUint`/`OpName`), `RegType` (`ToUint`/`RegName`), `OpCodeSize` (`ToUint`/`SizeName`).

**`AsmExtensions`** — extension-блок на `AssemblerBase` с `Emit*`-методами (`EmitMov`/`EmitAdd`/`EmitLdi`/`EmitJmp`/`EmitLoad`/`EmitStore`/`EmitCall`/`EmitEnd`/`EmitRet`/…). Кодогенератор работает только через эти методы.

### Оптимизации

**`AstOptimizer`** — `Add`/`Insert`/`Remove`/`RemoveAt`, `Count`, `GetType(i)`. `Optimize(program)` возвращает `IEnumerable<IReadOnlyLogOptimization>` — по одному на правило. Правил в этой партии нет.

**`PeepholeOptimizer`** — `Optimize(items, maxPasses = 5)` — цикл до неподвижной точки. Пропускает `AsmLabel`, применяет первое подходящее правило, `i = nextIndex` (правило решает, куда откатиться). Возвращает логи всех правил.

## Примеры для API

**Полный пайплайн: текст → байты:**

```csharp
using Nantuc.Compiler.C;
using Nantuc.Compiler.C.CodeGenerator;
using Nantuc.Compiler.C.Optimizators;
using Nantuc.Compiler.ASM;
using Nantuc.Compiler.ASM.IR;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Kernel.Common;
using Nantuc.Kernel.Diagnostics;

var throwCompiler = new ThrowCompiler();
string source = """
    int main() {
        int x = 42;
        return x;
    }
    """;

// 1. Lexer + Parser → AST
var tokens = new Lexer(source, throwCompiler).Tokenize();
var program = new Parser(tokens, throwCompiler).Parse();

// 2. AST-оптимизации (пустой список правил — no-op)
new AstOptimizer().Optimize(program);

// 3. Кодогенерация → AssemblerBase
var structTable = StructLayout.Resolve(program.StructNodes, throwCompiler);
var asm = new ILAssembler(baseAddress: 0x1000, throwCompiler);
new FunctionGenerator(asm, structTable, throwCompiler).Generate(program);

// 4. Peephole (пустой список правил — no-op)
var items = asm.GetItems();
new PeepholeOptimizer().Optimize(items);

// 5. Build → байты
byte[] bytecode = asm.Build();
```

**Прямой ассемблер без AST:**

```csharp
var throwCompiler = new ThrowCompiler();
var parser = new AssemblerParser();

byte[] bytecode = parser.Assemble("""
    LDI r0, 42
    LDI r1, 10
    ADD r0, r1
    END
    """, baseAddress: 0x0, throwCompiler);
```

**Прямой `Assembler` + extension-методы:**

```csharp
var asm = new Assembler(baseAddress: 0x0, throwCompiler);
asm.EmitLdi(RegType.r0, 42);
asm.EmitAdd(RegType.r0, RegType.r1);
asm.EmitJmp("loop");
asm.MarkLabel("loop");
asm.EmitEnd();
byte[] bytes = asm.Build();
```

**NANASM с секцией данных — полный пример:** **[NEW]**

```csharp
var throwCompiler = new ThrowCompiler();
var parser = new AssemblerParser();

byte[] bytecode = parser.Assemble("""
    .data
    msg:        .asciz "Hello, NANASM!\n"
    counter:    .quad 0
    table:      .byte 1, 2, 3, 4, 5
    big_buf:    .zero 64

    .text
    _start:
        LDI r0, msg          ; адрес msg → r0
        LOAD.S8 r1, msg      ; первый байт строки 'H' → r1
        LDI r2, 42
        STORE.S64 r2, counter ; записать 42 в counter
        LOAD.S64 r3, counter  ; r3 = 42
        ADD r3, r2            ; r3 = 84
        END
    """, baseAddress: 0x1000, throwCompiler);
```

**NANASM с `.data` через explicit `allowDataSection`:** **[NEW]**

```csharp
var asm = new Assembler(baseAddress: 0x0, throwCompiler);
var parser = new AssemblerParser();

// allowDataSection: true — .data разрешён
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

**Inline-asm в MiniC — `.data` запрещён (по умолчанию):** **[NEW]**

```csharp
// Внутри StatementGenerator.GenerateInlineAsm(...) вызывается:
asmParser.Assemble(asm.AsmCode, _asm);   // allowDataSection = false

// Значит, в MiniC нельзя:
//   asm {
//     .data
//     msg: .asciz "x"
//   }
// → выбросит NotSupported. Используйте глобальную MiniC-переменную или .data в отдельном NANASM-файле.
```

## Приближенность модуля к пользователю

Средняя. Пользователь MiniC работает с компилятором через `VmApplication` (например, `VmApplication.Compile(source)`), а не напрямую с `Nantuc.Compiler`. Но автор языка/расширений работает именно здесь.

- Автор нового синтаксиса — `Lexer`/`Parser`/`ASTNode`.
- Автор оптимизаций — `IAstOptimizationRule`/`IPeepholeRule`.
- Автор нового backend'а — `AssemblerBase` (реализовать `Build`, `EmitInstruction64` и т.д.).
- Автор нового target'а ISA — `InstructionEncoder` + `AsmExtensions`.
- **Автор NANASM-скриптов** — `AssemblerParser` с секциями `.data`/`.text` и директивами данных (см. [Ассемблер NANASM: секции данных](#ассемблер-nanasm-секции-данных)).

**Что стоит делать:**
- Использовать `ILAssembler` для production-пайплайна (двухпроходный, поддерживает peephole), `Assembler` — только для тестов/отладки.
- Пользовательские оптимизации — через `Add` в `AstOptimizer`/`PeepholeOptimizer`; правила должны быть идемпотентны (`Clear()` вызывается в начале каждого `Optimize`).
- Для inline-asm использовать `AsmLanguageDefinition.Mnemonics`/`Registers` как источник истины.
- Для статических данных в NANASM — `.data` + директивы (`.asciz`, `.zero`, `.quad`, …) вместо ручной генерации байтов через `.byte`.

**Что не стоит делать:**
- Полагаться на `FunctionNode.Body` у `extern`-функций — там `null!`, никто не проверяет.
- Полагаться на `RegisterAllocator` для больших функций (>22 одновременно живых скаляров) — жадная раскраска даст стек.
- Использовать `MiniCLanguageDefinition.GetKeywordRegex()` — dead code, может бросить на WASM AOT (см. [Что исправить](#что-исправить), п. 1).
- Использовать `.data` в inline-asm (`asm { }` в MiniC) — запрещено, выбросит `NotSupported`.

## Особенная информация

### WASM / кроссплатформенность

| Аспект | Статус | Комментарий |
|---|---|---|
| Сборка в WASM | ✅ | Нет `unsafe`, `DllImport`, `LibraryImport`. `stackalloc` только в `Assembler.Align8` (0–7 байт, WASM-safe). |
| `NativeMemory` | ✅ | Не используется. |
| `stackalloc` / `InlineArray` | ✅ | `stackalloc byte[pad]` в `Assembler.Align8` — bounded 0..7 байт, WASM-safe. `[InlineArray]` не используется. |
| `Interlocked` / `Volatile` | ✅ | Не используются. |
| `System.Collections.Frozen` | ✅ | `FrozenSet` в `MiniCLanguageDefinition.Keywords`/`Operators`/`Punctuation` и `AsmLanguageDefinition.Mnemonics`/`Registers`. AOT-friendly. |
| `RegexOptions.Compiled` | ❌ | `MiniCLanguageDefinition._keywordRegex = new(GetKeywordPattern(), RegexOptions.Compiled);` — **бросает `PlatformNotSupportedException` на browser-wasm AOT**. Также regex — dead code (Lexer не использует `GetKeywordRegex`). |
| `[GeneratedRegex]` | ✅ | `AssemblerParser.GetRexExtensionAsm()` — AOT-safe. |
| `Regex` без `Compiled` | ✅ | `AsmLanguageDefinition._mnemonicsRegex`/`regex` — `RegexOptions.IgnoreCase` без `Compiled`, работает в AOT (интерпретируется). |
| Trimming | ⚠️ | `using System.Security.Cryptography;` в `ASTNode.cs` — не используется, но может подтянуть crypto-сборки; trimming справится, но sloppy. |
| AOT | ⚠️ | Основной риск — `RegexOptions.Compiled`. Всё остальное (switch-паттерны, `record struct`, `switch` expressions) — AOT-friendly. |
| `switch` по `OpCode`/`TokenType` | ✅ | Компилируется в jump-table, AOT-friendly. |
| `MemoryStream`/`BinaryWriter` | ✅ | Managed, WASM-safe. |

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
  │
  ├── StructLayout.Resolve(program.StructNodes)
  │
  ▼
FunctionGenerator.Generate(program)
  ├── GlobalMemoryManager.Allocate(...)
  ├── GenerateGlobalInit (только скаляры)
  ├── EmitJmp("func_main")
  ├── foreach func in FunctionNodes:
  │     ├── CollectLocalVars(func.Body)
  │     ├── FunctionContext.Create(...) → VarMap, SaveAreaSize
  │     ├── RegisterAllocator.AllocateRegisters(...)
  │     ├── пролог: push savedRegs, sub rSP, stackAlloc, params → registers
  │     ├── StatementGenerator.GenerateBlock(func.Body)
  │     └── эпилог: add rSP, pop savedRegs, ret (или END для main)
  └── EmitData для глобалов и pseudo-globals
  │
  ▼
AssemblerBase (Assembler | ILAssembler)
  │
  ├── Assembler: MemoryStream + BinaryWriter, patch в Build()
  │
  └── ILAssembler: List<AsmItem>, двухпроходный Build()
        │
        ├── PeepholeOptimizer.Optimize(items) (опционально)
        │
        ▼
      byte[] (32-битные слова ISA)
```

> **NANASM отдельным потоком:** `AssemblerParser.Assemble(...)` буферизует `.data`-секцию и вызывает `_asm.EmitData(...)` в конце — то есть данные всегда попадают в хвост потока, как и в MiniC-пайплайне. `Build()` в обоих backend'ах выставит корректные `DataSectionStart`/`DataSectionSize`. См. [Ассемблер NANASM: секции данных](#ассемблер-nanasm-секции-данных).

### Соглашения кодогенератора

- **Регистры**: `r0` — return value / primary scratch; `r1`–`r4` — временные; `r5`–`r26` — переменные (по `RegisterAllocator`); `r27` — `ADDR_SCRATCH_REG_EN` (адресная арифметика); `rFL`/`rSP`/`rHP`/`rIP` — системные.
- **Соглашение о вызовах**: аргументы 0–3 — в `r0`–`r3`; аргументы 4+ — в pseudo-globals `__param_{func}_{param}`; возвращаемое значение — в `r0` (неявно, через `GenerateReturn` — но обработки возврата в `FunctionNode` нет, вызывающий должен сам взять из `r0`).
- **Стек**: `rSP` — указатель; save area сразу после local area: `[rSP, rSP + TotalLocalSize)` — локальные, `[rSP + TotalLocalSize, rSP + TotalLocalSize + SaveAreaSize)` — save slots. `SaveAreaSize = (ComputeMaxBinaryDepth + 1) * 8`.
- **Указательная арифметика**: `ptr + int` масштабируется на `sizeof(*ptr)` (`EmitMultiplyByConstant`); `ptr - ptr` — разница в байтах (без деления, не реализовано).

### Ассемблер NANASM: секции данных **[NEW]**

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
| `.byte` | — | 1 | `.byte "abc"` | `[61 62 63]` (без NUL) |
| `.word` | `dw` | 2 | `.word 0x1234, 0x5678` | `[34 12 78 56]` |
| `.dword` | `dd` | 4 | `.dword 0xDEADBEEF` | `[EF BE AD DE]` |
| `.quad` | `dq` | 8 | `.quad 0x1122334455667788` | 8 байт little-endian |
| `.ascii` | — | 1 (без NUL) | `.ascii "Hi"` | `[48 69]` |
| `.asciz` | `.string` | 1 (+NUL) | `.asciz "Hi"` | `[48 69 00]` |
| `.zero` | `.space` | N | `.zero 256` | 256 нулевых байт |

**Паттерны с метками**

```asm
.data
msg:        .asciz "Hello, NANASM!\n"   ; метка + директива на одной строке
counter:    .quad 0
table:      .byte 1, 2, 3, 4, 5
big_buf:    .zero 64
empty_str:  .ascii ""

.text
_start:
    LDI r0, msg              ; адрес msg → r0 (патчится в Build())
    LOAD.S8 r1, msg          ; первый байт строки
    LOAD.S64 r2, counter     ; 0
    STORE.S64 r3, counter    ; записать
    END
```

Допускаются и метка на отдельной строке, и `label: value` на одной — обе формы корректны. Метка без данных перед следующей меткой/директивой — ошибка (`Label 'x' has no data`).

**Как это работает под капотом**

1. `ProcessLines` при встрече `.data` выставляет `_inDataSection = true` и запрещает директивы кода.
2. Каждая директива данных кодирует байты и складывается в `_pendingData` как `(label, bytes)`.
3. `FlushPendingData()` в конце `Assemble(...)` вызывает `_asm.EmitData(label, bytes)` для каждой записи.
4. `Assembler.EmitData` делает `Align8()` → `MarkLabel` → запись; `ILAssembler.EmitData` — `AsmData(align:8) + AsmLabel + AsmData(data)`. Оба backend'а корректно выставляют `DataSectionStart`/`DataSectionSize` в `Build()`.
5. Патчи в `Build()` (`Assembler._patches`, `ILAssembler.labelAddresses`) разрешают метки из `.data` — `LDI r0, msg`, `STORE.S64 r2, counter` работают как обычно.

**Порядок сброса данных**

Парсер **всегда** сначала обрабатывает весь вход (буферизуя `.data`), затем сбрасывает буфер. Поэтому `.data` может стоять в любом месте файла — до, после или в середине `.text`. Физически данные всё равно окажутся в хвосте потока. Это совпадает с поведением `FunctionGenerator`, который вызывает `EmitData` в самом конце.

**Ограничения**

| Ограничение | Причина | Что делать |
|---|---|---|
| `.data` запрещён в inline-asm MiniC (`asm { }`) | `allowDataSection: false` по умолчанию | Используйте глобальную MiniC-переменную или вынесите `.data` в отдельный NANASM-файл |
| `.quad <label>` не поддерживается | Патч-механизм `EmitData` не умеет метки-операнды | Оставьте `.quad 0` и инициализируйте указатель кодом (`__global_init` в MiniC делает именно так) |
| `.align N` не поддержан | `EmitData` всегда выравнивает на 8 | Полагайтесь на авто-выравнивание 8 (для `.byte`/`.word` это over-padding, но корректно) |
| `;` внутри строки обрезается | Существующий `Split(['\n', ';'])` | Не используйте `;` в `.ascii`/`.asciz`; при необходимости — расширьте `TryAddStringData` hex-escape'ом |
| Пока только little-endian | Платформа Nantuc | — |

**Полный пример: MiniC → NANASM с общей `.data`**

В MiniC `.data` генерирует сам `FunctionGenerator` (через `EmitData` для глобалов и pseudo-globals). Отдельный NANASM-скрипт с `.data` нужен, когда вы пишете ассемблерный старт-ап или интегрируете Nantuc в чужой проект:

```asm
.data
stack_top:  .quad 0
vtable:     .quad 0, 0, 0, 0
banner:     .asciz "Nantuc // ready\n"
version:    .dword 0x00010000

.text
__start:
    LDI rSP, stack_top
    LDI r0, banner
    LOAD.S32 r1, version
    JZ r1, no_banner
    ; ... вывод
no_banner:
    END
```

```csharp
byte[] rom = new AssemblerParser().Assemble(source, baseAddress: 0x0, throwCompiler);
// rom[DataSectionStart .. DataSectionStart + DataSectionSize] — banner, vtable, version
```

### `Assembler` vs `ILAssembler`

| | `Assembler` | `ILAssembler` |
|---|---|---|
| Хранение | `MemoryStream` + `BinaryWriter` | `List<AsmItem>` |
| Патч меток | `_patches` в `Build()` | `labelAddresses` в первом проходе `Build()` |
| Peephole | ❌ | ✅ (оптимизатор работает с `List<AsmItem>`) |
| Проверка `HasNeed64IntData` | ❌ | ✅ (`EmitInstruction64` бросает) |
| Валидация дублей меток | ✅ (`MarkLabel` бросает) | ✅ (`MarkLabel` бросает) |
| Двухпроходный Build | ❌ (один проход + patch) | ✅ |
| `.data` через `AssemblerParser` **[UPD]** | ✅ `EmitData` пишет в хвост потока | ✅ `EmitData` → `AsmData` + `AsmLabel` |

### `PeepholeOptimizer` — порядок применения

```
for pass in 0..maxPasses-1:
    changed = false
    i = 0
    while i < items.Count:
        if items[i] is AsmLabel: i++; continue
        for rule in rules:
            if rule.TryOptimize(items, i, passCount, out newIndex):
                changed = true
                i = max(newIndex, 0)
                break
        if !applied: i++
    if !changed: break
```

`TryOptimize` возвращает `newIndex` — правило само решает, куда откатиться (например, `i - removedCount`, чтобы повторно проверить окрестность). `Clear()` у всех правил вызывается в начале `Optimize`.

## Что исправить

По убыванию важности. **[WASM]** — риски для browser-WASM/AOT; **[API]** — публичная поверхность; **[Дефект]** — поведение, не соответствующее ожиданиям/XML; **[Дом]** — организация кода; **[XML]** — документация.

### WASM / AOT

1. **[WASM] `MiniCLanguageDefinition._keywordRegex` — `RegexOptions.Compiled` + dead code.**  
   Файл: `MiniCLanguageDefinition.cs`, статический ctor: `_keywordRegex = new(GetKeywordPattern(), RegexOptions.Compiled);`. **`RegexOptions.Compiled` бросает `PlatformNotSupportedException` на browser-wasm AOT.** Плюс `GetKeywordRegex()` — публичный, но в предоставленном коде никем не используется (`Lexer` работает через `Keywords.Contains(value)`). То есть regex аллоцируется при первом обращении к `MiniCLanguageDefinition`, но не нужен.  
   Фикс: удалить `_keywordRegex` и `GetKeywordRegex()` (dead code); если нужен — заменить на `[GeneratedRegex]` с фиксированным паттерном.

2. **[WASM] `using System.Security.Cryptography;` в `ASTNode.cs` — не используется.**  
   Файл: `ASTNode.cs`, первая строка. Подтягивает crypto-сборки (trimming справится, но sloppy).  
   Фикс: удалить `using`.

### API

3. **[API] `RegisterAllocator` — комментарий «23 регистра» неверен.**  
   Файл: `RegisterAllocator.cs`, `GetAvailableRegisters()`: `return regs;   // r5..r27 — 23 регистра под переменные`. В `ReservedRegs` явно указан `RegType.r27`, то есть доступных — **22** (`r5..r26`).  
   Фикс: исправить комментарий на «r5..r26 — 22 регистра».

4. **[API] `ExpressionGenerator.GenerateAssignmentExpr` — NRE/null-name для member-access в expression-context.**  
   Файл: `ExpressionGenerator.cs`, `GenerateAssignmentExpr`: `_varAccessor.StoreFromRegister(assign.Name, targetReg)`. В `Parser.ParseAssignment` для `MemberAccessNode`/`DereferenceNode` создаётся `new AssignmentNode(null!, right) { LValue = left, ... }` — `Name == null`. При вызове `GenerateExpression(AssignmentNode, targetReg)` (вторая перегрузка) → `GenerateAssignmentExpr` → `StoreFromRegister(null!, ...)` → `ThrowCompiler.Throw(UndefinedVariable, null)`.  
   Проблема воспроизводится для `(p->x = 5) + 1` и подобных выражений.  
   Фикс: в `GenerateAssignmentExpr` делегировать в `GenerateAssignment` (statement-level), который обрабатывает `LValue`/`IndexExpr`/`CompoundOp`.

5. **[API] `StatementGenerator.GenerateAssignment` — case 4 (direct assign) — dead code.**  
   Файл: `StatementGenerator.cs`, `case 4`: условие `assign.LValue == null`. Но `Parser.ParseAssignment` для `IdentifierNode` создаёт `new AssignmentNode(id.Name, right) { LValue = left, CompoundOp = op }` — `LValue` **всегда** установлен. Условие никогда не выполняется.  
   Фикс: убрать проверку `assign.LValue == null`; либо проверять `assign.LValue is IdentifierNode`, либо использовать `assign.Name != null && assign.IndexExpr == null && assign.CompoundOp == null`.

6. **[API] `RegisterAllocator.BuildInterferenceGraph` — линейные live-диапазоны, не учитывают циклы.**  
   Файл: `RegisterAllocator.cs`. `CollectVarEvents` нумерует позиции в AST-обходе; переменная, определённая в цикле, но не используемая после, получит диапазон `[defPos, defPos]`, а не «всё тело цикла». Это может привести к некорректной раскраске в редких случаях (переменная в цикле и переменная после — оба могут получить один регистр, если их «живые диапазоны» не пересекаются в линейном обходе, но пересекаются в исполнении).  
   Фикс: либо задокументировать, что allocator — эвристика «best effort», либо реализовать реальный live-range analysis (с учётом циклов).

7. **[API] `FunctionGenerator.GenerateGlobalInit` — инициализация структур не поддерживается молча.**  
   Файл: `FunctionGenerator.cs`: `if (CodeGenUtils.IsStructType(global.Type, _structTable)) continue;`. Глобальная структура с `Initializer` (например, `Point origin = {0, 0};`) будет проигнорирована без ошибки.  
   Фикс: либо поддержать инициализацию структур (через `ArrayInitializer`-подобный механизм), либо бросать `NotSupported` с явным сообщением.

8. **[API] `AsmInstruction(uint, ulong, bool)` — fallback через `ToString()`.**  
   Файл: `AsmInstruction.cs`: `TargetLabel = absoluteTarget.ToString(); // некрасиво, но как fallback`. Если `isAbsolute: false` и `absoluteTarget` — не число (а на самом деле хеш/адрес), то `TargetLabel` будет строкой типа `"0x..."`. `Build()` потом ищет её в `labelAddresses` — не найдёт, бросит `Asm_UndefinedLabel`.  
   Фикс: убрать fallback, всегда `Immediate = absoluteTarget` (адрес уже абсолютный), либо перегрузку сделать явной.

9. **[API] `ExpressionGenerator.GenerateBinaryOp` — `&&`/`||` в `targetReg` через `Mov`.**  
   Файл: `ExpressionGenerator.cs`: `GenerateLogicalAndOr(binop); if (targetReg != r0) EmitMov(targetReg, r0);`. Результат всегда в `r0`, потом копируется. Для `a && b && c` — цепочка `Mov` между `r0` и `targetReg`. Не критично, но лишние инструкции.  
   Фикс: `GenerateLogicalAndOr(binop, targetReg)` — параметризовать целевой регистр.

10. **[API] `AssemblerParser.TryAddIntegerData` — `.quad <label>` пока не поддержан.** **[NEW]**  
    Файл: `AssemblerParser.cs`. Директивы `.byte/.word/.dword/.quad` принимают только числовые литералы (`TryParseLong`). Указатель на другую метку в `.data` (`ptr: .quad msg`) не поддержан.  
    Фикс: добавить в `AsmData`/`EmitData` механизм патча по метке (аналогично `_patches` для инструкций), либо принимать строковые аргументы и резолвить их в `Build()`.

11. **[API] `AssemblerParser.TryAddStringData` — `;` внутри строки обрезается.** **[NEW]**  
    Файл: `AssemblerParser.cs`. `ProcessLines` использует `Split(['\n', ';'])`, и `;` внутри `.asciz "a;b"` обрежет строку до `a`.  
    Фикс: вынести разбор строк из общего `Split` или добавить hex-escape (`\x3B`).

### Дом

12. **[Дом] `Nantuc.Compiler.C.Optimizators` — namespace с опечаткой.**  
    Файлы: `AstOptimizer.cs`, `IAstOptimizationRule.cs`, `ILogOptimization.cs` — `namespace Nantuc.Compiler.C.Optimizators`. Должно быть `Optimizers` (либо `Optimizations`). Публичная поверхность (в `using`).  
    Фикс: переименовать (breaking change) или оставить с `<remarks>` «legacy spelling».

13. **[Дом] `AsmLanguageDefinition` и `MiniCLanguageDefinition` — параллельные словари.**  
    Оба содержат `Registers`/`Mnemonics` (ASM) и `Keywords`/`Operators`/`Punctuation` (C), плюс regex-хелперы. Regex-хелперы в `MiniCLanguageDefinition` — dead code. В `AsmLanguageDefinition` — возможно, тоже.  
    Фикс: вынести общее в один `LanguageDefinition`-модуль, dead code убрать.

14. **[Дом] `FieldInfo` — `record` с `Name`/`Type`/`Offset`/`Size`.**  
    Файл: `StructLayout.cs`. Хорошо, но `GetField` использует `List.Find` — O(n) на каждый доступ; при большом количестве полей структуры это заметно.  
    Фикс: `Dictionary<string, FieldInfo>` в `StructLayout` (breaking change — `Fields` остаётся `IReadOnlyList`).

### XML

15. **[XML] AST-иерархия — без `///`.**  
    Файл: `ASTNode.cs`. 28 типов, только inline `//`-комментарии в шапке файла. Публичные свойства (`Name`, `ReturnType`, `Parameters`, `Body`, `Value`, `Condition`, ...) без документации. Для авторов оптимизаций/расширений это критично — непонятно, что гарантированно не-null, а что — nullable.  
    Фикс: `///` на `ASTNode` и ключевые узлы (`FunctionNode`, `VariableNode`, `AssignmentNode`, `BinaryOpNode`); отметить nullable-поля (`Initializer`, `ElseBlock`, `Init`/`Condition`/`Increment` в `ForNode`, `Value` в `ReturnNode`).

16. **[XML] `ExpressionGenerator`, `StatementGenerator`, `FunctionGenerator` — без `///`.**  
    Файлы в `CodeGenerator/`. Публичные методы (`GenerateExpression`, `GenerateBlock`, `Generate`, `SaveRegToStack`/`RestoreRegFromStack`/`ReleaseSaveSlot`, `EmitBinaryOperation`, `ComputeConstant`, `GenerateMemberAddress`, `GenerateCondition`, `ComputeArrayElementAddress`, `ResolveArrayStructType`, `TryInferStructTypeFromPointerExpr`) — без документации.  
    Фикс: минимум — `<summary>` на публичных методах; `<param>`/`<returns>` для `ComputeConstant`/`GenerateCondition`/`ComputeArrayElementAddress`.

17. **[XML] `CodeGenUtils` — без `///`.**  
    Файл: `CodeGenUtils.cs`. `GetSizeForType`, `GetSizeInBytes`, `GetAlignment` (две перегрузки), `IsStructType`, `IsPrimitiveType`, `GetTypeSize`, `EmitMultiplyByConstant` — без документации. `GetAlignment(type, structTable)` для структур возвращает жёстко `8` — не описано.  
    Фикс: `///` на каждый; отметить, что `GetAlignment(type, structTable)` для структур — 8 (максимум по полям, но не вычисляется точно).

18. **[XML] `AssemblerParser.Assemble` — без `///` на публичных методах.**  
    Файл: `AssemblerParser.cs`. Шапка класса — `///`-комментарий со списком инструкций, но `Assemble(string, ulong, ThrowCompiler)`, `Build(string, AssemblerBase)`, `Assemble(string, AssemblerBase, bool)` — без.  
    Фикс: `///` на три публичных метода, явно отметить `allowDataSection` и список поддерживаемых директив данных.

19. **[XML] `AssemblerParser` — шапка класса не перечисляет `.data`.** **[NEW]**  
    Файл: `AssemblerParser.cs`, `/// <summary>` у класса упоминает только инструкции (`NOP, END, RET, MOV, ...`). Директивы `.data`/`.text`/`.byte`/`.word`/`.dword`/`.quad`/`.ascii`/`.asciz`/`.zero` не указаны.  
    Фикс: расширить `<summary>`/`<remarks>`, перечислить директивы секций и данных, объяснить буферизацию (`_pendingData` → `FlushPendingData` в конце `Assemble`).

### Что уже сделано (для истории)

- `InstructionEncoder` — extension-блоки на `OpCode`/`RegType`/`OpCodeSize` + `AsmExtensions` на `AssemblerBase` — единый API эмиссии, кодогенератор не знает, какой backend используется.
- `ILAssembler` — двухпроходный `Build()` с предварительным вычислением `labelAddresses`, поддержка peephole.
- `AssemblerParser.GetRexExtensionAsm()` — `[GeneratedRegex]` (AOT-safe), нормализация `LOAD . S8` → `LOAD.S8`.
- **`AssemblerParser` — секции `.data`/`.text` + директивы данных** (`.byte`/`db`, `.word`/`dw`, `.dword`/`dd`, `.quad`/`dq`, `.ascii`, `.asciz`/`.string`, `.zero`/`.space`). Данные буферизуются в `_pendingData` и сливаются через `EmitData` в конце `Assemble(...)` — `DataSectionStart`/`DataSectionSize` корректны для обоих backend'ов. Inline-asm защищён `allowDataSection: false` по умолчанию. **[NEW]**
- `RegisterAllocator` — жадная раскраска по Welsh-Powell (степень вершины), с `CollectVarEvents` для live-диапазонов.
- `FunctionContext.ComputeMaxBinaryDepth` — рекурсивная оценка save area (`SaveAreaSize = (depth + 1) * 8`).
- `PeepholeOptimizer.Optimize` — до неподвижной точки с `maxPasses`.
- `StructLayout.Resolve` — итеративное разрешение зависимостей (queue + `lastResolved`), бросает `Parser_StructNotDefined` для неразрешимых.
```

---

## Краткая шпаргалка по NANASM `.data` (что стоит запомнить из изменений)

| Что | Как |
|---|---|
| Открыть данные | `.data` (или `.bss`, `.section .data`) |
| Вернуться в код | `.text` (или `.code`, `.section .text`) |
| Байт / слово / dword / quad | `.byte`/`.word`/`.dword`/`.quad` (алиасы `db`/`dw`/`dd`/`dq`) |
| Строка без NUL / с NUL | `.ascii "…"` / `.asciz "…"` (`string`) |
| Нулевой буфер | `.zero N` (`.space N`) |
| Метка + данные | `label: .asciz "…"` |
| Адрес метки из `.data` | `LDI r0, label` / `STORE.S64 r2, label` (патчится в `Build()`) |
| Порядок директив | любой: `.data` может быть до/после `.text` — данные всегда попадут в хвост |
| Inline-asm | `.data` запрещён (по умолчанию `allowDataSection: false`) |
| `.quad label` | не поддержан (только числа); для глобальных указателей используйте код в `__global_init` |
| `.align N` | не поддержан (авто-выравнивание на 8) |