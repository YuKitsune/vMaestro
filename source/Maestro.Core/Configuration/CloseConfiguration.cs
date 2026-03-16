namespace Maestro.Core.Configuration;

/// <summary>
///     Configuration for close airports where departing traffic may still be climbing
///     by the time they would normally become stable.
/// </summary>
public class CloseConfiguration
{
    /// <summary>
    ///     The ICAO identifier for the close airport.
    /// </summary>
    public required string Identifier { get; init; }

    /// <summary>
    ///     Per-airport minimum unstable time in minutes.
    ///     If null, uses AirportConfiguration.MinimumUnstableCloseMinutes.
    /// </summary>
    public int? MinimumUnstableMinutes { get; init; }
}
