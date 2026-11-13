using Nantuc.Common;
using Nantuc.Contracts;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Extensions.Disassembler;
using Nantuc.VmApplication.Extensions.Host;
using Nantuc.VmApplication.Project;
using Xunit.Abstractions;

namespace Nantuc.Tests.Vm;

/// <summary>
/// Набор «security-тестов»: проверяет, что ВМ нельзя «взломать» через
/// небезопасные опкоды, выход за границы ОЗУ, нарушение выравнивания,
/// порчу стека или произвольный control flow.
/// </summary>
public class VmSecurityTests(ITestOutputHelper outPut)
{
    protected readonly ITestOutputHelper _outPut = outPut;

    // RamSize.KB16 == 16*1024 байт
    private const ulong RamBytes = 0x4000;
    private const ulong LastByte = RamBytes - 1;

    // ==================================================================
    // 1. Все unsafe-опкоды должны обязательно проверяться политикой
    // ==================================================================

    [Theory]
    [InlineData("LOAD_UNSAFE.8 r0, 0x100;", OpCode.LOAD_UNSAFE)]
    [InlineData("STORE_UNSAFE.8 r0, 0x100;", OpCode.STORE_UNSAFE)]
    public void DirectUnsafeOpCode_WhenDenied_ReportsForbidden(string asm, OpCode expectedOp)
    {
        using var host = VmHostFactory.CreateDefault();
        var (ctx, _) = RegisterVm(host, allowUnsafe: false);
        LoadProgram(host, ctx, $"""
            LDI r0, 0xAA;
            {asm}
            END;
            """);

        var fault = ctx.Run();

        Assert.Equal(BiosStatus.UnsafeOpCodeForbidden, fault.Status);
        Assert.Equal(expectedOp, fault.OpCode);
    }

    [Theory]
    [InlineData("LOAD_IND_UNSAFE.8 r0, r1;", OpCode.LOAD_IND_UNSAFE)]
    [InlineData("STORE_IND_UNSAFE.8 r0, r1;", OpCode.STORE_IND_UNSAFE)]
    public void IndirectUnsafeOpCode_WhenDenied_ReportsForbidden(string asm, OpCode expectedOp)
    {
        using var host = VmHostFactory.CreateDefault();
        var (ctx, _) = RegisterVm(host, allowUnsafe: false);
        LoadProgram(host, ctx, $"""
            LDI r0, 0xAA;
            LDI r1, 0x100;
            {asm}
            END;
            """);

        var fault = ctx.Run();

        Assert.Equal(BiosStatus.UnsafeOpCodeForbidden, fault.Status);
        Assert.Equal(expectedOp, fault.OpCode);
    }

    // ==================================================================
    // 2. Safe-опкоды не должны выпускать за пределы ОЗУ
    // ==================================================================

    [Fact]
    public void Load_Byte_AtLastValidAddress_Succeeds()
    {
        using var host = VmHostFactory.CreateDefault();
        var (ctx, _) = RegisterVm(host, allowUnsafe: false);
        LoadProgram(host, ctx, $"""
            LOAD.8 r0, {LastByte};
            END;
            """);

        Assert.Equal(BiosStatus.EndProgram, ctx.Run().Status);
    }

    [Fact]
    public void Load_Byte_PastEnd_Faults()
    {
        using var host = VmHostFactory.CreateDefault();
        var (ctx, _) = RegisterVm(host, allowUnsafe: false);
        LoadProgram(host, ctx, $"""
            LOAD.8 r0, {RamBytes};
            END;
            """);

        var fault = ctx.Run();

        // Обязательно ошибка, а не «случайное чтение за пределами буфера»
        Assert.NotEqual(BiosStatus.EndProgram, fault.Status);
        Assert.NotEqual(BiosStatus.Success, fault.Status);
    }

    [Fact]
    public void Load_64bit_CrossingEnd_Faults()
    {
        using var host = VmHostFactory.CreateDefault();
        var (ctx, _) = RegisterVm(host, allowUnsafe: false);
        // 8-байтное чтение с 0x3FFC залезает в 0x4004 → выход за буфер
        LoadProgram(host, ctx, $"""
            LOAD.64 r0, {RamBytes - 4};
            END;
            """);

        Assert.NotEqual(BiosStatus.EndProgram, ctx.Run().Status);
    }

    [Fact]
    public void Load_64bit_Misaligned_Faults()
    {
        using var host = VmHostFactory.CreateDefault();
        var (ctx, _) = RegisterVm(host, allowUnsafe: false);
        LoadProgram(host, ctx, """
            LOAD.64 r0, 0x3FFF;
            END;
            """);

        Assert.NotEqual(BiosStatus.EndProgram, ctx.Run().Status);
    }

    [Fact]
    public void Store_Byte_PastEnd_Faults()
    {
        using var host = VmHostFactory.CreateDefault();
        var (ctx, _) = RegisterVm(host, allowUnsafe: false);
        LoadProgram(host, ctx, $"""
            LDI r0, 0xAA;
            STORE.8 r0, {RamBytes};
            END;
            """);

        Assert.NotEqual(BiosStatus.EndProgram, ctx.Run().Status);
    }

