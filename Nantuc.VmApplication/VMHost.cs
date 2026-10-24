using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Project;

namespace Nantuc.VmApplication;

public sealed record VmHost(VmHostProject Project, VmEmulator Emulator) : IDisposable
{
    public void Dispose() => Emulator.Dispose();
}