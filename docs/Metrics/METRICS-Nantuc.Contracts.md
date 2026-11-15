# Code Metrics Report

Path: `src/Nantuc.Contracts/Nantuc.Contracts.csproj`

## Summary

| Metric | Value |
| --- | ---: |
| Members analysed | 27 |
| C# files | 12 |
| Total lines | 344 |
| C# classes | 1 |
| C# records | 4 |
| C# enums | 0 |
| Analysed LOC | 65 |
| Cognitive avg | 0.6 |
| Cognitive median | 0 |
| Cognitive p90 | 2 |
| Cognitive max | 9 |
| Cyclomatic avg | 1.7 |
| Cyclomatic median | 1 |
| Cyclomatic p90 | 3 |
| Cyclomatic max | 11 |
| MI avg | 85.0 |
| MI minimum | 62.0 |

Highest risk member: `OptimizationId.IsValidId` (cognitive 9, cyclomatic 11) — `OptimizationId.cs:65`
Lowest maintainability: `OptimizationId.IsValidId` (MI 62.0) — `OptimizationId.cs:65`

## Risk bands

| Band | Range | Members |
| --- | --- | ---: |
| Light | 0-5 | 26 |
| Moderate | 6-10 | 1 |
| High | 11-20 | 0 |
| Severe | 21+ | 0 |

## Members

Ranked by cognitive complexity, then cyclomatic complexity. Showing 20 of 27 member(s).

| File | Line | Type | Member | Cognitive | Cyclomatic | LinesOfCode | MaxNesting | Parameters | MaintainabilityIndex |
| --- | ---: | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Contracts\OptimizationId.cs | 65 | OptimizationId | IsValidId | 9 | 11 | 8 | 2 | 1 | 62.0 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Contracts\ProcessorSnapshot.cs | 72 | ProcessorSnapshot | GetArr | 2 | 3 | 8 | 1 | 1 | 63.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Contracts\OptimizationId.cs | 83 | OptimizationId | Equals | 2 | 2 | 2 | 0 | 1 | 83.7 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Contracts\OptimizationId.cs | 40 | OptimizationId | OptimizationId.ctor | 1 | 2 | 6 | 1 | 1 | 68.9 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Contracts\OptimizationId.cs | 53 | OptimizationId | TryParse | 1 | 2 | 5 | 1 | 2 | 72.2 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Contracts\ProcessorSnapshot.cs | 53 | ProcessorSnapshot | GetRegister | 1 | 2 | 1 | 1 | 1 | 88.0 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Contracts\ProcessorSnapshot.cs | 58 | ProcessorSnapshot | DataFault | 1 | 2 | 1 | 1 | 0 | 89.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Contracts\OptimizationId.cs | 86 | OptimizationId | GetHashCode | 0 | 3 | 2 | 0 | 0 | 84.0 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Contracts\OptimizationId.cs | 92 | OptimizationId | ToString | 0 | 2 | 2 | 0 | 0 | 86.8 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Contracts\IProcessorPolicy.cs | 13 | IProcessorPolicy.DefaultProcessorPolicy | ShouldContinue | 0 | 1 | 1 | 0 | 1 | 90.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Contracts\IProcessorPolicy.cs | 15 | IProcessorPolicy | SleepWaitTimeout | 0 | 1 | 1 | 0 | 0 | 92.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Contracts\IProcessorPolicy.cs | 16 | IProcessorPolicy | AllowUnsafeOpCodes | 0 | 1 | 1 | 0 | 0 | 97.8 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Contracts\OptimizationId.cs | 34 | OptimizationId | IsValid | 0 | 1 | 1 | 0 | 0 | 92.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Contracts\OptimizationId.cs | 80 | OptimizationId | Equals | 0 | 1 | 2 | 0 | 1 | 82.7 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Contracts\OptimizationId.cs | 89 | OptimizationId | CompareTo | 0 | 1 | 2 | 0 | 1 | 84.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Contracts\OptimizationId.cs | 99 | OptimizationId | operator == | 0 | 1 | 1 | 0 | 2 | 92.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Contracts\OptimizationId.cs | 100 | OptimizationId | operator != | 0 | 1 | 1 | 0 | 2 | 91.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Contracts\OptimizationId.cs | 101 | OptimizationId | operator < | 0 | 1 | 1 | 0 | 2 | 90.8 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Contracts\OptimizationId.cs | 102 | OptimizationId | operator > | 0 | 1 | 1 | 0 | 2 | 90.8 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Contracts\OptimizationId.cs | 103 | OptimizationId | operator <= | 0 | 1 | 1 | 0 | 2 | 90.8 |

