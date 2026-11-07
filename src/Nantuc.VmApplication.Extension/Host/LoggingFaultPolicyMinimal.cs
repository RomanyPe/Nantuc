using Nantuc.Common;
using Nantuc.Contracts;

namespace Nantuc.VmApplication.Extensions.Host;

public class LoggingFaultPolicyMinimal(TextWriter output) : IProcessorFaultPolicy
{
    private readonly TextWriter _out = output;

    public bool ShouldContinue(in ProcessorFault fault)
    {
        _out.WriteLine(
            $"[FAULT] status={fault.Status}, " +
            $"data=0x{fault.FaultData:X}, " +
            $"ip=0x{fault.InstructionPointer:X}, " +
            $"op={fault.OpCode}");
        _out.Flush();

        return false; // останавливаем симуляцию
    }
}