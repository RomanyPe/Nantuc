using Nantuc.VmApplication.Emulator;
using Nantuc.VmApplication.Project;

namespace Nantuc.VmApplication;
/// <summary>
/// Хост системы, пока что является удобным фасадом над <see cref="VmHostProject"/> и <see cref="VmEmulator"/>
/// </summary>
/// <param name="Project">Менеджер проекта</param>
/// <param name="Emulator">Менеджер эмуляции</param>
public sealed record VmHost(VmHostProject Project, VmEmulator Emulator) : IDisposable
{
    /// <summary>
    /// Очистка ресурсов
    /// </summary>
    public void Dispose() => Emulator.Dispose();
}