using Microsoft.Extensions.Logging;

namespace TFMS.Core.Configuration;

public interface ILoggingConfigurationProvider
{
    LogLevel GetLogLevel();
    string GetOutputPath();
}
