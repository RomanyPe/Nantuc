namespace Nantuc.VmApplication.Extensions.StdLibraries;

public static class Libraries
{
    public const string heap_nanasm =
        """
        func_HeapGet:
            MOV r0, rHP;
            RET;

        func_HeapSet:
            MOV rHP, r0;
            RET;

        func_GetSP:
            MOV r0, rSP;
            RET;

        func_Load64:  LOAD_IND.64 r0, r0;  RET;
        func_Store64: STORE_IND.64 r1, r0; RET;

        func_Load32:  LOAD_IND.32 r0, r0;  RET;
        func_Store32: STORE_IND.32 r1, r0; RET;

        func_Load16:  LOAD_IND.16 r0, r0;  RET;
        func_Store16: STORE_IND.16 r1, r0; RET;

        func_Load8:   LOAD_IND.8 r0, r0;   RET;
        func_Store8:  STORE_IND.8 r1, r0;  RET;
        """;

    public const string alloc_minic =
        """
        #include "heap.nanasm"

        extern ulong HeapGet();
        extern void  HeapSet(ulong ptr);
        extern ulong GetSP();
        extern ulong Load64(ulong addr);
        extern void  Store64(ulong addr, ulong value);

        // =====================================================================
        // First-fit аллокатор с split'ом и coalescing'ом.
        //
        // Раскладка блока (всё выровнено на 8, size кратен 8):
        //   +0..7    : size | FREE_BIT(бит0)        — заголовок
        //   +8..15   : next_free (только для free)  — либо начало payload
        //   +16..sz-8: продолжение payload / unused
        //   +sz-8..sz: size | FREE_BIT(бит0)        — футер (для слияния влево)
        //
        //   payload у пользователя начинается с +8.
        //   Минимальный блок: 24 байта (8 hdr + 8 payload + 8 ftr).
        // =====================================================================

        ulong heapFreeList = 0;   // голова free-list
        ulong heapCur;        // вершина bump'а (== rHP)
        ulong heapStart;      // первый блок (для остановки coalesce влево)

        const ulong FREE_BIT  = 1;
        const ulong MIN_BLOCK = 24;

        ulong blkSize(ulong b) { return Load64(b) & ~FREE_BIT; }
        ulong blkFree(ulong b) { return Load64(b) & FREE_BIT; }

        void blkMark(ulong b, ulong size, int isFree)
        {
            ulong v = size;
            if (isFree) v = v | FREE_BIT;
            Store64(b, v);              // header
            Store64(b + size - 8, v);   // footer
        }

        // Удаляет блок из free-list (поиск с головы).
        void blkRemove(ulong b)
        {
            ulong prev = 0;
            ulong curr = heapFreeList;
            while (curr != 0 && curr != b)
            {
                prev = curr;
                curr = Load64(curr + 8);
            }
            if (curr == 0) return;

            ulong next = Load64(b + 8);
            if (prev == 0) heapFreeList = next;
            else           Store64(prev + 8, next);
            Store64(b + 8, 0);
        }

        void blkPush(ulong b)
        {
            Store64(b + 8, heapFreeList);
            heapFreeList = b;
        }

        // Слить b с правым соседом, если тот свободен.
        ulong mergeRight(ulong b)
        {
            ulong sz = blkSize(b);
            ulong nb = b + sz;
            if (nb >= heapCur)    return b;   // дальше bump не продвинулся
            if (blkFree(nb) == 0) return b;   // правый занят

            blkRemove(nb);
            blkMark(b, sz + blkSize(nb), 1);
            return b;
        }

        // Слить b с левым соседом, если тот свободен.
        ulong mergeLeft(ulong b)
        {
            if (b <= heapStart) return b;

            ulong pf  = Load64(b - 8);          // футер левого соседа
            ulong psz = pf & ~FREE_BIT;
            if ((pf & FREE_BIT) == 0) return b; // левый занят

            ulong p = b - psz;
            blkRemove(p);
            blkMark(p, psz + blkSize(b), 1);
            return p;
        }

        void heapInit()
        {
            if (heapCur != 0) return;
            ulong hp = HeapGet();
            if (hp == 0) hp = 0x100000;
            hp = (hp + 7) & ~7;
            heapCur   = hp;
            heapStart = hp;
        }

        // глобально, рядом с FREE_BIT и MIN_BLOCK
        ulong HDR_FTR = 16;   // 8 hdr + 8 ftr

        ulong alloc(ulong size)
        {
            if (size == 0) size = 1;

            // payload (кратно 8) + header + footer
            ulong need = ((size + 7) & ~7) + HDR_FTR;
            if (need < MIN_BLOCK) need = MIN_BLOCK;

            heapInit();

            // ---- 1. free-list (first fit) + split ----
            ulong prev = 0;
            ulong curr = heapFreeList;
            while (curr != 0)
            {
                ulong csz  = blkSize(curr);
                ulong next = Load64(curr + 8);   // один раз — до возможной мутации

                if (csz >= need)
                {
                    // отцепляем curr от free-list
                    if (prev == 0) heapFreeList = next;
                    else           Store64(prev + 8, next);
                    Store64(curr + 8, 0);

                    if (csz - need >= MIN_BLOCK)
                    {
                        // split: остаток снова в free-list
                        ulong rest = curr + need;
                        blkMark(rest, csz - need, 1);
                        blkPush(rest);
                        blkMark(curr, need, 0);
                    }
                    else
                    {
                        // блок целиком отдаём пользователю
                        blkMark(curr, csz, 0);
                    }
                    return curr + 8;
                }

                prev = curr;
                curr = next;                      // ← больше не перечитываем Load64
            }

            // ---- 2. bump ----
            ulong sp = GetSP();
            if (heapCur + need > sp) return 0;

            ulong b = heapCur;
            heapCur += need;
            blkMark(b, need, 0);
            HeapSet(heapCur);
            return b + 8;
        }

        void free(ulong ptr)
        {
            if (blkFree(b)) return;
            if (ptr == 0) return;
            ulong b = ptr - 8;
            blkMark(b, blkSize(b), 1);
            b = mergeRight(b);
            b = mergeLeft(b);
            blkPush(b);
        }
        """;
    public const string io_nanasm = """

    func_In:
        IN r1, r0;
        MOV r0, r1;
        RET;

    func_Out:
        OUT r0, r1;
        RET;
    """;

