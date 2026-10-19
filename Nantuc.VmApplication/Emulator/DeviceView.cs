namespace Nantuc.VmApplication.Emulator;

public readonly struct DeviceView(int id,
                                  uint sector, 
                                  string? name = null)
{
    public int Id { get; init; } = id;
    public uint Sector { get; init; } = sector;
    public string? Name { get; init; } = name;

}