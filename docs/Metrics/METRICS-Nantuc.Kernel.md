# Code Metrics Report

Path: `src/Nantuc.Kernel/Nantuc.Kernel.csproj`

## Summary

| Metric | Value |
| --- | ---: |
| Members analysed | 175 |
| C# files | 8 |
| Total lines | 2583 |
| C# classes | 9 |
| C# records | 0 |
| C# enums | 0 |
| Analysed LOC | 869 |
| Cognitive avg | 1.2 |
| Cognitive median | 0 |
| Cognitive p90 | 3 |
| Cognitive max | 12 |
| Cyclomatic avg | 2.5 |
| Cyclomatic median | 2 |
| Cyclomatic p90 | 4 |
| Cyclomatic max | 53 |
| MI avg | 76.7 |
| MI minimum | 28.1 |

Highest risk member: `Processor.Step` (cognitive 12, cyclomatic 53) — `Processor.cs:214`
Lowest maintainability: `Processor.Step` (MI 28.1) — `Processor.cs:214`

## Risk bands

| Band | Range | Members |
| --- | --- | ---: |
| Light | 0-5 | 168 |
| Moderate | 6-10 | 5 |
| High | 11-20 | 2 |
| Severe | 21+ | 0 |

## Members

Ranked by cognitive complexity, then cyclomatic complexity. Showing 20 of 175 member(s).

| File | Line | Type | Member | Cognitive | Cyclomatic | LinesOfCode | MaxNesting | Parameters | MaintainabilityIndex |
| --- | ---: | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\ProcessorSystem\Processor.cs | 214 | Processor | Step | 12 | 53 | 68 | 2 | 0 | 28.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\BiosSystem\Device.cs | 245 | Device | RunSimulationLoopWithLimit | 11 | 6 | 13 | 3 | 1 | 58.9 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\BiosSystem\Device.cs | 273 | Device | RunSimulationLoopDebug | 10 | 8 | 14 | 2 | 4 | 56.8 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\ProcessorSystem\Processor.cs | 794 | Processor | InstructionBodyLOAD | 9 | 9 | 33 | 2 | 3 | 44.9 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\ProcessorSystem\Processor.cs | 845 | Processor | InstructionBodySTORE | 9 | 9 | 29 | 2 | 3 | 46.6 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\Utilites\ManagerDevices.cs | 146 | ManagerDevices | TryAddDevice | 6 | 7 | 32 | 1 | 4 | 46.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\BiosSystem\Device.cs | 377 | Device | NextStepProcessorCount | 6 | 5 | 8 | 2 | 2 | 63.7 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\BiosSystem\Device.cs | 130 | Device | Run | 5 | 6 | 17 | 1 | 5 | 54.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\Utilites\ManagerDevices.cs | 63 | ManagerDevices | FindEntry | 5 | 5 | 5 | 2 | 1 | 68.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\ProcessorSystem\Processor.cs | 722 | Processor | InstructionBodyCALL | 4 | 5 | 15 | 1 | 1 | 54.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\ProcessorSystem\Processor.cs | 188 | Processor | ProcessSimulationStatusSlow | 4 | 4 | 6 | 2 | 0 | 65.9 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\ProcessorSystem\Processor.cs | 1031 | Processor | CurrentProcessorSnapshot.get | 4 | 4 | 15 | 2 | 0 | 54.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\Utilites\ManagerDevices.cs | 52 | ManagerDevices | Count.get | 4 | 4 | 6 | 2 | 0 | 67.3 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\BiosSystem\Device.cs | 228 | Device | RunSimulationLoop | 4 | 3 | 7 | 2 | 0 | 68.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\ControllersData\PortBus.cs | 42 | PortBus | PortBus.ctor | 3 | 4 | 14 | 1 | 2 | 55.8 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\ProcessorSystem\Processor.cs | 622 | Processor | InstructionJZ | 3 | 4 | 7 | 1 | 0 | 64.9 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\ProcessorSystem\Processor.cs | 635 | Processor | InstructionJNZ | 3 | 4 | 7 | 1 | 0 | 64.8 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\ProcessorSystem\Processor.cs | 648 | Processor | InstructionJL | 3 | 4 | 7 | 1 | 0 | 64.9 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\ProcessorSystem\Processor.cs | 661 | Processor | InstructionJG | 3 | 4 | 7 | 1 | 0 | 64.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\ProcessorSystem\Processor.cs | 675 | Processor | InstructionPUSH | 3 | 4 | 11 | 1 | 1 | 58.3 |

