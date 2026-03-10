using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Integration;
using Serilog;

namespace Maestro.Core.Model;

public class TrajectoryService(IAirportConfigurationProvider airportConfigurationProvider, ILogger logger)
    : ITrajectoryService
{
    public Trajectory GetTrajectory(Flight flight, string runwayIdentifier, string approachType)
    {
        var trajectory = GetTrajectoryInternal(
            flight.DestinationIdentifier,
            flight.FeederFixIdentifier,
            flight.Fixes.Select(x => x.FixIdentifier).ToArray(),
            approachType,
            runwayIdentifier,
            flight.GetPerformanceData());

        if (trajectory is null)
        {
            logger.Warning(
                "No trajectory found for {Callsign} on RWY {RunwayIdentifier} APCH {ApproachType}, using average",
                flight.Callsign,
                runwayIdentifier,
                approachType);
            return GetAverageTrajectory(flight.DestinationIdentifier);
        }

        return trajectory;
    }

    public Trajectory GetTrajectory(
        AircraftPerformanceData aircraftPerformanceData,
        string destinationIdentifier,
        string? feederFixIdentifier,
        string runwayIdentifier,
        string approachType)
    {
        var trajectory = GetTrajectoryInternal(
            destinationIdentifier,
            feederFixIdentifier,
            [],
            approachType,
            runwayIdentifier,
            aircraftPerformanceData);

        if (trajectory is null)
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

        return trajectory;
    }

    public Trajectory GetAverageTrajectory(string airportIdentifier)
    {
        var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(airportIdentifier);

        var allIntervals = airportConfiguration.Trajectories
            .Select(a => a.TimeToGoMinutes)
            .ToList();

        if (allIntervals.Count == 0)
        {
            // TODO: Make default trajectory configurable
            logger.Warning("No arrival intervals for {AirportIdentifier}, using default TTG", airportIdentifier);
            return new Trajectory(TimeSpan.FromMinutes(20));
        }

        var averageTtg = TimeSpan.FromMinutes(allIntervals.Average());
        return new Trajectory(averageTtg);
    }

    public string[] GetApproachTypes(
        string airportIdentifier,
        string? feederFixIdentifier,
        string[] fixNames,
        string runwayIdentifier,
        AircraftPerformanceData aircraftPerformanceData)
    {
        var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(airportIdentifier);

        var foundArrivalConfigurations = airportConfiguration.Trajectories
            .Where(x => x.FeederFix == feederFixIdentifier)
            .Where(x => x.RunwayIdentifier == runwayIdentifier)
            .Where(x => string.IsNullOrEmpty(x.ApproachFix) || fixNames.Contains(x.ApproachFix))
            .Where(x => x.Aircraft.Matches(aircraftPerformanceData))
            .OrderByDescending(GetRank)
            .ToArray();

        if (foundArrivalConfigurations.Length == 0)
            return [];

        if (foundArrivalConfigurations.Length > 1)
        {
            logger.Warning(
                "Multiple approach types found: Airport={AirportIdentifier}, FF={FeederFix}, RWY={RunwayIdentifier}, Type={Type}",
                airportIdentifier,
                feederFixIdentifier,
                runwayIdentifier,
                aircraftPerformanceData.TypeCode);
        }

        return foundArrivalConfigurations.Select(a => a.ApproachType).ToArray();

        int GetRank(TrajectoryConfiguration trajectoryConfiguration)
        {
            var rank = 0;
            if (!string.IsNullOrEmpty(trajectoryConfiguration.ApproachFix) &&
                fixNames.Contains(trajectoryConfiguration.ApproachFix))
            {
                rank++;
            }

            return rank;
        }
    }

    Trajectory? GetTrajectoryInternal(
        string airportIdentifier,
        string? feederFixIdentifier,
        string[] fixNames,
        string approachType,
        string runwayIdentifier,
        AircraftPerformanceData aircraftPerformanceData)
    {
        var airportConfiguration = airportConfigurationProvider.GetAirportConfiguration(airportIdentifier);

        var foundArrivalConfigurations = airportConfiguration.Trajectories
            .Where(x => x.FeederFix == feederFixIdentifier)
            .Where(x => x.ApproachType == approachType)
            .Where(x => string.IsNullOrEmpty(x.ApproachFix) || fixNames.Contains(x.ApproachFix))
            .Where(x => x.RunwayIdentifier == runwayIdentifier)
            .Where(x => x.Aircraft.Matches(aircraftPerformanceData))
            .OrderByDescending(GetRank)
            .ToArray();

        // No matches, return null (caller should use GetAverageTrajectory as fallback)
        if (foundArrivalConfigurations.Length == 0)
        {
            logger.Warning(
                "No trajectory found: Airport={AirportIdentifier}, FF={FeederFix}, RWY={RunwayIdentifier}, APCH={ApproachType}, Type={Type}",
                airportIdentifier,
                feederFixIdentifier,
                runwayIdentifier,
                approachType,
                aircraftPerformanceData.TypeCode);

            return null;
        }

        if (foundArrivalConfigurations.Length > 1)
        {
            logger.Warning(
                "Multiple trajectories found: Airport={AirportIdentifier}, FF={FeederFix}, RWY={RunwayIdentifier}, APCH={ApproachType}, Type={Type}",
                airportIdentifier,
                feederFixIdentifier,
                runwayIdentifier,
                approachType,
                aircraftPerformanceData.TypeCode);
        }

        var ttg = foundArrivalConfigurations.Average(x => x.TimeToGoMinutes);

        return new Trajectory(TimeSpan.FromMinutes(ttg));

        int GetRank(TrajectoryConfiguration trajectoryConfiguration)
        {
            var rank = 0;
            if (!string.IsNullOrEmpty(trajectoryConfiguration.ApproachType) &&
                trajectoryConfiguration.ApproachType == approachType)
            {
                rank++;
            }

            if (!string.IsNullOrEmpty(trajectoryConfiguration.ApproachFix) &&
                fixNames.Contains(trajectoryConfiguration.ApproachFix))
            {
                rank++;
            }

            return rank;
        }
    }
}
