using Nantuc.Kernel.Common;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Extensions.Disassembler;
using Nantuc.VmApplication.Extensions.Host;
using Nantuc.VmApplication.Project;
using Xunit.Abstractions;

namespace Nantuc.Tests.Compiler;

public abstract class CompileTestBase(ITestOutputHelper outPut)
{
    protected readonly ITestOutputHelper _outPut = outPut;

    protected void RunCompile(string program, ulong res)
    {
        using var host = VmHostFactory.CreateDefault();
        var handle = DeviceSpec.Create(RamSize.MB16, name: "test");
        var deviceId = host.Emulator.RegisterDevice(handle, sector: 0);
        var deviceCtx = host.Emulator.GetDeviceContext(deviceId)!;

        var comp = host.Project.CompileToIL(program, 0, false, SourceLanguage.C).Build();
        if (!comp.Success)
        {
            _outPut.WriteLine(string.Join("\n", comp.Errors ?? []));
            _outPut.WriteLine(program);
        }
        Assert.True(comp.Success);
        Assert.True(deviceCtx.TryLoadProgram(comp.Program!, 0, out var err), err);

        deviceCtx.Run();
        var regs = deviceCtx.GetRegistersSnapshot();
        bool v = res == regs[RegType.r0];

        if (!v)
        {
            _outPut.WriteLine($"Expected: {res}");
            _outPut.WriteLine($"Actual: {regs[RegType.r0]}");
            _outPut.WriteLine("--- MiniC ---");
            _outPut.WriteLine(program);
            _outPut.WriteLine("--- NANASM ---");
            _outPut.WriteLine(comp.DisassembleToString());
        }

        Assert.True(v);
    }

    protected void RunCompile_ConstViolation(string program)
    {
        using var host = VmHostFactory.CreateDefault();
        var handle = DeviceSpec.Create(RamSize.MB16, name: "test");
        var deviceId = host.Emulator.RegisterDevice(handle, sector: 0);
        var deviceCtx = host.Emulator.GetDeviceContext(deviceId)!;

        var comp = host.Project.CompileToIL(program, 0, false, SourceLanguage.C).Build();

        if (comp.Success)
        {
            _outPut.WriteLine("Expected compilation to FAIL, but it succeeded:");
            _outPut.WriteLine(program);
            _outPut.WriteLine("--- NANASM ---");
            _outPut.WriteLine(comp.DisassembleToString());
        }

        Assert.False(comp.Success, "Compilation unexpectedly succeeded — const violation not caught");
        Assert.NotNull(comp.Errors);
        Assert.NotEmpty(comp.Errors!);
    }
}
