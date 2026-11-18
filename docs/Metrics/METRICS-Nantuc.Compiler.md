# Code Metrics Report

Path: `src/Nantuc.Compiler/Nantuc.Compiler.csproj`

## Summary

| Metric | Value |
| --- | ---: |
| Members analysed | 468 |
| C# files | 112 |
| Total lines | 8364 |
| C# classes | 110 |
| C# records | 4 |
| C# enums | 1 |
| Analysed LOC | 3672 |
| Cognitive avg | 3.4 |
| Cognitive median | 1 |
| Cognitive p90 | 11 |
| Cognitive max | 53 |
| Cyclomatic avg | 4.3 |
| Cyclomatic median | 2 |
| Cyclomatic p90 | 10 |
| Cyclomatic max | 46 |
| MI avg | 72.3 |
| MI minimum | 29.8 |

Highest risk member: `InterferenceGraph.Build` (cognitive 53, cyclomatic 20) — `InterferenceGraph.cs:10`
Lowest maintainability: `CfgBuilder.Stmt` (MI 29.8) — `CfgBuilder.cs:51`

## Risk bands

| Band | Range | Members |
| --- | --- | ---: |
| Light | 0-5 | 371 |
| Moderate | 6-10 | 50 |
| High | 11-20 | 36 |
| Severe | 21+ | 11 |

## Members

Ranked by cognitive complexity, then cyclomatic complexity. Showing 20 of 468 member(s).

| File | Line | Type | Member | Cognitive | Cyclomatic | LinesOfCode | MaxNesting | Parameters | MaintainabilityIndex |
| --- | ---: | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\RegistersAlloc\InterferenceGraph.cs | 10 | InterferenceGraph | Build | 53 | 20 | 25 | 6 | 3 | 44.3 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\RegistersAlloc\LivenessAnalyzer.cs | 5 | LivenessAnalyzer | Run | 41 | 18 | 24 | 4 | 2 | 45.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\RegistersAlloc\RegAllocIr.cs | 36 | Liveness | Run | 41 | 18 | 27 | 4 | 2 | 44.0 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\RegistersAlloc\VariableAccessAnalyzer.cs | 104 | VariableAccessAnalyzer | CollectVarEvents | 37 | 34 | 83 | 3 | 1 | 29.9 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\RegistersAlloc\CfgBuilder.cs | 51 | CfgBuilder | Stmt | 34 | 23 | 89 | 3 | 1 | 29.8 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\RegistersAlloc\CfgBuilder.cs | 186 | CfgBuilder | Expr | 26 | 21 | 37 | 2 | 2 | 40.7 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\RegistersAlloc\VariableAccessAnalyzer.cs | 19 | VariableAccessAnalyzer | CollectAddressTakenVars | 24 | 28 | 59 | 4 | 1 | 34.6 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Expression\ExpressionGenerator.MemberAccess.cs | 172 | ExpressionGenerator | TryInferStructTypeFromPointerExpr | 22 | 17 | 19 | 3 | 1 | 49.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\Lexer\Lexer.cs | 28 | Lexer | Tokenize | 22 | 14 | 20 | 2 | 0 | 49.6 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\ASM\IR\IROptimizer.cs | 15 | IROptimizer | Optimize | 22 | 9 | 24 | 5 | 2 | 49.6 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Statement\StatementTypeResolver.cs | 138 | StatementTypeResolver | ResolveRootStructType | 21 | 18 | 26 | 3 | 1 | 45.6 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\Parser\Parser.Expressions.cs | 197 | Parser | ParseUnary | 19 | 22 | 23 | 2 | 0 | 46.7 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Expression\ExpressionGenerator.Sizeof.cs | 86 | ExpressionGenerator | GetIndexedElemSizeBytes | 19 | 12 | 15 | 3 | 1 | 52.6 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\ASM\Parser\AssemblerParser.Lexing.cs | 10 | AssemblerParser | ProcessLines | 19 | 10 | 16 | 4 | 1 | 53.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Expression\ExpressionGenerator.Binary.cs | 13 | ExpressionGenerator | GenerateBinaryOp | 18 | 19 | 47 | 2 | 2 | 37.7 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\RegistersAlloc\CoalescingColorer.cs | 109 | CoalescingColorer | Coalesce | 18 | 12 | 23 | 4 | 0 | 46.7 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Expression\ExpressionGenerator.Sizeof.cs | 44 | ExpressionGenerator | GetVariableTypeSize | 17 | 10 | 14 | 3 | 1 | 53.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\StructLayout.cs | 29 | StructLayout | Resolve | 17 | 9 | 22 | 4 | 2 | 49.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Expression\ExpressionGenerator.Assignment.cs | 36 | ExpressionGenerator | GenerateAssignmentExpr | 16 | 13 | 48 | 2 | 2 | 38.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Expression\ExpressionGenerator.Conditions.cs | 12 | ExpressionGenerator | GenerateCondition | 16 | 11 | 24 | 2 | 3 | 48.0 |

