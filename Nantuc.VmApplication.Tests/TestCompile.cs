using Microsoft.VisualStudio.TestPlatform.Utilities;
using Nantuc.Kernel.Common;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Extensions.Disassembler;
using Nantuc.VmApplication.Extensions.Host;
using Nantuc.VmApplication.Project;
using Xunit.Abstractions;

namespace Nantuc.Tests;

public class TestCompile(ITestOutputHelper outPut)
{
    private readonly ITestOutputHelper _outPut = outPut;

    [Theory]
    [InlineData(
        """
        int y[] = { 1, 2, 4, 8, 16 };
        int main() { return y[0] + y[1] + y[2] + y[3]; }
        """, 15)]
    [InlineData(
        """
        int y[] = { 1, 2, 4, 8 };
        int main() { return (y[0]+y[1]) * (y[2]+y[3]); }   // 3 * 12 = 36
        """, 36)]
    [InlineData(
        """
        int y[] = { 1, 2, 4, 8, 16 };
        int main() { return y[0] + (y[1] + (y[2] + (y[3] + y[4]))); }
        """, 31)]
    [InlineData(
        """
        int y[] = { 10, 20, 30, 40 };
        int i[] = { 3, 2, 1 };
        int main() { return y[i[0]] + y[i[1]]; }
        """, 70)]
    [InlineData(
        """
        int y[] = { 1, 2, 3 };
        int main() { int x = y[0] + y[2]; return x * 2; }   // (1+3)*2 = 8
        """, 8)]
    [InlineData(
        """
        int g[] = { 100, 200 };
        int main() { int l[] = { 10, 20 }; return l[0] + g[1]; }   // 210
        """, 210)]
    [InlineData(
        """
        int y[] = { 10, 20 };
        int add(int a, int b) { return a + b; }
        int main() { return add(y[0], y[1]); } 
        """, 30)]
    [InlineData(
        """
        int y[] = { 5, 7 };
        int id(int x) { return x; }
        int main() { if (y[0] < id(y[1])) {return 1;} return 0; }
        """, 1)]
    [InlineData(
        """
        int y[] = { 0, 1, 2, 3 };
        int i[] = { 3, 1, 0 };
        int main() { y[i[0]] = 99; return y[3]; }   // 99
        """, 99)]
    [InlineData(
        """
        int main(){
            return foo(42);
        }

        int foo(int c){
            return c;
        }
        """, 42)]
    [InlineData(
        """
        struct Point { int x; int y; };

        struct Point pts[3];

        int main() {
            pts[0].x = 10;  pts[0].y = 1;
            pts[1].x = 20;  pts[1].y = 2;
            pts[2].x = 30;  pts[2].y = 3;
            return pts[0].x + pts[1].y + pts[2].x;   // 10 + 2 + 30 = 42
        }
        """, 42)]
    [InlineData(
        """
        struct Point { int x; int y; };
        struct Line { struct Point a; struct Point b; };

        struct Line lines[2];

        int main() {
            lines[0].a.x = 1;  lines[0].b.x = 2;
            lines[1].a.x = 3;  lines[1].b.x = 4;
            return lines[0].a.x + lines[1].b.x;   // 1 + 4 = 5
        }
        """, 5)]
    // Побитовые операции
    [InlineData(
    """
    int main() {
        int c = 0xABCD;
        return (c & 0x00FF) + (c >> 8);   // 0xCD + 0xAB = 205 + 171 = 376
    }
    """, 376)]

    // Сдвиги
    [InlineData(
    """
    int main() {
        int x = 5 << 3;
        int y = 64 >> 2;
        return x + y;   // 40 + 16 = 56
    }
    """, 56)]

    // While
    [InlineData(
    """
    int main() {
        int i = 0;
        int sum = 0;
        while (i < 5) { sum = sum + i; i = i + 1; }
        return sum;   // 0+1+2+3+4 = 10
    }
    """, 10)]

    // For
    [InlineData(
    """
    int main() {
        int sum = 0;
        for (int i = 1; i <= 5; i = i + 1) { sum = sum + i; }
        return sum;   // 15
    }
    """, 15)]

