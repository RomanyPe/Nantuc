namespace Kernel.Common;

public enum BiosStatus : byte
{
    /// <summary> Выполнено </summary>
    Success,
    /// <summary> Выход за границы ОЗУ </summary>
    SegmentationFault,

    /// <summary> Попытка прочесть int по невыровненному адресу (например, 0x03) </summary>
    AlignmentFault,

    /// <summary> Не ошибка, остановка программы </summary>
    EndProgramm,

    /// <summary> Неизвестная команда OpCode </summary>
    NotImplementedOpCode,

    NullDeviceInput,

    NullDeviceOutput,

    InfinityLoop,

    DivOnZero,
    NotSupportedOpCode,
    StackUnderflow
}

public readonly record struct ProcessorFault(
    BiosStatus Status,
    ulong FaultData,
    ulong InstructionPointer,
    OpCode OpCode);

