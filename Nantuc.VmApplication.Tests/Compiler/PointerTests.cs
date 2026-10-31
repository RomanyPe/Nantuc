using Xunit.Abstractions;

namespace Nantuc.Tests.Compiler;

public class PointerTests(ITestOutputHelper outPut) : CompileTestBase(outPut)
{
    [Theory]
    [InlineData("""
    int main() {
        int a[3] = { 10, 20, 30 };
        int *p = a;
        return p[1];   // 20
    }
    """, 20)]
    [InlineData("""
    // Store через dereference с compound value
    int main() { int a[1] = { 0 }; int *q = a; *q = 7 * (1 + 1); return a[0]; }   // 14
    """, 14)]
    [InlineData("""
    int main() { int a[1] = { 7 }; int *q = a; *q += 3; return a[0]; }   // 10
    """, 10)]
    [InlineData("""
    int main() { int a[1] = { 7 }; int *q = a; ++(*q); return a[0]; }   // 8
    """, 8)]
    [InlineData("""
    int main() {
        int a[2] = { 10, 20 }; int *p = a;
        (*p)++;                          // a[0] = 11, p не двигается
        return a[0] + a[1];              // 11 + 20 = 31
    }
    """, 31)]
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
    int main() { int a[3] = { 10, 20, 30 }; int *p = a + 2; p--; return *p; }   // 20 (требует pointer arithmetic)
    """, 20)]
    [InlineData("""
    int main() { int a[3] = { 10, 20, 30 }; int *p = a + 2; return *p; }   // 30
    """, 30)]
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
    int main() { int a[3] = { 10, 20, 30 }; int *p = a; return (p[1] + 5) * (p[2] + 3); }   // 25 * 33 = 825
    """, 825)]
    [InlineData("""
    int g = 10;
    int main() { int *p, q; q = 5; p = &g; return *p + q; }   // 10 + 5 = 15
    """, 15)]
    [InlineData("""
    struct P { int x; int y; };
    int main() { struct P arr[2]; struct P *p = arr; p++; p->x = 42; return arr[1].x; }   // 42
    """, 42)]
    [InlineData("""
    struct P { int x; int y; };
    int main() { struct P arr[2]; struct P *p = arr; p->y = 7; return arr[0].y; }   // 7
    """, 7)]
    [InlineData("""
    struct P { int x; int y; };
    int main() { struct P arr[2]; struct P *p = arr + 1; p->x = 42; p->y = 43; return arr[1].x + arr[1].y; }   // 85
    """, 85)]
    [InlineData("""
    struct S { int a; int b; int c; };  // 24 байта (не степень 2)
    int main() { struct S arr[2]; struct S *p = arr; p++; p->a = 5; return arr[1].a; }   // 5
    """, 5)]
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
    public void Pointer_Operations(string program, ulong res) => RunCompile(program, res);
}