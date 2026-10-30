using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Nantuc.Kernel.Common;
/// <summary>
/// Расширения для выравнивания целочисленных значений по границе,
/// заданной степенью двойки.
/// </summary>
/// <remarks>
/// <para>
/// Методы доступны как свойства/методы-расширения для любого типа,
/// реализующего <see cref="IBinaryInteger{TSelf}"/> (например,
/// <see cref="int"/>, <see cref="long"/>, <see cref="uint"/> и т.п.),
/// и выполняют вычисления в терминах самого типа <typeparamref name="T"/>.
/// </para>
/// <para>
/// <see cref="AlignUp"/> и <see cref="AlignDown"/> требуют, чтобы
/// <c>alignment</c> был степенью двойки (1, 2, 4, 8, …).
/// Это условие проверяется <b>только в DEBUG-сборках</b>
/// (у метода проверки стоит <see cref="ConditionalAttribute"/> с
/// аргументом <c>"DEBUG"</c>). В Release-сборках передача неверного
/// <c>alignment</c> не выбросит исключение и даст некорректный результат.
/// </para>
/// <para>
/// <see cref="AlignUpArithmetic"/> не требует степени двойки — достаточно
/// положительного <c>alignment</c>; эта проверка выполняется всегда
/// (см. <see cref="ArgumentOutOfRangeException.ThrowIfLessThanOrEqual{T}(T, T, string?)"/>).
/// </para>
/// </remarks>
public static class AlignmentExtensions
{
    extension<T>(T value) where T : IBinaryInteger<T>
    {
        /// <summary>
        /// Выравнивает значение вверх до ближайшего числа, кратного
        /// <paramref name="alignment"/>.
        /// </summary>
        /// <param name="alignment">
        /// Шаг выравнивания. Должен быть степенью двойки (1, 2, 4, 8, …).
        /// В DEBUG-сборках неверное значение приводит к
        /// <see cref="ArgumentOutOfRangeException"/>.
        /// </param>
        /// <returns>
        /// Наименьшее число, не меньшее <paramref name="value"/>, кратное
        /// <paramref name="alignment"/>. Если <paramref name="value"/>
        /// уже выровнено, возвращается оно само.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Реализация использует битовую маску, поэтому корректна только
        /// для <paramref name="alignment"/>, являющегося степенью двойки.
        /// </para>
        /// <para>
        /// <b>Переполнение:</b> для значений, близких к
        /// <c>T.MaxValue</c>, сложение <c>value + (alignment - 1)</c> может
        /// переполниться. Поведение в этом случае соответствует переполнению
        /// соответствующего типа (для типов без checked-контекста —
        /// беззнаковое обрезание; для signed — переполнение по умолчанию
        /// непроверяемое).
        /// </para>
        /// <para>
        /// <b>Проверка аргумента условна:</b> в Release-сборках
        /// <paramref name="alignment"/>, не являющийся степенью двойки,
        /// не диагностируется, а даёт неверный результат. Если нужна
        /// гарантированная проверка во всех конфигурациях, используйте
        /// <see cref="AlignUpArithmetic"/> или проверяйте <paramref name="alignment"/>
        /// самостоятельно.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// В DEBUG-сборках — если <paramref name="alignment"/> не является
        /// степенью двойки или не больше нуля.
        /// </exception>
        public T AlignUp(T alignment)
        {
            IsOutOfRangeException(alignment);
            return (value + (alignment - T.One)) & ~(alignment - T.One);
        }

        /// <summary>
        /// Выравнивает значение вниз до ближайшего числа, кратного
        /// <paramref name="alignment"/>.
        /// </summary>
        /// <param name="alignment">
        /// Шаг выравнивания. Должен быть степенью двойки (1, 2, 4, 8, …).
        /// В DEBUG-сборках неверное значение приводит к
        /// <see cref="ArgumentOutOfRangeException"/>.
        /// </param>
        /// <returns>
        /// Наибольшее число, не большее <paramref name="value"/>, кратное
        /// <paramref name="alignment"/>. Если <paramref name="value"/>
        /// уже выровнено, возвращается оно само.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Реализация использует битовую маску, поэтому корректна только
        /// для <paramref name="alignment"/>, являющегося степенью двойки.
        /// </para>
        /// <para>
        /// <b>Проверка аргумента условна:</b> в Release-сборках
        /// <paramref name="alignment"/>, не являющийся степенью двойки,
        /// не диагностируется, а даёт неверный результат.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// В DEBUG-сборках — если <paramref name="alignment"/> не является
        /// степенью двойки или не больше нуля.
        /// </exception>
        public T AlignDown(T alignment)
        {
            IsOutOfRangeException(alignment);
            return value & ~(alignment - T.One);
        }

