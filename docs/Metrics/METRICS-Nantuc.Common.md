# Code Metrics Report

Path: `src/Nantuc.Common/Nantuc.Common.csproj`

## Summary

| Metric | Value |
| --- | ---: |
| Members analysed | 103 |
| C# files | 13 |
| Total lines | 1800 |
| C# classes | 3 |
| C# records | 2 |
| C# enums | 5 |
| Analysed LOC | 139 |
| Cognitive avg | 0.1 |
| Cognitive median | 0 |
| Cognitive p90 | 0 |
| Cognitive max | 2 |
| Cyclomatic avg | 1.2 |
| Cyclomatic median | 1 |
| Cyclomatic p90 | 2 |
| Cyclomatic max | 6 |
| MI avg | 91.4 |
| MI minimum | 56.5 |

Highest risk member: `AlignmentExtensions.IsOutOfRangeException` (cognitive 2, cyclomatic 3) — `AlignmentExtensions.cs:186`
Lowest maintainability: `InstructionDecoder.CreateNeedsUlongOperandTable` (MI 56.5) — `InstructionDecoder.cs:144`

## Risk bands

| Band | Range | Members |
| --- | --- | ---: |
| Light | 0-5 | 103 |
| Moderate | 6-10 | 0 |
| High | 11-20 | 0 |
| Severe | 21+ | 0 |

## Members

Ranked by cognitive complexity, then cyclomatic complexity. Showing 20 of 103 member(s).

| File | Line | Type | Member | Cognitive | Cyclomatic | LinesOfCode | MaxNesting | Parameters | MaintainabilityIndex |
| --- | ---: | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\AlignmentExtensions.cs | 186 | AlignmentExtensions | IsOutOfRangeException | 2 | 3 | 2 | 1 | 1 | 81.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\DevicePortSize.cs | 79 | DevicePortSize | Equals | 2 | 2 | 1 | 0 | 1 | 90.3 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\PortSize.cs | 115 | PortSize | Equals | 2 | 2 | 1 | 0 | 1 | 90.3 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\RamSize.cs | 153 | RamSize | Equals | 2 | 2 | 1 | 0 | 1 | 90.3 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\IdStateExtension.cs | 76 | IdStateExtension. | ToIdState | 1 | 6 | 7 | 1 | 0 | 65.3 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\AlignmentExtensions.cs | 150 | AlignmentExtensions. | AlignUpArithmetic | 1 | 2 | 5 | 1 | 1 | 70.3 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\DevicePortSize.cs | 109 | DevicePortSize | ToString | 1 | 2 | 1 | 1 | 0 | 88.7 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\InstructionDecoder.cs | 128 | InstructionDecoder | HasNeed64IntData | 1 | 2 | 2 | 1 | 1 | 80.0 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\PortSize.cs | 130 | PortSize | ToString | 1 | 2 | 2 | 1 | 0 | 82.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\RamSize.cs | 168 | RamSize | ToString | 1 | 2 | 2 | 1 | 0 | 82.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\DevicePortSize.cs | 35 | DevicePortSize | IsValid | 0 | 2 | 1 | 0 | 0 | 90.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\PortSize.cs | 56 | PortSize | IsValid | 0 | 2 | 1 | 0 | 0 | 90.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\RamSize.cs | 56 | RamSize | IsValid | 0 | 2 | 1 | 0 | 0 | 90.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\AlignmentExtensions.cs | 75 | AlignmentExtensions. | AlignUp | 0 | 1 | 2 | 0 | 1 | 80.9 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\AlignmentExtensions.cs | 110 | AlignmentExtensions. | AlignDown | 0 | 1 | 2 | 0 | 1 | 82.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\AlignmentExtensions.cs | 204 | AlignmentExtensions | GenerateOutOfRangeException | 0 | 1 | 1 | 0 | 1 | 92.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\AlignmentExtensions.cs | 226 | AlignmentExtensions | IsPowerOfTwo | 0 | 1 | 1 | 0 | 1 | 88.8 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\DevicePortSize.cs | 32 | DevicePortSize | Bytes | 0 | 1 | 1 | 0 | 0 | 93.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\DevicePortSize.cs | 39 | DevicePortSize | DevicePortSize.ctor | 0 | 1 | 1 | 0 | 1 | 93.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Common\DevicePortSize.cs | 42 | DevicePortSize | B8 | 0 | 1 | 1 | 0 | 0 | 95.1 |

