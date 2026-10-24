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
        Assert.True(deviceCtx.TryFastLoadProgram(comp.Program!, 0, out var err), err);

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
}