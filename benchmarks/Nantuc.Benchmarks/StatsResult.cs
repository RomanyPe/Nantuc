namespace Nantuc.Benchmarks;

public record struct StatsResult(
    double Mean,
    double Median,
    double Min,
    double Max,
    double P1,
    double P5,
    double P25,
    double P75,
    double P95,
    double P99,
    double StdDev,
    double CV,
    double IQR,
    double Range,
    double TrimmedMean5,
    double TrimmedMean10,
    double GeometricMean,
    double Skewness,
    double FirstHalfMean,
    double SecondHalfMean,
    int N)
{
    public static StatsResult From(IReadOnlyList<double> v)
    {
        if (v.Count == 0)
            return new StatsResult(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        return new StatsResult(
            Mean: Stats.Mean(v),
            Median: Stats.Median(v),
            Min: Stats.Min(v),
            Max: Stats.Max(v),
            P1: Stats.Percentile(v, 1),
            P5: Stats.Percentile(v, 5),
            P25: Stats.Percentile(v, 25),
            P75: Stats.Percentile(v, 75),
            P95: Stats.Percentile(v, 95),
            P99: Stats.Percentile(v, 99),
            StdDev: Stats.StdDev(v),
            CV: Stats.CV(v),
            IQR: Stats.IQR(v),
            Range: Stats.Range(v),
            TrimmedMean5: Stats.TrimmedMean(v, 5),
            TrimmedMean10: Stats.TrimmedMean(v, 10),
            GeometricMean: Stats.GeometricMean(v),
            Skewness: Stats.Skewness(v),
            FirstHalfMean: Stats.FirstHalfMean(v),
            SecondHalfMean: Stats.SecondHalfMean(v),
            N: v.Count);
    }
}