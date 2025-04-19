using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Maestro.Core.Configuration;

public interface ILoggingConfiguration
{
    LogLevel LogLevel { get; }
    string OutputPath { get; }
}

public class LoggingConfiguration(IConfigurationSection loggingConfigurationSection) : ILoggingConfiguration
{
    public LogLevel LogLevel => loggingConfigurationSection.GetValue<LogLevel>("LogLevel");
    public string OutputPath => loggingConfigurationSection.GetValue<string>("OutputPath")!;
}