    // Рекурсия
    [InlineData(
    """
    int fact(int n) { if (n <= 1) { return 1; } return n * fact(n - 1); }
    int main() { return fact(5); }   // 120
    """, 120)]

    // Указатели
    [InlineData(
    """
    int main() {
        int a[3] = { 10, 20, 30 };
        int *p = a;
        return p[1];   // 20
    }
    """, 20)]

    // if/else — обе ветки
    [InlineData(
    """
    int classify(int x) { if (x > 0) { return 1; } else { return -1;} }
    int main() { return classify(5) + classify(-3) + classify(0); }   // 1 + (-1) + (-1) = -1
    """, unchecked((ulong)-1))]

    [InlineData(
    """
    // Store с compound-индексом
    int y[] = { 0, 1, 2, 3 };
    int i[] = { 1, 2 };
    int main() { y[i[0] + i[1]] = 42; return y[3]; }   // 42
    """, 42)]
    [InlineData(
    """
    // Store через member с compound value
    struct P { int x; };
    struct P p;
    int main() { p.x = 5 * (2 + 3); return p.x; }      // 25
    """, 25)]
    [InlineData(
    """
    // Store через dereference с compound value
    int main() { int a[1] = { 0 }; int *q = a; *q = 7 * (1 + 1); return a[0]; }   // 14
    """, 14)]
    [InlineData(
    """
    // Condition с compound справа
    int y[] = { 5, 7 };
    int main() { if (y[0] < (y[1] + 10)) {return 1;} return 0; }   // 1
    """, 1)]
    [InlineData(
    """
    // 1. Вложенные вызовы в аргументах
    int id(int x) { return x; }
    int add(int a, int b) { return a + b; }
    int main() { return add(id(10), id(20)); }   // 30
    """, 30)]
    [InlineData(
    """
    // 2. 3 compound аргумента
    int s3(int a, int b, int c) { return a + b + c; }
    int y[] = { 1, 2, 3 };
    int main() { return s3(y[0], y[1], y[2]); }   // 6
    """, 6)]
    [InlineData(
    """
    // 3. 5 аргументов (4 в регистрах + 1 через pseudo-global)
    int s5(int a, int b, int c, int d, int e) { return a+b+c+d+e; }
    int main() { return s5(1, 2, 3, 4, 5); }   // 15
    """, 15)]
    [InlineData(
    """
    // 4. 5 compound аргументов (проверяет и pseudo-globals, и сохранение 0..3)
    int s5(int a, int b, int c, int d, int e) { return a+b+c+d+e; }
    int id(int x) { return x; }
    int main() { return s5(id(1), id(2), id(3), id(4), id(5)); }   // 15
    """, 15)]
    [InlineData("""
    int main() { int x = 0; return !x; }       // !0 = 1
    """, 1)]
    [InlineData("""
    int main() { int x = 5; return !x; }       // !5 = 0
    """, 0)]

    // a && b как выражение
    [InlineData("""
    int main() { int a = 1; int b = 1; return a && b; }   // 1
    """, 1)]
    [InlineData("""
    int main() { int a = 1; int b = 0; return a && b; }   // 0
    """, 0)]

    // a || b как выражение
    [InlineData("""
    int main() { int a = 0; int b = 0; return a || b; }   // 0
    """, 0)]
    [InlineData("""
    int main() { int a = 0; int b = 7; return a || b; }   // 1
    """, 1)]

    // ~x, -x
    [InlineData("""
    int main() { return -5; }       // -5 = 0xFFFF...FFFB
    """, unchecked((ulong)-5))]
    [InlineData("""
    int main() { return ~0; }       // -1
    """, unchecked((ulong)-1))]
    // if / else — обе ветки
    [InlineData("""
    int classify(int x) { if (x > 0) return 1; else return -1; }
    int main() { return classify(5) + classify(-3); }   // 1 + (-1) = 0
    """, 0)]

    // while
    [InlineData("""
    int main() {
        int i = 0; int sum = 0;
        while (i < 5) { sum = sum + i; i = i + 1; }
        return sum;   // 10
    }
    """, 10)]

