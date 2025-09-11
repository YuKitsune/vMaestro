using Maestro.Core.Messages;
using Maestro.Wpf.Integrations;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class ErrorNotificationHandler(IErrorReporter errorReporter) : INotificationHandler<ErrorNotification>
{
    public Task Handle(ErrorNotification notification, CancellationToken cancellationToken)
    {
        errorReporter.ReportError(notification.Exception);
        return Task.CompletedTask;
    }
}
