using Xunit.Abstractions;

namespace Nantuc.Tests.Compiler;

public class ConstCorrectnessTests(ITestOutputHelper outPut) : CompileTestBase(outPut)
{
    [Theory]
    // Позитивные
    [InlineData("const int x = 5; int main() { return x; }", 5)]
    [InlineData("const int G = 42; int main() { return G; }", 42)]
    [InlineData("int main() { const int x = 7; return x * 3; }", 21)]
    [InlineData("int main() { const int arr[3] = { 10, 20, 30 }; return arr[0] + arr[1] + arr[2]; }", 60)]
    [InlineData("int main() { int a = 5; int * const p = &a; *p = 10; return a; }", 10)]
    [InlineData("int main() { int a = 5; int * const p = &a; (*p)++; return a; }", 6)]
    [InlineData("int main() { int a = 5; int * const p = &a; *p += 3; return a; }", 8)]
    [InlineData("int main() { int a = 5; const int *p = &a; return *p; }", 5)]
    [InlineData("int main() { int a[3] = { 10, 20, 30 }; const int *p = a; p = p + 1; return *p; }", 20)]
    [InlineData("int main() { int a[3] = { 10, 20, 30 }; const int *p = a; p++; return *p; }", 20)]
    [InlineData("int main() { int a = 5; const int * const p = &a; return *p; }", 5)]
    [InlineData("int f(const int x) { return x + 1; } int main() { return f(10); }", 11)]
    [InlineData("int f(const int *p) { return *p * 2; } int main() { int a = 21; return f(&a); }", 42)]
    [InlineData("int main() { const int x = 5; return sizeof(x); }", 4)]
    [InlineData("int main() { return sizeof(const int); }", 4)]
    [InlineData("int main() { return sizeof(const int*); }", 8)]
    [InlineData("int f(int x) { x = 100; return x; } const int a = 5; int main() { return f(a); }", 100)]
    public void Const_Positive(string program, ulong res) => RunCompile(program, res);

    [Theory]
    // Негативные (должны упасть при компиляции)
    [InlineData("const int x = 5; int main() { x = 6; return x; }")]
    [InlineData("int main() { const int x = 5; x = 6; return x; }")]
    [InlineData("const int G = 5; int main() { G = 10; return G; }")]
    [InlineData("const int x = 5; int main() { x++; return x; }")]
    [InlineData("const int x = 5; int main() { --x; return x; }")]
    [InlineData("const int x = 5; int main() { x += 1; return x; }")]
    [InlineData("const int x = 5; int main() { x *= 2; return x; }")]
    [InlineData("int main() { const int arr[3] = { 1, 2, 3 }; arr[0] = 5; return 0; }")]
    [InlineData("int main() { const int arr[3] = { 1, 2, 3 }; arr[1]++; return 0; }")]
    [InlineData("int main() { const int arr[3] = { 1, 2, 3 }; arr[2] += 1; return 0; }")]
    [InlineData("int main() { int a = 5; int * const p = &a; p = &a; return 0; }")]
    [InlineData("int main() { int a = 5; int * const p = &a; p++; return 0; }")]
    [InlineData("int main() { int a = 5; int * const p = &a; --p; return 0; }")]
    [InlineData("int main() { int a = 5; const int *p = &a; *p = 10; return 0; }")]
    [InlineData("int main() { int a = 5; const int *p = &a; (*p)++; return 0; }")]
    [InlineData("int main() { int a = 5; const int *p = &a; *p += 1; return 0; }")]
    [InlineData("int main() { int a = 5; const int *p = &a; p[0] = 10; return 0; }")]
    [InlineData("int main() { int a = 5; const int *p = &a; p[0]++; return 0; }")]
    [InlineData("int main() { int a = 5; const int * const p = &a; *p = 10; return 0; }")]
    [InlineData("int main() { int a = 5; const int * const p = &a; p = &a; return 0; }")]
    [InlineData("int main() { int a = 5; const int * const p = &a; p++; return 0; }")]
    [InlineData("int f(const int x) { x = 5; return x; } int main() { return f(10); }")]
    [InlineData("int f(const int x) { x++; return x; } int main() { return f(10); }")]
    [InlineData("int f(const int *p) { *p = 10; return 0; } int main() { int a = 5; return f(&a); }")]
    [InlineData("int f(const int *p) { (*p)++; return 0; } int main() { int a = 5; return f(&a); }")]
    public void Const_Violations(string program) => RunCompile_ConstViolation(program);
}