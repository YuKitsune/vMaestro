namespace Maestro.Core.Messages;

public record RunwayModeDto(string Identifier, RunwayDto[] Runways);
public record RunwayDto(string Identifier, string ApproachType, int AcceptanceRateSeconds, string[] FeederFixes);
