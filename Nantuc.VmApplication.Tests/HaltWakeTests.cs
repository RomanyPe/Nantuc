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
    public async Task HALT_SetsSleepingFlagAndDoesNotAdvanceFurther()
    {
        using var host = VMHostFactory.CreateDefault();
        var faultPolicy = new CapturingFaultPolicy();
        var handle = DeviceSpec.Create(
            RamSize.MB16,
            processorFaultPolicy: faultPolicy,
            name: "cpu");

        var deviceId = host.Emulator.RegisterDevice(handle, sector: 0);
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

        // OnStep вызывается внутри DebugRun-потока. IProcessorReader — это
        // переиспользуемый объект, поэтому все значения фиксируем прямо в колбэке.
        var sleepDetected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        bool sawSleeping = false;
        bool wasRunningAtHalt = false;
        ulong r1AtHalt = 0;
        ulong r2AtHalt = 0;

        var options = new LaunchOptionsDebug(
            StartAddress: 0,
            // StepLimit не помогает после HALT — счётчик шагов не инкрементируется,
            // потому что Step() делает early-return. Поэтому не полагаемся на него.
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

        // Ждём, пока HALT будет обнаружен в OnStep
        await sleepDetected.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Принудительно останавливаем устройство — это разбудит DebugRun и он вернётся.
        deviceCtx.Stop();

        // Ждём корректного завершения фоновой задачи
        await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(sawSleeping, "HALT did not set the sleeping flag.");
        Assert.True(wasRunningAtHalt, "Device was already stopped at HALT.");
        Assert.Equal(0x11UL, r1AtHalt);
        Assert.Equal(0UL, r2AtHalt);            // инструкция после HALT не выполнилась
    }
    // ------------------------------------------------------------------
    // WAKE_INT
    // ------------------------------------------------------------------

    [Fact]
    public void WAKE_INT_WithoutDevice_ReportsNullDeviceOutputFault()
    {
        using var host = VMHostFactory.CreateDefault();
        var faultPolicy = new CapturingFaultPolicy();
        var handle = DeviceSpec.Create(
            RamSize.MB16,
            processorFaultPolicy: faultPolicy,
            name: "cpu");
        var deviceId = host.Emulator.RegisterDevice(handle, sector: 0);
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
        using var host = VMHostFactory.CreateDefault();

        // Устройство в секторе 1 → базовый адрес 0x10 (16 портов на устройство).
        var wakeable = new FakeWakingDevice();
        host.Emulator.RegisterDevice(wakeable, name: "wakeable", sector: 1);

        var handle = DeviceSpec.Create(RamSize.MB16, name: "cpu");
        var deviceId = host.Emulator.RegisterDevice(handle, sector: 0);
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
    public void WAKE_INT_UsesValueFromRegister()
    {
        using var host = VMHostFactory.CreateDefault();

        var wakeable = new FakeWakingDevice();
        host.Emulator.RegisterDevice(wakeable, name: "wakeable", sector: 2); // base = 0x20

        var handle = DeviceSpec.Create(RamSize.MB16, name: "cpu");
        var deviceId = host.Emulator.RegisterDevice(handle, sector: 0);
        var deviceCtx = host.Emulator.GetDeviceContext(deviceId)!;

        // Сначала записываем адрес в r2, затем переносим в r1 (MOV) и вызываем WAKE_INT r1.
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

    // ------------------------------------------------------------------
    // HALT + WAKE_INT: имитируем пробуждение при повторном запуске
    // ------------------------------------------------------------------
    [Fact]
    public async Task HALT_StopsAtInstruction_AndCanBeDetectedByFaultPolicy()
    {
        using var host = VMHostFactory.CreateDefault();
        var faultPolicy = new CapturingFaultPolicy();
        var handle = DeviceSpec.Create(
            RamSize.MB16,
            processorFaultPolicy: faultPolicy,
            name: "cpu");
        var deviceId = host.Emulator.RegisterDevice(handle, sector: 0);
        var deviceCtx = host.Emulator.GetDeviceContext(deviceId)!;

        string program = """
        LDI  r1, 0xCAFE
        HALT
        END
        """;

        var comp = host.Project
            .CompileToIL(program, 0, false, SourceLanguage.Asm)
            .Build();
        Assert.True(comp.Success, string.Join("\n", comp.Errors ?? []));
        Assert.True(deviceCtx.TryFastLoadProgram(comp.Program!, 0, out var err), err);

        // IProcessorReader переиспользуется — снимаем снимок прямо в колбэке.
        var sleepDetected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        bool sawSleeping = false;
        bool wasRunning = false;
        ulong r1AtHalt = 0;
        ulong r2AtHalt = 0;

        var options = new LaunchOptionsDebug(
            StartAddress: 0,
            // StepLimit после HALT не работает (шаги не засчитываются) —
            // поэтому не полагаемся на него и останавливаем устройство сами.
            StepLimit: long.MaxValue,
            OnStep: reader =>
            {
                if (reader.IsSleeping && !sleepDetected.Task.IsCompleted)
                {
                    sawSleeping = true;
                    wasRunning = reader.IsRunning;
                    r1AtHalt = reader.GetRegister(RegType.r1);
                    r2AtHalt = reader.GetRegister(RegType.r2);
                    sleepDetected.TrySetResult();
                }
            });

        // DebugRun блокирующий — уводим в отдельный поток.
        var runTask = Task.Run(() => deviceCtx.DebugRun(options));

        // Ждём факта HALT.
        await sleepDetected.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Разбудим DebugRun: это выведет его из бесконечного цикла.
        deviceCtx.Stop();

        // Дожидаемся корректного завершения фоновой задачи.
        await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(sawSleeping, "HALT did not set the sleeping flag.");
        Assert.True(wasRunning, "Device was already stopped at HALT.");
        Assert.Equal(0xCAFEUL, r1AtHalt);
        Assert.Equal(0UL, r2AtHalt);
    }

    [Fact]
    public void HALT_StopsAtInstruction_AndCanBeDetectedByFaultPolicy_StepMode()
    {
        using var host = VMHostFactory.CreateDefault();
        var faultPolicy = new CapturingFaultPolicy();
        var handle = DeviceSpec.Create(
            RamSize.MB16, processorFaultPolicy: faultPolicy, name: "cpu");
        var deviceId = host.Emulator.RegisterDevice(handle, sector: 0);
        var deviceCtx = host.Emulator.GetDeviceContext(deviceId)!;

        string program = """
        LDI  r1, 0xCAFE
        HALT
        END
        """;

        var comp = host.Project
            .CompileToIL(program, 0, false, SourceLanguage.Asm)
            .Build();
        Assert.True(comp.Success, string.Join("\n", comp.Errors ?? []));
        Assert.True(deviceCtx.TryFastLoadProgram(comp.Program!, 0, out var err), err);

        deviceCtx.SetStepMode(startAddress: 0);

        bool sawSleeping = false;
        // Хватит 4 шагов: LDI, HALT, и пары «пустых» (early-return) шагов.
        for (int i = 0; i < 4 && !sawSleeping; i++)
        {
            deviceCtx.SingleStep(reader =>
            {
                if (reader.IsSleeping)
                {
                    sawSleeping = true;
                    Assert.True(reader.IsRunning);
                    Assert.Equal(0xCAFEUL, reader.GetRegister(RegType.r1));
                    Assert.Equal(0UL, reader.GetRegister(RegType.r2));
                }
            });
        }

        deviceCtx.EndStepMode();

        Assert.True(sawSleeping, "HALT did not set the sleeping flag.");
    }

    [Fact]
    public void HALT_SetsSleepingFlagAndDoesNotAdvanceFurther_StepMode()
    {
        using var host = VMHostFactory.CreateDefault();
        var faultPolicy = new CapturingFaultPolicy();
        var deviceId = host.Emulator.RegisterDevice(
            DeviceSpec.Create(RamSize.MB16, processorFaultPolicy: faultPolicy, name: "cpu"),
            sector: 0);
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

        deviceCtx.SetStepMode(startAddress: 0);

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

    [Fact]
    public void HALT_SetsSleepingFlag_AndDoesNotAdvanceFurther_StepMode()
    {
        using var host = VMHostFactory.CreateDefault();
        var faultPolicy = new CapturingFaultPolicy();
        var handle = DeviceSpec.Create(
            RamSize.MB16,
            processorFaultPolicy: faultPolicy,
            name: "cpu");
        var deviceId = host.Emulator.RegisterDevice(handle, sector: 0);
        var deviceCtx = host.Emulator.GetDeviceContext(deviceId)!;

        string program = """
        LDI  r1, 0xCAFE
        HALT
        END
        """;

        var comp = host.Project
            .CompileToIL(program, 0, false, SourceLanguage.Asm)
            .Build();
        Assert.True(comp.Success, string.Join("\n", comp.Errors ?? []));
        Assert.True(deviceCtx.TryFastLoadProgram(comp.Program!, 0, out var err), err);

        deviceCtx.SetStepMode(startAddress: 0);

        // Шаг 1: LDI — процессор жив и не спит.
        deviceCtx.SingleStep(reader =>
        {
            Assert.False(reader.IsSleeping);
            Assert.True(reader.IsRunning);
        });

        // Шаг 2: HALT — флаг сна выставлен, r1 уже равен 0xCAFE.
        bool sawSleeping = false;
        bool wasRunning = false;
        ulong r1 = 0;

        deviceCtx.SingleStep(reader =>
        {
            sawSleeping = reader.IsSleeping;
            wasRunning = reader.IsRunning;
            r1 = reader.GetRegister(RegType.r1);
        });

        // Больше шагов НЕ делаем — иначе из-за описанного бага выполнится END.

        deviceCtx.EndStepMode();

        Assert.True(sawSleeping, "HALT не выставил флаг сна.");
        Assert.True(wasRunning, "Процессор уже остановлен — не должен.");
        Assert.Equal(0xCAFEUL, r1);
    }

    [Fact]
    public async Task HALT_RaisesSleepingFlag_DebugRun()
    {
        using var host = VMHostFactory.CreateDefault();
        var handle = DeviceSpec.Create(RamSize.MB16, name: "cpu");
        var deviceId = host.Emulator.RegisterDevice(handle, sector: 0);
        var deviceCtx = host.Emulator.GetDeviceContext(deviceId)!;

        string program = """
        LDI  r1, 0xCAFE
        HALT
        END
        """;
        var comp = host.Project
            .CompileToIL(program, 0, false, SourceLanguage.Asm)
            .Build();
        Assert.True(comp.Success, string.Join("\n", comp.Errors ?? []));
        Assert.True(deviceCtx.TryFastLoadProgram(comp.Program!, 0, out var err), err);

        var sleepDetected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool sawSleeping = false;
        ulong r1AtHalt = 0;

        var options = new LaunchOptionsDebug(
            StartAddress: 0,
            StepLimit: long.MaxValue,
            OnStep: reader =>
            {
                if (reader.IsSleeping && !sleepDetected.Task.IsCompleted)
                {
                    sawSleeping = true;
                    r1AtHalt = reader.GetRegister(RegType.r1);
                    sleepDetected.TrySetResult();
                }
            });

        var runTask = Task.Run(() => deviceCtx.DebugRun(options));
        await sleepDetected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        deviceCtx.Stop();
        await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(sawSleeping);
        Assert.Equal(0xCAFEUL, r1AtHalt);
    }
}