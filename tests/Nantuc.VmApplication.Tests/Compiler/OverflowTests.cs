using Xunit.Abstractions;

namespace Nantuc.Tests.Compiler;

public class OverflowTests(ITestOutputHelper outPut) : CompileTestBase(outPut)
{
    [Theory]
    [InlineData("int main() { byte b = 250; b = b + 10; return b; }", 4)]                                    // 260 % 256
    [InlineData("int main() { byte b = 250; b += 10; return b; }", 4)]
    [InlineData("int main() { byte b = 0; b--; return b; }", 255)]                                          // wrap
    [InlineData("int main() { byte b = 0; int x = b - 1; return x; }", unchecked((ulong)-1))]               // int result -1
    [InlineData("int main() { short s = 30000; s += 10000; return s; }", unchecked((ulong)-25536))]
    [InlineData("int main() { ushort u = 65535; u += 1; return u; }", 0)]
    [InlineData("int main() { int i = 0x7FFFFFFF; i += 1; return i; }", unchecked((ulong)(int)0x80000000))]
    [InlineData("byte f() { return 300; } int main() { return f(); }", 44)]
    [InlineData("byte f(byte b) { return b; } int main() { return f(300); }", 44)]
    [InlineData("byte b[2] = { 250 }; int main() { b[0] += 10; return b[0]; }", 4)]
    [InlineData("int main() { byte b = 200; int *p = 0; return (b = 300); }", 44)]
    public void Overflow(string program, ulong res) => RunCompile(program, res);
}
