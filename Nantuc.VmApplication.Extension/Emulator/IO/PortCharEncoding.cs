namespace Nantuc.VmApplication.Extensions.Emulator.IO;

/// <summary>
/// Способ кодирования символов при записи в порт данных.
/// </summary>
public enum PortCharEncoding
{
    /// <summary>1 Write = 1 символ (Latin-1). Простейший режим.</summary>
    BytePerChar,

    /// <summary>1..4 Write = 1 символ, полноценный UTF-8 (через Decoder).</summary>
    Utf8,

    /// <summary>2 Write = 1 символ, UTF-16 little-endian.</summary>
    Utf16
}
