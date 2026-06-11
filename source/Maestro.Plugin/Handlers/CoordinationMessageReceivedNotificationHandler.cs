using Maestro.Contracts.Coordination;
using Maestro.Plugin.Infrastructure;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;
using Serilog;

namespace Maestro.Plugin.Handlers;

public class CoordinationMessageReceivedNotificationHandler(WindowManager windowManager)
    : INotificationHandler<CoordinationMessageReceivedNotification>
{
    public Task Handle(CoordinationMessageReceivedNotification notification, CancellationToken cancellationToken)
    {
        windowManager.FocusOrCreateWindow(
            WindowKeys.Information2(notification.AirportIdentifier),
            "Information",
            windowHandle =>
            {
                var viewModel = new InformationViewModel(notification.AirportIdentifier, windowHandle, notification);
                return new InformationView2(viewModel);
            });

        return Task.CompletedTask;
    }
}
