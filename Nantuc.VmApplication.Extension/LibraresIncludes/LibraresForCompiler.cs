using Nantuc.VmApplication.Extensions.Host;
using Nantuc.VmApplication.Project;

namespace Nantuc.VmApplication.Extensions.LibraresIncludes;

public static class Librare
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

        ulong heapFreeList;   // голова free-list
        ulong heapCur;        // вершина bump'а (== rHP)
        ulong heapStart;      // первый блок (для остановки coalesce влево)

        ulong FREE_BIT  = 1;
        ulong MIN_BLOCK = 24;

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

        ulong alloc(ulong size)
        {
            if (size == 0) size = 1;
            ulong need = ((size + 7) / 8) * 8 + 16;   // payload + hdr + ftr
            if (need < MIN_BLOCK) need = MIN_BLOCK;
            heapInit();

            // ---- 1. free-list (first fit) + split ----
            ulong prev = 0;
            ulong curr = heapFreeList;
            while (curr != 0)
            {
                ulong csz = blkSize(curr);
                if (csz >= need)
                {
                    ulong next = Load64(curr + 8);
                    if (prev == 0) heapFreeList = next;
                    else           Store64(prev + 8, next);
                    Store64(curr + 8, 0);

                    if (csz - need >= MIN_BLOCK)
                    {
                        ulong rest = curr + need;
                        blkMark(rest, csz - need, 1);
                        blkPush(rest);
                        blkMark(curr, need, 0);
                    }
                    else
                    {
                        blkMark(curr, csz, 0);
                    }
                    return curr + 8;                  // payload
                }
                prev = curr;
                curr = Load64(curr + 8);
            }

            // ---- 2. bump ----
            ulong sp = GetSP();
            if (heapCur + need > sp) return 0;

            ulong b   = heapCur;
            heapCur   = heapCur + need;
            blkMark(b, need, 0);
            HeapSet(heapCur);
            return b + 8;
        }

        void free(ulong ptr)
        {
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
}
