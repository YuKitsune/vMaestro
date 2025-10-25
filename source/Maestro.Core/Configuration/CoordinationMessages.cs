namespace Maestro.Core.Configuration;

// TODO: Support airport-specific coordination messages
// TODO: Extract this into an interface with different helpers for rendering message templates
// TODO: Separate flight-specific and general messages

public class CoordinationMessageConfiguration
{
    public required string[] Templates { get; init; } = [];
}
