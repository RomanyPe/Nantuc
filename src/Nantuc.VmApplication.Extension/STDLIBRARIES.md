# Built-in libraries (`Libraries`)

> Reference for the string constants `Nantuc.VmApplication.Extensions.StdLibraries.Libraries`. These strings are the contents of `heap.nanasm`, `alloc.minic`, `io.nanasm`, `print.minic`, `imath.minic`, `bitops.minic`, `ovf.minic` and are intended for `#include` in MiniC/NANASM sources.
>
> `[← Root](../README.md) · [VmApplication.Extensions](README.md) · [Compiler](../../src/Nantuc.Compiler/README.md)`

## Contents

- [General](#general)
- [heap.nanasm](#heapnanasm)
- [alloc.minic](#allocminic)
- [io.nanasm](#ionanasm)
- [print.minic](#printminic)
- [imath.minic](#imathminic)
- [bitops.minic](#bitopsminic)
- [ovf.minic](#ovfminic)
- [Usage example](#usage-example)
- [Historical note about the compiler bug](#historical-note-about-the-compiler-bug)

## General

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

After that, you can write the following in the source:

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

The libraries are **independent** in meaning, but `alloc.minic` and `print.minic` include `heap.nanasm`/`io.nanasm` via `#include`. Therefore, when using `SaveFileInclude` for `alloc.minic`, you also need `heap.nanasm` (similarly, `print.minic` requires `io.nanasm`). `imath.minic`, `bitops.minic`, and `ovf.minic` require nothing additional.

## heap.nanasm

Low-level assembler wrappers over processor instructions. All functions are `func_*` to avoid conflicts with user names.

| Function | Signature (for `extern`) | What it does |
|---|---|---|
| `func_HeapGet` | `ulong func_HeapGet()` | `r0 = rHP` |
| `func_HeapSet` | `void func_HeapSet(ulong)` | `rHP = r0` |
| `func_GetSP` | `ulong func_GetSP()` | `r0 = rSP` |
| `func_Load64` | `ulong func_Load64(ulong addr)` | `r0 = LOAD_IND.64 r0` |
| `func_Store64` | `void func_Store64(ulong addr, ulong value)` | `STORE_IND.64 r1, r0` |
| `func_Load32` / `func_Store32` | similarly | `.32` |
| `func_Load16` / `func_Store16` | similarly | `.16` |
| `func_Load8` / `func_Store8` | similarly | `.8` |

Register convention: arguments are `r0`, `r1`; return value is `r0`. `RET` in each function. `rHP` is the heap pointer, `rSP` is the stack pointer (processor registers; the wrappers do not explicitly touch them, but `HeapGet`/`HeapSet` work with them).

## alloc.minic

First-fit allocator with split and coalescing. Public API — two functions:

| Function | Signature | What it does |
|---|---|---|
| `alloc` | `ulong alloc(ulong size)` | Allocates a block ≥ `size` bytes, returns the payload (or `0` if the bump went past `rSP`) |
| `free` | `void free(ulong ptr)` | Frees a block, marks it free, merges it with neighbors, and returns it to the free-list |

### Block layout

```
+0..7    : size | FREE_BIT(bit0)        — header
+8..15   : next_free (only for free)   — or the start of the payload
+16..sz-8: payload continuation / unused
+sz-8..sz: size | FREE_BIT(bit0)       — footer (for merging left)
```

- `size` is always a multiple of 8.
- The user `payload` starts at `+8`.
- `MIN_BLOCK = 24` bytes (8 hdr + 8 payload + 8 ftr) — the minimum block that can exist in the heap. `split` creates a remainder only if `csz - need >= MIN_BLOCK`.
- `FREE_BIT` (bit 0) is the free-block marker. `size` in the header/footer is always `size & ~FREE_BIT`.
- `HDR_FTR = 16` — total overhead of the header and footer, added to the aligned payload in `alloc`.

### State

```c
ulong heapFreeList = 0;   // head of the free-list (0 — empty)
ulong heapCur;            // top of the bump (== rHP), 0 — not initialized
ulong heapStart;          // first block (to stop coalescing left)

const ulong FREE_BIT  = 1;
const ulong MIN_BLOCK = 24;
ulong HDR_FTR = 16;       // 8 hdr + 8 ftr
```

> The state is currently in **globals**. This is legacy from the `FunctionGenerator.GenerateFunction` bug (see [Historical note](#historical-note-about-the-compiler-bug)); the bug is fixed, and migration to locals is a task for the next iteration.

### Flow of `alloc(size)`

1. `size = max(size, 1)`.
2. `need = ((size + 7) & ~7) + HDR_FTR` (hdr + payload + ftr, aligned to 8), but not less than `MIN_BLOCK`.
3. `heapInit()` — if `heapCur == 0`, then `heapCur = align8(rHP or 0x100000)`, `heapStart = heapCur`.
4. **First-fit over `heapFreeList`.** If `csz >= need`:
   - cut `curr` from the free-list;
   - if `csz - need >= MIN_BLOCK` — `blkMark(rest, csz - need, 1)` + `blkPush(rest)` + `blkMark(curr, need, 0)`; otherwise — `blkMark(curr, csz, 0)`;
   - return `curr + 8`.
5. **Bump.** If `heapCur + need > rSP` — return `0` (OOM). Otherwise `blkMark(heapCur, need, 0)`, `heapCur += need`, `HeapSet(heapCur)`, return `old_heapCur + 8`.

### Flow of `free(ptr)`

1. `if (ptr == 0) return;`
2. `b = ptr - 8` (header).
3. `blkMark(b, blkSize(b), 1)` — mark as free.
4. `b = mergeRight(b)` — if the right neighbor is free, merge.
5. `b = mergeLeft(b)` — if the left neighbor is free, merge.
6. `blkPush(b)` — to the head of the free-list.

`mergeRight` stops at `heapCur` (the bump has not advanced further). `mergeLeft` stops at `heapStart` (there is no left neighbor).

> ⚠️ In the current string constant `alloc_minic`, at the beginning of `free` there is the line `if (blkFree(b)) return;` **before** the declaration of `b` — this is a bug (reference to an undeclared variable) that needs to be removed; the target behavior is described above.

## io.nanasm

| Function | Signature (for `extern`) | What it does |
|---|---|---|
| `func_In` | `byte func_In(int port)` | `IN r1, r0; MOV r0, r1; RET` |
| `func_Out` | `void func_Out(byte value, int port)` | `OUT r0, r1; RET` |

Register convention: `func_In` — `r0 = port`, return in `r0`; `func_Out` — `r0 = value`, `r1 = port`.

## print.minic

Output functions via `io.nanasm`. Ports — `portWrite = 0x10`, `portFlush = 0x12` (by default). They can be overridden by assigning `portWrite`/`portFlush` **before** calling `print*`/`write*`.

| Function | Signature | What it does |
|---|---|---|
| `print8` | `void print8(char* str)` | Prints an ASCII string (1 byte per character), then `flush()` |
| `print16` | `void print16(char* str)` | Prints a UTF-16 string (2 bytes per character), then `flush()` |
| `write8` | `void write8(byte c)` | One byte via `Out(c, portWrite)` |
| `write16` | `void write16(char c)` | Two bytes: `write8(c)`, then `write8(c >> 8)` |
| `flush` | `void flush()` | `Out(1, portFlush)` |

Example:

```c
#include "print.minic"

int main() {
    print8("hello, world\n");
    return 0;
}
```

On the host, it is enough to connect `SynchronousIOStream` (or `QueuedIOStream`) to port `0x10`/`0x12`:

```csharp
var uart = new SynchronousIOStream(Console.Out, PortCharEncoding.BytePerChar);
host.Emulator.RegisterDevice(uart, name: "UART0", sector: 1);
// portWrite = GetBaseAddress(sector) + OutputPortLayout.Data
// portFlush = GetBaseAddress(sector) + OutputPortLayout.Flush
```

## imath.minic

Integer arithmetic: min/max/clamp, absolute value, GCD, power, root, division with rounding. No `#include`, independent.

| Function | Signature | What it does |
|---|---|---|
| `imin` / `imax` | `int imin(int a, int b)` / `int imax(int a, int b)` | Minimum / maximum of two `int`s |
| `umin` / `umax` | `ulong umin(ulong a, ulong b)` / `ulong umax(ulong a, ulong b)` | Minimum / maximum of two `ulong`s |
| `iclamp` | `int iclamp(int x, int lo, int hi)` | `x` clamped to `[lo, hi]` |
| `uclamp` | `ulong uclamp(ulong x, ulong lo, ulong hi)` | `x` clamped to `[lo, hi]` |
| `iabs` | `int iabs(int x)` | Absolute value of `int` |
| `igcd` | `int igcd(int a, int b)` | GCD (Euclidean algorithm); signs are normalized |
| `ugcd` | `ulong ugcd(ulong a, ulong b)` | GCD for `ulong` |
| `ipow` | `int ipow(int base, int exp)` | `base^exp` for `exp >= 0` (binary exponentiation) |
| `uisqrt` | `ulong uisqrt(ulong n)` | Integer `sqrt(n)` (Newton's method) |
| `idiv_floor` | `int idiv_floor(int a, int b)` | Division with rounding down (taking into account C99 semantics of `/`, `%`) |
| `idiv_ceil` | `int idiv_ceil(int a, int b)` | Division with rounding up |
| `udiv_round_up` | `ulong udiv_round_up(ulong a, ulong b)` | Unsigned division with rounding up (often needed by allocators) |

## bitops.minic

Bit operations on 64-bit words. No `#include`, independent.

| Function | Signature | What it does |
|---|---|---|
| `upopcount` | `int upopcount(ulong x)` | Number of set bits (Kernighan) |
| `uclz` | `int uclz(ulong x)` | Number of leading zeros, `0..64` (`64` for `x == 0`) |
| `uctz` | `int uctz(ulong x)` | Number of trailing zeros, `0..64` (`64` for `x == 0`) |
| `rotl64` | `ulong rotl64(ulong x, int n)` | Circular left shift (`n & 63`; `n == 0` → unchanged) |
| `rotr64` | `ulong rotr64(ulong x, int n)` | Circular right shift (`n & 63`; `n == 0` → unchanged) |
| `urev64` | `ulong urev64(ulong x)` | Bit reversal |
| `unext_pow2` | `ulong unext_pow2(ulong x)` | Nearest power of two `>= x`; `0` — overflow (for `x > 2^63`) |

## ovf.minic

Integer overflow checks. No `#include`, independent. Return `1` on overflow and `0` otherwise.

| Function | Signature | What it does |
|---|---|---|
| `add_ovf_i32` | `int add_ovf_i32(int a, int b, int* out)` | `a + b`; on overflow `*out` is not touched |
| `sub_ovf_i32` | `int sub_ovf_i32(int a, int b, int* out)` | `a - b`; on overflow `*out` is not touched |
| `mul_ovf_i32` | `int mul_ovf_i32(int a, int b, int* out)` | `a * b`; catches `INT_MIN * -1` before multiplication; on overflow `*out` is not touched |
| `add_ovf_u64` | `int add_ovf_u64(ulong a, ulong b, ulong* out)` | `a + b`; writes the truncated result to `*out` in any case, `1` — on carry |
| `sub_ovf_u64` | `int sub_ovf_u64(ulong a, ulong b, ulong* out)` | `a - b`; writes the truncated result to `*out` in any case, `1` — on borrow |
| `mul_ovf_u64` | `int mul_ovf_u64(ulong a, ulong b, ulong* out)` | `a * b`; on overflow `*out` is not touched |

> `INT_MAX` = `2147483647`, `INT_MIN` = `-2147483647 - 1`.

## Usage example

```csharp
using Nantuc.VmApplication;
using Nantuc.VmApplication.Extensions.Emulator.IO;
using Nantuc.VmApplication.Extensions.Host;
using Nantuc.VmApplication.Extensions.StdLibraries;
using Nantuc.VmApplication.Project;
using Nantuc.Kernel.Common;

using var host = VmHostFactory.CreateDefault();

// 1. Save the libraries to the project include directory.
host.Project.SaveFileInclude("heap.nanasm",  Libraries.heap_nanasm);
host.Project.SaveFileInclude("io.nanasm",    Libraries.io_nanasm);
host.Project.SaveFileInclude("alloc.minic",  Libraries.alloc_minic);
host.Project.SaveFileInclude("print.minic",  Libraries.print_minic);
host.Project.SaveFileInclude("imath.minic",  Libraries.imath_minic);

// 2. UART on sector 1.
var uart = new SynchronousIOStream(Console.Out, PortCharEncoding.BytePerChar);
host.Emulator.RegisterDevice(uart, name: "UART0", sector: 1);

// 3. Program.
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