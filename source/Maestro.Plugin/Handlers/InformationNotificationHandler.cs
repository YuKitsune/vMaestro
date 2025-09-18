using Maestro.Core.Handlers;
using Maestro.Core.Infrastructure;
using Maestro.Plugin.Infrastructure;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class InformationNotificationHandler(
    WindowManager windowManager,
    IErrorReporter errorReporter,
    INotificationStream<InformationNotification> notificationStream)
    : INotificationHandler<InformationNotification>
{
    public Task Handle(InformationNotification notification, CancellationToken cancellationToken)
    {
        windowManager.FocusOrCreateWindow(
            WindowKeys.Information2(notification.AirportIdentifier),
            "Information",
            windowHandle =>
            {
                var viewModel = new InformationViewModel(
                    notification.AirportIdentifier,
                    errorReporter,
                    windowHandle,
                    notificationStream);
                return new InformationView2(viewModel);
            });

        return Task.CompletedTask;
    }
}
