using Nantuc.Kernel.Common;
using Nantuc.Kernel.Contracts;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Emulator.Abstraction;
using Nantuc.VmApplication.Extensions.Host;
using Nantuc.VmApplication.Project;

namespace Nantuc.Tests;

public class HaltWakeTests
{
    /// <summary>Сохраняет все ошибки и останавливает симуляцию.</summary>
    private sealed class CapturingFaultPolicy : IProcessorFaultPolicy
    {
        public List<ProcessorFault> Faults { get; } = [];
        public bool ShouldContinue(in ProcessorFault fault)
        {
            Faults.Add(fault);
            return false;
        }
    }

    /// <summary>Устройство, которое только фиксирует вызов WakeProcessor.</summary>
    private sealed class FakeWakingDevice : PortControlBase
    {
        public int WakeCount { get; private set; }
        public override byte ReadPort(ulong offset) => 0;
        public override void WritePort(ulong offset, byte value) { }
        public override void WakeProcessor() => WakeCount++;
    }

    // ------------------------------------------------------------------
    // HALT
    // ------------------------------------------------------------------

    [Fact]
    public async Task HALT_SetsSleepingFlagAndDoesNotAdvanceFurther_DebugRun()
    {
        using var host = VmHostFactory.CreateDefault();
        var deviceId = host.Emulator.RegisterDevice(
            DeviceSpec.Create(RamSize.MB16, name: "cpu"), sector: 0);
        var deviceCtx = host.Emulator.GetDeviceContext(deviceId)!;

        string program = """
        LDI  r1, 0x11
        HALT
        LDI  r2, 0x22
        END
        """;

        var comp = host.Project
            .CompileToIL(program, 0, false, SourceLanguage.Asm)
            .Build();
        Assert.True(comp.Success, string.Join("\n", comp.Errors ?? []));
        Assert.True(deviceCtx.TryFastLoadProgram(comp.Program!, 0, out var err), err);

        var sleepDetected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        bool sawSleeping = false;
        bool wasRunningAtHalt = false;
        ulong r1AtHalt = 0;
        ulong r2AtHalt = 0;

        var options = new LaunchOptionsDebug(
            StartAddress: 0,
            StepLimit: long.MaxValue,
            OnStep: reader =>
            {
                if (reader.IsSleeping && !sleepDetected.Task.IsCompleted)
                {
                    sawSleeping = true;
                    wasRunningAtHalt = reader.IsRunning;
                    r1AtHalt = reader.GetRegister(RegType.r1);
                    r2AtHalt = reader.GetRegister(RegType.r2);
                    sleepDetected.TrySetResult();
                }
            });

        // DebugRun — синхронный и блокирующий, поэтому уводим его в отдельный поток.
        var runTask = Task.Run(() => deviceCtx.DebugRun(options));

        await sleepDetected.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Принудительно останавливаем устройство — это разбудит DebugRun.
        deviceCtx.Stop();

        await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(sawSleeping, "HALT did not set the sleeping flag.");
        Assert.True(wasRunningAtHalt, "Device was already stopped at HALT.");
        Assert.Equal(0x11UL, r1AtHalt);
        Assert.Equal(0UL, r2AtHalt); // инструкция после HALT не выполнилась
    }

    [Fact]
    public void HALT_SetsSleepingFlagAndDoesNotAdvanceFurther_StepMode()
    {
        using var host = VmHostFactory.CreateDefault();
        var deviceId = host.Emulator.RegisterDevice(
            DeviceSpec.Create(RamSize.MB16, name: "cpu"), sector: 0);
        var deviceCtx = host.Emulator.GetDeviceContext(deviceId)!;

        string program = """
        LDI  r1, 0x11
        HALT
        LDI  r2, 0x22
        END
        """;

        var comp = host.Project
            .CompileToIL(program, 0, false, SourceLanguage.Asm)
            .Build();
        Assert.True(comp.Success, string.Join("\n", comp.Errors ?? []));
        Assert.True(deviceCtx.TryFastLoadProgram(comp.Program!, 0, out var err), err);

        deviceCtx.RunStepMode(startAddress: 0);

        bool sawSleeping = false;
        for (int i = 0; i < 20 && !sawSleeping; i++)
        {
            deviceCtx.SingleStep(reader =>
            {
                if (reader.IsSleeping) sawSleeping = true;
            });
        }

        var regs = deviceCtx.GetRegistersSnapshot()!;
        deviceCtx.EndStepMode();

        Assert.True(sawSleeping, "HALT did not set the sleeping flag.");
        Assert.Equal(0x11UL, regs[RegType.r1]);
        Assert.Equal(0UL, regs[RegType.r2]);
    }

