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
    public void Array_Operations(string program, ulong res) => RunCompile(program, res);
}