# Nantuc.Compiler — README

> Обновление предыдущей версии: единственное изменение — переименование `IRAssembler` → `ILAssembler` (и файла `IRAssembler.cs` → `ILAssembler.cs`). Семантика та же: ассемблер через список `AsmItem` с двухпроходной сборкой и патчами меток. Имя теперь согласовано с `CompilationToILResult` / `CompileToIL` в `[Nantuc.VmApplication]`. Остальное — без изменений.  
> `[← Корень](../README.md) · [Common](../Nantuc.Kernel.Common/README.md) · [Contracts](../Nantuc.Kernel.Contracts/README.md) · [Diagnostics](../Nantuc.Kernel.Diagnostics/README.md) · [Kernel](../Nantuc.Kernel/README.md)`

## Содержание
- [Об этом модуле](#об-этом-модуле)
- [Дерево локального проекта](#дерево-локального-проекта)
- [API](#api)
- [Примеры для API](#примеры-для-api)
- [Приближенность модуля к пользователю](#приближенность-модуля-к-пользователю)
- [Особенная информация](#особенная-информация)
- [Что исправить](#что-исправить)

## Об этом модуле

`Nantuc.Compiler` — компилятор MiniC и ассемблер NANASM в одном проекте. Проект не зависит от `Nantuc.Kernel` (runtime), только от нижних слоёв: `Common` (ISA и размеры), `Contracts` (контракты оптимизаций) и `Diagnostics` (`ErrorCode`, `ThrowCompiler`, `CodeException`). Это позволяет использовать компилятор как самостоятельную библиотеку — например, в IDE, CI-сборке, REPL — без загрузки виртуальной машины и unsafe-кода ядра.

Модуль покрывает два независимых пути генерации байткода:

- MiniC → байткод: `Lexer` → `Parser` → AST → `FunctionGenerator` (+ `ExpressionGenerator`, `StatementGenerator`, `VariableAccessor`, `RegisterAllocator`, `GlobalMemoryManager`) → `AssemblerBase` → `byte[]`.
- NANASM (текст) → байткод: `AssemblerParser` → `AssemblerBase` (`Assembler` — прямой эмит, `ILAssembler` — через список `AsmItem` с последующей peephole-оптимизацией) → `byte[]`.

Ключевые роли:
- Frontend: `Lexer`, `MiniCLanguageDefinition`, `Parser`, иерархия `ASTNode`.
- Семантика: `StructLayout` (раскладка полей с выравниванием), `CodeGenUtils` (размеры/выравнивания типов, эмит умножения на константу).
- Backend: `FunctionGenerator` (главный вход), `RegisterAllocator` (жадная раскраска графа интерференции), `ExpressionGenerator`, `StatementGenerator`, `VariableAccessor`, `FunctionContext`, `GlobalMemoryManager`.
- ASM: `AssemblerBase` (абстракция), `Assembler` (прямой), `ILAssembler` (через `List<AsmItem>`), `AsmInstruction`/`AsmLabel`/`AsmData`, `InstructionEncoder` (кодирование 32-битной инструкции), `AssemblerParser` (текст → байткод).
- Оптимизации: `AstOptimizer` + `IAstOptimizationRule` (AST-уровень), `PeepholeOptimizer` + `IPeepholeRule` (IR-уровень). Логи оптимизаций реализуют `IReadOnlyLogOptimization` из `[Kernel.Contracts](../Nantuc.Kernel.Contracts/README.md)`.

## Дерево локального проекта

Namespace'ы соответствуют папкам:

```
Nantuc.Compiler/
├── Nantuc.Compiler.csproj                 # net10.0, ref → Common, Contracts, Diagnostics
├── C/                                      (namespace Nantuc.Compiler.C)
│   ├── ASTNode.cs                          # иерархия узлов AST
│   ├── Lexer.cs                            # лексер + TokenType/Token
│   ├── MiniCLanguageDefinition.cs          # Keywords, Operators, Punctuation, regex'ы
│   ├── Parser.cs                           # рекурсивный спуск, ProgramNode
│   ├── StructLayout.cs                     # раскладка структур + FieldInfo
│   ├── CodeGenerator/                      (namespace Nantuc.Compiler.C.CodeGenerator)
│   │   ├── CodeGenUtils.cs                 # размеры/выравнивания, EmitMultiplyByConstant
│   │   ├── ExpressionGenerator.cs          # генерация выражений в r0/targetReg
│   │   ├── FunctionContext.cs              # VarMap, TotalLocalSize, SaveAreaSize, ComputeMaxBinaryDepth
│   │   ├── FunctionGenerator.cs            # главный вход: ProgramNode → byte[]
│   │   ├── GlobalInfo.cs                   # struct GlobalInfo
│   │   ├── GlobalMemoryManager.cs          # реестр глобалов + GetLabel
│   │   ├── RegisterAllocator.cs            # жадная раскраска графа интерференции
│   │   ├── StatementGenerator.cs           # генерация инструкций/блоков/циклов
│   │   ├── VariableAccessor.cs             # Load/Store/LoadAddress/GetSize
│   │   └── VarLocation.cs                  # VarLocation + RegisterAllocationResult
│   └── Optimizators/                       (namespace Nantuc.Compiler.C.Optimizators)
│       ├── AstOptimizer.cs                 # список правил + OptimizationResult
│       ├── IAstOptimizationRule.cs         # контракт правила AST
│       └── ILogOptimization.cs             # расширение IReadOnlyLogOptimization
└── ASM/                                    (namespace Nantuc.Compiler.ASM)
    ├── Assembler.cs                        # прямой эмит в byte[]
    ├── AssemblerParser.cs                  # текстовый asm → byte[]/IL
    ├── Base/AssemblerBase.cs               # абстракция (Emit*, MarkLabel, Build)
    ├── IR/                                 (namespace Nantuc.Compiler.ASM.IR)
    │   ├── AsmInstruction.cs               # AsmItem/AsmLabel/AsmData/AsmInstruction
    │   ├── IPeepholeRule.cs                # контракт peephole-правила
    │   ├── ILAssembler.cs                  # AssemblerBase через List<AsmItem>
    │   └── PeepholeOptimizer.cs            # список правил + multi-pass
    └── Utils/InstructionEncoder.cs         # Encode*/Decode + AsmExtensions (Emit*)
```

Замечание: `VarLocation.cs` содержит два публичных типа — `VarLocation` и `RegisterAllocationResult`. Мелочь, но grep по `RegisterAllocationResult` ведёт в файл с другим именем.

## API

### Frontend

**`Lexer`** (namespace `Nantuc.Compiler.C`):

```csharp
public Lexer(string source);
public List<Token> Tokenize();
```

Возвращает список `Token` (`Type`, `Value`, `Line`, `Column`) с завершающим `TokenType.EOF`. При неожиданном символе бросает `CodeException` через `ThrowCompiler.Throw(ErrorCode.Lexer_UnexpectedCharacter, ...)`. Поддерживает `//`, `/* */`, идентификаторы, числа (десятичные, `0x`), строки, символы, операторы (включая `->`, `+=`, `-=`, `<<`, `>>` и т.д.).

**`Parser`** (namespace `Nantuc.Compiler.C`):

```csharp
public Parser(List<Token> tokens);
public ProgramNode Parse();
```

Рекурсивный спуск с классическими уровнями приоритетов: `ParseComma → ParseAssignment → ParseLogicalOr → ParseLogicalAnd → ParseBitwiseOr/Xor/And → ParseEquality → ParseRelational → ParseShift → ParseAdditive → ParseMultiplicative → ParseUnary → ParsePrimary`. Поддерживает `struct`, `extern`, `#include`, `if/else`, `while`, `for`, `do-while`, `break`, `continue`, `return`, `asm { ... }`, `sizeof(type)`, `sizeof(expr)`, `->`, `.`, префиксные/постфиксные `++/--`, указатели, массивы, вызовы функций.

**`ProgramNode`** — корень AST: `StructNodes`, `FunctionNodes`, `GlobalVarNodes`, `IncludesList`.

**ASTNode** — иерархия: `ProgramNode`, `FunctionNode`, `ParameterNode`, `BlockNode`, `VariableNode`, `AssignmentNode`, `BinaryOpNode`, `UnaryOpNode`, `NumberNode`, `IdentifierNode`, `IfNode`, `WhileNode`, `ForNode`, `ReturnNode`, `FunctionCallNode`, `ArrayAccessNode`, `AddressOfNode`, `DereferenceNode`, `InlineAsmNode`, `StructDeclNode`, `MemberAccessNode`, `BreakNode`, `ContinueNode`, `DoWhileNode`, `IncrementNode`, `SizeofTypeNode`, `SizeofExprNode`. Все — mutable-классы с сеттерами, кроме `BreakNode`/`ContinueNode` (маркерные).

### Семантика

**`StructLayout`** (namespace `Nantuc.Compiler.C`):

```csharp
public StructLayout(string name, List<(string type, string fieldName)> fieldDecls,
                    Dictionary<string, StructLayout> structTable);
public static Dictionary<string, StructLayout> Resolve(List<StructDeclNode> decls);
public FieldInfo? GetField(string name);
public record FieldInfo(string Name, string Type, int Offset, int Size);
```

`Resolve` — топологическая сортировка структур по зависимостям (цикл `do/while`, очередь неразрешённых). Поля выравниваются по `CodeGenUtils.GetAlignment`, размер — `AlignUp(offset + size, 8)`. Циклические ссылки между структурами не поддерживаются — бросает `CodeException(Parser_StructNotDefined, ...)`.

**`CodeGenUtils`** (namespace `Nantuc.Compiler.C.CodeGenerator`, `static`):

```csharp
public const RegType ADDR_SCRATCH_REG_EN = RegType.r27;
public static OpCodeSize GetSizeForType(string? type);
public static int GetSizeInBytes(OpCodeSize size);
public static int GetAlignment(string type);
public static int GetAlignment(string type, Dictionary<string, StructLayout> structTable);
public static int GetTypeSize(string type, Dictionary<string, StructLayout> structTable);
public static bool IsPrimitiveType(string type);
public static bool IsStructType(string type, Dictionary<string, StructLayout> structTable);
public static StructLayout GetStructLayout(string type, Dictionary<string, StructLayout> structTable);
public static bool IsComparisonOperator(string op);
public static void EmitMultiplyByConstant(this AssemblerBase asm, RegType reg, int multiplier);
```

Единственное место, где «зашит» маппинг `int/char/byte/ushort/...` → `OpCodeSize` и размеры. `r27` — глобальный scratch для временных адресов, зарезервирован за кодогенератором (нельзя использовать в раскраске регистров).

### Backend

**`FunctionGenerator`** (namespace `Nantuc.Compiler.C.CodeGenerator`) — главный вход бэкенда:

```csharp
public FunctionGenerator(AssemblerBase asm, Dictionary<string, StructLayout> structTable);
public void Generate(ProgramNode program);
```

Порядок секций в генерируемом байткоде: `__global_init` → `EmitJmp("func_main")` (если main есть) → функции → data-секция (глобалы и pseudo-globals для параметров ≥ 5-го). `AssemblerBase` можно заменить на `ILAssembler`, чтобы прогнать peephole перед `.Build()`.

**`ExpressionGenerator`** — генерация выражений. Два overload'а:

```csharp
public void GenerateExpression(ASTNode expr);                // → r0
public void GenerateExpression(ASTNode expr, RegType targetReg);  // → targetReg
```

Внутри — рекурсивный генератор с сохранением промежуточных значений через `SaveRegToStack`/`RestoreRegFromStack` (стек относительно `rSP`). Оптимизации: свёртка констант (`ComputeConstant`), выгрузка `x = x op y` без save-слота, масштабирование указателей (`ApplyPointerArithmeticScaling`).

**`StatementGenerator`** — блоки, `if/while/for/do-while`, `break/continue`, `return`, inline `asm { ... }`, compound assignment (`+=`, `<<=` и т.д.), локальные массивы.

**`RegisterAllocator`** (`static`):

```csharp
public static RegisterAllocationResult AllocateRegisters(
    FunctionNode func,
    Dictionary<string, VariableNode> localVars,
    Dictionary<string, StructLayout> structTable);
```

Жадная раскраска графа интерференции на диапазонах `[firstDef, lastUse]`. Зарезервированы: `r0..r4` (scratch/аргументы/возврат), `r27` (`ADDR_SCRATCH_REG_EN`), `rFL`, `rSP`, `rHP`, `rIP`. Под переменные идут `r5..r26` (22 регистра). Переменные, не поместившиеся в регистр (массивы, структуры, указатели, а также всё сверх 22), остаются в стеке.

**`VariableAccessor`** — единая точка доступа к переменной: регистр → стек → глобал, в этом порядке.

```csharp
public void LoadToRegister(string varName, RegType targetReg);
public void StoreFromRegister(string varName, RegType sourceReg);
public void LoadAddressToRegister(string varName, RegType targetReg);
public OpCodeSize GetVariableSize(string varName);
public bool TryGetRegister(string varName, out RegType reg);
```

`LoadAddressToRegister` бросает `CodeGen_CannotGetRegisterAddress`, если переменная в регистре (у регистра нет адреса).

**`FunctionContext`** — контекст текущей функции (`VarMap: Dictionary<string, VarLocation>`, `TotalLocalSize`, `SaveAreaSize`, `UsedRegisters`, `ExpressionSaveDepth`). Создаётся через `FunctionContext.Create(func, localVarNodes, structTable)`. `ComputeMaxBinaryDepth` рекурсивно считает пиковую глубину save-слота — по ней вычисляется `SaveAreaSize`.

### ASM

**`AssemblerBase`** (namespace `Nantuc.Compiler.ASM.Base`) — абстракция:

```csharp
public abstract class AssemblerBase(ulong baseAddress)
{
    public ulong BaseAddress { get; }
    public int CodeSectionSize { get; protected set; }
    public int DataSectionStart { get; protected set; }
    public int DataSectionSize { get; protected set; }
    public abstract byte[] Build();
    public abstract void EmitInstruction(uint instruction);
    public abstract void EmitInstruction64(uint instruction, ulong data);
    public abstract void EmitInstruction64WithLabel(uint instruction, string label);
    public abstract void EmitJump(uint jmpOpcode, string label);
    public abstract void EmitJumpToAbsolute(uint jmpOpcode, ulong absoluteTarget);
    public abstract void MarkLabel(string name);
    public abstract void EmitData(string label, byte[] data);
}
```

Расширения `Emit*` (`EmitLdi`, `EmitAdd`, `EmitLoadInd`, `EmitJz`, …) живут в `AsmExtensions` (`InstructionEncoder.cs`) и работают поверх любого `AssemblerBase` — это позволяет подменить прямой `Assembler` на `ILAssembler` без переписывания кодогенератора.

**`Assembler`** (namespace `Nantuc.Compiler.ASM`) — прямой эмит в `MemoryStream`. Метки собираются в `_labels`, патчи адресов — в `_patches` (разрешаются в `Build`). `EmitInstruction64` вставляет padding до 8-байтной границы перед операндом. В `Build` проставляет `CodeSectionSize`/`DataSectionStart`/`DataSectionSize`.

**`ILAssembler`** (namespace `Nantuc.Compiler.ASM.IR`) — то же API, но копит `List<AsmItem>` (`AsmInstruction`/`AsmLabel`/`AsmData`) и строит байты в `Build` двумя проходами (адреса меток → байты). Позволяет прогнать `PeepholeOptimizer` между эмитом и сборкой.

```csharp
public class ILAssembler(ulong baseAddress = 0) : AssemblerBase(baseAddress)
{
    public List<AsmItem> GetItems();
    public override byte[] Build();
    // ... все Emit*, MarkLabel
}
```

`Build` использует `InstructionEncoder.Encode(...)` для реконструкции `uint raw` из `OpCode`/`FirstReg`/`SecondReg`/`Size` — это ключевое отличие от `Assembler`, который пишет `uint` сразу при эмите.

**`AssemblerParser`** (namespace `Nantuc.Compiler.ASM`) — текстовый парсер:

```csharp
public byte[] Assemble(string code, ulong baseAddress = 0);   // создаёт Assembler
public byte[] Build(string code, AssemblerBase asm);          // использует переданный
public void Assemble(string code, AssemblerBase asm);         // просто эмитит
```

Поддерживает размерные суффиксы (`LOAD.S32`, `STORE.64` и т.д.), метки, `0x`-литералы. `AsmLanguageDefinition` (в том же файле) — FrozenSet'ы мнемоник и регистров для подсветки/автодополнения в IDE.

**`PeepholeOptimizer`** (namespace `Nantuc.Compiler.ASM.IR`):

```csharp
public void Add(IPeepholeRule rule);
public IEnumerable<IReadOnlyLogOptimization> Optimize(List<AsmItem> items, int maxPasses = 5);
```

Итерирует по `items`, для каждой инструкции пробует правила по порядку; правило возвращает `true` и `out newIndex`, если применилось. Повторяет до `maxPasses` или до отсутствия изменений.

**`InstructionEncoder`** (namespace `Nantuc.Compiler.ASM.Utils`, `static`) — кодировщик 32-битной инструкции в формате из `[Kernel.Common](../Nantuc.Kernel.Common/README.md)` (OpCode:8, Reg1:5, Reg2:5, SizeCode:2). Плюс `Decode(uint)` — человекочитаемый вид для отладки.

### Оптимизации AST

**`AstOptimizer`** (namespace `Nantuc.Compiler.C.Optimizators`):

```csharp
public void Add(IAstOptimizationRule rule);
public void Insert(int index, IAstOptimizationRule item);
public void Remove(IAstOptimizationRule item);
public void RemoveAt(int index);
public int Count { get; }
public OptimizationId GetType(int i);
public IEnumerable<IReadOnlyLogOptimization> Optimize(ProgramNode program);
```

**`IAstOptimizationRule`**:

```csharp
public OptimizationId Type { get; }
IReadOnlyLogOptimization Optimize(ProgramNode program);
```

**`OptimizationResult`** (в `AstOptimizer.cs`) — реализация `ILogOptimization : IReadOnlyLogOptimization` с методами `Append(pass, message)` и `Clear()`.

## Примеры для API

**Компиляция MiniC в байткод (без IR/оптимизаций):**

```csharp
using Nantuc.Compiler.C;
using Nantuc.Compiler.C.CodeGenerator;
using Nantuc.Compiler.ASM;

string source = File.ReadAllText("snake.minic");
var tokens = new Lexer(source).Tokenize();
var program = new Parser(tokens).Parse();

var structTable = StructLayout.Resolve(program.StructNodes);
var asm = new Assembler(baseAddress: 0);

new FunctionGenerator(asm, structTable).Generate(program);

byte[] bytecode = asm.Build();
File.WriteAllBytes("snake.nbin", bytecode);
```

**То же, но через `ILAssembler` + peephole:**

```csharp
using Nantuc.Compiler.ASM.IR;

var asm = new ILAssembler(baseAddress: 0);
new FunctionGenerator(asm, structTable).Generate(program);

var peephole = new PeepholeOptimizer();
peephole.Add(new MyFoldAddRule());
foreach (var log in peephole.Optimize(asm.GetItems()))
    foreach (var e in log.GetLogs())
        Console.WriteLine($"[{log.Id}] pass {e.Pass}: {e.Message}");

byte[] bytecode = asm.Build();
```

**Текстовый NANASM → байткод:**

```csharp
using Nantuc.Compiler.ASM;

string src = """
    start:
        LDI r0, 0x10
        LOAD.S64 r1, [r0]
        ADD r1, r1
        STORE.S64 [r0], r1
        END
    """;

byte[] bytes = new AssemblerParser().Assemble(src, baseAddress: 0);
```

**Своё AST-правило:**

```csharp
using Nantuc.Compiler.C;
using Nantuc.Compiler.C.Optimizators;
using Nantuc.Kernel.Contracts;

public sealed class FoldConstantRule : IAstOptimizationRule
{
    public OptimizationId Type => new OptimizationId("fold.constant");
    private readonly OptimizationResult _log = new(new OptimizationId("fold.constant"));

    public IReadOnlyLogOptimization Optimize(ProgramNode program)
    {
        _log.Clear();
        // обход program.FunctionNodes / GlobalVarNodes, замена BinaryOpNode(Number, Number)
        // на NumberNode и вызов _log.Append(pass, "...")
        return _log;
    }
}

var ast = new AstOptimizer();
ast.Add(new FoldConstantRule());
var logs = ast.Optimize(program).ToList();
```

## Приближенность модуля к пользователю

Средняя. Пользователь MiniC/NANASM напрямую этот модуль не импортирует — он пишет исходники. Модуль предназначен для:

- Автора компилятора/ассемблера: `Lexer`/`Parser` — для frontend'а, `FunctionGenerator` — для backend'а, `AssemblerParser` — для текстового asm. Ошибки — через `ThrowCompiler` + `ErrorCode`.
- Автора IDE/REPL/CI: `Lexer`/`Parser` для подсветки/валидации, `AssemblerParser` для быстрой сборки, `PeepholeOptimizer`/`AstOptimizer` для оптимизационных пайплайнов. `InstructionEncoder.Decode` — для дизассемблера/отладчика.
- Автора расширений к компилятору: `IAstOptimizationRule` / `IPeepholeRule` — единственный «официальный» способ вмешаться в генерацию без форка.

**Что стоит делать:**
- Использовать `ThrowCompiler.Throw(ErrorCode..., ...)` для ошибок компиляции — коды стабильны, сообщения локализуются через `IErrorMessageProvider`.
- Собирать `StructLayout.Resolve(program.StructNodes)` до `FunctionGenerator.Generate` — иначе кодогенератор упадёт на неизвестной структуре.
- Считать `TotalLocalSize + SaveAreaSize` (в `FunctionGenerator.GenerateFunction`) единственным источником истины по размеру стекового фрейма — не дублировать эту арифметику.
- Прогонять `PeepholeOptimizer` до `ILAssembler.Build` — после `Build` список уже скомпилирован в байты.
- Для своих правил `IAstOptimizationRule` возвращать `OptimizationResult`, а не `null` — `AstOptimizer` пробрасывает коллекцию наружу.

**Что не стоит делать:**
- Писать в `r27` (`ADDR_SCRATCH_REG_EN`) в своих правилах — этот регистр зарезервирован за кодогенератором.
- Использовать `RegisterAllocator.AllocateRegisters` отдельно, без `FunctionContext.Create` — `UsedRegisters`/`VarMap` должны быть согласованы.
- Ожидать, что `FunctionNode.Body` может быть `null` в `GenerateFunction` — external-функции пропускаются в `FunctionGenerator.Generate`, но если вызвать `GenerateFunction` руками на external, будет NRE.
- Менять `OpCode`/`RegType`/`OpCodeSize` без синхронизации с `[Kernel.Common](../Nantuc.Kernel.Common/README.md)` и спецификацией байткода — это wire-format.
- Полагаться на то, что `Lexer` вернёт токены в том же порядке, что и в исходнике после комментариев — комментарии пропускаются, `EOF` всегда в конце.

## Особенная информация

### Два ассемблера, один API

`Assembler` (прямой) и `ILAssembler` (через `List<AsmItem>`) реализуют один `AssemblerBase`, поэтому `AsmExtensions.Emit*` работают с обоими. Разница:

| | `Assembler` | `ILAssembler` |
|---|---|---|
| Хранение | `MemoryStream` + `BinaryWriter` | `List<AsmItem>` |
| Патчи меток | разрешаются в `Build` | разрешаются в `Build` (двухпроходный) |
| Peephole | недоступен | `PeepholeOptimizer.Optimize(asm.GetItems())` |
| Скорость | быстрее (прямая запись) | медленнее (список + 2 прохода) |
| Память | ниже | выше |
| Кодирование `uint raw` | в момент эмита | в `Build` (через `InstructionEncoder.Encode`) |

Для одноразовой сборки — `Assembler`. Для пайплайна «сборка → оптимизация → сборка» — `ILAssembler`.

### Формат инструкции и паддинг

32-битная инструкция (см. `[Kernel.Common](../Nantuc.Kernel.Common/README.md)`) + опциональный 8-байтный immediate. Правило выравнивания: `Assembler.EmitInstruction64` и `ILAssembler.Build` выравнивают поток до 8 байт перед записью 64-битного операнда. После 4-байтной инструкции с immediate может быть до 4 байт padding'а. `Processor.Step()` в `[Kernel](../Nantuc.Kernel/README.md)` учитывает это через `Get64IntData` (`AlignUp(ip, 8)`). Любой сторонний backend обязан соблюдать это правило — иначе байткод развалится на immediate-инструкциях.

### Стек и save-слоты

`ExpressionGenerator.SaveRegToStack` использует `_funcCtx.TotalLocalSize + _funcCtx.ExpressionSaveDepth * 8` — save-слоты начинаются после локальных переменных. `FunctionContext.SaveAreaSize = (ComputeMaxBinaryDepth + 1) * 8` — резерв на пиковую глубину вложенности выражений. В прологе `FunctionGenerator.GenerateFunction` вычитает `TotalLocalSize + SaveAreaSize` из `rSP`; в эпилоге возвращает обратно. `ComputeMaxBinaryDepth` — рекурсивный расчёт по AST; если вы добавите новый тип узла, обязательно добавьте его в `ComputeMaxBinaryDepth`, иначе save-слот переполнится и затрёт локальные переменные.

### Регистры: резервирование и раскраска

`RegisterAllocator.ReservedRegs`: `r0..r4` (scratch/return/args), `r27` (`ADDR_SCRATCH_REG_EN`), `rFL`, `rSP`, `rHP`, `rIP`. Под переменные — `r5..r26` (22 регистра). Если функция использует больше 22 одновременно живых скаляров, часть уйдёт в стек. Указатели, массивы и структуры всегда в стеке.

### Вызов функций: 4 регистра + pseudo-globals

Первые 4 аргумента передаются в `r0..r3`, остальные — через pseudo-globals `__param_{func}_{name}`. `FunctionGenerator.Generate` эмитит под них `AsmData` в data-секции (по 8 байт), `ExpressionGenerator.GenerateFunctionCall` пишет `EmitStore(r0, opSize, label)`, `FunctionGenerator.GenerateFunction` восстанавливает `EmitLoad(loc.Register, size, label)`. Ограничение: параметр ≥ 5 обязан быть в регистре (иначе `CodeGen_ParameterNotInRegister`). Структуры как параметры через регистры не поддерживаются.

### Inline `asm { ... }`

`Parser.ParseInlineAsm` собирает токены в строку, `StatementGenerator.GenerateInlineAsm` создаёт новый `AssemblerParser` и эмитит в текущий `AssemblerBase`. Inline asm может содержать любые метки и инструкции, но его метки попадают в общее пространство имён с остальным байткодом (риск коллизии; используйте префикс `__asm_`).

### Ограничения платформ

- `net10.0`, `<AllowUnsafeBlocks>` не включён — модуль безопасен для WASM/AOT без `unsafe`.
- `params ReadOnlySpan<object>` в `ThrowCompiler` требует `net9.0+`; проект на `net10.0`.
- `extension`-блоки (в `InstructionEncoder`), `FrozenDictionary`/`FrozenSet`, `[GeneratedRegex]` — всё это доступно на `net10.0`. Старые тулчейны не соберут.
- `Enum.GetValues<ErrorCode>()` в `ThrowCompiler` — при `PublishTrimmed=true` может понадобиться `[DynamicDependency]` на `ErrorCode`. Проверьте AOT-публикацию.
- `BinaryWriter`/`MemoryStream` в `Assembler` — работают в AOT, но `Assembler.Build` не идемпотентен: после вызова `_stream` и `_writer` закрыты, повторный `Build` упадёт.
- `AssemblerParser` использует `Regex` (`GetRexExtensionAsm`) — в AOT это работает через source generator (`[GeneratedRegex]`), но проверьте, что `AsmLanguageDefinition` тоже не стримится, если используете его в IDE-плагине.

## Что исправить

Ниже — конкретные проблемы с привязкой к файлам и методам. Порядок — по убыванию серьёзности. Все пункты из прошлой фиксации актуальны; переименование `IRAssembler` → `ILAssembler` на них не влияет.

1. **`FunctionGenerator.GenerateFunction` — push и pop регистров в разном порядке.**  
   Файл: `C/CodeGenerator/FunctionGenerator.cs`, метод `GenerateFunction`.  
   В прологе: `foreach (var reg in funcCtx.UsedRegisters.OrderBy(r => (int)r)) _asm.EmitPush(reg);` — push в отсортированном порядке. В эпилоге: `for (int i = funcCtx.UsedRegisters.Count - 1; i >= 0; i--) _asm.EmitPop(funcCtx.UsedRegisters[i]);` — pop в порядке, обратном неотсортированному `UsedRegisters`. Если `UsedRegisters` не отсортирован, pop восстановит регистры не в те, из которых push'ил.  
   Фикс: либо `var sorted = funcCtx.UsedRegisters.OrderBy(r => (int)r).ToList();` и использовать `sorted` в обоих местах, либо push тоже в порядке `UsedRegisters` без сортировки.

2. **`Lexer.ReadNumber` — `A-F` съедаются без `isHex`.**  
   Файл: `C/Lexer.cs`, метод `ReadNumber`, условие `if (char.IsDigit(c) || (isHex && (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))`.  
   Из-за приоритета `&&`/`||` это `IsDigit(c) || (isHex && a-f) || (A-F)` — `A-F` принимаются всегда. `int x = 10A` съест `A` как часть числа, потом `long.Parse` упадёт.  
   Фикс: `if (char.IsDigit(c) || (isHex && ((c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))))`.

3. **`Parser.IsTypeSpecifier` не признаёт `short`, `long`, `uint`.**  
   Файл: `C/Parser.cs`, метод `IsTypeSpecifier`.  
   Проверяет только `"int"`, `"char"`, `"void"`, `"byte"`, `"ushort"`, `"ulong"`. Но `MiniCLanguageDefinition.Keywords` содержит `"short"`, `"long"`, `"uint"` — они классифицируются лексером как `Keyword`, но `IsTypeSpecifier` возвращает `false`, и парсер падает с `Parser_UnexpectedToken`.  
   Фикс: добавить `"short"`, `"long"`, `"uint"` в проверку.

4. **`Parser.Expect` — неверные аргументы для шаблона `Parser_ExpectedToken`.**  
   Файл: `C/Parser.cs`, метод `Expect`.  
   Вызов: `ThrowCompiler.Throw(ErrorCode.Parser_ExpectedToken, type, Current.Type, Current.Value, Current.Column)`. Шаблон: `"Expected {0}, got {1} at {2}:{3}"`. В `{2}` попадает `Current.Value` (string), в `{3}` — `Current.Column` (int). Получается `Expected Keyword, got Identifier at foo:5`, хотя должно быть `line:column`.  
   Фикс: передавать `Current.Line, Current.Column` (но тогда в `{1}` попадёт `Type`, а `Value` — в `{2}`; либо менять шаблон).

5. **`Parser.ParseDeclarators` не поддерживает массивы в multi-declaration.**  
   Файл: `C/Parser.cs`, метод `ParseDeclarators`.  
   Рекурсивный цикл после запятой обрабатывает только `*` (указатель), но не `[N]` (массив). `int a, b[10];` — `b` парсится как scalar без `ArraySize`.  
   Фикс: в цикле после `Expect Identifier` вызывать `ParseVariableTail(...)` (он уже обрабатывает `[...]`), а не собирать `VariableNode` вручную.

6. **`InstructionEncoder.Decode` — опечатка в имени опкода.**  
   Файл: `ASM/Utils/InstructionEncoder.cs`, метод `Decode`, ветка `OpCode.JMP_IND => "JMP_INT"`.  
   Должно быть `"JMP_IND"`. Мелочь, но дизассемблер/отладчик будет показывать неверную мнемонику.

7. **`ExpressionGenerator.LoadPointerElementToR0` / `StorePointerElement` — «expection type» вместо явной ошибки.**  
   Файл: `C/CodeGenerator/ExpressionGenerator.cs`, методы `LoadPointerElementToR0` и `StorePointerElement`.  
   В fallback-ветке `pointedSize = CodeGenUtils.GetSizeForType("expection type")` — это опечатка (`exception`), и она полагается на то, что `GetSizeForType` бросит `CodeGen_UnknownTypeSize`. Работает, но сообщение вводит в заблуждение.  
   Фикс: явный `ThrowCompiler.Throw<OpCodeSize>(ErrorCode.CodeGen_UnknownPointedType)`.

8. **`ExpressionGenerator.GenerateAssignmentExpr` — `assign.Name` может быть `null`.**  
   Файл: `C/CodeGenerator/ExpressionGenerator.cs`, метод `GenerateAssignmentExpr`.  
   `Parser.ParseAssignment` создаёт `AssignmentNode(null!, right)` для `MemberAccessNode`/`DereferenceNode`. `GenerateAssignmentExpr` вызывает `_varAccessor.StoreFromRegister(assign.Name, targetReg)` — `StoreFromRegister` бросит `CodeGen_UndefinedVariable` с `null`, либо NRE. В `StatementGenerator.GenerateAssignment` этот путь не достигается (Member/Deref обрабатываются раньше), но `AssignmentNode` как выражение идёт через `GenerateExpression(assign, targetReg)` → `GenerateAssignmentExpr` → падение.  
   Фикс: в `GenerateAssignmentExpr` для `assign.LValue is MemberAccessNode`/`DereferenceNode` вызывать соответствующие `GenerateMemberAddress`/`GenerateDereference` + `EmitStoreInd`, аналогично `StatementGenerator.GenerateAssignment`.

9. **`RegisterAllocator.GetAvailableRegisters` — комментарий врёт про «23 регистра».**  
   Файл: `C/CodeGenerator/RegisterAllocator.cs`, метод `GetAvailableRegisters`, комментарий `// r5..r27 — 23 регистра`.  
   `r27` зарезервирован (`ReservedRegs`), значит доступны `r5..r26` = 22 регистра.  
   Фикс: поправить комментарий и документацию.

10. **`AsmExtensions.EmitStore` — параметр назван `dst`, но это source.**  
    Файл: `ASM/Utils/InstructionEncoder.cs`, `extension(AssemblerBase asm)`, метод `EmitStore(RegType dst, OpCodeSize size, string label)` и перегрузка с `ulong adrr`.  
    `STORE [label], reg` — reg это source, а не destination. Работает корректно (внутри `EncodeSTORE(dst.ToUint(), ...)`), но имя сбивает с толку. Плюс опечатка `adrr` (должно быть `addr`).  
    Фикс: переименовать параметр в `src`/`sourceReg`.

11. **`VarLocation.cs` содержит `RegisterAllocationResult`.**  
    Файл: `C/CodeGenerator/VarLocation.cs`.  
    Два публичных типа в одном файле — `VarLocation` и `RegisterAllocationResult`. Мелочь для навигации.  
    Фикс: вынести `RegisterAllocationResult` в отдельный файл или переименовать файл в `RegisterAllocation.cs`.

12. **`Parser.ParsePrimary` — приоритет `&&`/`||` без скобок.**  
    Файл: `C/Parser.cs`, метод `ParsePrimary`, условие `if (Current.Type == TokenType.Punctuation && Current.Value == "." || Current.Type == TokenType.Arrow)`.  
    Функционально правильно (`(A && B) || C`), но нечитаемо.  
    Фикс: `if (Current.Type == TokenType.Arrow || (Current.Type == TokenType.Punctuation && Current.Value == "."))`.

13. **`FunctionGenerator.GenerateFunction` — для `main` не восстанавливаются параметры.**  
    Файл: `C/CodeGenerator/FunctionGenerator.cs`, метод `GenerateFunction`, блок `if (!isMain) { for (int i = 0; ...) ... }`.  
    Для `main` параметры не копируются из регистров/pseudo-globals в `VarMap`. Если `main` объявлен с параметрами, они не будут доступны в теле.  
    Фикс: либо задокументировать ограничение, либо убрать `if (!isMain)` для восстановления параметров.