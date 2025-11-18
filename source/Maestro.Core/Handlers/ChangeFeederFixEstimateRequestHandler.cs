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

public class ChangeFeederFixEstimateRequestHandler(
    IMaestroInstanceManager instanceManager,
    IMaestroConnectionManager connectionManager,
    IEstimateProvider estimateProvider,
    IClock clock,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<ChangeFeederFixEstimateRequest>
{
    public async Task Handle(ChangeFeederFixEstimateRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Information("Relaying ChangeFeederFixEstimateRequest for {AirportIdentifier}", request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        var instance = await instanceManager.GetInstance(request.AirportIdentifier, cancellationToken);
        SessionMessage sessionMessage;

        using (await instance.Semaphore.LockAsync(cancellationToken))
        {
            var flight = instance.Session.Sequence.FindFlight(request.Callsign);
            if (flight == null)
            {
                logger.Warning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
                return;
            }

            // TODO: Track who initiated the change
            logger.Information("Changing feeder fix estimate for flight {Callsign} to {NewFeederFixEstimate}.", request.Callsign, request.NewFeederFixEstimate);

            flight.UpdateFeederFixEstimate(request.NewFeederFixEstimate, manual: true);

            // Re-calculate the landing estimate based on the new feeder fix estimate
            var landingEstimate = estimateProvider.GetLandingEstimate(
                flight,
                flight.Fixes.Last().Estimate);
            if (landingEstimate is not null)
                flight.UpdateLandingEstimate(landingEstimate.Value);

            instance.Session.Sequence.RepositionByEstimate(flight);
            if (flight.State is State.Unstable)
                flight.SetState(State.Stable, clock); // TODO: Make configurable

            sessionMessage = instance.Session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                instance.AirportIdentifier,
                sessionMessage),
            cancellationToken);
    }
}
