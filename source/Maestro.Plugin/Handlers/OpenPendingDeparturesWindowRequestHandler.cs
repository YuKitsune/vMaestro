using Maestro.Core.Infrastructure;
using Maestro.Plugin.Infrastructure;
using Maestro.Avalonia.Contracts;
using Maestro.Avalonia.Integrations;
using Maestro.Avalonia.ViewModels;
using Maestro.Avalonia.Views;
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
                    request.PendingFlights.Where(f => f.IsFromDepartureAirport).ToArray(),
                    windowHandle,
                    mediator,
                    clock,
                    errorReporter);

                return new PendingDeparturesView(viewModel);
            });

        return Task.CompletedTask;
    }
}
