namespace Nantuc.Kernel.Common;

public enum IdState
{
    Valid,
    NotAssigned,   // -1
    OutOfRange,    // -2
    NoFreeSlot,    // -3
    Rejected,      // -4
    Unknown
}