using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

// TODO: What if the runway doesn't exist in the current mode?
// TODO: What if the runway doesn't accept arrivals from that feeder fix?

// TODO: Test cases:
// - When swapping two flights, their landing times are swapped
// - When swapping two flights, their feeder fix times are updated
// - When swapping two flights, their runways are swapped
// - When swapping two flights, and they are unstable, they become stable
// - When swapping two flights, and one flight doesn't exist, an error is thrown

public class SwapFlightsRequestHandler(
    ISessionManager sessionManager,
    IMediator mediator,
    IClock clock,
    ILogger logger)
    : IRequestHandler<SwapFlightsRequest>
{
    public async Task Handle(SwapFlightsRequest request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        if (lockedSession.Session is { OwnsSequence: false, Connection: not null })
        {
            logger.Information("Relaying SwapFlightsRequest for {AirportIdentifier}", request.AirportIdentifier);
            await lockedSession.Session.Connection.Invoke(request, cancellationToken);
            return;
        }

        var sequence = lockedSession.Session.Sequence;
        sequence.SwapFlights(request.FirstFlightCallsign, request.SecondFlightCallsign);

        var firstFlight = sequence.FindTrackedFlight(request.FirstFlightCallsign);
        if (firstFlight is null)
            throw new MaestroException($"Could not find {request.FirstFlightCallsign}");

        var secondFlight = sequence.FindTrackedFlight(request.SecondFlightCallsign);
        if (secondFlight is null)
            throw new MaestroException($"Could not find {request.SecondFlightCallsign}");

        // Unstable flights become stable when moved
        if (firstFlight.State == State.Unstable) firstFlight.SetState(State.Stable, clock);
        if (secondFlight.State == State.Unstable) secondFlight.SetState(State.Stable, clock);

        logger.Information("Flights {FirstFlightCallsign} and {SecondFlightCallsign} swapped", firstFlight.Callsign, secondFlight.Callsign);

        await mediator.Publish(
            new SequenceUpdatedNotification(
                sequence.AirportIdentifier,
                sequence.ToMessage()),
            cancellationToken);
    }
}
