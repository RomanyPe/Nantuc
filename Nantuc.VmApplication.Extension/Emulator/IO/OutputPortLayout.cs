namespace Nantuc.VmApplication.Extensions.Emulator.IO;

/// <summary>
/// Раскладка портов устройства вывода. Устройство занимает 8 байт.
/// </summary>
public static class OutputPortLayout
{
    public const ulong WindowSize = 8;

    /// <summary>Запись байта на вывод. Чтение возвращает 0.</summary>
    public const ulong Data = 0;

    /// <summary>Статус: 0 — готово, 1 — есть необработанные данные в очереди.</summary>
    public const ulong Status = 1;

    /// <summary>Запись 1 — сбросить буфер вывода (для Queued — дождаться опустошения очереди).</summary>
    public const ulong Flush = 2;
}