        /// <summary>
        /// Выравнивает значение вверх до ближайшего числа, кратного
        /// <paramref name="alignment"/>, арифметическим способом
        /// (через остаток от деления).
        /// </summary>
        /// <param name="alignment">
        /// Шаг выравнивания. Должен быть строго больше нуля.
        /// </param>
        /// <returns>
        /// Наименьшее число, не меньшее <paramref name="value"/>, кратное
        /// <paramref name="alignment"/>. Если <paramref name="value"/>
        /// уже выровнено, возвращается оно само.
        /// </returns>
        /// <remarks>
        /// <para>
        /// В отличие от <see cref="AlignUp"/>, не требует, чтобы
        /// <paramref name="alignment"/> был степенью двойки, и проверяет
        /// аргумент <b>во всех конфигурациях сборки</b>, а не только в DEBUG.
        /// </para>
        /// <para>
        /// Может быть медленнее <see cref="AlignUp"/> из-за операции
        /// деления; выбирайте этот метод, когда <paramref name="alignment"/>
        /// не является степенью двойки или когда нужна гарантированная
        /// проверка аргумента.
        /// </para>
        /// <para>
        /// <b>Переполнение:</b> для значений, близких к <c>T.MaxValue</c>,
        /// сложение <c>value + (alignment - remainder)</c> может переполниться;
        /// поведение — как у обычного сложения соответствующего типа.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Если <paramref name="alignment"/> меньше либо равно нулю.
        /// </exception>
        public T AlignUpArithmetic(T alignment)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(alignment, T.Zero);

            T remainder = value % alignment;
            if (remainder == T.Zero)
                return value;
            return value + (alignment - remainder);
        }
    }

    /// <summary>
    /// Проверяет корректность шага выравнивания для битовых методов
    /// (<see cref="AlignUp"/> / <see cref="AlignDown"/>).
    /// </summary>
    /// <typeparam name="T">Целочисленный тип значения.</typeparam>
    /// <param name="alignment">
    /// Проверяемое значение; должно быть степенью двойки и больше нуля.
    /// </param>
    /// <remarks>
    /// <para>
    /// Метод помечен <see cref="ConditionalAttribute"/> с аргументом
    /// <c>"DEBUG"</c>: в Release-сборках вызов полностью удаляется
    /// компилятором, а вместе с ним — и проверка.
    /// </para>
    /// <para>
    /// <b>Именование:</b> имя <c>IsOutOfRangeException</c> читается как
    /// предикат, но метод ничего не возвращает и только бросает исключение.
    /// Рассмотрите переименование в <c>ValidateAlignment</c> или
    /// <c>ThrowIfInvalidAlignment</c>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Если <paramref name="alignment"/> не больше нуля или не является
    /// степенью двойки.
    /// </exception>
    [Conditional("DEBUG")]
    private static void IsOutOfRangeException<T>(T alignment) where T : IBinaryInteger<T>
    {
        if (alignment <= T.Zero || !IsPowerOfTwo(alignment))
            GenerateOutOfRangeException(nameof(alignment));
    }

    /// <summary>
    /// Генерирует <see cref="ArgumentOutOfRangeException"/> с сообщением
    /// о некорректном шаге выравнивания.
    /// </summary>
    /// <param name="name">Имя параметра, попавшего в исключение.</param>
    /// <remarks>
    /// Выделен в отдельный метод с <see cref="MethodImplOptions.NoInlining"/>,
    /// чтобы код построения исключения не занимал место в горячем пути
    /// (<c>AlignUp</c> / <c>AlignDown</c>) и не мешал инлайнингу.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Всегда.</exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void GenerateOutOfRangeException(string name)
    {
        throw new ArgumentOutOfRangeException(name, "Выравнивание должно быть степенью двойки и больше нуля.");
    }

    /// <summary>
    /// Определяет, является ли значение степенью двойки.
    /// </summary>
    /// <typeparam name="T">Целочисленный тип значения.</typeparam>
    /// <param name="value">Проверяемое значение.</param>
    /// <returns>
    /// <see langword="true"/>, если <paramref name="value"/> является
    /// степенью двойки; иначе <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// <b>Внимание:</b> для <paramref name="value"/>, равного нулю,
    /// возвращает <see langword="true"/> (свойство битовой маски).
    /// Ноль отсекается отдельной проверкой <c>alignment &lt;= T.Zero</c>
    /// в вызывающем коде; при переиспользовании метода вне этого контекста
    /// проверяйте ноль самостоятельно.
    /// </remarks>
    private static bool IsPowerOfTwo<T>(T value) where T : IBinaryInteger<T>
    {
        return (value & (value - T.One)) == T.Zero;
    }
}