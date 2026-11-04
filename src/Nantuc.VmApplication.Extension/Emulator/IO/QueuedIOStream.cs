using Nantuc.VmApplication.Emulator.Abstraction;
using System.Text;
using System.Threading.Channels;

namespace Nantuc.VmApplication.Extensions.Emulator.IO;

/// <summary>
/// Асинхронный драйвер устройства вывода.
/// WritePort кладёт байт в потокобезопасный канал и сразу возвращает
/// управление CPU. Фоновой таск разгружает очередь.
/// </summary>
public sealed class QueuedIOStream : PortControlBase
{
    private readonly TextWriter _writer;
    private readonly PortCharEncoding _encoding;
    private readonly Channel<byte> _channel;
    private readonly Task _drainTask;
    private readonly CancellationTokenSource _cts = new();

    // Ручной счётчик: ChannelReader.Count у unbounded-каналов не поддерживается.
    private int _pendingCount;

    public QueuedIOStream(TextWriter writer, PortCharEncoding encoding)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _encoding = encoding;

        _channel = Channel.CreateUnbounded<byte>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

        _drainTask = Task.Run(DrainAsync);
    }

    public override byte ReadPort(ulong offset) => offset switch
    {
        // 0 — очередь пуста, 1 — есть необработанные байты
        OutputPortLayout.Status => (byte)(Volatile.Read(ref _pendingCount) > 0 ? 1 : 0),
        _ => 0
    };

    public override void WritePort(ulong offset, byte value)
    {
        ThrowIfDisposed();
        switch (offset)
        {
            case OutputPortLayout.Data:
                if (!_channel.Writer.TryWrite(value))
                    throw new InvalidOperationException("Output queue is closed.");
                Interlocked.Increment(ref _pendingCount);
                break;

            case OutputPortLayout.Flush:
                Flush();
                break;
        }
    }

    private void Flush()
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (Volatile.Read(ref _pendingCount) > 0 && DateTime.UtcNow < deadline)
            Thread.Sleep(1);
        _writer.Flush();
    }

    public override void WakeProcessor() => Flush();

    private async Task DrainAsync()
    {
        byte? pendingLowByte = null;
        Decoder? utf8Decoder = _encoding == PortCharEncoding.Utf8
            ? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetDecoder()
            : null;

        byte[] single = new byte[1];
        char[] chars = new char[4];

        try
        {
            await foreach (byte b in _channel.Reader.ReadAllAsync(_cts.Token))
            {
                switch (_encoding)
                {
                    case PortCharEncoding.BytePerChar:
                        _writer.Write((char)b);
                        break;

                    case PortCharEncoding.Utf16:
                        if (pendingLowByte is null)
                        {
                            // Ждём второй байт — но счётчик уже уменьшаем:
                            // первый байт фактически "поглощён" буфером драйвера.
                            pendingLowByte = b;
                            Interlocked.Decrement(ref _pendingCount);
                            continue;
                        }
                        char c = (char)(pendingLowByte.Value | (b << 8));
                        pendingLowByte = null;
                        _writer.Write(c);
                        break;

                    case PortCharEncoding.Utf8:
                        single[0] = b;
                        int charsUsed = utf8Decoder!.GetChars(single, chars, flush: false);
                        if (charsUsed > 0)
                            _writer.Write(chars, 0, charsUsed);
                        break;
                }
                _writer.Flush();
                Interlocked.Decrement(ref _pendingCount);
            }
        }
        catch (OperationCanceledException) { /* нормальное завершение */ }
    }

    protected override void OnDispose()
    {
        _channel.Writer.TryComplete();
        try { _drainTask.Wait(TimeSpan.FromSeconds(5)); }
        catch (AggregateException) { }

        try { _writer.Flush(); } catch { }
        _cts.Dispose();
    }
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(IsDisposed, this);
}