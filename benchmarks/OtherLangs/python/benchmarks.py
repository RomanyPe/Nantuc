# benchmarks.py
import sys

def bench_call():
    def sum_(a, b): return a + b
    r0, r1 = 0, 3_000_000
    while r1 != 0:
        r0 = sum_(r0, r1)
        r1 -= 1
    return r0

def bench_math():
    r0, r1 = 0, 3_000_000
    while r1 != 0:
        r0 += r1
        r1 -= 1
    return r0

def bench_alu_tput1():
    r0, r7, r6 = 0, 1, 800_000
    while r6 != 0:
        r0 += r7; r0 += r7; r0 += r7; r0 += r7
        r6 -= 1
    return r0

def bench_alu_tput4():
    r0 = r1 = r2 = r3 = 0
    r7, r6 = 1, 800_000
    while r6 != 0:
        r0 += r7; r1 += r7; r2 += r7; r3 += r7
        r6 -= 1
    return r0 + r1 + r2 + r3

def bench_alu_tput_mixed():
    r0 = r1 = r2 = r3 = 0
    r7, r6 = 1, 800_000
    while r6 != 0:
        r0 += r7
        r1 -= r7
        r2 &= r7
        r3 |= r7
        r6 -= 1
    return r0 + r1 + r2 + r3

def bench_memory():
    arr = [0] * 25_000
    total = 0
    for _ in range(20):
        for i in range(25_000):
            arr[i] = i
        total += sum(arr)
    return total

def bench_bit_ops():
    r0 = r1 = 1
    r2 = r3 = r4 = 0
    r6 = 3_000_000
    while r6 != 0:
        r0 >>= r1
        r2 &= r0
        r3 |= r0
        r4 ^= r0
        r6 -= 1
    return r0 + r2 + r3 + r4

def bench_mul():
    r0, r1, r6 = 1, 3, 3_000_000
    while r6 != 0:
        r0 *= r1
        r6 -= 1
    return r0

def bench_branch():
    r5, r6, r2, r3 = 500_000, 0, 0, 0
    while r5 != 0:
        r6 += 1
        if r6 & 1:
            r2 += 1
        else:
            r3 += 1
        r5 -= 1
    return r2 + r3

def bench_div_mod():
    r0, r1, r2, r6 = 1_000_000, 7, 0, 500_000
    while r6 != 0:
        r2 += r0 // r1
        r2 += r0 % r1
        r0 += 1
        r6 -= 1
    return r2

def bench_recursion():
    def down(n):
        if n <= 0: return 0
        return down(n - 1)
    total = 0
    for _ in range(500):
        total += down(500)
    return total

def bench_pointer():
    # В Python указателей нет; эмулируем через список и индексы.
    arr = [0] * 1000
    n = 1000
    for i in range(n):
        arr[i] = i
    s = 0
    for _ in range(2000):
        for i in range(n):
            s += arr[i]
    return s

def bench_struct():
    # Эмуляция структуры: список кортежей (x, y).
    pts = [(0, 0)] * 1000
    total = 0
    for _ in range(200):
        for i in range(1000):
            pts[i] = (i, i + 1)
        s = 0
        for x, y in pts:
            s += x + y
        total += s
    return total

def bench_sort():
    arr = [0] * 200
    total = 0
    for rep in range(50, 0, -1):
        for i in range(200):
            arr[i] = (i * 37 + rep * 13) % 1000
        # Пузырьковая сортировка (как в MiniC)
        for pass_ in range(199):
            for j in range(199 - pass_):
                if arr[j] > arr[j + 1]:
                    arr[j], arr[j + 1] = arr[j + 1], arr[j]
        total += arr[0] + arr[199]
    return total

BENCHES = [
    ("Call", bench_call), ("Math", bench_math),
    ("AluTput1", bench_alu_tput1), ("AluTput4", bench_alu_tput4),
    ("AluTputMixed", bench_alu_tput_mixed), ("Memory", bench_memory),
    ("BitOps", bench_bit_ops), ("Mul", bench_mul),
    ("Branch", bench_branch), ("DivMod", bench_div_mod),
    ("Recursion", bench_recursion), ("Pointer", bench_pointer),
    ("Struct", bench_struct), ("Sort", bench_sort),
]

if __name__ == "__main__":
    import time
    for name, fn in BENCHES:
        t0 = time.perf_counter()
        res = fn()
        dt = time.perf_counter() - t0
        print(f"{name:12s}  {dt:8.4f} s  (result={res})", file=sys.stderr)