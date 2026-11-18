# Code Metrics Report

Path: `Nantuc.slnx`

## Summary

| Metric | Value |
| --- | ---: |
| Members analysed | 1425 |
| C# files | 285 |
| Total lines | 25520 |
| C# classes | 242 |
| C# records | 28 |
| C# enums | 13 |
| Analysed LOC | 8573 |
| Cognitive avg | 1.8 |
| Cognitive median | 0 |
| Cognitive p90 | 5 |
| Cognitive max | 37 |
| Cyclomatic avg | 2.9 |
| Cyclomatic median | 1 |
| Cyclomatic p90 | 6 |
| Cyclomatic max | 56 |
| MI avg | 76.5 |
| MI minimum | 27.5 |

Highest risk member: `VariableAccessAnalyzer.CollectVarEvents` (cognitive 37, cyclomatic 34) — `VariableAccessAnalyzer.cs:104`
Lowest maintainability: `IRLog.FormatAsmItem` (MI 27.5) — `IRLog.cs:22`

## Risk bands

| Band | Range | Members |
| --- | --- | ---: |
| Light | 0-5 | 1284 |
| Moderate | 6-10 | 71 |
| High | 11-20 | 60 |
| Severe | 21+ | 10 |

## Members

Ranked by cognitive complexity, then cyclomatic complexity. Showing 20 of 1425 member(s).

| File | Line | Type | Member | Cognitive | Cyclomatic | LinesOfCode | MaxNesting | Parameters | MaintainabilityIndex |
| --- | ---: | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\RegistersAlloc\VariableAccessAnalyzer.cs | 104 | VariableAccessAnalyzer | CollectVarEvents | 37 | 34 | 83 | 3 | 1 | 29.9 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Disassembler\Disassembler.cs | 12 | Disassembler. | Disassemble | 31 | 16 | 70 | 4 | 1 | 33.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Disassembler\StringDisassembler.cs | 105 | StringDisassembler | WriteSectionData | 26 | 11 | 21 | 4 | 6 | 48.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\RegistersAlloc\VariableAccessAnalyzer.cs | 19 | VariableAccessAnalyzer | CollectAddressTakenVars | 24 | 28 | 59 | 4 | 1 | 34.6 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Optimizations\AstRules\FoldConstantsRule.cs | 70 | FoldConstantsRule | FoldConstantsInExpr | 24 | 18 | 45 | 4 | 2 | 38.7 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Expression\ExpressionGenerator.MemberAccess.cs | 168 | ExpressionGenerator | TryInferStructTypeFromPointerExpr | 22 | 17 | 19 | 3 | 1 | 49.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\Lexer\Lexer.cs | 28 | Lexer | Tokenize | 22 | 14 | 17 | 2 | 0 | 51.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\ASM\IR\IROptimizer.cs | 15 | IROptimizer | Optimize | 22 | 9 | 24 | 5 | 2 | 49.6 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Statement\StatementTypeResolver.cs | 136 | StatementTypeResolver | ResolveRootStructType | 21 | 18 | 26 | 3 | 1 | 45.7 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication\Default\RamFileService.cs | 80 | RamFileService | Normalize | 21 | 16 | 27 | 3 | 1 | 45.3 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Optimizations\AstRules\InlineSmallVoidFunctionsRule.cs | 126 | InlineSmallVoidFunctionsRule | CloneStatementWithRename | 20 | 23 | 66 | 2 | 2 | 33.3 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\Parser\Parser.Expressions.cs | 197 | Parser | ParseUnary | 19 | 22 | 23 | 2 | 0 | 46.7 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Expression\ExpressionGenerator.Sizeof.cs | 85 | ExpressionGenerator | GetIndexedElemSizeBytes | 19 | 12 | 15 | 3 | 1 | 52.7 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Optimizations\AstRules\InlineSmallVoidFunctionsRule.cs | 45 | InlineSmallVoidFunctionsRule | InlineCallsInBlock | 19 | 12 | 31 | 3 | 4 | 44.6 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Optimizations\AstRules\RemoveUnreachableCodeRule.cs | 33 | RemoveUnreachableCodeRule | OptimizeBlock | 19 | 11 | 35 | 3 | 2 | 44.0 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\ASM\Parser\AssemblerParser.Lexing.cs | 10 | AssemblerParser | ProcessLines | 19 | 10 | 15 | 4 | 1 | 54.3 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Expression\ExpressionGenerator.Binary.cs | 13 | ExpressionGenerator | GenerateBinaryOp | 18 | 19 | 47 | 2 | 2 | 37.7 |
| C:\Users\xity4\source\repos\Nantuc\samples\Nantuc.Samples.Scratch\SnakeGame\SnakeGameOnNantuc.cs | 15 | SnakeGameOnNantuc | Run | 18 | 16 | 52 | 3 | 0 | 36.9 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Optimizations\AstRules\RemoveUnreachableCodeRule.cs | 89 | RemoveUnreachableCodeRule | SimplifyControlFlow | 18 | 11 | 22 | 4 | 2 | 48.8 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Expression\ExpressionGenerator.Sizeof.cs | 43 | ExpressionGenerator | GetVariableTypeSize | 17 | 10 | 14 | 3 | 1 | 53.6 |

