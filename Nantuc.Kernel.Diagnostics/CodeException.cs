namespace Nantuc.Kernel.Diagnostics;

public class CodeException(ErrorCode errorCode, string message) : Exception(message)
{
    public ErrorCode ErrorCode { get; } = errorCode;
    public int NumericCode => (int)ErrorCode;
}