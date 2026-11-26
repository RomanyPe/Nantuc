using Nantuc.Common;
using Nantuc.Contracts;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Extensions.Host;
using Nantuc.VmApplication.Project;

namespace Nantuc.Tests.Vm;

public class SyncVmReuseTests
{
    /// <summary>Сохраняет все ошибки и останавливает симуляцию.</summary>
    private sealed class CapturingFaultPolicy : IProcessorPolicy
    {
        public List<ProcessorFault> Faults { get; } = [];
        public bool AllowUnsafeOpCodes => false;
        public bool ShouldContinue(in ProcessorFault fault)
        {
            Faults.Add(fault);
            return false;
        }
    }

    // ------------------------------------------------------------------
    // 1. Перезапуск после нормального завершения (END)
    // ------------------------------------------------------------------

    [Fact]
    public void MultipleRuns_RestartAfterCompletion_ExecutesSecondProgramCorrectly()
    {
        using var host = VmHostFactory.CreateDefault();
        var deviceId = host.Emulator.RegisterDevice(
            DeviceSpec.Create(RamSize.MB16, name: "cpu"), sector: 0);
        var deviceCtx = host.Emulator.GetDeviceContext(deviceId)!;

        // ====== Первая программа ======
        string program1 = """
            LDI  r0, 0x11
            LDI  r1, 0x22
            END
            """;

        var comp1 = host.Project
            .CompileToIR(program1, 0, false, SourceLanguage.Nanasm)
            .Build();
        Assert.True(comp1.Success, string.Join("\n", comp1.Errors ?? []));
        Assert.True(deviceCtx.TryLoadProgram(comp1.Program!, 0, out var err1), err1);

        deviceCtx.Run();

        var snapshot1 = deviceCtx.GetRegistersSnapshot()!;
        Assert.Equal(0x11UL, snapshot1[RegType.r0]);
        Assert.Equal(0x22UL, snapshot1[RegType.r1]);

        // ====== Вторая программа (перезапуск на том же контексте) ======
        string program2 = """
            LDI  r2, 0xAA
            LDI  r3, 0xBB
            END
            """;

        var comp2 = host.Project
            .CompileToIR(program2, 0, false, SourceLanguage.Nanasm)
            .Build();
        Assert.True(comp2.Success, string.Join("\n", comp2.Errors ?? []));
        Assert.True(deviceCtx.TryLoadProgram(comp2.Program!, 0, out var err2), err2);

        deviceCtx.Run();

        var snapshot2 = deviceCtx.GetRegistersSnapshot()!;
        Assert.Equal(0xAAUL, snapshot2[RegType.r2]);
        Assert.Equal(0xBBUL, snapshot2[RegType.r3]);
    }

    // ------------------------------------------------------------------
    // 2. Перезапуск после HALT (требует Stop/Wake для выхода из сна)
    // ------------------------------------------------------------------

    [Fact]
    public async Task MultipleRuns_RestartAfterHalt_RequiresStopAndContinues()
    {
        using var host = VmHostFactory.CreateDefault();
        var deviceId = host.Emulator.RegisterDevice(
            DeviceSpec.Create(RamSize.MB16, name: "cpu"), sector: 0);
        var deviceCtx = host.Emulator.GetDeviceContext(deviceId)!;

        // ====== Первая программа с HALT ======
        string program1 = """
            LDI  r0, 0x11
            HALT
            LDI  r1, 0x22
            END
            """;

        var comp1 = host.Project
            .CompileToIR(program1, 0, false, SourceLanguage.Nanasm)
            .Build();
        Assert.True(comp1.Success);
        Assert.True(deviceCtx.TryLoadProgram(comp1.Program!, 0, out var err1), err1);

        var sleepDetected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var options1 = new LaunchOptionsDebug(
            StartAddress: 0,
            StepLimit: long.MaxValue,
            OnStep: reader =>
            {
                if (reader.IsSleeping) sleepDetected.TrySetResult();
            });

        // DebugRun блокирующий — уводим в Task.Run
        var runTask1 = Task.Run(() => deviceCtx.DebugRun(options1));
        await sleepDetected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        deviceCtx.Stop();
        await runTask1.WaitAsync(TimeSpan.FromSeconds(5));

        // ====== Вторая программа ======
        string program2 = """
            LDI  r5, 0x55
            END
            """;

        var comp2 = host.Project
            .CompileToIR(program2, 0, false, SourceLanguage.Nanasm)
            .Build();
        Assert.True(comp2.Success);
        Assert.True(deviceCtx.TryLoadProgram(comp2.Program!, 0, out var err2), err2);

        deviceCtx.Run();

        var snapshot = deviceCtx.GetRegistersSnapshot()!;
        Assert.Equal(0x55UL, snapshot[RegType.r5]);
    }

    // ------------------------------------------------------------------
    // 3. Перезапуск после ProcessorFault
    // ------------------------------------------------------------------

    [Fact]
    public void MultipleRuns_RestartAfterFault_ExecutesNewProgram()
    {
        using var host = VmHostFactory.CreateDefault();
        var faultPolicy = new CapturingFaultPolicy();
        var deviceId = host.Emulator.RegisterDevice(
            DeviceSpec.Create(RamSize.MB16, processorFaultPolicy: faultPolicy, name: "cpu"),
            sector: 0);
        var deviceCtx = host.Emulator.GetDeviceContext(deviceId)!;

        // ====== Первая программа: заведомо невалидное обращение ======
        string program1 = """
            LDI  r1, 0xF0
            WAKE_INT r1
            END
            """;

        var comp1 = host.Project
            .CompileToIR(program1, 0, false, SourceLanguage.Nanasm)
            .Build();
        Assert.True(comp1.Success);
        Assert.True(deviceCtx.TryLoadProgram(comp1.Program!, 0, out var err1), err1);

        deviceCtx.Run();
        Assert.NotEmpty(faultPolicy.Faults);
        faultPolicy.Faults.Clear();

        // ====== Вторая программа: корректная ======
        string program2 = """
            LDI  r0, 0x77
            END
            """;

        var comp2 = host.Project
            .CompileToIR(program2, 0, false, SourceLanguage.Nanasm)
            .Build();
        Assert.True(comp2.Success);
        Assert.True(deviceCtx.TryLoadProgram(comp2.Program!, 0, out var err2), err2);

        deviceCtx.Run();

        Assert.Empty(faultPolicy.Faults);
        var snapshot = deviceCtx.GetRegistersSnapshot()!;
        Assert.Equal(0x77UL, snapshot[RegType.r0]);
    }

