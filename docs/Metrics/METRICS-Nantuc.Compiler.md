# Code Metrics Report

Path: `src/Nantuc.Compiler/Nantuc.Compiler.csproj`

## Summary

| Metric | Value |
| --- | ---: |
| Members analysed | 423 |
| C# files | 99 |
| Total lines | 7480 |
| C# classes | 95 |
| C# records | 3 |
| C# enums | 1 |
| Analysed LOC | 3311 |
| Cognitive avg | 3.2 |
| Cognitive median | 1 |
| Cognitive p90 | 10 |
| Cognitive max | 69 |
| Cyclomatic avg | 4.3 |
| Cyclomatic median | 2 |
| Cyclomatic p90 | 10 |
| Cyclomatic max | 50 |
| MI avg | 72.7 |
| MI minimum | 28.4 |

Highest risk member: `AssemblerParser.ProcessLines` (cognitive 69, cyclomatic 26) — `AssemblerParser.cs:131`
Lowest maintainability: `AssemblerParser.ParseInstruction` (MI 28.4) — `AssemblerParser.cs:387`

## Risk bands

| Band | Range | Members |
| --- | --- | ---: |
| Light | 0-5 | 340 |
| Moderate | 6-10 | 45 |
| High | 11-20 | 30 |
| Severe | 21+ | 8 |

## Members

Ranked by cognitive complexity, then cyclomatic complexity. Showing 20 of 423 member(s).

| File | Line | Type | Member | Cognitive | Cyclomatic | LinesOfCode | MaxNesting | Parameters | MaintainabilityIndex |
| --- | ---: | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\ASM\AssemblerParser.cs | 131 | AssemblerParser | ProcessLines | 69 | 26 | 65 | 5 | 1 | 33.6 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\RegistersAlloc\VariableAccessAnalyzer.cs | 102 | VariableAccessAnalyzer | CollectVarEvents | 37 | 33 | 80 | 3 | 1 | 30.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\ASM\IR\IRAssembler.cs | 75 | IRAssembler | Build | 36 | 20 | 62 | 5 | 0 | 34.6 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\RegistersAlloc\VariableAccessAnalyzer.cs | 19 | VariableAccessAnalyzer | CollectAddressTakenVars | 24 | 27 | 56 | 4 | 1 | 35.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Expression\ExpressionGenerator.MemberAccess.cs | 168 | ExpressionGenerator | TryInferStructTypeFromPointerExpr | 22 | 17 | 19 | 3 | 1 | 49.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\Lexer\Lexer.cs | 28 | Lexer | Tokenize | 22 | 14 | 17 | 2 | 0 | 51.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\ASM\IR\IROptimizer.cs | 15 | IROptimizer | Optimize | 22 | 9 | 24 | 5 | 2 | 49.6 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Statement\StatementTypeResolver.cs | 136 | StatementTypeResolver | ResolveRootStructType | 21 | 18 | 26 | 3 | 1 | 45.7 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\Parser\Parser.Expressions.cs | 184 | Parser | ParseUnary | 19 | 22 | 23 | 2 | 0 | 46.7 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Expression\ExpressionGenerator.Sizeof.cs | 85 | ExpressionGenerator | GetIndexedElemSizeBytes | 19 | 12 | 15 | 3 | 1 | 52.7 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Expression\ExpressionGenerator.Binary.cs | 13 | ExpressionGenerator | GenerateBinaryOp | 18 | 19 | 47 | 2 | 2 | 37.7 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Expression\ExpressionGenerator.Sizeof.cs | 43 | ExpressionGenerator | GetVariableTypeSize | 17 | 10 | 14 | 3 | 1 | 53.6 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\StructLayout.cs | 29 | StructLayout | Resolve | 17 | 9 | 22 | 4 | 2 | 49.2 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Expression\ExpressionGenerator.Assignment.cs | 36 | ExpressionGenerator | GenerateAssignmentExpr | 16 | 13 | 48 | 2 | 2 | 38.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Expression\ExpressionGenerator.Conditions.cs | 12 | ExpressionGenerator | GenerateCondition | 16 | 11 | 24 | 2 | 3 | 48.0 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Statement\StatementTypeResolver.cs | 33 | StatementTypeResolver | GetArrayElem | 16 | 11 | 16 | 3 | 1 | 52.2 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Expression\ExpressionGenerator.Calls.cs | 16 | ExpressionGenerator | GenerateFunctionCall | 14 | 11 | 29 | 2 | 1 | 44.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Expression\ExpressionGenerator.Increment.cs | 42 | ExpressionGenerator | GenerateIncrementIdent | 14 | 10 | 26 | 2 | 4 | 46.8 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\RegistersAlloc\RegisterAllocator.Interference.cs | 11 | RegisterAllocator | BuildInterferenceGraph | 14 | 9 | 19 | 3 | 2 | 50.2 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Expression\ExpressionGenerator.Arrays.cs | 254 | ExpressionGenerator | IsArrayElemSigned | 13 | 10 | 8 | 2 | 1 | 60.6 |

