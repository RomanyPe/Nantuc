using Nantuc.Common;

namespace Nantuc.VmApplication.Extensions.Debugging;

/// <summary>
/// Снимок останова: причина, состояние процессора и последний <see cref="ProcessorFault"/>.
/// </summary>
public readonly record struct DebugStop(
    DebugStopReason Reason,
    ulong InstructionPointer,
    long StepCount,
    ProcessorSnapshot Snapshot,
    ProcessorFault Fault)
{
    /// <summary>
    /// <c>true</c>, если программа больше не может исполняться
    /// (<see cref="DebugStopReason.Completed"/>, <see cref="DebugStopReason.Faulted"/> или <see cref="DebugStopReason.Error"/>).
    /// </summary>
    public bool IsTerminal => Reason is DebugStopReason.Completed
                                     or DebugStopReason.Faulted
                                     or DebugStopReason.Error;

    private static string AsText(DebugStopReason r) => r switch
    {
        DebugStopReason.None => nameof(DebugStopReason.None),
        DebugStopReason.StepComplete => nameof(DebugStopReason.StepComplete),
        DebugStopReason.Breakpoint => nameof(DebugStopReason.Breakpoint),
        DebugStopReason.Completed => nameof(DebugStopReason.Completed),
        DebugStopReason.Faulted => nameof(DebugStopReason.Faulted),
        DebugStopReason.Sleeping => nameof(DebugStopReason.Sleeping),
        DebugStopReason.StepLimit => nameof(DebugStopReason.StepLimit),
        DebugStopReason.UserStop => nameof(DebugStopReason.UserStop),
        DebugStopReason.Error => nameof(DebugStopReason.Error),
        _ => ((int)r).ToString(),
    };
    /// <inheritdoc />
    public override string ToString()
        => $"[{AsText(Reason)}] IP=0x{InstructionPointer:X8} step={StepCount} " +
           $"fault={Fault.Status.AsText} op={Snapshot.OpCode.AsText}";
}
