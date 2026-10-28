namespace Nantuc.Kernel.Common;

/// <summary>
/// Статус Id присланного от системы
/// </summary>
public enum IdState
{
    Valid,
    NotAssigned,   // -1
    OutOfRange,    // -2
    NoFreeSlot,    // -3
    Rejected,      // -4
    Unknown
}