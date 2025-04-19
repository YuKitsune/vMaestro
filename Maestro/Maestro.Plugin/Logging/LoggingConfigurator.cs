using Maestro.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace Maestro.Plugin.Logging;

public class LoggingConfigurator(ILoggerFactory loggerFactory, ILoggingConfiguration loggingConfiguration)
{
    public void ConfigureLogging()
    {
        var logFileWriter = GetLogFileWriter();
        var provider = new FileLoggerProvider(loggingConfiguration.LogLevel, logFileWriter);
        loggerFactory.AddProvider(provider);
    }

    StreamWriter GetLogFileWriter()
    {
        // TODO: Trim log file so that it doesn't get too big
        
        var outputPath = loggingConfiguration.OutputPath;
        var writer = new StreamWriter(outputPath, append: true);
        return writer;
    }
}