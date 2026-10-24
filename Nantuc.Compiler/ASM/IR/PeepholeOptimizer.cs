using Nantuc.Kernel.Contracts;

namespace Nantuc.Compiler.ASM.IR;


public class PeepholeOptimizer
{
    private readonly List<IPeepholeRule> _rules = [];
    public void Add(IPeepholeRule rule) => _rules.Add(rule);
    public void Insert(int index, IPeepholeRule item) => _rules.Insert(index, item);
    public void Remove(IPeepholeRule item) => _rules.Remove(item);
    public void RemoveAt(int index) => _rules.RemoveAt(index);
    public int Count => _rules.Count;

    public IEnumerable<IReadOnlyLogOptimization> Optimize(List<AsmItem> items, int maxPasses = 5)
    {
        _rules.ForEach(rule => rule.Clear());

        bool changed;
        int passCount = 0;

        do
        {
            changed = false;
            int i = 0;
            while (i < items.Count)
            {
                if (items[i] is AsmLabel)
                {
                    i++;
                    continue;
                }

                bool ruleApplied = false;
                foreach (var rule in _rules)
                {
                    if (rule.TryOptimize(items, i, passCount, out int nextIndex))
                    {
                        if (nextIndex < 0)
                            nextIndex = 0;
                        changed = true;
                        ruleApplied = true;
                        i = nextIndex;
                        break;
                    }
                }

                if (!ruleApplied)
                {
                    i++;
                }
            }

            passCount++;
        }
        while (changed && passCount < maxPasses);
        return _rules.Select(static rule => rule.GetLogs());
    }
}

