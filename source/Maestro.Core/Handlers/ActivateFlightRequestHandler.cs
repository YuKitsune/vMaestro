using Maestro.Contracts.Flights;
using Maestro.Contracts.Sessions;
using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Integration;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public record ActivateFlightRequest(string AirportIdentifier, string Callsign) : IRequest;

public class ActivateFlightRequestHandler(
    ISessionManager sessionManager,
    IAirportConfigurationProvider airportConfigurationProvider,
    ITrajectoryService trajectoryService,
    IClock clock,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<ActivateFlightRequest>
{
    public async Task Handle(ActivateFlightRequest request, CancellationToken cancellationToken)
    {
        var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(request.AirportIdentifier);
        var session = await sessionManager.GetSession(request.AirportIdentifier, cancellationToken);
        SessionDto? sessionDto = null;

        using (await session.Semaphore.LockAsync(cancellationToken))
        {
            var changed = await TryActivate(session, airportConfiguration, request.Callsign, request.AirportIdentifier, cancellationToken);
            if (changed)
                sessionDto = session.Snapshot();
        }

        if (sessionDto is not null)
        {
            await mediator.Publish(
                new SessionUpdatedNotification(session.AirportIdentifier, sessionDto),
                cancellationToken);
        }
    }

    Task<bool> TryActivate(
        Session session,
        AirportConfiguration airportConfiguration,
        string callsign,
        string airportIdentifier,
        CancellationToken cancellationToken)
    {
        // Race-condition guard: another FDR update may have already activated the flight
        if (session.Sequence.FindFlight(callsign) is not null ||
            session.DeSequencedFlights.Any(f => f.Callsign == callsign))
        {
            logger.Warning("{Callsign} already activated, skipping", callsign);
            return Task.FromResult(false);
        }

        if (!session.FlightDataRecords.TryGetValue(callsign, out var record))
        {
            logger.Warning("No FlightDataRecord for {Callsign}, cannot activate", callsign);
            return Task.FromResult(false);
        }

        var feederFix = record.Estimates.LastOrDefault(x => airportConfiguration.FeederFixes.Contains(x.FixIdentifier));
        var approximateLandingEstimate = record.Estimates.LastOrDefault()?.Estimate;

        if (approximateLandingEstimate is null)
        {
            logger.Debug("{Callsign} has no landing estimate, cannot activate", callsign);
            return Task.FromResult(false);
        }

        var isFromDepartureAirport = airportConfiguration.DepartureAirports.Any(d => d.Identifier == record.Origin);
        var performanceData = new AircraftPerformanceData(record.AircraftType, record.AircraftCategory, record.WakeCategory);
        var fixNames = record.Estimates.Select(e => e.FixIdentifier).ToArray();

        var runwayMode = session.Sequence.GetRunwayModeAt(approximateLandingEstimate.Value);
        var runway = runwayMode.Runways.FirstOrDefault(r => feederFix is not null && r.FeederFixes.Contains(feederFix.FixIdentifier))
                     ?? runwayMode.Default;

        var enrouteTrajectory = trajectoryService.GetEnrouteTrajectory(
            airportIdentifier,
            fixNames,
            feederFix?.FixIdentifier ?? string.Empty);

        var terminalTrajectory = trajectoryService.GetTrajectory(
            performanceData,
            airportIdentifier,
            feederFix?.FixIdentifier,
            runway.Identifier,
            runway.ApproachType,
            fixNames,
            session.Sequence.UpperWind);

        // New flights may overtake Unstable and Stable ones
        var earliestInsertionIndex = session.Sequence.FindLastIndex(f =>
            f.State is not State.Unstable and not State.Stable &&
            f.AssignedRunwayIdentifier == runway.Identifier) + 1;

        var insertionIndex = session.Sequence.FindIndex(
            earliestInsertionIndex,
            f => f.LandingEstimate.IsAfter(approximateLandingEstimate.Value));

        if (insertionIndex == -1)
            insertionIndex = session.Sequence.Flights.Count;

        var flight = new Flight(
            callsign: callsign,
            aircraftType: record.AircraftType,
            aircraftCategory: record.AircraftCategory,
            wakeCategory: record.WakeCategory,
            destinationIdentifier: airportIdentifier,
            originIdentifier: record.Origin,
            isFromDepartureAirport: isFromDepartureAirport,
            estimatedDepartureTime: record.EstimatedDepartureTime,
            assignedRunwayIdentifier: runway.Identifier,
            approachType: runway.ApproachType,
            enrouteTrajectory: enrouteTrajectory,
            terminalTrajectory: terminalTrajectory,
            feederFixIdentifier: feederFix?.FixIdentifier,
            feederFixEstimate: feederFix?.Estimate,
            landingEstimate: approximateLandingEstimate.Value,
            activatedTime: clock.UtcNow(),
            position: record.Position);

        flight.HighPriority = feederFix is null;

        session.Sequence.Insert(insertionIndex, flight);

        logger.Information("{Callsign} added to the sequence", callsign);
        logger.Information(
            "{Callsign} allocated to RWY {Runway} APCH {ApproachType} | TTG: {TimeToGo}, P: {Pressure}, PMax: {MaxPressure}",
            callsign,
            runway.Identifier,
            runway.ApproachType,
            terminalTrajectory.NormalTimeToGo,
            terminalTrajectory.PressureTimeToGo,
            terminalTrajectory.MaxPressureTimeToGo);

        return Task.FromResult(true);
    }
}
