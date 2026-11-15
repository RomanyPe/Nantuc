using Nantuc.Common;
using Nantuc.Contracts;
using Nantuc.Kernel.ControllersData;
using static Nantuc.Common.InstructionDecoder;

namespace Nantuc.Kernel.ProcessorSystem;

public sealed partial class Processor
{
    // ============================================================
    // Снимок состояния для внешних наблюдателей.
    // ============================================================

    public ProcessorSnapshot CurrentProcessorSnapshot
    {
        get
        {
            Span<ulong> arr = stackalloc ulong[32];
            CopyRegisters(arr);
            var inst = _ram.ReadInt32LE(_registers[RegType.rIP.Int()]);
            var status = _ram.UpdateAndReadStatus();
            if (status != BiosStatus.Success)
                return new(arr, _isRunning, _isSleeping, inst, _ram.FaultData, status);

            ulong data = 0;
            if (HasNeed64IntData(GetOpCode(inst)))
            {
                ulong ip = _registers[RegType.rIP.Int()];
                ip = (ip + 7) & ~7UL;
                data = _ram.ReadInt64LE(ip);
                status = _ram.UpdateAndReadStatus();
                if (status != BiosStatus.Success)
                    return new(arr, _isRunning, _isSleeping, inst, _ram.FaultData, status);
            }
            return new(arr, IsRunning, IsSleeping, inst, data);
        }
    }
}