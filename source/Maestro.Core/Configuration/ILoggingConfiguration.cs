using Serilog.Events;

namespace Maestro.Core.Configuration;

public interface ILoggingConfiguration
{
    LogEventLevel LogLevel { get; }
    int MaxFileAgeDays { get; }
}

public class LoggingConfiguration : ILoggingConfiguration
{
    public required LogEventLevel LogLevel { get; init; }
    public required string OutputDirectory { get; init; }
    public required int MaxFileAgeDays { get; init; }
}