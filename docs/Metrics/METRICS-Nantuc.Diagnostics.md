# Code Metrics Report

Path: `src/Nantuc.Diagnostics/Nantuc.Diagnostics.csproj`

## Summary

| Metric | Value |
| --- | ---: |
| Members analysed | 19 |
| C# files | 6 |
| Total lines | 519 |
| C# classes | 3 |
| C# records | 1 |
| C# enums | 1 |
| Analysed LOC | 33 |
| Cognitive avg | 0.3 |
| Cognitive median | 0 |
| Cognitive p90 | 2 |
| Cognitive max | 2 |
| Cyclomatic avg | 1.5 |
| Cyclomatic median | 1 |
| Cyclomatic p90 | 3 |
| Cyclomatic max | 4 |
| MI avg | 88.5 |
| MI minimum | 62.6 |

Highest risk member: `DefaultErrorMessageProvider.GetMessage` (cognitive 2, cyclomatic 4) — `DefaultErrorMessageProvider.cs:102`
Lowest maintainability: `DefaultErrorMessageProvider.GetMessage` (MI 62.6) — `DefaultErrorMessageProvider.cs:102`

## Risk bands

| Band | Range | Members |
| --- | --- | ---: |
| Light | 0-5 | 19 |
| Moderate | 6-10 | 0 |
| High | 11-20 | 0 |
| Severe | 21+ | 0 |

## Members

Ranked by cognitive complexity, then cyclomatic complexity. Showing 19 of 19 member(s).

| File | Line | Type | Member | Cognitive | Cyclomatic | LinesOfCode | MaxNesting | Parameters | MaintainabilityIndex |
| --- | ---: | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Diagnostics\DefaultErrorMessageProvider.cs | 102 | DefaultErrorMessageProvider | GetMessage | 2 | 4 | 8 | 1 | 2 | 62.6 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Diagnostics\DefaultErrorMessageProvider.cs | 82 | DefaultErrorMessageProvider | MergeTemplates | 2 | 3 | 6 | 1 | 2 | 66.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Diagnostics\DefaultErrorMessageProvider.cs | 97 | DefaultErrorMessageProvider | TryGetTemplate | 1 | 2 | 1 | 1 | 1 | 88.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Diagnostics\DiagnosticData.cs | 16 | DiagnosticData | Of | 1 | 2 | 1 | 1 | 1 | 90.7 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Diagnostics\DiagnosticData.cs | 11 | DiagnosticData | DiagnosticData.ctor | 0 | 2 | 1 | 0 | 1 | 90.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Diagnostics\DiagnosticData.cs | 17 | DiagnosticData | Of | 0 | 2 | 1 | 0 | 1 | 91.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Diagnostics\DiagnosticData.cs | 19 | DiagnosticData | Of | 0 | 2 | 1 | 0 | 1 | 90.7 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Diagnostics\CodeException.cs | 17 | CodeException | NumericCode | 0 | 1 | 1 | 0 | 0 | 95.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Diagnostics\DefaultErrorMessageProvider.cs | 66 | DefaultErrorMessageProvider | DefaultTemplates | 0 | 1 | 1 | 0 | 0 | 97.8 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Diagnostics\DefaultErrorMessageProvider.cs | 71 | DefaultErrorMessageProvider | Templates | 0 | 1 | 1 | 0 | 0 | 97.8 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Diagnostics\DiagnosticData.cs | 13 | DiagnosticData | ToString | 0 | 1 | 1 | 0 | 0 | 97.8 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Diagnostics\DiagnosticData.cs | 22 | DiagnosticData | operator DiagnosticData | 0 | 1 | 1 | 0 | 1 | 95.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Diagnostics\DiagnosticData.cs | 23 | DiagnosticData | operator DiagnosticData | 0 | 1 | 1 | 0 | 1 | 92.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Diagnostics\DiagnosticData.cs | 24 | DiagnosticData | operator DiagnosticData | 0 | 1 | 1 | 0 | 1 | 90.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Diagnostics\DiagnosticData.cs | 25 | DiagnosticData | operator DiagnosticData | 0 | 1 | 1 | 0 | 1 | 90.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Diagnostics\DiagnosticData.cs | 26 | DiagnosticData | operator DiagnosticData | 0 | 1 | 1 | 0 | 1 | 90.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Diagnostics\DiagnosticData.cs | 27 | DiagnosticData | operator DiagnosticData | 0 | 1 | 1 | 0 | 1 | 90.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Diagnostics\ThrowCompiler.cs | 56 | ErrorReporter | Throw | 0 | 1 | 2 | 0 | 2 | 81.7 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Diagnostics\ThrowCompiler.cs | 82 | ErrorReporter | Throw | 0 | 1 | 2 | 0 | 2 | 81.7 |