    // for
    [InlineData("""
    int main() {
        int sum = 0;
        for (int i = 1; i <= 5; i = i + 1) sum = sum + i;
        return sum;   // 15
    }
    """, 15)]

    // сравнения — все 6 операторов
    [InlineData("""
    int main() {
        int a = 5; int b = 3;
        int ok = (a > b) + (a >= b) + (a != b) + (b < a) + (b <= a) + (a == 5);
        return ok;   // 6
    }
    """, 6)]

    // вложенные if
    [InlineData("""
    int f(int x) {
        if (x > 10) return 100;
        else if (x > 0) return 10;
        else return 0;
    }
    int main() { return f(50) + f(5) + f(-5); }   // 100 + 10 + 0 = 110
    """, 110)]

    // рекурсия через if (уже был тест, но добавьте с !=)
    [InlineData("""
    int fib(int n) {
        if (n <= 1) return n;
        return fib(n-1) + fib(n-2);
    }
    int main() { return fib(7); }   // 13
    """, 13)]

    [InlineData("""
    int main() { int sum = 0; for (int i = 1; i <= 5; i = i + 1) sum = sum + i; return sum; }
    """, 15)]
    [InlineData("""
    int f(int x) { if (x > 10) return 100; else if (x > 0) return 10; else return 0; }
    int main() { return f(50) + f(5) + f(-5); }
    """, 110)]
    [InlineData("""
    int main() { if (1) return 1; else return 0; }
    """, 1)]
    [InlineData("""
    int main() { int i = 0; for (; i < 3; i = i + 1) { } return i; }
    """, 3)]  // пустой блок — работает; пустое тело без брекетов — нет
              // Твоя задача
    [InlineData("""
    int main() {
        int a = -22, b = 2999, c = 0;
        return a + b + c;   // -22 + 2999 = 2977
    }
    """, 2977)]

    // Без инициализаторов
    [InlineData("""
    int main() { int a, b, c; a = 1; b = 2; c = 3; return a + b + c; }
    """, 6)]

    // Со смешанным указателем (проверка, что второй не получил *)
    [InlineData("""
    int g = 10;
    int main() { int *p, q; q = 5; p = &g; return *p + q; }   // 10 + 5 = 15
    """, 15)]

    // for-init с несколькими переменными
    [InlineData("""
    int main() {
        int sum = 0;
        for (int i = 0, j = 10; i < 3; i = i + 1) sum = sum + i + j;
        return sum;   // (0+10) + (1+10) + (2+10) = 33
    }
    """, 33)]

    // Глобальные с несколькими
    [InlineData("""
    int a = 1, b = 2, c = 3;
    int main() { return a + b + c; }
    """, 6)]
    [InlineData("""
        int add(int a, int b) { return a + b; }
        int main() { int x = 1; int y = 2; return add((x, y), 0); }   // (x,y) = comma → y=2; add(2, 0) = 2
        """, 2)]
    // break в for
    [InlineData("""
int main() {
    int sum = 0;
    for (int i = 1; i <= 10; i = i + 1) {
        if (i > 5) break;
        sum = sum + i;
    }
    return sum;   // 1+2+3+4+5 = 15
}
""", 15)]

    // continue в for
    [InlineData("""
int main() {
    int sum = 0;
    for (int i = 1; i <= 10; i = i + 1) {
        if (i % 2 == 0) continue;   // пропустить чётные
        sum = sum + i;
    }
    return sum;   // 1+3+5+7+9 = 25
}
""", 25)]

    // continue в while
    [InlineData("""
int main() {
    int i = 0, s = 0;
    while (i < 10) {
        i = i + 1;
        if (i % 2 == 0) continue;
        s = s + i;
    }
    return s;   // 1+3+5+7+9 = 25
}
""", 25)]

    // вложенные циклы — break только из внутреннего
    [InlineData("""
int main() {
    int s = 0;
    for (int i = 0; i < 3; i = i + 1) {
        for (int j = 0; j < 10; j = j + 1) {
            if (j == 2) break;
            s = s + 1;
        }
    }
    return s;   // 3 * 2 = 6
}
""", 6)]

    // do-while
    [InlineData("""
int main() {
    int i = 0, s = 0;
    do { s = s + i; i = i + 1; } while (i < 5);
    return s;   // 0+1+2+3+4 = 10
}
""", 10)]

