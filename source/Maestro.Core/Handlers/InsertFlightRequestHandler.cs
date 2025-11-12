using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class InsertFlightRequestHandler(
    ISessionManager sessionManager,
    IMaestroConnectionManager connectionManager,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<InsertFlightRequest>
{
    const int MaxCallsignLength = 12; // TODO: Verify the VATSIM limit

    public async Task Handle(InsertFlightRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Information("Relaying InsertFlightRequest for {AirportIdentifier}", request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        var sequence = lockedSession.Session.Sequence;

        var callsign = request.Callsign?.ToUpperInvariant().Truncate(MaxCallsignLength)!;
        if (string.IsNullOrEmpty(callsign))
            callsign = lockedSession.Session.NewDummyCallsign();

        var state = State.Frozen; // TODO: Make this configurable

        int index;
        string runwayIdentifier;
        DateTimeOffset landingTime;

        switch (request.Options)
        {
            case ExactInsertionOptions exactInsertionOptions:
            {
                index = sequence.IndexOf(exactInsertionOptions.TargetLandingTime);

                var runwayMode = sequence.GetRunwayModeAt(index);
                var runway = runwayMode.Runways.FirstOrDefault(r => exactInsertionOptions.RunwayIdentifiers.Contains(r.Identifier)) ?? runwayMode.Default;
                runwayIdentifier = runway.Identifier;
                landingTime = exactInsertionOptions.TargetLandingTime;
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

                runwayIdentifier = referenceFlight.AssignedRunwayIdentifier;

                var runwayMode = sequence.GetRunwayModeAt(index);
                var runway = runwayMode.Runways.FirstOrDefault(r => r.Identifier == runwayIdentifier) ?? runwayMode.Default;

                landingTime = relativeInsertionOptions.Position switch
                {
                    RelativePosition.Before => referenceFlight.LandingTime,
                    RelativePosition.After => referenceFlight.LandingTime.Add(runway.AcceptanceRate),
                    _ => throw new ArgumentOutOfRangeException()
                };
                break;
            }
            default:
                throw new NotSupportedException($"Cannot insert flight with {request.Options.GetType().Name}");
        }

        var flight = new Flight(
            callsign,
            request.AircraftType,
            request.AirportIdentifier,
            runwayIdentifier,
            landingTime,
            state);

        sequence.Insert(index, flight);

        logger.Information("Inserted dummy flight {Callsign} for {AirportIdentifier}", callsign, request.AirportIdentifier);

        await mediator.Publish(
            new SequenceUpdatedNotification(sequence.AirportIdentifier, sequence.ToMessage()),
            cancellationToken);
    }
}
