using Maestro.Contracts.Flights;
using MessagePack;

namespace Maestro.Contracts.Sessions;

/// <summary>
/// Represents the current state of a Maestro session for an airport.
/// </summary>
[MessagePackObject]
public class SessionDto
{
    /// <summary>
    /// The ICAO identifier of the airport (e.g., YSSY).
    /// </summary>
    [Key(0)]
    public required string AirportIdentifier { get; init; }

    /// <summary>
    /// Flights that are pending insertion into the sequence.
    /// </summary>
    [Key(1)]
    public required FlightDto[] PendingFlights { get; init; }

    /// <summary>
    /// Flights that have been temporarily removed from the sequence.
    /// </summary>
    [Key(2)]
    public required FlightDto[] DeSequencedFlights { get; init; }

    /// <summary>
    /// The current sequence state including all active flights and TMA configuration.
    /// </summary>
    [Key(3)]
    public required SequenceDto Sequence { get; init; }

    /// <summary>
    /// Counter used for generating dummy flight callsigns.
    /// </summary>
    [Key(4)]
    public required int DummyCounter { get; init; }

    /// <summary>
    /// Landing statistics tracking actual landing times and achieved rates.
    /// </summary>
    [Key(5)]
    public required LandingStatisticsDto LandingStatistics { get; init; }

    /// <summary>
    /// Surface wind direction and speed.
    /// </summary>
    [Key(6)]
    public required WindDto SurfaceWind { get; init; }

    /// <summary>
    /// Upper wind direction and speed.
    /// </summary>
    [Key(7)]
    public required WindDto UpperWind { get; init; }

    /// <summary>
    /// Whether the wind values were manually provided by the user or automatically calculated.
    /// </summary>
    [Key(8)]
    public required bool ManualWind { get; init; }
}
