using System.Diagnostics;
using System.Text;
using Maestro.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace Maestro.Plugin;

public class LoggingConfigurator(ILoggerFactory loggerFactory, ILoggingConfigurationProvider configurationProvider)
{
    public void ConfigureLogging()
    {
        var logFileWriter = GetLogFileWriter();
        var provider = new CustomFileLoggerProvider(configurationProvider.GetLogLevel(), logFileWriter);
        loggerFactory.AddProvider(provider);
    }

    StreamWriter GetLogFileWriter()
    {
        // TODO: Trim log file so that it doesn't get too big
        
        var outputPath = configurationProvider.GetOutputPath();
        var writer = new StreamWriter(outputPath, append: true);
        return writer;
    }
}

public class CustomFileLoggerProvider(LogLevel logLevel, StreamWriter logFileWriter) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new CustomFileLogger(categoryName, logLevel, logFileWriter);
    }

    public void Dispose()
    {
        logFileWriter.Dispose();
    }
}

public class CustomFileLogger(
    string categoryName,
    LogLevel level,
    StreamWriter logFileWriter)
    : ILogger
{
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

        var message = formatter(state, exception);

        logFileWriter.WriteLine($"[{logLevel}] [{categoryName}] {message}");
        logFileWriter.Flush();
    }
}