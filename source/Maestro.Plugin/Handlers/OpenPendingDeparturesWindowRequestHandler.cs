using Maestro.Core.Infrastructure;
using Maestro.Plugin.Infrastructure;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.Messages;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class OpenPendingDeparturesWindowRequestHandler(WindowManager windowManager, IMediator mediator, IClock clock, IErrorReporter errorReporter)
    : IRequestHandler<OpenPendingDeparturesWindowRequest>
{
    public Task Handle(OpenPendingDeparturesWindowRequest request, CancellationToken cancellationToken)
    {
        windowManager.FocusOrCreateWindow(
            WindowKeys.InsertDeparture(request.AirportIdentifier),
            "Insert a Flight",
            windowHandle =>
            {
                var viewModel = new PendingDeparturesViewModel(
                    request.AirportIdentifier,
                    request.PendingFlights,
                    windowHandle,
                    mediator,
                    clock,
                    errorReporter);

                return new PendingDeparturesView(viewModel);
            });

        return Task.CompletedTask;
    }
}
