using Xunit.Abstractions;

namespace Nantuc.Tests.Compiler;

public class CastTests(ITestOutputHelper outPut) : CompileTestBase(outPut)
{
    [Theory]
    // ---- Identity / int casts ----
    [InlineData("int main() { return (int)5; }", 5)]
    [InlineData("int main() { int x = 10; return (int)x; }", 10)]
    [InlineData("int main() { int x = 42; int y = (int)x; return y; }", 42)]
    [InlineData("int main() { ulong x = 12345; return (int)x; }", 12345)]

    // ---- Narrowing to byte (unsigned 8-bit) ----
    [InlineData("int main() { return (byte)255; }", 255)]
    [InlineData("int main() { return (byte)256; }", 0)]
    [InlineData("int main() { return (byte)300; }", 44)]              // 300 % 256
    [InlineData("int main() { return (byte)1000; }", 232)]            // 1000 % 256
    [InlineData("int main() { int x = 300; return (byte)x; }", 44)]
    [InlineData("int main() { int x = 1000; byte b = (byte)x; return b; }", 232)]

    // ---- Negative to byte ----
    [InlineData("int main() { return (byte)(-1); }", 255)]
    [InlineData("int main() { return (byte)(-2); }", 254)]
    [InlineData("int main() { return (byte)(-256); }", 0)]
    [InlineData("int main() { int x = -5; return (byte)x; }", 251)]

    // ---- ushort (unsigned 16-bit) ----
    [InlineData("int main() { return (ushort)65535; }", 65535)]
    [InlineData("int main() { return (ushort)65536; }", 0)]
    [InlineData("int main() { return (ushort)70000; }", 4464)]        // 70000 % 65536
    [InlineData("int main() { return (ushort)(-1); }", 65535)]

    // ---- short (signed 16-bit) ----
    [InlineData("int main() { return (short)32767; }", 32767)]
    [InlineData("int main() { return (short)32768; }", unchecked((ulong)-32768))]
    [InlineData("int main() { return (short)40000; }", unchecked((ulong)-25536))]
    [InlineData("int main() { return (short)(-1); }", unchecked((ulong)-1))]

    // ---- Cast in expression ----
    [InlineData("int main() { return (int)5 + (int)7; }", 12)]
    [InlineData("int main() { int x = 300; return (byte)x + 1; }", 45)]
    [InlineData("int main() { int x = 300; return ((byte)x) * 2; }", 88)]
    [InlineData("int main() { byte b = 200; return (int)b + 100; }", 300)]
    [InlineData("int main() { ushort u = 500; return (int)u * 2; }", 1000)]

    // ---- Multi-level cast ----
    [InlineData("int main() { return (int)(byte)300; }", 44)]
    [InlineData("int main() { return (byte)(ushort)70000; }", 112)]   // 70000 -> 0x1170 -> 0x70
    [InlineData("int main() { return (int)(short)40000; }", unchecked((ulong)-25536))]

    // ---- Cast в переменную, затем сравнение (не в condition напрямую) ----
    [InlineData("""
    int main() {
        int x = 300;
        byte b = (byte)x;
        if (b == 44) return 1;
        return 0;
    }
    """, 1)]

    [InlineData("""
    int main() {
        int x = 300;
        byte b = (byte)x;
        if (b != 44) return 1;
        return 0;
    }
    """, 0)]

    // ---- Cast of char ----
    [InlineData("int main() { char c = (char)65; return (int)c; }", 65)]
    [InlineData("int main() { return (char)65; }", 65)]

    // ---- Cast in assignment expression ----
    [InlineData("int main() { int x; byte b = (byte)(x = 300); return b; }", 44)]
    [InlineData("int main() { int x = 300; byte b; return b = (byte)x; }", 44)]

    // ---- Cast from ushort to short (reinterpret) ----
    [InlineData("int main() { ushort u = 65535; return (short)u; }", unchecked((ulong)-1))]
    [InlineData("int main() { ushort u = 1; return (short)u; }", 1)]

    [InlineData("""
    int main() {
        int x = 250;
        int s = 0;
        for (int i = 0; (byte)(x + i) != 0; i = i + 1) s = s + 1;
        return s;
    }
    """, 6)]
    // ---- Обёртка byte в цикле через переменную ----
    [InlineData("""
    int main() {
        int x = 250;
        int s = 0;
        int i = 0;
        while (i < 10) {
            byte b = (byte)(x + i);
            if (b == 0) break;
            s = s + 1;
            i = i + 1;
        }
        return s;   // 6: i=0..5 -> 250..255 != 0, i=6 -> 0
    }
    """, 6)]

    // ---- Pointer casts: только identity / адресная арифметика (без deref) ----
    [InlineData("""
    int main() {
        int a[1] = { 42 };
        int *p = a;
        ulong addr = (ulong)p;
        int *q = (int*)addr;
        return q == p;   // 1
    }
    """, 1)]

    [InlineData("""
    int main() {
        int a[2] = { 10, 20 };
        int *p = a;
        void *v = (void*)p;
        int *q = (int*)v;
        return q == p;   // 1
    }
    """, 1)]

    [InlineData("""
    int main() {
        int a[1] = { 99 };
        int *p = a;
        ulong addr = (ulong)p;
        return addr != 0;   // 1
    }
    """, 1)]

    [InlineData("""
    int main() {
        int a[1] = { 99 };
        int *p = a;
        ulong addr = (ulong)p;
        ulong addr2 = (ulong)a;
        return addr == addr2;   // 1
    }
    """, 1)]

    // ---- Cast указателя на функцию через ulong (без вызова) ----
    [InlineData("""
    int foo() { return 42; }
    int main() {
        void *f = foo;
        ulong addr = (ulong)f;
        void *g = (void*)addr;
        return g == f;   // 1
    }
    """, 1)]

    // ---- Смещение указателя через ulong, затем обратно в int* (identity-сравнение) ----
    [InlineData("""
    int main() {
        int a[2] = { 10, 20 };
        int *p = a;
        ulong addr = (ulong)p;
        ulong addr1 = addr + 4;
        return addr1 == addr + 4;   // 1
    }
    """, 1)]
    public void Casts(string program, ulong res) => RunCompile(program, res);
}