using System;
using System.Collections.Generic;
using System.Text;

namespace Nantuc.Compiler.ASM.Parser;

public partial class AssemblerParser
{
    // ============================================================
    // Директивы секции данных: .byte/.word/.dword/.quad/.ascii/.asciz/.zero.
    // ============================================================
    private bool TryProcessDataDirective(string line, out string? errorMsg)
    {
        errorMsg = null;

        int spaceIdx = line.IndexOfAny([' ', '\t']);
        string directive = spaceIdx < 0 ? line : line[..spaceIdx];
        string args = spaceIdx < 0 ? "" : line[(spaceIdx + 1)..].Trim();
        string dir = directive.ToLowerInvariant();

        switch (dir)
        {
            case ".byte":
            case "db":
                if (args.Length >= 2 && (args[0] == '"' || args[0] == '\''))
                    return TryAddStringData(args, nullTerminated: false, out errorMsg);
                return TryAddIntegerData(args, 1, out errorMsg);

            case ".word": case "dw": return TryAddIntegerData(args, 2, out errorMsg);
            case ".dword": case "dd": return TryAddIntegerData(args, 4, out errorMsg);
            case ".quad": case "dq": return TryAddIntegerData(args, 8, out errorMsg);

            case ".ascii": return TryAddStringData(args, nullTerminated: false, out errorMsg);
            case ".asciz":
            case ".string": return TryAddStringData(args, nullTerminated: true, out errorMsg);

            case ".zero":
            case ".space":
                if (string.IsNullOrWhiteSpace(args))
                { errorMsg = $"{directive} requires a count"; return true; }
                if (!long.TryParse(args, out long cnt) || cnt < 0)
                { errorMsg = $"{directive} count must be a non-negative integer"; return true; }
                AddPendingData(new byte[cnt]);
                return true;
        }
        return false;
    }

    private bool TryAddIntegerData(string args, int width, out string? errorMsg)
    {
        errorMsg = null;
        if (string.IsNullOrWhiteSpace(args))
        { errorMsg = "missing data values"; return true; }

        var parts = args.Split([',', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var bytes = new byte[parts.Length * width];

        for (int i = 0; i < parts.Length; i++)
        {
            if (!TryParseLong(parts[i], out long value))
            { errorMsg = $"invalid numeric value: {parts[i]}"; return true; }

            for (int b = 0; b < width; b++)
                bytes[i * width + b] = (byte)((ulong)value >> (b * 8));
        }

        AddPendingData(bytes);
        return true;
    }

    private bool TryAddStringData(string args, bool nullTerminated, out string? errorMsg)
    {
        errorMsg = null;
        args = args.Trim();
        if (args.Length < 2 || (args[0] != '"' && args[0] != '\'') || args[^1] != args[0])
        { errorMsg = "string literal expected"; return true; }

        string inner = args[1..^1];
        var bytes = new List<byte>(inner.Length + 1);

        for (int i = 0; i < inner.Length; i++)
        {
            char c = inner[i];
            if (c == '\\' && i + 1 < inner.Length)
            {
                i++;
                bytes.Add(inner[i] switch
                {
                    'n' => (byte)'\n',
                    't' => (byte)'\t',
                    'r' => (byte)'\r',
                    '0' => 0,
                    '\\' => (byte)'\\',
                    '"' => (byte)'"',
                    '\'' => (byte)'\'',
                    _ => (byte)inner[i],
                });
            }
            else bytes.Add((byte)c);
        }

        if (nullTerminated) bytes.Add(0);
        AddPendingData([.. bytes]);
        return true;
    }

    private void AddPendingData(byte[] bytes)
    {
        _pendingData.Add(new PendingData(_pendingLabel, bytes));
        _pendingLabel = null;
    }
}
