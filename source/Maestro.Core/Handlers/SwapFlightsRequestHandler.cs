using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Hosting;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class SwapFlightsRequestHandler(
    IMaestroInstanceManager instanceManager,
    IMaestroConnectionManager connectionManager,
    IArrivalLookup arrivalLookup,
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
            logger.Information("Relaying SwapFlightsRequest for {AirportIdentifier}", request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        var instance = await instanceManager.GetInstance(request.AirportIdentifier, cancellationToken);
        SessionMessage sessionMessage;

        using (await instance.Semaphore.LockAsync(cancellationToken))
        {
            var sequence = instance.Session.Sequence;

            var firstFlight = sequence.FindFlight(request.FirstFlightCallsign);
            if (firstFlight is null)
                throw new MaestroException($"{request.FirstFlightCallsign} not found");

            var secondFlight = sequence.FindFlight(request.SecondFlightCallsign);
            if (secondFlight is null)
                throw new MaestroException($"{request.SecondFlightCallsign} not found");

            // Swap positions
            sequence.Swap(firstFlight, secondFlight);

            // TODO: This has been moved into the Sequence. I'm not sure how I feel about it.

            // // Swap landing times
            // var firstLandingTime = firstFlight.LandingTime;
            // var secondLandingTime = secondFlight.LandingTime;
            // firstFlight.SetLandingTime(secondLandingTime);
            // secondFlight.SetLandingTime(firstLandingTime);
            //
            // // Re-calculate feeder-fix times (don't swap because they could be on different arrivals with different intervals)
            // var firstFeederFixTime = GetFeederFixTime(firstFlight);
            // if (firstFeederFixTime is not null)
            //     firstFlight.SetFeederFixTime(firstFeederFixTime.Value);
            //
            // var secondFeederFixTime = GetFeederFixTime(secondFlight);
            // if (secondFeederFixTime is not null)
            //     secondFlight.SetFeederFixTime(secondFeederFixTime.Value);
            //
            // // Swap runways
            // var firstRunway = firstFlight.AssignedRunwayIdentifier;
            // var secondRunway = secondFlight.AssignedRunwayIdentifier;
            // firstFlight.SetRunway(secondRunway, manual: true);
            // secondFlight.SetRunway(firstRunway, manual: true);

            // Unstable flights become stable when swapped
            if (firstFlight.State == State.Unstable) firstFlight.SetState(State.Stable, clock);
            if (secondFlight.State == State.Unstable) secondFlight.SetState(State.Stable, clock);

            logger.Information("Flights {FirstFlightCallsign} and {SecondFlightCallsign} swapped", firstFlight.Callsign, secondFlight.Callsign);

            sessionMessage = instance.Session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                instance.AirportIdentifier,
                sessionMessage),
            cancellationToken);
    }

    DateTimeOffset? GetFeederFixTime(Flight flight)
    {
        var interval = arrivalLookup.GetArrivalInterval(
            flight.DestinationIdentifier,
            flight.FeederFixIdentifier,
            flight.AssignedArrivalIdentifier,
            flight.AssignedRunwayIdentifier,
            flight.AircraftType,
            flight.AircraftCategory);

        if (interval is null)
            return null;

        return flight.LandingTime.Subtract(interval.Value);
    }
}
