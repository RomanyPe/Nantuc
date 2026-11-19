namespace Nantuc.Compiler.C.CodeGenerator.RegistersAlloc;

internal sealed class InterferenceGraph
{
    public Dictionary<string, HashSet<string>> Adj { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, HashSet<IrInstruction>> Moves { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, double> SpillCost { get; } = new(StringComparer.Ordinal);
    public List<IrInstruction> AllMoves { get; } = [];

    public static InterferenceGraph Build(
        List<BasicBlock> rpo, HashSet<string> live, Dictionary<string, string> typeKey)
    {
        var g = new InterferenceGraph();
        foreach (var n in live)
        {
            g.Adj[n] = new(StringComparer.Ordinal);
            g.Moves[n] = [];
            g.SpillCost[n] = 0.01;
        }

        foreach (var b in rpo)
            foreach (var ins in b.Code)
            {
                // Цена спилла: чтение ~4 инструкции, запись ~3, в цикле ×10 за уровень.
                double w = Math.Pow(10, ins.LoopDepth);
                foreach (var u in ins.Uses) if (live.Contains(u)) g.SpillCost[u] += 4 * w;
                foreach (var d in ins.Defs) if (live.Contains(d)) g.SpillCost[d] += 3 * w;

                bool isCopy = ins is { IsCopy: true, Defs.Count: 1, Uses.Count: 1 }
                              && live.Contains(ins.Defs[0]) && live.Contains(ins.Uses[0])
                              && ins.Defs[0] != ins.Uses[0]
                              // CAST в StoreFromRegister канонизирует home-регистр:
                              // объединять можно только одинаковые типы.
                              && typeKey[ins.Defs[0]] == typeKey[ins.Uses[0]];

                if (isCopy)
                {
                    var d = ins.Defs[0]; var s = ins.Uses[0];
                    foreach (var v in ins.LiveOut)
                        if (v != d && v != s) g.AddEdge(d, v);   // ребро d–s НЕ добавляем
                    g.Moves[d].Add(ins); g.Moves[s].Add(ins);
                    g.AllMoves.Add(ins);
                }
                else
                {
                    foreach (var d in ins.Defs)
                    {
                        if (!live.Contains(d)) continue;
                        foreach (var v in ins.LiveOut) if (v != d) g.AddEdge(d, v);
                    }
                }
            }
        return g;
    }

    private void AddEdge(string a, string b)
    {
        if (a == b) return;
        if (Adj[a].Add(b)) Adj[b].Add(a);
    }
}