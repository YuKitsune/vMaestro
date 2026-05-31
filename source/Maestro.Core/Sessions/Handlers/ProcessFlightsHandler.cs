using Maestro.Contracts.Flights;
using Maestro.Contracts.Sessions;
using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Connectivity;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Maestro.Core.Sessions.Contracts;
using MediatR;
using Serilog;

namespace Maestro.Core.Sessions.Handlers;

public class ProcessFlightsHandler(
    ISessionManager sessionManager,
    IMaestroConnectionManager connectionManager,
    IAirportConfigurationProvider airportConfigurationProvider,
    ITrajectoryService trajectoryService,
    IMediator mediator,
    IClock clock,
    ILogger logger)
    : IRequestHandler<ProcessFlightsRequest>
{
    public async Task Handle(ProcessFlightsRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Debug("Skipping flight processing for {AirportIdentifier} as we are not the master", request.AirportIdentifier);
            return;
        }

        var session = await sessionManager.GetSession(request.AirportIdentifier, cancellationToken);
        SessionDto sessionDto;

        using (await session.Semaphore.LockAsync(cancellationToken))
        {
            var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(request.AirportIdentifier);
            var lostTimeout = TimeSpan.FromMinutes(airportConfiguration.LostFlightTimeoutMinutes);

            ProcessSequencedFlights(session, airportConfiguration, lostTimeout);
            ProcessDesequencedFlights(session, airportConfiguration, lostTimeout);

            sessionDto = session.Snapshot();
        }

        await mediator.Publish(
            new SessionUpdatedNotification(session.AirportIdentifier, sessionDto),
            cancellationToken);
    }

    void ProcessSequencedFlights(Session session, AirportConfiguration airportConfiguration, TimeSpan lostTimeout)
    {
        foreach (var flight in session.Sequence.Flights.ToList())
        {
            session.FlightDataRecords.TryGetValue(flight.Callsign, out var record);

            var isLost = record is null
                ? !flight.IsManuallyInserted
                : clock.UtcNow() - record.LastSeen > lostTimeout;
        // TODO test case: When processing a flight, and no FDR exists, nothing changes

            if (!isLost && record is not null)
            {
        // TODO test case: When processing a flight, data is updated
                UpdateFlightData(record, flight);
                RecomputeIfUnstable(flight, record, session, airportConfiguration);

        // TODO test case: When processing a flight, estimates are updated
                if (record.Position is not null && !record.Position.IsOnGround)
                    CalculateEstimates(flight, record);

        // TODO test case: When processing a flight, and it is unstable, it is repositioned based on its estimate
        // TODO test case: When processing a flight, and it is unstable, and its estimate moves ahead of a Stable, SuperStable, or Frozen flight, it does not overtake the Stable, SuperStable, or Frozen flight (Theory with InlineData)
        // TODO test case: When processing a flight, and it is unstable, and its estimate moves ahead of an Unstable flight, its position is changed
                if (flight.State is State.Unstable)
                    RepositionInSequence(flight, session);

        // TODO test case: When processing a flight, and delay is being absorbed, remaining delay is updated
                UpdateRemainingDelay(flight, airportConfiguration);
            }

        // TODO test case: When processing a flight, state is updated
            flight.UpdateStateBasedOnTime(clock, airportConfiguration);

            logger.Debug("Flight updated: {Flight}", flight);
        }
    }

    void ProcessDesequencedFlights(Session session, AirportConfiguration airportConfiguration, TimeSpan lostTimeout)
    {
        foreach (var flight in session.DeSequencedFlights)
        {
            session.FlightDataRecords.TryGetValue(flight.Callsign, out var record);

            var isLost = record is null || clock.UtcNow() - record.LastSeen > lostTimeout;

            if (!isLost)
            {
                UpdateFlightData(record, flight);
                CalculateEstimates(flight, record);
            }

            flight.UpdateStateBasedOnTime(clock, airportConfiguration);

            logger.Debug("Desequenced flight updated: {Flight}", flight);
        }
    }

    void RecomputeIfUnstable(Flight flight, FlightDataRecord record, Session session, AirportConfiguration airportConfiguration)
    {
        if (flight.State is not State.Unstable || string.IsNullOrEmpty(flight.AssignedRunwayIdentifier))
            return;

        // TODO test case: When processing a flight, and it is unstable, feeder fix changes are detected (check new FF, and trajectory)
        var fixNames = record.Estimates.Select(e => e.FixIdentifier).ToArray();
        var feederFix = record.Estimates.LastOrDefault(x => airportConfiguration.FeederFixes.Contains(x.FixIdentifier));
        var landingEstimate = record.Estimates.LastOrDefault()?.Estimate ?? flight.LandingEstimate;

        var updatedTrajectory = trajectoryService.GetTrajectory(
            flight,
            flight.AssignedRunwayIdentifier,
            flight.ApproachType,
            fixNames,
            session.Sequence.UpperWind);

        if (record.Position is not null && !record.Position.IsOnGround &&
            !flight.ManualFeederFixEstimate &&
            (string.IsNullOrEmpty(flight.FeederFixIdentifier) || flight.FeederFixEstimate > clock.UtcNow()))
        {
            flight.SetFeederFix(
                feederFix?.FixIdentifier,
                updatedTrajectory,
                feederFix?.Estimate,
                landingEstimate);
        }

        var updatedEnrouteTrajectory = trajectoryService.GetEnrouteTrajectory(
            flight.DestinationIdentifier,
            fixNames,
            feederFix?.FixIdentifier ?? string.Empty);
        flight.SetEnrouteTrajectory(updatedEnrouteTrajectory);

        logger.Debug(
            "{Callsign} allocated to RWY {Runway} APCH {ApproachType} | TTG: {TimeToGo}, P: {Pressure}, PMax: {MaxPressure}",
            flight.Callsign,
            flight.AssignedRunwayIdentifier,
            flight.ApproachType,
            updatedTrajectory.NormalTimeToGo,
            updatedTrajectory.PressureTimeToGo,
            updatedTrajectory.MaxPressureTimeToGo);
    }

    void RepositionInSequence(Flight flight, Session session)
    {
        var currentIndex = session.Sequence.IndexOf(flight);
        var earliestIndex = session.Sequence.FindLastIndex(
            currentIndex,
            f => f.AssignedRunwayIdentifier == flight.AssignedRunwayIdentifier &&
                 f.State != State.Unstable) + 1;

        var desiredIndex = session.Sequence.FindIndex(f =>
            f.LandingEstimate.IsAfter(flight.LandingEstimate));

        var newIndex = desiredIndex == -1
            ? session.Sequence.Flights.Count
            : desiredIndex;

        if (newIndex < earliestIndex)
            newIndex = earliestIndex;

        if (newIndex != currentIndex)
        {
            flight.InvalidateSequenceData();
            session.Sequence.Move(flight, newIndex);
        }
    }

    void UpdateRemainingDelay(Flight flight, AirportConfiguration airportConfiguration)
    {
        var remainingEnrouteDelay = flight.FeederFixTime - flight.FeederFixEstimate;
        var remainingTotalDelay = flight.LandingTime - flight.LandingEstimate;
        var remainingControlAction = DelayStrategyCalculator.GetControlAction(
            remainingTotalDelay,
            flight.TerminalTrajectory,
            flight.EnrouteTrajectory,
            airportConfiguration.DelayStrategy);
        flight.SetRemainingDelayData(
            new DelayDistribution(
                remainingEnrouteDelay,
                TerminalDelay: remainingTotalDelay - remainingEnrouteDelay,
                remainingControlAction));
    }

    void CalculateEstimates(Flight flight, FlightDataRecord record)
    {
        // TODO test case: When processing a flight, and manual ETA_FF is set, ETA_FF is not changed
        if (flight.ManualFeederFixEstimate)
            return;

        if (record.Position is null || record.Position.IsOnGround)
            return;

        // TODO test case: When processing a flight, and ETA_FF exists, ETA_FF is sourced from route estimates
        if (!string.IsNullOrEmpty(flight.FeederFixIdentifier))
        {
            if (flight.FeederFixEstimate <= clock.UtcNow())
                return;

            var feederFixSystemEstimate = record.Estimates.LastOrDefault(e => e.FixIdentifier == flight.FeederFixIdentifier);
            if (feederFixSystemEstimate?.Estimate != null)
            {
                logger.Debug(
                    "{Callsign} ETA_FF for {FeederFix} now {FeederFixEstimate}",
                    flight.Callsign,
                    flight.FeederFixIdentifier,
                    feederFixSystemEstimate.Estimate);

                flight.UpdateFeederFixEstimate(feederFixSystemEstimate.Estimate);
            }

            return;
        }

        // TODO test case: When processing a flight, and no FF exists, ETA_FF is ETA (last waypoint) - TTG
        var landingEstimate = record.Estimates.LastOrDefault()?.Estimate;
        if (landingEstimate is null)
        {
            logger.Warning("No estimates available for {Callsign}, cannot update estimates", flight.Callsign);
            return;
        }

        logger.Debug("{Callsign} (no FF) ETA now {LandingEstimate}", flight.Callsign, landingEstimate);
        flight.UpdateLandingEstimate(landingEstimate.Value);
    }

    static void UpdateFlightData(FlightDataRecord record, Flight flight)
    {
        flight.AircraftType = record.AircraftType;
        flight.AircraftCategory = record.AircraftCategory;
        flight.WakeCategory = record.WakeCategory;
        flight.OriginIdentifier = record.Origin;
        flight.EstimatedDepartureTime = record.EstimatedDepartureTime;
        flight.UpdatePosition(record.Position);
    }
}
