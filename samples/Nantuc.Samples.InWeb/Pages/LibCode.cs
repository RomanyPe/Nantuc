namespace Nantuc.Samples.InWeb.Pages;

public static class LibCode
{
    public const string monitor_nanasm =
        """
        func_MonClear:
            LDI r1, 0x13
            LDI r0, 1
            OUT r0, r1
            LDI r0, 4
            OUT r0, r1
            RET

        func_MonFlush:
            LDI r1, 0x13
            LDI r0, 3
            OUT r0, r1
            RET

        func_MonPlot:
            PUSH r3
            LDI  r3, 0x10
            OUT r0, r3
            LDI  r3, 0x11
            OUT r1, r3
            LDI  r3, 0x12
            OUT r2, r3
            POP  r3
            RET
        """;

    public const string commander_nanasm =
        """
        func_CmdWaitTick:
            LDI r1, 0x21
        cmd_wait_loop:
            IN  r0, r1
            LDI r2, 0
            CMP r0, r2
            JZ  cmd_wait_loop
            RET

        func_CmdReadInput:
            LDI r1, 0x20
            IN  r0, r1
            RET

        func_CmdYield:
            LDI r1, 0x20
            LDI r0, 1
            OUT r0, r1
            RET

        func_CmdGameOver:
            LDI r1, 0x22
            LDI r0, 1
            OUT r0, r1
            RET

        func_CmdSetScore:
            LDI r1, 0x23
            OUT r0, r1
            RET
        """;

    public const string snake_minic =
        """
        #include "monitor.nanasm"
        #include "commander.nanasm"

        extern void MonClear();
        extern void MonFlush();
        extern void MonPlot(int x, int y, int v);
        extern void CmdWaitTick();
        extern int  CmdReadInput();
        extern void CmdYield();
        extern void CmdGameOver();
        extern void CmdSetScore(int s);

        int sx[256];
        int sy[256];
        int slen;
        int dir;
        int foodX;
        int foodY;
        int rngState;

        int rng() {
            rngState = rngState * 1103515245 + 12345;
            return (rngState >> 16) & 0x7FFF;
        }

        int occupied(int x, int y) {
            int i = 0;
            while (i < slen) {
                if (sx[i] == x && sy[i] == y) return 1;
                i = i + 1;
            }
            return 0;
        }

        void spawnFood() {
            while (1) {
                int x = rng() % 32;
                int y = rng() % 16;
                if (!occupied(x, y)) {
                    foodX = x;
                    foodY = y;
                    return;
                }
            }
        }

        void resetGame() {
            slen = 3;
            dir  = 4;
            sx[0] = 16; sy[0] = 8;
            sx[1] = 15; sy[1] = 8;
            sx[2] = 14; sy[2] = 8;
            rngState = 12345;
            spawnFood();
            CmdSetScore(0);
        }

        void render() {
            MonClear();
            MonPlot(foodX, foodY, 1);
            int i = 0;
            while (i < slen) {
                MonPlot(sx[i], sy[i], 1);
                i = i + 1;
            }
            MonFlush();
        }

        int tick() {
            int k = CmdReadInput();
            if (k == 1 && dir != 2) dir = 1;
            if (k == 2 && dir != 1) dir = 2;
            if (k == 3 && dir != 4) dir = 3;
            if (k == 4 && dir != 3) dir = 4;

            int nx = sx[0];
            int ny = sy[0];
            if (dir == 1) ny = ny - 1;
            if (dir == 2) ny = ny + 1;
            if (dir == 3) nx = nx - 1;
            if (dir == 4) nx = nx + 1;

            // проход сквозь стенки
            if (nx < 0)  nx = 31;
            if (nx > 31) nx = 0;
            if (ny < 0)  ny = 15;
            if (ny > 15) ny = 0;

            int grows = 0;
            if (nx == foodX && ny == foodY) grows = 1;

            int checkEnd = slen - 1;
            if (grows) checkEnd = slen;

            int i = 0;
            while (i < checkEnd) {
                if (sx[i] == nx && sy[i] == ny) {
                    // Смерть: сдвигаем голову на место аварии (для отрисовки),
                    // тело не сдвигаем — на кадре видно наложение
                    sx[0] = nx;
                    sy[0] = ny;
                    return 1;
                }
                i = i + 1;
            }

            int moveEnd = slen - 1;
            if (grows) moveEnd = slen;

            i = moveEnd;
            while (i > 0) {
                sx[i] = sx[i - 1];
                sy[i] = sy[i - 1];
                i = i - 1;
            }
            sx[0] = nx;
            sy[0] = ny;

            if (grows) {
                slen = slen + 1;
                CmdSetScore(slen - 3);
                spawnFood();
            }
            return 0;
        }

        void main() {
            resetGame();
            render();
            CmdYield();

            while (1) {
                CmdWaitTick();
                if (tick()) {
                    render();          // кадр аварии
                    CmdGameOver();
                    CmdYield();
                    resetGame();       // готовимся к новой игре
                } else {
                    render();
                    CmdYield();
                }
            }
        }
        """;
}