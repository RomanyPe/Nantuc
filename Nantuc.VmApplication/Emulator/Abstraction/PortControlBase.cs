using Nantuc.Kernel.Contracts;

namespace Nantuc.VmApplication.Emulator.Abstraction;

public abstract class PortControlBase : IPortController
{
    private int _disposed;

    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        OnDispose();
        GC.SuppressFinalize(this);
    }

    public abstract byte ReadPort(ulong offset);
    public abstract void WakeProcessor();
    public abstract void WritePort(ulong offset, byte value);

    protected virtual void OnDispose() { }
}