using Nantuc.Common;
using Nantuc.Contracts;

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

    /// <inheritdoc />
    public override string ToString()
        => $"[{Reason}] IP=0x{InstructionPointer:X8} step={StepCount} " +
           $"fault={Fault.Status} op={Snapshot.OpCode}";
}
