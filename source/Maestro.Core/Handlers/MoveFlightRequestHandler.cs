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

            var runwayMode = sequence.GetRunwayModeAt(request.NewLandingTime);
            var runwayIdentifier = runwayMode.Runways.FirstOrDefault(r => request.RunwayIdentifiers.Contains(r.Identifier))?.Identifier
                ?? runwayMode.Default.Identifier;

            sequence.ThrowIfSlotIsUnavailable(newIndex, runwayIdentifier);

            // TODO: Manually set the runway for now, but we need to revisit this later
            // Re: delaying into a new runway mode
            flight.SetRunway(runwayIdentifier, manual: true);

            flight.InvalidateSequenceData();
            sequence.Move(flight, newIndex, forceRescheduleStable: true);

            // Unstable flights become stable when moved
            if (flight.State == State.Unstable)
                flight.SetState(State.Stable, clock);

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
