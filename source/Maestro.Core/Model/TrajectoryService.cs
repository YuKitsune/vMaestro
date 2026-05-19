using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Integration;
using Maestro.Core.Sessions;
using Serilog;

namespace Maestro.Core.Model;

public class TrajectoryService(
    IAirportConfigurationProvider airportConfigurationProvider,
    IPerformanceLookup performanceLookup,
    ILogger logger)
    : ITrajectoryService
{
    public EnrouteTrajectory GetEnrouteTrajectory(
        string airportIdentifier,
        string[] waypointNames,
        string feederFixIdentifier)
    {
        var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(airportIdentifier);

        var enrouteTrajectoryConfiguration = airportConfiguration.EnrouteTrajectories.FirstOrDefault(c => waypointNames.Contains(c.EntryPoint) && c.FeederFix == feederFixIdentifier);
        if (enrouteTrajectoryConfiguration is null)
            return new EnrouteTrajectory(TimeSpan.FromMinutes(airportConfiguration.DefaultMaxEnrouteLinearDelayMinutes), TimeSpan.Zero);

        return new EnrouteTrajectory(
            TimeSpan.FromMinutes(enrouteTrajectoryConfiguration.MaxEnrouteLinearDelayMinutes),
            TimeSpan.FromMinutes(enrouteTrajectoryConfiguration.ShortcutTimeToGainMinutes));
    }

    public TerminalTrajectory GetTrajectory(Flight flight, string runwayIdentifier, string approachType, string[] fixNames, Wind upperWind)
    {
        var config = FindConfiguration(
            flight.DestinationIdentifier,
            flight.FeederFixIdentifier,
            fixNames,
            approachType,
            runwayIdentifier);

        if (config is null)
        {
            logger.Warning(
                "No trajectory found for {Callsign} on RWY {RunwayIdentifier} APCH {ApproachType}, using average",
                flight.Callsign,
                runwayIdentifier,
                approachType);
            return GetAverageTrajectory(flight.DestinationIdentifier);
        }

        var speedBands = performanceLookup.GetSpeedProfile(flight.GetPerformanceData());
        return ComputeTrajectory(config, speedBands, upperWind ?? new Wind(0, 0));
    }

    public TerminalTrajectory GetTrajectory(
        AircraftPerformanceData aircraftPerformanceData,
        string destinationIdentifier,
        string? feederFixIdentifier,
        string runwayIdentifier,
        string approachType,
        string[] fixNames,
        Wind upperWind)
    {
        var config = FindConfiguration(
            destinationIdentifier,
            feederFixIdentifier,
            fixNames,
            approachType,
            runwayIdentifier);

        if (config is null)
        {
            logger.Warning(
                "No trajectory found for {AircraftType} to {Destination} via {FeederFix} on RWY {RunwayIdentifier} APCH {ApproachType}, using average",
                aircraftPerformanceData.TypeCode,
                destinationIdentifier,
                feederFixIdentifier ?? "N/A",
                runwayIdentifier,
                approachType);
            return GetAverageTrajectory(destinationIdentifier);
        }

        var speedBands = performanceLookup.GetSpeedProfile(aircraftPerformanceData);
        return ComputeTrajectory(config, speedBands, upperWind ?? new Wind(0, 0));
    }

    public TerminalTrajectory GetAverageTrajectory(string airportIdentifier)
    {
        var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(airportIdentifier);

        if (airportConfiguration.TerminalTrajectories.Length == 0)
        {
            var defaultTtg = TimeSpan.FromMinutes(airportConfiguration.DefaultTimeToGoMinutes);
            return new TerminalTrajectory(defaultTtg, defaultTtg, defaultTtg);
        }

        var zeroWind = new Wind(0, 0);
        var defaultSpeedBands = performanceLookup.GetSpeedProfile(AircraftPerformanceData.Default);
        var trajectories = airportConfiguration.TerminalTrajectories
            .Select(t => ComputeTrajectory(t, defaultSpeedBands, zeroWind))
            .ToArray();

        var avgTtg = TimeSpan.FromTicks((long)trajectories.Average(t => t.NormalTimeToGo.Ticks));
        var avgPressure = TimeSpan.FromTicks((long)trajectories.Average(t => t.PressureTimeToGo.Ticks));
        var avgMaxPressure = TimeSpan.FromTicks((long)trajectories.Average(t => t.MaxPressureTimeToGo.Ticks));

        return new TerminalTrajectory(avgTtg, avgPressure, avgMaxPressure);
    }

    public string[] GetApproachTypes(
        string airportIdentifier,
        string? feederFixIdentifier,
        string[] fixNames,
        string runwayIdentifier,
        AircraftPerformanceData aircraftPerformanceData)
    {
        var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(airportIdentifier);

        var matches = airportConfiguration.TerminalTrajectories
            .Where(x => x.FeederFix == feederFixIdentifier)
            .Where(x => x.RunwayIdentifier == runwayIdentifier)
            .Where(x => string.IsNullOrEmpty(x.TransitionFix) || fixNames.Contains(x.TransitionFix))
            .OrderByDescending(x => string.IsNullOrEmpty(x.TransitionFix) ? 0 : 1)
            .ToArray();

        if (matches.Length == 0)
            return [];

        return matches.Select(a => a.ApproachType).Distinct().ToArray();
    }

    TerminalTrajectoryConfiguration? FindConfiguration(
        string airportIdentifier,
        string? feederFixIdentifier,
        string[] fixNames,
        string approachType,
        string runwayIdentifier)
    {
        var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(airportIdentifier);

        var matches = airportConfiguration.TerminalTrajectories
            .Where(x => x.FeederFix == feederFixIdentifier)
            .Where(x => x.ApproachType == approachType)
            .Where(x => x.RunwayIdentifier == runwayIdentifier)
            .Where(x => string.IsNullOrEmpty(x.TransitionFix) || fixNames.Contains(x.TransitionFix))
            .OrderByDescending(x => string.IsNullOrEmpty(x.TransitionFix) ? 0 : 1)
            .ToArray();

        if (matches.Length == 0)
        {
            logger.Warning(
                "No trajectory found: Airport={AirportIdentifier}, FF={FeederFix}, RWY={RunwayIdentifier}, APCH={ApproachType}",
                airportIdentifier,
                feederFixIdentifier,
                runwayIdentifier,
                approachType);
            return null;
        }

        if (matches.Length > 1)
        {
            logger.Warning(
                "Multiple trajectories found: Airport={AirportIdentifier}, FF={FeederFix}, RWY={RunwayIdentifier}, APCH={ApproachType}",
                airportIdentifier,
                feederFixIdentifier,
                runwayIdentifier,
                approachType);
        }

        return matches[0];
    }

    TerminalTrajectory ComputeTrajectory(TerminalTrajectoryConfiguration config, SpeedBand[] speedBands, Wind wind)
    {
        var ttgHours = SumEti(config.Segments, speedBands, wind);
        var ttg = TimeSpan.FromHours(ttgHours);

        // Pressure: branch from base trajectory, fly alternative path
        var pressureHours = ComputeBranchingTrajectory(
            config.Segments,
            config.Pressure?.After,
            config.Pressure?.Segments ?? [],
            speedBands,
            wind,
            ttgHours,
            "Pressure");

        // MaxPressure: branch from base trajectory, fly alternative path; fall back to Pressure if not configured
        var maxPressureHours = config.MaxPressure is null
            ? pressureHours
            : ComputeBranchingTrajectory(
                config.Segments,
                config.MaxPressure.After,
                config.MaxPressure.Segments,
                speedBands,
                wind,
                ttgHours,
                "MaxPressure");

        var pressure = TimeSpan.FromHours(pressureHours);
        var maxPressure = TimeSpan.FromHours(maxPressureHours);

        return new TerminalTrajectory(ttg, pressure, maxPressure);
    }

    double ComputeBranchingTrajectory(
        TrajectorySegmentConfiguration[] baseSegments,
        string? after,
        TrajectorySegmentConfiguration[] alternativeSegments,
        SpeedBand[] speedBands,
        Wind wind,
        double ttgHours,
        string trajectoryType)
    {
        // No after segment or no alternative segments: fallback to TTG
        if (string.IsNullOrEmpty(after) || alternativeSegments.Length == 0)
            return ttgHours;

        // Find after segment in base trajectory
        var afterIdx = FindSegmentIndex(baseSegments, after);
        if (afterIdx is null)
        {
            logger.Error(
                "{TrajectoryType} After segment '{After}' not found in base trajectory, using TTG",
                trajectoryType,
                after);
            return ttgHours;
        }

        // Sum ETI from feeder fix through after segment, then along alternative path.
        // For the base portion, initialDtg is the full route distance (feeder fix to runway) so
        // speed bands reflect where each segment sits relative to the runway.
        // For the alternative segments, they replace the remainder of the base route and lead to
        // the runway, so their initialDtg is their own total distance.
        var segmentsThroughAfter = baseSegments.Take(afterIdx.Value + 1).ToArray();
        var totalBaseDistance = baseSegments.Sum(s => s.DistanceNM);

        var baseThroughAfter = SumEti(segmentsThroughAfter, speedBands, wind, totalBaseDistance);
        var alternativeFromAfter = SumEti(alternativeSegments, speedBands, wind);

        return baseThroughAfter + alternativeFromAfter;
    }

    static int? FindSegmentIndex(TrajectorySegmentConfiguration[] segments, string? identifier)
    {
        if (string.IsNullOrEmpty(identifier))
            return null;

        for (var i = 0; i < segments.Length; i++)
        {
            if (segments[i].Identifier.Equals(identifier, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return null;
    }

    // Computes the total estimated time in hours for a sequence of segments, using distance-to-go
    // speed bands. The initial DTG is the total route distance (feeder fix to runway), so that speed
    // band selection reflects how far the aircraft is from the runway at each segment.
    static double SumEti(TrajectorySegmentConfiguration[] segments, SpeedBand[] speedBands, Wind wind)
    {
        var totalDistance = segments.Sum(s => s.DistanceNM);
        return SumEti(segments, speedBands, wind, totalDistance);
    }

    // Overload allowing the caller to specify the DTG at the start of the first segment.
    // Used for branching trajectories where the sub-route starts partway through the full route.
    static double SumEti(TrajectorySegmentConfiguration[] segments, SpeedBand[] speedBands, Wind wind, double initialDtg)
    {
        if (speedBands.Length == 0)
            return 0;

        var sortedBands = speedBands.OrderByDescending(b => b.ThresholdNM).ToArray();
        var dtg = initialDtg;
        double total = 0;

        foreach (var segment in segments)
        {
            var headwind = wind.Speed * Math.Cos(ToRadians(segment.Track - wind.Direction));
            var dtgStart = dtg;
            var dtgEnd = dtg - segment.DistanceNM;

            // Find all band boundaries (ThresholdNM values) that fall strictly inside (dtgEnd, dtgStart).
            // These are the points where the speed changes mid-segment and require a split.
            var splitPoints = sortedBands
                .Select(b => b.ThresholdNM)
                .Where(nm => nm > dtgEnd && nm < dtgStart)
                .ToArray(); // already sorted descending from sortedBands

            // Build breakpoints: segment start, each speed boundary, segment end.
            var breakpoints = new double[splitPoints.Length + 2];
            breakpoints[0] = dtgStart;
            splitPoints.CopyTo(breakpoints, 1);
            breakpoints[breakpoints.Length - 1] = dtgEnd;

            for (var i = 0; i < breakpoints.Length - 1; i++)
            {
                var subDtgStart = breakpoints[i];
                var subDistance = subDtgStart - breakpoints[i + 1];
                var speed = GetSpeedForDtg(sortedBands, subDtgStart);
                var groundSpeed = Math.Max(speed - headwind, 1.0);
                total += subDistance / groundSpeed;
            }

            dtg = dtgEnd;
        }

        return total;
    }

    // Returns the speed (knots) for the given distance-to-go.
    // Iterates bands sorted descending by ThresholdNM; returns the first band where dtg > ThresholdNM.
    static int GetSpeedForDtg(SpeedBand[] sortedBands, double dtg)
    {
        foreach (var band in sortedBands)
        {
            if (dtg > band.ThresholdNM)
                return band.SpeedKnots;
        }

        // Fallback: use the last (lowest) band. Handles dtg == 0 edge case.
        return sortedBands[sortedBands.Length - 1].SpeedKnots;
    }

    static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
