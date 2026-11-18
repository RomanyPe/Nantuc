using Nantuc.Common;
using Nantuc.Diagnostics;

namespace Nantuc.Compiler.ASM.Parser;

public partial class AssemblerParser
{
    // ============================================================
    // Числовые и регистровые хелперы.
    // ============================================================
    private RegType ParseReg(string s)
    {
        if (_regMap.TryGetValue(s, out RegType reg)) return reg;
        return _asm.ErrorReporter.Throw<RegType>(ErrorCode.Asm_UnknownRegister, DiagnosticData.Of(_currentLine));
    }

    private static bool TryParseSize(string s, out OpCodeSize size)
    {
        switch (s.ToUpperInvariant())
        {
            case "S8": case "8": size = OpCodeSize.S8; return true;
            case "S16": case "16": size = OpCodeSize.S16; return true;
            case "S32": case "32": size = OpCodeSize.S32; return true;
            case "S64": case "64": size = OpCodeSize.S64; return true;
            default: size = default; return false;
        }
    }

    private static bool TryParseLong(string s, out long value)
    {
        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return long.TryParse(s[2..], System.Globalization.NumberStyles.HexNumber, null, out value);
        return long.TryParse(s, out value);
    }

    private static ulong ParseNumber(string s)
    {
        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return Convert.ToUInt64(s[2..], 16);
        return ulong.Parse(s);
    }

    private static bool IsNumber(string s)
    {
        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return ulong.TryParse(s.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out _);
        return ulong.TryParse(s, out _);
    }
}