    // ------------------------------------------------------------------
    // WAKE_INT
    // ------------------------------------------------------------------

    [Fact]
    public void WAKE_INT_WithoutDevice_ReportsNullDeviceOutputFault()
    {
        using var host = VmHostFactory.CreateDefault();
        var faultPolicy = new CapturingFaultPolicy();
        var deviceId = host.Emulator.RegisterDevice(
            DeviceSpec.Create(RamSize.MB16, processorFaultPolicy: faultPolicy, name: "cpu"),
            sector: 0);
        var deviceCtx = host.Emulator.GetDeviceContext(deviceId)!;

        // Сектор 5 — устройства с таким адресом нет.
        string program = """
            LDI      r1, 0x50
            WAKE_INT r1
            END
            """;

        var comp = host.Project
            .CompileToIL(program, 0, false, SourceLanguage.Asm)
            .Build();
        Assert.True(comp.Success, string.Join("\n", comp.Errors ?? []));
        Assert.True(deviceCtx.TryFastLoadProgram(comp.Program!, 0, out var err), err);

        deviceCtx.Run();

        Assert.NotEmpty(faultPolicy.Faults);
        Assert.Equal(BiosStatus.NullDeviceOutput, faultPolicy.Faults[0].Status);
    }

    [Fact]
    public void WAKE_INT_WithRegisteredDevice_InvokesWakeProcessor()
    {
        using var host = VmHostFactory.CreateDefault();

        // Устройство в секторе 1 → базовый адрес 0x10 (16 портов на устройство).
        var wakeable = new FakeWakingDevice();
        host.Emulator.RegisterDevice(wakeable, name: "wakeable", sector: 1);

        var deviceId = host.Emulator.RegisterDevice(
            DeviceSpec.Create(RamSize.MB16, name: "cpu"), sector: 0);
        var deviceCtx = host.Emulator.GetDeviceContext(deviceId)!;

        string program = """
            LDI      r1, 0x10
            WAKE_INT r1
            END
            """;

        var comp = host.Project
            .CompileToIL(program, 0, false, SourceLanguage.Asm)
            .Build();
        Assert.True(comp.Success, string.Join("\n", comp.Errors ?? []));
        Assert.True(deviceCtx.TryFastLoadProgram(comp.Program!, 0, out var err), err);

        deviceCtx.Run();

        Assert.Equal(1, wakeable.WakeCount);
    }

    [Fact]
    public void WAKE_INT_UsesValueFromRegister_AfterMov()
    {
        using var host = VmHostFactory.CreateDefault();

        var wakeable = new FakeWakingDevice();
        host.Emulator.RegisterDevice(wakeable, name: "wakeable", sector: 2); // base = 0x20

        var deviceId = host.Emulator.RegisterDevice(
            DeviceSpec.Create(RamSize.MB16, name: "cpu"), sector: 0);
        var deviceCtx = host.Emulator.GetDeviceContext(deviceId)!;

        // Адрес сначала попадает в r2, затем переносится в r1 (MOV) и передаётся в WAKE_INT.
        string program = """
            LDI  r2, 0x20
            MOV  r1, r2
            WAKE_INT r1
            END
            """;

        var comp = host.Project
            .CompileToIL(program, 0, false, SourceLanguage.Asm)
            .Build();
        Assert.True(comp.Success, string.Join("\n", comp.Errors ?? []));
        Assert.True(deviceCtx.TryFastLoadProgram(comp.Program!, 0, out var err), err);

        deviceCtx.Run();

        Assert.Equal(1, wakeable.WakeCount);
    }
}