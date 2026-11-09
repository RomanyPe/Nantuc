# OtherLangs Benchmarks
> `[← Root](../../README.md)`

Comparison of the [Nantuc](https://github.com/RomanyPe/Nantuc) virtual machine with other programming languages on an identical set of cases from `SourceCasesMiniC.cs`.

## Why this exists

[`Nantuc.Benchmarks.BenchmarkDotNet`](../Nantuc.Benchmarks.BenchmarkDotNet/README.md) measures Nantuc performance **in-process** via BenchmarkDotNet. This is convenient for regressions, but it does not allow comparison with other languages: they require separate processes, their own runners, and their own warmups.

`OtherLangs` is a separate measurement channel: each solution (Python, Lua, Ruby, JS, C, C++, C#, Java, Rust) runs in its own native environment, and the results are consolidated into a single file.

**BDN and OtherLangs are intentionally not mixed:**

- BDN is optimized for C# and in-process measurements (JIT warmup, GC control, statistics).
- Other languages require their own tools (CPython, PUC Lua, MRI, V8, JVM, .NET, LLVM).
- Trying to wrap foreign processes in a `[Benchmark]` method produces distortions: process startup noise, lack of warmup, timeouts.

Bottom line: **BDN is for Nantuc regressions, OtherLangs is for comparison with other languages.** They are consolidated at the report level, not at the runner level.

## Structure

```
OtherLangs/
├── README.md
├── README.ru.md            
├── langs.json              # manifest: id, name, category, command, cwd
├── runner/
│   ├── run.ps1             # Windows PowerShell (primary)
│   └── run.py              # cross-platform Python runner
├── python/benchmarks.py
├── lua/benchmarks.lua
├── luajit/benchmarks.lua
├── ruby/benchmarks.rb
├── javascript/benchmarks.js
├── c/benchmarks.c
├── cpp/benchmarks.cpp
├── csharp/Benchmarks.cs
├── java/Benchmarks.java
└── rust/
    ├── Cargo.toml
    └── main.rs
```

## Output contract

Each program must print results **to stderr** (the runner reads both stdout and stderr — in case the language writes to the wrong place) in the format:

```
<Name>  <seconds> s  (result=<N>)
```

Example:

```
Call          1.2345 s  (result=4500001500000)
Math          0.5678 s  (result=4500001500000)
```

Requirements:

- `<Name>` — the case name from `SourceCasesMiniC` (`Call`, `Math`, `AluTput1`, `AluTput4`, `AluTputMixed`, `Memory`, `BitOps`, `Mul`, `Branch`, `DivMod`, `Recursion`, `Pointer`, `Struct`, `Sort`).
- `<seconds>` — a real number, with a **dot** as the separator (not a comma).
- `<N>` — an integer (the computation result; needed by the runner to verify correctness and to prevent JIT compilers from eliminating the loop entirely).

This exact format is already implemented in the scripts for all languages. If you add a new language, preserve the format; otherwise the `runner` will not parse it.

## Scripting languages (Python, Lua, LuaJIT, Ruby, JavaScript)

These languages are run **directly by the interpreter**, without a separate build. The `runner` finds the required executable in `PATH` itself, runs the script from the corresponding directory, and parses the results.

### Prerequisites

| Language | Interpreter in `PATH` | Check |
|---|---|---|
| Python | `python` (or `python3`/`py` on Windows) | `python --version` |
| Lua (PUC) | `lua` | `lua -v` |
| LuaJIT | `luajit` | `luajit -v` |
| Ruby (MRI) | `ruby` | `ruby --version` |
| JavaScript | `node` | `node --version` |

If the interpreter has a different name (`python3.12`, `lua54`, `lua53`), adjust the `run` field in `langs.json` — it defines the exact launch command. The runner does not guess names; it executes exactly what is written in the manifest.

### Running

**PowerShell (Windows):**

```powershell
cd OtherLangs\runner
.\run.ps1
```

**Python (cross-platform):**

```bash
cd OtherLangs/runner
python run.py
```

Both commands read `../langs.json`, run every available language, and store the result in `results.json` and `results.csv` in the current directory (relative to `runner/`).

### Useful flags

| Purpose | PowerShell | Python |
|---|---|---|
| Only one language | `-Languages python,ruby` | `--langs python ruby` |
| Only specific cases | `-Cases Math,Sort` | `--cases Math Sort` |
| Timeout per language, sec | `-Timeout 600` | `--timeout 600` |
| Repeats per case (minimum is taken) | `-Repeat 5` | `--repeat 5` |
| Verbose (raw output) | `-Verbose` | `-v` / `--verbose` |
| Output file | `-Output foo.json` | `-o foo.json` |

Example: only Python and Lua, only `Call` and `Math`, three repeats:

```powershell
.\run.ps1 -Languages python,lua -Cases Call,Math -Repeat 3
```

```bash
python run.py --langs python lua --cases Call Math --repeat 3
```

### Output

- `results.json` — an array of records `{lang, language, category, case, seconds, result}`.
- `results.csv` — the same in CSV.
- A summary table is printed to the console: rows are cases, columns are languages, values are seconds (minimum if `-Repeat` was used).

## Compiled languages (C, C++, C#, Java, Rust)

All compiled solutions use the same 14 cases and the same output format as the scripting ones. **Build time is not included in the measured time**: the runner launches an already built binary (or JIT runtime), so build in advance.

Each benchmark performs one warmup run before measurement (`main` calls each case once, then again with the timer). For JIT languages this is not enough; details below.

### Quick cheat sheet

| Language | Build | Run |
|---|---|---|
| C | `gcc -O2 -fwrapv benchmarks.c -o benchmarks` | `./benchmarks` |
| C++ | `g++ -O2 -fwrapv benchmarks.cpp -o benchmarks` | `./benchmarks` |
| C# | `dotnet run -c Release` | (launches immediately) |
| Java | `javac Benchmarks.java` | `java Benchmarks` |
| Rust (Cargo) | `cargo build --release` | `./target/release/benchmarks` |
| Rust (rustc) | `rustc -O main.rs -o benchmarks` | `./benchmarks` |

### C

```bash
cd OtherLangs/c
gcc -O2 -fwrapv benchmarks.c -o benchmarks
./benchmarks
```

- `-fwrapv` **is mandatory**: in C, signed `int` overflow is UB, and without the flag the compiler is free to eliminate loops or fold them into a constant (the arithmetic in the cases intentionally overflows in the same way as in Nantuc and the original `SourceCasesMiniC`).
- `-O2` is the standard level; `-O3` and `-march=native` will give different numbers, but that is already a different baseline — record it in the report.
- `sum_fn` is marked `__attribute__((noinline))` — real call overhead is measured, as in C++ and Rust.

### C++

```bash
cd OtherLangs/cpp
g++ -O2 -fwrapv benchmarks.cpp -o benchmarks
./benchmarks
```

Same as in C: `-fwrapv` is mandatory, `-O2` is the default, `sum_fn` is marked `[[gnu::noinline]]`.

### C#

```bash
cd OtherLangs/csharp
dotnet run -c Release
```

If you want to separate build from run:

```bash
dotnet build -c Release -o ./out
dotnet ./out/BenchmarksProject.dll
```

- The target framework is `net10.0` (see `BenchmarksProject.csproj`). It **unconditionally** has `<Optimize>true</Optimize>`, so `-c Release` mainly affects output paths, not codegen. Nevertheless, `-c Release` is a convention that most readers expect, and it is worth following.
- `Sum` is marked `[MethodImpl(MethodImplOptions.NoInlining)]` — intentionally, to measure call overhead. The other benchmark methods are invoked via `Func<long>[]`, which also inhibits inlining.
- ⚠️ **Warmup: one call is not enough.** RyuJIT compiles a method on first call, and tiers (tiering) take seconds to warm up. For fair numbers, use **BenchmarkDotNet** (`[Benchmark]`, `[WarmupCount]`, `[IterationCount]`, `[MemoryDiagnoser]`): it warms up itself, isolates the JIT phase, and provides statistics. The numbers from the current one-off measurement are a guide, not the truth.

### Java

```bash
cd OtherLangs/java
javac Benchmarks.java
java Benchmarks
```

- `javac` does not need flags: `int` overflow in Java is defined as wrapping, so no `-fwrapv` equivalent is required.
- ⚠️ **Warmup: one call is not enough.** HotSpot C2 compiles a method after ~10,000 calls/iterations. For fair numbers, use **JMH** (`org.openjdk.jmh`): `@Benchmark`, `@Warmup`, `@Fork`, `@BenchmarkMode(Mode.AverageTime)`, `@State`.
- If you want to force C2 in the current one-off measurement, add `-Xbatch`: compilation happens synchronously on the first "hot" call. Note that this **changes the picture**: without a profile, deoptimizations behave differently than in "normal" mode. For comparisons, JMH is better than `-Xbatch`.

### Rust

**Via Cargo (recommended — `Cargo.toml` is already configured):**

```bash
cd OtherLangs/rust
cargo build --release
./target/release/benchmarks
```

`Cargo.toml` already has fair release profiling configured: `opt-level = 3`, `lto = "fat"`, `codegen-units = 1`, `panic = "abort"`, `overflow-checks = false`. One binary — `[[bin]] name = "benchmarks"`, `path = "main.rs"`.

**Directly with `rustc` (if the file is named `benchmarks.rs`):**

```bash
rustc -O benchmarks.rs -o benchmarks
./benchmarks
```

- `-O` enables `opt-level=2` and **disables** `debug-assertions`, and therefore overflow checks — which is what we want: the code intentionally uses `wrapping_add` / `wrapping_sub` / `wrapping_mul` to match C/C++ with `-fwrapv` and Nantuc.
- `sum_fn` is marked `#[inline(never)]` — real call overhead is measured.
- `bench_pointer` uses `*mut i32` and `add` — real pointer arithmetic (unlike the emulation in scripting languages).

### What to keep in mind when comparing numbers

1. **`Call` in compiled and scripting languages means different things.** In C/C++/Rust the function is genuinely not inlined (`noinline` / `#[inline(never)]`), and the call goes through `call`. In C#/Java inlining is decided by the JIT (we force `NoInlining` only on `Sum`). In Python/Lua/Ruby the call is interpreted. Do not compare `Call` between groups head-on.

2. **`Pointer` in C/C++/Rust is real pointer arithmetic.** In Python/Ruby/JS/Lua it is emulation via array/table indices. Compare this case **within a group** (whose emulation is more efficient), not between groups.

3. **`Struct` in the three groups is three different memory models:**
   - C/C++/Rust — a flat array of structs/`Vec<Point>`, fields packed;
   - C# — an array of `readonly struct Point` (value type, no boxing);
   - Java — an array of `Point` objects (each a separate object on the heap, references);
   - scripting — an array of dictionaries/objects.

   The numbers reflect these models specifically, not "language speed in general."

4. **`BitOps` degenerates after the first iteration** (`r0 = 1 >> 1 = 0`) because the case is copied one-to-one from `SourceCasesMiniC`. This is expected and the same for all languages.

5. **A single run is not the truth.**
   - C/C++/Rust: restart the binary 5–10 times, take the median (noise from ASLR, CPU governor, turbo boost).
   - C#/Java: **only** BenchmarkDotNet / JMH. Without them, variance easily reaches 2–3× due to JIT tiers and deoptimizations.
   - `-Repeat` in the runner averages noise between process launches, but not within a process — this is not enough for JIT.

6. **Build time is not included in the measurement.** `gcc`/`g++`/`javac`/`dotnet build`/`cargo build` are a separate step; the runner does not launch them. If you run the benchmark "with one command" (`cargo run --release`, `dotnet run`), the build will also not be included in the measurement, but the runtime behavior itself may differ (for example, `dotnet run` may start slightly differently than `dotnet ./out/*.dll`).

## How to add a new language

1. Create a subdirectory `OtherLangs/<lang-id>/` with the source file `benchmarks.<ext>`.
2. Implement all 14 cases while preserving the output format.
3. Add an entry to `langs.json`:

   ```json
   {
     "id": "mylang",
     "name": "MyLang 1.0",
     "category": "scripting",
     "run": ["mylang", "benchmarks.mylang"],
     "cwd": "mylang"
   }
   ```

   The `category` field is one of `scripting` / `jit` / `native`. It is used only for grouping in reports; it does not affect execution.

4. Run the `runner`. If the interpreter is in `PATH`, the language will appear in `results.json`.

No changes to `runner/` are needed.

## What to keep in mind when reading the results

1. **A single run is a guide, not the truth.** For stable numbers:
   - C/C++/Rust — restart the binary 5–10 times, take the median;
   - C#/Java — use BenchmarkDotNet / JMH (see the compiled languages section);
   - Python/Lua/Ruby — the `-Repeat` flag reduces noise, but a 5–15% spread remains due to the OS; for serious conclusions, run on an isolated machine.

2. **`Call` is not semantically uniform.** In C/C++/Rust the function is marked `noinline` — real call overhead is measured. In C#/Java inlining is left to the JIT — what the JIT decides is measured. In Python/Lua/Ruby the call is interpreted. Directly comparing these numbers between groups is comparing different things.

3. **`Pointer` and `Struct` in scripting languages are emulation.** In Python it is work with a list, in Ruby with an array, in JS with `Int32Array`. In C/C++/Rust it is real pointer arithmetic. Compare these cases **within a group** (whose emulation is more efficient), not between groups.

4. **`BitOps` in the original degenerates** after the first iteration (`r0 = 1 >> 1 = 0`) because it is copied one-to-one from `SourceCasesMiniC`. This is expected and the same for all languages.

5. **`Sort` (bubble sort)** is the only case where cache locality and branch prediction matter. It is the longest in absolute time; make sure it does not consume 90% of the run.

6. **Different interpreter versions produce different numbers.** Specify versions (`python --version`, `lua -v`, `node --version`, `ruby --version`) in reports — otherwise comparisons between runs are meaningless.

## License and contributions

The scripts are Apache 2.0, like all of Nantuc. PRs with new languages and cases are welcome: the main thing is to preserve the output format and add an entry to `langs.json`.