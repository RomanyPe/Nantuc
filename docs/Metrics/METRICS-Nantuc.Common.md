# Code Metrics Report

Path: `src/Nantuc.Common/Nantuc.Common.csproj`

## Summary

| Metric | Value |
| --- | ---: |
| Members analysed | 110 |
| C# files | 17 |
| Total lines | 1947 |
| C# classes | 7 |
| C# records | 2 |
| C# enums | 5 |
| Analysed LOC | 245 |
| Cognitive avg | 0.2 |
| Cognitive median | 0 |
| Cognitive p90 | 1 |
| Cognitive max | 2 |
| Cyclomatic avg | 2.1 |
| Cyclomatic median | 1 |
| Cyclomatic p90 | 2 |
| Cyclomatic max | 46 |
| MI avg | 90.0 |
| MI minimum | 34.0 |

Highest risk member: `AlignmentExtensions.ThrowIfInvalidAlignment` (cognitive 2, cyclomatic 3) — `AlignmentExtensions.cs:186`
Lowest maintainability: `OpCodeExtension..OpName` (MI 34.0) — `OpCodeExtension.cs:12`

## Risk bands

| Band | Range | Members |
| --- | --- | ---: |
| Light | 0-5 | 110 |
| Moderate | 6-10 | 0 |
| High | 11-20 | 0 |
| Severe | 21+ | 0 |

## Members

Ranked by cognitive complexity, then cyclomatic complexity. Showing 20 of 110 member(s).

| File | Line | Type | Member | Cognitive | Cyclomatic | LinesOfCode | MaxNesting | Parameters | MaintainabilityIndex |
| --- | ---: | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\AlignmentExtensions.cs | 186 | AlignmentExtensions | ThrowIfInvalidAlignment | 2 | 3 | 2 | 1 | 1 | 81.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\DevicePortSize.cs | 79 | DevicePortSize | Equals | 2 | 2 | 1 | 0 | 1 | 90.3 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\PortSize.cs | 115 | PortSize | Equals | 2 | 2 | 1 | 0 | 1 | 90.3 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\RamSize.cs | 153 | RamSize | Equals | 2 | 2 | 1 | 0 | 1 | 90.3 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\OpCodeExtension.cs | 12 | OpCodeExtension. | OpName | 1 | 46 | 47 | 1 | 0 | 34.0 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\RegTypeExtension.cs | 22 | RegTypeExtension. | RegName | 1 | 33 | 34 | 1 | 0 | 40.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\BiosStatusExtension.cs | 14 | BiosStatusExtension. | StatusName | 1 | 14 | 15 | 1 | 0 | 53.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\InstructionDecoder.cs | 118 | InstructionDecoder | HasNeed64IntData | 1 | 12 | 14 | 1 | 1 | 56.6 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\IdStateExtension.cs | 76 | IdStateExtension. | ToIdState | 1 | 6 | 7 | 1 | 0 | 65.3 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\OpCodeSizeExtension.cs | 11 | OpCodeSizeExtension. | SizeName | 1 | 5 | 6 | 1 | 0 | 67.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\AlignmentExtensions.cs | 150 | AlignmentExtensions. | AlignUpArithmetic | 1 | 2 | 5 | 1 | 1 | 70.3 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\DevicePortSize.cs | 109 | DevicePortSize | ToString | 1 | 2 | 1 | 1 | 0 | 88.7 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\PortSize.cs | 130 | PortSize | ToString | 1 | 2 | 2 | 1 | 0 | 82.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\RamSize.cs | 168 | RamSize | ToString | 1 | 2 | 2 | 1 | 0 | 82.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\DevicePortSize.cs | 35 | DevicePortSize | IsValid | 0 | 2 | 1 | 0 | 0 | 90.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\PortSize.cs | 56 | PortSize | IsValid | 0 | 2 | 1 | 0 | 0 | 90.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\RamSize.cs | 56 | RamSize | IsValid | 0 | 2 | 1 | 0 | 0 | 90.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\AlignmentExtensions.cs | 75 | AlignmentExtensions. | AlignUp | 0 | 1 | 2 | 0 | 1 | 80.9 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\AlignmentExtensions.cs | 110 | AlignmentExtensions. | AlignDown | 0 | 1 | 2 | 0 | 1 | 82.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\AlignmentExtensions.cs | 204 | AlignmentExtensions | GenerateOutOfRangeException | 0 | 1 | 1 | 0 | 1 | 92.4 |

