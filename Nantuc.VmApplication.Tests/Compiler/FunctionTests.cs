using Xunit.Abstractions;

namespace Nantuc.Tests.Compiler;

public class FunctionTests(ITestOutputHelper outPut) : CompileTestBase(outPut)
{
    [Theory]
    [InlineData("""
        int y[] = { 10, 20 };
        int add(int a, int b) { return a + b; }
        int main() { return add(y[0], y[1]); } 
        """, 30)]
    [InlineData("""
        int y[] = { 5, 7 };
        int id(int x) { return x; }
        int main() { if (y[0] < id(y[1])) {return 1;} return 0; }
        """, 1)]
    [InlineData("""
        int main(){ return foo(42); }
        int foo(int c){ return c; }
        """, 42)]
    [InlineData("""
    // 1. Вложенные вызовы в аргументах
    int id(int x) { return x; }
    int add(int a, int b) { return a + b; }
    int main() { return add(id(10), id(20)); }   // 30
    """, 30)]
    [InlineData("""
    // 2. 3 compound аргумента
    int s3(int a, int b, int c) { return a + b + c; }
    int y[] = { 1, 2, 3 };
    int main() { return s3(y[0], y[1], y[2]); }   // 6
    """, 6)]
    [InlineData("""
    // 3. 5 аргументов (4 в регистрах + 1 через pseudo-global)
    int s5(int a, int b, int c, int d, int e) { return a+b+c+d+e; }
    int main() { return s5(1, 2, 3, 4, 5); }   // 15
    """, 15)]
    [InlineData("""
    // 4. 5 compound аргументов (проверяет и pseudo-globals, и сохранение 0..3)
    int s5(int a, int b, int c, int d, int e) { return a+b+c+d+e; }
    int id(int x) { return x; }
    int main() { return s5(id(1), id(2), id(3), id(4), id(5)); }   // 15
    """, 15)]
    [InlineData("""
    // Рекурсия
    int fact(int n) { if (n <= 1) { return 1; } return n * fact(n - 1); }
    int main() { return fact(5); }   // 120
    """, 120)]
    [InlineData("""
    int fib(int n) {
        if (n <= 1) return n;
        return fib(n-1) + fib(n-2);
    }
    int main() { return fib(7); }   // 13
    """, 13)]
    [InlineData("""
    int s4(int a, int b, int c, int d) { return a + b + c + d; }
    int main() { int i = 3, j = 4; return s4(1, 2, 3, i + j); }   // 1+2+3+7 = 13
    """, 13)]
    [InlineData("""
        int add(int a, int b) { return a + b; }
        int main() { int x = 1; int y = 2; return add((x, y), 0); }   // (x,y) = comma → y=2; add(2, 0) = 2
        """, 2)]
    public void Function_Calls(string program, ulong res) => RunCompile(program, res);
}