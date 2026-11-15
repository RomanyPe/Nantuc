# Code Metrics Report

Path: `src/Nantuc.Compiler/Nantuc.Compiler.csproj`

## Summary

| Metric | Value |
| --- | ---: |
| Members analysed | 438 |
| C# files | 105 |
| Total lines | 7553 |
| C# classes | 101 |
| C# records | 3 |
| C# enums | 1 |
| Analysed LOC | 3291 |
| Cognitive avg | 3.0 |
| Cognitive median | 1 |
| Cognitive p90 | 10 |
| Cognitive max | 37 |
| Cyclomatic avg | 4.1 |
| Cyclomatic median | 2 |
| Cyclomatic p90 | 10 |
| Cyclomatic max | 46 |
| MI avg | 72.7 |
| MI minimum | 30.5 |

Highest risk member: `VariableAccessAnalyzer.CollectVarEvents` (cognitive 37, cyclomatic 33) — `VariableAccessAnalyzer.cs:102`
Lowest maintainability: `VariableAccessAnalyzer.CollectVarEvents` (MI 30.5) — `VariableAccessAnalyzer.cs:102`

## Risk bands

| Band | Range | Members |
| --- | --- | ---: |
| Light | 0-5 | 355 |
| Moderate | 6-10 | 45 |
| High | 11-20 | 32 |
| Severe | 21+ | 6 |

## Members

Ranked by cognitive complexity, then cyclomatic complexity. Showing 20 of 438 member(s).

| File | Line | Type | Member | Cognitive | Cyclomatic | LinesOfCode | MaxNesting | Parameters | MaintainabilityIndex |
| --- | ---: | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\RegistersAlloc\VariableAccessAnalyzer.cs | 102 | VariableAccessAnalyzer | CollectVarEvents | 37 | 33 | 80 | 3 | 1 | 30.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\RegistersAlloc\VariableAccessAnalyzer.cs | 19 | VariableAccessAnalyzer | CollectAddressTakenVars | 24 | 27 | 56 | 4 | 1 | 35.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Expression\ExpressionGenerator.MemberAccess.cs | 168 | ExpressionGenerator | TryInferStructTypeFromPointerExpr | 22 | 17 | 19 | 3 | 1 | 49.5 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\Lexer\Lexer.cs | 28 | Lexer | Tokenize | 22 | 14 | 17 | 2 | 0 | 51.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\ASM\IR\IROptimizer.cs | 15 | IROptimizer | Optimize | 22 | 9 | 24 | 5 | 2 | 49.6 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Statement\StatementTypeResolver.cs | 136 | StatementTypeResolver | ResolveRootStructType | 21 | 18 | 26 | 3 | 1 | 45.7 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\Parser\Parser.Expressions.cs | 184 | Parser | ParseUnary | 19 | 22 | 23 | 2 | 0 | 46.7 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Expression\ExpressionGenerator.Sizeof.cs | 85 | ExpressionGenerator | GetIndexedElemSizeBytes | 19 | 12 | 15 | 3 | 1 | 52.7 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\ASM\Parser\AssemblerParser.Lexing.cs | 10 | AssemblerParser | ProcessLines | 19 | 10 | 15 | 4 | 1 | 54.3 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Expression\ExpressionGenerator.Binary.cs | 13 | ExpressionGenerator | GenerateBinaryOp | 18 | 19 | 47 | 2 | 2 | 37.7 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Expression\ExpressionGenerator.Sizeof.cs | 43 | ExpressionGenerator | GetVariableTypeSize | 17 | 10 | 14 | 3 | 1 | 53.6 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\StructLayout.cs | 29 | StructLayout | Resolve | 17 | 9 | 22 | 4 | 2 | 49.2 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Expression\ExpressionGenerator.Assignment.cs | 36 | ExpressionGenerator | GenerateAssignmentExpr | 16 | 13 | 48 | 2 | 2 | 38.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Expression\ExpressionGenerator.Conditions.cs | 12 | ExpressionGenerator | GenerateCondition | 16 | 11 | 24 | 2 | 3 | 48.0 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Statement\StatementTypeResolver.cs | 33 | StatementTypeResolver | GetArrayElem | 16 | 11 | 16 | 3 | 1 | 52.2 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\ASM\Parser\AssemblerParser.Lexing.cs | 73 | AssemblerParser | ProcessDataLine | 15 | 9 | 22 | 3 | 1 | 49.4 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Expression\ExpressionGenerator.Calls.cs | 16 | ExpressionGenerator | GenerateFunctionCall | 14 | 11 | 29 | 2 | 1 | 44.1 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Expression\ExpressionGenerator.Increment.cs | 42 | ExpressionGenerator | GenerateIncrementIdent | 14 | 10 | 26 | 2 | 4 | 46.8 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\RegistersAlloc\RegisterAllocator.Interference.cs | 11 | RegisterAllocator | BuildInterferenceGraph | 14 | 9 | 19 | 3 | 2 | 50.2 |
| C:\Users\xity4\source\repos\Nantuc\src\Nantuc.Compiler\C\CodeGenerator\Expression\ExpressionGenerator.Arrays.cs | 254 | ExpressionGenerator | IsArrayElemSigned | 13 | 10 | 8 | 2 | 1 | 60.6 |

