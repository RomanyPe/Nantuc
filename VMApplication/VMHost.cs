using VMApplication.Emulator;
using VMApplication.Project;

namespace VMApplication;

public sealed record VMHost(VMHostProject Project, VMEmulator Emulator) : IDisposable
{
    public void Dispose() => Emulator.Dispose();
}