// benchmarks.cpp
// g++ -O2 -fwrapv benchmarks.cpp -o benchmarks
#include <cstdio>
#include <chrono>

using Clock = std::chrono::steady_clock;

[[gnu::noinline]] static int sum_fn(int a, int b) { return a + b; }

static long long bench_call() {
    int r0 = 0, r1 = 3'000'000;
    while (r1 != 0) { r0 = sum_fn(r0, r1); --r1; }
    return r0;
}
static long long bench_math() {
    int r0 = 0, r1 = 3'000'000;
    while (r1 != 0) { r0 += r1; --r1; }
    return r0;
}
static long long bench_alu_tput1() {
    int r0 = 0, r7 = 1, r6 = 800'000;
    while (r6 != 0) { r0 += r7; r0 += r7; r0 += r7; r0 += r7; --r6; }
    return r0;
}
static long long bench_alu_tput4() {
    int r0 = 0, r1 = 0, r2 = 0, r3 = 0, r7 = 1, r6 = 800'000;
    while (r6 != 0) { r0 += r7; r1 += r7; r2 += r7; r3 += r7; --r6; }
    return (long long)r0 + r1 + r2 + r3;
}
static long long bench_alu_tput_mixed() {
    int r0 = 0, r1 = 0, r2 = 0, r3 = 0, r7 = 1, r6 = 800'000;
    while (r6 != 0) { r0 += r7; r1 -= r7; r2 &= r7; r3 |= r7; --r6; }
    return (long long)r0 + r1 + r2 + r3;
}
static long long bench_memory() {
    static int arr[25'000];
    long long total = 0;
    for (int outer = 0; outer < 20; ++outer) {
        for (int i = 0; i < 25'000; ++i) arr[i] = i;
        long long s = 0;
        for (int i = 0; i < 25'000; ++i) s += arr[i];
        total += s;
    }
    return total;
}
static long long bench_bit_ops() {
    int r0 = 1, r1 = 1, r2 = 0, r3 = 0, r4 = 0, r6 = 3'000'000;
    while (r6 != 0) { r0 >>= r1; r2 &= r0; r3 |= r0; r4 ^= r0; --r6; }
    return (long long)r0 + r2 + r3 + r4;
}
static long long bench_mul() {
    int r0 = 1, r1 = 3, r6 = 3'000'000;
    while (r6 != 0) { r0 *= r1; --r6; }
    return r0;
}
static long long bench_branch() {
    int r5 = 500'000, r6 = 0;
    long long r2 = 0, r3 = 0;
    while (r5 != 0) { ++r6; if (r6 & 1) ++r2; else ++r3; --r5; }
    return r2 + r3;
}
static long long bench_div_mod() {
    int r0 = 1'000'000, r1 = 7, r6 = 500'000;
    long long r2 = 0;
    while (r6 != 0) { r2 += r0 / r1; r2 += r0 % r1; ++r0; --r6; }
    return r2;
}
static int down(int n) { return n <= 0 ? 0 : down(n - 1); }
static long long bench_recursion() {
    long long total = 0;
    for (int o = 0; o < 500; ++o) total += down(500);
    return total;
}
static long long bench_pointer() {
    int arr[1000];
    int* p = arr;
    for (int i = 0; i < 1000; ++i) *(p + i) = i;
    long long s = 0;
    for (int rep = 0; rep < 2000; ++rep)
        for (int i = 0; i < 1000; ++i) s += *(p + i);
    return s;
}
struct Point { int x, y; };
static Point pts[1000];
static long long bench_struct() {
    long long total = 0;
    for (int rep = 0; rep < 200; ++rep) {
        for (int i = 0; i < 1000; ++i) { pts[i].x = i; pts[i].y = i + 1; }
        long long s = 0;
        for (int i = 0; i < 1000; ++i) s += pts[i].x + pts[i].y;
        total += s;
    }
    return total;
}
static long long bench_sort() {
    int arr[200];
    long long total = 0;
    for (int rep = 50; rep > 0; --rep) {
        for (int i = 0; i < 200; ++i) arr[i] = (i * 37 + rep * 13) % 1000;
        for (int pass = 0; pass < 199; ++pass)
            for (int j = 0; j < 199 - pass; ++j)
                if (arr[j] > arr[j + 1]) std::swap(arr[j], arr[j + 1]);
        total += arr[0] + arr[199];
    }
    return total;
}

struct Bench { const char* name; long long (*fn)(); };

int main() {
    Bench benches[] = {
        {"Call", bench_call}, {"Math", bench_math},
        {"AluTput1", bench_alu_tput1}, {"AluTput4", bench_alu_tput4},
        {"AluTputMixed", bench_alu_tput_mixed}, {"Memory", bench_memory},
        {"BitOps", bench_bit_ops}, {"Mul", bench_mul},
        {"Branch", bench_branch}, {"DivMod", bench_div_mod},
        {"Recursion", bench_recursion}, {"Pointer", bench_pointer},
        {"Struct", bench_struct}, {"Sort", bench_sort},
    };
    for (auto& b : benches) {
        b.fn();                                       // warmup
        auto t0 = Clock::now();
        long long res = b.fn();
        auto dt = std::chrono::duration<double>(Clock::now() - t0).count();
        std::fprintf(stderr, "%-12s  %8.4f s  (result=%lld)\n", b.name, dt, res);
    }
}