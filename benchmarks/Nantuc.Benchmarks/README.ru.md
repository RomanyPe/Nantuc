# Nantuc.Benchmarks — README

> Консольный бенчмарк виртуальной машины Nantuc: измеряет MIPS (million instructions per second), стабильность, JIT/warmup impact и статистику по серии прогонов на типовых нагрузках (память, ALU, вызовы, стек, битовые операции, умножение, ветвления, рекурсия, указатели, структуры, сортировка) на **MiniC**-источниках.  
> Зависит от `[Nantuc.VmApplication.Extensions](../../src/Nantuc.VmApplication.Extensions/README.ru.md)` (транзитивно — от `VmApplication`, `Compiler`, `Kernel`).  
> `[← Корень](../../README.ru.md) · [VmApplication](../../src/Nantuc.VmApplication/README.ru.md) · [Extensions](../../src/Nantuc.VmApplication.Extensions/README.ru.md) · [Kernel](../../src/Nantuc.Kernel/README.ru.md) · [Compiler](../../src/Nantuc.Compiler/README.ru.md)`

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

1. Печатает прогресс-бар (`PrintProgress`) и пишет полный лог в `logs/result_Benchmark_Logs_YYYY-MM-DD_HH-mm-ss.txt`.
2. Создаёт `VmHost` через `VmHostFactory.CreateDefault`, регистрирует одну `Device` (RAM 64 MiB) на `sector 0`.
3. Гоняет **14 фиксированных MiniC-программ** (`internal static class SourceCasesMiniC` в `SourceCasesMiniC.cs`), каждая — `WarmupRuns=5` + `Runs=20` раз.
4. Собирает MIPS, elapsed, steps по каждому прогону, считает статистику (`Stats`/`StatsResult`): mean, median, trimmed mean, перцентили, CV, skewness, JIT-impact.

Модуль — образец того, **как использовать `VmApplication` для измерений**: он не трогает `Kernel` напрямую, только через `VmHost`/`DeviceContext`, и показывает, что публичного API хватает для бенчмарка без хаков.

Ключевые роли:
- **`Program.cs`** — точка входа + массив кейсов + форматирование вывода.
- **`SourceCasesMiniC.cs`** — все 14 MiniC-программ как `public const string ...Mc` (`callMc`, `mathMc`, `aluTput1Mc`, `aluTput4Mc`, `aluTputMixedMc`, `memoryMc`, `bitOpsMc`, `mulMc`, `branchMc`, `divModMc`, `recursionMc`, `pointerMc`, `structMc`, `sortMc`).
- **`BenchRunner`** — раннер на один `BenchCase`: подготовка (`TryPrepare`), warmup, `RunOnce`, накопление логов и вычисление `PeakMips`/`StepsOnce`/`StepsVariance`.
- **`BenchCase`** — `record` с именем, текстом программы, числом прогонов и (неиспользуемым) `ExpectedInstructions`.
- **`Stats`** / **`StatsResult`** — статистика: mean, median, перцентили, IQR, CV, trimmed mean, geometric mean, skewness, half-means.

## Дерево локального проекта

Namespace — `Nantuc.Benchmarks` (все файлы в одной папке):

```
Nantuc.Benchmarks/
├── Nantuc.Benchmarks.csproj            # net10.0, Exe, ref → VmApplication.Extensions
├── Program.cs                          # Main + массив BenchCase[] + вывод/прогресс
├── SourceCasesMiniC.cs                 # все 14 MiniC-источников (callMc…sortMc)
├── BenchRunner.cs                      # BenchRunner + ListExtensions.ComputeVariance
├── BenchCase.cs                        # record BenchCase(Name, ProgramText, Runs, WarmupRuns, ExpectedInstructions?)
├── Stats.cs                            # static class Stats
└── StatsResult.cs                      # record struct StatsResult + From(...)
```

Замечания по организации:
- `BenchRunner.cs` содержит и `BenchRunner`, и file-local `ListExtensions` (extension `ComputeVariance`). Для навигации неудобно (grep `ComputeVariance` ведёт в файл с другим именем).
- `Program.cs` — уже не «монолит с источниками»: все MiniC-программы вынесены в `SourceCasesMiniC.cs`. Остаётся `Main` + `PrintTestHeader` / `PrintProgress` / `AppendStatsExtended` / `AppendIterations`. Дальнейшее расширение набора кейсов — через `SourceCasesMiniC` + строку в массиве `cases`.

