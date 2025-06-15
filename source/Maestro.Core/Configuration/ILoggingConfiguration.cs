using Microsoft.Extensions.Logging;

namespace Maestro.Core.Configuration;

public interface ILoggingConfiguration
{
    LogLevel LogLevel { get; }
    string OutputDirectory { get; }
    int MaxFileAgeDays { get; }
}

public class LoggingConfiguration : ILoggingConfiguration
{
    public required LogLevel LogLevel { get; init; }
    public required string OutputDirectory { get; init; }
    public required int MaxFileAgeDays { get; init; }
}