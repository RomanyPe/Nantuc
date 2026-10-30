namespace Nantuc.Kernel.Common;

/// <summary>
/// Расширение для интерпретации числового идентификатора (Id), полученного
/// от внешней системы, в дискретный статус <see cref="IdState"/>.
/// </summary>
/// <remarks>
/// <para>
/// Позволяет единообразно преобразовывать «сырой» целочисленный Id
/// в значение <see cref="IdState"/> без явных проверок в вызывающем коде.
/// </para>
/// <para>
/// Соответствие значений Id и статусов:
/// <list type="table">
///   <listheader>
///     <term>Значение Id</term>
///     <description>Результирующий <see cref="IdState"/></description>
///   </listheader>
///   <item>
///     <term><c>&gt;= 0</c></term>
///     <description><see cref="IdState.Valid"/></description>
///   </item>
///   <item>
///     <term><c>-1</c></term>
///     <description><see cref="IdState.NotAssigned"/></description>
///   </item>
///   <item>
///     <term><c>-2</c></term>
///     <description><see cref="IdState.OutOfRange"/></description>
///   </item>
///   <item>
///     <term><c>-3</c></term>
///     <description><see cref="IdState.NoFreeSlot"/></description>
///   </item>
///   <item>
///     <term><c>-4</c></term>
///     <description><see cref="IdState.Rejected"/></description>
///   </item>
///   <item>
///     <term>любое иное (в т.ч. <c>-5</c>)</term>
///     <description><see cref="IdState.Unknown"/></description>
///   </item>
/// </list>
/// </para>
/// <para>
/// Преобразование тотально: оно определено для любого значения <see cref="int"/>
/// и никогда не бросает исключений, поэтому может безопасно применяться
/// к произвольным данным, приходящим извне.
/// </para>
/// <para>
/// <b>Важно:</b> неотрицательные значения трактуются как <see cref="IdState.Valid"/>,
/// в том числе <c>default(int)</c> (<c>0</c>). Если вызывающий код полагается на то,
/// что «неинициализированный» Id должен считаться ошибкой, ему следует
/// предварительно проверять признак инициализации отдельно.
/// </para>
/// </remarks>
public static class IdStateExtension
{
    /// <summary>
    /// Преобразует числовой идентификатор в соответствующий статус <see cref="IdState"/>.
    /// </summary>
    /// <value>
    /// <see cref="IdState.Valid"/>, если <c>id</c> неотрицателен;
    /// иначе — одна из причин отказа (<see cref="IdState.NotAssigned"/>,
    /// <see cref="IdState.OutOfRange"/>, <see cref="IdState.NoFreeSlot"/>,
    /// <see cref="IdState.Rejected"/>) либо <see cref="IdState.Unknown"/>
    /// для нераспознанных значений.
    /// </value>
    /// <remarks>
    /// Свойство-расширение определено на типе <see cref="int"/> и доступно
    /// как <c>someId.ToIdState</c> для любого целочисленного идентификатора.
    /// Не бросает исключений; результат детерминирован для каждого значения <c>id</c>.
    /// </remarks>
    extension(int id)
    {
        public IdState ToIdState => id switch
        {
            >= 0 => IdState.Valid,
            -1 => IdState.NotAssigned,
            -2 => IdState.OutOfRange,
            -3 => IdState.NoFreeSlot,
            -4 => IdState.Rejected,
            _ => IdState.Unknown,
        };
    }
}