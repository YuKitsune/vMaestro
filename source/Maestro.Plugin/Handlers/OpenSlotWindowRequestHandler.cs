using Maestro.Core.Infrastructure;
using Maestro.Plugin.Infrastructure;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.Messages;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class OpenSlotWindowRequestHandler(WindowManager windowManager, IMessageDispatcher messageDispatcher, IErrorReporter errorReporter)
    : IRequestHandler<OpenSlotWindowRequest>
{
    public Task Handle(OpenSlotWindowRequest request, CancellationToken cancellationToken)
    {
        windowManager.FocusOrCreateWindow(
            WindowKeys.Slot(request.AirportIdentifier),
            "Insert Slot",
            windowHandle =>
            {
                var viewModel = new SlotViewModel(
                    request.AirportIdentifier,
                    request.SlotId,
                    request.StartTime,
                    request.EndTime,
                    request.RunwayIdentifiers,
                    messageDispatcher,
                    windowHandle,
                    errorReporter);

                return new SlotView(viewModel);
            });

        return Task.CompletedTask;
    }
}
