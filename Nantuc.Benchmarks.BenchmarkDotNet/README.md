# Nantuc.Benchmarks.BenchmarkDotNet — README

> Benchmark for the Nantuc virtual machine based on **BenchmarkDotNet**: measures MIPS, stability, and allocations on 14 typical MiniC programs (calls, ALU, memory, bit operations, multiplication, branches, division, recursion, pointers, structs, sorting).  
> Depends on `[Nantuc.VmApplication.Extensions](../Nantuc.VmApplication.Extensions/README.ru.md)` (transitively on `VmApplication`, `Compiler`, `Kernel`).  
> `[← Root](../README.ru.md) · [VmApplication](../Nantuc.VmApplication/README.ru.md) · [Extensions](../Nantuc.VmApplication.Extensions/README.ru.md) · [Kernel](../Nantuc.Kernel/README.ru.md) · [Compiler](../Nantuc.Compiler/README.ru.md)`

## Contents
- [About this module](#about-this-module)
- [Local project tree](#local-project-tree)
- [API](#api)
- [API examples](#api-examples)
- [User-facing relevance](#user-facing-relevance)
- [Special notes](#special-notes)
- [What to fix](#what-to-fix)

## About this module

`Nantuc.Benchmarks.BenchmarkDotNet` is not a library but a **console application** (`<OutputType>Exe</OutputType>`) that uses **BenchmarkDotNet** to measure the performance of the Nantuc virtual machine. It is an alternative to the manual benchmark `[Nantuc.Benchmarks](../Nantuc.Benchmarks/README.ru.md)`: all statistics, warmup, outlier handling, and report generation are delegated to BenchmarkDotNet.

The module:

1. Creates a `VmHost` via `VmHostFactory.CreateDefault` and registers one `Device` (RAM 64 MiB) at `sector 0`.
2. Compiles and loads a MiniC program in `GlobalSetup` — **outside the measurement**.
3. For each of the 14 cases (`SourceCasesMiniC.AllCases`), runs the `[Benchmark]` method `RunVm`, which executes the VM once (`ctx.Run`) and returns the number of executed steps.
4. Prints the standard BenchmarkDotNet report (time, allocations, statistics) and saves artifacts to `BenchmarkDotNet.Artifacts/`.

Key roles:
- **`NantucBenchmarks.cs`** — benchmark class with `[ShortRunJob]`, `[MemoryDiagnoser]`, `[ParamsSource]`, `GlobalSetup`/`GlobalCleanup`, and `[Benchmark] RunVm`. The `record BenchCase` is also defined here.
- **`SourceCasesMiniC.cs`** — texts of the 14 MiniC programs and the static `AllCases` property returning `IEnumerable<BenchCase>`.
- **`Program.cs`** — entry point: `BenchmarkRunner.Run<NantucBenchmarks>()`.

The module is an example of **how to use `VmApplication` together with BenchmarkDotNet**: it does not touch `Kernel` directly, only through `VmHost`/`DeviceContext`, and shows that the public API is sufficient for benchmarking without hacks.

## Local project tree

Namespace — `Nantuc.Benchmarks.BenchmarkDotNet`:
