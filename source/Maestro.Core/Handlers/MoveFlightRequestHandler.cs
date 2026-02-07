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

public class MoveFlightRequestHandler(
    IMaestroInstanceManager instanceManager,
    IMaestroConnectionManager connectionManager,
    IArrivalLookup arrivalLookup,
    IMediator mediator,
    IClock clock,
    ILogger logger)
    : IRequestHandler<MoveFlightRequest>
{
    public async Task Handle(MoveFlightRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Information("Relaying MoveFlightRequest for {AirportIdentifier}", request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        var instance = await instanceManager.GetInstance(request.AirportIdentifier, cancellationToken);
        SessionMessage sessionMessage;

        using (await instance.Semaphore.LockAsync(cancellationToken))
        {
            var sequence = instance.Session.Sequence;
            var flight = sequence.FindFlight(request.Callsign);
            if (flight is null)
                throw new MaestroException($"{request.Callsign} not found");

            var newIndex = sequence.IndexOf(request.NewLandingTime);

            flight.SetTargetLandingTime(request.NewLandingTime);

            var runwayMode = sequence.GetRunwayModeAt(request.NewLandingTime);
            var runwayIdentifier = runwayMode.Runways.FirstOrDefault(r => request.RunwayIdentifiers.Contains(r.Identifier))?.Identifier
                ?? runwayMode.Default.Identifier;

            sequence.ThrowIsTimeIsUnavailable(request.Callsign, request.NewLandingTime, runwayIdentifier);

            // TODO: Manually set the runway for now, but we need to revisit this later
            // Re: delaying into a new runway mode
            flight.SetRunway(runwayIdentifier, manual: true);

            // Update the approach type if the new runway doesn't have the current approach type
            var approachTypes = arrivalLookup.GetApproachTypes(
                flight.DestinationIdentifier,
                flight.FeederFixIdentifier,
                flight.Fixes.Select(x => x.ToString()).ToArray(),
                flight.AssignedRunwayIdentifier,
                flight.AircraftType,
                flight.AircraftCategory);

            if (!approachTypes.Contains(flight.ApproachType))
                flight.SetApproachType(approachTypes.FirstOrDefault() ?? string.Empty);

            flight.InvalidateSequenceData();

            // Unstable flights become stable when moved
            if (flight.State == State.Unstable)
                flight.SetState(State.Stable, clock);

            sequence.Move(flight, newIndex);

            logger.Information("Flight {Callsign} moved to {NewLandingTime}", flight.Callsign, flight.LandingTime);

            sessionMessage = instance.Session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                instance.AirportIdentifier,
                sessionMessage),
            cancellationToken);
    }
}
