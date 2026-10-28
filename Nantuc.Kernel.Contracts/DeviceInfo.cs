namespace Nantuc.Kernel.Contracts;

/// <summary>
/// Структура информации о устройстве
/// </summary>
/// <param name="Id">Id в системе</param>
/// <param name="Sector">Сектор где зарегестрирован</param>
/// <param name="Name">Имя</param>
public readonly record struct DeviceInfo(int Id, uint Sector, string? Name);