## API

Проект — `Exe`, публичного API нет. Ниже — внутренняя структура, полезная для правок и расширения.

### Точка входа

`Program.Main()` (private static):

1. `VmHostFactory.CreateDefault()` → `host`.
2. `host.Emulator.RegisterDevice(DeviceSpec.Create(RamSize.MB64, "ConsoleVM"), sector: 0)` → `deviceId`.
3. Массив `BenchCase[]` (14 кейсов, `_runs=20`, `_runsWarmup=5`).
4. Для каждого кейса: `new BenchRunner(host, deviceId, case)`, `TryPrepare()`, warmup, runs, сбор статистики.
5. `File.WriteAllText` в `logs/`, `Console.WriteLine`, `Console.ReadLine()`.

Тестовые программы (`public const string` в `SourceCasesMiniC.cs`):

| Константа | Кейс | Что меряет |
|---|---|---|
| `callMc` | `Call` | Вызов функции `sum(r0, r1)` в цикле, 3M итераций |
| `mathMc` | `Math` | `ADD` + `DEC` + loop, 3M итераций |
| `aluTput1Mc` | `AluTput1` | 4× `ADD` в один регистр (r0), 800K итераций |
| `aluTput4Mc` | `AluTput4` | 4× `ADD` в разные регистры (ILP), 800K итераций |
| `aluTputMixedMc` | `AluTputMixed` | `ADD`/`SUB`/`AND`/`OR` в разные регистры, 800K итераций |
| `memoryMc` | `Memory` | Заполнение массива `arr[25000]` + суммирование, 20× |
| `bitOpsMc` | `BitOps` | `SHR`/`AND`/`OR`/`XOR`, 3M итераций |
| `mulMc` | `Mul` | `MUL`, 3M итераций |
| `branchMc` | `Branch` | `if ((r6 & 1) != 0)`, 500K итераций |
| `divModMc` | `DivMod` | `DIV` + `MOD`, 500K итераций |
| `recursionMc` | `Recursion` | Рекурсия `down(500)` × 500 раз |
| `pointerMc` | `Pointer` | Арифметика указателей, чтение/запись через `*(p + i)`, 1000 × 2000 |
| `structMc` | `Struct` | Доступ к полям `struct Point` в массиве, 1000 × 200 |
| `sortMc` | `Sort` | Пузырьковая сортировка `arr[200]`, 50× |

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

    public bool TryPrepare();        // CompileToIL(SourceLanguage.C, optimize: true) → TryLoadProgram
    public void RunOnce();           // Launch(MipsLogs, ElapsedLog, StepsLogs, ctx)
    public void Warmup();            // Launch([], [], [], ctx) — лог отбрасывается
    public static void Launch(List<double> mips, List<double> elapsed,
                              List<long> steps, DeviceContext? ctx);
}
```

`TryPrepare` использует `_host.Project.CompileToIL(_case.ProgramText, ProjectBuilder.BaseAddressProgram, optimize: true, SourceLanguage.C).Build()`. Загрузка в устройство — через `ctx.TryLoadProgram(res.Program, 0, out string? errorMath)`. Пеephole-оптимизатор не применяется (см. [Особенная информация](#особенная-информация)).

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

`ExpectedInstructions` **не используется** ни в `BenchRunner`, ни в `Program.cs`. Задел на будущее (сверка фактических шагов с ожидаемыми). См. [Что исправить, п.5](#что-исправить).

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

## Примеры для API

**Добавить свой кейс через `SourceCasesMiniC`:**

```csharp
// SourceCasesMiniC.cs
public const string myNewMc =
"""
int main() {
    int r0 = 0;
    int r1 = 1000000;
    while (r1 != 0) {
        r0 = r0 ^ r0;
        r1 = r1 - 1;
    }
    return r0;
}
""";

// Program.cs, в массиве cases:
new BenchCase("XorSelf", SourceCasesMiniC.myNewMc, Runs: _runs, WarmupRuns: _runsWarmup)
```

**Использовать `BenchRunner` в своём коде (не через `Program.Main`):**

```csharp
using var host = VmHostFactory.CreateDefault();
int deviceId = host.Emulator.RegisterDevice(
    DeviceSpec.Create(RamSize.MB64, "Bench"), sector: 0);

