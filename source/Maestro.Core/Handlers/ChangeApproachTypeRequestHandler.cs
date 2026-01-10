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

// TODO: test cases
//  - When changing approach type, the assigned approach type is updated
//  - When changing approach type, the landing estimate is updated based on the ETA_FF + TTG
//  - When changing approach type, and we're in online mode, relay to master


public class ChangeApproachTypeRequestHandler(
    IMaestroInstanceManager instanceManager,
    IMaestroConnectionManager connectionManager,
    IArrivalLookup arrivalLookup,
    IClock clock,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<ChangeApproachTypeRequest>
{
    public async Task Handle(ChangeApproachTypeRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Information("Relaying ChangeApproachTypeRequest for {AirportIdentifier}", request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        var instance = await instanceManager.GetInstance(request.AirportIdentifier, cancellationToken);
        SessionMessage sessionMessage;

        using (await instance.Semaphore.LockAsync(cancellationToken))
        {
            var sequence = instance.Session.Sequence;

            var flight = sequence.FindFlight(request.Callsign);
            if (flight == null)
            {
                logger.Warning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
                return;
            }

            // TODO: Track who initiated the change
            logger.Information("Changing approach type for {Callsign} to {NewApproachType}.", request.Callsign, request.ApproachType);

            // TODO: Update TTG, P and Pmax
            flight.SetApproachType(request.ApproachType);

            // Update the landing estimate
            var timeToGo = arrivalLookup.GetArrivalInterval(flight);
            if (flight.FeederFixEstimate is not null && timeToGo is not null)
            {
                var newLandingEstimate = flight.FeederFixEstimate.Value + timeToGo.Value;

                // TODO: Calculate temp ETA if no FF is available
                flight.UpdateLandingEstimate(newLandingEstimate);
            }

            sessionMessage = instance.Session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                instance.AirportIdentifier,
                sessionMessage),
            cancellationToken);
    }
}
