using Nantuc.Common;
using Nantuc.Compiler.C.AST;

namespace Nantuc.Compiler.C.CodeGenerator.RegistersAlloc;

// ============================================================
// Распределение регистров: оркестратор.
//
// Partial-файлы:
//   • RegisterAllocator.cs              — public API (этот файл)
//   • RegisterAllocator.Interference.cs — построение графа интерференции
//
// Вынесены отдельные классы:
//   • VariableAccessAnalyzer — AST-walker'ы (def/use events, address-taken)
//   • GreedyGraphColorer     — жадная раскраска графа
// ============================================================
public static partial class RegisterAllocator
{
    private const int TotalRegisters = 32;

    // r0..r4 — возврат/временные, r27 — scratch (CodeGenUtils.ADDR_SCRATCH_REG_EN),
    // rFL/rSP/rHP/rIP — системные.
    private static readonly HashSet<RegType> ReservedRegs =
    [
        RegType.r0, RegType.r1, RegType.r2, RegType.r3, RegType.r4,
        RegType.r27,
        RegType.rFL, RegType.rSP, RegType.rHP, RegType.rIP,
    ];

    private static List<RegType> GetAvailableRegisters()
    {
        var regs = new List<RegType>(TotalRegisters);
        for (int i = 0; i < TotalRegisters; i++)
        {
            var reg = (RegType)i;
            if (!ReservedRegs.Contains(reg)) regs.Add(reg);
        }
        return regs;   // r5..r26 — 22 регистра под переменные
    }

    // В разработке
    /*
    public static RegisterAllocationResult AllocateRegistersNew(
    FunctionNode func,
    Dictionary<string, VariableNode> localVars,
    Dictionary<string, StructLayout> structTable)
    {
        var candidates = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (name, v) in localVars)
            if (!v.IsArray && !structTable.ContainsKey(v.Type)) candidates.Add(name);
        candidates.ExceptWith(VariableAccessAnalyzer.CollectAddressTakenVars(func.Body));

        var cfg = CfgBuilder.Build(func);
        var regs = GetAvailableRegisters();
        var result = new RegisterAllocationResult();
        var spilled = new HashSet<string>(StringComparer.Ordinal);

        while (true)
        {
            LivenessAnalyzer.Run(cfg.Rpo, candidates);
            var graph = InterferenceGraph.Build(cfg.Rpo, candidates);
            var colorer = new CoalescingColorer(graph, regs);
            colorer.Run();

            if (colorer.Spills.Count == 0 || candidates.Count == 0)
            {
                foreach (var n in candidates)
                    if (colorer.Color.TryGetValue(n, out var r))
                    { result.VarToRegister[n] = r; result.UsedRegisters.Add(r); }
                    else result.VarToRegister[n] = null;

                // подсказка codegen: эти `x = y;` можно не эмитить
                foreach (var ins in cfg.All)
                    if (ins is { IsCopy: true, Source: not null }
                        && ins.CopyDst is { } d && ins.CopySrc is { } s
                        && result.VarToRegister.GetValueOrDefault(d) is { } rd
                        && result.VarToRegister.GetValueOrDefault(s) == rd)
                        result.ElidedCopies.Add(ins.Source);
                break;
            }

            // spill-everywhere: переменная уходит в память, пересобираем граф
            foreach (var s in colorer.Spills) { candidates.Remove(s); spilled.Add(s); }
        }

        foreach (var s in spilled) result.VarToRegister[s] = null;
        return result;
    }
    */
    public static RegisterAllocationResult AllocateRegisters(
        FunctionNode func,
        Dictionary<string, VariableNode> localVars,
        Dictionary<string, StructLayout> structTable)
    {

        // 1) Кандидаты — скаляры, не массивы и не структуры
        var candidates = new Dictionary<string, VariableNode>();
        foreach (var (name, varNode) in localVars)
        {
            if (varNode.IsArray || structTable.ContainsKey(varNode.Type)) continue;
            candidates[name] = varNode;
        }

        // 2) Убираем переменные, чей адрес берётся (&x) — они обязаны жить в памяти
        foreach (var addrTaken in VariableAccessAnalyzer.CollectAddressTakenVars(func.Body))
            candidates.Remove(addrTaken);

        // 3) Граф интерференции
        var interference = BuildInterferenceGraph(func, candidates);

        // 4) Жадная раскраска
        var coloring = GreedyGraphColorer.Color(interference, GetAvailableRegisters());

        // 5) Результат
        var result = new RegisterAllocationResult();
        foreach (var varName in candidates.Keys)
        {
            if (coloring.TryGetValue(varName, out var reg))
            {
                result.VarToRegister[varName] = reg;
                result.UsedRegisters.Add(reg);
            }
            else
            {
                result.VarToRegister[varName] = null;   // в стек
            }
        }
        return result;
    }
}