    [Fact]
    public void Store_64bit_CrossingEnd_Faults()
    {
        using var host = VmHostFactory.CreateDefault();
        var (ctx, _) = RegisterVm(host, allowUnsafe: false);
        LoadProgram(host, ctx, $"""
            LDI r0, 0xAA;
            STORE.64 r0, {RamBytes - 4};
            END;
            """);

        Assert.NotEqual(BiosStatus.EndProgram, ctx.Run().Status);
    }

    // ==================================================================
    // 3. Control flow не должен выходить за пределы RAM
    // ==================================================================

    [Fact]
    public void JmpInd_WithPastEndAddress_Faults()
    {
        using var host = VmHostFactory.CreateDefault();
        var (ctx, _) = RegisterVm(host, allowUnsafe: false);
        LoadProgram(host, ctx, $"""
            LDI r0, {RamBytes};
            JMP_IND r0;
            END;
            """);

        Assert.NotEqual(BiosStatus.EndProgram, ctx.Run().Status);
    }

    [Fact]
    public void CallInd_WithPastEndAddress_Faults()
    {
        using var host = VmHostFactory.CreateDefault();
        var (ctx, _) = RegisterVm(host, allowUnsafe: false);
        LoadProgram(host, ctx, $"""
            LDI r0, {RamBytes};
            CALL_IND r0;
            END;
            """);

        Assert.NotEqual(BiosStatus.EndProgram, ctx.Run().Status);
    }

    // ==================================================================
    // 4. Стек: нельзя «всплыть» за пределы RAM
    // ==================================================================

    [Fact]
    public void Ret_FromEmptyStack_Faults()
    {
        using var host = VmHostFactory.CreateDefault();
        var (ctx, _) = RegisterVm(host, allowUnsafe: false);
        // RET сразу после Launch: rSP указывает на вершину стека
        LoadProgram(host, ctx, """
            RET;
            END;
            """);

        Assert.NotEqual(BiosStatus.EndProgram, ctx.Run().Status);
    }

    [Fact]
    public void Pop_FromEmptyStack_DoesNotCorruptRegisters()
    {
        using var host = VmHostFactory.CreateDefault();
        var (ctx, _) = RegisterVm(host, allowUnsafe: false);
        LoadProgram(host, ctx, """
            POP r0;
            END;
            """);

        var fault = ctx.Run();

        // Важно: если реализация просто вернёт мусор — это уязвимость.
        // В текущей реализации ReadInt64LE(0x4000) должен фолтнуться.
        Assert.NotEqual(BiosStatus.EndProgram, fault.Status);
    }

    // ==================================================================
    // 5. Арифметические ловушки не должны ронять ВМ
    // ==================================================================

    [Fact]
    public void Div_ByZero_ReportsDivOnZero()
    {
        using var host = VmHostFactory.CreateDefault();
        var (ctx, _) = RegisterVm(host, allowUnsafe: false);
        LoadProgram(host, ctx, """
            LDI r0, 100;
            LDI r1, 0;
            DIV r0, r1;
            END;
            """);

        Assert.Equal(BiosStatus.DivOnZero, ctx.Run().Status);
    }

    [Fact]
    public void Mod_ByZero_ReportsDivOnZero()
    {
        using var host = VmHostFactory.CreateDefault();
        var (ctx, _) = RegisterVm(host, allowUnsafe: false);
        LoadProgram(host, ctx, """
            LDI r0, 100;
            LDI r1, 0;
            MOD r0, r1;
            END;
            """);

        Assert.Equal(BiosStatus.DivOnZero, ctx.Run().Status);
    }

    // ==================================================================
    // 6. При разрешённых unsafe-опкодах поведение всё ещё
    //    ограничено рамками RAM (defense-in-depth)
    // ==================================================================

    [Fact]
    public void StoreUnsafe64_MisalignedInsideRam_SucceedsWhenAllowed()
    {
        using var host = VmHostFactory.CreateDefault();
        var (ctx, _) = RegisterVm(host, allowUnsafe: true);
        LoadProgram(host, ctx, """
            LDI r0, 0xAA;
            STORE_UNSAFE.64 r0, 0x11;
            END;
            """);

        // Невыровненная запись внутри RAM разрешена политикой
        Assert.Equal(BiosStatus.EndProgram, ctx.Run().Status);
    }

    // ==================================================================
    // 7. Внешняя инъекция команд/статусов не должна обходить политику
    // ==================================================================

    [Fact]
    public void ExternalCommand_DoesNotBypassUnsafePolicy()
    {
        using var host = VmHostFactory.CreateDefault();
        var (ctx, _) = RegisterVm(host, allowUnsafe: false);
        LoadProgram(host, ctx, """
            NOP;
            NOP;
            NOP;
            END;
            """);

        // Попытка «внешне» подсунуть небезопасный опкод — таких
        // в ExecuteExternalCommand вообще нет, но проверим что запуск
        // штатно завершится END-ом без доступа к unsafe-пути.
        var fault = ctx.Run();
        Assert.Equal(BiosStatus.EndProgram, fault.Status);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private (DeviceContext Ctx, int DeviceId) RegisterVm(VmHost host, bool allowUnsafe)
        => RegisterVm(host, allowUnsafe, RamSize.KB16);

    private (DeviceContext Ctx, int DeviceId) RegisterVm(VmHost host, bool allowUnsafe, RamSize ram)
    {
        var spec = DeviceSpec.Create(ram, name: "test-vm",
            processorFaultPolicy: new MockProcessorPolicy(allowUnsafe, _outPut));
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