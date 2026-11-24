using Nantuc.VmApplication.Emulator.Abstraction;

namespace Nantuc.Samples.InWeb.Pages;

public sealed class SnakeCommander : PortControlBase
{
    // Read (VM читает)
    public const byte R_INPUT = 0x00; // 0..4, auto-consume
    public const byte R_TICK_READY = 0x01; // 0|1, auto-consume

    // Write (VM пишет)
    public const byte W_YIELD = 0x00; // 1 = «кадр готов, управление вернулось»
    public const byte W_GAME_OVER = 0x02; // 1 = змейка умерла
    public const byte W_SCORE = 0x03; // текущий счёт

    private volatile bool _pendingTick;
    private volatile byte _pendingInput;
    private volatile bool _yielded;
    private volatile bool _gameOver;
    private volatile int _score;

    // --- API для игрового цикла ---
    public void SignalTick() => _pendingTick = true;
    public void SetInput(byte key) { if (key != 0) _pendingInput = key; }

    public bool ConsumeYielded() { if (!_yielded) return false; _yielded = false; return true; }
    public bool ConsumeGameOver() { if (!_gameOver) return false; _gameOver = false; return true; }
    public int Score => _score;

    public override byte ReadPort(ulong offset) => (byte)(offset & 0x0F) switch
    {
        R_INPUT => ConsumeInput(),
        R_TICK_READY => ConsumeTick(),
        _ => 0
    };

    public override void WritePort(ulong offset, byte value)
    {
        switch ((byte)(offset & 0x0F))
        {
            case W_YIELD: if (value != 0) _yielded = true; break;
            case W_GAME_OVER: if (value != 0) _gameOver = true; break;
            case W_SCORE: _score = value; break;
        }
    }

    public override void WakeProcessor() { }

    private byte ConsumeTick()
    {
        if (!_pendingTick) return 0;
        _pendingTick = false;
        return 1;
    }

    private byte ConsumeInput()
    {
        byte v = _pendingInput;
        _pendingInput = 0;
        return v;
    }
}