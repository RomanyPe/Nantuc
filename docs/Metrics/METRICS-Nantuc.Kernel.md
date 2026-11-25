# Code Metrics Report

Path: `src/Nantuc.Kernel/Nantuc.Kernel.csproj`

## Summary

| Metric | Value |
| --- | ---: |
| Members analysed | 173 |
| C# files | 18 |
| Total lines | 2467 |
| C# classes | 19 |
| C# records | 0 |
| C# enums | 0 |
| Analysed LOC | 860 |
| Cognitive avg | 1.2 |
| Cognitive median | 1 |
| Cognitive p90 | 3 |
| Cognitive max | 12 |
| Cyclomatic avg | 2.5 |
| Cyclomatic median | 2 |
| Cyclomatic p90 | 4 |
| Cyclomatic max | 53 |
| MI avg | 76.4 |
| MI minimum | 28.1 |

Highest risk member: `Processor.Step` (cognitive 12, cyclomatic 53) — `Processor.Step.cs:10`
Lowest maintainability: `Processor.Step` (MI 28.1) — `Processor.Step.cs:10`

## Risk bands

| Band | Range | Members |
| --- | --- | ---: |
| Light | 0-5 | 168 |
| Moderate | 6-10 | 4 |
| High | 11-20 | 1 |
| Severe | 21+ | 0 |

## Members

Ranked by cognitive complexity, then cyclomatic complexity. Showing 20 of 173 member(s).

| File | Line | Type | Member | Cognitive | Cyclomatic | LinesOfCode | MaxNesting | Parameters | MaintainabilityIndex |
| --- | ---: | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\ProcessorSystem\Processor.Step.cs | 10 | Processor | Step | 12 | 53 | 68 | 2 | 0 | 28.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\ProcessorSystem\Processor.Memory.cs | 90 | Processor | InstructionBodyLOAD | 9 | 9 | 33 | 2 | 3 | 44.9 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\ProcessorSystem\Processor.Memory.cs | 137 | Processor | InstructionBodySTORE | 9 | 9 | 29 | 2 | 3 | 46.6 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\BiosSystem\Device.cs | 274 | Device | RunSimulationLoopDebug | 7 | 6 | 9 | 2 | 4 | 62.6 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\Utilities\ManagerDevices.cs | 171 | ManagerDevices | TryAddDevice | 6 | 7 | 32 | 1 | 4 | 46.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\BiosSystem\Device.cs | 129 | Device | Run | 5 | 6 | 19 | 1 | 5 | 53.3 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\BiosSystem\Device.cs | 364 | Device | BatchStepDebug | 5 | 5 | 6 | 2 | 2 | 66.9 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\Utilities\ManagerDevices.cs | 88 | ManagerDevices | FindEntry | 5 | 5 | 5 | 2 | 1 | 68.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\BiosSystem\Device.cs | 257 | Device | RunSimulationLoopWithLimit | 5 | 4 | 7 | 2 | 1 | 66.9 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\ProcessorSystem\Processor.ControlFlow.cs | 104 | Processor | InstructionBodyCALL | 4 | 5 | 15 | 1 | 1 | 54.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\ProcessorSystem\Processor.Interrupts.cs | 25 | Processor | ProcessSimulationStatusSlow | 4 | 4 | 6 | 2 | 0 | 65.9 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\ProcessorSystem\Processor.Snapshot.cs | 16 | Processor | CurrentProcessorSnapshot.get | 4 | 4 | 15 | 2 | 0 | 54.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\Utilities\ManagerDevices.cs | 62 | ManagerDevices | Count.get | 4 | 4 | 7 | 2 | 0 | 65.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\Utilities\ManagerDevices.cs | 276 | ManagerDevices | Dispose | 3 | 5 | 13 | 1 | 0 | 56.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\BiosSystem\Device.cs | 105 | Device | TryLoadProgram | 3 | 4 | 8 | 1 | 2 | 63.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\ControllersData\PortBus.cs | 38 | PortBus | PortBus.ctor | 3 | 4 | 11 | 1 | 2 | 59.6 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\ProcessorSystem\Processor.ControlFlow.cs | 25 | Processor | InstructionJZ | 3 | 4 | 7 | 1 | 0 | 64.9 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\ProcessorSystem\Processor.ControlFlow.cs | 39 | Processor | InstructionJNZ | 3 | 4 | 7 | 1 | 0 | 64.8 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\ProcessorSystem\Processor.ControlFlow.cs | 53 | Processor | InstructionJL | 3 | 4 | 7 | 1 | 0 | 64.9 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Kernel\ProcessorSystem\Processor.ControlFlow.cs | 67 | Processor | InstructionJG | 3 | 4 | 7 | 1 | 0 | 64.5 |

