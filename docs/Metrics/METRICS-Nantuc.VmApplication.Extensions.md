# Code Metrics Report

Path: `src/Nantuc.VmApplication.Extension/Nantuc.VmApplication.Extensions.csproj`

## Summary

| Metric | Value |
| --- | ---: |
| Members analysed | 294 |
| C# files | 40 |
| Total lines | 5266 |
| C# classes | 35 |
| C# records | 7 |
| C# enums | 4 |
| Analysed LOC | 2027 |
| Cognitive avg | 2.4 |
| Cognitive median | 0 |
| Cognitive p90 | 8 |
| Cognitive max | 31 |
| Cyclomatic avg | 3.2 |
| Cyclomatic median | 1 |
| Cyclomatic p90 | 9 |
| Cyclomatic max | 56 |
| MI avg | 75.9 |
| MI minimum | 27.5 |

Highest risk member: `Disassembler..Disassemble` (cognitive 31, cyclomatic 16) — `Disassembler.cs:12`
Lowest maintainability: `IRLog.FormatAsmItem` (MI 27.5) — `IRLog.cs:22`

## Risk bands

| Band | Range | Members |
| --- | --- | ---: |
| Light | 0-5 | 256 |
| Moderate | 6-10 | 12 |
| High | 11-20 | 23 |
| Severe | 21+ | 3 |

## Members

Ranked by cognitive complexity, then cyclomatic complexity. Showing 20 of 294 member(s).

| File | Line | Type | Member | Cognitive | Cyclomatic | LinesOfCode | MaxNesting | Parameters | MaintainabilityIndex |
| --- | ---: | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Disassembler\Disassembler.cs | 12 | Disassembler. | Disassemble | 31 | 16 | 70 | 4 | 1 | 33.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Disassembler\StringDisassembler.cs | 105 | StringDisassembler | WriteSectionData | 26 | 11 | 21 | 4 | 6 | 48.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Optimizations\AstRules\FoldConstantsRule.cs | 70 | FoldConstantsRule | FoldConstantsInExpr | 24 | 18 | 45 | 4 | 2 | 38.7 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Optimizations\AstRules\InlineSmallVoidFunctionsRule.cs | 126 | InlineSmallVoidFunctionsRule | CloneStatementWithRename | 20 | 23 | 66 | 2 | 2 | 33.3 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Optimizations\AstRules\InlineSmallVoidFunctionsRule.cs | 45 | InlineSmallVoidFunctionsRule | InlineCallsInBlock | 19 | 12 | 31 | 3 | 4 | 44.6 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Optimizations\AstRules\RemoveUnreachableCodeRule.cs | 33 | RemoveUnreachableCodeRule | OptimizeBlock | 19 | 11 | 35 | 3 | 2 | 44.0 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Optimizations\AstRules\RemoveUnreachableCodeRule.cs | 89 | RemoveUnreachableCodeRule | SimplifyControlFlow | 18 | 11 | 22 | 4 | 2 | 48.8 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Optimizations\AstRules\RemoveUnusedVariablesRule.cs | 30 | RemoveUnusedVariablesRule | CollectUsedVariables | 16 | 14 | 39 | 3 | 2 | 42.8 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Optimizations\AstRules\FoldConstantsRule.cs | 30 | FoldConstantsRule | FoldConstantsInBlock | 16 | 12 | 32 | 3 | 2 | 44.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Optimizations\AstRules\RemoveUnusedVariablesRule.cs | 109 | RemoveUnusedVariablesRule | RemoveUnusedFromBlock | 16 | 10 | 19 | 3 | 3 | 51.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Optimizations\AstRules\PropagateConstantsRule.cs | 40 | PropagateConstantsRule | CollectAssignedVariables | 16 | 9 | 20 | 3 | 2 | 51.9 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Optimizations\IRAsmRules\ReplaceSafeMemAccessWithUnsafeRule.cs | 18 | ReplaceSafeMemAccessWithUnsafeRule | TryOptimize | 15 | 19 | 37 | 2 | 4 | 41.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Debugging\Debugger.cs | 168 | Debugger | Continue | 15 | 13 | 22 | 3 | 1 | 49.2 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Optimizations\IRAsmRules\ConstantFoldingRule.cs | 15 | ConstantFoldingRule | TryOptimize | 14 | 19 | 38 | 1 | 4 | 40.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Optimizations\AstRules\PropagateConstantsRule.cs | 68 | PropagateConstantsRule | CollectConstantsFromBlock | 14 | 10 | 18 | 3 | 3 | 52.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Debugging\Debugger.cs | 104 | Debugger | StepOver | 13 | 13 | 20 | 2 | 0 | 50.3 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Host\HostExtensions.cs | 32 | VMHostRunExtensions. | RunAndKeep | 13 | 13 | 34 | 2 | 8 | 41.7 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Host\HostExtensions.cs | 129 | VMHostRunExtensions. | AsyncRunAndKeep | 13 | 13 | 34 | 2 | 9 | 41.6 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Optimizations\AstRules\PropagateConstantsRule.cs | 94 | PropagateConstantsRule | ReplaceVariablesWithConstants | 13 | 11 | 32 | 3 | 2 | 44.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Bytecode\BytecodeExtension.cs | 18 | BytecodeExtension. | GetOpCodeSafety | 13 | 10 | 32 | 3 | 0 | 45.0 |

