using Nantuc.Kernel.Common;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Extensions.Host;
using System.Text;



namespace Nantuc.Samples.Scratch.SnakeGame;

public class SnakeLauncher
{
    public static void LaunchSnake()
    {
        using var host = VmHostFactory.CreateDefault();

        host.Project.SaveFileProject("snake.minic", LibCode.snake_minic);
        host.Project.SaveFileInclude("monitor.nanasm", LibCode.monitor_nanasm);
        host.Project.SaveFileInclude("halt.nanasm", LibCode.halt_nanasm);
        host.Project.SaveFileInclude("input.nanasm", LibCode.input_nanasm);

        var monitor = new MonochromeMonitor(32, 16, RenderToConsole);

        var handle = DeviceSpec.Create(ramSize: RamSize.MB16, name: "SnakeVM");
        int deviceId = host.Emulator.RegisterDevice(handle, sector: 0);   // процессор

        host.Emulator.RegisterDevice(monitor, name: "monitor", sector: 1);

        var ctx = host.Emulator.GetDeviceContext(deviceId)!;
        var portBus = host.Emulator.GetPortBus();

        // -------- компиляция --------
        var res = host.Project.CompileToIL(baseAddress: 0, optimize: false).Build();
        if (!res.Success)
        {
            foreach (var e in res.Errors!) Console.WriteLine(e);
            return;
        }
        if (!ctx.TryFastLoadProgram(res.Program!, 0, out var err))
        {
            Console.WriteLine(err);
            return;
        }

        // -------- ввод с клавиатуры: пишем в порт 0 процессора --------
        bool running = true;

        var inputThread = new Thread(() =>
        {
            while (running)
            {
                if (!Console.KeyAvailable) { Thread.Sleep(15); continue; }

                var k = Console.ReadKey(intercept: true);
                byte code = 0;
                switch (k.Key)
                {
                    case ConsoleKey.UpArrow: code = 1; break;
                    case ConsoleKey.DownArrow: code = 2; break;
                    case ConsoleKey.LeftArrow: code = 3; break;
                    case ConsoleKey.RightArrow: code = 4; break;
                    case ConsoleKey.Q:
                    case ConsoleKey.Escape:
                        running = false;
                        ctx.Stop();
                        return;
                }
                if (code != 0)
                    portBus.WritePort(0, code);   // процессор прочитает через IN из своего буфера
            }
        })
        { IsBackground = true };

        // -------- тикер: будим процессор раз в 200 мс --------
        using var ticker = new Timer(_ =>
        {
            if (running) portBus.WakeProcessor(0);
        }, null, 200, 200);

        // -------- запуск --------
        Console.CursorVisible = false;
        inputThread.Start();

        try
        {
            ctx.Run();
        }
        catch (Exception ex) when (running)
        {
            Console.WriteLine($"\nVM error: {ex.Message}");
        }
        finally
        {
            running = false;
            Console.CursorVisible = true;
            Console.Write("\x1b[0m");
            Console.SetCursorPosition(0, 18);
        }

    }

    private static void RenderToConsole(byte[] fb, int w, int h)
    {
        var sb = new StringBuilder(w * h * 6 + 64);
        sb.Append("\x1b[H");
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
                sb.Append(fb[y * w + x] != 0 ? "██" : "  ");
            sb.Append('\n');
        }
        sb.Append("Arrows=move  Q=quit\n");
        Console.Write(sb.ToString());
    }
}
