using Nantuc.VmApplication.Emulator.Abstraction;

namespace Nantuc.Samples.Scratch.SnakeGame;

public sealed class MonochromeMonitor : PortControlBase
{
    public const int REG_CURSOR_X = 0;
    public const int REG_CURSOR_Y = 1;
    public const int REG_PIXEL = 2;
    public const int REG_COMMAND = 3;
    public const int REG_WIDTH = 4;
    public const int REG_HEIGHT = 5;
    public const int REG_FRAME = 6;

    public const byte CMD_CLEAR = 1;
    public const byte CMD_FILL = 2;
    public const byte CMD_FLUSH = 3;
    public const byte CMD_HOME = 4;

    private readonly byte[] _fb;
    private readonly Action<byte[], int, int> _renderer;
    private int _cx, _cy;
    private byte _pixel;
    private byte _frame;

    public int Width { get; }
    public int Height { get; }

    public MonochromeMonitor(int width, int height, Action<byte[], int, int>? renderer = null)
    {
        if (width is <= 0 or > 255) throw new ArgumentOutOfRangeException(nameof(width));
        if (height is <= 0 or > 255) throw new ArgumentOutOfRangeException(nameof(height));
        Width = width;
        Height = height;
        _fb = new byte[width * height];
        _renderer = renderer ?? ((_, _, _) => { });
    }

    public override byte ReadPort(ulong offset) => (int)(offset & 0x0F) switch
    {
        REG_CURSOR_X => (byte)_cx,
        REG_CURSOR_Y => (byte)_cy,
        REG_PIXEL => _fb[_cy * Width + _cx],
        REG_WIDTH => (byte)Width,
        REG_HEIGHT => (byte)Height,
        REG_FRAME => _frame,
        _ => 0
    };

    public override void WritePort(ulong offset, byte value)
    {
        switch ((int)(offset & 0x0F))
        {
            case REG_CURSOR_X:
                _cx = Math.Clamp((int)value, 0, Width - 1);
                break;
            case REG_CURSOR_Y:
                _cy = Math.Clamp((int)value, 0, Height - 1);
                break;
            case REG_PIXEL:
                _pixel = value;
                _fb[_cy * Width + _cx] = value;
                break;
            case REG_COMMAND:
                switch (value)
                {
                    case CMD_CLEAR:
                        Array.Clear(_fb);
                        _cx = _cy = 0;
                        break;
                    case CMD_FILL:
                        Array.Fill(_fb, _pixel);
                        break;
                    case CMD_HOME:
                        _cx = _cy = 0;
                        break;
                    case CMD_FLUSH:
                        _renderer(_fb, Width, Height);
                        unchecked { _frame++; }
                        break;
                }
                break;
        }
    }

    public override void WakeProcessor() { }
}