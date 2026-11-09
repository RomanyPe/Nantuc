# Бенчмарки OtherLangs
> `[← Корень](../../README.ru.md)`

Сравнение виртуальной машины [Nantuc](https://github.com/RomanyPe/Nantuc) с другими языками
программирования на идентичном наборе кейсов из `SourceCasesMiniC.cs`.

## Зачем это здесь

[`Nantuc.Benchmarks.BenchmarkDotNet`](../Nantuc.Benchmarks.BenchmarkDotNet/README.ru.md) измеряет производительность Nantuc **in-process**
через BenchmarkDotNet. Это удобно для регрессий, но не позволяет сравнивать с другими
языками: для них нужны отдельные процессы, свои раннеры и свои прогревы.

`OtherLangs` — это отдельный канал измерения: каждое решение (Python, Lua, Ruby, JS,
C, C++, C#, Java, Rust) исполняется в своём нативном окружении, а результаты сводятся
в единый файл.

**BDN и OtherLangs намеренно не смешиваются:**

- BDN оптимизирован для C# и in-process измерений (JIT-прогрев, GC-контроль, статистика).
- Другие языки требуют своих инструментов (CPython, PUC Lua, MRI, V8, JVM, .NET, LLVM).
- Попытка обернуть чужие процессы в `[Benchmark]`-метод даёт искажения: шум старта
  процесса, отсутствие прогрева, таймауты.

Итог: **BDN — для регрессий Nantuc, OtherLangs — для сравнения с другими языками.**
Сводятся они на уровне отчёта, а не на уровне раннера.

## Структура

```
OtherLangs/
├── README.md
├── README.ru.md
├── langs.json              # манифест: id, name, category, command, cwd
├── runner/
│   ├── run.ps1             # Windows PowerShell (основной)
│   └── run.py              # кросс-платформенный Python-раннер
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

## Контракт вывода

Каждая программа обязана печатать результаты **в stderr** (раннер читает и stdout, и
stderr — на случай, если язык пишет не туда) в формате:

```
<Name>  <seconds> s  (result=<N>)
```

Пример:

```
Call          1.2345 s  (result=4500001500000)
Math          0.5678 s  (result=4500001500000)
```

Требования:

- `<Name>` — имя кейса из `SourceCasesMiniC` (`Call`, `Math`, `AluTput1`, `AluTput4`,
  `AluTputMixed`, `Memory`, `BitOps`, `Mul`, `Branch`, `DivMod`, `Recursion`,
  `Pointer`, `Struct`, `Sort`).
- `<seconds>` — вещественное число, **точка** как разделитель (не запятая).
- `<N>` — целое число (результат вычисления; нужен раннеру для проверки корректности
  и чтобы JIT-компиляторы не выкинули цикл целиком).

Именно этот формат уже реализован в скриптах всех языков. Если добавляете новый язык —
сохраняйте формат, иначе `runner` его не распарсит.

## Скриптовые языки (Python, Lua, LuaJIT, Ruby, JavaScript)

Эти языки запускаются **напрямую интерпретатором**, без отдельной сборки. `runner` сам
находит нужный исполняемый файл в `PATH`, запускает скрипт из соответствующего каталога
и парсит результаты.

### Предварительные требования

| Язык | Интерпретатор в `PATH` | Проверка |
|---|---|---|
| Python | `python` (или `python3`/`py` на Windows) | `python --version` |
| Lua (PUC) | `lua` | `lua -v` |
| LuaJIT | `luajit` | `luajit -v` |
| Ruby (MRI) | `ruby` | `ruby --version` |
| JavaScript | `node` | `node --version` |

Если интерпретатор называется иначе (`python3.12`, `lua54`, `lua53`), поправьте поле
`run` в `langs.json` — именно оно задаёт точную команду запуска. Раннер не угадывает
имена, а исполняет ровно то, что написано в манифесте.

### Запуск

**PowerShell (Windows):**

```powershell
cd OtherLangs\runner
.\run.ps1
```

**Python (кросс-платформенно):**

```bash
cd OtherLangs/runner
python run.py
```

Обе команды читают `../langs.json`, прогоняют каждый доступный язык и складывают
результат в `results.json` и `results.csv` в текущем каталоге (относительно `runner/`).

### Полезные флаги

| Назначение | PowerShell | Python |
|---|---|---|
| Только один язык | `-Languages python,ruby` | `--langs python ruby` |
| Только отдельные кейсы | `-Cases Math,Sort` | `--cases Math Sort` |
| Таймаут на язык, сек | `-Timeout 600` | `--timeout 600` |
| Повторов на кейс (берётся min) | `-Repeat 5` | `--repeat 5` |
| Verbose (сырой вывод) | `-Verbose` | `-v` / `--verbose` |
| Выходной файл | `-Output foo.json` | `-o foo.json` |

Пример: только Python и Lua, только `Call` и `Math`, три повтора:

```powershell
.\run.ps1 -Languages python,lua -Cases Call,Math -Repeat 3
```

```bash
python run.py --langs python lua --cases Call Math --repeat 3
```

### Выход

- `results.json` — массив записей `{lang, language, category, case, seconds, result}`.
- `results.csv` — то же в CSV.
- Сводная таблица печатается в консоль: строки — кейсы, столбцы — языки, значения —
  секунды (минимальные, если использовался `-Repeat`).

## Компилируемые языки (C, C++, C#, Java, Rust)

Все компилируемые решения используют те же 14 кейсов и тот же формат вывода, что и скриптовые. **Сборка не входит в измеряемое время**: раннер запускает уже готовый бинарь (или JIT-рантайм), поэтому собирайте заранее.

Каждый бенчмарк делает один прогревочный прогон перед замером (`main` вызывает каждый кейс один раз, затем — снова уже с таймером). Для JIT-языков этого мало, подробности — ниже.

### Быстрая шпаргалка

| Язык | Сборка | Запуск |
|---|---|---|
| C | `gcc -O2 -fwrapv benchmarks.c -o benchmarks` | `./benchmarks` |
| C++ | `g++ -O2 -fwrapv benchmarks.cpp -o benchmarks` | `./benchmarks` |
| C# | `dotnet run -c Release` | (сразу запускает) |
| Java | `javac Benchmarks.java` | `java Benchmarks` |
| Rust (Cargo) | `cargo build --release` | `./target/release/benchmarks` |
| Rust (rustc) | `rustc -O main.rs -o benchmarks` | `./benchmarks` |

### C

```bash
cd OtherLangs/c
gcc -O2 -fwrapv benchmarks.c -o benchmarks
./benchmarks
```

- `-fwrapv` **обязателен**: в C переполнение знакового `int` — UB, и без флага компилятор вправе выбросить циклы или свернуть их в константу (арифметика в кейсах намеренно переполняется так же, как в Nantuc и исходных `SourceCasesMiniC`).
- `-O2` — стандартный уровень; `-O3` и `-march=native` дадут другие числа, но это уже другой baseline — фиксируйте его в отчёте.
- `sum_fn` помечен `__attribute__((noinline))` — измеряется реальный overhead вызова, как в C++ и Rust.

### C++

```bash
cd OtherLangs/cpp
g++ -O2 -fwrapv benchmarks.cpp -o benchmarks
./benchmarks
```

Всё то же, что и в C: `-fwrapv` обязателен, `-O2` — дефолт, `sum_fn` помечен `[[gnu::noinline]]`.

### C#

```bash
cd OtherLangs/csharp
dotnet run -c Release
```

Если хотите отделить сборку от запуска:

```bash
dotnet build -c Release -o ./out
dotnet ./out/BenchmarksProject.dll
```

- Целевой фреймворк — `net10.0` (см. `BenchmarksProject.csproj`). В нём **безусловно** стоит `<Optimize>true</Optimize>`, поэтому `-c Release` влияет в основном на пути вывода, но не на кодоген. Тем не менее, `-c Release` — конвенция, которую видит большинство читателей, и её стоит придерживаться.
- `Sum` помечен `[MethodImpl(MethodImplOptions.NoInlining)]` — намеренно, чтобы измерить overhead вызова. Остальные методы бенчмарка вызываются через `Func<long>[]`, что тоже мешает инлайну.
- ⚠️ **Прогрев: один вызов — этого мало.** RyuJIT компилирует метод при первом вызове, а тиры (tiering) прогреваются секунды. Для честных чисел используйте **BenchmarkDotNet** (`[Benchmark]`, `[WarmupCount]`, `[IterationCount]`, `[MemoryDiagnoser]`): он сам прогревает, изолирует JIT-фазу, отдаёт статистику. Числа из текущего одноразового замера — ориентир, а не истина.

### Java

```bash
cd OtherLangs/java
javac Benchmarks.java
java Benchmarks
```

- `javac` не нуждается в флагах: переполнение `int` в Java определено как wrapping, поэтому `-fwrapv`-аналог не требуется.
- ⚠️ **Прогрев: один вызов — этого мало.** HotSpot C2 компилирует метод после ~10 000 вызовов/итераций. Для честных чисел используйте **JMH** (`org.openjdk.jmh`): `@Benchmark`, `@Warmup`, `@Fork`, `@BenchmarkMode(Mode.AverageTime)`, `@State`.
- Если хочется форсировать C2 в текущем одноразовом замере, добавьте `-Xbatch`: компиляция идёт синхронно при первом «горячем» вызове. Учтите, что это **меняет картину**: без профиля деоптимизации ведут себя иначе, чем в «обычном» режиме. Для сравнений лучше JMH, а не `-Xbatch`.

### Rust

**Через Cargo (рекомендуется — `Cargo.toml` уже настроен):**

```bash
cd OtherLangs/rust
cargo build --release
./target/release/benchmarks
```

В `Cargo.toml` уже выставлено честное release-профилирование: `opt-level = 3`, `lto = "fat"`, `codegen-units = 1`, `panic = "abort"`, `overflow-checks = false`. Один бинарь — `[[bin]] name = "benchmarks"`, `path = "main.rs"`.

**Напрямую `rustc` (если файл называется `benchmarks.rs`):**

```bash
rustc -O benchmarks.rs -o benchmarks
./benchmarks
```

- `-O` включает `opt-level=2` и **отключает** `debug-assertions`, а значит и проверку переполнения — это то, что нужно: код намеренно использует `wrapping_add` / `wrapping_sub` / `wrapping_mul`, чтобы совпадать с C/C++ с `-fwrapv` и с Nantuc.
- `sum_fn` помечен `#[inline(never)]` — измеряется реальный overhead вызова.
- `bench_pointer` использует `*mut i32` и `add` — настоящая арифметика указателей (в отличие от эмуляции в скриптовых языках).

### О чём помнить при сравнении чисел

1. **`Call` в компилируемых и в скриптовых — это разные вещи.** В C/C++/Rust функция реально не инлайнится (`noinline` / `#[inline(never)]`), и вызов идёт через `call`. В C#/Java инлайнинг решает JIT (мы принудительно `NoInlining` только у `Sum`). В Python/Lua/Ruby вызов интерпретируется. Не сравнивайте `Call` между группами «в лоб».

2. **`Pointer` в C/C++/Rust — настоящая арифметика указателей.** В Python/Ruby/JS/Lua — эмуляция через индексы массивов/таблиц. Сравнивайте этот кейс **внутри группы** (у кого эмуляция эффективнее), а не между группами.

3. **`Struct` в трёх группах — три разные модели памяти:**
   - C/C++/Rust — плоский массив структур/`Vec<Point>`, поля упакованы;
   - C# — массив `readonly struct Point` (value type, без боксинга);
   - Java — массив объектов `Point` (каждый — отдельный объект в куче, ссылки);
   - скриптовые — массив словарей/объектов.

   Числа отражают именно эти модели, а не «скорость языка вообще».

4. **`BitOps` вырождается после первой итерации** (`r0 = 1 >> 1 = 0`), потому что кейс скопирован один-в-один из `SourceCasesMiniC`. Это ожидаемо и одинаково для всех языков.

5. **Один прогон — не истина.**
   - C/C++/Rust: перезапустите бинарь 5–10 раз, берите медиану (шум от ASLR, CPU governor, турбо-буста).
   - C#/Java: **только** BenchmarkDotNet / JMH. Без них разброс легко достигает 2–3× из-за JIT-тиров и деоптимизаций.
   - `-Repeat` в раннере усредняет шум между запусками процесса, но не внутри процесса — для JIT этого недостаточно.

6. **Время сборки не входит в измерение.** `gcc`/`g++`/`javac`/`dotnet build`/`cargo build` — отдельный шаг, раннер их не запускает. Если вы гоняете бенчмарк «одной командой» (`cargo run --release`, `dotnet run`), сборка тоже не попадёт в замер, но само поведение рантайма может отличаться (например, `dotnet run` может стартовать чуть иначе, чем `dotnet ./out/*.dll`).

## Как добавить новый язык

1. Создайте подкаталог `OtherLangs/<lang-id>/` с исходником `benchmarks.<ext>`.
2. Реализуйте все 14 кейсов, сохранив формат вывода.
3. Добавьте запись в `langs.json`:

   ```json
   {
     "id": "mylang",
     "name": "MyLang 1.0",
     "category": "scripting",
     "run": ["mylang", "benchmarks.mylang"],
     "cwd": "mylang"
   }
   ```

   Поле `category` — одно из `scripting` / `jit` / `native`. Используется только для
   группировки в отчётах; на запуск не влияет.

4. Запустите `runner`. Если интерпретатор в `PATH` есть — язык появится в
   `results.json`.

Никаких изменений в `runner/` вносить не нужно.

## О чём помнить при чтении результатов

1. **Один прогон — ориентир, а не истина.** Для стабильных чисел:
   - C/C++/Rust — перезапустите бинарь 5–10 раз, берите медиану;
   - C#/Java — используйте BenchmarkDotNet / JMH (см. раздел про компилируемые);
   - Python/Lua/Ruby — флаг `-Repeat` уменьшает шум, но разброс 5–15 % остаётся
     из-за ОС; для серьёзных выводов — запуск на изолированной машине.

2. **`Call` неоднороден по смыслу.** В C/C++/Rust функция помечена `noinline` —
   измеряется реальный overhead вызова. В C#/Java инлайнинг оставлен на JIT —
   измеряется то, что JIT решит. В Python/Lua/Ruby вызов интерпретируется.
   Прямое сравнение этих чисел между группами — сравнение разных вещей.

3. **`Pointer` и `Struct` в скриптовых языках — эмуляция.** В Python это работа
   со списком, в Ruby — с массивом, в JS — с `Int32Array`. В C/C++/Rust — настоящая
   арифметика указателей. Сравнивайте эти кейсы **внутри группы** (у кого эмуляция
   эффективнее), а не между группами.

4. **`BitOps` в оригинале вырождается** после первой итерации
   (`r0 = 1 >> 1 = 0`), потому что скопирован один-в-один из `SourceCasesMiniC`.
   Это ожидаемо и одинаково для всех языков.

5. **`Sort` (пузырьковая сортировка)** — единственный кейс, где важна кэш-локальность
   и предсказание ветвлений. Он самый долгий по абсолютному времени; следите, чтобы
   он не съедал 90 % прогона.

6. **Разные версии интерпретаторов дают разные числа.** Указывайте версии
   (`python --version`, `lua -v`, `node --version`, `ruby --version`) в отчётах —
   иначе сравнение между запусками бессмысленно.

## Лицензия и вклад

Скрипты — Apache 2.0, как и весь Nantuc. PR с новыми языками и кейсами приветствуются:
главное — сохранять формат вывода и добавлять запись в `langs.json`.