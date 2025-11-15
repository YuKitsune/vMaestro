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

public class InsertOvershootRequestHandler(
    IMaestroInstanceManager instanceManager,
    IMaestroConnectionManager connectionManager,
    IClock clock,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<InsertOvershootRequest>
{
    public async Task Handle(InsertOvershootRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Information("Relaying InsertOvershootRequest for {AirportIdentifier}", request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        var instance = await instanceManager.GetInstance(request.AirportIdentifier, cancellationToken);
        SessionMessage sessionMessage;

        using (await instance.Semaphore.LockAsync(cancellationToken))
        {
            var sequence = instance.Session.Sequence;

        // BUG: If inserting after a frozen flight, nothing happens
        var landedFlight = sequence.FindFlight(request.Callsign);
        if (landedFlight is null)
        {
            throw new MaestroException($"Flight {request.Callsign} not found in landed flights");
        }

        int index;
        Runway runway;

        switch (request.Options)
        {
            case ExactInsertionOptions exactInsertionOptions:
            {
                index = sequence.IndexOf(exactInsertionOptions.TargetLandingTime);

                var runwayMode = sequence.GetRunwayModeAt(exactInsertionOptions.TargetLandingTime);
                runway = runwayMode.Runways.FirstOrDefault(r => exactInsertionOptions.RunwayIdentifiers.Contains(r.Identifier)) ?? runwayMode.Default;
                break;
            }
            case RelativeInsertionOptions relativeInsertionOptions:
            {
                var referenceFlight = sequence.FindFlight(relativeInsertionOptions.ReferenceCallsign);
                if (referenceFlight == null)
                    throw new MaestroException($"Reference flight {relativeInsertionOptions.ReferenceCallsign} not found in sequence.");

                index = relativeInsertionOptions.Position switch
                {
                    RelativePosition.Before => sequence.IndexOf(referenceFlight.LandingTime),
                    RelativePosition.After => sequence.IndexOf(referenceFlight.LandingTime) + 1,
                    _ => throw new ArgumentOutOfRangeException()
                };

                var runwayMode = sequence.GetRunwayModeAt(referenceFlight.LandingTime);
                runway = runwayMode.Runways.FirstOrDefault(r => r.Identifier == referenceFlight.AssignedRunwayIdentifier) ?? runwayMode.Default;
                break;
            }
            default:
                throw new NotSupportedException($"Cannot insert flight with {request.Options.GetType().Name}");
        }

            // TODO: What do we do about the ETA? If they've landed, the ETA is gonna be inaccurate.

            sequence.ThrowIfSlotIsUnavailable(index, runway.Identifier);
            landedFlight.SetRunway(runway.Identifier, manual: true);
            sequence.Move(landedFlight, index);

            landedFlight.SetState(State.Frozen, clock);

            logger.Information("Inserted overshoot flight {Callsign} for {AirportIdentifier}", landedFlight.Callsign, request.AirportIdentifier);

            sessionMessage = instance.Session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                instance.AirportIdentifier,
                sessionMessage),
            cancellationToken);
    }
}
