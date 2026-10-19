namespace Nantuc.Kernel.Diagnostics;

public interface IErrorMessageProvider
{
    string GetMessage(ErrorCode code, params ReadOnlySpan<object> args);
}
