using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

// TODO:
// - [X] Refactor dummy flight into a separate type
// - [X] Separate pending flight insertion from dummy flight insertion
// - [ ] Update WPF to support new DummyFlight type
// - [ ] Test

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
            callsign = sequence.NewDummyCallsign();

        var state = State.Frozen; // TODO: Make this configurable

        switch (request.Options)
        {
            case ExactInsertionOptions exactInsertionOptions:
                sequence.InsertDummyFlight(
                    callsign,
                    request.AircraftType ?? string.Empty,
                    exactInsertionOptions.TargetLandingTime,
                    exactInsertionOptions.RunwayIdentifiers,
                    state);
                break;
            case RelativeInsertionOptions relativeInsertionOptions:
                sequence.InsertDummyFlight(
                    callsign,
                    request.AircraftType ?? string.Empty,
                    relativeInsertionOptions.Position,
                    relativeInsertionOptions.ReferenceCallsign,
                    state);
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }

        logger.Information("Inserted dummy flight {Callsign} for {AirportIdentifier}", callsign, request.AirportIdentifier);

        await mediator.Publish(
            new SequenceUpdatedNotification(sequence.AirportIdentifier, sequence.ToMessage()),
            cancellationToken);
    }
}
