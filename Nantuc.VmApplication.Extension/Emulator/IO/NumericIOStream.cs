using Nantuc.VmApplication.Emulator.Abstraction;
using System.Buffers;
using System.Globalization;
using System.Text;

namespace Nantuc.VmApplication.Extensions.Emulator.IO;

public sealed class NumericIOStream : PortControlBase
{
    private readonly TextWriter _writer;
    private readonly PortCharEncoding _encoding;
    private readonly Decoder? _utf8Decoder;
    private readonly char[] _charBuffer = new char[4];
    private readonly bool _hex;
    private byte? _pendingLowByte;

    public NumericIOStream(TextWriter writer, PortCharEncoding encoding, bool hex = false)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _encoding = encoding;
        _hex = hex;

        if (encoding == PortCharEncoding.Utf8)
            _utf8Decoder = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetDecoder();
    }

    public override byte ReadPort(ulong offset) => 0;

    public override void WritePort(ulong offset, byte value)
    {
        ThrowIfDisposed();
        switch (offset)
        {
            case OutputPortLayout.Data:
                EmitByte(value);
                break;

            case OutputPortLayout.Flush:
                _writer.Flush();
                break;
        }
    }

    public override void WakeProcessor() => _writer.Flush();

    private void EmitByte(byte value)
    {
        switch (_encoding)
        {
            case PortCharEncoding.BytePerChar:
                EmitNumber(value);
                _writer.Flush();
                break;

            case PortCharEncoding.Utf16:
                if (_pendingLowByte is null)
                {
                    _pendingLowByte = value;
                    return;                    // ждём старший байт — как в оригинале
                }
                EmitNumber(_pendingLowByte.Value | (value << 8));   // UTF-16 code unit
                _pendingLowByte = null;
                _writer.Flush();
                break;

            case PortCharEncoding.Utf8:
                Span<byte> one = [value];
                int charsUsed = _utf8Decoder!.GetChars(one, _charBuffer, flush: false);
                if (charsUsed > 0)
                {
                    EmitUtf8CodePoints(_charBuffer.AsSpan(0, charsUsed));
                    _writer.Flush();
                }
                break;
        }
    }

    private void EmitUtf8CodePoints(ReadOnlySpan<char> chars)
    {
        while (!chars.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(chars, out Rune rune, out int consumed) != OperationStatus.Done)
                break;
            EmitNumber(rune.Value);
            chars = chars[consumed..];
        }
    }

    private void EmitNumber(int n)
    {
        _writer.Write(_hex
            ? $"0x{n:X}"
            : n.ToString(CultureInfo.InvariantCulture));
        _writer.Write(' ');
    }

    protected override void OnDispose()
    {
        try { _writer.Flush(); } catch { }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(IsDisposed, this);
}