    // do-while с continue
    [InlineData("""
int main() {
    int i = 0, s = 0;
    do {
        i = i + 1;
        if (i % 2 == 0) continue;
        s = s + i;
    } while (i < 5);
    return s;   // 1+3+5 = 9
}
""", 9)]

    // % модуло
    [InlineData("""
int main() { return 17 % 5 + 20 % 3; }   // 2 + 2 = 4
""", 4)]

    // compound: +=
    [InlineData("""
int main() { int x = 10; x += 5; x -= 3; x *= 2; return x; }   // (10+5-3)*2 = 24
""", 24)]

    // compound: bitwise
    [InlineData("""
int main() { int x = 0xF0; x |= 0x0F; x &= 0x3C; return x; }   // (0xF0|0x0F)&0x3C = 0xFF & 0x3C = 0x3C = 60
""", 60)]

    // compound в массиве
    [InlineData("""
int main() { int a[1] = { 10 }; a[0] += 5; return a[0]; }   // 15
""", 15)]

    // compound в поле структуры
    [InlineData("""
struct P { int x; };
int main() { struct P p; p.x = 10; p.x *= 3; return p.x; }   // 30
""", 30)]

    // compound через указатель
    [InlineData("""
int main() { int a[1] = { 7 }; int *q = a; *q += 3; return a[0]; }   // 10
""", 10)]

    // chained assignment
    [InlineData("""
int main() { int x = 0, y = 0; x = y = 5; return x + y; }   // 10
""", 10)]
    [InlineData("""
int s4(int a, int b, int c, int d) { return a + b + c + d; }
int main() { int i = 3, j = 4; return s4(1, 2, 3, i + j); }   // 1+2+3+7 = 13
""", 13)]
    [InlineData("""
int main() {
    int a[3] = { 10, 20, 30 };
    int *p = a;
    return (p[1] + 5) * (p[2] + 3);   // 25 * 33 = 825
}
""", 825)]
    // Postfix возвращает old
    [InlineData("int main() { int a[1] = { 5 }; int x = a[0]++; return x + a[0]; }", 11)] // 5 + 6
                                                                                          // Prefix возвращает new
    [InlineData("int main() { int a[1] = { 5 }; int x = ++a[0]; return x + a[0]; }", 12)] // 6 + 6
                                                                                          // Указатель
    [InlineData("""
int main() {
    int a[2] = { 10, 20 }; int *p = a;
    (*p)++;                          // a[0] = 11, p не двигается
    return a[0] + a[1];              // 11 + 20 = 31
}
""", 31)]
    // Struct member
    [InlineData("struct P { int x; }; int main() { struct P p; p.x = 10; int y = p.x++; return y + p.x; }", 21)] // 10 + 11
                                                                                                                 // Цикл с i++
    [InlineData("int main() { int s = 0; for (int i = 0; i < 5; i++) s = s + i; return s; }", 10)]
    // Compound body: декремент в while
    [InlineData("int main() { int n = 5; int s = 0; while (n--) s = s + n; return s; }", 10)] // 4+3+2+1+0
                                                                                              // Prefix deref как statement
    [InlineData("""
int main() { int a[1] = { 7 }; int *q = a; ++(*q); return a[0]; }   // 8
""", 8)]

    // Postfix array: результат отбрасывается (statement)
    [InlineData("""
int main() { int a[1] = { 5 }; a[0]++; return a[0]; }   // 6
""", 6)]

    // Prefix array: результат отбрасывается
    [InlineData("""
int main() { int a[1] = { 5 }; ++a[0]; return a[0]; }   // 6
""", 6)]

    // Member postfix как statement
    [InlineData("""
struct P { int x; };
int main() { struct P p; p.x = 5; p.x++; return p.x; }   // 6
""", 6)]

    // Increment в expression с targetReg != r0 (проверяет tempReg)
    [InlineData("""
int main() { int a[1] = { 5 }; int x = 100 + a[0]++; return x + a[0]; }   // (100+5) + 6 = 111
""", 111)]

