namespace Nantuc.Kernel.Diagnostics;

/// <summary>
/// Исключения компилятора
/// </summary>
/// <param name="errorCode">код ошибки</param>
/// <param name="message">сообщение с ошиибки</param>
public class CodeException(ErrorCode errorCode, string message) : Exception(message)
{
    /// <summary>
    /// Код ошибки ввиде <c>enum</c>
    /// </summary>
    public ErrorCode ErrorCode { get; } = errorCode;
    /// <summary>
    /// Код ошибки ввиде <c>int</c>
    /// </summary>
    public int NumericCode => (int)ErrorCode;
}