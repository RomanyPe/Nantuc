namespace Nantuc.Kernel.Common;

/// <summary>
/// Идентификатор регистра процессора. Используется как индекс в массиве регистров
/// и как операнд в декодированной инструкции.
/// </summary>
/// <remarks>
/// <para>
/// Процессор содержит 32 регистра по 64 бита (<c>ArrayRegisters</c> — <c>[InlineArray(32)]</c>
/// из <c>ulong</c>). Значения перечисления обязаны быть непрерывными в диапазоне
/// <c>0..31</c>: они напрямую индексируют массив без дополнительной проверки
/// (кроме <c>ProcessorReader.GetRegister</c>, который проверяет границу).
/// </para>
/// <para>
/// <b>Не меняйте порядок и не удаляйте члены</b> без одновременного обновления
/// <c>Processor.CountReg</c>, декодера (<c>InstructionDecoder.GetReg1/GetReg2</c>) и
/// любых сохранённых образов/сериализованных значений. Новые регистры добавляйте
/// только в конец, до <see cref="rFL"/>, и синхронно правьте <c>CountReg</c>.
/// </para>
/// <para>
/// Регистры <c>r0..r27</c> — регистры общего назначения. В отличие от MIPS/RISC-V,
/// <b>нет</b> аппаратно-занулённого регистра: для обнуления используйте <c>XOR rN, rN</c>.
/// </para>
/// <para>
/// Специальные регистры (<see cref="rFL"/>, <see cref="rSP"/>, <see cref="rHP"/>, <see cref="rIP"/>)
/// занимают старшие индексы <c>28..31</c>. Их назначение описано в документации членов.
/// </para>
/// </remarks>
public enum RegType : byte
{
    /// <summary>Регистр общего назначения r0.</summary>
    r0,
    /// <summary>Регистр общего назначения r1.</summary>
    r1,
    /// <summary>Регистр общего назначения r2.</summary>
    r2,
    /// <summary>Регистр общего назначения r3.</summary>
    r3,
    /// <summary>Регистр общего назначения r4.</summary>
    r4,
    /// <summary>Регистр общего назначения r5.</summary>
    r5,
    /// <summary>Регистр общего назначения r6.</summary>
    r6,
    /// <summary>Регистр общего назначения r7.</summary>
    r7,
    /// <summary>Регистр общего назначения r8.</summary>
    r8,
    /// <summary>Регистр общего назначения r9.</summary>
    r9,
    /// <summary>Регистр общего назначения r10.</summary>
    r10,
    /// <summary>Регистр общего назначения r11.</summary>
    r11,
    /// <summary>Регистр общего назначения r12.</summary>
    r12,
    /// <summary>Регистр общего назначения r13.</summary>
    r13,
    /// <summary>Регистр общего назначения r14.</summary>
    r14,
    /// <summary>Регистр общего назначения r15.</summary>
    r15,
    /// <summary>Регистр общего назначения r16.</summary>
    r16,
    /// <summary>Регистр общего назначения r17.</summary>
    r17,
    /// <summary>Регистр общего назначения r18.</summary>
    r18,
    /// <summary>Регистр общего назначения r19.</summary>
    r19,
    /// <summary>Регистр общего назначения r20.</summary>
    r20,
    /// <summary>Регистр общего назначения r21.</summary>
    r21,
    /// <summary>Регистр общего назначения r22.</summary>
    r22,
    /// <summary>Регистр общего назначения r23.</summary>
    r23,
    /// <summary>Регистр общего назначения r24.</summary>
    r24,
    /// <summary>Регистр общего назначения r25.</summary>
    r25,
    /// <summary>Регистр общего назначения r26.</summary>
    r26,
    /// <summary>Регистр общего назначения r27.</summary>
    r27,

    /// <summary>
    /// Регистр флагов (Flags Register).
    /// </summary>
    /// <remarks>
    /// Битовое поле:
    /// <list type="bullet">
    ///   <item><description>бит 0 — Zero (Z): результат последней <see cref="OpCode.CMP"/> / <see cref="OpCode.TEST"/> равен нулю;</description></item>
    ///   <item><description>бит 1 — Negative (N): старший бит результата установлен.</description></item>
    /// </list>
    /// Остальные биты зарезервированы и должны читаться как 0. Флаги обновляются
    /// только инструкциями <see cref="OpCode.CMP"/> и <see cref="OpCode.TEST"/>;
    /// арифметические и логические инструкции флаги не изменяют.
    /// Начальное значение — <c>0</c> (флаги сброшены).
    /// </remarks>
    rFL,

    /// <summary>
    /// Указатель стека (Stack Pointer).
    /// </summary>
    /// <remarks>
    /// Инициализируется в <c>Processor.LaunchProgramm</c> вершиной ОЗУ, выровненной
    /// по границе 8 байт. Уменьшается на 8 при <see cref="OpCode.PUSH"/> / <see cref="OpCode.CALL"/>
    /// и увеличивается на 8 при <see cref="OpCode.POP"/> / <see cref="OpCode.RET"/>.
    /// При коллизии с областью кучи (<see cref="rHP"/>) возвращается
    /// <see cref="BiosStatus.SegmentationFault"/>.
    /// </remarks>
    rSP,

    /// <summary>
    /// Указатель кучи (Heap Pointer).
    /// </summary>
    /// <remarks>
    /// Инициализируется через <c>Processor.InitRegHP</c>. Используется для обнаружения
    /// коллизии «стек ↔ куча»: если <c>rSP</c> опускается ниже <c>rHP</c>, операция
    /// считается ошибкой доступа к памяти.
    /// </remarks>
    rHP,

    /// <summary>
    /// Указатель инструкций (Instruction Pointer / Program Counter).
    /// </summary>
    /// <remarks>
    /// Содержит адрес следующей инструкции. В начале <c>Processor.Step</c> из памяти
    /// читается 32-битное слово инструкции по текущему <c>rIP</c>, после чего <c>rIP</c>
    /// увеличивается на 4. Непосредственные 64-битные операнды (адреса, константы)
    /// читаются отдельно и дополнительно сдвигают <c>rIP</c> с выравниванием по 8 байт.
    /// Изменяется инструкциями перехода/вызова/возврата
    /// (<see cref="OpCode.JMP"/>, <see cref="OpCode.JZ"/>, <see cref="OpCode.JNZ"/>,
    /// <see cref="OpCode.JG"/>, <see cref="OpCode.JL"/>, <see cref="OpCode.CALL"/>,
    /// <see cref="OpCode.CALL_IND"/>, <see cref="OpCode.RET"/> и др.).
    /// </remarks>
    rIP,
}