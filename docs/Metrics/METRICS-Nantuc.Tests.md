# Code Metrics Report

Path: `tests/Nantuc.VmApplication.Tests/Nantuc.Tests.csproj`

## Summary

| Metric | Value |
| --- | ---: |
| Members analysed | 184 |
| C# files | 28 |
| Total lines | 4315 |
| C# classes | 41 |
| C# records | 0 |
| C# enums | 0 |
| Analysed LOC | 1355 |
| Cognitive avg | 0.3 |
| Cognitive median | 0 |
| Cognitive p90 | 1 |
| Cognitive max | 5 |
| Cyclomatic avg | 1.3 |
| Cyclomatic median | 1 |
| Cyclomatic p90 | 2 |
| Cyclomatic max | 5 |
| MI avg | 74.1 |
| MI minimum | 43.0 |

Highest risk member: `HaltWakeTests.HALT_SetsSleepingFlagAndDoesNotAdvanceFurther_StepMode` (cognitive 5, cyclomatic 5) — `HaltWakeTests.cs:98`
Lowest maintainability: `HaltWakeTests.HALT_SetsSleepingFlagAndDoesNotAdvanceFurther_DebugRun` (MI 43.0) — `HaltWakeTests.cs:40`

## Risk bands

| Band | Range | Members |
| --- | --- | ---: |
| Light | 0-5 | 184 |
| Moderate | 6-10 | 0 |
| High | 11-20 | 0 |
| Severe | 21+ | 0 |

## Members

Ranked by cognitive complexity, then cyclomatic complexity. Showing 20 of 184 member(s).

| File | Line | Type | Member | Cognitive | Cyclomatic | LinesOfCode | MaxNesting | Parameters | MaintainabilityIndex |
| --- | ---: | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\HaltWakeTests.cs | 98 | HaltWakeTests | HALT_SetsSleepingFlagAndDoesNotAdvanceFurther_StepMode | 5 | 5 | 26 | 3 | 0 | 47.5 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\Vm\AsyncHaltWakeTests.cs | 106 | AsyncHaltWakeTests | HALT_SetsSleepingFlagAndDoesNotAdvanceFurther_StepMode_Async | 5 | 5 | 26 | 3 | 0 | 47.5 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\Compiler\CompileTestBase.cs | 69 | CompileTestBase | RunCompile_WithLibs | 3 | 5 | 23 | 1 | 3 | 48.4 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\HaltWakeTests.cs | 40 | HaltWakeTests | HALT_SetsSleepingFlagAndDoesNotAdvanceFurther_DebugRun | 3 | 4 | 38 | 2 | 0 | 43.0 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\Vm\AsyncHaltWakeTests.cs | 45 | AsyncHaltWakeTests | HALT_SetsSleepingFlagAndDoesNotAdvanceFurther_DebugRun_Async | 3 | 4 | 38 | 2 | 0 | 43.1 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\Vm\UnsafeOpCodeTests.cs | 64 | UnsafeOpCodeTests | StepDebug_WithAllowUnsafeOpCodesFalse_StopsAtUnsafeOpCode | 3 | 3 | 12 | 2 | 0 | 57.9 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\Compiler\CompileTestBase.cs | 16 | CompileTestBase | RunCompile | 2 | 4 | 21 | 1 | 2 | 49.7 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\Vm\AsyncVmReuseTests.cs | 81 | AsyncVmReuseTests | MultipleRuns_RestartAfterHalt_RequiresStopAndContinues | 2 | 2 | 38 | 2 | 0 | 43.6 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\Vm\SyncVmReuseTests.cs | 79 | SyncVmReuseTests | MultipleRuns_RestartAfterHalt_RequiresStopAndContinues | 2 | 2 | 38 | 2 | 0 | 43.5 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\VMHostTests.cs | 15 | VMHostTests | RunProgramWithIO | 1 | 3 | 14 | 1 | 4 | 54.5 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\Compiler\CompileTestBase.cs | 49 | CompileTestBase | RunCompile_Error | 1 | 2 | 13 | 1 | 1 | 56.4 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\LanguageDefinitionDriftTests.cs | 13 | LanguageDefinitionDriftTests | MiniC_KeywordRegex_matches_every_keyword | 1 | 2 | 2 | 1 | 0 | 81.5 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\LanguageDefinitionDriftTests.cs | 29 | LanguageDefinitionDriftTests | Asm_MnemonicRegex_matches_every_mnemonic | 1 | 2 | 2 | 1 | 0 | 81.5 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\LanguageDefinitionDriftTests.cs | 36 | LanguageDefinitionDriftTests | Asm_RegisterRegex_matches_every_register | 1 | 2 | 2 | 1 | 0 | 81.5 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\Module.Diagnostics\DefaultErrorMessageProviderTests.cs | 8 | DefaultErrorMessageProviderTests | AllDefinedCodes | 1 | 2 | 4 | 1 | 0 | 72.7 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\Module.Diagnostics\DefaultErrorMessageProviderTests.cs | 26 | DefaultErrorMessageProviderTests | DefaultTemplates_HasEntryForEveryDefinedCode | 1 | 2 | 3 | 1 | 0 | 75.4 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\Module.Diagnostics\DefaultErrorMessageProviderTests.cs | 128 | DefaultErrorMessageProviderTests.MarkerProvider | TryGetTemplate | 1 | 2 | 3 | 1 | 1 | 77.7 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\TestHost.cs | 11 | TestHost | OpCode_AllValuesDefined_AndNamesMatch | 1 | 2 | 5 | 1 | 0 | 68.6 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\TestHost.cs | 23 | TestHost | RegType_AllValuesDefined_AndNamesMatch | 1 | 2 | 5 | 1 | 0 | 68.6 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\TestHost.cs | 34 | TestHost | OpCodeSizeSize_AllValuesDefined_AndNamesMatch | 1 | 2 | 5 | 1 | 0 | 68.6 |

