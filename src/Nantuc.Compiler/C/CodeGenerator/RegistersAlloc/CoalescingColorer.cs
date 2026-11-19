using Nantuc.Common;

namespace Nantuc.Compiler.C.CodeGenerator.RegistersAlloc;

internal sealed class CoalescingColorer(InterferenceGraph g, List<RegType> regs)
{
    readonly InterferenceGraph _g = g;
    readonly List<RegType> _regs = regs;
    readonly int _k = regs.Count;

    // Исходные соседи (Adj разрушается при simplify) + объединение при coalesce
    readonly Dictionary<string, HashSet<string>> _nb0 = new(StringComparer.Ordinal);
    readonly Dictionary<string, string> _alias = new(StringComparer.Ordinal);
    readonly HashSet<string> _coalesced = new(StringComparer.Ordinal);
    readonly SortedSet<string> _simplify = new(StringComparer.Ordinal);
    readonly SortedSet<string> _freeze = new(StringComparer.Ordinal);
    readonly SortedSet<string> _spill = new(StringComparer.Ordinal);
    readonly Stack<string> _stack = new();
    readonly List<IrInstruction> _moves = [];
    readonly HashSet<IrInstruction> _active = [];
    readonly Dictionary<string, RegType> _color = new(StringComparer.Ordinal);

    public Dictionary<string, RegType> Color => _color;
    public List<string> Spills { get; } = [];
    public int CoalescedCount => _coalesced.Count;

    public void Run()
    {
        foreach (var (n, adj) in _g.Adj) _nb0[n] = new HashSet<string>(adj, StringComparer.Ordinal);
        foreach (var m in _g.AllMoves) { _active.Add(m); _moves.Add(m); }

        foreach (var n in _g.Adj.Keys.OrderBy(x => x, StringComparer.Ordinal))
        {
            if (_g.Adj[n].Count >= _k) _spill.Add(n);
            else if (MoveRelated(n)) _freeze.Add(n);
            else _simplify.Add(n);
        }

        while (true)
        {
            if (_simplify.Count > 0) Simplify();
            else if (_moves.Count > 0) Coalesce();
            else if (_freeze.Count > 0) Freeze();
            else if (_spill.Count > 0) SelectSpill();
            else break;
        }
        AssignColors();
    }

    private string Alias(string n) => _coalesced.Contains(n) ? Alias(_alias[n]) : n;

    private bool MoveRelated(string n)
    {
        foreach (var m in _g.Moves[n]) if (_active.Contains(m)) return true;
        return false;
    }

    private void EnableMoves(IEnumerable<string> nodes)
    {
        foreach (var n in nodes)
            foreach (var m in _g.Moves[n])
                if (_active.Contains(m)) _moves.Add(m);   // дубликаты отсеются при pop
    }

    private void AddWorkList(string n)
    {
        if (!_coalesced.Contains(n) && !MoveRelated(n) && _g.Adj[n].Count < _k)
        { _freeze.Remove(n); _spill.Remove(n); _simplify.Add(n); }
    }

    private void AddEdge(string a, string b)
    {
        if (a == b) return;
        if (_g.Adj[a].Add(b)) _g.Adj[b].Add(a);
    }

    private void Simplify()
    {
        var n = _simplify.Min!; _simplify.Remove(n);
        _stack.Push(n);
        foreach (var m in _g.Adj[n].ToList())
        {
            _g.Adj[m].Remove(n);
            if (_g.Adj[m].Count == _k - 1)      // пересёк порог сверху вниз
            {
                EnableMoves([m]);
                EnableMoves([.. _g.Adj[m]]);
                if (_spill.Remove(m)) { if (MoveRelated(m)) _freeze.Add(m); else _simplify.Add(m); }
            }
        }
        _g.Adj[n].Clear();
    }

    private bool Briggs(string u, string v)
    {
        int sig = 0;
        foreach (var n in _g.Adj[u]) if (_g.Adj[n].Count >= _k) sig++;
        foreach (var n in _g.Adj[v]) if (_g.Adj[n].Count >= _k && !_g.Adj[u].Contains(n)) sig++;
        return sig < _k;
    }

    private bool George(string u, string v)
    {
        foreach (var t in _g.Adj[v])
            if (!_g.Adj[u].Contains(t) && _g.Adj[t].Count >= _k) return false;
        return true;
    }

    private void Coalesce()
    {
        var m = _moves[^1]; _moves.RemoveAt(_moves.Count - 1);
        if (!_active.Remove(m)) return;         // уже обработан или заморожен

        var u = Alias(m.Defs[0]);
        var v = Alias(m.Uses[0]);

        if (u == v) { AddWorkList(u); return; }
        if (_g.Adj[u].Contains(v) || !(Briggs(u, v) || George(u, v)))
        { AddWorkList(u); AddWorkList(v); return; }   // freeze

        _coalesced.Add(v); _alias[v] = u;
        _simplify.Remove(v); _freeze.Remove(v); _spill.Remove(v);
        _g.Moves[u].UnionWith(_g.Moves[v]);
        _nb0[u].UnionWith(_nb0[v]);

        foreach (var t in _g.Adj[v].ToList())
        {
            bool had = _g.Adj[t].Contains(u);
            AddEdge(t, u);
            _g.Adj[t].Remove(v);
            if (had && _g.Adj[t].Count == _k - 1)
            {
                EnableMoves([t]);
                EnableMoves([.. _g.Adj[t]]);
                if (_spill.Remove(t)) { if (MoveRelated(t)) _freeze.Add(t); else _simplify.Add(t); }
            }
        }
        _g.Adj[v].Clear();

        if (_g.Adj[u].Count >= _k) { _simplify.Remove(u); _freeze.Remove(u); _spill.Add(u); }
        EnableMoves([v]);
        AddWorkList(u);
    }

    private void Freeze()
    {
        var u = _freeze.Min!; _freeze.Remove(u);
        _simplify.Add(u);
        FreezeMoves(u);
    }

    private void FreezeMoves(string u)
    {
        var au = Alias(u);
        foreach (var m in _g.Moves[u].ToList())
        {
            if (!_active.Remove(m)) continue;
            var x = Alias(m.Defs[0]); var y = Alias(m.Uses[0]);
            var v = y == au ? x : y;
            if (v != au && !MoveRelated(v) && _g.Adj[v].Count < _k)
            { _freeze.Remove(v); _simplify.Add(v); }
        }
    }

    private void SelectSpill()
    {
        // Оптимистичный спилл по Briggs: минимальная цена на единицу степени.
        // SortedSet обходится в ordinal-порядке → детерминированный tie-break.
        string? best = null; double bestScore = double.PositiveInfinity;
        foreach (var n in _spill)
        {
            var score = _g.SpillCost[n] / Math.Max(1, _g.Adj[n].Count);
            if (score < bestScore) { bestScore = score; best = n; }
        }
        _spill.Remove(best!);
        _simplify.Add(best!);
        FreezeMoves(best!);
    }

    private void AssignColors()
    {
        while (_stack.Count > 0)
        {
            var n = _stack.Pop();
            var free = new List<RegType>(_regs);
            foreach (var w in _nb0[n])
                if (_color.TryGetValue(Alias(w), out var c)) free.Remove(c);

            if (free.Count == 0) Spills.Add(n);   // реальный спилл, не потенциальный
            else _color[n] = free[0];
        }
        foreach (var n in _coalesced)
        {
            if (_color.TryGetValue(Alias(n), out var c)) _color[n] = c;
            else Spills.Add(n);
        }
    }
}