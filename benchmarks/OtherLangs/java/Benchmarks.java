// Benchmarks.java
// javac Benchmarks.java && java Benchmarks
public class Benchmarks {
    static int sum(int a, int b) { return a + b; }

    static long benchCall() {
        int r0 = 0, r1 = 3_000_000;
        while (r1 != 0) { r0 = sum(r0, r1); r1--; }
        return r0;
    }
    static long benchMath() {
        int r0 = 0, r1 = 3_000_000;
        while (r1 != 0) { r0 += r1; r1--; }
        return r0;
    }
    static long benchAluTput1() {
        int r0 = 0, r7 = 1, r6 = 800_000;
        while (r6 != 0) { r0 += r7; r0 += r7; r0 += r7; r0 += r7; r6--; }
        return r0;
    }
    static long benchAluTput4() {
        int r0 = 0, r1 = 0, r2 = 0, r3 = 0, r7 = 1, r6 = 800_000;
        while (r6 != 0) { r0 += r7; r1 += r7; r2 += r7; r3 += r7; r6--; }
        return (long)r0 + r1 + r2 + r3;
    }
    static long benchAluTputMixed() {
        int r0 = 0, r1 = 0, r2 = 0, r3 = 0, r7 = 1, r6 = 800_000;
        while (r6 != 0) { r0 += r7; r1 -= r7; r2 &= r7; r3 |= r7; r6--; }
        return (long)r0 + r1 + r2 + r3;
    }
    static long benchMemory() {
        int[] arr = new int[25_000];
        long total = 0;
        for (int outer = 0; outer < 20; outer++) {
            for (int i = 0; i < 25_000; i++) arr[i] = i;
            long s = 0;
            for (int i = 0; i < 25_000; i++) s += arr[i];
            total += s;
        }
        return total;
    }
    static long benchBitOps() {
        int r0 = 1, r1 = 1, r2 = 0, r3 = 0, r4 = 0, r6 = 3_000_000;
        while (r6 != 0) { r0 >>= r1; r2 &= r0; r3 |= r0; r4 ^= r0; r6--; }
        return (long)r0 + r2 + r3 + r4;
    }
    static long benchMul() {
        int r0 = 1, r1 = 3, r6 = 3_000_000;
        while (r6 != 0) { r0 *= r1; r6--; }
        return r0;
    }
    static long benchBranch() {
        int r5 = 500_000, r6 = 0;
        long r2 = 0, r3 = 0;
        while (r5 != 0) { r6++; if ((r6 & 1) != 0) r2++; else r3++; r5--; }
        return r2 + r3;
    }
    static long benchDivMod() {
        int r0 = 1_000_000, r1 = 7, r6 = 500_000;
        long r2 = 0;
        while (r6 != 0) { r2 += r0 / r1; r2 += r0 % r1; r0++; r6--; }
        return r2;
    }
    static int down(int n) { return n <= 0 ? 0 : down(n - 1); }
    static long benchRecursion() {
        long total = 0;
        for (int o = 0; o < 500; o++) total += down(500);
        return total;
    }
    static long benchPointer() {
        int[] arr = new int[1000];
        for (int i = 0; i < 1000; i++) arr[i] = i;
        long s = 0;
        for (int rep = 0; rep < 2000; rep++)
            for (int i = 0; i < 1000; i++) s += arr[i];
        return s;
    }
    static final class Point { int x, y; Point(int x, int y) { this.x = x; this.y = y; } }
    static long benchStruct() {
        Point[] pts = new Point[1000];
        long total = 0;
        for (int rep = 0; rep < 200; rep++) {
            for (int i = 0; i < 1000; i++) pts[i] = new Point(i, i + 1);
            long s = 0;
            for (int i = 0; i < 1000; i++) s += pts[i].x + pts[i].y;
            total += s;
        }
        return total;
    }
    static long benchSort() {
        int[] arr = new int[200];
        long total = 0;
        for (int rep = 50; rep > 0; rep--) {
            for (int i = 0; i < 200; i++) arr[i] = (i * 37 + rep * 13) % 1000;
            for (int pass = 0; pass < 199; pass++)
                for (int j = 0; j < 199 - pass; j++)
                    if (arr[j] > arr[j + 1]) {
                        int t = arr[j]; arr[j] = arr[j + 1]; arr[j + 1] = t;
                    }
            total += arr[0] + arr[199];
        }
        return total;
    }

    interface Bench { long run(); }

    public static void main(String[] args) {
        String[] names = {"Call","Math","AluTput1","AluTput4","AluTputMixed",
                          "Memory","BitOps","Mul","Branch","DivMod",
                          "Recursion","Pointer","Struct","Sort"};
        Bench[] fns = {
            Benchmarks::benchCall, Benchmarks::benchMath,
            Benchmarks::benchAluTput1, Benchmarks::benchAluTput4,
            Benchmarks::benchAluTputMixed, Benchmarks::benchMemory,
            Benchmarks::benchBitOps, Benchmarks::benchMul,
            Benchmarks::benchBranch, Benchmarks::benchDivMod,
            Benchmarks::benchRecursion, Benchmarks::benchPointer,
            Benchmarks::benchStruct, Benchmarks::benchSort,
        };
        for (int i = 0; i < names.length; i++) {
            fns[i].run();                                   // warmup
            long t0 = System.nanoTime();
            long res = fns[i].run();
            long dtNs = System.nanoTime() - t0;
            System.err.printf("%-12s  %8.4f s  (result=%d)%n", names[i], dtNs / 1e9, res);
        }
    }
}