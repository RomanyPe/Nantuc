using Nantuc.Common;

namespace Nantuc.Compiler.C.CodeGenerator.RegistersAlloc;

// ============================================================
// Жадная раскраска графа интерференции.
// Порядок: по убыванию степени (эвристика Welsh–Powell).
// Возвращает отображение "имя → регистр"; нераскрашенные
// переменные отсутствуют в результате (идут в стек).
// ============================================================
internal static class GreedyGraphColorer
{
    public static Dictionary<string, RegType> Color(
        Dictionary<string, HashSet<string>> graph,
        List<RegType> availableRegs)
    {
        var coloring = new Dictionary<string, RegType>(graph.Count);
        var usedByNeighbors = new HashSet<RegType>();

        var ordered = graph.Keys
            .OrderByDescending(v => graph[v].Count)
            .ToList();

        foreach (var varName in ordered)
        {
            usedByNeighbors.Clear();
            foreach (var neighbor in graph[varName])
                if (coloring.TryGetValue(neighbor, out var neighborReg))
                    usedByNeighbors.Add(neighborReg);

            foreach (var reg in availableRegs)
            {
                if (!usedByNeighbors.Contains(reg))
                {
                    coloring[varName] = reg;
                    break;
                }
            }
            // Если регистра не нашлось — переменная останется в стеке.
        }
        return coloring;
    }
}