using Maestro.Core.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Maestro.Plugin.Logging;

public class FileLogger(
    string categoryName,
    LogLevel level,
    StreamWriter logFileWriter,
    IClock clock)
    : ILogger
{
    readonly string _categoryName = categoryName.Split('.').Last();
    
    public IDisposable BeginScope<TState>(TState state)
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= level;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var time = clock.UtcNow();
        var message = formatter(state, exception);

        logFileWriter.WriteLine($"{time:HH:mm:ss} [{logLevel}] [{_categoryName}] {message}");
        logFileWriter.Flush();
    }
}