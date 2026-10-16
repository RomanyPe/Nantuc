using Kernel.Common;

namespace Kernel.Contracts;

public interface IDeviceLoggerContext
{
    void Log(string message, LogLevel logLevel = LogLevel.Log);

    public static IDeviceLoggerContext NullContext { get; } = new NullDeviceLoggerContext();
    private class NullDeviceLoggerContext : IDeviceLoggerContext
    {
        public void Log(string message, LogLevel logLevel = LogLevel.Log) { }
    }
}
