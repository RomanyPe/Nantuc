namespace Nantuc.Kernel.Common;

/// <summary>
/// Результат декодирования одного 32-битного слова инструкции: набор полей,
/// извлечённых декодером (<c>InstructionDecoder</c>) для последующего исполнения процессором.
/// </summary>
/// <remarks>
/// <para>
/// Экземпляр является неизменяемым снимком разобранной инструкции: опкод, два регистра-операнда
/// и код размера данных. Непосредственные 64-битные операнды (адреса, константы) сюда не входят —
/// они читаются процессором из памяти отдельно, сразу за словом инструкции.
/// </para>
/// <para>
/// Поля <paramref name="Reg1"/> и <paramref name="Reg2"/> имеют смысл только для опкодов,
/// которые используют соответствующие операнды; для остальных они содержат значения,
/// извлечённые из битов слова, и не должны интерпретироваться.
/// </para>
/// </remarks>
/// <param name="OpCode">Опкод инструкции (см. <see cref="OpCode"/>).</param>
/// <param name="Reg1">Первый регистр-операнд (обычно приёмник/левый операнд).</param>
/// <param name="Reg2">Второй регистр-операнд (обычно источник/правый операнд).</param>
/// <param name="DataSizeCode">
/// Код разрядности операнда памяти/регистра (см. <see cref="OpCodeSize"/>).
/// Используется только опкодами группы загрузки-сохранения
/// (<see cref="OpCode.LOAD"/>, <see cref="OpCode.STORE"/>, <see cref="OpCode.LOAD_IND"/>,
/// <see cref="OpCode.STORE_IND"/> и их <c>_UNSAFE</c>-вариантами).
/// </param>
public readonly record struct DecodeInstructionResult(OpCode OpCode,
                                                      RegType Reg1,
                                                      RegType Reg2,
                                                      OpCodeSize DataSizeCode);