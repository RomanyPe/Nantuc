using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Nantuc.Kernel.Diagnostics;

public class ThrowCompiler(IErrorMessageProvider? provider)
{
    public IErrorMessageProvider ErrorMessageProvider = provider ?? IErrorMessageProvider.Default;

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Throw(ErrorCode code, params ReadOnlySpan<object> args)
    {
        var message = ErrorMessageProvider.GetMessage(code, args);
        throw new CodeException(code, message);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public T Throw<T>(ErrorCode code, params ReadOnlySpan<object> args)
    {
        var message = ErrorMessageProvider.GetMessage(code, args);
        throw new CodeException(code, message);
    }
}