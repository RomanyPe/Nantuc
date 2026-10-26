using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Nantuc.Kernel.Diagnostics;

public static class ThrowCompiler
{
    private static readonly ErrorCode[] _errorCodesValidateTable = Enum.GetValues<ErrorCode>();

    public static IErrorMessageProvider ErrorMessageProvider
    {
        get; set => field = value ?? throw new ArgumentNullException(nameof(value)); 
    } = IErrorMessageProvider.Default;

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Throw(ErrorCode code, params ReadOnlySpan<object> args)
    {
        var message = ErrorMessageProvider.GetMessage(code, args);
        throw new CodeException(code, message);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static T Throw<T>(ErrorCode code, params ReadOnlySpan<object> args)
    {
        var message = ErrorMessageProvider.GetMessage(code, args);
        throw new CodeException(code, message);
    }

    extension(IReadOnlyDictionary<ErrorCode, string> templates)
    {
        public void ValidateAllCodesCovered()
        {
            foreach (var c in _errorCodesValidateTable)
                if (!templates.ContainsKey(c))
                    throw new InvalidOperationException($"Missing message template for error code {c}");
        }

        public bool TryValidateAllCodesCovered(out ErrorCode code)
        {
            foreach (var c in _errorCodesValidateTable)
                if (!templates.ContainsKey(c))
                {
                    code = c;
                    return false;
                }
            code = default;
            return true;
        }
    }
}
