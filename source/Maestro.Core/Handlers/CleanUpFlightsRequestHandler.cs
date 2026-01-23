using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Hosting;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class CleanUpFlightsRequestHandler(
    IMaestroInstanceManager instanceManager,
    IAirportConfigurationProvider airportConfigurationProvider,
    IClock clock,
    ILogger logger)
    : IRequestHandler<CleanUpFlightsRequest>
{
    public async Task Handle(CleanUpFlightsRequest request, CancellationToken cancellationToken)
    {
        var instance = await instanceManager.GetInstance(request.AirportIdentifier, cancellationToken);

        using (await instance.Semaphore.LockAsync(cancellationToken))
        {
            var sequence = instance.Session.Sequence;
            var airportConfiguration = airportConfigurationProvider
                .GetAirportConfigurations()
                .First(c => c.Identifier == sequence.AirportIdentifier);

            var landedFlights = sequence.Flights
                .Where(f => f.State == State.Landed)
                .ToArray();

            for (var i = 0; i < landedFlights.Length; i++)
            {
                var landedFlight = landedFlights[i];
                var timeSinceLanded = clock.UtcNow() - landedFlight.LandingTime;
                var landedFlightTimeout = TimeSpan.FromMinutes(airportConfiguration.LandedFlightTimeoutMinutes);
                if (i >= airportConfiguration.MaxLandedFlights || timeSinceLanded >= landedFlightTimeout)
                {
                    sequence.Remove(landedFlight);
                    logger.Information(
                        "Deleting {Callsign} from {AirportIdentifier} as it has landed.",
                        landedFlight.Callsign,
                        sequence.AirportIdentifier);
                }
            }
        }
    }
}
