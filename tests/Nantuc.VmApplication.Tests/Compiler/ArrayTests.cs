using Xunit.Abstractions;

namespace Nantuc.Tests.Compiler;

public class ArrayTests(ITestOutputHelper outPut) : CompileTestBase(outPut)
{
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
        // Store с compound-индексом
        int y[] = { 0, 1, 2, 3 };
        int i[] = { 1, 2 };
        int main() { y[i[0] + i[1]] = 42; return y[3]; }   // 42
        """, 42)]
    // ---- a[i] = v как выражение ----
    [InlineData("""
    int main() {
        int a[3] = { 10, 20, 30 };
        int x = (a[1] = 25) + 5;
        return x + a[1];   // 30 + 25 = 55
    }
    """, 55)]

    // ---- a[i] op= v как выражение ----
    [InlineData("""
    int main() {
        int a[3] = { 10, 20, 30 };
        int x = (a[1] += 5) * 2;
        return x + a[1];   // 50 + 25 = 75
    }
    """, 75)]

    // ---- Регрессия: до фикса (a[1] = v) писал в a[0] (тихая потеря) ----
    [InlineData("""
    int main() {
        int a[3] = { 1, 2, 3 };
        int x = (a[1] = 5) + 1;
        return a[0] + a[1] + a[2] + x;   // 1 + 5 + 3 + 6 = 15
    }
    """, 15)]

    // ---- Compound с неконстантным индексом ----
    [InlineData("""
    int main() {
        int a[4] = { 1, 2, 3, 4 };
        int i = 1, j = 1;
        int x = (a[i + j] -= 1) * 3;
        return x + a[2];   // (3-1)*3 + 2 = 6 + 2 = 8
    }
    """, 8)]

    // ---- Вложенный: y[i] = (z = value) ----
    [InlineData("""
    int y[3] = { 0, 0, 0 };
    int main() {
        int z = 0;
        int x = (y[1] = (z = 42)) + 1;
        return x + y[1] + z;   // 43 + 42 + 42 = 127
    }
    """, 127)]
    public void Array_Operations(string program, ulong res) => RunCompile(program, res);
}