var runner = new BenchRunner(host, deviceId,
    new BenchCase("MyCase", SourceCasesMiniC.myNewMc, Runs: 10, WarmupRuns: 3));

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

**Средняя.** Это не библиотека, а «продукт» — образец использования и инструмент для оценки производительности VM. Конечному пользователю MiniC бенчмарк не нужен; автору ядра/компилятора — полезен для регрессий.

**Что стоит делать:**
- Запускать на **изолированной машине** (или хотя бы без нагрузки) — иначе affinity/priority не помогут.
- На Linux для `PriorityClass.High` — `sudo`, иначе будет `[Предупреждение]` и обычный приоритет.
- Читать логи из `logs/` — консоль может обрезать вывод (`PrintProgress` пишет в одну строку с `\r`).
- Расширять набор кейсов через `BenchCase` + `SourceCasesMiniC`, а не через форк `BenchRunner`.
- Для сравнения «с оптимизациями vs без» — добавьте флаг `optimize` в `BenchCase` и пробросьте его в `BenchRunner.TryPrepare` вместо жёсткого `true`.

**Что не стоит делать:**
- Запускать бенчмарк в CI/headless — `Console.ReadLine()` в конце `Main` заблокирует процесс, а `Console.Clear()` может бросить.
- Сравнивать MIPS между машинами — привязка к ядру, частота, JIT, GC, thermal throttling сильно влияют. Сравнивайте только в пределах одной сессии.
- Ожидать, что `optimize: true` в `BenchRunner.TryPrepare` даст тот же эффект, что AST-оптимизации + peephole для ASM: `optimize: true` для MiniC включает AST-оптимизации, но peephole (`VmPeepholeOptimizer.AddStdRules`) не вызывается автоматически.
- Использовать `PeakMips` как единственную метрику — он берёт `Min(Elapsed)`, что чувствительно к выбросам. `TrimmedMean10` из `MipsLogs` надёжнее.

## Особенная информация

### `optimize: true` для MiniC

`BenchRunner.TryPrepare` вызывает `_host.Project.CompileToIL(_case.ProgramText, ProjectBuilder.BaseAddressProgram, optimize: true, SourceLanguage.C).Build()`. Для MiniC-источников `optimize: true` включает AST-оптимизации компилятора, но **не** включает peephole: `VmPeepholeOptimizer.AddStdRules` никто не вызывает, а `OptimizationIL(..., peepholeOptimizationCount)` в `BenchRunner`/`Program` не используется. Поэтому все 14 кейсов — это AST-оптимизированный, но не peephole-оптимизированный код.

Если хотите сравнить «с peephole vs без», нужно явно:

```csharp
host.Project.Optimizator.VMPeepholeOptimizer.AddStdRules();
var il = host.Project.CompileToIL(text, ProjectBuilder.BaseAddressProgram, optimize: false, SourceLanguage.C);
il = host.Project.OptimizationIL(il, peepholeOptimizationCount: 5);
var comp = il.Build();
```

### `Stats` — почему несколько метрик

- `Mean` — чувствителен к выбросам (GC-паузы, thermal throttling).
- `Median` — устойчив, но не показывает «средний» сценарий.
- `TrimmedMean10` — компромисс: отбрасывает 10% сверху и снизу, игнорирует выбросы, показывает типичное значение. **Основная метрика** для отчёта.
- `CV` — коэффициент вариации; `CV < 5%` — стабильно, `CV > 15%` — шумно.
- `Skewness > 0` — хвост вправо (выбросы «медленные прогоны»); `< 0` — хвост влево.
- `FirstHalfMean`/`SecondHalfMean` — JIT/warmup: если `firstHalf > secondHalf` на 20%+, JIT ещё не прогрелся к концу warmup'а, стоит увеличить `WarmupRuns`.

### Вывод и формат лога

