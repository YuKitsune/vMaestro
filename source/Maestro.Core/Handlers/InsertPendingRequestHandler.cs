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

public class InsertPendingRequestHandler(
    IMaestroInstanceManager instanceManager,
    IMaestroConnectionManager connectionManager,
    IClock clock,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<InsertPendingRequest>
{
    public async Task Handle(InsertPendingRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Information("Relaying InsertPendingRequest for {AirportIdentifier}", request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        var instance = await instanceManager.GetInstance(request.AirportIdentifier, cancellationToken);
        SessionMessage sessionMessage;

        using (await instance.Semaphore.LockAsync(cancellationToken))
        {
            var sequence = instance.Session.Sequence;

            var flight = instance.Session.PendingFlights.SingleOrDefault(f => f.Callsign == request.Callsign);
        if (flight is null)
        {
            // TODO: Confirm what should happen in this case
            // The UI seems to accept manual input
            // Maybe use Aircraft type to determine a speed and figure out a landing time from there?
            throw new MaestroException($"{request.Callsign} was not found in the pending list.");
        }

        int index;
        Runway runway;

        switch (request.Options)
        {
            case ExactInsertionOptions exactInsertionOptions:
            {
                var runwayMode = sequence.GetRunwayModeAt(exactInsertionOptions.TargetLandingTime);
                runway = runwayMode.Runways.FirstOrDefault(r => exactInsertionOptions.RunwayIdentifiers.Contains(r.Identifier))
                         ?? runwayMode.Default;

                index = sequence.IndexOf(exactInsertionOptions.TargetLandingTime);
                break;
            }
            case RelativeInsertionOptions relativeInsertionOptions:
            {
                var referenceFlight = sequence.FindFlight(relativeInsertionOptions.ReferenceCallsign);
                if (referenceFlight is null)
                    throw new MaestroException($"{relativeInsertionOptions.ReferenceCallsign} not found");

                var referenceFlightIndex = sequence.IndexOf(referenceFlight);
                index = relativeInsertionOptions.Position switch
                {
                    RelativePosition.Before => referenceFlightIndex,
                    RelativePosition.After => referenceFlightIndex + 1,
                    _ => throw new ArgumentOutOfRangeException()
                };

                var runwayMode = sequence.GetRunwayModeAt(referenceFlight.LandingTime);
                runway = runwayMode.Runways.FirstOrDefault(r => r.Identifier == referenceFlight.AssignedRunwayIdentifier)
                         ?? runwayMode.Default;
                break;
            }
            default:
                throw new NotSupportedException($"Cannot insert flight with {request.Options.GetType().Name}");
        }

        // TODO: Feels odd to do this validation here, but we don't want to mutate the runway if the insertion is invalid
        // Maybe we _can_ mutate the runway? The code will run again and re-assign the runway anyway if we try again
        sequence.ThrowIfSlotIsUnavailable(index, runway.Identifier);

        flight.SetRunway(runway.Identifier, manual: true);

        sequence.Insert(index, flight);

            flight.SetState(State.Stable, clock);

            logger.Information("Inserted pending flight {Callsign} for {AirportIdentifier}", flight.Callsign, request.AirportIdentifier);

            sessionMessage = instance.Session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                instance.AirportIdentifier,
                sessionMessage),
            cancellationToken);
    }
}
