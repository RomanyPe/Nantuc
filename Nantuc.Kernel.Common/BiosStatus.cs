namespace Nantuc.Kernel.Common;

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

    /// <summary> Нереализованная команда OpCode </summary>
    NotImplementedOpCode,

    /// <summary> Отсутсвует устройство для ввода </summary>
    NullDeviceInput,

    /// <summary> Отсутсвует устройство для вывода </summary>
    NullDeviceOutput,

    /// <summary> Программа ушла в бесконечный цикл </summary>
    InfinityLoop,

    /// <summary> Деление на 0 </summary>
    DivOnZero,
    /// <summary>
    /// данный <see cref="OpCode"/> не поддерживается
    /// </summary>
    NotSupportedOpCode,
    /// <summary>
    /// Переполнение стека вниз
    /// </summary>
    StackUnderflow
}

/// <summary>
/// Причина остановки устройства
/// </summary>
/// <param name="Status">последний статус системы</param>
/// <param name="FaultData">данные полученные при падение</param>
/// <param name="InstructionPointer">указатель на инструкцию</param>
/// <param name="OpCode">код операции при которой это произошло (NOP - не инициализировано)</param>
public readonly record struct ProcessorFault(
    BiosStatus Status,
    ulong FaultData,
    ulong InstructionPointer,
    OpCode OpCode);

