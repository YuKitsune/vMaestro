using Maestro.Contracts.Flights;
using Maestro.Contracts.Sessions;
using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Hosting;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class ChangeFeederFixEstimateRequestHandler(
    IMaestroInstanceManager instanceManager,
    IMaestroConnectionManager connectionManager,
    IAirportConfigurationProvider airportConfigurationProvider,
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
            logger.Information("Relaying ChangeFeederFixEstimateRequest for {Callsign} at {AirportIdentifier}", request.Callsign, request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        logger.Verbose("Changing feeder fix estimate for {Callsign} to {NewFeederFixEstimate:HHmm} at {AirportIdentifier}", request.Callsign, request.NewFeederFixEstimate, request.AirportIdentifier);

        var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(request.AirportIdentifier);

        var instance = await instanceManager.GetInstance(request.AirportIdentifier, cancellationToken);
        SessionDto sessionDto;

        using (await instance.Semaphore.LockAsync(cancellationToken))
        {
            var flight = instance.Session.Sequence.FindFlight(request.Callsign);
            if (flight == null)
            {
                logger.Warning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
                return;
            }

            flight.UpdateFeederFixEstimate(request.NewFeederFixEstimate, manual: true);

            instance.Session.Sequence.RepositionByLandingEstimate(flight);
            if (flight.State is State.Unstable)
                flight.SetState(airportConfiguration.ManualInteractionState, clock); // TODO: Make configurable

            logger.Information("{Callsign} feeder fix estimate changed to {NewFeederFixEstimate:HHmm}", flight.Callsign, request.NewFeederFixEstimate);

            sessionDto = instance.Session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(
                instance.AirportIdentifier,
                sessionDto),
            cancellationToken);
    }
}
