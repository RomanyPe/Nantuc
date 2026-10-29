using Nantuc.Kernel.Common;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Emulator.Abstraction;

namespace Nantuc.Tests;

public class TestHost
{
    [Fact]
    public void TestindIdState()
    {
        using var host = VmHostFactory.CreateDefault(
            portBusSize: PortSize.B128,
            portsPerDevice: DevicePortSize.B128);
        var n = new NullPortController();
        Assert.Equal(IdState.Valid, host.Emulator.TryRegisterDevice(n, "null", 0, out _));
        Assert.NotEqual(IdState.Valid, host.Emulator.TryRegisterDevice(n, "null", 0, out _));
        Assert.NotEqual(IdState.Valid, host.Emulator.TryRegisterDevice(n, "null", 3, out _));
    }

    [Fact]
    public void TestDisposingObjects()
    {
        var obj = new NeedToDoubleDispose();
        var host = VmHostFactory.CreateDefault();
        var state = host.Emulator.TryRegisterDevice(obj, "test", 0, out int id);
        Assert.Equal(IdState.Valid, state);
        Assert.Equal(0, id);
        obj.Dispose();
        host.Dispose();
        Assert.Equal(1,obj.CounterDisposing);
    }

    [Fact]
    public void TestUnregistingObjects()
    {
        var obj = new NeedToDoubleDispose();
        using var host = VmHostFactory.CreateDefault();
        var state = host.Emulator.TryRegisterDevice(obj, "test", 0, out int id);
        Assert.Equal(IdState.Valid, state);
        Assert.Equal(0, id);
        Assert.True(host.Emulator.UnregisterDevice(id));
        var list = host.Emulator.GetAllDevices();
        Assert.Empty(list);
        Assert.True(obj.IsDisposed);
    }

    private class NeedToDoubleDispose : PortControlBase
    {
        public int CounterDisposing { get; private set; } = 0;
        public override byte ReadPort(ulong offset) => 0;

        public override void WakeProcessor() { }

        public override void WritePort(ulong offset, byte value) { }

        protected override void OnDispose() => CounterDisposing++;
    }

    private class NullPortController : PortControlBase
    {
        public override byte ReadPort(ulong offset) => 0;
        public override void WakeProcessor() { }
        public override void WritePort(ulong offset, byte value) { }

    }
}