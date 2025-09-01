namespace Maestro.Wpf.Integrations;

public interface IErrorReporter
{
    void ReportError(Exception exception);
}
