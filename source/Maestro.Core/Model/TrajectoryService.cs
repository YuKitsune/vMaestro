using Maestro.Core.Configuration;
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
    const int DefaultDescentSpeedKnots = 150;

    public Trajectory GetTrajectory(Flight flight, string runwayIdentifier, string approachType, string[] fixNames, Wind upperWind)
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

        var approachSpeed = performanceLookup.GetApproachSpeed(flight.AircraftType) ?? DefaultDescentSpeedKnots;
        return ComputeTrajectory(config, approachSpeed, upperWind ?? new Wind(0, 0));
    }

    public Trajectory GetTrajectory(
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

        var approachSpeed = performanceLookup.GetApproachSpeed(aircraftPerformanceData.TypeCode) ?? DefaultDescentSpeedKnots;
        return ComputeTrajectory(config, approachSpeed, upperWind ?? new Wind(0, 0));
    }

    public Trajectory GetAverageTrajectory(string airportIdentifier)
    {
        var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(airportIdentifier);

        if (airportConfiguration.Trajectories.Length == 0)
        {
            var defaultTtg = TimeSpan.FromMinutes(airportConfiguration.DefaultTTGMinutes);
            return new Trajectory(defaultTtg, defaultTtg, defaultTtg);
        }

        var zeroWind = new Wind(0, 0);
        var trajectories = airportConfiguration.Trajectories
            .Select(t => ComputeTrajectory(t, DefaultDescentSpeedKnots, zeroWind))
            .ToArray();

        var avgTtg = TimeSpan.FromTicks((long)trajectories.Average(t => t.TimeToGo.Ticks));
        var avgPressure = TimeSpan.FromTicks((long)trajectories.Average(t => t.Pressure.Ticks));
        var avgMaxPressure = TimeSpan.FromTicks((long)trajectories.Average(t => t.MaxPressure.Ticks));

        return new Trajectory(avgTtg, avgPressure, avgMaxPressure);
    }

    public string[] GetApproachTypes(
        string airportIdentifier,
        string? feederFixIdentifier,
        string[] fixNames,
        string runwayIdentifier,
        AircraftPerformanceData aircraftPerformanceData)
    {
        var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(airportIdentifier);

        var matches = airportConfiguration.Trajectories
            .Where(x => x.FeederFix == feederFixIdentifier)
            .Where(x => x.RunwayIdentifier == runwayIdentifier)
            .Where(x => string.IsNullOrEmpty(x.TransitionFix) || fixNames.Contains(x.TransitionFix))
            .OrderByDescending(x => string.IsNullOrEmpty(x.TransitionFix) ? 0 : 1)
            .ToArray();

        if (matches.Length == 0)
            return [];

        return matches.Select(a => a.ApproachType).Distinct().ToArray();
    }

    TrajectoryConfiguration? FindConfiguration(
        string airportIdentifier,
        string? feederFixIdentifier,
        string[] fixNames,
        string approachType,
        string runwayIdentifier)
    {
        var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(airportIdentifier);

        var matches = airportConfiguration.Trajectories
            .Where(x => x.FeederFix == feederFixIdentifier)
            .Where(x => string.IsNullOrEmpty(x.ApproachType) || x.ApproachType == approachType)
            .Where(x => x.RunwayIdentifier == runwayIdentifier)
            .Where(x => string.IsNullOrEmpty(x.TransitionFix) || fixNames.Contains(x.TransitionFix))
            .OrderByDescending(x => string.IsNullOrEmpty(x.ApproachType) ? 0 : 1)
            .ThenByDescending(x => string.IsNullOrEmpty(x.TransitionFix) ? 0 : 1)
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

    Trajectory ComputeTrajectory(TrajectoryConfiguration config, int approachSpeedKnots, Wind wind)
    {
        var ttgHours = SumEti(config.Segments, approachSpeedKnots, wind);
        var ttg = TimeSpan.FromHours(ttgHours);

        // Pressure: branch from base trajectory, fly alternative path
        var pressureHours = ComputeBranchingTrajectory(
            config.Segments,
            config.Pressure?.After,
            config.Pressure?.Segments ?? [],
            approachSpeedKnots,
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
                approachSpeedKnots,
                wind,
                ttgHours,
                "MaxPressure");

        var pressure = TimeSpan.FromHours(pressureHours);
        var maxPressure = TimeSpan.FromHours(maxPressureHours);

        return new Trajectory(ttg, pressure, maxPressure);
    }

    double ComputeBranchingTrajectory(
        TrajectorySegmentConfiguration[] baseSegments,
        string? after,
        TrajectorySegmentConfiguration[] alternativeSegments,
        int approachSpeedKnots,
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

        // Sum ETI from feeder fix through after segment, then along alternative path
        var segmentsThroughAfter = baseSegments.Take(afterIdx.Value + 1).ToArray();
        var baseThroughAfter = SumEti(segmentsThroughAfter, approachSpeedKnots, wind);
        var alternativeFromAfter = SumEti(alternativeSegments, approachSpeedKnots, wind);

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

    static double SumEti(TrajectorySegmentConfiguration[] segments, int approachSpeedKnots, Wind wind)
    {
        double total = 0;
        foreach (var segment in segments)
        {
            var headwind = wind.Speed * Math.Cos(ToRadians(segment.Track - wind.Direction));
            var groundSpeed = Math.Max(approachSpeedKnots - headwind, 1.0);
            total += segment.DistanceNM / groundSpeed;
        }
        return total;
    }

    static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
