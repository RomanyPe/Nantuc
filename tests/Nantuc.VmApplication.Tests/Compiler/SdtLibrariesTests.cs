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

    [Theory]
    [InlineData(
        """
        #include "imath.minic"

        int main() {
            int a = imin(5, 10);         // 5
            int b = imax(5, 10);         // 10
            int c = iabs(-42);           // 42
            int d = igcd(48, 18);        // 6
            int e = ipow(2, 10);         // 1024
            ulong f = uisqrt(144);       // 12
            int g = idiv_floor(-5, 2);   // -3
            int h = idiv_ceil(5, 2);     // 3
            ulong i = udiv_round_up(10, 3); // 4
            
            // Возвращаем сумму выборочных результатов: 1024 + 12 + 4 = 1040
            return e + f + i; 
        }
        """, 1040)]
    public void MathTest(string content, ulong value)
        => RunCompile_WithLibs(content, value,
            [("imath.minic", Libraries.imath_minic)]);

    [Theory]
    [InlineData(
        """
        #include "bitops.minic"

        int main() {
            ulong x = 0xAAAA; // 43690 (в бинарном виде 1010101010101010)
            int p = upopcount(x); // 8
            
            int clz = uclz(1);    // 63
            int ctz = uctz(8);    // 3
            
            ulong r1 = rotl64(1, 2);  // 4
            ulong r2 = rotr64(8, 2);  // 2
            
            // Формируем 0x8000000000000000 через сдвиг, чтобы избежать проблем с парсингом литералов
            ulong high_bit = 1;
            high_bit = high_bit << 63;
            ulong rev = urev64(high_bit); // 1
            
            ulong np2 = unext_pow2(17);   // 32
            
            // Сумма: 8 + 63 + 3 + 4 + 2 + 1 + 32 = 113
            return p + clz + ctz + r1 + r2 + rev + np2;
        }
        """, 113)]
    public void BitOpsTest(string content, ulong value)
        => RunCompile_WithLibs(content, value,
            [("bitops.minic", Libraries.bitops_minic)]);

    [Theory]
    // ============ add_ovf_i32 ============
    [InlineData(
    """
    #include "ovf.minic"
    int main() {
        int out;
        // 2_000_000_000 + 2_000_000_000 переполняет int32
        int r = add_ovf_i32(2000000000, 2000000000, &out);
        return r; // Ожидаем 1 (переполнение)
    }
    """, 1)]

    [InlineData(
    """
    #include "ovf.minic"
    int main() {
        int out;
        int r = add_ovf_i32(10, 20, &out);
        if (r == 0) {
            return out;
        } else {
            return -1;
        }
    }
    """, 30)]

    // ============ sub_ovf_i32 ============
    [InlineData(
    """
    #include "ovf.minic"
    int main() {
        int out;
        // -2_000_000_000 - 2_000_000_000 переполняет int32
        int r = sub_ovf_i32(-2000000000, 2000000000, &out);
        return r; // Ожидаем 1 (переполнение)
    }
    """, 1)]

    [InlineData(
    """
    #include "ovf.minic"
    int main() {
        int out;
        int r = sub_ovf_i32(50, 20, &out);
        if (r == 0) {
            return out;
        } else {
            return -1;
        }
    }
    """, 30)]

    // ============ mul_ovf_i32 ============
    [InlineData(
    """
    #include "ovf.minic"
    int main() {
        int out;
        // 100_000 * 100_000 = 10_000_000_000 (переполнение int32)
        int r = mul_ovf_i32(100000, 100000, &out);
        return r; // Ожидаем 1 (переполнение)
    }
    """, 1)]

    [InlineData(
    """
    #include "ovf.minic"
    int main() {
        int out;
        // INT_MIN * -1 переполняет int32 (специальная проверка из библиотеки)
        int r = mul_ovf_i32(-1, -2147483647 - 1, &out);
        return r; // Ожидаем 1 (переполнение)
    }
    """, 1)]

    [InlineData(
    """
    #include "ovf.minic"
    int main() {
        int out;
        int r = mul_ovf_i32(15, 2, &out);
        if (r == 0) {
            return out;
        } else {
            return -1;
        }
    }
    """, 30)]

    // ============ add_ovf_u64 ============
    [InlineData(
    """
    #include "ovf.minic"
    int main() {
        ulong max_u64 = 0;
        max_u64 = max_u64 - 1; // Получаем 0xFFFFFFFFFFFFFFFF
        ulong out;
        int r = add_ovf_u64(max_u64, 1, &out);
        return r; // Ожидаем 1 (перенос)
    }
    """, 1)]

    [InlineData(
    """
    #include "ovf.minic"
    int main() {
        ulong out;
        int r = add_ovf_u64(10, 20, &out);
        if (r == 0) {
            return (int)out;
        } else {
            return -1;
        }
    }
    """, 30)]

    // ============ sub_ovf_u64 ============
    [InlineData(
    """
    #include "ovf.minic"
    int main() {
        ulong out;
        int r = sub_ovf_u64(0, 1, &out);
        return r; // Ожидаем 1 (заём)
    }
    """, 1)]

    [InlineData(
    """
    #include "ovf.minic"
    int main() {
        ulong out;
        int r = sub_ovf_u64(50, 20, &out);
        if (r == 0) {
            return (int)out;
        } else {
            return -1;
        }
    }
    """, 30)]

    // ============ mul_ovf_u64 ============
    [InlineData(
    """
    #include "ovf.minic"
    int main() {
        ulong out;
        ulong max_u64 = 0;
        max_u64 = max_u64 - 1; // Получаем 0xFFFFFFFFFFFFFFFF
        int r = mul_ovf_u64(max_u64, 2, &out);
        return r; // Ожидаем 1 (переполнение)
    }
    """, 1)]

    [InlineData(
    """
    #include "ovf.minic"
    int main() {
        ulong out;
        int r = mul_ovf_u64(6, 5, &out);
        if (r == 0) {
            return (int)out;
        } else {
            return -1;
        }
    }
    """, 30)]

    [InlineData(
        """
        #include "ovf.minic"

        int main() {
            int out1;
            // 2_000_000_000 + 2_000_000_000 переполняет int32
            int r1 = add_ovf_i32(2000000000, 2000000000, &out1); // 1
            
            ulong max_u64 = 0;
            max_u64 = max_u64 - 1; // Получаем 0xFFFFFFFFFFFFFFFF
            ulong out2;
            int r2 = add_ovf_u64(max_u64, 1, &out2); // 1 (перенос)
            
            int out3;
            // 100_000 * 100_000 = 10_000_000_000 (переполнение int32)
            int r3 = mul_ovf_i32(100000, 100000, &out3); // 1
            
            ulong out4;
            int r4 = sub_ovf_u64(0, 1, &out4); // 1 (заём)
            
            int out5;
            int r5 = add_ovf_i32(10, 20, &out5); // 0 (нет переполнения), out5 = 30
            
            // Сумма: 1 + 1 + 1 + 1 + 0 + 30 = 34
            return r1 + r2 + r3 + r4 + r5 + out5;
        }
        """, 34)]
    public void OverflowTest(string content, ulong value)
        => RunCompile_WithLibs(content, value,
            [("ovf.minic", Libraries.ovf_minic)]);

   
}
