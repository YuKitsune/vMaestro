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
        Wind upperWind)
    {
        var config = FindConfiguration(
            destinationIdentifier,
            feederFixIdentifier,
            [],
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

    static Trajectory ComputeTrajectory(TrajectoryConfiguration config, int approachSpeedKnots, Wind wind)
    {
        double ttgHours = 0;
        double pressureHours = 0;
        double maxPressureHours = 0;

        foreach (var segment in config.Segments)
        {
            var headwind = wind.Speed * Math.Cos(ToRadians(segment.Track - wind.Direction));
            var groundSpeed = Math.Max(approachSpeedKnots - headwind, 1.0);
            var eti = segment.DistanceNM / groundSpeed;

            if (segment.MaxPressure)
                maxPressureHours += eti;
            else if (segment.Pressure)
                pressureHours += eti;
            else
                ttgHours += eti;
        }

        var ttg = TimeSpan.FromHours(ttgHours);
        var pressure = TimeSpan.FromHours(ttgHours + pressureHours);
        var maxPressure = TimeSpan.FromHours(ttgHours + pressureHours + maxPressureHours);

        return new Trajectory(ttg, pressure, maxPressure);
    }

    static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
