namespace Maestro.Avalonia.Integrations;

public interface IErrorReporter
{
    void ReportError(Exception exception);
}
