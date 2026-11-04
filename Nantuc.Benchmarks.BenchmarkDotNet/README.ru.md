# Nantuc.Benchmarks.BenchmarkDotNet — README

> Бенчмарк виртуальной машины Nantuc на базе **BenchmarkDotNet**: измеряет MIPS, стабильность и аллокации на 14 типовых MiniC-программах (вызовы, ALU, память, битовые операции, умножение, ветвления, деление, рекурсия, указатели, структуры, сортировка).  
> Зависит от `[Nantuc.VmApplication.Extensions](../Nantuc.VmApplication.Extensions/README.ru.md)` (транзитивно — от `VmApplication`, `Compiler`, `Kernel`).  
> `[← Корень](../README.ru.md) · [VmApplication](../Nantuc.VmApplication/README.ru.md) · [Extensions](../Nantuc.VmApplication.Extensions/README.ru.md) · [Kernel](../Nantuc.Kernel/README.ru.md) · [Compiler](../Nantuc.Compiler/README.ru.md)`

## Содержание
- [Об этом модуле](#об-этом-модуле)
- [Дерево локального проекта](#дерево-локального-проекта)
- [API](#api)
- [Примеры для API](#примеры-для-api)
- [Приближенность модуля к пользователю](#приближенность-модуля-к-пользователю)
- [Особенная информация](#особенная-информация)
- [Что исправить](#что-исправить)

## Об этом модуле

`Nantuc.Benchmarks.BenchmarkDotNet` — это не библиотека, а **консольное приложение** (`<OutputType>Exe</OutputType>`), которое использует **BenchmarkDotNet** для измерения производительности виртуальной машины Nantuc. Это альтернатива ручному бенчмарку `[Nantuc.Benchmarks](../Nantuc.Benchmarks/README.ru.md)`: вся статистика, warmup, обработка выбросов и генерация отчётов делегированы BenchmarkDotNet.

Модуль:

1. Создаёт `VmHost` через `VmHostFactory.CreateDefault` и регистрирует одну `Device` (RAM 64 MiB) на `sector 0`.
2. Компилирует и загружает MiniC-программу в `GlobalSetup` — **вне замера**.
3. Для каждого из 14 кейсов (`SourceCasesMiniC.AllCases`) выполняет `[Benchmark]`-метод `RunVm`, который запускает ВМ один раз (`ctx.Run`) и возвращает число выполненных шагов.
4. Выводит стандартный отчёт BenchmarkDotNet (время, аллокации, статистика) и сохраняет артефакты в `BenchmarkDotNet.Artifacts/`.

Ключевые роли:
- **`NantucBenchmarks.cs`** — класс бенчмарка с `[ShortRunJob]`, `[MemoryDiagnoser]`, `[ParamsSource]`, `GlobalSetup`/`GlobalCleanup` и `[Benchmark] RunVm`. Здесь же определён `record BenchCase`.
- **`SourceCasesMiniC.cs`** — тексты 14 MiniC-программ и статическое свойство `AllCases`, возвращающее `IEnumerable<BenchCase>`.
- **`Program.cs`** — точка входа: `BenchmarkRunner.Run<NantucBenchmarks>()`.

Модуль — образец того, **как использовать `VmApplication` совместно с BenchmarkDotNet**: он не трогает `Kernel` напрямую, только через `VmHost`/`DeviceContext`, и показывает, что публичного API хватает для бенчмарка без хаков.

## Дерево локального проекта

Namespace — `Nantuc.Benchmarks.BenchmarkDotNet`:

```
Nantuc.Benchmarks.BenchmarkDotNet/
├── Nantuc.Benchmarks.BenchmarkDotNet.csproj  # net10.0, Exe, BenchmarkDotNet 0.15.2, VSDiagnostics
├── Program.cs                                # BenchmarkRunner.Run<NantucBenchmarks>()
├── NantucBenchmarks.cs                       # класс бенчмарка + record BenchCase
└── SourceCasesMiniC.cs                       # 14 MiniC-программ + AllCases
```

Замечания по организации:
- `BenchCase` определён в `NantucBenchmarks.cs`, хотя логичнее вынести его в отдельный файл (как в ручном бенчмарке `Nantuc.Benchmarks`).
- `SourceCasesMiniC.AllCases` создаёт новый список при каждом обращении; для BenchmarkDotNet это не критично (вызывается один раз при построении параметров), но можно закэшировать.
- Поля `Runs` и `WarmupRuns` в `BenchCase` не используются BenchmarkDotNet — это наследие ручного бенчмарка (см. [Что исправить, п.1](#что-исправить)).

## API

Проект — `Exe`, публичного API нет. Ниже — внутренняя структура, полезная для правок и расширения.

### Точка входа

`Program.cs` (top-level statements):

```csharp
using BenchmarkDotNet.Running;

BenchmarkRunner.Run<NantucBenchmarks>();
```

### `NantucBenchmarks`

```csharp
[ShortRunJob]
[MemoryDiagnoser]
public class NantucBenchmarks
{
    [ParamsSource(nameof(Cases))]
    public BenchCase Case { get; set; }

    public static IEnumerable<BenchCase> Cases => SourceCasesMiniC.AllCases;

    [GlobalSetup]    public void GlobalSetup();
    [GlobalCleanup]  public void GlobalCleanup();

    [Benchmark]
    public long RunVm();
}
```

- `[ShortRunJob]` — быстрый режим (мало итераций). Для точных цифр замените на `[MediumRunJob]` или `[LongRunJob]`.
- `[MemoryDiagnoser]` — показывает аллокации managed-памяти.
- `[ParamsSource]` — BenchmarkDotNet переберёт все кейсы из `SourceCasesMiniC.AllCases`.
- `GlobalSetup`:
  - создаёт `VmHost` и регистрирует устройство;
  - компилирует `Case.ProgramText` через `CompileToIL(..., optimize: true, SourceLanguage.C)`;
  - при ошибке компиляции бросает `InvalidOperationException` с перечнем ошибок;
  - загружает программу через `ctx.TryLoadProgram(res.Program, 0, out error)`;
  - вызывает `GC.Collect(2, GCCollectionMode.Forced, true)` и `GC.WaitForPendingFinalizers()`.
- `GlobalCleanup` — освобождает `VmHost`.
- `RunVm`:
  - получает `DeviceContext`;
  - создаёт `LaunchOptions(ShowTimer: true, OnEnd: res => steps = res.Steps)`;
  - вызывает `ctx.Run(opt)`;
  - возвращает `steps` (чтобы JIT не выбросил вызов).

### `BenchCase`

```csharp
public record BenchCase(
    string Name,
    string ProgramText,
    int Runs = 1,
    int WarmupRuns = 1)
{
    public override string ToString() => Name;
}
```

`Runs` и `WarmupRuns` **не используются** BenchmarkDotNet (он сам управляет итерациями). Оставлены для совместимости с ручным бенчмарком. `ToString()` возвращает `Name` — это то, что BenchmarkDotNet покажет в отчёте как имя параметра.

### `SourceCasesMiniC`

```csharp
internal static class SourceCasesMiniC
{
    public const string callMc = """...""";
    // ... mathMc, aluTput1Mc, aluTput4Mc, aluTputMixedMc,
    //     memoryMc, bitOpsMc, mulMc, branchMc, divModMc,
    //     recursionMc, pointerMc, structMc, sortMc

    public static IEnumerable<BenchCase> AllCases =>
    [
        new BenchCase("Call",         callMc),
        new BenchCase("Math",         mathMc),
        new BenchCase("AluTput1",     aluTput1Mc),
        new BenchCase("AluTput4",     aluTput4Mc),
        new BenchCase("AluTputMixed", aluTputMixedMc),
        new BenchCase("Memory",       memoryMc),
        new BenchCase("BitOps",       bitOpsMc),
        new BenchCase("Mul",          mulMc),
        new BenchCase("Branch",       branchMc),
        new BenchCase("DivMod",       divModMc),
        new BenchCase("Recursion",    recursionMc),
        new BenchCase("Pointer",      pointerMc),
        new BenchCase("Struct",       structMc),
        new BenchCase("Sort",         sortMc),
    ];
}
```

| Кейс | Что меряет |
|---|---|
| `Call` | Вызов функции `sum(r0, r1)` в цикле (3M итераций) |
| `Math` | `ADD` + `DEC` + `TEST` + `JNZ` (3M итераций) |
| `AluTput1` | 4× `ADD` в один регистр + цикл (800K итераций) |
| `AluTput4` | 4× `ADD` в разные регистры (ILP) + цикл (800K итераций) |
| `AluTputMixed` | `ADD`/`SUB`/`AND`/`OR` в разные регистры (800K итераций) |
| `Memory` | Заполнение массива `int[25000]` и суммирование, 20 повторов |
| `BitOps` | `SHR`/`AND`/`OR`/`XOR` (3M итераций) |
| `Mul` | `MULT_INT` (3M итераций) |
| `Branch` | `if ((r6 & 1) != 0)` — чёт/нечет (500K итераций) |
| `DivMod` | `DIV` + `MOD` (500K итераций) |
| `Recursion` | Рекурсивный `down(500)` × 500 вызовов |
| `Pointer` | Арифметика указателей `*(p + i)` (2000 повторов) |
| `Struct` | Доступ к полям `struct Point` в массиве (200 повторов) |
| `Sort` | Пузырьковая сортировка `int[200]` (50 повторов) |

## Примеры для API

**Добавить свой кейс:**

```csharp
// в SourceCasesMiniC.cs
public const string myCaseMc =
"""
int main() {
    int r0 = 0;
    int r6 = 1000000;
    while (r6 != 0) {
        r0 = r0 ^ r0;
        r6 = r6 - 1;
    }
    return r0;
}
""";

// в AllCases:
new BenchCase("XorSelf", myCaseMc),
```

**Запустить только выбранные кейсы (через аргументы командной строки):**

```bash
dotnet run -c Release -- --filter *Call* *Memory*
```

**Заменить Job на более точный:**

```csharp
// вместо [ShortRunJob]
[SimpleJob(RunStrategy.Throughput, launchCount: 3, warmupCount: 5, iterationCount: 10)]
// или
[MediumRunJob]
```

**Запустить из кода (не через `Program.Main`):**

```csharp
using BenchmarkDotNet.Running;
using Nantuc.Benchmarks.BenchmarkDotNet;

var summary = BenchmarkRunner.Run<NantucBenchmarks>();
```

**Получить число шагов в отчёте:**

`RunVm` возвращает `long steps`. BenchmarkDotNet покажет это как `Result` (не как основную метрику, но это предотвращает dead-code elimination). Основная метрика — время выполнения.

## Приближенность модуля к пользователю

**Средняя.** Это инструмент для авторов ядра/компилятора VM, а не для конечных пользователей MiniC. BenchmarkDotNet даёт готовую статистику и отчёты, но требует понимания его модели (Job, Warmup, Iteration, Params).

**Что стоит делать:**
- Запускать в **Release**-конфигурации: `dotnet run -c Release`.
- Для точных цифр заменить `[ShortRunJob]` на `[MediumRunJob]` или `[LongRunJob]` — `ShortRunJob` даёт шумные результаты.
- Использовать `--filter` для выбора конкретных кейсов, чтобы не гонять все 14.
- Смотреть артефакты в `BenchmarkDotNet.Artifacts/` (HTML-отчёт, CSV, лог).
- Помнить, что `GlobalSetup` выполняется **один раз** на кейс, а `RunVm` — много раз; состояние устройства сохраняется между итерациями (см. [Особенная информация](#особенная-информация)).

**Что не стоит делать:**
- Сравнивать MIPS между машинами — BenchmarkDotNet предупреждает о нестабильности, но привязка к ядру, частота, JIT, GC и thermal throttling всё равно влияют.
- Ожидать, что `Runs`/`WarmupRuns` из `BenchCase` что-то изменят — BenchmarkDotNet их игнорирует.
- Запускать в CI без `--filter` и без ограничения времени — полный прогон 14 кейсов с `MediumRunJob` может занять десятки минут.

## Особенная информация

### `ShortRunJob` vs `MediumRunJob`/`LongRunJob`

`[ShortRunJob]` — это `Job.ShortRun`: мало итераций, быстрый прогон, но высокий разброс. Подходит для быстрой проверки, что бенчмарк работает. Для сравнения версий VM используйте `[MediumRunJob]` (рекомендуется) или `[LongRunJob]`.

### `GlobalSetup` и состояние устройства

`GlobalSetup` компилирует и загружает программу **один раз** на кейс. Затем `RunVm` вызывает `ctx.Run(opt)` многократно на одном и том же `DeviceContext`. Это значит, что память устройства **не сбрасывается** между итерациями. Для кейсов, которые модифицируют память (`Memory`, `Pointer`, `Struct`, `Sort`), результаты могут зависеть от предыдущих прогонов (например, массив уже заполнен). Это соответствует поведению ручного бенчмарка `Nantuc.Benchmarks` (там тоже `RunOnce` вызывался многократно на одном контексте). Если нужна изоляция — добавляйте сброс состояния в начало программы или используйте отдельный `DeviceContext` на итерацию (но это уже за пределами `[Benchmark]`).

### `optimize: true` для MiniC

В отличие от ASM-бенчмарка, здесь `CompileToIL(..., optimize: true, SourceLanguage.C)` действительно включает AST-оптимизации, так как язык — C (MiniC). Это корректно.

### `GC.Collect` в `GlobalSetup`

Вызов `GC.Collect(2, GCCollectionMode.Forced, true)` и `GC.WaitForPendingFinalizers()` наследует ручной бенчмарк. BenchmarkDotNet сам выполняет сборку мусора между итерациями, поэтому этот вызов скорее «для успокоения». Можно удалить без потери точности.

### `LaunchOptions(ShowTimer: true)`

`ShowTimer: true` включает внутренний таймер VM и заполняет `res.Elapsed`/`res.Steps` в `OnEnd`. BenchmarkDotNet измеряет время `RunVm` снаружи; внутренний таймер VM нужен только для получения `Steps`. Если убрать `ShowTimer`, `OnEnd` может не получить корректные данные.

### Артефакты

BenchmarkDotNet сохраняет результаты в `BenchmarkDotNet.Artifacts/`:
- `results/*.md` — Markdown-отчёт;
- `results/*.csv` — CSV с метриками;
- `log/*.log` — лог выполнения;
- `*.html` — HTML-отчёт (если включён экспортёр).

### Пакет `Microsoft.VisualStudio.DiagnosticsHub.BenchmarkDotNetDiagnosers`

Добавляет диагностику для Visual Studio. Для CI и обычного запуска не требуется; можно удалить, если не используете VS-диагностику.

### Что этот модуль не делает

- Не является библиотекой — это `Exe`.
- Не публикует baseline-результаты во внешнее хранилище.
- Не сравнивает версии Nantuc между собой — только измеряет текущую.
- Не тестирует многопоточность — все кейсы однопоточные, один `Device` на всё.

## Что исправить

По убыванию серьёзности.

1. **`BenchCase.Runs`/`WarmupRuns` не используются BenchmarkDotNet.**  
   Файл: `NantucBenchmarks.cs`, `record BenchCase`.  
   Поля есть, но BenchmarkDotNet управляет итерациями сам. Читатель может подумать, что они влияют на замер.  
   Фикс: удалить `Runs`/`WarmupRuns` из `BenchCase` (оставить только `Name` и `ProgramText`) или добавить комментарий `// ignored by BenchmarkDotNet`.

2. **`_case = Case;` — избыточное присваивание.**  
   Файл: `NantucBenchmarks.cs`, `GlobalSetup`: поле `_case` инициализируется значением свойства `Case`, которое и так доступно.  
   Фикс: использовать `Case` напрямую в `GlobalSetup` и `RunVm` (или оставить поле, но убрать присваивание — оно ничего не даёт).

3. **`using Microsoft.VSDiagnostics;` не используется.**  
   Файл: `NantucBenchmarks.cs`.  
   Фикс: удалить `using`, если не используются атрибуты/классы из этого namespace.

4. **`[ShortRunJob]` может давать шумные результаты.**  
   Файл: `NantucBenchmarks.cs`, атрибут класса.  
   Для сравнения версий VM `ShortRunJob` слишком неточен.  
   Фикс: заменить на `[MediumRunJob]` (или `[LongRunJob]`) либо оставить `ShortRunJob` с комментарием «для быстрой проверки, для точных цифр — MediumRunJob».

5. **`SourceCasesMiniC.AllCases` создаёт новый список при каждом обращении.**  
   Файл: `SourceCasesMiniC.cs`.  
   BenchmarkDotNet вызывает `ParamsSource` один раз, но если кто-то будет использовать `AllCases` в цикле — лишние аллокации.  
   Фикс: закэшировать в `static readonly IReadOnlyList<BenchCase>`.

6. **`Program.cs` — top-level statements без `using System;`.**  
   Файл: `Program.cs`.  
   Работает за счёт implicit usings (если включены). В csproj не указан `<ImplicitUsings>enable</ImplicitUsings>`, но SDK для net10.0 может включать по умолчанию. Если отключить — понадобится `using System;`.  
   Фикс: явно включить `<ImplicitUsings>enable</ImplicitUsings>` в csproj или добавить `using System;`.

7. **`<Nullable>` не включён.**  
   Файл: `Nantuc.Benchmarks.BenchmarkDotNet.csproj`.  
   В коде есть `null!` и nullable-аннотации, но без `<Nullable>enable</Nullable>` они не проверяются.  
   Фикс: добавить `<Nullable>enable</Nullable>` (и, возможно, `<ImplicitUsings>enable</ImplicitUsings>`).

8. **`RunVm` возвращает `long steps`, но BenchmarkDotNet не использует это как метрику.**  
   Файл: `NantucBenchmarks.cs`, `[Benchmark] RunVm`.  
   Возврат предотвращает dead-code elimination, но `steps` не попадает в отчёт как отдельная метрика (только `Result`). Если нужны шаги в отчёте — можно добавить `[Benchmark]`-метод, возвращающий `steps`, и настроить `[BenchmarkCategory]`, либо использовать `Consumer`.  
   Фикс: задокументировать, что `steps` — только защита от оптимизатора; для анализа шагов используйте ручной бенчмарк или `[Benchmark]` с возвратом `steps` и отдельной категорией.

9. **`Microsoft.VisualStudio.DiagnosticsHub.BenchmarkDotNetDiagnosers` — лишняя зависимость для CI.**  
   Файл: `Nantuc.Benchmarks.BenchmarkDotNet.csproj`.  
   Пакет нужен только для диагностики в Visual Studio.  
   Фикс: удалить `PackageReference`, если не используете VS-диагностику (или оставить с комментарием).