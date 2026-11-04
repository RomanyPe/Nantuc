using Xunit.Abstractions;

namespace Nantuc.Tests.Compiler;

public class OperatorTests(ITestOutputHelper outPut) : CompileTestBase(outPut)
{
    [Theory]
    // Побитовые операции
    [InlineData("""
    int main() {
        int c = 0xABCD;
        return (c & 0x00FF) + (c >> 8);   // 0xCD + 0xAB = 205 + 171 = 376
    }
    """, 376)]
    [InlineData("""
    int main() {
        int x = 5 << 3;
        int y = 64 >> 2;
        return x + y;   // 40 + 16 = 56
    }
    """, 56)]
    [InlineData("""
    int main() { int x = 0xF0; x |= 0x0F; x &= 0x3C; return x; }   // (0xF0|0x0F)&0x3C = 0xFF & 0x3C = 0x3C = 60
    """, 60)]
    // Логические
    [InlineData("""
    int main() { int x = 0; return !x; }       // !0 = 1
    """, 1)]
    [InlineData("""
    int main() { int x = 5; return !x; }       // !5 = 0
    """, 0)]
    [InlineData("""
    int main() { int a = 1; int b = 1; return a && b; }   // 1
    """, 1)]
    [InlineData("""
    int main() { int a = 1; int b = 0; return a && b; }   // 0
    """, 0)]
    [InlineData("""
    int main() { int a = 0; int b = 0; return a || b; }   // 0
    """, 0)]
    [InlineData("""
    int main() { int a = 0; int b = 7; return a || b; }   // 1
    """, 1)]
    // Унарные
    [InlineData("""
    int main() { return -5; }       // -5 = 0xFFFF...FFFB
    """, unchecked((ulong)-5))]
    [InlineData("""
    int main() { return ~0; }       // -1
    """, unchecked((ulong)-1))]
    // Сравнения
    [InlineData("""
    int main() {
        int a = 5; int b = 3;
        int ok = (a > b) + (a >= b) + (a != b) + (b < a) + (b <= a) + (a == 5);
        return ok;   // 6
    }
    """, 6)]
    // Математика
    [InlineData("""
    int main() { return 17 % 5 + 20 % 3; }   // 2 + 2 = 4
    """, 4)]
    [InlineData("""
    int main() { int a = -22, b = 2999, c = 0; return a + b + c;   // -22 + 2999 = 2977
    }
    """, 2977)]
    // Compound
    [InlineData("""
    int main() { int x = 10; x += 5; x -= 3; x *= 2; return x; }   // (10+5-3)*2 = 24
    """, 24)]
    [InlineData("""
    int main() { int a[1] = { 10 }; a[0] += 5; return a[0]; }   // 15
    """, 15)]
    [InlineData("""
    int main() { int a[1] = { 5 }; int x = a[0]++; return x + a[0]; }   // 5 + 6 = 11
    """, 11)]
    [InlineData("""
    int main() { int a[1] = { 5 }; int x = ++a[0]; return x + a[0]; }   // 6 + 6 = 12
    """, 12)]
    [InlineData("""
    int main() { int a[1] = { 5 }; a[0]++; return a[0]; }   // 6
    """, 6)]
    [InlineData("""
    int main() { int a[1] = { 5 }; ++a[0]; return a[0]; }   // 6
    """, 6)]
    [InlineData("""
    int main() { int a[1] = { 5 }; int x = 100 + a[0]++; return x + a[0]; }   // (100+5) + 6 = 111
    """, 111)]
    [InlineData("""
    int main() { int sum = 0; int i = 0; while (i < 3) { sum = sum + i; i++; } return sum; }   // 0+1+2 = 3
    """, 3)]
    [InlineData("int main() { int s = 0; for (int i = 0; i < 5; i++) s = s + i; return s; }", 10)]
    [InlineData("int main() { int n = 5; int s = 0; while (n--) s = s + n; return s; }", 10)]
    [InlineData("""
    int main() { int x = 0, y = 0; x = y = 5; return x + y; }   // 10
    """, 10)]
    // Sizeof
    [InlineData("int main() { return sizeof(int); }", 4)]
    [InlineData("int main() { return sizeof(char); }", 2)]
    [InlineData("int main() { return sizeof(byte); }", 1)]
    [InlineData("int main() { return sizeof(ushort); }", 2)]
    [InlineData("int main() { return sizeof(ulong); }", 8)]
    [InlineData("int main() { return sizeof(int*); }", 8)]
    [InlineData("struct P { int x; int y; }; int main() { return sizeof(P); }", 8)]
    [InlineData("struct P { int x; int y; }; int main() { return sizeof(struct P); }", 8)]
    [InlineData("int main() { int x; return sizeof(x); }", 4)]
    [InlineData("int main() { ulong x; return sizeof(x); }", 8)]
    [InlineData("int main() { int arr[10]; return sizeof(arr); }", 40)]
    [InlineData("int main() { int *p; return sizeof(p); }", 8)]
    [InlineData("int main() { int a[1]; int *p = a; return sizeof(*p); }", 4)]
    [InlineData("int main() { return sizeof(int) * 2; }", 8)]
    [InlineData("struct P { int x; byte b; }; int main() { struct P p; return sizeof(p.x) + sizeof(p.b); }", 5)]
    public void Operators_And_Sizeof(string program, ulong res) => RunCompile(program, res);
}