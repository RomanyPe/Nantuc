namespace Nantuc.VmApplication.Extensions.Debugging;

/// <summary>
/// Best-effort кадр стека вызовов, найденный сканированием стека VM
/// на предмет значений, похожих на адреса возврата.
/// </summary>
/// <remarks>
/// VM не хранит frame pointer, поэтому кадры восстанавливаются эвристически:
/// 8-байтные значения в диапазоне <c>[ProgramStart, ProgramEnd)</c>, выровненные по 4.
/// Это может давать ложные срабатывания на локальных переменных, значения которых
/// случайно попали в диапазон кода.
/// </remarks>
public readonly record struct CallStackFrame(
    int Index,
    ulong ReturnAddress,
    ulong StackAddress);
