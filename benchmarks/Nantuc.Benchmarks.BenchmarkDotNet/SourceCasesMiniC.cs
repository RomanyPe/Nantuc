using System.Collections.Generic;

namespace Nantuc.Benchmarks.BenchmarkDotNet;

internal static class SourceCasesMiniC
{
    // ────────── MiniC-источники ──────────

    // Вызов функции: sum(r0, r1) в цикле. Аналог CALL-бенчмарка.
    public const string callMc =
    """
int sum(int a, int b) { return a + b; }

int main() {
    int r0 = 0;
    int r1 = 3000000;
    while (r1 != 0) {
        r0 = sum(r0, r1);
        r1 = r1 - 1;
    }
    return r0;
}
""";

    // Простой арифметический цикл.
    public const string mathMc =
    """
int main() {
    int r0 = 0;
    int r1 = 3000000;
    while (r1 != 0) {
        r0 = r0 + r1;
        r1 = r1 - 1;
    }
    return r0;
}
""";

    // 4 ADD в один регистр за итерацию.
    public const string aluTput1Mc =
    """
int main() {
    int r0 = 0;
    int r7 = 1;
    int r6 = 800000;
    while (r6 != 0) {
        r0 = r0 + r7;
        r0 = r0 + r7;
        r0 = r0 + r7;
        r0 = r0 + r7;
        r6 = r6 - 1;
    }
    return r0;
}
""";

    // 4 ADD в разные регистры (IRP-дружественный).
    public const string aluTput4Mc =
    """
int main() {
    int r0 = 0;
    int r1 = 0;
    int r2 = 0;
    int r3 = 0;
    int r7 = 1;
    int r6 = 800000;
    while (r6 != 0) {
        r0 = r0 + r7;
        r1 = r1 + r7;
        r2 = r2 + r7;
        r3 = r3 + r7;
        r6 = r6 - 1;
    }
    return r0 + r1 + r2 + r3;
}
""";

    // Смесь ADD/SUB/AND/OR.
    public const string aluTputMixedMc =
    """
int main() {
    int r0 = 0;
    int r1 = 0;
    int r2 = 0;
    int r3 = 0;
    int r7 = 1;
    int r6 = 800000;
    while (r6 != 0) {
        r0 = r0 + r7;
        r1 = r1 - r7;
        r2 = r2 & r7;
        r3 = r3 | r7;
        r6 = r6 - 1;
    }
    return r0 + r1 + r2 + r3;
}
""";

    // Память: заполнение массива и его суммирование.
    public const string memoryMc =
    """
int arr[25000];

int main() {
    int outer = 20;
    int total = 0;
    while (outer != 0) {
        int i = 0;
        while (i < 25000) {
            arr[i] = i;
            i = i + 1;
        }
        int sum = 0;
        i = 0;
        while (i < 25000) {
            sum = sum + arr[i];
            i = i + 1;
        }
        total = total + sum;
        outer = outer - 1;
    }
    return total;
}
""";

    // Битовые операции.
    public const string bitOpsMc =
    """
int main() {
    int r0 = 1;
    int r1 = 1;
    int r2 = 0;
    int r3 = 0;
    int r4 = 0;
    int r6 = 3000000;
    while (r6 != 0) {
        r0 = r0 >> r1;
        r2 = r2 & r0;
        r3 = r3 | r0;
        r4 = r4 ^ r0;
        r6 = r6 - 1;
    }
    return r0 + r2 + r3 + r4;
}
""";

    // Умножение.
    public const string mulMc =
    """
int main() {
    int r0 = 1;
    int r1 = 3;
    int r6 = 3000000;
    while (r6 != 0) {
        r0 = r0 * r1;
        r6 = r6 - 1;
    }
    return r0;
}
""";

