# Code Metrics Report

Path: `Nantuc.slnx`

## Summary

| Metric | Value |
| --- | ---: |
| Members analysed | 1455 |
| C# files | 292 |
| Total lines | 26229 |
| C# classes | 251 |
| C# records | 29 |
| C# enums | 13 |
| Analysed LOC | 8943 |
| Cognitive avg | 1.9 |
| Cognitive median | 0 |
| Cognitive p90 | 6 |
| Cognitive max | 53 |
| Cyclomatic avg | 3.0 |
| Cyclomatic median | 1 |
| Cyclomatic p90 | 7 |
| Cyclomatic max | 56 |
| MI avg | 76.3 |
| MI minimum | 25.1 |

Highest risk member: `InterferenceGraph.Build` (cognitive 53, cyclomatic 20) — `InterferenceGraph.cs:10`
Lowest maintainability: `CfgBuilder.Stmt` (MI 25.1) — `CfgBuilder.cs:76`

## Risk bands

| Band | Range | Members |
| --- | --- | ---: |
| Light | 0-5 | 1305 |
| Moderate | 6-10 | 74 |
| High | 11-20 | 61 |
| Severe | 21+ | 15 |

## Members

Ranked by cognitive complexity, then cyclomatic complexity. Showing 20 of 1455 member(s).

| File | Line | Type | Member | Cognitive | Cyclomatic | LinesOfCode | MaxNesting | Parameters | MaintainabilityIndex |
| --- | ---: | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\RegistersAlloc\InterferenceGraph.cs | 10 | InterferenceGraph | Build | 53 | 20 | 25 | 6 | 3 | 44.3 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\RegistersAlloc\RegisterAllocator.cs | 31 | RegisterAllocator | AllocateRegisters | 50 | 24 | 44 | 5 | 3 | 37.3 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\RegistersAlloc\Liveness.cs | 5 | Liveness | Run | 41 | 18 | 27 | 4 | 2 | 44.0 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\RegistersAlloc\CfgBuilder.cs | 76 | CfgBuilder | Stmt | 36 | 27 | 131 | 3 | 1 | 25.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Disassembler\Disassembler.cs | 12 | Disassembler. | Disassemble | 31 | 16 | 70 | 4 | 1 | 33.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\RegistersAlloc\CfgBuilder.cs | 255 | CfgBuilder | Expr | 30 | 23 | 63 | 3 | 2 | 35.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Disassembler\StringDisassembler.cs | 105 | StringDisassembler | WriteSectionData | 26 | 11 | 21 | 4 | 6 | 48.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\RegistersAlloc\VariableAccessAnalyzer.cs | 19 | VariableAccessAnalyzer | CollectAddressTakenVars | 24 | 28 | 59 | 4 | 1 | 34.6 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Optimizations\AstRules\FoldConstantsRule.cs | 70 | FoldConstantsRule | FoldConstantsInExpr | 24 | 18 | 45 | 4 | 2 | 38.7 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Expression\ExpressionGenerator.Binary.cs | 13 | ExpressionGenerator | GenerateBinaryOp | 22 | 26 | 52 | 2 | 2 | 35.2 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Expression\ExpressionGenerator.MemberAccess.cs | 172 | ExpressionGenerator | TryInferStructTypeFromPointerExpr | 22 | 17 | 19 | 3 | 1 | 49.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\Lexer\Lexer.cs | 28 | Lexer | Tokenize | 22 | 14 | 20 | 2 | 0 | 49.6 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\ASM\IR\IROptimizer.cs | 15 | IROptimizer | Optimize | 22 | 9 | 24 | 5 | 2 | 49.6 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Statement\StatementTypeResolver.cs | 138 | StatementTypeResolver | ResolveRootStructType | 21 | 18 | 26 | 3 | 1 | 45.6 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication\Default\RamFileService.cs | 80 | RamFileService | Normalize | 21 | 16 | 27 | 3 | 1 | 45.3 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Optimizations\AstRules\InlineSmallVoidFunctionsRule.cs | 126 | InlineSmallVoidFunctionsRule | CloneStatementWithRename | 20 | 23 | 66 | 2 | 2 | 33.3 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\Parser\Parser.Expressions.cs | 197 | Parser | ParseUnary | 19 | 22 | 23 | 2 | 0 | 46.7 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Expression\ExpressionGenerator.Sizeof.cs | 86 | ExpressionGenerator | GetIndexedElemSizeBytes | 19 | 12 | 15 | 3 | 1 | 52.6 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Optimizations\AstRules\InlineSmallVoidFunctionsRule.cs | 45 | InlineSmallVoidFunctionsRule | InlineCallsInBlock | 19 | 12 | 31 | 3 | 4 | 44.6 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.VmApplication.Extension\Optimizations\AstRules\RemoveUnreachableCodeRule.cs | 33 | RemoveUnreachableCodeRule | OptimizeBlock | 19 | 11 | 35 | 3 | 2 | 44.0 |

