using Nantuc.Common;
using System.Runtime.CompilerServices;

namespace Nantuc.Kernel.ProcessorSystem;

public sealed partial class Processor
{
    // ============================================================
    // Обработка сбоев.
    // ============================================================

    private BiosStatus RecordFaultContext(BiosStatus s, ulong data)
    {
        _lastFaultData = data;
        return s;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private BiosStatus SetStatusNotImplementedOpCode(OpCode code)
    {
        _lastFaultData = (ulong)code;
        return BiosStatus.NotImplementedOpCode;
    }

    private static bool IsTerminal(BiosStatus s) =>
        s is BiosStatus.EndProgram or BiosStatus.NotImplementedOpCode
          or BiosStatus.NotSupportedOpCode or BiosStatus.UnsafeOpCodeForbidden
          or BiosStatus.HostStopped or BiosStatus.InfinityLoop;
}