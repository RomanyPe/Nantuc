using Xunit.Abstractions;

namespace Nantuc.Tests.Compiler;

public class ControlFlowTests(ITestOutputHelper outPut) : CompileTestBase(outPut)
{
    [Theory]
    [InlineData("""
    int main() {
        int i = 0;
        int sum = 0;
        while (i < 5) { sum = sum + i; i = i + 1; }
        return sum;   // 0+1+2+3+4 = 10
    }
    """, 10)]
    [InlineData("""
    int main() {
        int sum = 0;
        for (int i = 1; i <= 5; i = i + 1) { sum = sum + i; }
        return sum;   // 15
    }
    """, 15)]
    [InlineData("""
    int main() {
        int sum = 0;
        for (int i = 1; i <= 5; i = i + 1) sum = sum + i;
        return sum;   // 15
    }
    """, 15)]
    [InlineData("""
    int main() { int sum = 0; for (int i = 1; i <= 5; i = i + 1) sum = sum + i; return sum; }
    """, 15)]
    [InlineData("""
    int main() { if (1) return 1; else return 0; }
    """, 1)]
    [InlineData("""
    int main() { int i = 0; for (; i < 3; i = i + 1) { } return i; }
    """, 3)]
    [InlineData("""
    int main() {
        int sum = 0;
        for (int i = 0, j = 10; i < 3; i = i + 1) sum = sum + i + j;
        return sum;   // (0+10) + (1+10) + (2+10) = 33
    }
    """, 33)]
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
    [InlineData("""
    int main() {
        int i = 0, s = 0;
        do { s = s + i; i = i + 1; } while (i < 5);
        return s;   // 0+1+2+3+4 = 10
    }
    """, 10)]
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
    [InlineData("""
    int classify(int x) { if (x > 0) return 1; else return -1; }
    int main() { return classify(5) + classify(-3); }   // 1 + (-1) = 0
    """, 0)]
    [InlineData("""
    int f(int x) {
        if (x > 10) return 100;
        else if (x > 0) return 10;
        else return 0;
    }
    int main() { return f(50) + f(5) + f(-5); }   // 100 + 10 + 0 = 110
    """, 110)]
    [InlineData("""
    int classify(int x) { if (x > 0) { return 1; } else { return -1;} }
    int main() { return classify(5) + classify(-3) + classify(0); }   // 1 + (-1) + (-1) = -1
    """, unchecked((ulong)-1))]
    public void Control_Flow(string program, ulong res) => RunCompile(program, res);
}