using Nantuc.Kernel.Common;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Emulator.Abstraction;
using Nantuc.VmApplication.Extensions.Emulator.IO;
using Nantuc.VmApplication.Extensions.Host;
using Nantuc.VmApplication.Project;
using System.Text;

namespace Nantuc.Tests;

public class VMHostTests
{
    // Вспомогательный метод для запуска программы с IO-устройством
    private static void RunProgramWithIO(string program, PortCharEncoding encoding, bool useQueued, string expected)
    {
        using var host = VmHostFactory.CreateDefault();
        var writer = new StringWriter();
        PortControlBase ioDevice = useQueued
            ? new QueuedIOStream(writer, encoding)
            : new SynchronousIOStream(writer, encoding);

        host.Emulator.RegisterDevice(ioDevice, "console", sector: 1);

        var handle = DeviceSpec.Create(RamSize.MB16, name: "test");
        var deviceId = host.Emulator.RegisterDevice(handle, sector: 0);
        var deviceCtx = host.Emulator.GetDeviceContext(deviceId)!;

        var comp = host.Project.CompileToIL(program, 0, false, SourceLanguage.Asm).Build();
        Assert.True(comp.Success, string.Join("\n", comp.Errors ?? []));
        Assert.True(deviceCtx.TryFastLoadProgram(comp.Program!, 0, out var err), err);

        deviceCtx.Run();

        Assert.Equal(expected, writer.ToString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IOStream_BytePerChar_ShouldOutputCorrectly(bool useQueued)
    {
        // Программа выводит "Hello"
        string program = """
            LDI r1, 0x48
            LDI r2, 0x10
            OUT r1, r2
            LDI r1, 0x65
            OUT r1, r2
            LDI r1, 0x6C
            OUT r1, r2
            LDI r1, 0x6C
            OUT r1, r2
            LDI r1, 0x6F
            OUT r1, r2
            LDI r1, 1
            LDI r2, 0x12
            OUT r1, r2
            END
            """;
        RunProgramWithIO(program, PortCharEncoding.BytePerChar, useQueued, "Hello");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IOStream_Utf8_ShouldOutputCorrectly(bool useQueued)
    {
        // UTF-8 для 'é' = 0xC3 0xA9
        string program = """
            LDI r1, 0xC3
            LDI r2, 0x10
            OUT r1, r2
            LDI r1, 0xA9
            OUT r1, r2
            LDI r1, 1
            LDI r2, 0x12
            OUT r1, r2
            END
            """;
        RunProgramWithIO(program, PortCharEncoding.Utf8, useQueued, "é");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IOStream_Utf16_ShouldOutputCorrectly(bool useQueued)
    {
        // UTF-16 LE для 'é' = 0xE9 0x00
        string program = """
            LDI r1, 0xE9
            LDI r2, 0x10
            OUT r1, r2
            LDI r1, 0x00
            OUT r1, r2
            LDI r1, 1
            LDI r2, 0x12
            OUT r1, r2
            END
            """;
        RunProgramWithIO(program, PortCharEncoding.Utf16, useQueued, "é");
    }

    [Fact]
    public void StepMode_ShouldExecuteInstructionsOneByOne()
    {
        using var host = VmHostFactory.CreateDefault();
        var handle = DeviceSpec.Create(RamSize.MB16, name: "test");
        var deviceId = host.Emulator.RegisterDevice(handle, sector: 0);
        var deviceCtx = host.Emulator.GetDeviceContext(deviceId)!;

        string program = """
            LDI r1, 1
            LDI r2, 2
            LDI r3, 3
            LDI r4, 4
            END
            """;
        var comp = host.Project.CompileToIL(program, 0, false, SourceLanguage.Asm).Build();
        Assert.True(comp.Success, string.Join("\n", comp.Errors ?? []));
        Assert.True(deviceCtx.TryFastLoadProgram(comp.Program!, 0, out var err), err);

        deviceCtx.RunStepMode(startAddress: 0);
        Assert.True(deviceCtx.IsRunning);

        deviceCtx.SingleStep();
        Assert.Equal(1, deviceCtx.StepCount);

        deviceCtx.SingleStep();
        Assert.Equal(2, deviceCtx.StepCount);

        deviceCtx.MultiStep(2);
        Assert.Equal(4, deviceCtx.StepCount);

        deviceCtx.EndStepMode();
        Assert.False(deviceCtx.IsRunning);
    }

    [Fact]
    public void DebugRun_ShouldStopAfterStepLimit()
    {
        using var host = VmHostFactory.CreateDefault();
        var handle = DeviceSpec.Create(RamSize.MB16, name: "test");
        var deviceId = host.Emulator.RegisterDevice(handle, sector: 0);
        var deviceCtx = host.Emulator.GetDeviceContext(deviceId)!;

        string program = """
            LDI r1, 1
            LDI r2, 2
            LDI r3, 3
            LDI r4, 4
            END
            """;
        var comp = host.Project.CompileToIL(program, 0, false, SourceLanguage.Asm).Build();
        Assert.True(comp.Success, string.Join("\n", comp.Errors ?? []));
        Assert.True(deviceCtx.TryFastLoadProgram(comp.Program!, 0, out var err), err);

        var options = new LaunchOptionsDebug(StartAddress: 0, StepLimit: 2);
        deviceCtx.DebugRun(options);
        Assert.Equal(2, deviceCtx.StepCount);
    }

    [Fact]
    public void Run_ShouldInvokeOnEnd()
    {
        using var host = VmHostFactory.CreateDefault();
        var handle = DeviceSpec.Create(RamSize.MB16, name: "test");
        var deviceId = host.Emulator.RegisterDevice(handle, sector: 0);
        var deviceCtx = host.Emulator.GetDeviceContext(deviceId)!;

        string program = "LDI r1, 1\nEND";
        var comp = host.Project.CompileToIL(program, 0, false, SourceLanguage.Asm).Build();
        Assert.True(comp.Success, string.Join("\n", comp.Errors ?? []));
        Assert.True(deviceCtx.TryFastLoadProgram(comp.Program!, 0, out var err), err);

        bool ended = false;
        var options = new LaunchOptions(StartAddress: 0, OnEnd: (res) => ended = true);
        deviceCtx.Run(options);
        Assert.True(ended);
    }

    [Fact]
    public async Task QueuedIOStream_FlushShouldWaitForPendingData()
    {
        using var host = VmHostFactory.CreateDefault();
        var writer = new BlockingTextWriter();
        var ioDevice = new QueuedIOStream(writer, PortCharEncoding.BytePerChar);
        host.Emulator.RegisterDevice(ioDevice, "console", sector: 1);

        var handle = DeviceSpec.Create(RamSize.MB16, name: "test");
        var deviceId = host.Emulator.RegisterDevice(handle, sector: 0);
        var deviceCtx = host.Emulator.GetDeviceContext(deviceId)!;

        string program = """
            LDI r1, 0x41
            LDI r2, 0x10
            OUT r1, r2
            LDI r1, 1
            LDI r2, 0x12
            OUT r1, r2
            END
            """;
        var comp = host.Project.CompileToIL(program, 0, false, SourceLanguage.Asm).Build();
        Assert.True(comp.Success, string.Join("\n", comp.Errors ?? []));
        Assert.True(deviceCtx.TryFastLoadProgram(comp.Program!, 0, out var err), err);

        var runTask = Task.Run(() => deviceCtx.Run());

        // Ждём, пока фоновый поток IO вызовет Write и заблокируется
        await writer.WriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Устройство должно быть заблокировано в Flush, поэтому runTask не завершён
        Assert.False(runTask.IsCompleted);

        // Освобождаем писателя
        writer.Release();

        // Теперь выполнение должно завершиться
        await runTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("A", writer.ToString());
    }
    [Fact]
    public void RunAndKeep_ShouldKeepDeviceAlive()
    {
        using var host = VmHostFactory.CreateDefault();
        var writer = new StringWriter();
        var ioDevice = new SynchronousIOStream(writer, PortCharEncoding.BytePerChar);
        host.Emulator.RegisterDevice(ioDevice, "console", sector: 1);

        string program = """
            LDI r1, 0x41
            LDI r2, 0x10
            OUT r1, r2
            END
            """;
        var outcome = host.RunAndKeep(program, SourceLanguage.Asm, RamSize.MB16);
        Assert.True(outcome.Success);
        Assert.NotNull(outcome.Device);
        Assert.Equal("A", writer.ToString());

        // Очистка
        if (outcome.DeviceId.HasValue)
            host.Emulator.UnregisterDevice(outcome.DeviceId.Value);
    }
}

// Вспомогательный TextWriter, блокирующий запись до сигнала
public class BlockingTextWriter : TextWriter
{
    private readonly TaskCompletionSource _releaseGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly StringBuilder _sb = new();

    public TaskCompletionSource WriteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
    {
        WriteStarted.TrySetResult();
        // Блокируем поток драйвера до вызова Release()
        _releaseGate.Task.GetAwaiter().GetResult();
        _sb.Append(value);
    }

    public void Release() => _releaseGate.TrySetResult();

    public override string ToString() => _sb.ToString();
}