    // for-цикл со statement-increment внутри
    [InlineData("""
int main() { int sum = 0; int i = 0; while (i < 3) { sum = sum + i; i++; } return sum; }   // 0+1+2 = 3
""", 3)]
    [InlineData("""
int main() { int a[1] = { 7 }; int *p = a; int y = 5 + *p; return y; }   // 12
""", 12)]
    [InlineData("""
int main() { int a[1] = { 7 }; int *p = a; int x = *p + *p; return x; }   // 14
""", 14)]
    [InlineData("""
int main() { int a[1] = { 7 }; int *p = a; return *p * 3; }   // 21
""", 21)]

    [InlineData("""
int main() { int a[3] = { 10, 20, 30 }; int *p = a; p++; return *p; }   // 20
""", 20)]
    [InlineData("""
int main() { int a[3] = { 10, 20, 30 }; int *p = a; ++p; return *p; }   // 20
""", 20)]
    [InlineData("""
int main() { int a[3] = { 10, 20, 30 }; int *p = a; int *q = p++; return *q + *p; }   // 10+20 = 30
""", 30)]
    [InlineData("""
struct P { int x; int y; };
int main() { struct P arr[2]; struct P *p = arr; p++; p->x = 42; return arr[1].x; }   // 42
""", 42)]
    [InlineData("""
int main() { int a[3] = { 10, 20, 30 }; int *p = a + 2; p--; return *p; }   // 20 (требует pointer arithmetic)
""", 20)]
    [InlineData("int main() { return sizeof(int); }", 4)]
    [InlineData("int main() { return sizeof(char); }", 2)]
    [InlineData("int main() { return sizeof(byte); }", 1)]
    [InlineData("int main() { return sizeof(ushort); }", 2)]
    [InlineData("int main() { return sizeof(ulong); }", 8)]
    [InlineData("int main() { return sizeof(int*); }", 8)]
    [InlineData("""
struct P { int x; int y; };
int main() { return sizeof(P); }
""", 8)]
    [InlineData("""
struct P { int x; int y; };
int main() { return sizeof(struct P); }
""", 8)]
    [InlineData("int main() { int x; return sizeof(x); }", 4)]
    [InlineData("int main() { ulong x; return sizeof(x); }", 8)]
    [InlineData("int main() { int arr[10]; return sizeof(arr); }", 40)]
    [InlineData("int main() { int *p; return sizeof(p); }", 8)]
    [InlineData("int main() { int a[1]; int *p = a; return sizeof(*p); }", 4)]
    [InlineData("int main() { return sizeof(int) * 2; }", 8)]
    [InlineData("""
struct P { int x; byte b; };
int main() { struct P p; return sizeof(p.x) + sizeof(p.b); }
""", 5)]

    // pointer + int (array decay)
    [InlineData("""
int main() { int a[3] = { 10, 20, 30 }; int *p = a + 2; return *p; }   // 30
""", 30)]
    [InlineData("""
int main() { int a[3] = { 10, 20, 30 }; int *p = a + 2; p--; return *p; }   // 20
""", 20)]
    [InlineData("""
int main() { int a[3] = { 10, 20, 30 }; int *p = a + 1; int *q = p + 1; return *q; }   // 30
""", 30)]
    [InlineData("""
int main() { int a[3] = { 10, 20, 30 }; int *p = 1 + a; return *p; }   // 20 (commutative)
""", 20)]
    [InlineData("""
int main() { int a[3] = { 10, 20, 30 }; int *p = a + 2; int *q = p - 1; return *q; }   // 20
""", 20)]

    [InlineData("""
struct P { int x; int y; };
int main() { struct P arr[2]; struct P *p = arr; p->y = 7; return arr[0].y; }   // 7
""", 7)]
    [InlineData("""
struct P { int x; int y; };
int main() { struct P arr[2]; struct P *p = arr + 1; p->x = 42; p->y = 43; return arr[1].x + arr[1].y; }   // 85
""", 85)]

    // struct with non-power-of-2 size (проверяет EmitMultiplyByConstant)
    [InlineData("""
struct S { int a; int b; int c; };  // 24 байта (не степень 2)
int main() { struct S arr[2]; struct S *p = arr; p++; p->a = 5; return arr[1].a; }   // 5
""", 5)]

