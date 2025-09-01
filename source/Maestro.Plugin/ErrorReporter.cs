using Maestro.Wpf.Integrations;
using vatsys;

namespace Maestro.Plugin;

public class ErrorReporter(string source) : IErrorReporter
{
    public void ReportError(Exception exception)
    {
        Errors.Add(exception, source);
    }
}
