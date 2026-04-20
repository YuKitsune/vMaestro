using Maestro.Core.Infrastructure;
using Maestro.Wpf.Integrations;
using Serilog;
using vatsys;

namespace Maestro.Plugin;

public class ErrorReporter(string source, ILogger logger, IClock clock) : IErrorReporter
{
    static readonly Dictionary<string, DateTimeOffset> ErrorMessages = new();

    public void ReportError(Exception exception)
    {
        logger.Error(exception, "An error occurred");
        TryAddErrorInternal(exception);
    }

    void TryAddErrorInternal(Exception exception)
    {
        lock (ErrorMessages)
        {
            var now = clock.UtcNow();

            // Don't flood the error window with the same message over and over again
            if (ErrorMessages.TryGetValue(exception.Message, out var lastShown) &&
                now - lastShown <= TimeSpan.FromMinutes(1))
                return;

            Errors.Add(exception, source);
            ErrorMessages[exception.Message] = now;
        }
    }
}
