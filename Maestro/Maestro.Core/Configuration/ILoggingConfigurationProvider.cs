using Microsoft.Extensions.Logging;

namespace Maestro.Core.Configuration;

public interface ILoggingConfigurationProvider
{
    LogLevel GetLogLevel();
    string GetOutputPath();
}
