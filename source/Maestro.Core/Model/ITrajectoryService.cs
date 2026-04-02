using Maestro.Core.Integration;
using Maestro.Core.Sessions;

namespace Maestro.Core.Model;

public interface ITrajectoryService
{
    Trajectory GetTrajectory(
        Flight flight,
        string runwayIdentifier,
        string approachType,
        string[] fixNames,
        Wind upperWind);

    Trajectory GetTrajectory(
        AircraftPerformanceData aircraftPerformanceData,
        string destinationIdentifier,
        string? feederFixIdentifier,
        string runwayIdentifier,
        string approachType,
        string[] fixNames,
        Wind upperWind);

    Trajectory GetAverageTrajectory(string airportIdentifier);

    string[] GetApproachTypes(
        string airportIdentifier,
        string? feederFixIdentifier,
        string[] fixNames,
        string runwayIdentifier,
        AircraftPerformanceData aircraftPerformanceData);
}
