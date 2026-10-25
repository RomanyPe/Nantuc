namespace Nantuc.Samples.Scratch.SnakeGame;

public static class LibCode
{
    public const string monitor_nanasm =
        """
    func_MonClear:
        LDI r1, 0x13;
        LDI r0, 1;
        OUT r0, r1;
        LDI r0, 4;
        OUT r0, r1;
        RET;

    func_MonFlush:
        LDI r1, 0x13;
        LDI r0, 3;
        OUT r0, r1;
        RET;

    func_MonPlot:
        LDI r4, 0x10;
        OUT r0, r4;
        LDI r4, 0x11;
        OUT r1, r4;
        LDI r4, 0x12;
        OUT r2, r4;
        RET;
    """;

    public const string input_nanasm =
        """
        func_ReadKey:
            LDI r1, 0x00;
            IN  r0, r1;
            LDI r3, 0x00;
            CMP r0, r3;
            JZ  no_key;
            MOV r3, r0;
            LDI r0, 0x00;
            LDI r1, 0x00;
            OUT r0, r1;
            MOV r0, r3;
        no_key:
            RET;
        """;
    public const string halt_nanasm =
        """
        func_Halt:
            HALT;
            RET;
        """;
    public const string snake_minic =
        """
        #include "monitor.nanasm"
        #include "halt.nanasm"
        #include "input.nanasm"

        extern void  MonClear();
        extern void  MonFlush();
        extern void  MonPlot(int x, int y, int v);
        extern void  Halt();
        extern ulong ReadKey();

        int sx[256];
        int sy[256];
        int slen;
        int dir;
        int fx;
        int fy;
        int rngState;

        int newX;
        int newY;
        int ti;
        int te;
        int tg;
        int th;
        ulong tk;

        int rng() {
            rngState = rngState * 1103515245 + 12345;
            return (rngState >> 16) & 0x7FFF;
        }

        int occupied(int x, int y) {
            ti = 0;
            while (ti < slen) {
                if (sx[ti] == x && sy[ti] == y) return 1;
                ti = ti + 1;
            }
            return 0;
        }

        void spawnFood() {
            while (1) {
                newX = rng() % 32;
                newY = rng() % 16;
                if (!occupied(newX, newY)) { fx = newX; fy = newY; return; }
            }
        }

        void initGame() {
            slen = 3;
            dir  = 4;
            sx[0] = 16; sy[0] = 8;
            sx[1] = 15; sy[1] = 8;
            sx[2] = 14; sy[2] = 8;
            rngState = 12345;
            spawnFood();
        }

        void drawGame() {
            MonClear();
            MonPlot(fx, fy, 1);
            ti = 0;
            while (ti < slen) {
                MonPlot(sx[ti], sy[ti], 1);
                ti = ti + 1;
            }
            MonFlush();
        }

        void tickGame() {
            tk = ReadKey();
            if (tk == 1 && dir != 2) dir = 1;
            if (tk == 2 && dir != 1) dir = 2;
            if (tk == 3 && dir != 4) dir = 3;
            if (tk == 4 && dir != 3) dir = 4;

            newX = sx[0]; newY = sy[0];
            if (dir == 1) newY = newY - 1;
            if (dir == 2) newY = newY + 1;
            if (dir == 3) newX = newX - 1;
            if (dir == 4) newX = newX + 1;
            if (newX < 0)  newX = 31;
            if (newX > 31) newX = 0;
            if (newY < 0)  newY = 15;
            if (newY > 15) newY = 0;

            tg = 0;
            if (newX == fx && newY == fy) tg = 1;

            te = slen;
            if (tg == 0) te = slen - 1;
            th = 0;
            ti = 0;
            while (ti < te) {
                if (sx[ti] == newX && sy[ti] == newY) { th = 1; break; }
                ti = ti + 1;
            }

            if (th) { initGame(); return; }

            te = slen - 1;
            if (tg) te = slen;
            ti = te;
            while (ti > 0) {
                sx[ti] = sx[ti - 1];
                sy[ti] = sy[ti - 1];
                ti = ti - 1;
            }
            sx[0] = newX; sy[0] = newY;
            if (tg) { slen = slen + 1; spawnFood(); }
        }

        void main() {
            initGame();
            drawGame();
            while (1) {
                Halt();
                tickGame();
                drawGame();
            }
        }
        """;
}