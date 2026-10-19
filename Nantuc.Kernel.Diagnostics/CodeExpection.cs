namespace Nantuc.Kernel.Diagnostics;

public class CodeExpection(ErrorCode errorCode, string message) : Exception(message)
{
    public ErrorCode ErrorCode { get; } = errorCode;
    public int NumericCode => (int)ErrorCode;
}