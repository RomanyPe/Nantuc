# Встроенные библиотеки (`Libraries`)

> Справочник по строковым константам `Nantuc.VmApplication.Extensions.StdLibraries.Libraries`. Эти строки — содержимое `heap.nanasm`, `alloc.minic`, `io.nanasm`, `print.minic`, `imath.minic`, `bitops.minic`, `ovf.minic` — предназначены для `#include` в исходниках MiniC/NANASM.
>
> `[← Корень](../README.ru.md) · [VmApplication.Extensions](README.ru.md) · [Compiler](../../src/Nantuc.Compiler/README.ru.md)`

## Содержание

- [Общее](#общее)
- [heap.nanasm](#heapnanasm)
- [alloc.minic](#allocminic)
- [io.nanasm](#ionanasm)
- [print.minic](#printminic)
- [imath.minic](#imathminic)
- [bitops.minic](#bitopsminic)
- [ovf.minic](#ovfminic)
- [Пример использования](#пример-использования)
- [Историческая справка про баг компилятора](#историческая-справка-про-баг-компилятора)

## Общее

```csharp
using Nantuc.VmApplication.Extensions.StdLibraries;

host.Project.SaveFileInclude("heap.nanasm",   Libraries.heap_nanasm);
host.Project.SaveFileInclude("alloc.minic",   Libraries.alloc_minic);
host.Project.SaveFileInclude("io.nanasm",     Libraries.io_nanasm);
host.Project.SaveFileInclude("print.minic",   Libraries.print_minic);
host.Project.SaveFileInclude("imath.minic",   Libraries.imath_minic);
host.Project.SaveFileInclude("bitops.minic",  Libraries.bitops_minic);
host.Project.SaveFileInclude("ovf.minic",     Libraries.ovf_minic);
```

После этого в исходнике можно писать:

```c
#include "alloc.minic"
#include "print.minic"
#include "imath.minic"
#include "bitops.minic"

int main() {
    char* p = alloc(128);
    print8("hello\n");
    free(p);
    return 0;
}
```

Библиотеки **независимы** по смыслу, но `alloc.minic` и `print.minic` подключают `heap.nanasm`/`io.nanasm` через `#include`. Поэтому при `SaveFileInclude` для `alloc.minic` нужен и `heap.nanasm` (аналогично — `print.minic` требует `io.nanasm`). `imath.minic`, `bitops.minic` и `ovf.minic` не требуют ничего дополнительно.

## heap.nanasm

Низкоуровневые ассемблерные обёртки над инструкциями процессора. Все функции — `func_*`, чтобы не конфликтовать с пользовательскими именами.

| Функция | Сигнатура (для `extern`) | Что делает |
|---|---|---|
| `func_HeapGet` | `ulong func_HeapGet()` | `r0 = rHP` |
| `func_HeapSet` | `void func_HeapSet(ulong)` | `rHP = r0` |
| `func_GetSP` | `ulong func_GetSP()` | `r0 = rSP` |
| `func_Load64` | `ulong func_Load64(ulong addr)` | `r0 = LOAD_IND.64 r0` |
| `func_Store64` | `void func_Store64(ulong addr, ulong value)` | `STORE_IND.64 r1, r0` |
| `func_Load32` / `func_Store32` | аналогично | `.32` |
| `func_Load16` / `func_Store16` | аналогично | `.16` |
| `func_Load8` / `func_Store8` | аналогично | `.8` |

Регистровая конвенция: аргументы — `r0`, `r1`; возврат — `r0`. `RET` в каждой функции. `rHP` — heap pointer, `rSP` — stack pointer (регистры процессора, не трогаются обёртками явно, но `HeapGet`/`HeapSet` работают с ними).

## alloc.minic

First-fit аллокатор с split и coalescing. Публичное API — две функции:

| Функция | Сигнатура | Что делает |
|---|---|---|
| `alloc` | `ulong alloc(ulong size)` | Выделяет блок ≥ `size` байт, возвращает payload (или `0`, если bump вышел за `rSP`) |
| `free` | `void free(ulong ptr)` | Освобождает блок, помечает free, сливает с соседями и возвращает в free-list |

### Раскладка блока

```
+0..7    : size | FREE_BIT(бит0)        — заголовок
+8..15   : next_free (только для free)  — либо начало payload
+16..sz-8: продолжение payload / unused
+sz-8..sz: size | FREE_BIT(бит0)        — футер (для слияния влево)
```

- `size` всегда кратен 8.
- `payload` для пользователя начинается с `+8`.
- `MIN_BLOCK = 24` байта (8 hdr + 8 payload + 8 ftr) — минимальный блок, который может существовать в куче. `split` создаёт остаток только если `csz - need >= MIN_BLOCK`.
- `FREE_BIT` (бит 0) — маркер свободного блока. `size` в заголовке/футере — всегда `size & ~FREE_BIT`.
- `HDR_FTR = 16` — суммарный overhead заголовка и футера, прибавляется к выровненному payload'у в `alloc`.

### Состояние

```c
ulong heapFreeList = 0;   // голова free-list (0 — пусто)
ulong heapCur;            // вершина bump'а (== rHP), 0 — не инициализировано
ulong heapStart;          // первый блок (для остановки coalesce влево)

const ulong FREE_BIT  = 1;
const ulong MIN_BLOCK = 24;
ulong HDR_FTR = 16;       // 8 hdr + 8 ftr
```

> Состояние сейчас в **глобалах**. Это легаси от бага `FunctionGenerator.GenerateFunction` (см. [Историческая справка](#историческая-справка-про-баг-компилятора)); баг исправлен, миграция в локалы — задача следующей итерации.

### Поток `alloc(size)`

1. `size = max(size, 1)`.
2. `need = ((size + 7) & ~7) + HDR_FTR` (hdr + payload + ftr, выровнено на 8), но не меньше `MIN_BLOCK`.
3. `heapInit()` — если `heapCur == 0`, `heapCur = align8(rHP or 0x100000)`, `heapStart = heapCur`.
4. **First-fit по `heapFreeList`.** Если `csz >= need`:
   - вырезать `curr` из free-list;
   - если `csz - need >= MIN_BLOCK` — `blkMark(rest, csz - need, 1)` + `blkPush(rest)` + `blkMark(curr, need, 0)`; иначе — `blkMark(curr, csz, 0)`;
   - вернуть `curr + 8`.
5. **Bump.** Если `heapCur + need > rSP` — вернуть `0` (OOM). Иначе `blkMark(heapCur, need, 0)`, `heapCur += need`, `HeapSet(heapCur)`, вернуть `old_heapCur + 8`.

### Поток `free(ptr)`

1. `if (ptr == 0) return;`
2. `b = ptr - 8` (заголовок).
3. `blkMark(b, blkSize(b), 1)` — пометить free.
4. `b = mergeRight(b)` — если правый сосед free, слить.
5. `b = mergeLeft(b)` — если левый сосед free, слить.
6. `blkPush(b)` — в голову free-list.

`mergeRight` останавливается на `heapCur` (дальше bump не продвинулся). `mergeLeft` останавливается на `heapStart` (левого соседа нет).

> ⚠️ В текущей строковой константе `alloc_minic` в начале `free` стоит строка `if (blkFree(b)) return;` **до** объявления `b` — это баг (обращение к необъявленной переменной), который нужно убрать; выше описано целевое поведение.

## io.nanasm

| Функция | Сигнатура (для `extern`) | Что делает |
|---|---|---|
| `func_In` | `byte func_In(int port)` | `IN r1, r0; MOV r0, r1; RET` |
| `func_Out` | `void func_Out(byte value, int port)` | `OUT r0, r1; RET` |

Регистровая конвенция: `func_In` — `r0 = port`, возврат в `r0`; `func_Out` — `r0 = value`, `r1 = port`.

## print.minic

Функции вывода через `io.nanasm`. Порты — `portWrite = 0x10`, `portFlush = 0x12` (по умолчанию). Можно переопределить, присвоив `portWrite`/`portFlush` **до** вызова `print*`/`write*`.

| Функция | Сигнатура | Что делает |
|---|---|---|
| `print8` | `void print8(char* str)` | Печатает ASCII-строку (1 байт на символ), затем `flush()` |
| `print16` | `void print16(char* str)` | Печатает UTF-16-строку (2 байта на символ), затем `flush()` |
| `write8` | `void write8(byte c)` | Один байт через `Out(c, portWrite)` |
| `write16` | `void write16(char c)` | Два байта: `write8(c)`, затем `write8(c >> 8)` |
| `flush` | `void flush()` | `Out(1, portFlush)` |

Пример:

```c
#include "print.minic"

int main() {
    print8("hello, world\n");
    return 0;
}
```

На хосте достаточно подключить `SynchronousIOStream` (или `QueuedIOStream`) на порт `0x10`/`0x12`:

```csharp
var uart = new SynchronousIOStream(Console.Out, PortCharEncoding.BytePerChar);
host.Emulator.RegisterDevice(uart, name: "UART0", sector: 1);
// portWrite = GetBaseAddress(sector) + OutputPortLayout.Data
// portFlush = GetBaseAddress(sector) + OutputPortLayout.Flush
```

## imath.minic

Целочисленная арифметика: min/max/clamp, модуль, НОД, степень, корень, деление с округлением. Без `#include`, независима.

| Функция | Сигнатура | Что делает |
|---|---|---|
| `imin` / `imax` | `int imin(int a, int b)` / `int imax(int a, int b)` | Минимум / максимум двух `int` |
| `umin` / `umax` | `ulong umin(ulong a, ulong b)` / `ulong umax(ulong a, ulong b)` | Минимум / максимум двух `ulong` |
| `iclamp` | `int iclamp(int x, int lo, int hi)` | `x`, зажатый в `[lo, hi]` |
| `uclamp` | `ulong uclamp(ulong x, ulong lo, ulong hi)` | `x`, зажатый в `[lo, hi]` |
| `iabs` | `int iabs(int x)` | Модуль `int` |
| `igcd` | `int igcd(int a, int b)` | НОД (алгоритм Евклида); знаки нормализуются |
| `ugcd` | `ulong ugcd(ulong a, ulong b)` | НОД для `ulong` |
| `ipow` | `int ipow(int base, int exp)` | `base^exp` для `exp >= 0` (бинарное возведение) |
| `uisqrt` | `ulong uisqrt(ulong n)` | Целочисленный `sqrt(n)` (метод Ньютона) |
| `idiv_floor` | `int idiv_floor(int a, int b)` | Деление с округлением вниз (с учётом C99-семантики `/`, `%`) |
| `idiv_ceil` | `int idiv_ceil(int a, int b)` | Деление с округлением вверх |
| `udiv_round_up` | `ulong udiv_round_up(ulong a, ulong b)` | Беззнаковое деление с округлением вверх (часто нужно аллокаторам) |

## bitops.minic

Битовые операции над 64-битными словами. Без `#include`, независима.

| Функция | Сигнатура | Что делает |
|---|---|---|
| `upopcount` | `int upopcount(ulong x)` | Число установленных бит (Kernighan) |
| `uclz` | `int uclz(ulong x)` | Число ведущих нулей, `0..64` (`64` для `x == 0`) |
| `uctz` | `int uctz(ulong x)` | Число хвостовых нулей, `0..64` (`64` для `x == 0`) |
| `rotl64` | `ulong rotl64(ulong x, int n)` | Циклический сдвиг влево (`n & 63`; `n == 0` → без изменений) |
| `rotr64` | `ulong rotr64(ulong x, int n)` | Циклический сдвиг вправо (`n & 63`; `n == 0` → без изменений) |
| `urev64` | `ulong urev64(ulong x)` | Зеркальное отражение бит |
| `unext_pow2` | `ulong unext_pow2(ulong x)` | Ближайшая степень двойки `>= x`; `0` — переполнение (для `x > 2^63`) |

## ovf.minic

Проверки целочисленного переполнения. Без `#include`, независима. Возвращают `1` при переполнении и `0` — иначе.

| Функция | Сигнатура | Что делает |
|---|---|---|
| `add_ovf_i32` | `int add_ovf_i32(int a, int b, int* out)` | `a + b`; при переполнении `*out` не трогается |
| `sub_ovf_i32` | `int sub_ovf_i32(int a, int b, int* out)` | `a - b`; при переполнении `*out` не трогается |
| `mul_ovf_i32` | `int mul_ovf_i32(int a, int b, int* out)` | `a * b`; ловит `INT_MIN * -1` до умножения; при переполнении `*out` не трогается |
| `add_ovf_u64` | `int add_ovf_u64(ulong a, ulong b, ulong* out)` | `a + b`; пишет усечённый результат в `*out` в любом случае, `1` — при переносе |
| `sub_ovf_u64` | `int sub_ovf_u64(ulong a, ulong b, ulong* out)` | `a - b`; пишет усечённый результат в `*out` в любом случае, `1` — при заёме |
| `mul_ovf_u64` | `int mul_ovf_u64(ulong a, ulong b, ulong* out)` | `a * b`; при переполнении `*out` не трогается |

> `INT_MAX` = `2147483647`, `INT_MIN` = `-2147483647 - 1`.

## Пример использования

```csharp
using Nantuc.VmApplication;
using Nantuc.VmApplication.Extensions.Emulator.IO;
using Nantuc.VmApplication.Extensions.Host;
using Nantuc.VmApplication.Extensions.StdLibraries;
using Nantuc.VmApplication.Project;
using Nantuc.Kernel.Common;

using var host = VmHostFactory.CreateDefault();

// 1. Сохраняем библиотеки в include-директорию проекта.
host.Project.SaveFileInclude("heap.nanasm",  Libraries.heap_nanasm);
host.Project.SaveFileInclude("io.nanasm",    Libraries.io_nanasm);
host.Project.SaveFileInclude("alloc.minic",  Libraries.alloc_minic);
host.Project.SaveFileInclude("print.minic",  Libraries.print_minic);
host.Project.SaveFileInclude("imath.minic",  Libraries.imath_minic);

// 2. UART на секторе 1.
var uart = new SynchronousIOStream(Console.Out, PortCharEncoding.BytePerChar);
host.Emulator.RegisterDevice(uart, name: "UART0", sector: 1);

// 3. Программа.
const string source = """
#include "alloc.minic"
#include "print.minic"
#include "imath.minic"

char str[] = "hello from MiniC\n";
char n = "\n";

int main() {
    print8(str);
    char* p = alloc(64);
    p[0] = 'O';
    p[1] = 'K';
    p[2] = '\0';
    print8(p);
    print8(n);
    free(p);
    return 0;
}
""";

var outcome = host.RunAndKeep(source, SourceLanguage.C, sector: 1);
```

---
