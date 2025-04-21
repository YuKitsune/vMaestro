using Maestro.Core.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Maestro.Plugin.Logging;

public class FileLoggerProvider(LogLevel logLevel, StreamWriter logFileWriter, IClock clock) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(categoryName, logLevel, logFileWriter, clock);
    }

    public void Dispose()
    {
        logFileWriter.Dispose();
    }
}