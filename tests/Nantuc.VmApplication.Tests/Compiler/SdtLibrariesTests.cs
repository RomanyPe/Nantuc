using Nantuc.VmApplication.Extensions.StdLibraries;
using Xunit.Abstractions;

namespace Nantuc.Tests.Compiler;

public class SdtLibrariesTests(ITestOutputHelper outPut) : CompileTestBase(outPut)
{
    [Theory]
    [InlineData(
        """
        #include "alloc.minic"

        int main() {
            ulong a = alloc(16);
            ulong b = alloc(16);
            if (a == 0 || b == 0) return 1;
            if (a == b)           return 2;

            Store64(a, 111);
            Store64(b, 222);
            if (Load64(a) != 111) return 3;
            if (Load64(b) != 222) return 4;

            free(a);
            ulong c = alloc(16);
            if (c != a) return 5;          // free-list вернул тот же блок
            Store64(c, 333);
            if (Load64(c) != 333) return 6;
            if (Load64(b) != 222) return 7; // сосед не тронут

            free(b);
            free(c);

            // bump после освобождений + split
            ulong s = 0;
            ulong i = 0;
            while (i < 4) {
                ulong p = alloc(8);
                Store64(p, i + 1);
                s = s + Load64(p);
                i = i + 1;
            }
            return s;   // 10
        }
        """, 10)]
    public void AllocatorTest(string content, ulong value) 
        => RunCompile_WithLibs(content, value, 
            [("alloc.minic", Libraries.alloc_minic),
            ("heap.nanasm", Libraries.heap_nanasm)]);
}
