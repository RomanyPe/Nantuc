# Code Metrics Report

Path: `tests/Nantuc.VmApplication.Tests/Nantuc.Tests.csproj`

## Summary

| Metric | Value |
| --- | ---: |
| Members analysed | 150 |
| C# files | 25 |
| Total lines | 3115 |
| C# classes | 34 |
| C# records | 0 |
| C# enums | 0 |
| Analysed LOC | 829 |
| Cognitive avg | 0.2 |
| Cognitive median | 0 |
| Cognitive p90 | 1 |
| Cognitive max | 5 |
| Cyclomatic avg | 1.2 |
| Cyclomatic median | 1 |
| Cyclomatic p90 | 2 |
| Cyclomatic max | 5 |
| MI avg | 75.9 |
| MI minimum | 43.0 |

Highest risk member: `HaltWakeTests.HALT_SetsSleepingFlagAndDoesNotAdvanceFurther_StepMode` (cognitive 5, cyclomatic 5) — `HaltWakeTests.cs:98`
Lowest maintainability: `HaltWakeTests.HALT_SetsSleepingFlagAndDoesNotAdvanceFurther_DebugRun` (MI 43.0) — `HaltWakeTests.cs:40`

## Risk bands

| Band | Range | Members |
| --- | --- | ---: |
| Light | 0-5 | 150 |
| Moderate | 6-10 | 0 |
| High | 11-20 | 0 |
| Severe | 21+ | 0 |

## Members

Ranked by cognitive complexity, then cyclomatic complexity. Showing 20 of 150 member(s).

| File | Line | Type | Member | Cognitive | Cyclomatic | LinesOfCode | MaxNesting | Parameters | MaintainabilityIndex |
| --- | ---: | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\HaltWakeTests.cs | 98 | HaltWakeTests | HALT_SetsSleepingFlagAndDoesNotAdvanceFurther_StepMode | 5 | 5 | 26 | 3 | 0 | 47.5 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\Compiler\CompileTestBase.cs | 69 | CompileTestBase | RunCompile_WithLibs | 3 | 5 | 23 | 1 | 3 | 48.4 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\HaltWakeTests.cs | 40 | HaltWakeTests | HALT_SetsSleepingFlagAndDoesNotAdvanceFurther_DebugRun | 3 | 4 | 38 | 2 | 0 | 43.0 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\Vm\VmTestBase.cs | 64 | UnsafeOpCodeTests | SingleStep_WithAllowUnsafeOpCodesFalse_StopsAtUnsafeOpCode | 3 | 3 | 12 | 2 | 0 | 57.9 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\Compiler\CompileTestBase.cs | 16 | CompileTestBase | RunCompile | 2 | 4 | 21 | 1 | 2 | 49.7 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\VMHostTests.cs | 15 | VMHostTests | RunProgramWithIO | 1 | 3 | 14 | 1 | 4 | 54.5 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\Compiler\CompileTestBase.cs | 49 | CompileTestBase | RunCompile_Error | 1 | 2 | 13 | 1 | 1 | 56.4 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\LanguageDefinitionDriftTests.cs | 13 | LanguageDefinitionDriftTests | MiniC_KeywordRegex_matches_every_keyword | 1 | 2 | 2 | 1 | 0 | 81.5 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\LanguageDefinitionDriftTests.cs | 29 | LanguageDefinitionDriftTests | Asm_MnemonicRegex_matches_every_mnemonic | 1 | 2 | 2 | 1 | 0 | 81.5 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\LanguageDefinitionDriftTests.cs | 36 | LanguageDefinitionDriftTests | Asm_RegisterRegex_matches_every_register | 1 | 2 | 2 | 1 | 0 | 81.5 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\Module.Diagnostics\DefaultErrorMessageProviderTests.cs | 8 | DefaultErrorMessageProviderTests | AllDefinedCodes | 1 | 2 | 4 | 1 | 0 | 72.7 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\Module.Diagnostics\DefaultErrorMessageProviderTests.cs | 26 | DefaultErrorMessageProviderTests | DefaultTemplates_HasEntryForEveryDefinedCode | 1 | 2 | 3 | 1 | 0 | 75.4 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\Module.Diagnostics\DefaultErrorMessageProviderTests.cs | 128 | DefaultErrorMessageProviderTests.MarkerProvider | TryGetTemplate | 1 | 2 | 3 | 1 | 1 | 77.7 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\VMHostTests.cs | 246 | VMHostTests | RunAndKeep_ShouldKeepDeviceAlive | 1 | 2 | 16 | 1 | 0 | 54.9 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\Vm\VmSecurityTests.cs | 305 | VmSecurityTests | RegisterVm | 1 | 1 | 1 | 0 | 2 | 90.8 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\Vm\VmTestBase.cs | 93 | UnsafeOpCodeTests | RegisterVm | 1 | 1 | 1 | 0 | 2 | 90.8 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\HaltWakeTests.cs | 142 | HaltWakeTests | WAKE_INT_WithoutDevice_ReportsNullDeviceOutputFault | 0 | 2 | 19 | 0 | 0 | 52.0 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\HaltWakeTests.cs | 171 | HaltWakeTests | WAKE_INT_WithRegisteredDevice_InvokesWakeProcessor | 0 | 2 | 18 | 0 | 0 | 52.6 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\HaltWakeTests.cs | 201 | HaltWakeTests | WAKE_INT_UsesValueFromRegister_AfterMov | 0 | 2 | 19 | 0 | 0 | 52.1 |
| C:\Users\xity4\source\repos\Nantuc\tests\Nantuc.VmApplication.Tests\Module.Diagnostics\ThrowCompilerTests.cs | 42 | ThrowCompilerTests | PassThrough | 0 | 2 | 1 | 0 | 2 | 87.2 |

