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