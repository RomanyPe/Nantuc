using Nantuc.Common;
using System.Runtime.CompilerServices;

namespace Nantuc.Contracts;

/// <summary>
/// Снапшот чтения состояния процессора
/// </summary>
public readonly struct ProcessorSnapshot
{
    private const int RegisterCount = 32;

    [InlineArray(RegisterCount)]
    private struct Arr { public ulong _el0; }

    private readonly Arr _arr;

    public ProcessorSnapshot(Span<ulong> span, bool isRunning, bool isSleeping, uint rawInstruction, ulong dataByInstruction)
    {
        _arr = GetArr(span);
        IsRunning = isRunning;
        IsSleeping = isSleeping;
        RawInstruction = rawInstruction;
        DataByInstruction = dataByInstruction;
        CurrentBiosStatus = BiosStatus.Success;
    }

    public ProcessorSnapshot(Span<ulong> span, bool isRunning, bool isSleeping, uint rawInstruction, ulong dataByInstruction, BiosStatus currentBiosStatus)
    {
        _arr = GetArr(span);
        IsRunning = isRunning;
        IsSleeping = isSleeping;
        RawInstruction = rawInstruction;
        DataByInstruction = dataByInstruction;
        CurrentBiosStatus = currentBiosStatus;
    }




    /// <summary>
    /// Проверка находится ли процессора в режиме: работы
    /// </summary>
    public bool IsRunning { get; }
    /// <summary>
    /// Проверка находится ли процессора в режиме: сна
    /// </summary>
    public bool IsSleeping { get; }
    /// <summary>
    /// Получение данных регистра
    /// </summary>
    /// <param name="regType">регистр</param>
    public ulong GetRegister(RegType regType) => (uint)regType < RegisterCount ? _arr[(int)regType] : 0;

    public uint RawInstruction { get; init; }
    public ulong DataByInstruction { get; init; }
    public BiosStatus CurrentBiosStatus { get; init; }
    public ulong DataFault => CurrentBiosStatus != BiosStatus.Success ? DataByInstruction : 0;

    public OpCode OpCode => InstructionDecoder.GetOpCode(RawInstruction);
    public RegType FirstRegister => InstructionDecoder.GetReg1(RawInstruction);
    public RegType SecondRegister => InstructionDecoder.GetReg2(RawInstruction);
    public OpCodeSize DataSizeCode => InstructionDecoder.GetDataSizeCode(RawInstruction);

    /// <summary>
    /// Копирует значения регистров из <paramref name="span"/> в inline-массив.
    /// </summary>
    /// <param name="span">Снапшот регистров; должен содержать ровно <see cref="RegisterCount"/> элементов.</param>
    /// <exception cref="ArgumentException">
    /// Если <paramref name="span"/> содержит не <see cref="RegisterCount"/> элементов.
    /// </exception>
    private static Arr GetArr(ReadOnlySpan<ulong> span)
    {
        if (span.Length != RegisterCount)
        {
            throw new ArgumentException(
                $"Processor snapshot must contain exactly {RegisterCount} register values, but contains {span.Length}.",
                nameof(span));
        }

        var arr = new Arr();
        for (int i = 0; i < RegisterCount; i++)
            arr[i] = span[i];
        return arr;
    }
}