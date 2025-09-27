namespace Maestro.Core.Messages;

public record RunwayModeDto(string Identifier, Dictionary<string, int> AcceptanceRates);
