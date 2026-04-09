using Maestro.Plugin.Infrastructure;
using Maestro.Avalonia.Contracts;
using Maestro.Avalonia.Integrations;
using Maestro.Avalonia.ViewModels;
using Maestro.Avalonia.Views;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class OpenSlotWindowRequestHandler(WindowManager windowManager, IMediator mediator, IErrorReporter errorReporter)
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
                    mediator,
                    windowHandle,
                    errorReporter);

                return new SlotView(viewModel);
            });

        return Task.CompletedTask;
    }
}
