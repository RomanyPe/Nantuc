using Nantuc.VmApplication.Emulator.Abstraction;

namespace Nantuc.Samples.Scratch.SnakeGame;

public sealed class KeyboardDevice : PortControlBase
{
    public const byte REG_KEY = 0x00;
    public const byte REG_HAS_NEW = 0x01;

    public const byte KEY_NONE = 0;
    public const byte KEY_UP = 1;
    public const byte KEY_DOWN = 2;
    public const byte KEY_LEFT = 3;
    public const byte KEY_RIGHT = 4;

    private byte _key;
    private volatile bool _hasNew;

    public void Press(byte key) { _key = key; _hasNew = true; }

    private static uint Rel(ulong offset) => (uint)(offset & 0xFF);

    public override byte ReadPort(ulong offset) => Rel(offset) switch
    {
        REG_KEY => _key,
        REG_HAS_NEW => ConsumeNew(),
        _ => 0
    };

    private byte ConsumeNew()
    {
        if (!_hasNew) return 0;
        _hasNew = false;
        return 1;
    }

    public override void WritePort(ulong offset, byte value)
    {
        if (Rel(offset) == REG_KEY && value == 0) _key = 0;
    }

    public override void WakeProcessor() { }
}