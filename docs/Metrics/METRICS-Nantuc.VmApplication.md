# Code Metrics Report

Path: `src/Nantuc.VmApplication/Nantuc.VmApplication.csproj`

## Summary

| Metric | Value |
| --- | ---: |
| Members analysed | 133 |
| C# files | 30 |
| Total lines | 2089 |
| C# classes | 20 |
| C# records | 8 |
| C# enums | 2 |
| Analysed LOC | 414 |
| Cognitive avg | 0.7 |
| Cognitive median | 0 |
| Cognitive p90 | 2 |
| Cognitive max | 21 |
| Cyclomatic avg | 1.7 |
| Cyclomatic median | 1 |
| Cyclomatic p90 | 3 |
| Cyclomatic max | 16 |
| MI avg | 83.5 |
| MI minimum | 43.8 |

Highest risk member: `RamFileService.Normalize` (cognitive 21, cyclomatic 16) — `RamFileService.cs:80`
Lowest maintainability: `ProjectBuilder.Build` (MI 43.8) — `ProjectBuilder.cs:25`

## Risk bands

| Band | Range | Members |
| --- | --- | ---: |
| Light | 0-5 | 130 |
| Moderate | 6-10 | 2 |
| High | 11-20 | 0 |
| Severe | 21+ | 1 |

## Members

Ranked by cognitive complexity, then cyclomatic complexity. Showing 20 of 133 member(s).

| File | Line | Type | Member | Cognitive | Cyclomatic | LinesOfCode | MaxNesting | Parameters | MaintainabilityIndex |
| --- | ---: | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication\Default\RamFileService.cs | 80 | RamFileService | Normalize | 21 | 16 | 27 | 3 | 1 | 45.3 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication\Project\ProjectBuilder.cs | 25 | ProjectBuilder | Build | 7 | 8 | 32 | 1 | 7 | 43.8 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication\Project\ProjectBuilder.cs | 86 | ProjectBuilder | ResolveIncludesRecursive | 6 | 6 | 17 | 2 | 7 | 53.6 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication\Project\VmHostProject.cs | 159 | VmHostProject | OptimizationIR | 5 | 6 | 15 | 2 | 2 | 54.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication\Default\RamFileService.cs | 63 | RamFileService | HasAnyExtension | 5 | 5 | 5 | 2 | 2 | 69.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication\Project\ProjectBuilder.cs | 130 | ProjectBuilder | ReadInclude | 4 | 5 | 13 | 1 | 4 | 56.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication\Project\VmHostProject.cs | 101 | VmHostProject | CompileInternal | 4 | 4 | 12 | 2 | 5 | 57.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication\Default\RamFileService.cs | 128 | RamFileService | IsUnderPath | 3 | 4 | 6 | 1 | 3 | 67.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication\Emulator\SnapshotRegisters.cs | 63 | SnapshotRegisters | Equals | 3 | 3 | 4 | 2 | 1 | 72.2 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication\Project\ProjectBuilder.cs | 164 | ProjectBuilder | GetSourceFiles | 3 | 3 | 7 | 2 | 2 | 64.8 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication\Project\ProjectBuilder.cs | 191 | ProjectBuilder | HasExtension | 3 | 3 | 4 | 2 | 2 | 73.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication\Project\ProjectBuilder.cs | 200 | ProjectBuilder | FindLang | 3 | 3 | 3 | 1 | 2 | 75.9 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication\Project\VmHostProjectBuilder.cs | 52 | VmHostProjectBuilder | Build | 2 | 4 | 3 | 1 | 0 | 75.0 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication\Default\RamFileService.cs | 28 | RamFileService | RamFileService.ctor | 2 | 3 | 4 | 1 | 2 | 71.3 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication\Emulator\DeviceContext.cs | 112 | DeviceContext | TryLoadProgram | 2 | 3 | 8 | 1 | 3 | 65.0 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication\Emulator\SnapshotRegisters.cs | 23 | SnapshotRegisters | SnapshotRegisters.ctor | 2 | 3 | 3 | 1 | 1 | 74.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication\Project\CompilationResult.cs | 56 | CompilationResult | Success | 2 | 3 | 1 | 0 | 0 | 87.8 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication\Project\CompilationToILResult.cs | 56 | CompilationToIRResult | Build | 2 | 3 | 7 | 1 | 0 | 65.8 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication\Emulator\SnapshotRegisters.cs | 103 | SnapshotRegisters | Equals | 2 | 2 | 1 | 0 | 1 | 90.3 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication\Project\VmHostProject.cs | 234 | VmHostProject | Compile | 2 | 2 | 9 | 2 | 3 | 64.4 |

