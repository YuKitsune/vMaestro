using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;

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
    IClock clock)
    : IRequestHandler<SwapFlightsRequest>
{
    public async Task Handle(SwapFlightsRequest request, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken);
        if (lockedSession.Session is { OwnsSequence: false, Connection: not null })
        {
            await lockedSession.Session.Connection.Invoke(request, cancellationToken);
            return;
        }

        var sequence = lockedSession.Session.Sequence;
        var firstFlight = sequence.FindTrackedFlight(request.FirstFlightCallsign);
        if (firstFlight is null)
            throw new MaestroException($"Could not find {request.FirstFlightCallsign}");

        var secondFlight = sequence.FindTrackedFlight(request.SecondFlightCallsign);
        if (secondFlight is null)
            throw new MaestroException($"Could not find {request.SecondFlightCallsign}");

        var firstLandingTime = firstFlight.LandingTime;
        var firstRunway = firstFlight.AssignedRunwayIdentifier;

        var secondLandingTime = secondFlight.LandingTime;
        var secondRunway = secondFlight.AssignedRunwayIdentifier;

        firstFlight.SetLandingTime(secondLandingTime, manual: true);
        firstFlight.SetRunway(secondRunway, manual: true);
        UpdateFeederFixTime(firstFlight, secondLandingTime);
        if (firstFlight.State == State.Unstable)
        {
            // Unstable flights become stable when moved
            firstFlight.SetState(State.Stable, clock);
        }
        else
        {
            firstFlight.UpdateStateBasedOnTime(clock);
        }

        secondFlight.SetLandingTime(firstLandingTime, manual: true);
        secondFlight.SetRunway(firstRunway, manual: true);
        UpdateFeederFixTime(secondFlight, firstLandingTime);
        if (secondFlight.State == State.Unstable)
        {
            // Unstable flights become stable when moved
            secondFlight.SetState(State.Stable, clock);
        }
        else
        {
            secondFlight.UpdateStateBasedOnTime(clock);
        }

        // We don't need to reschedule because we know both flights have already been scheduled

        await mediator.Publish(
            new SequenceUpdatedNotification(
                sequence.AirportIdentifier,
                sequence.ToMessage()),
            cancellationToken);
    }

    void UpdateFeederFixTime(Flight flight, DateTimeOffset newLandingTime)
    {
        if (string.IsNullOrEmpty(flight.FeederFixIdentifier) || flight.FeederFixEstimate is null || flight.HasPassedFeederFix)
            return;

        var totalDelay = newLandingTime - flight.LandingEstimate;
        var feederFixTime = flight.FeederFixEstimate.Value + totalDelay;
        flight.SetFeederFixTime(feederFixTime);
    }
}
