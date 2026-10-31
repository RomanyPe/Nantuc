using Nantuc.Kernel.Common;
using System.Runtime.CompilerServices;

namespace Nantuc.Kernel.Contracts;

/// <summary>
/// Интерфейс чтения состояния процессора
/// </summary>
public readonly struct ProcessorReader(Span<ulong> span, bool isRun, bool isSleep)
{
    [InlineArray(32)]
    private struct Arr { public ulong _el0; }
    private readonly Arr _arr = GetArr(span);
    /// <summary>
    /// Проверка находится ли процессора в режиме: работы
    /// </summary>
    public bool IsRunning { get; } = isRun;
    /// <summary>
    /// Проверка находится ли процессора в режиме: сна
    /// </summary>
    public bool IsSleeping { get; } = isSleep;
    /// <summary>
    /// Получение данных регистра
    /// </summary>
    /// <param name="regType">регистр</param>
    public ulong GetRegister(RegType regType) => _arr[(int)regType];

    private static Arr GetArr(Span<ulong> span)
    {
        var arr = new Arr();
        for (int i = 0; i < span.Length; i++)
            arr[i] = span[i];
        return arr;
    }
}