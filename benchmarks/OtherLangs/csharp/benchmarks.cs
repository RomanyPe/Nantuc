// benchmarks.cs
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

static class Benchmarks
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    static int Sum(int a, int b) => a + b;

    static long BenchCall() {
        int r0 = 0, r1 = 3_000_000;
        while (r1 != 0) { r0 = Sum(r0, r1); r1--; }
        return r0;
    }
    static long BenchMath() {
        int r0 = 0, r1 = 3_000_000;
        while (r1 != 0) { r0 += r1; r1--; }
        return r0;
    }
    static long BenchAluTput1() {
        int r0 = 0, r7 = 1, r6 = 800_000;
        while (r6 != 0) { r0 += r7; r0 += r7; r0 += r7; r0 += r7; r6--; }
        return r0;
    }
    static long BenchAluTput4() {
        int r0 = 0, r1 = 0, r2 = 0, r3 = 0, r7 = 1, r6 = 800_000;
        while (r6 != 0) { r0 += r7; r1 += r7; r2 += r7; r3 += r7; r6--; }
        return (long)r0 + r1 + r2 + r3;
    }
    static long BenchAluTputMixed() {
        int r0 = 0, r1 = 0, r2 = 0, r3 = 0, r7 = 1, r6 = 800_000;
        while (r6 != 0) { r0 += r7; r1 -= r7; r2 &= r7; r3 |= r7; r6--; }
        return (long)r0 + r1 + r2 + r3;
    }
    static long BenchMemory() {
        var arr = new int[25_000];
        long total = 0;
        for (int outer = 0; outer < 20; outer++) {
            for (int i = 0; i < 25_000; i++) arr[i] = i;
            long s = 0;
            for (int i = 0; i < 25_000; i++) s += arr[i];
            total += s;
        }
        return total;
    }
    static long BenchBitOps() {
        int r0 = 1, r1 = 1, r2 = 0, r3 = 0, r4 = 0, r6 = 3_000_000;
        while (r6 != 0) { r0 >>= r1; r2 &= r0; r3 |= r0; r4 ^= r0; r6--; }
        return (long)r0 + r2 + r3 + r4;
    }
    static long BenchMul() {
        int r0 = 1, r1 = 3, r6 = 3_000_000;
        while (r6 != 0) { r0 *= r1; r6--; }
        return r0;
    }
    static long BenchBranch() {
        int r5 = 500_000, r6 = 0;
        long r2 = 0, r3 = 0;
        while (r5 != 0) { r6++; if ((r6 & 1) != 0) r2++; else r3++; r5--; }
        return r2 + r3;
    }
    static long BenchDivMod() {
        int r0 = 1_000_000, r1 = 7, r6 = 500_000;
        long r2 = 0;
        while (r6 != 0) { r2 += r0 / r1; r2 += r0 % r1; r0++; r6--; }
        return r2;
    }
    static int Down(int n) => n <= 0 ? 0 : Down(n - 1);
    static long BenchRecursion() {
        long total = 0;
        for (int o = 0; o < 500; o++) total += Down(500);
        return total;
    }
    static long BenchPointer() {
        var arr = new int[1000];
        for (int i = 0; i < 1000; i++) arr[i] = i;
        long s = 0;
        for (int rep = 0; rep < 2000; rep++)
            for (int i = 0; i < 1000; i++) s += arr[i];
        return s;
    }
    readonly struct Point { public readonly int X, Y; public Point(int x, int y) { X = x; Y = y; } }
    static long BenchStruct() {
        var pts = new Point[1000];
        long total = 0;
        for (int rep = 0; rep < 200; rep++) {
            for (int i = 0; i < 1000; i++) pts[i] = new Point(i, i + 1);
            long s = 0;
            for (int i = 0; i < 1000; i++) s += pts[i].X + pts[i].Y;
            total += s;
        }
        return total;
    }
    static long BenchSort() {
        var arr = new int[200];
        long total = 0;
        for (int rep = 50; rep > 0; rep--) {
            for (int i = 0; i < 200; i++) arr[i] = (i * 37 + rep * 13) % 1000;
            for (int pass = 0; pass < 199; pass++)
                for (int j = 0; j < 199 - pass; j++)
                    if (arr[j] > arr[j + 1]) (arr[j], arr[j + 1]) = (arr[j + 1], arr[j]);
            total += arr[0] + arr[199];
        }
        return total;
    }

    static void Main() {
        var benches = new (string, Func<long>)[] {
            ("Call", BenchCall), ("Math", BenchMath),
            ("AluTput1", BenchAluTput1), ("AluTput4", BenchAluTput4),
            ("AluTputMixed", BenchAluTputMixed), ("Memory", BenchMemory),
            ("BitOps", BenchBitOps), ("Mul", BenchMul),
            ("Branch", BenchBranch), ("DivMod", BenchDivMod),
            ("Recursion", BenchRecursion), ("Pointer", BenchPointer),
            ("Struct", BenchStruct), ("Sort", BenchSort),
        };
        foreach (var (name, fn) in benches) {
            fn();                          // warmup
            var sw = Stopwatch.StartNew();
            long res = fn();
            sw.Stop();
            Console.Error.WriteLine($"{name,-12}  {sw.Elapsed.TotalSeconds,8:F4} s  (result={res})");
        }
    }
}