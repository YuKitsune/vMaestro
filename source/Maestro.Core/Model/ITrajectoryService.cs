using Maestro.Core.Integration;
using Maestro.Core.Sessions;

namespace Maestro.Core.Model;

public interface ITrajectoryService
{
    EnrouteTrajectory GetEnrouteTrajectory(string airportIdentifier, string[] waypointNames, string feederFixIdentifier);

    TerminalTrajectory GetTrajectory(Flight flight, string runwayIdentifier, string approachType, string[] fixNames, Wind upperWind);

    TerminalTrajectory GetTrajectory(
        AircraftPerformanceData aircraftPerformanceData,
        string destinationIdentifier,
        string? feederFixIdentifier,
        string runwayIdentifier,
        string approachType,
        string[] fixNames,
        Wind upperWind);

    TerminalTrajectory GetAverageTrajectory(string airportIdentifier);

    string[] GetApproachTypes(
        string airportIdentifier,
        string? feederFixIdentifier,
        string[] fixNames,
        string runwayIdentifier,
        AircraftPerformanceData aircraftPerformanceData);
}