`Program.cs` использует:
- `PrintProgress` с `\r` и ANSI-символами `▓░✓✗•` — в CI/redirected output `Console.IsOutputRedirected` пропускает прогресс-бар, но `Console.Clear()` в конце `Main` — **не** пропускает (см. [Что исправить, п.4](#что-исправить)).
- `AppendStatsExtended` — все 20 полей `StatsResult` в формате `\t    name: value`.
- `AppendIterations` — по одному значению на строку, формат `F4`.

Лог-файл: `logs/result_Benchmark_Logs_{yyyy-MM-dd_HH-mm-ss}.txt` в `AppDomain.CurrentDomain.BaseDirectory`. Содержит **всё**: от прогресса до полного дизассемблера каждой программы.

### Ограничения платформ

- **`Console.ReadLine()`** в конце `Main` — блокирует. В CI/headless процесс зависнет. См. [Что исправить, п.4](#что-исправить).

### Что этот модуль не делает

- Не является библиотекой — это `Exe`.
- Не публикует результаты во внешнее хранилище (BenchmarkDotNet-style baseline) — только `logs/*.txt`.
- Не сравнивает версии Nantuc между собой — только измеряет текущую.
- Не использует `BenchmarkDotNet` — всё вручную, чтобы контролировать `LaunchOptions(ShowTimer: true)` и не зависеть от `VmHost` в чужом фреймворке.
- Не тестирует многопоточность — все кейсы однопоточные, один `Device` на всё.

## Что исправить

По убыванию серьёзности. Ссылки на файлы/методы.

1. **`BenchRunner.TryPrepare` — `Console.ReadLine()` в фейл-путях блокирует.**  
   Файл: `BenchRunner.cs`, `TryPrepare`, два `Console.ReadLine()` после `Console.WriteLine`.  
   Тот же риск, что и в `Main`. В `Program.Main` фейл обрабатывается через `PrintProgress(..., failed: true)` и `continue`, а `BenchRunner` сам блокирует — несогласованно.  
   Фикс: убрать `Console.ReadLine()` из `TryPrepare`, вернуть `false` и дать вызывающему решать (в `Main` уже есть обработка).

2. **`BenchRunner.PeakMips`/`StepsOnce` — берёт `StepsLogs[0]`, а не минимум/медиану.**  
   Файл: `BenchRunner.cs`, свойства `PeakMips` и `StepsOnce`.  
   `StepsOnce = _stepsLogs[0]` — если первый прогон был с другим поведением (например, GC между `TryPrepare` и первым `RunOnce`), число шагов может отличаться от последующих. `PeakMips = StepsOnce / Min(Elapsed)` смешивает шаги одного прогона с временем другого.  
   Фикс: `StepsOnce` → `_stepsLogs.Min()` (или `Median`), `PeakMips` → `_stepsLogs.Zip(_elapsedSecondsLogs).Max(p => p.First / p.Second / 1e6)`.

3. **`BenchRunner.Warmup`/`RunOnce` — warmup пишет в пустые списки.**  
   Файл: `BenchRunner.cs`, методы `Warmup` и `RunOnce`.  
   `Warmup` вызывает `Launch([], [], [], ctx)` — `Metric` добавляет запись в пустые списки, которые сразу выбрасываются. Аллокация на каждый warmup-прогон (3 списка) — мелочь, но некрасиво.  
   Фикс: `Launch` с `onMetric: null` — не добавлять, если списки пустые, или отдельный overload без списков.

4. **`Program.Main` — `Console.Clear()` без `try/catch`.**  
   Файл: `Program.cs`, `Main`: `Console.Clear();` после `totalSw.Stop()`.  
   На headless (перенаправленный вывод, CI, некоторые терминалы) бросает `IOException`. Весь бенчмарк уже посчитан, но лог не сохранится.  
   Фикс: `try { Console.Clear(); } catch (IOException) { /* ignore */ }`.

5. **`BenchCase.ExpectedInstructions` не используется.**  
   Файл: `BenchCase.cs` (поле) + `Program.cs` (все `new BenchCase(...)` без него) + `BenchRunner` (не читает).  
   Поле есть, но `BenchRunner` не сверяет фактические `StepsLogs` с ожидаемыми. Если VM выдаст аномальное число шагов (например, из-за бага в `Processor.Step`), бенчмарк это не заметит — MIPS будет «правильный», но шаги — нет.  
   Фикс: в `BenchRunner.RunOnce` после прогона: `if (_case.ExpectedInstructions is { } expected && steps[^1] != expected) Console.Error.WriteLine($"Steps mismatch: expected {expected}, got {steps[^1]}");`. Заполнить `ExpectedInstructions` для всех кейсов (значения можно взять из эталонного прогона).

6. **`Program.cs` — `PrintProgress` для `fail` передаёт `done=0, phaseTotal=0`.**  
   Файл: `Program.cs`, `Main`, ветка `if (!runner.TryPrepare())`:
   ```csharp
   PrintProgress(testIndex, testTotal, name, "fail", 0, 0,
                 phaseTotal, phaseTotal, globalStep, totalSteps, forceNewLine: true, failed: true);
   ```
   `done=0, phaseTotal=0` — в `phaseStr` не используется (`phase == "fail"` → `$"{phase,-4}"`), но в `testPct = phaseStepsTotal <= 0 ? 1.0 : ...` — `phaseStepsTotal = phaseTotal > 0`, так что процент считается по `phaseStep/phaseTotal = 1.0`. Не баг, но параметры «0, 0» вводят в заблуждение — либо `phaseTotal, phaseTotal`, либо добавить комментарий.

7. **`Stats.Percentile`/`Median`/`TrimmedMean` — `OrderBy().ToArray()` на каждый вызов.**  
   Файл: `Stats.cs`, методы `Percentile`, `Median`, `TrimmedMean`.  
   `StatsResult.From` вызывает `Percentile` 10 раз, `Median` 1 раз, `TrimmedMean` 2 раза — итого 13 сортировок одного и того же массива. Для `n=20` незаметно, но при `n=1000` (если расширите `Runs`) — заметно.  
   Фикс: отсортировать один раз в `StatsResult.From` и передавать `ReadOnlySpan<double>`/отсортированный массив. Или кэшировать в локальной переменной внутри `From`.

8. **`BenchRunner.cs` — `ListExtensions` в том же файле.**  
   Файл: `BenchRunner.cs`, `file static class ListExtensions`.  
   `ComputeVariance` — единственный extension в проекте, лежит рядом с `BenchRunner`. Для навигации неудобно.  
   Фикс: вынести в `Stats.cs` как `public static double Variance(IReadOnlyList<double> v)` и не делать extension (extension на `IReadOnlyList<double>` — сомнительная практика, конфликтует с `System.Linq`).

9. **`Program.cs` — `Console.WriteLine($"Файл результатов...")` с кириллицей и `Console.ReadLine()`.**  
   Файл: `Program.cs`, конец `Main`.  
   В консоли Windows с кодовой страницей 866/1251 кириллица может отображаться кракозябрами, если не выставлен `Console.OutputEncoding = Encoding.UTF8`.  
   Фикс: `Console.OutputEncoding = System.Text.Encoding.UTF8;` в начале `Main` (или `Console.OutputEncoding = new UTF8Encoding(false)` для CI).

10. **`BenchRunner.TryPrepare` — `Program = res` до `TryLoadProgram`.**  
    Файл: `BenchRunner.cs`, `TryPrepare`:
    ```csharp
    var ctx = _host.Emulator.GetDeviceContext(_deviceId)!;
    Program = res;
    if (!ctx.TryLoadProgram(res.Program, 0, out string? errorMath)) { ... return false; }
    ```
    Если загрузка упала, `Program` уже установлен, а `IsPrepared = false`. Если вызывающий код смотрит на `Program`, он увидит «успешную» компиляцию, но программа не загружена.  
    Фикс: `Program = res` после успешной загрузки. Или задокументировать, что `Program` — результат компиляции, не загрузки.

11. **`BenchRunner.TryPrepare` — жёсткий `optimize: true` и отсутствие peephole.**  
    Файл: `BenchRunner.cs`, `TryPrepare` + `BenchCase.cs`.  
    `optimize: true` захардкожен, `BenchCase` не позволяет сравнить с `optimize: false`. Плюс peephole не вызывается (см. [Особенная информация](#особенная-информация)).  
    Фикс: добавить в `BenchCase` флаг `OptimizeAst` (по умолчанию `true`) и, опционально, `RunPeephole` — чтобы можно было измерять эффект оптимизаций, а не только «голый» запуск.
```