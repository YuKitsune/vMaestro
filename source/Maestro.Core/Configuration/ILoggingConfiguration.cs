using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Maestro.Core.Configuration;

public interface ILoggingConfiguration
{
    LogLevel LogLevel { get; }
    string OutputDirectory { get; }
    int MaxFileAgeDays { get; }
}

public class LoggingConfiguration(IConfigurationSection loggingConfigurationSection) : ILoggingConfiguration
{
    public LogLevel LogLevel => loggingConfigurationSection.GetValue<LogLevel>("LogLevel");
    public string OutputDirectory => loggingConfigurationSection.GetValue<string>("OutputDirectory")!;
    public int MaxFileAgeDays => loggingConfigurationSection.GetValue<int>("MaxFileAgeDays");
}