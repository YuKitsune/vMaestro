using Maestro.Core.Handlers;
using Maestro.Plugin.Infrastructure;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class InformationNotificationHandler(WindowManager windowManager)
    : INotificationHandler<InformationNotification>
{
    public Task Handle(InformationNotification notification, CancellationToken cancellationToken)
    {
        windowManager.FocusOrCreateWindow(
            WindowKeys.Information2(notification.AirportIdentifier),
            "Information",
            windowHandle =>
            {
                var viewModel = new InformationViewModel(notification.AirportIdentifier, windowHandle);
                return new InformationView2(viewModel);
            });

        return Task.CompletedTask;
    }
}
