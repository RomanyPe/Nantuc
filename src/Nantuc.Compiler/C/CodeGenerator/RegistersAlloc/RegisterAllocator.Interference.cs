using Nantuc.Compiler.C.AST;

namespace Nantuc.Compiler.C.CodeGenerator.RegistersAlloc;

public static partial class RegisterAllocator
{
    // ============================================================
    // Граф интерференции: ребро между a и b, если их live-диапазоны
    // [firstDef, lastUse] пересекаются.
    // ============================================================
    private static Dictionary<string, HashSet<string>> BuildInterferenceGraph(
        FunctionNode func,
        Dictionary<string, VariableNode> candidates)
    {
        var (defs, uses) = VariableAccessAnalyzer.CollectVarEvents(func.Body);

        // Параметры определяются в самом начале функции (позиция 0)
        foreach (var param in func.Parameters)
            if (candidates.ContainsKey(param.Name))
                VariableAccessAnalyzer.AddEvent(defs, param.Name, 0);

        var ranges = ComputeLiveRanges(candidates, defs, uses);

        var graph = new Dictionary<string, HashSet<string>>(candidates.Count);
        foreach (var name in candidates.Keys)
            graph[name] = [];

        var names = candidates.Keys.ToList();
        for (int i = 0; i < names.Count; i++)
            for (int j = i + 1; j < names.Count; j++)
            {
                var a = names[i];
                var b = names[j];
                if (!ranges.TryGetValue(a, out var ra) || !ranges.TryGetValue(b, out var rb))
                    continue;

                if (Overlaps(ra, rb))
                {
                    graph[a].Add(b);
                    graph[b].Add(a);
                }
            }
        return graph;
    }

    private static Dictionary<string, (int start, int end)> ComputeLiveRanges(
        Dictionary<string, VariableNode> candidates,
        Dictionary<string, List<int>> defs,
        Dictionary<string, List<int>> uses)
    {
        var ranges = new Dictionary<string, (int start, int end)>(candidates.Count);
        foreach (var name in candidates.Keys)
        {
            if (!defs.TryGetValue(name, out var defPositions) || defPositions.Count == 0)
                continue;

            int start = defPositions.Min();
            int end = defPositions.Max();
            if (uses.TryGetValue(name, out var usePositions) && usePositions.Count > 0)
                end = Math.Max(end, usePositions.Max());
            ranges[name] = (start, end);
        }
        return ranges;
    }

    private static bool Overlaps((int start, int end) a, (int start, int end) b)
        => Math.Max(a.start, b.start) <= Math.Min(a.end, b.end);
}