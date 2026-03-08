namespace Maestro.Core.Configuration;

public class DepartureConfiguration
{
    // Lookup Parameters
    public required string Identifier { get; init; }
    // public required string FeederFix { get; init; } // Reserved for future use
    public required IAircraftDescriptor[] Aircraft { get; init; } // TODO: Match any

    // Lookup Values
    public required double Distance { get; init; } // Reserved for future use

    /// <summary>
    ///     The time it would take for an aircraft matching <see cref="Aircraft"/> to fly from <see cref="Identifier"/>
    ///     to the sequenced airport.
    /// </summary>
    public required int EstimatedFlightTimeMinutes { get; init; }
}
