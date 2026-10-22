using Nantuc.Kernel.Common;
using Nantuc.VmApplication;
using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Extensions.Host;
using Nantuc.VmApplication.Project;

namespace Nantuc.Tests;

public class TestCompile
{
    [Theory]
    [InlineData(
        """
        int y[] = { 1, 2, 4, 8, 16 };
        int main() { return y[0] + y[1] + y[2] + y[3]; }
        """, 15)]
    [InlineData(
        """
        int y[] = { 1, 2, 4, 8 };
        int main() { return (y[0]+y[1]) * (y[2]+y[3]); }   // 3 * 12 = 36
        """, 36)]
    [InlineData(
        """
        int y[] = { 1, 2, 4, 8, 16 };
        int main() { return y[0] + (y[1] + (y[2] + (y[3] + y[4]))); }
        """, 31)]
    [InlineData(
        """
        int y[] = { 10, 20, 30, 40 };
        int i[] = { 3, 2, 1 };
        int main() { return y[i[0]] + y[i[1]]; }
        """, 70)]
    [InlineData(
        """
        int y[] = { 1, 2, 3 };
        int main() { int x = y[0] + y[2]; return x * 2; }   // (1+3)*2 = 8
        """, 8)]
    [InlineData(
        """
        int g[] = { 100, 200 };
        int main() { int l[] = { 10, 20 }; return l[0] + g[1]; }   // 210
        """, 210)]
    [InlineData(
        """
        int y[] = { 10, 20 };
        int add(int a, int b) { return a + b; }
        int main() { return add(y[0], y[1]); } 
        """, 30)]
    [InlineData(
        """
        int y[] = { 5, 7 };
        int id(int x) { return x; }
        int main() { if (y[0] < id(y[1])) return 1; return 0; }
        """, 1)]
    [InlineData(
        """
        int y[] = { 0, 1, 2, 3 };
        int i[] = { 3, 1, 0 };
        int main() { y[i[0]] = 99; return y[3]; }   // 99
        """, 99)]
    [InlineData(
        """
        int main(){
            return foo(42);
        }

        int foo(int c){
            return c;
        }
        """, 42)]
    public void RunCompile(string program, ulong res)
    {
        using var host = VMHostFactory.CreateDefault();
        var handle = DeviceSpec.Create(RamSize.MB16, name: "test");
        var deviceId = host.Emulator.RegisterDevice(handle, sector: 0);
        var deviceCtx = host.Emulator.GetDeviceContext(deviceId)!;

        var comp = host.Project.CompileToIL(program, 0, false, SourceLanguage.C).Build();
        Assert.True(comp.Success, string.Join("\n", comp.Errors ?? []));
        Assert.True(deviceCtx.TryFastLoadProgram(comp.Program!, 0, out var err), err);

        deviceCtx.Run();
        var regs = deviceCtx.GetRegistersSnapshot();
        Assert.Equal(res, regs[RegType.r1]);
    }
}