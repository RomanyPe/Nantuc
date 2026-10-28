using Nantuc.Kernel.Diagnostics;

namespace Nantuc.VmApplication.Extensions.Compiler;

public static class CompilerExtension
{
    private static readonly ErrorCode[] _errorCodesValidateTable = Enum.GetValues<ErrorCode>();

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
