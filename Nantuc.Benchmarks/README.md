# Nantuc.Benchmarks — README

> Консольный бенчмарк виртуальной машины Nantuc: измеряет MIPS (million instructions per second), стабильность, JIT/warmup impact и статистику по серии прогонов на типовых нагрузках (память, ALU, вызовы, стек, битовые операции, умножение, ветвления).  
> Зависит от `[Nantuc.VmApplication.Extensions](../Nantuc.VmApplication.Extensions/README.md)` (транзитивно — от `VmApplication`, `Compiler`, `Kernel`).  
> `[← Корень](../README.md) · [VmApplication](../Nantuc.VmApplication/README.md) · [Extensions](../Nantuc.VmApplication.Extensions/README.md) · [Kernel](../Nantuc.Kernel/README.md) · [Compiler](../Nantuc.Compiler/README.md)`

## Содержание
- [Об этом модуле](#об-этом-модуле)
- [Дерево локального проекта](#дерево-локального-проекта)
- [API](#api)
- [Примеры для API](#примеры-для-api)
- [Приближенность модуля к пользователю](#приближенность-модуля-к-пользователю)
- [Особенная информация](#особенная-информация)
- [Что исправить](#что-исправить)

## Об этом модуле

`Nantuc.Benchmarks` — это не библиотека, а **консольное приложение** (`<OutputType>Exe</OutputType>`), которое:

1. Повышает приоритет процесса и привязывает его к одному CPU-ядру (`BenchmarkOptimizer`).
2. Создаёт `VmHost` через `VmHostFactory.CreateDefault`, регистрирует одну `Device` (RAM 64 MiB) на `sector 0`.
3. Гоняет 12 фиксированных программ на NANASM (текстовые константы `text1`…`text8` в `Program.cs`), каждая — `WarmupRuns=5` + `Runs=20` раз.
4. Собирает MIPS, elapsed, steps по каждому прогону, считает статистику (`Stats`/`StatsResult`): mean, median, trimmed mean, перцентили, CV, skewness, JIT-impact.
5. Печатает прогресс-бар (`PrintProgress`) и пишет полный лог в `logs/result_Benchmark_Logs_YYYY-MM-DD_HH-mm-ss.txt`.

Модуль — образец того, **как использовать `VmApplication` для измерений**: он не трогает `Kernel` напрямую, только через `VmHost`/`DeviceContext`, и показывает, что публичного API хватает для бенчмарка без хаков.

Ключевые роли:
- **`Program.cs`** — точка входа + все тестовые программы (`text*`) + форматирование вывода.
- **`BenchRunner`** — раннер на один `BenchCase`: подготовка (`TryPrepare`), warmup, `RunOnce`, накопление логов и вычисление `PeakMips`/`StepsOnce`/`StepsVariance`.
- **`BenchCase`** — `record` с именем, текстом программы, числом прогонов и (неиспользуемым) `ExpectedInstructions`.
- **`Stats`** / **`StatsResult`** — статистика: mean, median, перцентили, IQR, CV, trimmed mean, geometric mean, skewness, half-means.
- **`BenchmarkOptimizer`** — процессный тюнинг: `ProcessPriorityClass.High` + привязка к последнему CPU-ядру (`ProcessorAffinity` на Windows, `sched_setaffinity` на Linux, `thread_policy_set` на macOS).

## Дерево локального проекта

Namespace — `Nantuc.Benchmarks` (все файлы в одной папке):

```
Nantuc.Benchmarks/
├── Nantuc.Benchmarks.csproj            # net10.0, Exe, ref → VmApplication.Extensions
├── Program.cs                          # Main + тестовые программы text1..text8 + вывод
├── BenchRunner.cs                      # BenchRunner + ListExtensions.ComputeVariance
├── BenchCase.cs                        # record BenchCase(Name, ProgramText, Runs, WarmupRuns, ExpectedInstructions?)
├── Stats.cs                            # static class Stats
├── StatsResult.cs                      # record struct StatsResult + From(...)
└── BenchmarkOptimizer.cs               # priority + CPU affinity; P/Invoke в LinuxNative
```

Замечания по организации:
- `BenchRunner.cs` содержит и `BenchRunner`, и file-local `ListExtensions` (extension `ComputeVariance`). Для навигации неудобно (grep `ComputeVariance` ведёт в файл с другим именем).
- `Program.cs` — монолит: 350+ строк с константами `text*`, `Main`, `PrintTestHeader`, `PrintProgress`, `AppendStatsExtended`, `AppendIterations`. Если планируете расширять набор кейсов, стоит вынести тестовые программы в `Cases/` или отдельный `BenchPrograms.cs`.

## API

Проект — `Exe`, публичного API нет. Ниже — внутренняя структура, полезная для правок и расширения.

### Точка входа

`Program.Main()` (private static):

1. `BenchmarkOptimizer.OptimizeCurrentProcess()` — priority + affinity.
2. `VmHostFactory.CreateDefault()` → `host`.
3. `host.Emulator.RegisterDevice(DeviceSpec.Create(RamSize.MB64, "ConsoleVM"), sector: 0)` → `deviceId`.
4. Массив `BenchCase[]` (12 кейсов).
5. Для каждого кейса: `new BenchRunner(host, deviceId, case)`, `TryPrepare()`, warmup, runs, сбор статистики.
6. `File.WriteAllText` в `logs/`, `Console.WriteLine`, `Console.ReadLine()`.

Тестовые программы (`const string` в `Program.cs`):

| Константа | Кейс | Что меряет |
|---|---|---|
| `text1` | `Call` | `CALL`/`RET` в цикле (10M итераций, `CALL Sum; ADD; RET`) |
| `text2` | `Math` | `ADD` + `DEC` + `TEST` + `JNZ` (10M итераций) |
| `text3` | `AluTput1` | 4× `ADD` в одном регистре + loop (2.5M итераций) |
| `text3a` | `AluTput4` | 4× `ADD` в разные регистры (ILP) + loop |
| `text3m` | `AluTputM1` | `ADD`/`SUB`/`AND`/`OR` в один регистр |
| `text3m2` | `AluTputM2` | 4× `ADD` в разные регистры (дубликат `text3a` по форме) |
| `text4` | `Memory` | `STORE_IND.S32` + `LOAD_IND.S32` (safe), 100×50000 |
| `text4u` | `MemoryUnsafe` | `STORE_IND_UNSAFE.S32` + `LOAD_IND_UNSAFE.S32`, 100×50000 |
| `text5` | `PushPop` | `PUSH r0; POP r0` в цикле (10M) |
| `text6` | `BitOps` | `SHR`/`AND`/`OR`/`XOR` (10M) |
| `text7` | `Mul` | `MULT_INT` (10M) |
| `text8` | `Branch` | `TEST`/`JZ`/`JMP` (ветвления, 1M итераций, ~6 инструкций на итерацию) |

### `BenchRunner`

```csharp
public class BenchRunner(VmHost host, int deviceId, BenchCase benchCase)
{
    public BenchCase Case { get; }
    public IReadOnlyList<double> MipsLogs { get; }
    public IReadOnlyList<double> ElapsedSecondsLog { get; }
    public IReadOnlyList<long> StepsLogs { get; }
    public bool IsPrepared { get; private set; }
    public double PeakMips { get; }        // StepsOnce / Min(Elapsed) / 1e6
    public long   StepsOnce { get; }        // StepsLogs[0]
    public double StepsVariance { get; }    // дисперсия StepsLogs (extension)
    public CompilationResult? Program { get; private set; }

    public bool TryPrepare();        // CompileToIL(ASM, optimize:true) → TryFastLoadProgram
    public void RunOnce();           // Launch(MipsLogs, ElapsedLog, StepsLogs, ctx)
    public void Warmup();            // Launch([], [], [], ctx) — лог отбрасывается
    public static void Launch(List<double> mips, List<double> elapsed,
                              List<long> steps, DeviceContext? ctx);
}
```

`TryPrepare` использует `_host.Project.CompileToIL(text, 0, optimize: true, SourceLanguage.Asm).Build()`. Для ASM `optimize: true` не делает ничего — `ProjectBuilder.Build` ассемблирует asm-файлы напрямую (`AsmParser.Assemble`), без AST/peephole. См. [Особенная информация](#особенная-информация).

`Launch` запускает `ctx.Run(new LaunchOptions(ShowTimer: true, OnEnd: ...))`. В `OnEnd` считаются `Mips = Steps / Elapsed.TotalSeconds / 1e6`, добавляются в три списка.

### `BenchCase`

```csharp
public record BenchCase(
    string Name,
    string ProgramText,
    int Runs,
    int WarmupRuns,
    long? ExpectedInstructions = null);
```

`ExpectedInstructions` **не используется** ни в `BenchRunner`, ни в `Program.cs`. Задел на будущее (сверка фактических шагов с ожидаемыми). См. [Что исправить, п.9](#что-исправить).

### Статистика

```csharp
public static class Stats
{
    public static double Mean(IReadOnlyList<double> v);
    public static double StdDev(IReadOnlyList<double> v);
    public static double Percentile(IReadOnlyList<double> v, double p);
    public static double Median(IReadOnlyList<double> v);
    public static double Min(Max/Sum/Range/IQR)(IReadOnlyList<double> v);
    public static double CV(IReadOnlyList<double> v);                   // StdDev / Mean
    public static double TrimmedMean(IReadOnlyList<double> v, double trimPercent);
    public static double GeometricMean(IReadOnlyList<double> v);
    public static double Skewness(IReadOnlyList<double> v);
    public static double FirstHalfMean(SecondHalfMean)(IReadOnlyList<double> v);
}

public record struct StatsResult(
    double Mean, double Median, double Min, double Max,
    double P1, double P5, double P25, double P75, double P95, double P99,
    double StdDev, double CV, double IQR, double Range,
    double TrimmedMean5, double TrimmedMean10,
    double GeometricMean, double Skewness,
    double FirstHalfMean, double SecondHalfMean, int N)
{
    public static StatsResult From(IReadOnlyList<double> v);
}
```

- `Percentile` — линейная интерполяция (rank-based, `rank = p/100 * (n-1)`), работает и для n=1 (возвращает единственный элемент).
- `TrimmedMean` — отбрасывает `floor(n * trim/100)` с каждого конца, возвращает `Median`, если после отбрасывания пусто.
- `GeometricMean` — игнорирует `x <= 0` (защита от `log(0) = -inf`).
- `Skewness` — sample skewness с поправкой `n / ((n-1)(n-2))`.
- `FirstHalfMean`/`SecondHalfMean` — для JIT-impact: разница между первой и второй половиной прогонов.

### `BenchmarkOptimizer`

```csharp
public static partial class BenchmarkOptimizer
{
    public static void OptimizeCurrentProcess();   // priority + affinity

    private static partial class LinuxNative
    {
        [LibraryImport("libc", EntryPoint = "sched_setaffinity", SetLastError = true)]
        public static partial int sched_setaffinity(int pid, IntPtr cpusetsize, ref long mask);

        [LibraryImport("libc", EntryPoint = "pthread_self")]
        public static partial int pthread_self();

        [LibraryImport("libc", EntryPoint = "thread_policy_set")]
        public static partial int thread_policy_set(int thread, int policy, IntPtr flavor, int count);
    }
}
```

- **Windows**: `Process.PriorityClass = High`, `ProcessorAffinity = 1L << (ProcessorCount - 1)`.
- **Linux**: `sched_setaffinity(pid, sizeof(long), &mask)` — привязка к последнему ядру. Требует прав root для `PriorityClass.High`, affinity доступна без root в пределах своих ядер.
- **macOS**: `thread_policy_set(thread, THREAD_AFFINITY_POLICY, &tag, 1)` — affinity tag, не классическая привязка.

Все ошибки ловятся и печатаются как `[Предупреждение]` / `[Ошибка]`, бенчмарк продолжается без привязки.

## Примеры для API

**Добавить свой кейс в `Program.Main`:**

```csharp
const string textNew =
    """
    LDI r0, 0;
    LDI r6, 1000000;
loop:
    XOR r0, r0;
    DEC r6;
    TEST r6, r6;
    JNZ loop;
    END;
    """;

// в массиве cases:
new BenchCase("XorSelf", textNew, Runs: 20, WarmupRuns: 5)
```

**Использовать `BenchRunner` в своём коде (не через `Program.Main`):**

```csharp
using var host = VmHostFactory.CreateDefault();
int deviceId = host.Emulator.RegisterDevice(
    DeviceSpec.Create(RamSize.MB64, "Bench"), sector: 0);

var runner = new BenchRunner(host, deviceId,
    new BenchCase("MyCase", textNew, Runs: 10, WarmupRuns: 3));

if (!runner.TryPrepare()) return;

for (int i = 0; i < 3; i++) runner.Warmup();
for (int i = 0; i < 10; i++) runner.RunOnce();

var mips = StatsResult.From(runner.MipsLogs);
Console.WriteLine($"Peak MIPS: {runner.PeakMips:F2}");
Console.WriteLine($"Median MIPS: {mips.Median:F2}");
Console.WriteLine($"Steps once: {runner.StepsOnce:N0}");
```

**Посчитать статистику без VM:**

```csharp
var samples = new[] { 100.0, 102.0, 99.0, 101.0, 103.0, 98.0, 500.0 };
var s = StatsResult.From(samples);
Console.WriteLine($"Median={s.Median:F2} Trim10={s.TrimmedMean10:F2} CV={s.CV*100:F2}%");
// Median=101.00 Trim10=101.00 CV=95.11% — выброс 500 не влияет на медиану/trim
```

**Дизассемблировать программу кейса:**

```csharp
if (runner.Program is { } comp)
    Console.WriteLine(comp.DisassembleToString());
```

## Приближенность модуля к пользователю

**Средняя.** Это не библиотека, а «продукт» — образец использования и инструмент для оценки производительности VM. Конечному пользователю MiniC/NANASM бенчмарк не нужен; автору ядра/компилятора — полезен для регрессий.

**Что стоит делать:**
- Запускать на **изолированной машине** (или хотя бы без нагрузки) — иначе affinity/priority не помогут.
- На Linux для `PriorityClass.High` — `sudo`, иначе будет `[Предупреждение]` и обычный приоритет.
- Читать логи из `logs/` — консоль может обрезать вывод (`PrintProgress` пишет в одну строку с `\r`).
- Расширять набор кейсов через `BenchCase` + `text*` в `Program.cs`, а не через форк `BenchRunner`.
- Для сравнения «safe vs unsafe» памяти — `Memory` vs `MemoryUnsafe` уже есть. Для сравнения «с оптимизациями vs без» — добавьте `optimize: false` в `BenchRunner.TryPrepare` или отдельный флаг в `BenchCase`.

**Что не стоит делать:**
- Запускать бенчмарк в CI/headless — `Console.ReadLine()` в конце `Main` заблокирует процесс, а `Console.Clear()` может бросить.
- Сравнивать MIPS между машинами — привязка к ядру, частота, JIT, GC, thermal throttling сильно влияют. Сравнивайте только в пределах одной сессии.
- Ожидать, что `optimize: true` в `BenchRunner.TryPrepare` что-то даст для ASM-программ — не даст (см. ниже).
- Использовать `PeakMips` как единственную метрику — он берёт `Min(Elapsed)`, что чувствительно к выбросам. `TrimmedMean10` из `MipsLogs` надёжнее.

## Особенная информация

### `optimize: true` — no-op для ASM

`BenchRunner.TryPrepare` вызывает `_host.Project.CompileToIL(_case.ProgramText, ..., optimize: true, SourceLanguage.Asm).Build()`. В `ProjectBuilder.Build` ASM-файлы обрабатываются так:

```csharp
foreach (SourceFile file in files.Where(file => file.Language == SourceLanguage.Asm))
{
    asmParser.Assemble(file.Content, assembler);   // без AST, без peephole
}
```

`optimize: true` включает только AST-оптимизации (которые для ASM не применяются) и **не** включает peephole (`VmPeepholeOptimizer.AddStdRules` никто не вызывает). Так что все 12 кейсов — это ровно то, что вы написали в `text*`, без оптимизаций.

Если хотите сравнить «с оптимизациями vs без», нужно явно:

```csharp
host.Project.Optimizator.VMPeepholeOptimizer.AddStdRules();
var il = host.Project.CompileToIL(text, 0, optimize: false, SourceLanguage.Asm);
il = host.Project.OptimizationIL(il, peepholeOptimizationCount: 5);
var comp = il.Build();
```

### `text4` vs `text4u` — safe vs unsafe

Оба кейса используют одинаковый паттерн `MOV r7, rHP; ADD rHP, r0;` (ручной bump allocator), но:

- `text4` — `STORE_IND.S32` / `LOAD_IND.S32` (с проверкой границ и выравнивания в `Processor.Step`).
- `text4u` — `STORE_IND_UNSAFE.S32` / `LOAD_IND_UNSAFE.S32` (без проверок).

Комментарий в `text4u` (`// <-- замена ALLOC r7`) — исторический: когда-то в языке был опкод `ALLOC`, сейчас его нет, и аллокация делается вручную. На текущий бенчмарк это не влияет.

Ожидаемая разница: `MemoryUnsafe` должен быть на 20-40% быстрее, потому что пропускает `AlignmentFault`-проверки и `_ram.UpdateAndReadStatus()`. Если разница < 10% — значит, `Processor` не в горячем пути, или JIT не инлайнит `_ram.ReadInt32LEUnsafe` (см. `[Nantuc.Kernel](../Nantuc.Kernel/README.md)`, `[MethodImpl(AggressiveOptimization)]` на `Step`).

### `BenchmarkOptimizer` — почему последнее ядро

`targetCore = ProcessorCount - 1` — чтобы уйти от системного шума на ядрах 0/1 (обычно там живут kernel threads, прерывания, GC-потоки). Это не гарантия: планировщик всё равно может мигрировать поток, если affinity не задана, но на Windows `ProcessorAffinity` — жёсткая привязка, на Linux `sched_setaffinity` — тоже.

### `Stats` — почему несколько метрик

- `Mean` — чувствителен к выбросам (GC-паузы, thermal throttling).
- `Median` — устойчив, но не показывает «средний» сценарий.
- `TrimmedMean10` — компромисс: отбрасывает 10% сверху и снизу, игнорирует выбросы, показывает типичное значение. **Основная метрика** для отчёта.
- `CV` — коэффициент вариации; `CV < 5%` — стабильно, `CV > 15%` — шумно.
- `Skewness > 0` — хвост вправо (выбросы «медленные прогоны»); `< 0` — хвост влево.
- `FirstHalfMean`/`SecondHalfMean` — JIT/warmup: если `firstHalf > secondHalf` на 20%+, JIT ещё не прогрелся к концу warmup'а, стоит увеличить `WarmupRuns`.

### JIT/warmup и tiered compilation

`BenchmarkOptimizer` не выключает tiered compilation. Это значит: первые прогоны идут в Tier0 (быстрый JIT, медленный код), через ~30 вызовов метод промоутится в Tier1 (оптимизированный). `WarmupRuns=5` **недостаточно** для Tier1 — нужно ~30 вызовов метода. `FirstHalfMean` vs `SecondHalfMean` покажет это в отчёте. Если JIT-impact большой, добавьте в `.csproj`:

```xml
<PropertyGroup>
  <TieredCompilation>false</TieredCompilation>
  <TieredPGO>false</TieredPGO>
</PropertyGroup>
```

или установите переменную окружения `DOTNET_TieredCompilation=0` перед запуском. Это уберёт «холодные» прогоны и сделает MIPS стабильнее, но менее реалистичным (в реальной эксплуатации tiered compilation включён).

### Вывод и формат лога

`Program.cs` использует:
- `PrintProgress` с `\r` и ANSI-символами `▓░✓✗•` — в CI/redirected output `Console.IsOutputRedirected` пропускает прогресс-бар, но `Console.Clear()` в конце `Main` — **не** пропускает (см. [Что исправить, п.3](#что-исправить)).
- `AppendStatsExtended` — все 20 полей `StatsResult` в формате `\t    name: value`.
- `AppendIterations` — по одному значению на строку, формат `F4`.

Лог-файл: `logs/result_Benchmark_Logs_{yyyy-MM-dd_HH-mm-ss}.txt` в `AppDomain.CurrentDomain.BaseDirectory`. Содержит **всё**: от прогресса до полного дизассемблера каждой программы.

### Ограничения платформ

- **`net10.0`**, `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` — но `unsafe`-кода нет. Либо для транзитивных зависимостей (маловероятно — `AllowUnsafeBlocks` не наследуется через `ProjectReference`), либо забыто.
- **`BenchmarkOptimizer`** — `sched_setaffinity`/`thread_policy_set` — только Linux/macOS, на Windows используется `ProcessorAffinity`. В WASM `Process`/P/Invoke не работают — бенчмарк не соберётся под `browser-wasm` (и не должен).
- **`Console.ReadLine()`** в конце `Main` — блокирует. В CI/headless процесс зависнет. См. [Что исправить, п.3](#что-исправить).
- **`1L << targetCore`** в `BenchmarkOptimizer.TryBindToLastCpuCore` — на 64-ядерной машине `targetCore = 63`, `1L << 63` = `long.MinValue` (переполнение знака). На 128-ядерной — UB. См. [Что исправить, п.1](#что-исправить).

### Что этот модуль не делает

- Не является библиотекой — это `Exe`.
- Не публикует результаты в внешнее хранилище (BenchmarkDotNet-style baseline) — только `logs/*.txt`.
- Не сравнивает версии Nantuc между собой — только измеряет текущую.
- Не использует `BenchmarkDotNet` — всё вручную, чтобы контролировать `LaunchOptions(ShowTimer: true)` и не зависеть от `VmHost` в чужом фреймворке.
- Не тестирует многопоточность — все кейсы однопоточные, один `Device` на всё.

## Что исправить

По убыванию серьёзности. Ссылки на файлы/методы.

1. **`BenchmarkOptimizer.TryBindToLastCpuCore` — `1L << targetCore` переполняется на ≥64 ядрах.**  
   Файл: `BenchmarkOptimizer.cs`, метод `TryBindToLastCpuCore`: `long affinityMask = 1L << targetCore;`.  
   На машине с 64+ ядрами `targetCore >= 63` → сдвиг в знаковый бит / undefined behavior; affinity-маска получается отрицательной или нулевой, `ProcessorAffinity`/`sched_setaffinity` привяжут не туда (или никуда).  
   Фикс: проверять `targetCore < 63` (для 64-битной маски), либо использовать `ulong`, либо `IntPtr.Size`-aware маску (на 32-битных системах — 31 ядро max), либо `1UL << targetCore` с проверкой. Для 128-ядерных систем — `cpu_set_t` в Linux требует массив из нескольких `long`.

2. **`BenchmarkOptimizer.LinuxNative.pthread_self` — `int` вместо `IntPtr`.**  
   Файл: `BenchmarkOptimizer.cs`, `private static partial class LinuxNative`, `public static partial int pthread_self();`.  
   На 64-битных macOS `pthread_t` — это указатель (`_opaque_pthread_t*`). `int` обрежет старшие 32 бита, `thread_policy_set` получит мусорный handle.  
   Фикс: `public static partial IntPtr pthread_self();`. Аналогично `thread_policy_set(int thread, ...)` — первый параметр должен быть `IntPtr`.

3. **`Program.Main` — `Console.ReadLine()` в конце блокирует CI/headless.**  
   Файл: `Program.cs`, конец `Main` (`Console.WriteLine($"Файл результатов..."); Console.ReadLine();`).  
   Плюс `Console.Clear()` перед сохранением — на headless может бросить `IOException`.  
   Фикс: `if (!Console.IsInputRedirected && !Console.IsOutputRedirected) Console.ReadLine();` и `try { Console.Clear(); } catch (IOException) { }`. Аналогично для `Console.Clear()` в начале.

4. **`BenchRunner.TryPrepare` — `Console.WriteLine(res.Errors)` печатает имя типа, а не ошибки.**  
   Файл: `BenchRunner.cs`, `TryPrepare`: `Console.WriteLine(res.Errors);`.  
   `IReadOnlyList<string>` в `Console.WriteLine` выводит `System.Collections.Generic.List...`. Ошибки не видны.  
   Фикс: `foreach (var e in res.Errors ?? []) Console.WriteLine(e);` или `Console.WriteLine(string.Join(Environment.NewLine, res.Errors ?? []))`.

5. **`BenchRunner.TryPrepare` — `Console.ReadLine()` в фейл-путях блокирует.**  
   Файл: `BenchRunner.cs`, `TryPrepare`, два `Console.ReadLine()` после `Console.WriteLine`.  
   Тот же риск, что и в `Main`. В `Program.Main` фейл обрабатывается через `PrintProgress(..., failed: true)` и `continue`, а `BenchRunner` сам блокирует — несогласованно.  
   Фикс: убрать `Console.ReadLine()` из `TryPrepare`, вернуть `false` и дать вызывающему решать (в `Main` уже есть обработка).

6. **`BenchRunner.PeakMips`/`StepsOnce` — берёт `StepsLogs[0]`, а не минимум/медиану.**  
   Файл: `BenchRunner.cs`, свойства `PeakMips` и `StepsOnce`.  
   `StepsOnce = _stepsLogs[0]` — если первый прогон был с другим поведением (например, GC между `TryPrepare` и первым `RunOnce`), число шагов может отличаться от последующих. `PeakMips = StepsOnce / Min(Elapsed)` смешивает шаги одного прогона с временем другого.  
   Фикс: `StepsOnce` → `_stepsLogs.Min()` (или `Median`), `PeakMips` → `_stepsLogs.Zip(_elapsedSecondsLogs).Max(p => p.First / p.Second / 1e6)`.

7. **`BenchRunner.Warmup`/`RunOnce` — warmup пишет в пустые списки.**  
   Файл: `BenchRunner.cs`, методы `Warmup` и `RunOnce`.  
   `Warmup` вызывает `Launch([], [], [], ctx)` — `Metric` добавляет запись в пустые списки, которые сразу выбрасываются. Аллокация на каждый warmup-прогон (3 списка) — мелочь, но некрасиво.  
   Фикс: `Launch` с `onMetric: null` — не добавлять, если списки пустые, или отдельный overload без списков.

8. **`Program.Main` — `Console.Clear()` без `try/catch`.**  
   Файл: `Program.cs`, `Main`: `Console.Clear();` после `totalSw.Stop()`.  
   На headless (перенаправленный вывод, CI, некоторые терминалы) бросает `IOException`. Весь бенчмарк уже посчитан, но лог не сохранится.  
   Фикс: `try { Console.Clear(); } catch (IOException) { /* ignore */ }`.

9. **`BenchCase.ExpectedInstructions` не используется.**  
   Файл: `BenchCase.cs` (поле) + `Program.cs` (все `new BenchCase(...)` без него) + `BenchRunner` (не читает).  
   Поле есть, но `BenchRunner` не сверяет фактические `StepsLogs` с ожидаемыми. Если VM выдаст аномальное число шагов (например, из-за бага в `Processor.Step`), бенчмарк это не заметит — MIPS будет «правильный», но шаги — нет.  
   Фикс: в `BenchRunner.RunOnce` после прогона: `if (_case.ExpectedInstructions is { } expected && steps[^1] != expected) Console.Error.WriteLine($"Steps mismatch: expected {expected}, got {steps[^1]}");`. Заполнить `ExpectedInstructions` для всех кейсов (значения можно взять из эталонного прогона).

10. **`Program.cs` — тестовые программы `text*` в одном файле с `Main`.**  
    Файл: `Program.cs`, `const string text1`…`text8` (350+ строк).  
    Монолит, тяжело навигировать, тяжело расширять.  
    Фикс: вынести в `Cases/BenchPrograms.cs` (`internal static class BenchPrograms { public const string Call = """..."""; ... }`) или в `BenchCase`-фабрику с `switch`. Тогда `Program.Main` станет короче и чище.

11. **`Program.cs` — `text3a` и `text3m2` семантически эквивалентны.**  
    Файл: `Program.cs`, `const string text3a` и `const string text3m2`.  
    Оба — 4× `ADD` в разные регистры (r0/r1/r2/r3), r7=1, loop с `DEC r6; TEST r6, r6; JNZ loop`. Различие — только в порядке `LDI`. Кейсы `AluTput4` и `AluTputM2` будут показывать одинаковые MIPS.  
    Фикс: либо заменить `text3m2` на что-то отличное (например, `MULT_INT` в разные регистры), либо удалить кейс `AluTputM2`.

12. **`Program.cs` — `PrintProgress` для `fail` передаёт `done=0, phaseTotal=0`.**  
    Файл: `Program.cs`, `Main`, ветка `if (!runner.TryPrepare())`:
    ```csharp
    PrintProgress(testIndex, testTotal, name, "fail", 0, 0,
                  phaseTotal, phaseTotal, globalStep, totalSteps, forceNewLine: true, failed: true);
    ```
    `done=0, phaseTotal=0` — в `phaseStr` не используется (`phase == "fail"` → `$"{phase,-4}"`), но в `testPct = phaseStepsTotal <= 0 ? 1.0 : ...` — `phaseStepsTotal = phaseTotal > 0`, так что процент считается по `phaseStep/phaseTotal = 1.0`. Не баг, но параметры «0, 0» вводят в заблуждение — либо `phaseTotal, phaseTotal`, либо добавить комментарий.

13. **`Stats.Percentile`/`Median`/`TrimmedMean` — `OrderBy().ToArray()` на каждый вызов.**  
    Файл: `Stats.cs`, методы `Percentile`, `Median`, `TrimmedMean`.  
    `StatsResult.From` вызывает `Percentile` 10 раз, `Median` 1 раз, `TrimmedMean` 2 раза — итого 13 сортировок одного и того же массива. Для `n=20` незаметно, но при `n=1000` (если расширите `Runs`) — заметно.  
    Фикс: отсортировать один раз в `StatsResult.From` и передавать `ReadOnlySpan<double>`/отсортированный массив. Или кэшировать в локальной переменной внутри `From`.

14. **`BenchRunner.cs` — `ListExtensions` в том же файле.**  
    Файл: `BenchRunner.cs`, `file static class ListExtensions`.  
    `ComputeVariance` — единственный extension в проекте, лежит рядом с `BenchRunner`. Для навигации неудобно.  
    Фикс: вынести в `Stats.cs` как `public static double Variance(IReadOnlyList<double> v)` и не делать extension (extension на `IReadOnlyList<double>` — сомнительная практика, конфликтует с `System.Linq`).

15. **`Benchmarks.csproj` — `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` без `unsafe`-кода.**  
    Файл: `Nantuc.Benchmarks.csproj`. В коде проекта `unsafe` нет.  
    Фикс: убрать (или добавить комментарий `<!-- нужно для ... -->`, если действительно нужно для транзитивных зависимостей).

16. **`Program.cs` — `case text4` не использует heap-allocator.**  
    Файл: `Program.cs`, `const string text4`. Комментарий в `text4u`: `// <-- замена ALLOC r7`. В `text4` — та же замена (`MOV r7, rHP; ADD rHP, r0;`), но без комментария. Читатель может подумать, что `text4` — «оригинал с ALLOC», а это не так.  
    Фикс: добавить в `text4` тот же комментарий или вынести bump-alloc в отдельную функцию (через `#include` или inline asm), чтобы было видно, что это ручная аллокация, а не опкод.

17. **`Program.cs` — `Console.WriteLine($"Файл результатов...")` с кириллицей и `Console.ReadLine()`.**  
    Файл: `Program.cs`, конец `Main`.  
    В консоли Windows с кодовой страницей 866/1251 кириллица может отображаться кракозябрами, если не выставлен `Console.OutputEncoding = Encoding.UTF8`.  
    Фикс: `Console.OutputEncoding = System.Text.Encoding.UTF8;` в начале `Main` (или `Console.OutputEncoding = new UTF8Encoding(false)` для CI).

18. **`BenchRunner.TryPrepare` — `Program = res` до `TryFastLoadProgram`.**  
    Файл: `BenchRunner.cs`, `TryPrepare`:
    ```csharp
    var ctx = _host.Emulator.GetDeviceContext(_deviceId)!;
    Program = res;
    if (!ctx.TryFastLoadProgram(res.Program, 0, out string? errorMath)) { ... return false; }
    ```
    Если загрузка упала, `Program` уже установлен, а `IsPrepared = false`. Если вызывающий код смотрит на `Program`, он увидит «успешную» компиляцию, но программа не загружена.  
    Фикс: `Program = res` после успешной загрузки. Или задокументировать, что `Program` — результат компиляции, не загрузки.