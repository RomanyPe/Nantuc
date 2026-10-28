using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Nantuc.Kernel.Common;

/// <summary>
/// Класс ресширение для удобного выравнивания чисел
/// </summary>
public static class AlignmentExtensions
{
    extension<T>(T value) where T : IBinaryInteger<T>
    {
        /// <summary>
        /// Выравнивает значение вверх до ближайшего числа, кратного alignment.
        /// alignment должно быть степенью двойки (1, 2, 4, 8, ...).
        /// </summary>
        public T AlignUp(T alignment)
        {
            IsOutOfRangeException(alignment);
            return (value + (alignment - T.One)) & ~(alignment - T.One);
        }

        /// <summary>
        /// Выравнивает значение вниз до ближайшего числа, кратного alignment.
        /// alignment должно быть степенью двойки (1, 2, 4, 8, ...).
        /// </summary>
        public T AlignDown(T alignment)
        {
            IsOutOfRangeException(alignment);
            return value & ~(alignment - T.One);
        }

        public T AlignUpArithmetic(T alignment)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(alignment, T.Zero);

            T remainder = value % alignment;
            if (remainder == T.Zero)
                return value;
            return value + (alignment - remainder);
        }
    }

    [Conditional("DEBUG")]
    private static void IsOutOfRangeException<T>(T alignment) where T : IBinaryInteger<T>
    {
        if (alignment <= T.Zero || !IsPowerOfTwo(alignment))
            GenerateOutOfRangeException(nameof(alignment));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void GenerateOutOfRangeException(string name)
    {
        throw new ArgumentOutOfRangeException(name, "Выравнивание должно быть степенью двойки и больше нуля.");
    }
    private static bool IsPowerOfTwo<T>(T value) where T : IBinaryInteger<T>
    {
        return (value & (value - T.One)) == T.Zero;
    }
}