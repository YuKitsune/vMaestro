using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Maestro.Plugin.Logging;

public class LoggingConfigurator(ILoggerFactory loggerFactory, ILoggingConfiguration loggingConfiguration, IClock clock)
{
    public void ConfigureLogging()
    {
        var logFileWriter = GetLogFileWriter();
        var provider = new FileLoggerProvider(loggingConfiguration.LogLevel, logFileWriter, clock);
        loggerFactory.AddProvider(provider);
    }

    StreamWriter GetLogFileWriter()
    {
        // TODO: Trim log file so that it doesn't get too big
        
        var outputPath = loggingConfiguration.OutputPath;
        var fileStream = new FileStream(
            outputPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.ReadWrite,
            bufferSize: 4096,
            FileOptions.WriteThrough);
        
        var writer = new StreamWriter(fileStream);
        writer.AutoFlush = true;
        return writer;
    }
}