    // Ветвление (чёт/нечет).
    public const string branchMc =
    """
int main() {
    int r5 = 500000;
    int r6 = 0;
    int r2 = 0;
    int r3 = 0;
    while (r5 != 0) {
        r6 = r6 + 1;
        if ((r6 & 1) != 0) {
            r2 = r2 + 1;
        } else {
            r3 = r3 + 1;
        }
        r5 = r5 - 1;
    }
    return r2 + r3;
}
""";

    // NEW: DIV + MOD.
    public const string divModMc =
    """
int main() {
    int r0 = 1000000;
    int r1 = 7;
    int r2 = 0;
    int r6 = 500000;
    while (r6 != 0) {
        r2 = r2 + (r0 / r1);
        r2 = r2 + (r0 % r1);
        r0 = r0 + 1;
        r6 = r6 - 1;
    }
    return r2;
}
""";

    // NEW: Рекурсия (заменяет PushPop — нагрузка на стек через кадры).
    public const string recursionMc =
    """
int down(int n) {
    if (n <= 0) return 0;
    return down(n - 1);
}

int main() {
    int total = 0;
    int outer = 500;
    while (outer != 0) {
        total = total + down(500);
        outer = outer - 1;
    }
    return total;
}
""";

    // NEW: Арифметика указателей + чтение/запись через *p.
    public const string pointerMc =
    """
int main() {
    int arr[1000];
    int *p = arr;
    int n = 1000;
    int i = 0;
    while (i < n) {
        *(p + i) = i;
        i = i + 1;
    }
    int sum = 0;
    int rep = 2000;
    while (rep != 0) {
        i = 0;
        while (i < n) {
            sum = sum + *(p + i);
            i = i + 1;
        }
        rep = rep - 1;
    }
    return sum;
}
""";

    // NEW: Доступ к полям структур в массиве.
    public const string structMc =
    """
struct Point { int x; int y; };

struct Point pts[1000];

int main() {
    int rep = 200;
    int total = 0;
    while (rep != 0) {
        int i = 0;
        while (i < 1000) {
            pts[i].x = i;
            pts[i].y = i + 1;
            i = i + 1;
        }
        int sum = 0;
        i = 0;
        while (i < 1000) {
            sum = sum + pts[i].x + pts[i].y;
            i = i + 1;
        }
        total = total + sum;
        rep = rep - 1;
    }
    return total;
}
""";

    // NEW: Пузырьковая сортировка — комплексный алгоритм.
    public const string sortMc =
    """
int arr[200];

int main() {
    int rep = 50;
    int total = 0;
    while (rep != 0) {
        int i = 0;
        while (i < 200) {
            arr[i] = (i * 37 + rep * 13) % 1000;
            i = i + 1;
        }
        int pass = 0;
        while (pass < 199) {
            int j = 0;
            while (j < 199 - pass) {
                if (arr[j] > arr[j + 1]) {
                    int tmp = arr[j];
                    arr[j] = arr[j + 1];
                    arr[j + 1] = tmp;
                }
                j = j + 1;
            }
            pass = pass + 1;
        }
        total = total + arr[0] + arr[199];
        rep = rep - 1;
    }
    return total;
}
""";

    public static IEnumerable<BenchCase> AllCases { get; } =
    [
        new BenchCase("Call",         callMc),
        new BenchCase("Math",         mathMc),
        new BenchCase("AluTput1",     aluTput1Mc),
        new BenchCase("AluTput4",     aluTput4Mc),
        new BenchCase("AluTputMixed", aluTputMixedMc),
        new BenchCase("Memory",       memoryMc),
        new BenchCase("BitOps",       bitOpsMc),
        new BenchCase("Mul",          mulMc),
        new BenchCase("Branch",       branchMc),
        new BenchCase("DivMod",       divModMc),
        new BenchCase("Recursion",    recursionMc),
        new BenchCase("Pointer",      pointerMc),
        new BenchCase("Struct",       structMc),
        new BenchCase("Sort",         sortMc),
    ];
}