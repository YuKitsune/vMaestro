using Maestro.Core;
using Maestro.Core.Messages.Connectivity;
using Maestro.Wpf.Integrations;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class PermissionDeniedNotificationHandler(IErrorReporter errorReporter)
    : INotificationHandler<PermissionDeniedNotification>
{
    public async Task Handle(PermissionDeniedNotification notification, CancellationToken cancellationToken)
    {
        var exception = new MaestroException($"Permission denied for action '{notification.Action}': {notification.Message}");
        errorReporter.ReportError(exception);
        await Task.CompletedTask;
    }
}