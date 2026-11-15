# Code Metrics Report

Path: `benchmarks/Nantuc.Benchmarks.BenchmarkDotNet/Nantuc.Benchmarks.BenchmarkDotNet.csproj`

## Summary

| Metric | Value |
| --- | ---: |
| Members analysed | 6 |
| C# files | 4 |
| Total lines | 418 |
| C# classes | 2 |
| C# records | 1 |
| C# enums | 0 |
| Analysed LOC | 24 |
| Cognitive avg | 0.3 |
| Cognitive median | 0 |
| Cognitive p90 | 2 |
| Cognitive max | 2 |
| Cyclomatic avg | 1.7 |
| Cyclomatic median | 1 |
| Cyclomatic p90 | 4 |
| Cyclomatic max | 4 |
| MI avg | 82.7 |
| MI minimum | 55.3 |

Highest risk member: `NantucBenchmarks.GlobalSetup` (cognitive 2, cyclomatic 4) — `NantucBenchmarks.cs:29`
Lowest maintainability: `NantucBenchmarks.GlobalSetup` (MI 55.3) — `NantucBenchmarks.cs:29`

## Risk bands

| Band | Range | Members |
| --- | --- | ---: |
| Light | 0-5 | 6 |
| Moderate | 6-10 | 0 |
| High | 11-20 | 0 |
| Severe | 21+ | 0 |

## Members

Ranked by cognitive complexity, then cyclomatic complexity. Showing 6 of 6 member(s).

| File | Line | Type | Member | Cognitive | Cyclomatic | LinesOfCode | MaxNesting | Parameters | MaintainabilityIndex |
| --- | ---: | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| C:\Users\xity4\source\repos\Nantuc\benchmarks\Nantuc.Benchmarks.BenchmarkDotNet\NantucBenchmarks.cs | 29 | NantucBenchmarks | GlobalSetup | 2 | 4 | 13 | 1 | 0 | 55.3 |
| C:\Users\xity4\source\repos\Nantuc\benchmarks\Nantuc.Benchmarks.BenchmarkDotNet\NantucBenchmarks.cs | 55 | NantucBenchmarks | GlobalCleanup | 0 | 2 | 1 | 0 | 0 | 93.4 |
| C:\Users\xity4\source\repos\Nantuc\benchmarks\Nantuc.Benchmarks.BenchmarkDotNet\BenchCase.cs | 7 | BenchCase | ToString | 0 | 1 | 1 | 0 | 0 | 97.8 |
| C:\Users\xity4\source\repos\Nantuc\benchmarks\Nantuc.Benchmarks.BenchmarkDotNet\NantucBenchmarks.cs | 27 | NantucBenchmarks | Cases | 0 | 1 | 1 | 0 | 0 | 93.5 |
| C:\Users\xity4\source\repos\Nantuc\benchmarks\Nantuc.Benchmarks.BenchmarkDotNet\NantucBenchmarks.cs | 64 | NantucBenchmarks | RunVm | 0 | 1 | 7 | 1 | 0 | 65.8 |
| C:\Users\xity4\source\repos\Nantuc\benchmarks\Nantuc.Benchmarks.BenchmarkDotNet\Program.cs | 4 | <top-level> | <main> | 0 | 1 | 1 | 0 | 0 | 90.2 |

