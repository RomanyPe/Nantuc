// benchmarks.c
// gcc -O2 -fwrapv benchmarks.c -o benchmarks
#include <stdio.h>
#include <time.h>

static double now(void) {
    struct timespec ts;
    clock_gettime(CLOCK_MONOTONIC, &ts);
    return (double)ts.tv_sec + (double)ts.tv_nsec * 1e-9;
}

__attribute__((noinline))
static int sum_fn(int a, int b) { return a + b; }

static long long bench_call(void) {
    int r0 = 0, r1 = 3000000;
    while (r1 != 0) { r0 = sum_fn(r0, r1); r1--; }
    return r0;
}

static long long bench_math(void) {
    int r0 = 0, r1 = 3000000;
    while (r1 != 0) { r0 += r1; r1--; }
    return r0;
}

static long long bench_alu_tput1(void) {
    int r0 = 0, r7 = 1, r6 = 800000;
    while (r6 != 0) { r0 += r7; r0 += r7; r0 += r7; r0 += r7; r6--; }
    return r0;
}

static long long bench_alu_tput4(void) {
    int r0 = 0, r1 = 0, r2 = 0, r3 = 0, r7 = 1, r6 = 800000;
    while (r6 != 0) { r0 += r7; r1 += r7; r2 += r7; r3 += r7; r6--; }
    return (long long)r0 + r1 + r2 + r3;
}

static long long bench_alu_tput_mixed(void) {
    int r0 = 0, r1 = 0, r2 = 0, r3 = 0, r7 = 1, r6 = 800000;
    while (r6 != 0) { r0 += r7; r1 -= r7; r2 &= r7; r3 |= r7; r6--; }
    return (long long)r0 + r1 + r2 + r3;
}

#define MEM_N 25000
static int mem_arr[MEM_N];

static long long bench_memory(void) {
    long long total = 0;
    for (int outer = 0; outer < 20; outer++) {
        for (int i = 0; i < MEM_N; i++) mem_arr[i] = i;
        long long sum = 0;
        for (int i = 0; i < MEM_N; i++) sum += mem_arr[i];
        total += sum;
    }
    return total;
}

static long long bench_bit_ops(void) {
    int r0 = 1, r1 = 1, r2 = 0, r3 = 0, r4 = 0, r6 = 3000000;
    while (r6 != 0) { r0 >>= r1; r2 &= r0; r3 |= r0; r4 ^= r0; r6--; }
    return (long long)r0 + r2 + r3 + r4;
}

static long long bench_mul(void) {
    int r0 = 1, r1 = 3, r6 = 3000000;
    while (r6 != 0) { r0 *= r1; r6--; }
    return r0;
}

static long long bench_branch(void) {
    int r5 = 500000, r6 = 0;
    long long r2 = 0, r3 = 0;
    while (r5 != 0) {
        r6++;
        if (r6 & 1) r2++; else r3++;
        r5--;
    }
    return r2 + r3;
}

static long long bench_div_mod(void) {
    int r0 = 1000000, r1 = 7, r6 = 500000;
    long long r2 = 0;
    while (r6 != 0) { r2 += r0 / r1; r2 += r0 % r1; r0++; r6--; }
    return r2;
}

static int down(int n) { return n <= 0 ? 0 : down(n - 1); }

static long long bench_recursion(void) {
    long long total = 0;
    for (int outer = 0; outer < 500; outer++) total += down(500);
    return total;
}

static long long bench_pointer(void) {
    int arr[1000];
    int *p = arr;
    for (int i = 0; i < 1000; i++) *(p + i) = i;
    long long sum = 0;
    for (int rep = 0; rep < 2000; rep++)
        for (int i = 0; i < 1000; i++) sum += *(p + i);
    return sum;
}

struct Point { int x, y; };
static struct Point pts[1000];

static long long bench_struct(void) {
    long long total = 0;
    for (int rep = 0; rep < 200; rep++) {
        for (int i = 0; i < 1000; i++) { pts[i].x = i; pts[i].y = i + 1; }
        long long sum = 0;
        for (int i = 0; i < 1000; i++) sum += pts[i].x + pts[i].y;
        total += sum;
    }
    return total;
}

static int sort_arr[200];

static long long bench_sort(void) {
    long long total = 0;
    for (int rep = 50; rep > 0; rep--) {
        for (int i = 0; i < 200; i++) sort_arr[i] = (i * 37 + rep * 13) % 1000;
        for (int pass = 0; pass < 199; pass++)
            for (int j = 0; j < 199 - pass; j++)
                if (sort_arr[j] > sort_arr[j + 1]) {
                    int t = sort_arr[j];
                    sort_arr[j] = sort_arr[j + 1];
                    sort_arr[j + 1] = t;
                }
        total += sort_arr[0] + sort_arr[199];
    }
    return total;
}

typedef struct { const char *name; long long (*fn)(void); } Bench;

int main(void) {
    Bench benches[] = {
        {"Call", bench_call}, {"Math", bench_math},
        {"AluTput1", bench_alu_tput1}, {"AluTput4", bench_alu_tput4},
        {"AluTputMixed", bench_alu_tput_mixed}, {"Memory", bench_memory},
        {"BitOps", bench_bit_ops}, {"Mul", bench_mul},
        {"Branch", bench_branch}, {"DivMod", bench_div_mod},
        {"Recursion", bench_recursion}, {"Pointer", bench_pointer},
        {"Struct", bench_struct}, {"Sort", bench_sort},
    };
    int n = sizeof(benches) / sizeof(benches[0]);
    for (int i = 0; i < n; i++) {
        benches[i].fn();                        // warmup
        double t0 = now();
        long long res = benches[i].fn();
        double dt = now() - t0;
        fprintf(stderr, "%-12s  %8.4f s  (result=%lld)\n", benches[i].name, dt, res);
    }
    return 0;
}