using Nantuc.Diagnostics;

namespace Nantuc.VmApplication.Extensions.Diagnostics;

public static class DiagnosticsCompilerExtension
{
    private static readonly HashSet<ErrorCode> _errorCodesValidateTable = [.. Enum.GetValues<ErrorCode>()];

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
