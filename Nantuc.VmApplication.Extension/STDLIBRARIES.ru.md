# Встроенные библиотеки (`LibraresForCompiler`)

> Справочник по строковым константам `Nantuc.VmApplication.Extensions.LibraresIncludes.LibraresForCompiler`. Эти строки — содержимое `heap.nanasm`, `alloc.minic`, `io.nanasm`, `print.minic` — предназначены для `#include` в исходниках MiniC/NANASM.
>
> `[← Корень](../README.ru.md) · [VmApplication.Extensions](README.ru.md) · [Compiler](../Nantuc.Compiler/README.ru.md)`

## Содержание

- [Общее](#общее)
- [heap.nanasm](#heapnanasm)
- [alloc.minic](#allocminic)
- [io.nanasm](#ionanasm)
- [print.minic](#printminic)
- [Пример использования](#пример-использования)
- [Историческая справка про баг компилятора](#историческая-справка-про-баг-компилятора)

## Общее

```csharp
using Nantuc.VmApplication.Extensions.LibraresIncludes;

host.Project.SaveFileInclude("heap.nanasm",  LibraresForCompiler.heap_nanasm);
host.Project.SaveFileInclude("alloc.minic",   LibraresForCompiler.alloc_minic);
host.Project.SaveFileInclude("io.nanasm",     LibraresForCompiler.io_nanasm);
host.Project.SaveFileInclude("print.minic",   LibraresForCompiler.print_minic);
```

После этого в исходнике можно писать:

```c
#include "alloc.minic"
#include "print.minic"

int main() {
    char* p = (char*)alloc(128);
    print8("hello\n");
    free(p);
    return 0;
}
```

Все четыре библиотеки **независимы** по смыслу, но `alloc.minic` и `print.minic` подключают `heap.nanasm`/`io.nanasm` через `#include`. Поэтому при `SaveFileInclude` для `alloc.minic` нужен и `heap.nanasm` (аналогично — `print.minic` требует `io.nanasm`).

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

### Состояние

```c
ulong heapFreeList;   // голова free-list (0 — пусто)
ulong heapCur;        // вершина bump'а (== rHP), 0 — не инициализировано
ulong heapStart;      // первый блок (для остановки coalesce влево)
ulong FREE_BIT  = 1;
ulong MIN_BLOCK = 24;
```

> Состояние сейчас в **глобалах**. Это легаси от бага `FunctionGenerator.GenerateFunction` (см. [Историческая справка](#историческая-справка-про-баг-компилятора)); баг исправлен, миграция в локалы — задача следующей итерации.

### Поток `alloc(size)`

1. `size = max(size, 1)`.
2. `need = ((size + 7) / 8) * 8 + 16` (hdr + payload + ftr, выровнено на 8), но не меньше `MIN_BLOCK`.
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
| `write16` | `void write16(char c)` | Два байта: `Out(c & 0xFF)`, `Out((c >> 8) & 0xFF)` |
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

## Пример использования

```csharp
using Nantuc.VmApplication;
using Nantuc.VmApplication.Extensions.Emulator.IO;
using Nantuc.VmApplication.Extensions.Host;
using Nantuc.VmApplication.Extensions.LibraresIncludes;
using Nantuc.VmApplication.Project;
using Nantuc.Kernel.Common;

using var host = VmHostFactory.CreateDefault();

// 1. Сохраняем библиотеки в include-директорию проекта.
host.Project.SaveFileInclude("heap.nanasm",  LibraresForCompiler.heap_nanasm);
host.Project.SaveFileInclude("io.nanasm",    LibraresForCompiler.io_nanasm);
host.Project.SaveFileInclude("alloc.minic",  LibraresForCompiler.alloc_minic);
host.Project.SaveFileInclude("print.minic",  LibraresForCompiler.print_minic);

// 2. UART на секторе 1.
var uart = new SynchronousIOStream(Console.Out, PortCharEncoding.BytePerChar);
host.Emulator.RegisterDevice(uart, name: "UART0", sector: 1);

// 3. Программа.
const string source = """
#include "alloc.minic"
#include "print.minic"

int main() {
    print8("hello from MiniC\n");
    char* p = (char*)alloc(64);
    p[0] = 'O';
    p[1] = 'K';
    p[2] = '\0';
    print8((char*)p);
    print8("\n");
    free(p);
    return 0;
}
""";

var outcome = host.RunAndKeep(source, SourceLanguage.C, sector: 1);
```