namespace Nantuc.Compiler.C.CodeGenerator.RegistersAlloc;

internal static class LivenessAnalyzer
{
    public static void Run(List<BasicBlock> rpo, HashSet<string> tracked)
    {
        foreach (var b in rpo)
        {
            b.Use.Clear(); b.Def.Clear();
            foreach (var ins in b.Code)
            {
                foreach (var u in ins.Uses) if (tracked.Contains(u) && !b.Def.Contains(u)) b.Use.Add(u);
                foreach (var d in ins.Defs) if (tracked.Contains(d)) b.Def.Add(d);
            }
            b.LiveIn.Clear(); b.LiveOut.Clear();
        }

        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int i = rpo.Count - 1; i >= 0; i--)      // обратный RPO = быстрая сходимость
            {
                var b = rpo[i];
                var outSet = new HashSet<string>(StringComparer.Ordinal);
                foreach (var s in b.Succs) outSet.UnionWith(s.LiveIn);
                var inSet = new HashSet<string>(outSet, StringComparer.Ordinal);
                inSet.ExceptWith(b.Def); inSet.UnionWith(b.Use);
                if (!outSet.SetEquals(b.LiveOut) || !inSet.SetEquals(b.LiveIn))
                { b.LiveOut = outSet; b.LiveIn = inSet; changed = true; }
            }
        }

        foreach (var b in rpo)
        {
            var live = new HashSet<string>(b.LiveOut, StringComparer.Ordinal);
            for (int i = b.Code.Count - 1; i >= 0; i--)
            {
                var ins = b.Code[i];
                ins.LiveOut = new HashSet<string>(live, StringComparer.Ordinal);
                foreach (var d in ins.Defs) live.Remove(d);
                foreach (var u in ins.Uses) if (tracked.Contains(u)) live.Add(u);
            }
        }
    }
}