using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Nantuc.Common;

/// <summary>
/// Статический декодер 32-битных инструкций виртуальной машины.
/// Раскладывает сырое слово инструкции на поля: код операции, два регистра
/// и код размера данных; также предоставляет таблицу признака
/// «инструкция требует 64-битного операнда».
/// </summary>
/// <remarks>
/// <para>Раскладка сырой инструкции (<see cref="uint"/>, 32 бита):</para>
/// <list type="table">
///   <listheader>
///     <term>Биты</term>
///     <description>Поле</description>
///   </listheader>
///   <item><term>0–7</term><description><see cref="OpCode"/> (8 бит)</description></item>
///   <item><term>8–12</term><description><see cref="RegType"/> Reg1 (5 бит)</description></item>
///   <item><term>13–17</term><description><see cref="RegType"/> Reg2 (5 бит)</description></item>
///   <item><term>18–19</term><description><see cref="OpCodeSize"/> (2 бита)</description></item>
///   <item><term>20–31</term><description>Зарезервировано / непосредственный операнд</description></item>
/// </list>
/// <para>
/// Класс полностью управляемый: нет <c>unsafe</c>-кода, указателей и нативных
/// вызовов. Работает на всех целевых платформах .NET, включая
/// <b>WebAssembly</b> (в том числе в режиме <b>AOT</b>).
/// </para>
/// </remarks>
public static class InstructionDecoder
{
    /// <summary>
    /// Извлекает код операции из сырой инструкции (младшие 8 бит).
    /// </summary>
    /// <param name="rawInst">Сырое 32-битное слово инструкции.</param>
    /// <returns>Код операции (<see cref="OpCode"/>).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OpCode GetOpCode(uint rawInst) =>
        (OpCode)(rawInst & 0xFF);

    /// <summary>
    /// Извлекает первый регистр-операнд (биты 8–12, 5 бит).
    /// </summary>
    /// <param name="rawInst">Сырое 32-битное слово инструкции.</param>
    /// <returns>Тип первого регистра (<see cref="RegType"/>).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RegType GetReg1(uint rawInst) =>
        (RegType)((rawInst >> 8) & 0x1F);

    /// <summary>
    /// Извлекает второй регистр-операнд (биты 13–17, 5 бит).
    /// </summary>
    /// <param name="rawInst">Сырое 32-битное слово инструкции.</param>
    /// <returns>Тип второго регистра (<see cref="RegType"/>).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RegType GetReg2(uint rawInst) =>
        (RegType)((rawInst >> 13) & 0x1F);

    /// <summary>
    /// Извлекает код размера данных (биты 18–19, 2 бита).
    /// </summary>
    /// <param name="rawInst">Сырое 32-битное слово инструкции.</param>
    /// <returns>Код размера данных (<see cref="OpCodeSize"/>).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OpCodeSize GetDataSizeCode(uint rawInst) =>
        (OpCodeSize)((rawInst >> 18) & 0x3);

    /// <summary>
    /// За один проход раскладывает сырую инструкцию на все поля и возвращает
    /// их в виде <see cref="DecodeInstructionResult"/>.
    /// </summary>
    /// <param name="rawInst">Сырое 32-битное слово инструкции.</param>
    /// <returns>
    /// Результат декодирования: код операции, два регистра и код размера данных.
    /// </returns>
    /// <remarks>
    /// Эквивалентно последовательному вызову <see cref="GetOpCode"/>,
    /// <see cref="GetReg1"/>, <see cref="GetReg2"/> и
    /// <see cref="GetDataSizeCode"/>, но выполняет сдвиги и маскирование
    /// один раз.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DecodeInstructionResult DecodeRawInstToEnums(uint rawInst)
    {
        OpCode opCode = (OpCode)(rawInst & 0xFF);

        RegType reg1 = (RegType)((rawInst >> 8) & 0x1F);
        RegType reg2 = (RegType)((rawInst >> 13) & 0x1F);

        OpCodeSize dataSizeCode = (OpCodeSize)((rawInst >> 18) & 0x3);
        return new DecodeInstructionResult(opCode, reg1, reg2, dataSizeCode);
    }

    /// <summary>
    /// Проверяет, требует ли инструкция с указанным кодом операции
    /// 64-битного целочисленного операнда (константу, адрес памяти,
    /// адрес подпрограммы или адрес перехода).
    /// </summary>
    /// <param name="opCode">
    /// Код операции. Должен быть не больше <see cref="OpCodeExtension.LastValue"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/>, если инструкция требует 64-битного операнда;
    /// иначе <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Значение <paramref name="opCode"/> больше <see cref="OpCodeExtension.LastValue"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <see langword="true"/> возвращается для кодов
    /// <c>LDI</c>, <c>LOAD</c>, <c>STORE</c>, <c>CALL</c>,
    /// <c>JMP</c>, <c>JZ</c>, <c>JNZ</c>, <c>JG</c>, <c>JL</c>,
    /// <c>LOAD_UNSAFE</c>, <c>STORE_UNSAFE</c>.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasNeed64IntData(OpCode opCode)
    {
        if ((uint)opCode > (uint)OpCodeExtension.LastValue)
            ThrowOutOfRange(opCode);

        return opCode is
            OpCode.LDI or          // константа
            OpCode.LOAD or         // адрес памяти
            OpCode.STORE or        // адрес памяти
            OpCode.CALL or         // адрес подпрограммы
            OpCode.JMP or          // адрес перехода
            OpCode.JZ or           // адрес перехода
            OpCode.JNZ or          // адрес перехода
            OpCode.JG or           // адрес перехода
            OpCode.JL or           // адрес перехода
            OpCode.LOAD_UNSAFE or
            OpCode.STORE_UNSAFE;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowOutOfRange(OpCode opCode) =>
        throw new ArgumentOutOfRangeException(nameof(opCode), opCode, null);
}