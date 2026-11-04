using Xunit.Abstractions;

namespace Nantuc.Tests.Compiler;

public class StructTests(ITestOutputHelper outPut) : CompileTestBase(outPut)
{
    [Theory]
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
    [InlineData(
        """
        // Store через member с compound value
        struct P { int x; };
        struct P p;
        int main() { p.x = 5 * (2 + 3); return p.x; }      // 25
        """, 25)]
    [InlineData("""
        struct P { int x; };
        int main() { struct P p; p.x = 10; p.x *= 3; return p.x; }   // 30
        """, 30)]
    [InlineData("""
        struct P { int x; };
        int main() { struct P p; p.x = 5; p.x++; return p.x; }   // 6
        """, 6)]
    [InlineData("struct P { int x; }; int main() { struct P p; p.x = 10; int y = p.x++; return y + p.x; }", 21)]
    public void Struct_Operations(string program, ulong res) => RunCompile(program, res);
}
