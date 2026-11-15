# Code Metrics Report

Path: `tests/Nantuc.CLI.Tests/Nantuc.CLI.Tests.csproj`

## Summary

| Metric | Value |
| --- | ---: |
| Members analysed | 50 |
| C# files | 6 |
| Total lines | 621 |
| C# classes | 5 |
| C# records | 1 |
| C# enums | 0 |
| Analysed LOC | 281 |
| Cognitive avg | 0.1 |
| Cognitive median | 0 |
| Cognitive p90 | 1 |
| Cognitive max | 1 |
| Cyclomatic avg | 1.1 |
| Cyclomatic median | 1 |
| Cyclomatic p90 | 2 |
| Cyclomatic max | 2 |
| MI avg | 70.8 |
| MI minimum | 55.6 |

Highest risk member: `CLITest.DumpOnFailure` (cognitive 1, cyclomatic 2) — `CLITest.cs:101`
Lowest maintainability: `DisassembleCommandTests.Execute_WithSave_WritesFileIntoGivenDirectory` (MI 55.6) — `DisassembleCommandTests.cs:130`

## Risk bands

| Band | Range | Members |
| --- | --- | ---: |
| Light | 0-5 | 50 |
| Moderate | 6-10 | 0 |
| High | 11-20 | 0 |
| Severe | 21+ | 0 |

## Members

Ranked by cognitive complexity, then cyclomatic complexity. Showing 20 of 50 member(s).

| File | Line | Type | Member | Cognitive | Cyclomatic | LinesOfCode | MaxNesting | Parameters | MaintainabilityIndex |
| --- | ---: | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.CLI.Tests\CLITest.cs | 101 | CLITest | DumpOnFailure | 1 | 2 | 6 | 1 | 3 | 68.1 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.CLI.Tests\CliTestContext.cs | 7 | CliTestContext | DefaultRoot | 1 | 2 | 3 | 1 | 0 | 79.7 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.CLI.Tests\DisassembleCommandTests.cs | 114 | DisassembleCommandTests | Execute_NoSave_WritesDisassemblyToOutput | 1 | 2 | 9 | 0 | 0 | 60.8 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.CLI.Tests\DisassembleCommandTests.cs | 174 | DisassembleCommandTests | CreateRequired | 1 | 2 | 3 | 1 | 4 | 74.1 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.CLI.Tests\DisassembleCommandTests.cs | 182 | DisassembleCommandTests | DumpOnFailure | 1 | 2 | 5 | 1 | 4 | 71.2 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.CLI.Tests\DisassembleCommandTests.cs | 191 | DisassembleCommandTests | DumpOnFailure | 1 | 2 | 5 | 1 | 3 | 70.6 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.CLI.Tests\CliTestContext.cs | 15 | CliTestContext | CliTestContext.ctor | 0 | 2 | 7 | 0 | 1 | 65.6 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.CLI.Tests\CLITest.cs | 7 | CLITest | Compile_NoSource_Succeeds | 0 | 1 | 4 | 0 | 0 | 72.2 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.CLI.Tests\CLITest.cs | 17 | CLITest | Compile_WithOptimization_Then_Run_Returns_100 | 0 | 1 | 14 | 0 | 0 | 56.4 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.CLI.Tests\CLITest.cs | 41 | CLITest | EmptyArgs_Returns_Error_To_Stderr | 0 | 1 | 5 | 0 | 0 | 69.7 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.CLI.Tests\CLITest.cs | 51 | CLITest | UnknownCommand_Returns_Error_To_Stderr | 0 | 1 | 6 | 0 | 0 | 67.1 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.CLI.Tests\CLITest.cs | 62 | CLITest | Run_With_DumbArgs | 0 | 1 | 13 | 0 | 0 | 56.2 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.CLI.Tests\CLITest.cs | 82 | CLITest | Run_MissingProgram_Returns_Error | 0 | 1 | 4 | 0 | 0 | 72.3 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.CLI.Tests\CLITest.cs | 91 | CLITest | Help_Prints_Usage_To_Stdout | 0 | 1 | 5 | 0 | 0 | 69.6 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.CLI.Tests\CliTestContext.cs | 29 | CliTestContext | Run | 0 | 1 | 6 | 0 | 1 | 67.7 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.CLI.Tests\CliTestContext.cs | 39 | CliTestContext | RunLib | 0 | 1 | 1 | 0 | 2 | 90.2 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.CLI.Tests\CliTestContext.cs | 43 | CliTestContext | ProjectPath | 0 | 1 | 1 | 0 | 1 | 89.5 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.CLI.Tests\CliTestContext.cs | 44 | CliTestContext | IncludePath | 0 | 1 | 1 | 0 | 1 | 89.5 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.CLI.Tests\CliTestContext.cs | 46 | CliTestContext | WriteProjectFile | 0 | 1 | 1 | 0 | 2 | 89.8 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.CLI.Tests\CliTestContext.cs | 49 | CliTestContext | WriteIncludeFile | 0 | 1 | 1 | 0 | 2 | 89.8 |

