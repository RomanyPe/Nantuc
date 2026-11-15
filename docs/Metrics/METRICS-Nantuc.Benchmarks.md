# Code Metrics Report

Path: `benchmarks/Nantuc.Benchmarks/Nantuc.Benchmarks.csproj`

## Summary

| Metric | Value |
| --- | ---: |
| Members analysed | 36 |
| C# files | 6 |
| Total lines | 839 |
| C# classes | 5 |
| C# records | 2 |
| C# enums | 0 |
| Analysed LOC | 282 |
| Cognitive avg | 1.9 |
| Cognitive median | 1 |
| Cognitive p90 | 5 |
| Cognitive max | 13 |
| Cyclomatic avg | 2.8 |
| Cyclomatic median | 2 |
| Cyclomatic p90 | 5 |
| Cyclomatic max | 14 |
| MI avg | 74.5 |
| MI minimum | 29.7 |

Highest risk member: `Program.PrintProgress` (cognitive 13, cyclomatic 14) — `Program.cs:161`
Lowest maintainability: `Program.Main` (MI 29.7) — `Program.cs:19`

## Risk bands

| Band | Range | Members |
| --- | --- | ---: |
| Light | 0-5 | 34 |
| Moderate | 6-10 | 1 |
| High | 11-20 | 1 |
| Severe | 21+ | 0 |

## Members

Ranked by cognitive complexity, then cyclomatic complexity. Showing 20 of 36 member(s).

| File | Line | Type | Member | Cognitive | Cyclomatic | LinesOfCode | MaxNesting | Parameters | MaintainabilityIndex |
| --- | ---: | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| C:\Users\xity4\source\repos\Nantuc\benchmarks\Nantuc.Benchmarks\Program.cs | 161 | Program | PrintProgress | 13 | 14 | 24 | 2 | 13 | 45.7 |
| C:\Users\xity4\source\repos\Nantuc\benchmarks\Nantuc.Benchmarks\Program.cs | 19 | Program | Main | 9 | 6 | 95 | 2 | 0 | 29.7 |
| C:\Users\xity4\source\repos\Nantuc\benchmarks\Nantuc.Benchmarks\Stats.cs | 49 | Stats | TrimmedMean | 5 | 6 | 11 | 1 | 2 | 57.6 |
| C:\Users\xity4\source\repos\Nantuc\benchmarks\Nantuc.Benchmarks\Stats.cs | 67 | Stats | GeometricMean | 5 | 5 | 8 | 2 | 1 | 63.2 |
| C:\Users\xity4\source\repos\Nantuc\benchmarks\Nantuc.Benchmarks\BenchRunner.cs | 39 | BenchRunner | TryPrepare | 4 | 5 | 17 | 2 | 0 | 53.0 |
| C:\Users\xity4\source\repos\Nantuc\benchmarks\Nantuc.Benchmarks\Stats.cs | 82 | Stats | Skewness | 3 | 4 | 8 | 1 | 1 | 62.4 |
| C:\Users\xity4\source\repos\Nantuc\benchmarks\Nantuc.Benchmarks\Stats.cs | 95 | Stats | FirstHalfMean | 3 | 4 | 5 | 1 | 1 | 68.0 |
| C:\Users\xity4\source\repos\Nantuc\benchmarks\Nantuc.Benchmarks\Stats.cs | 105 | Stats | SecondHalfMean | 3 | 4 | 5 | 1 | 1 | 67.6 |
| C:\Users\xity4\source\repos\Nantuc\benchmarks\Nantuc.Benchmarks\Program.cs | 151 | Program | PrintTestHeader | 2 | 3 | 4 | 1 | 6 | 70.2 |
| C:\Users\xity4\source\repos\Nantuc\benchmarks\Nantuc.Benchmarks\Program.cs | 206 | Program | FormatTime | 2 | 3 | 3 | 1 | 1 | 72.6 |
| C:\Users\xity4\source\repos\Nantuc\benchmarks\Nantuc.Benchmarks\Program.cs | 244 | Program | AppendIterations | 2 | 3 | 9 | 1 | 3 | 61.7 |
| C:\Users\xity4\source\repos\Nantuc\benchmarks\Nantuc.Benchmarks\Stats.cs | 15 | Stats | Percentile | 2 | 3 | 7 | 1 | 2 | 63.1 |
| C:\Users\xity4\source\repos\Nantuc\benchmarks\Nantuc.Benchmarks\Stats.cs | 26 | Stats | Median | 2 | 3 | 5 | 1 | 1 | 67.4 |
| C:\Users\xity4\source\repos\Nantuc\benchmarks\Nantuc.Benchmarks\BenchRunner.cs | 25 | BenchRunner | PeakMips | 1 | 2 | 3 | 1 | 0 | 76.7 |
| C:\Users\xity4\source\repos\Nantuc\benchmarks\Nantuc.Benchmarks\BenchRunner.cs | 30 | BenchRunner | StepsOnce | 1 | 2 | 1 | 1 | 0 | 88.3 |
| C:\Users\xity4\source\repos\Nantuc\benchmarks\Nantuc.Benchmarks\BenchRunner.cs | 33 | BenchRunner | StepsVariance | 1 | 2 | 3 | 2 | 0 | 76.1 |
| C:\Users\xity4\source\repos\Nantuc\benchmarks\Nantuc.Benchmarks\BenchRunner.cs | 89 | ListExtensions. | ComputeVariance | 1 | 2 | 3 | 1 | 0 | 74.3 |
| C:\Users\xity4\source\repos\Nantuc\benchmarks\Nantuc.Benchmarks\Program.cs | 213 | Program | SafeWindowWidth | 1 | 2 | 2 | 1 | 0 | 82.0 |
| C:\Users\xity4\source\repos\Nantuc\benchmarks\Nantuc.Benchmarks\Stats.cs | 5 | Stats | Mean | 1 | 2 | 1 | 1 | 1 | 88.7 |
| C:\Users\xity4\source\repos\Nantuc\benchmarks\Nantuc.Benchmarks\Stats.cs | 7 | Stats | StdDev | 1 | 2 | 4 | 1 | 1 | 70.8 |