    // p + int + p->field
    [InlineData("""
struct P { int x; };
int main() {
    struct P arr[3];
    (arr + 0)->x = 10;
    (arr + 1)->x = 20;
    (arr + 2)->x = 30;
    return (arr + 1)->x;
}
""", 20)]
    [InlineData("""
struct P { int x; int y; };
int main() {
    struct P arr[2];
    struct P *p = arr + 1;
    p->x = 42;
    return arr[1].x;
}
""", 42)]

    // ═══════════════════════════════════════════════════════════════
    // CONST — позитивные: компилируются и возвращают ожидаемое значение
    // ═══════════════════════════════════════════════════════════════

    // базовый const-скаляр: чтение работает как у обычной переменной
    [InlineData("const int x = 5; int main() { return x; }", 5)]
    [InlineData("const int G = 42; int main() { return G; }", 42)]
    [InlineData("int main() { const int x = 7; return x * 3; }", 21)]

    // const-массив: элементы read-only, но читаются нормально
    [InlineData("int main() { const int arr[3] = { 10, 20, 30 }; return arr[0] + arr[1] + arr[2]; }", 60)]

    // int * const p — p нельзя переприсвоить, *p можно менять
    [InlineData("int main() { int a = 5; int * const p = &a; *p = 10; return a; }", 10)]
    [InlineData("int main() { int a = 5; int * const p = &a; (*p)++; return a; }", 6)]
    [InlineData("int main() { int a = 5; int * const p = &a; *p += 3; return a; }", 8)]

    // const int *p — *p нельзя менять, но p можно переприсваивать
    [InlineData("int main() { int a = 5; const int *p = &a; return *p; }", 5)]
    [InlineData("int main() { int a[3] = { 10, 20, 30 }; const int *p = a; p = p + 1; return *p; }", 20)]
    [InlineData("int main() { int a[3] = { 10, 20, 30 }; const int *p = a; p++; return *p; }", 20)]

    // const int * const p — ни то, ни другое, но читать можно
    [InlineData("int main() { int a = 5; const int * const p = &a; return *p; }", 5)]

    // параметры функции
    [InlineData("int f(const int x) { return x + 1; } int main() { return f(10); }", 11)]
    [InlineData("int f(const int *p) { return *p * 2; } int main() { int a = 21; return f(&a); }", 42)]

    // sizeof от const — const не влияет на размер
    [InlineData("int main() { const int x = 5; return sizeof(x); }", 4)]
    [InlineData("int main() { return sizeof(const int); }", 4)]
    [InlineData("int main() { return sizeof(const int*); }", 8)]

    // передача const в не-const параметр по значению — допустимо (copy)
    [InlineData("int f(int x) { x = 100; return x; } const int a = 5; int main() { return f(a); }", 100)]
    // ═══════════════════════════════════════════════════════════════
    // FUNCTION POINTERS — MVP
    // void *f = foo; f();
    // Все указатели — 8 байт, типизация условная (как в железе).
    // ═══════════════════════════════════════════════════════════════

    // базовый сценарий: присваивание и косвенный вызов
    [InlineData("""
int foo() { return 42; }
int main() { void *f = foo; return f(); }
""", 42)]

    // через унарный &
    [InlineData("""
int foo() { return 42; }
int main() { void *f = &foo; return f(); }
""", 42)]

    // без инициализатора: присваивание и вызов
    [InlineData("""
int foo() { return 7; }
int main() { void *f; f = foo; return f(); }
""", 7)]

    // с двумя аргументами
    [InlineData("""
int add(int a, int b) { return a + b; }
int main() { void *f = add; return f(3, 4); }
""", 7)]

    // 4 аргумента — верхняя граница MVP
    // (5+ не поддерживается: extras идут через __param_<callee>_<name>,
    //  а при косвенном вызове имя callee неизвестно)
    [InlineData("""
int s4(int a, int b, int c, int d) { return a + b + c + d; }
int main() { void *f = s4; return f(1, 2, 3, 4); }
""", 10)]

