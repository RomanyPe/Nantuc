# Built-in Libraries (`LibraresForCompiler`)

> Reference for the string constants in `Nantuc.VmApplication.Extensions.LibraresIncludes.LibraresForCompiler`. These strings are the contents of `heap.nanasm`, `alloc.minic`, `io.nanasm`, `print.minic` — intended for `#include` in MiniC/NANASM sources.
>
> `[← Root](../README.md) · [VmApplication.Extensions](README.md) · [Compiler](../../src/Nantuc.Compiler/README.md)`

## Table of Contents

- [General](#general)
- [heap.nanasm](#heapnanasm)
- [alloc.minic](#allocminic)
- [io.nanasm](#ionanasm)
- [print.minic](#printminic)
- [Usage Example](#usage-example)
- [Historical Note on the Compiler Bug](#historical-note-on-the-compiler-bug)

## General

```csharp
using Nantuc.VmApplication.Extensions.LibraresIncludes;

host.Project.SaveFileInclude("heap.nanasm",  LibraresForCompiler.heap_nanasm);
host.Project.SaveFileInclude("alloc.minic",   LibraresForCompiler.alloc_minic);
host.Project.SaveFileInclude("io.nanasm",     LibraresForCompiler.io_nanasm);
host.Project.SaveFileInclude("print.minic",   LibraresForCompiler.print_minic);
```

After that, you can write the following in your source:

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

All four libraries are **independent** in meaning, but `alloc.minic` and `print.minic` include `heap.nanasm`/`io.nanasm` via `#include`. Therefore, when calling `SaveFileInclude` for `alloc.minic`, you also need `heap.nanasm` (similarly, `print.minic` requires `io.nanasm`).

## heap.nanasm

Low-level assembly wrappers around processor instructions. All functions are `func_*` to avoid conflicts with user-defined names.

| Function | Signature (for `extern`) | What it does |
|---|---|---|
| `func_HeapGet` | `ulong func_HeapGet()` | `r0 = rHP` |
| `func_HeapSet` | `void func_HeapSet(ulong)` | `rHP = r0` |
| `func_GetSP` | `ulong func_GetSP()` | `r0 = rSP` |
| `func_Load64` | `ulong func_Load64(ulong addr)` | `r0 = LOAD_IND.64 r0` |
| `func_Store64` | `void func_Store64(ulong addr, ulong value)` | `STORE_IND.64 r1, r0` |
| `func_Load32` / `func_Store32` | similar | `.32` |
| `func_Load16` / `func_Store16` | similar | `.16` |
| `func_Load8` / `func_Store8` | similar | `.8` |

Register convention: arguments are `r0`, `r1`; return value is in `r0`. `RET` in each function. `rHP` — heap pointer, `rSP` — stack pointer (processor registers; the wrappers do not explicitly touch them, but `HeapGet`/`HeapSet` operate on them).

## alloc.minic

First-fit allocator with split and coalescing. Public API — two functions:

| Function | Signature | What it does |
|---|---|---|
| `alloc` | `ulong alloc(ulong size)` | Allocates a block ≥ `size` bytes, returns the payload (or `0` if the bump goes past `rSP`) |
| `free` | `void free(ulong ptr)` | Frees the block, marks it free, merges with neighbors, and returns it to the free list |

### Block Layout

```
+0..7    : size | FREE_BIT(bit0)        — header
+8..15   : next_free (only for free)    — or start of payload
+16..sz-8: continuation of payload / unused
+sz-8..sz: size | FREE_BIT(bit0)        — footer (for left coalescing)
```

- `size` is always a multiple of 8.
- The user `payload` starts at `+8`.
- `MIN_BLOCK = 24` bytes (8 hdr + 8 payload + 8 ftr) — the minimum block that can exist in the heap. `split` creates a remainder only if `csz - need >= MIN_BLOCK`.
- `FREE_BIT` (bit 0) — free block marker. `size` in the header/footer is always `size & ~FREE_BIT`.

### State

```c
ulong heapFreeList;   // head of free list (0 — empty)
ulong heapCur;        // bump top (== rHP), 0 — not initialized
ulong heapStart;      // first block (to stop left coalescing)
ulong FREE_BIT  = 1;
ulong MIN_BLOCK = 24;
```

> The state is currently in **globals**. This is legacy from the `FunctionGenerator.GenerateFunction` bug (see [Historical Note](#historical-note-on-the-compiler-bug)); the bug is fixed, migration to locals is a task for the next iteration.

### `alloc(size)` Flow

1. `size = max(size, 1)`.
2. `need = ((size + 7) / 8) * 8 + 16` (hdr + payload + ftr, aligned to 8), but not less than `MIN_BLOCK`.
3. `heapInit()` — if `heapCur == 0`, `heapCur = align8(rHP or 0x100000)`, `heapStart = heapCur`.
4. **First-fit over `heapFreeList`.** If `csz >= need`:
   - remove `curr` from the free list;
   - if `csz - need >= MIN_BLOCK` — `blkMark(rest, csz - need, 1)` + `blkPush(rest)` + `blkMark(curr, need, 0)`; otherwise — `blkMark(curr, csz, 0)`;
   - return `curr + 8`.
5. **Bump.** If `heapCur + need > rSP` — return `0` (OOM). Otherwise `blkMark(heapCur, need, 0)`, `heapCur += need`, `HeapSet(heapCur)`, return `old_heapCur + 8`.

### `free(ptr)` Flow

1. `if (ptr == 0) return;`
2. `b = ptr - 8` (header).
3. `blkMark(b, blkSize(b), 1)` — mark free.
4. `b = mergeRight(b)` — if the right neighbor is free, merge.
5. `b = mergeLeft(b)` — if the left neighbor is free, merge.
6. `blkPush(b)` — push to the head of the free list.

`mergeRight` stops at `heapCur` (the bump has not advanced further). `mergeLeft` stops at `heapStart` (no left neighbor).

## io.nanasm

| Function | Signature (for `extern`) | What it does |
|---|---|---|
| `func_In` | `byte func_In(int port)` | `IN r1, r0; MOV r0, r1; RET` |
| `func_Out` | `void func_Out(byte value, int port)` | `OUT r0, r1; RET` |

Register convention: `func_In` — `r0 = port`, return in `r0`; `func_Out` — `r0 = value`, `r1 = port`.

## print.minic

Output functions via `io.nanasm`. Ports — `portWrite = 0x10`, `portFlush = 0x12` (by default). You can override them by assigning to `portWrite`/`portFlush` **before** calling `print*`/`write*`.

| Function | Signature | What it does |
|---|---|---|
| `print8` | `void print8(char* str)` | Prints an ASCII string (1 byte per character), then `flush()` |
| `print16` | `void print16(char* str)` | Prints a UTF-16 string (2 bytes per character), then `flush()` |
| `write8` | `void write8(byte c)` | One byte via `Out(c, portWrite)` |
| `write16` | `void write16(char c)` | Two bytes: `Out(c & 0xFF)`, `Out((c >> 8) & 0xFF)` |
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

## Usage Example

```csharp
using Nantuc.VmApplication;
using Nantuc.VmApplication.Extensions.Emulator.IO;
using Nantuc.VmApplication.Extensions.Host;
using Nantuc.VmApplication.Extensions.LibraresIncludes;
using Nantuc.VmApplication.Project;
using Nantuc.Kernel.Common;

using var host = VmHostFactory.CreateDefault();

// 1. Save the libraries to the project's include directory.
host.Project.SaveFileInclude("heap.nanasm",  LibraresForCompiler.heap_nanasm);
host.Project.SaveFileInclude("io.nanasm",    LibraresForCompiler.io_nanasm);
host.Project.SaveFileInclude("alloc.minic",  LibraresForCompiler.alloc_minic);
host.Project.SaveFileInclude("print.minic",  LibraresForCompiler.print_minic);

// 2. UART on sector 1.
var uart = new SynchronousIOStream(Console.Out, PortCharEncoding.BytePerChar);
host.Emulator.RegisterDevice(uart, name: "UART0", sector: 1);

// 3. Program.
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

## Historical Note on the Compiler Bug

Previously, `alloc.minic` kept all its state in globals because of a bug in `FunctionGenerator.GenerateFunction` (asymmetric push/pop of callee-saved registers with 3-4 locals), which corrupted the allocator's behavior. The bug has been fixed. Therefore, the workaround "keep state in globals" is no longer required — migration to locals is a task for the next iteration. The current code still uses globals as legacy.