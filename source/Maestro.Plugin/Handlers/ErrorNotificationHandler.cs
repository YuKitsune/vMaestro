using Maestro.Core.Messages;
using Maestro.Wpf.Integrations;
using MediatR;
using Serilog;

namespace Maestro.Plugin.Handlers;

public class ErrorNotificationHandler(IErrorReporter errorReporter, ILogger logger) : INotificationHandler<ErrorNotification>
{
    public Task Handle(ErrorNotification notification, CancellationToken cancellationToken)
    {
        logger.Error(notification.Exception, "An error occurred: {Message}", notification.Exception.Message);
        errorReporter.ReportError(notification.Exception);
        return Task.CompletedTask;
    }
}