    // compound-аргументы (r0..r3 сохраняются на стек)
    [InlineData("""
int add(int a, int b) { return a + b; }
int main() { int x = 3; void *f = add; return f(x * 2, x + 1); }
""", 10)]

    // два указателя одновременно
    [InlineData("""
int a() { return 10; }
int b() { return 20; }
int main() { void *f = a; void *g = b; return f() + g(); }
""", 30)]

    // глобальная переменная-указатель (инициализируется в __global_init)
    [InlineData("""
int foo() { return 99; }
void *gfp = foo;
int main() { return gfp(); }
""", 99)]

    // динамический выбор функции в рантайме
    [InlineData("""
int add(int a, int b) { return a + b; }
int sub(int a, int b) { return a - b; }
int main() {
    void *f = add;
    int r1 = f(10, 3);   // 13
    f = sub;
    int r2 = f(10, 3);   // 7
    return r1 + r2;      // 20
}
""", 20)]

    // передача указателя на функцию как аргумента
    [InlineData("""
int foo() { return 7; }
int call(void *f) { return f(); }
int main() { return call(foo); }
""", 7)]

    // указатель на функцию как возвращаемое значение (через void*)
    [InlineData("""
int foo() { return 55; }
void *get() { return foo; }
int main() { void *f = get(); return f(); }
""", 55)]

    // вызов-как-statement (результат отбрасывается)
    [InlineData("""
int counter = 0;
void bump() { counter = counter + 1; }
int main() { void *f = bump; f(); f(); f(); return counter; }
""", 3)]

    // callee принимает обычный указатель (совместимость типов через void*)
    [InlineData("""
int deref(int *p) { return *p; }
int main() { int a = 88; void *f = deref; return f(&a); }
""", 88)]

    // рекурсивный вызов через указатель
    [InlineData("""
int countdown(int n) {
    if (n <= 0) return 0;
    return 1 + countdown(n - 1);
}
int main() { void *f = countdown; return f(5); }
""", 5)]

    // два указателя на одну функцию
    [InlineData("""
int foo() { return 42; }
int main() { void *a = foo; void *b = foo; return a() + b(); }
""", 84)]

    // переприсваивание параметра внутри callee не влияет на caller
    [InlineData("""
int a() { return 1; }
int b() { return 2; }
int pick(void *f) { f = b; return f(); }
int main() { void *g = a; return pick(g); }
""", 2)]

    // указатель как условие (ненулевой адрес → truthy)
    [InlineData("""
int foo() { return 1; }
int main() { void *f = foo; if (f) return 7; return 0; }
""", 7)]

    // копирование указателя через переменную
    [InlineData("""
int foo() { return 123; }
int main() { void *f = foo; void *g = f; return g(); }
""", 123)]
    // копирование указателя через переменную
    [InlineData("""
int foo() { return 7; }
int bar() { return 13; }
int add(int a, int b) { return a + b; }

int main() {
    void *arr[3] = { foo, bar, add };

    int x = arr[0]();                    // 7
    int y = arr[1]();                    // 13
    int z = arr[2](x, y);                // 20
    return x + y + z;                    // 40
}
""", 40)]