    public const string print_minic =
        """
    #include "io.nanasm"

    extern void Out(byte value, int port);

    int portWrite = 0x10;
    int portFlush = 0x12;

    void print8(char* str) {
        int i = 0;
        while (str[i] != '\0') {
            write8(str[i]);
            i = i + 1;
        }
        flush();
    }

    void print16(char* str) {
        int i = 0;
        while (str[i] != '\0') {
            write16(str[i]);
            i = i + 1;
        }
        flush();
    }
    void write8(byte c) {
        Out(c, portWrite);
    }
    void write16(char c) {
        write8(c);
        write8(c >> 8);
    }

    void flush() {
        Out(1, portFlush);
    }
    """;

    public const string imath_minic =
    """
            // ---- min/max/clamp ----
    int imin(int a, int b) { if (a < b) return a; return b; }
    int imax(int a, int b) { if (a > b) return a; return b; }
    ulong umin(ulong a, ulong b) { if (a < b) return a; return b; }
    ulong umax(ulong a, ulong b) { if (a > b) return a; return b; }

    int iclamp(int x, int lo, int hi) {
        if (x < lo) return lo;
        if (x > hi) return hi;
        return x;
    }

    ulong uclamp(ulong x, ulong lo, ulong hi) {
        if (x < lo) return lo;
        if (x > hi) return hi;
        return x;
    }

    // ---- abs ----
    int iabs(int x) {
        if (x < 0) return -x;
        return x;
    }

    // ---- gcd (Евклид) ----
    int igcd(int a, int b) {
        if (a < 0) a = -a;
        if (b < 0) b = -b;
        while (b != 0) {
            int t = a % b;
            a = b;
            b = t;
        }
        return a;
    }

    ulong ugcd(ulong a, ulong b) {
        while (b != 0) {
            ulong t = a % b;
            a = b;
            b = t;
        }
        return a;
    }

    // ---- целочисленное возведение в степень (exp >= 0) ----
    int ipow(int base, int exp) {
        int r = 1;
        while (exp > 0) {
            if (exp & 1) r = r * base;
            base = base * base;
            exp = exp >> 1;
        }
        return r;
    }

    // ---- целочисленный квадратный корень (Newton) ----
    ulong uisqrt(ulong n) {
        if (n == 0) return 0;
        ulong x = n;
        ulong y = (x >> 1) + 1;
        while (y < x) {
            x = y;
            y = (x + n / x) >> 1;
        }
        return x;
    }

    // ---- округлённое деление ----
    // C99: '/' усекает к нулю, '%' — знак делимого.
    // floor: если знаки разные и r != 0 -> q - 1
    int idiv_floor(int a, int b) {
        int q = a / b;
        int r = a % b;
        if (r != 0) {
            if ((r < 0) != (b < 0)) q = q - 1;
        }
        return q;
    }

    // ceil: если знаки одинаковые и r != 0 -> q + 1
    int idiv_ceil(int a, int b) {
        int q = a / b;
        int r = a % b;
        if (r != 0) {
            if ((r < 0) == (b < 0)) q = q + 1;
        }
        return q;
    }

    // беззнаковое деление с округлением вверх (часто нужно для аллокаторов)
    ulong udiv_round_up(ulong a, ulong b) {
        ulong q = a / b;
        if (a % b != 0) q = q + 1;
        return q;
    }
    """;

