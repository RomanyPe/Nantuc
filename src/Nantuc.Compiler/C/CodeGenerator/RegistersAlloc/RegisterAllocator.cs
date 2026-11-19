using Nantuc.Common;
using Nantuc.Compiler.C.AST;

namespace Nantuc.Compiler.C.CodeGenerator.RegistersAlloc;

public static partial class RegisterAllocator
{
    private const int TotalRegisters = 32;

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
        return regs;
    }

    private static string TypeKey(VariableNode v)
        => v.IsPointer ? "\0ptr" : v.Type;

    public static RegisterAllocationResult AllocateRegisters(
        FunctionNode func,
        Dictionary<string, VariableNode> localVars,
        Dictionary<string, StructLayout> structTable)
    {
        var candidates = new Dictionary<string, VariableNode>(StringComparer.Ordinal);
        foreach (var (name, v) in localVars)
        {
            if (v.IsArray || v.IsPointer || structTable.ContainsKey(v.Type))
                continue;
            candidates[name] = v;
        }

        foreach (var a in VariableAccessAnalyzer.CollectAddressTakenVars(func.Body))
            candidates.Remove(a);

        var result = new RegisterAllocationResult();
        if (candidates.Count == 0) return result;

        var typeKeys = candidates.ToDictionary(kv => kv.Key, kv => TypeKey(kv.Value), StringComparer.Ordinal);
        var regs = GetAvailableRegisters();
        var cfg = CfgBuilder.Build(func);

        var live = new HashSet<string>(candidates.Keys, StringComparer.Ordinal);
        var spilled = new HashSet<string>(StringComparer.Ordinal);

        while (true)
        {
            Liveness.Run(cfg.Rpo, live);
            var graph = InterferenceGraph.Build(cfg.Rpo, live, typeKeys);
            var colorer = new CoalescingColorer(graph, regs);
            colorer.Run();

            if (colorer.Spills.Count == 0)
            {
                foreach (var n in live)
                {
                    var r = colorer.Color[n];
                    result.VarToRegister[n] = r;
                    result.UsedRegisters.Add(r);
                }
                result.CoalescedCount = colorer.CoalescedCount;

                foreach (var ins in cfg.Instructions)
                {
                    if (!ins.IsCopy || ins.Source is null) continue;
                    if (colorer.Color.TryGetValue(ins.Defs[0], out var rd) &&
                        colorer.Color.TryGetValue(ins.Uses[0], out var rs) && rd == rs)
                        result.ElideCopies.Add(ins.Source);

                    foreach (var site in ins.UseSites)
                    {
                        if (!live.Contains(site.Name)) continue;
                        if (ins.LiveOut.Contains(site.Name) || ins.Defs.Contains(site.Name)) continue;
                        if (ins.Uses.Count(u => u == site.Name) != 1) continue;
                        result.DeadAfterUse.Add(site);
                    }
                }
                break;
            }

            foreach (var s in colorer.Spills) { live.Remove(s); spilled.Add(s); }
            if (live.Count == 0) break;
        }

        foreach (var s in spilled) result.VarToRegister[s] = null;
        result.SpillCount = spilled.Count;

#if DEBUG
        VerifyAllocation(cfg, result.VarToRegister);
#endif
        return result;
    }

#if DEBUG
    private static void VerifyAllocation(CfgResult cfg, Dictionary<string, RegType?> map)
    {
        foreach (var b in cfg.Rpo)
        foreach (var ins in b.Code)
        {
            var seen = new Dictionary<RegType, string>();
            foreach (var v in ins.LiveOut)
            {
                if (!map.TryGetValue(v, out var r) || r is not { } rr) continue;
                if (seen.TryGetValue(rr, out var other))
                    throw new InvalidOperationException(
                        $"RA verify: '{v}' и '{other}' живы одновременно в {rr} (instr #{ins.GetHashCode():x})");
                seen[rr] = v;
            }

            foreach (var d in ins.Defs)
            {
                if (!map.TryGetValue(d, out var rd) || rd is null) continue;
                foreach (var v in ins.LiveOut)
                {
                    if (v == d) continue;
                    if (ins.IsCopy && ins.Uses.Count == 1 && ins.Uses[0] == v) continue;
                    if (map.TryGetValue(v, out var rv) && rv == rd)
                        throw new InvalidOperationException(
                            $"RA verify: def '{d}' затирает живой '{v}' в {rd}");
                }
            }
        }
    }
#endif
}