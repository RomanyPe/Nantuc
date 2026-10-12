using Compiller.ASM;
using Kernel.Common;
using Kernel.Contracts;
using VMApplication.Project;

namespace VMApplication;

public static class VMHostHelper
{
    public static ResultDeCompilation DisassemblCode(ReadOnlySpan<byte> prog, ulong baseAddress = 0UL)
    {
        var text = Disassembler.Disassemble(prog, out int lenght, out int size, baseAddress);
        return new(text, lenght, size);
    }

    private class DeviceLoggerSingleObj : IDeviceLoggerContext
    {
        public void Log(string message, LogLevel level = LogLevel.Log)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = level switch
            {
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                _ => originalColor
            };
            Console.WriteLine(message);
            Console.ForegroundColor = originalColor;
        }
    }
}
