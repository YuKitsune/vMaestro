using Maestro.Contracts.Sessions;
using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Sessions;
using Maestro.Plugin.Infrastructure;
using Maestro.Wpf.Contracts;
using Maestro.Wpf.Integrations;
using Maestro.Wpf.ViewModels;
using Maestro.Wpf.Views;
using MediatR;

namespace Maestro.Plugin.Handlers;

public class OpenPendingDeparturesWindowRequestHandler(
    WindowManager windowManager,
    ISessionManager sessionManager,
    IAirportConfigurationProvider airportConfigurationProvider,
    IMediator mediator,
    IClock clock,
    IErrorReporter errorReporter)
    : IRequestHandler<OpenPendingDeparturesWindowRequest>
{
    public async Task Handle(OpenPendingDeparturesWindowRequest request, CancellationToken cancellationToken)
    {
        var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(request.AirportIdentifier);
        var session = await sessionManager.GetSession(request.AirportIdentifier, cancellationToken);

        SessionDto sessionDto;
        using (await session.Semaphore.LockAsync(cancellationToken))
        {
            sessionDto = session.Snapshot();
        }

        var activatedCallsigns = new HashSet<string>(
            sessionDto.Sequence.Flights.Select(f => f.Callsign)
                .Concat(sessionDto.DeSequencedFlights.Select(f => f.Callsign)),
            StringComparer.OrdinalIgnoreCase);

        var departureFlights = sessionDto.FlightDataRecords
            .Where(r => !activatedCallsigns.Contains(r.Callsign) &&
                        airportConfiguration.DepartureAirports.Any(d => d.Identifier == r.Origin))
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
    }
}
