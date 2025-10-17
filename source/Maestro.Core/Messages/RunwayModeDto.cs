namespace Maestro.Core.Messages;

public record RunwayModeDto(
    string Identifier,
    RunwayDto[] Runways,
    int OffModeSeparationSeconds);

public record RunwayDto(
    string Identifier,
    string ApproachType,
    int AcceptanceRateSeconds,
    RunwayDependencyDto[] Dependencies);

public record RunwayDependencyDto(string RunwayIdentifier, int SeparationSeconds);
