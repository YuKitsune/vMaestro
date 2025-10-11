using Maestro.Core.Extensions;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Handlers;

// TODO:
// - [X] Refactor dummy flight into a separate type
// - [X] Separate pending flight insertion from dummy flight insertion
// - [ ] Update WPF to support new DummyFlight type
// - [ ] Test

public class InsertFlightRequestHandler(ISessionManager sessionManager, IMediator mediator)
    : IRequestHandler<InsertFlightRequest>
{
    const int MaxCallsignLength = 12; // TODO: Verify the VATSIM limit

    public async Task Handle(InsertFlightRequest request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        if (lockedSession.Session is { OwnsSequence: false, Connection: not null })
        {
            await lockedSession.Session.Connection.Invoke(request, cancellationToken);
            return;
        }

        var sequence = lockedSession.Session.Sequence;

        var callsign = request.Callsign?.ToUpperInvariant().Truncate(MaxCallsignLength) ?? sequence.NewDummyCallsign();
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

        await mediator.Publish(
            new SequenceUpdatedNotification(sequence.AirportIdentifier, sequence.ToMessage()),
            cancellationToken);
    }
}
