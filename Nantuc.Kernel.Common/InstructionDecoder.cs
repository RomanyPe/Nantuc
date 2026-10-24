using System.Collections.Immutable;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Nantuc.Kernel.Common;

public static class InstructionDecoder
{

    private static readonly ImmutableArray<bool> _has = CreateNeedsUlongOperandTable();


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OpCode GetOpCode(uint rawInst) =>
        (OpCode)(rawInst & 0xFF);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RegType GetReg1(uint rawInst) =>
        (RegType)((rawInst >> 8) & 0x1F);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RegType GetReg2(uint rawInst) =>
        (RegType)((rawInst >> 13) & 0x1F);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OpCodeSize GetDataSizeCode(uint rawInst) =>
        (OpCodeSize)((rawInst >> 18) & 0x3);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DecodeInstructionResult DecodeRawInstToEnums(uint rawInst)
    {
        OpCode opCode = (OpCode)(rawInst & 0xFF);

        RegType reg1 = (RegType)((rawInst >> 8) & 0x1F);
        RegType reg2 = (RegType)((rawInst >> 13) & 0x1F);

        OpCodeSize dataSizeCode = (OpCodeSize)((rawInst >> 18) & 0x3);
        return new DecodeInstructionResult(opCode, reg1, reg2, dataSizeCode);
    }

    public static bool HasNeed64IntData(OpCode opCode)
    {
        return _has[(int)opCode];
    }

    public static ImmutableArray<bool> CreateNeedsUlongOperandTable()
    {
        bool[] table = new bool[256];

        table[(int)OpCode.LDI] = true;      // константа
        table[(int)OpCode.LOAD] = true;     // адрес памяти
        table[(int)OpCode.STORE] = true;    // адрес памяти
        table[(int)OpCode.CALL] = true;     // адрес подпрограммы
        table[(int)OpCode.JMP] = true;      // адрес перехода
        table[(int)OpCode.JZ] = true;       // адрес перехода
        table[(int)OpCode.JNZ] = true;      // адрес перехода
        table[(int)OpCode.JG] = true;       // адрес перехода
        table[(int)OpCode.JL] = true;       // адрес перехода

        return [.. table];
    }


    extension(RegType reg)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Int() => (int)reg;
    }
}

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