namespace Nantuc.Benchmarks;

public static class Stats
{
    public static double Mean(IReadOnlyList<double> v) => v.Count == 0 ? 0 : v.Average();

    public static double StdDev(IReadOnlyList<double> v)
    {
        if (v.Count < 2) return 0;
        double avg = v.Average();
        double sum = v.Sum(x => (x - avg) * (x - avg));
        return Math.Sqrt(sum / (v.Count - 1));
    }

    public static double Percentile(IReadOnlyList<double> v, double p)
    {
        if (v.Count == 0) return 0;
        var s = v.OrderBy(x => x).ToArray();
        double rank = (p / 100.0) * (s.Length - 1);
        int lo = (int)Math.Floor(rank);
        int hi = (int)Math.Ceiling(rank);
        if (lo == hi) return s[lo];
        return s[lo] + (s[hi] - s[lo]) * (rank - lo);
    }

    public static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(x => x).ToArray();
        int n = sorted.Length;
        if (n % 2 == 1) return sorted[n / 2];
        return (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
    }

    public static double Min(IReadOnlyList<double> v) => v.Count == 0 ? 0 : v.Min();
    public static double Max(IReadOnlyList<double> v) => v.Count == 0 ? 0 : v.Max();
    public static double Sum(IReadOnlyList<double> v) => v.Count == 0 ? 0 : v.Sum();
    public static double Range(IReadOnlyList<double> v) => v.Count == 0 ? 0 : v.Max() - v.Min();
    public static double IQR(IReadOnlyList<double> v) => Percentile(v, 75) - Percentile(v, 25);

    /// <summary>Коэффициент вариации (StdDev / Mean). Безразмерный, показывает стабильность.</summary>
    public static double CV(IReadOnlyList<double> v)
    {
        var m = Mean(v);
        return m == 0 ? 0 : StdDev(v) / m;
    }

    /// <summary>Усечённое среднее: отбрасываем по trimPercent% с обоих концов и считаем среднее.</summary>
    public static double TrimmedMean(IReadOnlyList<double> v, double trimPercent)
    {
        if (v.Count == 0) return 0;
        if (trimPercent <= 0) return Mean(v);
        if (trimPercent >= 50) return Median(v);

        var sorted = v.OrderBy(x => x).ToArray();
        int cut = (int)Math.Floor(sorted.Length * trimPercent / 100.0);
        int start = cut;
        int end = sorted.Length - cut;
        if (end <= start) return Median(v);

        double sum = 0;
        for (int i = start; i < end; i++) sum += sorted[i];
        return sum / (end - start);
    }

    /// <summary>Среднее геометрическое. Для MIPS осмысленно, если значения — «скорости».</summary>
    public static double GeometricMean(IReadOnlyList<double> v)
    {
        if (v.Count == 0) return 0;
        double logSum = 0;
        int n = 0;
        foreach (var x in v)
        {
            if (x <= 0) continue;
            logSum += Math.Log(x);
            n++;
        }
        return n == 0 ? 0 : Math.Exp(logSum / n);
    }

    /// <summary>Асимметрия. >0 — хвост вправо (выбросы вверх), <0 — хвост влево.</summary>
    public static double Skewness(IReadOnlyList<double> v)
    {
        int n = v.Count;
        if (n < 3) return 0;
        double m = Mean(v);
        double s = StdDev(v);
        if (s == 0) return 0;
        double sum = 0;
        foreach (var x in v) sum += Math.Pow((x - m) / s, 3);
        return (double)n / ((n - 1) * (n - 2)) * sum;
    }

    /// <summary>Среднее по первой половине прогонов (обычно "холодные").</summary>
    public static double FirstHalfMean(IReadOnlyList<double> v)
    {
        if (v.Count == 0) return 0;
        int n = v.Count / 2;
        double sum = 0;
        for (int i = 0; i < n; i++) sum += v[i];
        return n == 0 ? 0 : sum / n;
    }

    /// <summary>Среднее по второй половине прогонов (обычно "горячие").</summary>
    public static double SecondHalfMean(IReadOnlyList<double> v)
    {
        if (v.Count == 0) return 0;
        int n = v.Count / 2;
        double sum = 0;
        for (int i = v.Count - n; i < v.Count; i++) sum += v[i];
        return n == 0 ? 0 : sum / n;
    }
}