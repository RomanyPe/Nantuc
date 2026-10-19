using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Project;

namespace Nantuc.VmApplication;

public sealed record VMHost(VMHostProject Project, VMEmulator Emulator) : IDisposable
{
    public void Dispose() => Emulator.Dispose();
}