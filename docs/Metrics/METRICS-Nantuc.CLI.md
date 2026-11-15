# Code Metrics Report

Path: `src/Nantuc.CLI/Nantuc.CLI.csproj`

## Summary

| Metric | Value |
| --- | ---: |
| Members analysed | 57 |
| C# files | 14 |
| Total lines | 1013 |
| C# classes | 11 |
| C# records | 0 |
| C# enums | 1 |
| Analysed LOC | 419 |
| Cognitive avg | 1.6 |
| Cognitive median | 1 |
| Cognitive p90 | 4 |
| Cognitive max | 11 |
| Cyclomatic avg | 3.8 |
| Cyclomatic median | 3 |
| Cyclomatic p90 | 7 |
| Cyclomatic max | 27 |
| MI avg | 70.9 |
| MI minimum | 41.5 |

Highest risk member: `LibCommand.Add` (cognitive 11, cyclomatic 8) — `LibCommand.cs:21`
Lowest maintainability: `RunCommandHelpers.TryParseRamSize` (MI 41.5) — `RunCommandHelpers.cs:69`

## Risk bands

| Band | Range | Members |
| --- | --- | ---: |
| Light | 0-5 | 53 |
| Moderate | 6-10 | 3 |
| High | 11-20 | 1 |
| Severe | 21+ | 0 |

## Members

Ranked by cognitive complexity, then cyclomatic complexity. Showing 20 of 57 member(s).

| File | Line | Type | Member | Cognitive | Cyclomatic | LinesOfCode | MaxNesting | Parameters | MaintainabilityIndex |
| --- | ---: | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.CLI\Commands\LibCommand.cs | 21 | LibCommand | Add | 11 | 8 | 27 | 2 | 1 | 47.0 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.CLI\Commands\RunCommand.cs | 171 | RunCommand | TryGetOptionalFilePath | 6 | 7 | 14 | 1 | 6 | 55.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.CLI\Commands\CompileCommand.cs | 38 | CompileCommand | Execute | 6 | 6 | 24 | 2 | 0 | 49.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.CLI\Commands\DisassembleCommand.cs | 15 | DisassembleCommand | TryCreate | 6 | 6 | 16 | 2 | 5 | 53.6 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.CLI\Commands\RunCommand.cs | 52 | RunCommand | Execute | 4 | 5 | 26 | 1 | 0 | 46.8 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.CLI\Commands\RunCommand.cs | 120 | RunCommand | TryCreate | 4 | 5 | 29 | 1 | 5 | 48.2 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.CLI\CLIHelper.cs | 14 | CLIHelper | TryGetArgByFlag | 4 | 4 | 7 | 2 | 3 | 65.2 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.CLI\Commands\LibCommand.cs | 94 | LibCommand | Exist | 3 | 4 | 7 | 1 | 1 | 65.0 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.CLI\CLIHelper.cs | 8 | CLIHelper | HaveFlag | 3 | 3 | 2 | 2 | 2 | 81.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.CLI\CliApplication.cs | 20 | CliApplication | Run | 2 | 11 | 13 | 1 | 1 | 56.8 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.CLI\Commands\RunCommand.cs | 17 | RunCommand | GetDevicePortSize | 2 | 4 | 4 | 1 | 1 | 70.8 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.CLI\Commands\RunCommand.cs | 25 | RunCommand | GetPortSize | 2 | 4 | 4 | 1 | 1 | 70.8 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.CLI\Commands\RunCommand.cs | 34 | RunCommand | GetRamSize | 2 | 4 | 4 | 1 | 1 | 70.8 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.CLI\Commands\RunCommand.cs | 43 | RunCommand | GetLimitSteps | 2 | 4 | 4 | 1 | 1 | 71.0 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.CLI\CliApplication.cs | 42 | CliApplication | RunDisassemble | 2 | 3 | 4 | 1 | 1 | 71.6 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.CLI\CliApplication.cs | 68 | CliApplication | RunCompile | 2 | 3 | 7 | 1 | 1 | 65.9 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.CLI\CliApplication.cs | 84 | CliApplication | RunRun | 2 | 3 | 7 | 1 | 1 | 65.8 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.CLI\Commands\LibCommand.cs | 75 | LibCommand | Remove | 2 | 3 | 9 | 1 | 1 | 62.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.CLI\Commands\LibCommand.cs | 108 | LibCommand | List | 2 | 3 | 7 | 1 | 0 | 66.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.CLI\Commands\RunCommand.cs | 114 | RunCommand | EnsureParentDirectory | 2 | 3 | 3 | 1 | 1 | 74.7 |

