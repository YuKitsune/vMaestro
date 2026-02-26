namespace Maestro.Core.Model;

public interface ITrajectoryService
{
    Trajectory GetTrajectory(Flight flight, string runwayIdentifier, string approachType);

    Trajectory GetTrajectory(
        string aircraftType,
        AircraftCategory aircraftCategory,
        string destinationIdentifier,
        string? feederFixIdentifier,
        string runwayIdentifier,
        string approachType);

    Trajectory GetAverageTrajectory(string airportIdentifier);
}
