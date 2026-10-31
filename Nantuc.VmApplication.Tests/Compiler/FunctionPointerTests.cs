using Xunit.Abstractions;

namespace Nantuc.Tests.Compiler;

public class FunctionPointerTests(ITestOutputHelper outPut) : CompileTestBase(outPut)
{
    [Theory]
    [InlineData("""
    int foo() { return 42; }
    int main() { void *f = foo; return f(); }
    """, 42)]
    [InlineData("""
    int foo() { return 42; }
    int main() { void *f = &foo; return f(); }
    """, 42)]
    [InlineData("""
    int foo() { return 7; }
    int main() { void *f; f = foo; return f(); }
    """, 7)]
    [InlineData("""
    int add(int a, int b) { return a + b; }
    int main() { void *f = add; return f(3, 4); }
    """, 7)]
    [InlineData("""
    int s4(int a, int b, int c, int d) { return a + b + c + d; }
    int main() { void *f = s4; return f(1, 2, 3, 4); }
    """, 10)]
    [InlineData("""
    int add(int a, int b) { return a + b; }
    int main() { int x = 3; void *f = add; return f(x * 2, x + 1); }
    """, 10)]
    [InlineData("""
    int a() { return 10; }
    int b() { return 20; }
    int main() { void *f = a; void *g = b; return f() + g(); }
    """, 30)]
    [InlineData("""
    int foo() { return 99; }
    void *gfp = foo;
    int main() { return gfp(); }
    """, 99)]
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
    [InlineData("""
    int foo() { return 7; }
    int call(void *f) { return f(); }
    int main() { return call(foo); }
    """, 7)]
    [InlineData("""
    int foo() { return 55; }
    void *get() { return foo; }
    int main() { void *f = get(); return f(); }
    """, 55)]
    [InlineData("""
    int counter = 0;
    void bump() { counter = counter + 1; }
    int main() { void *f = bump; f(); f(); f(); return counter; }
    """, 3)]
    [InlineData("""
    int deref(int *p) { return *p; }
    int main() { int a = 88; void *f = deref; return f(&a); }
    """, 88)]
    [InlineData("""
    int countdown(int n) {
        if (n <= 0) return 0;
        return 1 + countdown(n - 1);
    }
    int main() { void *f = countdown; return f(5); }
    """, 5)]
    [InlineData("""
    int foo() { return 42; }
    int main() { void *a = foo; void *b = foo; return a() + b(); }
    """, 84)]
    [InlineData("""
    int a() { return 1; }
    int b() { return 2; }
    int pick(void *f) { f = b; return f(); }
    int main() { void *g = a; return pick(g); }
    """, 2)]
    [InlineData("""
    int foo() { return 1; }
    int main() { void *f = foo; if (f) return 7; return 0; }
    """, 7)]
    [InlineData("""
    int foo() { return 123; }
    int main() { void *f = foo; void *g = f; return g(); }
    """, 123)]
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
    public void Function_Pointers(string program, ulong res) => RunCompile(program, res);
}