using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Maestro.Plugin.Logging;

public class LoggingConfigurator(ILoggerFactory loggerFactory, ILoggingConfiguration loggingConfiguration, IClock clock)
{
    const string LogFilePrefix = "MaestroLog";
    
    public void ConfigureLogging()
    {
        DeleteOldLogFiles();
        var logFileWriter = GetLogFileWriter();
        var provider = new FileLoggerProvider(loggingConfiguration.LogLevel, logFileWriter, clock);
        loggerFactory.AddProvider(provider);
    }

    StreamWriter GetLogFileWriter()
    {
        var now = clock.UtcNow().ToLocalTime();
        var logFileName = $"{LogFilePrefix}.{now:yyyy-MM-dd}.txt";
        var outputPath = Path.Combine(loggingConfiguration.OutputDirectory, logFileName);
        
        var fileStream = File.Exists(outputPath)
            ? File.OpenWrite(outputPath)
            : File.Create(outputPath);
        
        var writer = new StreamWriter(fileStream);
        return writer;
    }

    void DeleteOldLogFiles()
    {
        var now = clock.UtcNow().ToLocalTime();
        var files = Directory.GetFiles(loggingConfiguration.OutputDirectory, $"{LogFilePrefix}*.txt");
        foreach (var file in files)
        {
            var lastWriteDate = File.GetLastWriteTime(file);
            var timeSinceLastWrite = now - lastWriteDate;
            if (timeSinceLastWrite.TotalDays > loggingConfiguration.MaxFileAgeDays)
            {
                File.Delete(file);
            }
        }
    }
}