    public const string bitops_minic =
        """
                // ---- popcount (Kernighan) ----
        int upopcount(ulong x) {
            int c = 0;
            while (x != 0) {
                x = x & (x - 1);
                c = c + 1;
            }
            return c;
        }

        // ---- count leading zeros (0..64) ----
        int uclz(ulong x) {
            if (x == 0) return 64;
            int n = 0;
            if ((x >> 32) == 0) { n = n + 32; x = x << 32; }
            if ((x >> 48) == 0) { n = n + 16; x = x << 16; }
            if ((x >> 56) == 0) { n = n + 8;  x = x << 8;  }
            if ((x >> 60) == 0) { n = n + 4;  x = x << 4;  }
            if ((x >> 62) == 0) { n = n + 2;  x = x << 2;  }
            if ((x >> 63) == 0) { n = n + 1; }
            return n;
        }

        // ---- count trailing zeros (0..64) ----
        int uctz(ulong x) {
            if (x == 0) return 64;
            int n = 0;
            if ((x & 0xFFFFFFFF) == 0) { n = n + 32; x = x >> 32; }
            if ((x & 0xFFFF) == 0)     { n = n + 16; x = x >> 16; }
            if ((x & 0xFF) == 0)       { n = n + 8;  x = x >> 8;  }
            if ((x & 0xF) == 0)        { n = n + 4;  x = x >> 4;  }
            if ((x & 0x3) == 0)        { n = n + 2;  x = x >> 2;  }
            if ((x & 0x1) == 0)        { n = n + 1; }
            return n;
        }

        // ---- циклические сдвиги (n приводится к 0..63) ----
        ulong rotl64(ulong x, int n) {
            n = n & 63;
            if (n == 0) return x;
            return (x << n) | (x >> (64 - n));
        }

        ulong rotr64(ulong x, int n) {
            n = n & 63;
            if (n == 0) return x;
            return (x >> n) | (x << (64 - n));
        }

        // ---- зеркальное отражение бит ----
        ulong urev64(ulong x) {
            x = ((x & 0x5555555555555555) << 1) | ((x >> 1) & 0x5555555555555555);
            x = ((x & 0x3333333333333333) << 2) | ((x >> 2) & 0x3333333333333333);
            x = ((x & 0x0F0F0F0F0F0F0F0F) << 4) | ((x >> 4) & 0x0F0F0F0F0F0F0F0F);
            x = ((x & 0x00FF00FF00FF00FF) << 8) | ((x >> 8) & 0x00FF00FF00FF00FF);
            x = ((x & 0x0000FFFF0000FFFF) << 16) | ((x >> 16) & 0x0000FFFF0000FFFF);
            x = (x << 32) | (x >> 32);
            return x;
        }

        // ---- следующая степень двойки ----
        // если x уже степень двойки — возвращаем x; 0 означает переполнение
        ulong unext_pow2(ulong x) {
            if (x <= 1) return 1;
            if ((x & (x - 1)) == 0) return x;
            int s = 64 - uclz(x);
            if (s >= 64) return 0;
            return (ulong)1 << s;
        }
        """;
    public const string ovf_minic =
        """
                // ============ Signed 32-bit ============
        // INT_MAX = 2147483647, INT_MIN = -2147483647 - 1

        int add_ovf_i32(int a, int b, int* out) {
            // знаки a и b совпадают, а у результата — нет
            int s = a + b;
            if ((a >= 0) == (b >= 0) && (a >= 0) != (s >= 0)) return 1;
            *out = s;
            return 0;
        }

        int sub_ovf_i32(int a, int b, int* out) {
            int s = a - b;
            if ((a >= 0) != (b >= 0) && (a >= 0) != (s >= 0)) return 1;
            *out = s;
            return 0;
        }

        int mul_ovf_i32(int a, int b, int* out) {
            if (a == 0 || b == 0) { *out = 0; return 0; }
            // ловим INT_MIN * -1 до умножения (иначе UB)
            if (a == -1 && b == -2147483647 - 1) return 1;
            if (b == -1 && a == -2147483647 - 1) return 1;
            int s = a * b;
            if (s / b != a) return 1;
            *out = s;
            return 0;
        }

        // ============ Unsigned 64-bit ============

        int add_ovf_u64(ulong a, ulong b, ulong* out) {
            ulong s = a + b;
            *out = s;
            if (s < a) return 1;          // перенос
            return 0;
        }

        int sub_ovf_u64(ulong a, ulong b, ulong* out) {
            ulong s = a - b;
            *out = s;
            if (a < b) return 1;          // заём
            return 0;
        }

        int mul_ovf_u64(ulong a, ulong b, ulong* out) {
            if (a == 0 || b == 0) { *out = 0; return 0; }
            ulong s = a * b;
            if (s / a != b) return 1;
            *out = s;
            return 0;
        }
        """;
}