    //// внешняя функция как значение (IsExternal — только адрес, вызов → линкер)
    //// ⚠️ раскомментируй и добавь соответствующую extern-декларацию,
    ////    если у хоста есть такая функция
    //[InlineData("""
    // extern int abs(int x);
    // int main() { void *f = abs; return f(-7); }
    // """, 7)]
    public void RunCompile(string program, ulong res)
    {
        using var host = VmHostFactory.CreateDefault();
        var handle = DeviceSpec.Create(RamSize.MB16, name: "test");
        var deviceId = host.Emulator.RegisterDevice(handle, sector: 0);
        var deviceCtx = host.Emulator.GetDeviceContext(deviceId)!;

        var comp = host.Project.CompileToIL(program, 0, false, SourceLanguage.C).Build();
        if (!comp.Success)
        {
            _outPut.WriteLine(string.Join("\n", comp.Errors ?? []));
            _outPut.WriteLine(program);
        }
        Assert.True(comp.Success);
        Assert.True(deviceCtx.TryLoadProgram(comp.Program!, 0, out var err), err);

        deviceCtx.Run();
        var regs = deviceCtx.GetRegistersSnapshot();
        bool v = res == regs[RegType.r0];

        if (!v)
        {
            _outPut.WriteLine($"Expected: {res}");
            _outPut.WriteLine($"Actual: {regs[RegType.r0]}");
            _outPut.WriteLine("--- MiniC ---");
            _outPut.WriteLine(program);
            _outPut.WriteLine("--- NANASM ---");
            _outPut.WriteLine(comp.DisassembleToString());
        }

        Assert.True(v);
    }
    // ═══════════════════════════════════════════════════════════════
    // CONST — негативные: компиляция ОБЯЗАНА упасть
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    // базовый const-скаляр
    [InlineData("const int x = 5; int main() { x = 6; return x; }")]
    [InlineData("int main() { const int x = 5; x = 6; return x; }")]
    [InlineData("const int G = 5; int main() { G = 10; return G; }")]
    [InlineData("const int x = 5; int main() { x++; return x; }")]
    [InlineData("const int x = 5; int main() { --x; return x; }")]
    [InlineData("const int x = 5; int main() { x += 1; return x; }")]
    [InlineData("const int x = 5; int main() { x *= 2; return x; }")]

    // массив const — элементы read-only
    [InlineData("int main() { const int arr[3] = { 1, 2, 3 }; arr[0] = 5; return 0; }")]
    [InlineData("int main() { const int arr[3] = { 1, 2, 3 }; arr[1]++; return 0; }")]
    [InlineData("int main() { const int arr[3] = { 1, 2, 3 }; arr[2] += 1; return 0; }")]

    // int * const p — нельзя переприсвоить сам указатель
    [InlineData("int main() { int a = 5; int * const p = &a; p = &a; return 0; }")]
    [InlineData("int main() { int a = 5; int * const p = &a; p++; return 0; }")]
    [InlineData("int main() { int a = 5; int * const p = &a; --p; return 0; }")]

    // const int *p — нельзя писать через указатель
    [InlineData("int main() { int a = 5; const int *p = &a; *p = 10; return 0; }")]
    [InlineData("int main() { int a = 5; const int *p = &a; (*p)++; return 0; }")]
    [InlineData("int main() { int a = 5; const int *p = &a; *p += 1; return 0; }")]
    [InlineData("int main() { int a = 5; const int *p = &a; p[0] = 10; return 0; }")]
    [InlineData("int main() { int a = 5; const int *p = &a; p[0]++; return 0; }")]

    // const int * const p — ни то, ни другое
    [InlineData("int main() { int a = 5; const int * const p = &a; *p = 10; return 0; }")]
    [InlineData("int main() { int a = 5; const int * const p = &a; p = &a; return 0; }")]
    [InlineData("int main() { int a = 5; const int * const p = &a; p++; return 0; }")]

    // параметры
    [InlineData("int f(const int x) { x = 5; return x; } int main() { return f(10); }")]
    [InlineData("int f(const int x) { x++; return x; } int main() { return f(10); }")]
    [InlineData("int f(const int *p) { *p = 10; return 0; } int main() { int a = 5; return f(&a); }")]
    [InlineData("int f(const int *p) { (*p)++; return 0; } int main() { int a = 5; return f(&a); }")]
    public void RunCompile_ConstViolation(string program)
    {
        using var host = VmHostFactory.CreateDefault();
        var handle = DeviceSpec.Create(RamSize.MB16, name: "test");
        var deviceId = host.Emulator.RegisterDevice(handle, sector: 0);
        var deviceCtx = host.Emulator.GetDeviceContext(deviceId)!;

        var comp = host.Project.CompileToIL(program, 0, false, SourceLanguage.C).Build();

        if (comp.Success)
        {
            _outPut.WriteLine("Expected compilation to FAIL, but it succeeded:");
            _outPut.WriteLine(program);
            _outPut.WriteLine("--- NANASM ---");
            _outPut.WriteLine(comp.DisassembleToString());
        }

        Assert.False(comp.Success,
            "Compilation unexpectedly succeeded — const violation not caught");
        Assert.NotNull(comp.Errors);
        Assert.NotEmpty(comp.Errors!);
    }
}