    // ------------------------------------------------------------------
    // 4. Несколько CPU-устройств на одной ВМ
    // ------------------------------------------------------------------

    [Fact]
    public void MultipleCpuDevices_RunIndependentlyWithSeparatePrograms()
    {
        using var host = VmHostFactory.CreateDefault();

        var deviceIdA = host.Emulator.RegisterDevice(
            DeviceSpec.Create(RamSize.MB16, name: "cpuA"), sector: 0);
        var deviceIdB = host.Emulator.RegisterDevice(
            DeviceSpec.Create(RamSize.MB16, name: "cpuB"), sector: 1);

        var ctxA = host.Emulator.GetDeviceContext(deviceIdA)!;
        var ctxB = host.Emulator.GetDeviceContext(deviceIdB)!;

        // ====== Программы для каждого CPU ======
        string progA = """
            LDI  r0, 0xAAA
            END
            """;
        string progB = """
            LDI  r0, 0xBBB
            END
            """;

        var compA = host.Project
            .CompileToIR(progA, 0, false, SourceLanguage.Nanasm)
            .Build();
        var compB = host.Project
            .CompileToIR(progB, 0, false, SourceLanguage.Nanasm)
            .Build();

        Assert.True(compA.Success && compB.Success);
        Assert.True(ctxA.TryLoadProgram(compA.Program!, 0, out var eA), eA);
        Assert.True(ctxB.TryLoadProgram(compB.Program!, 0, out var eB), eB);

        ctxA.Run();
        ctxB.Run();

        var snapA = ctxA.GetRegistersSnapshot()!;
        var snapB = ctxB.GetRegistersSnapshot()!;

        Assert.Equal(0xAAUL * 16 + 0xAUL, snapA[RegType.r0]); // 0xAAA
        Assert.Equal(0xBBUL * 16 + 0xBUL, snapB[RegType.r0]); // 0xBBB
    }

    // ------------------------------------------------------------------
    // 5. Проверка перезаписи регистров при повторной загрузке программы
    // ------------------------------------------------------------------

    [Fact]
    public void MultipleRuns_RegisterOverwrite_NewProgramOverwritesAffectedRegisters()
    {
        using var host = VmHostFactory.CreateDefault();
        var deviceId = host.Emulator.RegisterDevice(
            DeviceSpec.Create(RamSize.MB16, name: "cpu"), sector: 0);
        var deviceCtx = host.Emulator.GetDeviceContext(deviceId)!;

        // ====== Первая программа ======
        string program1 = """
            LDI  r0, 0x11
            LDI  r1, 0x22
            END
            """;

        var comp1 = host.Project
            .CompileToIR(program1, 0, false, SourceLanguage.Nanasm)
            .Build();
        Assert.True(comp1.Success);
        Assert.True(deviceCtx.TryLoadProgram(comp1.Program!, 0, out var err1), err1);

        deviceCtx.Run();

        var snap1 = deviceCtx.GetRegistersSnapshot()!;
        Assert.Equal(0x11UL, snap1[RegType.r0]);
        Assert.Equal(0x22UL, snap1[RegType.r1]);

        // ====== Вторая программа ======
        string program2 = """
            LDI  r0, 0xFF
            END
            """;

        var comp2 = host.Project
            .CompileToIR(program2, 0, false, SourceLanguage.Nanasm)
            .Build();
        Assert.True(comp2.Success);
        Assert.True(deviceCtx.TryLoadProgram(comp2.Program!, 0, out var err2), err2);

        deviceCtx.Run();

        var snap2 = deviceCtx.GetRegistersSnapshot()!;
        Assert.Equal(0xFFUL, snap2[RegType.r0]);
        // Проверка семантики: сбрасывается ли r1 при перезагрузке программы
        Assert.Equal(0UL, snap2[RegType.r1]);
    }

    // ------------------------------------------------------------------
    // 6. Циклический перезапуск (stress)
    // ------------------------------------------------------------------

    [Fact]
    public void MultipleRuns_CyclicRestart_NoLeakOrHang()
    {
        using var host = VmHostFactory.CreateDefault();
        var deviceId = host.Emulator.RegisterDevice(
            DeviceSpec.Create(RamSize.MB16, name: "cpu"), sector: 0);
        var deviceCtx = host.Emulator.GetDeviceContext(deviceId)!;

        const int iterations = 20;

        for (int i = 0; i < iterations; i++)
        {
            byte value = (byte)(i + 1);
            string program = $"""
                LDI  r0, 0x{value:X2}
                END
                """;

            var comp = host.Project
                .CompileToIR(program, 0, false, SourceLanguage.Nanasm)
                .Build();
            Assert.True(comp.Success);
            Assert.True(deviceCtx.TryLoadProgram(comp.Program!, 0, out var err), err);

            deviceCtx.Run();

            var snap = deviceCtx.GetRegistersSnapshot()!;
            Assert.Equal((ulong)value, snap[RegType.r0]);
        }
    }
}