using Serilog;

namespace Maestro.Core.Model;

public class TrajectoryService(IArrivalLookup arrivalLookup, ILogger logger) : ITrajectoryService
{
    public Trajectory GetTrajectory(Flight flight, string runwayIdentifier, string approachType)
    {
        var trajectory = arrivalLookup.GetTrajectory(flight, runwayIdentifier, approachType);
        if (trajectory is null)
        {
            logger.Warning(
                "No trajectory found for {Callsign} on RWY {RunwayIdentifier} APCH {ApproachType}, using average",
                flight.Callsign,
                runwayIdentifier,
                approachType);
            return arrivalLookup.GetAverageTrajectory(flight.DestinationIdentifier);
        }

        return trajectory;
    }

    public Trajectory GetTrajectory(
        string aircraftType,
        AircraftCategory aircraftCategory,
        string destinationIdentifier,
        string? feederFixIdentifier,
        string runwayIdentifier,
        string approachType)
    {
        var trajectory = arrivalLookup.GetTrajectory(
            destinationIdentifier,
            feederFixIdentifier,
            [],
            approachType,
            runwayIdentifier,
            aircraftType,
            aircraftCategory);

        if (trajectory is null)
        {
            logger.Warning(
                "No trajectory found for {AircraftType} to {Destination} via {FeederFix} on RWY {RunwayIdentifier} APCH {ApproachType}, using average",
                aircraftType,
                destinationIdentifier,
                feederFixIdentifier ?? "N/A",
                runwayIdentifier,
                approachType);
            return arrivalLookup.GetAverageTrajectory(destinationIdentifier);
        }

        return trajectory;
    }

    public Trajectory GetAverageTrajectory(string airportIdentifier)
    {
        return arrivalLookup.GetAverageTrajectory(airportIdentifier);
    }
}
