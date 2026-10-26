namespace Nantuc.Kernel.Diagnostics;

public interface IErrorMessageProvider
{
    static IErrorMessageProvider Default { get; } = new DefaultErrorMessageProvider();
    string GetMessage(ErrorCode code, params ReadOnlySpan<object> args);
}
