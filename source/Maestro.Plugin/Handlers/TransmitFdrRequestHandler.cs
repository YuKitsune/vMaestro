using Maestro.Contracts.Flights;
using Maestro.Contracts.Shared;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;
using vatsys;
using Coordinate = Maestro.Contracts.Shared.Coordinate;

namespace Maestro.Plugin.Handlers;

public record TransmitFdrRequest(FDP2.FDR Fdr) : IRequest;

public class TransmitFdrRequestHandler(IMediator mediator, ISessionManager sessionManager, ILogger logger)
    : IRequestHandler<TransmitFdrRequest>
{
    public async Task Handle(TransmitFdrRequest request, CancellationToken cancellationToken)
    {
        var updated = request.Fdr;

        // BUG: FDR updates can be sent before the instance manager has been created, in which case we miss updates
        //  When an instance is created, scan the active FDRs to ensure it's populated.
        if (!sessionManager.SessionExists(updated.DesAirport))
            return;

        var routeSegments = updated.ParsedRoute
            .ToArray() // Materialize to avoid mutation during enumeration
            .Select((s, i) => (Segment: s, Index: i, Dto: new FixEstimate(s.Intersection.Name, ToDateTimeOffset(s.ETO))))
            .Where(x => x.Index > updated.ParsedRoute.OverflownIndex && x.Segment.Type == FDP2.FDR.ExtractedRoute.Segment.SegmentTypes.WAYPOINT)
            .ToArray();

        // If any remaining estimates are null, ETOs haven't finished computing yet — wait for the next update
        if (routeSegments.Any(x => x.Dto.Estimate == DateTimeOffset.MaxValue))
        {
            logger.Verbose("{Callsign} skipped: one or more ETOs not yet computed", updated.Callsign);
            return;
        }

        var state = updated.State switch
        {
            FDP2.FDR.FDRStates.STATE_NULL or FDP2.FDR.FDRStates.STATE_SUSPENDED or FDP2.FDR.FDRStates.STATE_INACTIVE or FDP2.FDR.FDRStates.STATE_PREACTIVE or FDP2.FDR.FDRStates.STATE_COORDINATED => FlightPlanState.Preactive,
            FDP2.FDR.FDRStates.STATE_FINISHED => FlightPlanState.Completed,
            _ => FlightPlanState.Active
        };

        var estimates = routeSegments.Select(x => x.Dto).ToArray();

        FlightPosition? position = null;
        if (updated.CoupledTrack is not null)
        {
            var track = updated.CoupledTrack;
            var verticalTrack = track.VerticalSpeed >= RDP.VS_CLIMB
                ? VerticalTrack.Climbing
                : track.VerticalSpeed <= RDP.VS_DESCENT
                    ? VerticalTrack.Descending
                    : VerticalTrack.Maintaining;

            position = new FlightPosition(
                new Coordinate(track.LatLong.Latitude, track.LatLong.Longitude),
                track.CorrectedAltitude,
                verticalTrack,
                track.GroundSpeed,
                track.OnGround);
        }

        // PerformanceData can be null
        var aircraftCategory = updated.PerformanceData is null || updated.PerformanceData.IsJet
            ? AircraftCategory.Jet
            : AircraftCategory.NonJet;

        var wake = updated.AircraftWake switch
        {
            "J" => WakeCategory.SuperHeavy,
            "H" => WakeCategory.Heavy,
            "M" => WakeCategory.Medium,
            "L" => WakeCategory.Light,
            _ => WakeCategory.Heavy
        };

        var notification = new FlightPlanUpdatedNotification(
            updated.Callsign,
            updated.AircraftType,
            aircraftCategory,
            wake,
            updated.DepAirport,
            updated.DesAirport,
            ToDateTimeOffset(updated.ETD),
            updated.EET,
            state,
            position,
            estimates);

        await mediator.Publish(notification, cancellationToken);
    }

    static DateTimeOffset ToDateTimeOffset(DateTime dateTime)
    {
        if (dateTime == DateTime.MaxValue)
            return DateTimeOffset.MaxValue;

        return new DateTimeOffset(
            dateTime.Year, dateTime.Month, dateTime.Day,
            dateTime.Hour, dateTime.Minute, dateTime.Second, dateTime.Millisecond,
            TimeSpan.Zero);
    }
}
