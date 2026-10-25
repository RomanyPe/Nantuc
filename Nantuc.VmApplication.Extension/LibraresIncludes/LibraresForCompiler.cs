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
        // First-fit аллокатор с минимальным использованием callee-saved регистров.
        //
        // ВАЖНО: обходной путь для бага кодогенерации MiniC — если функция
        // держит 3-4 переменные в r5..r8, компилятор генерирует несимметричный
        // push/pop и портит r5/r6 у вызывающего. Поэтому всё, что живёт через
        // вызовы функций, храним в глобалах, а в регистрах — максимум 1–2.
        //
        // Формат блока (заголовок 16 байт, выравнивание 8):
        //   +0..7  : размер блока в байтах включая заголовок (кратен 8)
        //   +8..15 : next — указатель на следующий свободный блок
        //   +16..  : пользовательские данные
        // =====================================================================

        ulong heapFreeList = 0;   // голова free-list
        ulong heapPrev     = 0;   // prev для поиска в free-list
        ulong heapNeed     = 0;   // требуемый размер в alloc
        ulong heapResult   = 0;   // результат allocSearch
        ulong heapHP       = 0;   // временное хранение rHP в alloc

        // Tail-recursive поиск в free-list. В callee-saved — только `curr`
        // (плюс `next` на коротком участке до Store64). Максимум 2 → как в free.
        ulong allocSearch(ulong curr)
        {
            if (curr == 0) return 0;

            if (Load64(curr) >= heapNeed)
            {
                ulong next = Load64(curr + 8);
                if (heapPrev == 0) heapFreeList = next;
                else               Store64(heapPrev + 8, next);
                Store64(curr + 8, 0);
                return curr + 16;
            }

            heapPrev = curr;
            return allocSearch(Load64(curr + 8));
        }

        ulong alloc(ulong size)
        {
            if (size == 0) size = 8;
            heapNeed = ((size + 7) / 8) * 8 + 16;   // size умирает здесь
            heapPrev = 0;

            // --- 1. free-list ---
            heapResult = allocSearch(heapFreeList);
            if (heapResult != 0) return heapResult;

            // --- 2. bump ---
            heapHP = HeapGet();
            if (heapHP == 0) heapHP = 0x100000;

            if (heapHP + heapNeed > GetSP()) return 0;

            HeapSet(heapHP + heapNeed);
            Store64(heapHP,      heapNeed);
            Store64(heapHP + 8,  0);
            return heapHP + 16;
        }

        void free(ulong ptr)
        {
            if (ptr == 0) return;
            ulong block = ptr - 16;
            Store64(block + 8, heapFreeList);
            heapFreeList = block;
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

    void write8(byte c) {
        Out(c, portWrite);
    }

    void flush() {
        Out(1, portFlush);
    }
    """;
}
