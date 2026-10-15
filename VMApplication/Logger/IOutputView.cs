using Kernel.Common;

namespace VMApplication.Logger;

public interface IOutputView
{
    void AppendLine(string message, LogLevel level = LogLevel.Log);    // LogLevel = Info, Warning, Error
    void Clear();
}

public sealed class NullOutputView : IOutputView
{
    public static readonly NullOutputView Instance = new();
    public void AppendLine(string message, LogLevel level = LogLevel.Log) { }
    public void Clear() { }
}
