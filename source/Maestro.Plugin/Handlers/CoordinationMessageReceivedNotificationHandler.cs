using Maestro.Core.Handlers;
using Maestro.Core.Messages;
using Maestro.Plugin.Infrastructure;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;
using Serilog;

namespace Maestro.Plugin.Handlers;

public class CoordinationMessageReceivedNotificationHandler(WindowManager windowManager, ILogger logger)
    : INotificationHandler<CoordinationMessageReceivedNotification>
{
    public Task Handle(CoordinationMessageReceivedNotification notification, CancellationToken cancellationToken)
    {
        logger.Information("Attempting to open information window with message {Message}", notification.Message);
        windowManager.FocusOrCreateWindow(
            WindowKeys.Information2(notification.AirportIdentifier),
            "Information",
            windowHandle =>
            {
                var viewModel = new InformationViewModel(notification.AirportIdentifier, windowHandle, notification);
                return new InformationView2(viewModel);
            });

        logger.Information("Information window opened");

        return Task.CompletedTask;
    }
}
