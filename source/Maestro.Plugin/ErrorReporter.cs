using Maestro.Wpf.Integrations;
using Serilog;
using vatsys;

namespace Maestro.Plugin;

public class ErrorReporter(string source, ILogger logger) : IErrorReporter
{
    public void ReportError(Exception exception)
    {
        logger.Error(exception, "An error occurred");
        Errors.Add(exception, source);
    }
}
