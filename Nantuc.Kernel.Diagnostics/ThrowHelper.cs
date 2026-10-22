using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Nantuc.Kernel.Diagnostics;

public static class ThrowHelper
{
    private static readonly ErrorCode[] _errorCodesValidateTable = Enum.GetValues<ErrorCode>();

    public static IErrorMessageProvider ErrorMessageProvider { get; set; } = new DefaultErrorMessageProvider();

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowMiniC(ErrorCode code, params ReadOnlySpan<object> args)
    {
        var message = ErrorMessageProvider.GetMessage(code, args);
        throw new CodeExpection(code, message);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static T ThrowMiniC<T>(ErrorCode code, params ReadOnlySpan<object> args)
    {
        var message = ErrorMessageProvider.GetMessage(code, args);
        throw new CodeExpection(code, message);
    }

    public static void ValidateAllCodesCovered(IReadOnlyDictionary<ErrorCode, string> templates)
    {
        if (_errorCodesValidateTable.Any(code => !templates.ContainsKey(code)))
        {
            ErrorCode firstMissing = _errorCodesValidateTable.First(code => !templates.ContainsKey(code));
            throw new InvalidOperationException($"Missing message template for error code {firstMissing}");
        }
    }

    public static bool TryValidateAllCodesCovered(this IReadOnlyDictionary<ErrorCode, string> templates, out ErrorCode code)
    {
        if (_errorCodesValidateTable.Any(code => !templates.ContainsKey(code)))
        {
            code = _errorCodesValidateTable.First(code => !templates.ContainsKey(code));
            return false;
        }
        code = default;
        return true;
    }
    
}
