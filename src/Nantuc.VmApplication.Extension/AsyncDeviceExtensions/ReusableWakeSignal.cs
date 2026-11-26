using System.Threading.Tasks.Sources;

namespace Nantuc.VmApplication.Extensions.AsyncDeviceExtensions;

public sealed class ReusableWakeSignal : IValueTaskSource<bool>, IDisposable
{
    private ManualResetValueTaskSourceCore<bool> _core;
    private readonly Timer _timer;
    private readonly Lock _gate = new();
    private CancellationTokenRegistration _ctr;
    private bool _signaled;
    private bool _waiting;
    private bool _disposed;

    public ReusableWakeSignal()
    {
        _core.RunContinuationsAsynchronously = true;
        _timer = new Timer(static s => ((ReusableWakeSignal)s!).Complete(false),
            this, Timeout.Infinite, Timeout.Infinite);
    }

    public ValueTask<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_disposed || cancellationToken.IsCancellationRequested)
                return new ValueTask<bool>(false);

            if (_signaled)
            {
                _signaled = false;
                return new ValueTask<bool>(true);
            }

            if (_waiting)
                return new ValueTask<bool>(true);

            _core.Reset();
            _waiting = true;

            if (cancellationToken.CanBeCanceled)
            {
                _ctr = cancellationToken.UnsafeRegister(
                    static s => ((ReusableWakeSignal)s!).Complete(false), this);
            }

            _timer.Change(timeout, Timeout.InfiniteTimeSpan);
            return new ValueTask<bool>(this, _core.Version);
        }
    }

    public void Set() => Complete(true);

    private void Complete(bool isSignaled)
    {
        CancellationTokenRegistration ctrToDispose;
        lock (_gate)
        {
            if (_disposed)
                return;

            if (!_waiting)
            {
                if (isSignaled)
                    _signaled = true;
                return;
            }

            ctrToDispose = _ctr;
            _ctr = default;

            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            _core.SetResult(isSignaled);
        }

        ctrToDispose.Dispose();
    }

    public bool GetResult(short token)
    {
        lock (_gate)
        {
            try
            {
                return _core.GetResult(token);
            }
            finally
            {
                _waiting = false;
            }
        }
    }

    public ValueTaskSourceStatus GetStatus(short token)
    {
        lock (_gate)
            return _core.GetStatus(token);
    }

    public void OnCompleted(Action<object?> c, object? s, short t, ValueTaskSourceOnCompletedFlags f)
    {
        lock (_gate)
            _core.OnCompleted(c, s, t, f);
    }

    public void Dispose()
    {
        CancellationTokenRegistration ctrToDispose = default;

        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            _timer.Dispose();

            if (_waiting)
            {
                ctrToDispose = _ctr;
                _ctr = default;
                _core.SetResult(false);
            }
        }

        ctrToDispose.Dispose();
    }
}
