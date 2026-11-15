using Nantuc.Common;
using Nantuc.Compiler.ASM.Base;
using Nantuc.Compiler.ASM.Utils;
using Nantuc.Compiler.C.AST;
using Nantuc.Compiler.C.CodeGenerator.Expression;
using Nantuc.Compiler.C.CodeGenerator.FunctionCtx;
using Nantuc.Compiler.C.CodeGenerator.Statement;
using Nantuc.Diagnostics;

namespace Nantuc.Compiler.C.CodeGenerator.FunctionGen;

// ============================================================
// FunctionGenerator — генерация программы и функций.
//
// Partial-файлы:
//   - FunctionGenerator.cs            — оркестратор (этот файл)
//   - FunctionGenerator.DataSection.cs — эмиссия секции данных
//   - FunctionGenerator.Prologue.cs    — пролог + маршалинг параметров
//   - FunctionGenerator.Epilogue.cs    — эпилог
//
// Вынесены отдельные классы:
//   - LocalVarCollector    — обход AST для сбора локальных переменных
//   - ConstantEvaluator    — свёртка констант в инициализаторах
// ============================================================
public partial class FunctionGenerator(
    AssemblerBase asm,
    Dictionary<string, StructLayout> structTable,
    ErrorReporter errorReporter)
{
    private readonly AssemblerBase _asm = asm;
    private readonly Dictionary<string, StructLayout> _structTable = structTable;
    private readonly ErrorReporter _erRep = errorReporter;
    private readonly Dictionary<string, FunctionNode> _functionTable = [];

    private GlobalMemoryManager _globalMem = null!;
    private int _labelCounter;

    public void Generate(ProgramNode program)
    {
        _globalMem = new GlobalMemoryManager();

        // 1) Секция глобальных переменных
        foreach (var g in program.GlobalVarNodes)
            _globalMem.Allocate(g.Name, g.Type, g.IsArray, g.IsPointer, g.PointedType,
                                g.ArraySize, g.IsConst, g.IsPointedConst);

        // 2) Таблица функций
        _functionTable.Clear();
        foreach (var f in program.FunctionNodes)
            _functionTable[f.Name] = f;

        // 3) Генерация кода
        _asm.MarkLabel("__global_init");
        GenerateGlobalInit(program);

        if (program.FunctionNodes.Any(f => f.Name == "main"))
            _asm.EmitJmp("func_main");

        foreach (var f in program.FunctionNodes)
            if (!f.IsExternal) GenerateFunction(f);

        // 4) Секция данных
        EmitDataSection(program);
    }

    private void GenerateFunction(FunctionNode func)
    {
        _asm.MarkLabel($"func_{func.Name}");

        // Контекст
        var localVarNodes = LocalVarCollector.Collect(func.Body);
        var funcCtx = FunctionContext.Create(func, localVarNodes, _structTable, _erRep);

        // Генераторы
        var varAccessor = new VariableAccessor(_asm, funcCtx, _globalMem);
        var exprGen = new ExpressionGenerator(_asm, _globalMem, funcCtx, _functionTable,
                                              GetLabel, _structTable, varAccessor);
        var stmtGen = new StatementGenerator(_asm, exprGen, funcCtx, GetLabel,
                                             _globalMem, _structTable, varAccessor);

        // Пролог / тело / эпилог
        EmitPrologue(funcCtx, func);
        stmtGen.GenerateBlock(func.Body);
        EmitEpilogue(funcCtx, func);
    }

    private void GenerateGlobalInit(ProgramNode program)
    {
        // Временный контекст: global initializers не должны обращаться к локальным.
        var dummyCtx = new FunctionContext();
        var varAccessor = new VariableAccessor(_asm, dummyCtx, _globalMem);
        var exprGen = new ExpressionGenerator(_asm, _globalMem, dummyCtx, _functionTable,
                                              GetLabel, _structTable, varAccessor);

        foreach (var g in program.GlobalVarNodes)
        {
            if (g.ArrayInitializer != null && g.IsPointer)
                EmitPointerArrayGlobalInit(g, exprGen);

            if (g.Initializer != null)
                EmitScalarGlobalInit(g, exprGen);
        }
    }

    private void EmitPointerArrayGlobalInit(VariableNode g, ExpressionGenerator exprGen)
    {
        const OpCodeSize elemSize = OpCodeSize.S64;
        const int elemBytes = 8;

        for (int i = 0; i < g.ArrayInitializer!.Count; i++)
        {
            exprGen.GenerateExpression(g.ArrayInitializer[i]);
            _asm.EmitLdi(CodeGenUtils.ADDR_SCRATCH_REG_EN, GlobalMemoryManager.GetLabel(g.Name));
            _asm.EmitLdi(RegType.r1, (ulong)(i * elemBytes));
            _asm.EmitAdd(CodeGenUtils.ADDR_SCRATCH_REG_EN, RegType.r1);
            _asm.EmitStoreInd(RegType.r0, CodeGenUtils.ADDR_SCRATCH_REG_EN, elemSize);
        }
    }

    private void EmitScalarGlobalInit(VariableNode g, ExpressionGenerator exprGen)
    {
        if (CodeGenUtils.IsStructType(g.Type, _structTable)) return;   // struct-init не поддержан

        exprGen.GenerateExpression(g.Initializer!);
        OpCodeSize opSize = g.IsPointer ? OpCodeSize.S64
                                        : CodeGenUtils.GetSizeForType(g.Type, _erRep);
        _asm.EmitStore(RegType.r0, opSize, GlobalMemoryManager.GetLabel(g.Name));
    }

    private string GetLabel() => $"L{_labelCounter++}";
}