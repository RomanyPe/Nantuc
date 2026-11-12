using Nantuc.Common;
using Nantuc.Contracts;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Extensions.Disassembler;
using Nantuc.VmApplication.Extensions.Host;
using Nantuc.VmApplication.Project;
using Xunit.Abstractions;

namespace Nantuc.Tests.Vm;

public class UnsafeOpCodeTests(ITestOutputHelper outPut)
{
    protected readonly ITestOutputHelper _outPut = outPut;

    private const string UnsafeProgram = """
        LDI r0, 0x80;
        STORE_UNSAFE.8 r0, 0x100;
        NOP;
        END;
        """;

    [Fact]
    public void Run_WithAllowUnsafeOpCodesFalse_ReportsUnsafeOpCodeForbidden()
    {
        using var host = VmHostFactory.CreateDefault();
        var (deviceCtx, _) = RegisterVm(host, false);
        LoadProgram(host, deviceCtx, UnsafeProgram);

        // Предполагаем, что LaunchOptions имеет параметр AllowUnsafeOpCodes.
        var fault = deviceCtx.Run();

        Assert.Equal(BiosStatus.UnsafeOpCodeForbidden, fault.Status);
        Assert.Equal(OpCode.STORE_UNSAFE, fault.OpCode); // проверяем, что зафиксирован именно небезопасный опкод
    }

    [Fact]
    public void Run_WithAllowUnsafeOpCodesTrue_ExecutesInstruction()
    {
        using var host = VmHostFactory.CreateDefault();
        var (deviceCtx, _) = RegisterVm(host, true);
        LoadProgram(host, deviceCtx, UnsafeProgram);

        var fault = deviceCtx.Run();

        // INT выполнился, программа дошла до END.
        Assert.Equal(BiosStatus.EndProgram, fault.Status);
    }

    [Fact]
    public void DebugRun_WithAllowUnsafeOpCodesFalse_ReportsUnsafeOpCodeForbidden()
    {
        using var host = VmHostFactory.CreateDefault();
        var (deviceCtx, _) = RegisterVm(host, false);
        LoadProgram(host, deviceCtx, UnsafeProgram);

        var options = new LaunchOptionsDebug();
        var fault = deviceCtx.DebugRun(options);

        Assert.Equal(BiosStatus.UnsafeOpCodeForbidden, fault.Status);
        Assert.Equal(OpCode.STORE_UNSAFE, fault.OpCode);
    }

    [Fact]
    public void SingleStep_WithAllowUnsafeOpCodesFalse_StopsAtUnsafeOpCode()
    {
        using var host = VmHostFactory.CreateDefault();
        var (deviceCtx, _) = RegisterVm(host, false);
        LoadProgram(host, deviceCtx, UnsafeProgram);

        // В пошаговом режиме флаг передаётся через RunStepMode.
        deviceCtx.RunStepMode(startAddress: 0);

        // Выполняем шаги, пока не дойдём до INT.
        for (int i = 0; i < 10; i++)
        {
            deviceCtx.SingleStep();
            if (deviceCtx.GetLastFault().Status == BiosStatus.UnsafeOpCodeForbidden)
                break;
        }

        var fault = deviceCtx.GetLastFault();
        deviceCtx.EndStepMode();

        Assert.Equal(BiosStatus.UnsafeOpCodeForbidden, fault.Status);
        Assert.Equal(OpCode.STORE_UNSAFE, fault.OpCode);
    }

    // ------------------------------------------------------------------
    // Вспомогательные методы
    // ------------------------------------------------------------------

    private (DeviceContext Ctx, int DeviceId) RegisterVm(VmHost host, bool allowUnsafe)
    {
        var spec = DeviceSpec.Create(RamSize.KB16, name: "test-vm", processorFaultPolicy: new MockProcessorPolicy(allowUnsafe, _outPut));
        var state = host.Emulator.TryRegisterDevice(spec, sector: 0, out int id);
        Assert.Equal(IdState.Valid, state);
        var ctx = host.Emulator.GetDeviceContext(id);
        Assert.NotNull(ctx);
        return (ctx, id);
    }

    private void LoadProgram(VmHost host, DeviceContext ctx, string asm)
    {
        var comp = host.Project.Compile(asm, 0, false, SourceLanguage.Asm);
        Assert.True(comp.Success, string.Join("\n", comp.Errors ?? []));
        _outPut.WriteLine(comp.DisassembleToString());
        Assert.True(ctx.TryLoadProgram(comp.Program!, 0, out var err), err);
    }

    private class MockProcessorPolicy(bool allowUnsafe, ITestOutputHelper outPut) : IProcessorPolicy
    {
        public bool AllowUnsafeOpCodes => allowUnsafe;

        public bool ShouldContinue(in ProcessorFault fault)
        {
            outPut.WriteLine(fault.ToString());
            return false;
        }
    }
}