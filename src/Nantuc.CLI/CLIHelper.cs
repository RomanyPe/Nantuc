using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Nantuc.CLI;

public static class CLIHelper
{
    public static bool HaveFlag(ReadOnlySpan<string> flags, ReadOnlySpan<string> args)
    {
        foreach (var arg in flags) if (args.Contains(arg)) return true;
        return false;
    }

    public static bool TryGetArgByFlag(ReadOnlySpan<string> flags, ReadOnlySpan<string> args, [NotNullWhen(true)] out string? path)
    {
        foreach (string item in flags)
        {
            int i = args.IndexOf(item);
            if (i >= 0 && i + 1 < args.Length)
            {
                path = args[i + 1];
                return true;
            }
        }
        path = null;
        return false;
    }

    public static ReadOnlySpan<byte> GetBytesFromText(string p)
    {
        byte[] buf = new byte[p.Length];
        int n = Encoding.Latin1.GetBytes(p, buf);
        return buf.AsSpan(0, n);
    }
}
