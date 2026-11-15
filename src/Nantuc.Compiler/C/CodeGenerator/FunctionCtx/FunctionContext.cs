using Nantuc.Common;
using Nantuc.Compiler.C.AST;
using Nantuc.Compiler.C.CodeGenerator.RegistersAlloc;
using Nantuc.Diagnostics;
using static Nantuc.Compiler.C.CodeGenerator.FunctionCtx.VariableLayoutBuilder;

namespace Nantuc.Compiler.C.CodeGenerator.FunctionCtx;

// ============================================================
// FunctionContext – контекст текущей функции
// ============================================================
public class FunctionContext
{
    public Dictionary<string, VarLocation> VarMap { get; private set; } = null!;
    public int TotalLocalSize { get; private set; }
    public string FunctionName { get; private set; } = null!;
    public string EpilogueLabel { get; private set; } = null!;
    public List<RegType> UsedRegisters { get; private set; } = [];
    public int SaveAreaSize { get; private set; } = 0;
    public int ExpressionSaveDepth { get; set; } = 0;
    public OpCodeSize ReturnSize { get; private set; } = OpCodeSize.S64;
    public bool ReturnIsSigned { get; private set; }
    public bool ReturnIsVoid { get; private set; }

    public static FunctionContext Create(
        FunctionNode func,
        Dictionary<string, VariableNode> localVarNodes,
        Dictionary<string, StructLayout> structTable,
        ErrorReporter errorReporter)
    {
        var ctx = new FunctionContext
        {
            FunctionName = func.Name,
            EpilogueLabel = $"__epilogue_{func.Name}",
        };

        var allVars = CollectVariables(func, localVarNodes);
        var allVarNodes = BuildVarNodeMap(func, localVarNodes);
        var allocation = RegisterAllocator.AllocateRegisters(func, allVarNodes, structTable);

        ctx.VarMap = BuildVarMap(allVars, allocation, structTable, errorReporter, out int totalLocalSize);
        ctx.TotalLocalSize = totalLocalSize;

        ConfigureReturnInfo(ctx, func, structTable, errorReporter);

        ctx.SaveAreaSize = (SaveDepthEstimator.Estimate(func.Body) + 1) * 8;
        ctx.UsedRegisters = [.. ctx.VarMap.Values.Where(v => v.IsRegister).Select(v => v.Register)];

        return ctx;
    }

    // --- Сбор переменных ---
    private static List<VarInfo> CollectVariables(FunctionNode func, Dictionary<string, VariableNode> locals)
    {
        var result = new List<VarInfo>(func.Parameters.Count + locals.Count);

        foreach (var p in func.Parameters)
            result.Add(new VarInfo(p.Name, p.Type, false, 0, p.IsPointer, p.PointedType, p.IsConst, p.IsPointedConst));

        var declared = result.Select(v => v.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var (name, v) in locals)
        {
            if (declared.Contains(name)) continue;
            result.Add(new VarInfo(name, v.Type, v.IsArray, v.ArraySize, v.IsPointer, v.PointedType, v.IsConst, v.IsPointedConst));
        }
        return result;
    }

    private static Dictionary<string, VariableNode> BuildVarNodeMap(
        FunctionNode func, Dictionary<string, VariableNode> locals)
    {
        var map = new Dictionary<string, VariableNode>(func.Parameters.Count + locals.Count);
        foreach (var p in func.Parameters)
            map[p.Name] = new VariableNode(p.Type, p.Name)
            {
                IsPointer = p.IsPointer,
                PointedType = p.PointedType,
                IsArray = false,
                ArraySize = 0,
            };
        foreach (var (name, v) in locals) map.TryAdd(name, v);
        return map;
    }

    // --- Раскладка (делегирует в VariableLayoutBuilder) ---
    private static Dictionary<string, VarLocation> BuildVarMap(
        List<VarInfo> vars,
        RegisterAllocationResult allocation,
        Dictionary<string, StructLayout> structTable,
        ErrorReporter er,
        out int totalLocalSize)
    {
        var map = new Dictionary<string, VarLocation>(vars.Count);
        int stackOffset = 0;

        foreach (var v in vars)
        {
            map[v.Name] = VariableLayoutBuilder.Build(v, stackOffset, allocation, structTable, er, out int consumed);
            stackOffset += consumed;
        }

        totalLocalSize = stackOffset;
        return map;
    }

    private static void ConfigureReturnInfo(
        FunctionContext ctx, FunctionNode func,
        Dictionary<string, StructLayout> structTable, ErrorReporter er)
    {
        ctx.ReturnIsVoid = func.ReturnType == "void";
        if (ctx.ReturnIsVoid) return;
        if (func.ReturnIsPointer) return;
        if (structTable.ContainsKey(func.ReturnType)) return;

        ctx.ReturnSize = CodeGenUtils.GetSizeForType(func.ReturnType, er);
        ctx.ReturnIsSigned = CodeGenUtils.IsSignedType(func.ReturnType);
    }
}

