using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;
using Maestro.Plugin.Infrastructure;
using Maestro.Wpf.Contracts;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class OpenPendingDeparturesWindowRequestHandler(WindowManager windowManager, IAirportConfigurationProvider airportConfigurationProvider, IMediator mediator, IClock clock, IErrorReporter errorReporter)
    : IRequestHandler<OpenPendingDeparturesWindowRequest>
{
    public Task Handle(OpenPendingDeparturesWindowRequest request, CancellationToken cancellationToken)
    {
        var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(request.AirportIdentifier);
        var departureFlights = request.FlightDataRecords
            .Where(r => airportConfiguration.DepartureAirports.Any(d => d.Identifier == r.Origin))
            .ToArray();

        windowManager.FocusOrCreateWindow(
            WindowKeys.InsertDeparture(request.AirportIdentifier),
            "Insert a Flight",
            windowHandle =>
            {
                var viewModel = new PendingDeparturesViewModel(
                    request.AirportIdentifier,
                    departureFlights,
                    windowHandle,
                    mediator,
                    clock,
                    errorReporter);

                return new PendingDeparturesView(viewModel);
            });

        return Task.CompletedTask;
    }
}
