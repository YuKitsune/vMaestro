using Maestro.Contracts.Flights;
using Maestro.Contracts.Sessions;
using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class SwapFlightsRequestHandler(
    ISessionManager sessionManager,
    IMaestroConnectionManager connectionManager,
    IAirportConfigurationProvider airportConfigurationProvider,
    IMediator mediator,
    IClock clock,
    ILogger logger)
    : IRequestHandler<SwapFlightsRequest>
{
    public async Task Handle(SwapFlightsRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Information("Relaying SwapFlightsRequest for {FirstCallsign} and {SecondCallsign} at {AirportIdentifier}", request.FirstFlightCallsign, request.SecondFlightCallsign, request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        logger.Verbose("Swapping {FirstCallsign} and {SecondCallsign} for {AirportIdentifier}", request.FirstFlightCallsign, request.SecondFlightCallsign, request.AirportIdentifier);

        var session = await sessionManager.GetSession(request.AirportIdentifier, cancellationToken);
        SessionDto sessionDto;

        var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(request.AirportIdentifier);
        using (await session.Semaphore.LockAsync(cancellationToken))
        {
            var sequence = session.Sequence;

            var firstFlight = sequence.FindFlight(request.FirstFlightCallsign);
            if (firstFlight is null)
                throw new MaestroException($"{request.FirstFlightCallsign} not found");

            var secondFlight = sequence.FindFlight(request.SecondFlightCallsign);
            if (secondFlight is null)
                throw new MaestroException($"{request.SecondFlightCallsign} not found");

            // Swap positions
            sequence.Swap(firstFlight, secondFlight);

            // Unstable flights become stable when swapped
            if (firstFlight.State == State.Unstable) firstFlight.SetState(airportConfiguration.ManualInteractionState, clock);
            if (secondFlight.State == State.Unstable) secondFlight.SetState(airportConfiguration.ManualInteractionState, clock);

            logger.Information("Flights {FirstFlightCallsign} and {SecondFlightCallsign} swapped", firstFlight.Callsign, secondFlight.Callsign);

            sessionDto = session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                session.AirportIdentifier,
                sessionDto),
            cancellationToken);
    }

}
