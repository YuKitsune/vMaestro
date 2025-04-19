using Microsoft.Extensions.Logging;

namespace Maestro.Plugin.Logging;

public class FileLoggerProvider(LogLevel logLevel, StreamWriter logFileWriter) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(categoryName, logLevel, logFileWriter);
    }

    public void Dispose()
    {
        logFileWriter.Dispose();
    }
}