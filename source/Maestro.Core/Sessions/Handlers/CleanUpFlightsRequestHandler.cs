using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Sessions.Contracts;
using MediatR;
using Serilog;

namespace Maestro.Core.Sessions.Handlers;

public class CleanUpFlightsRequestHandler(
    IMaestroConnectionManager connectionManager,
    ISessionManager sessionManager,
    IAirportConfigurationProvider airportConfigurationProvider,
    IClock clock,
    ILogger logger)
    : IRequestHandler<CleanUpFlightsRequest>
{
    public async Task Handle(CleanUpFlightsRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Debug("Skipping flight clean up for {AirportIdentifier} as we are not the master of this sequence", request.AirportIdentifier);
            return;
        }

        logger.Verbose("Attempting to clean up flights for {AirportIdentifier}", request.AirportIdentifier);

        var session = await sessionManager.GetSession(request.AirportIdentifier, cancellationToken);

        using (await session.Semaphore.LockAsync(cancellationToken))
        {
            var sequence = session.Sequence;
            var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(request.AirportIdentifier);

            var landedFlights = sequence.Flights
                .Where(f => f.State == State.Landed)
                .ToArray();

            for (var i = 0; i < landedFlights.Length; i++)
            {
                var landedFlight = landedFlights[i];
                var timeSinceLanded = clock.UtcNow() - landedFlight.LandingTime;
                var landedFlightTimeout = TimeSpan.FromMinutes(airportConfiguration.LandedFlightTimeoutMinutes);
                if (i < airportConfiguration.MaxLandedFlights && timeSinceLanded < landedFlightTimeout)
                    continue;

                sequence.Remove(landedFlight);
                logger.Information(
                    "Deleting {Callsign} from {AirportIdentifier} as it has landed.",
                    landedFlight.Callsign,
                    sequence.AirportIdentifier);
            }

            // TODO: Lost flights
        }
    }
}
