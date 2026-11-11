using Nantuc.Diagnostics;

namespace Nantuc.VmApplication.Project;

public readonly record struct DiagnosticResult(string Message, ErrorCode ErrorCode);