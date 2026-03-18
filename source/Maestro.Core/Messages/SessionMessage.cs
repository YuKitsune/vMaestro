namespace Maestro.Core.Messages;

/// <summary>
/// Represents the current state of a Maestro session for an airport.
/// </summary>
public class SessionMessage
{
    /// <summary>
    /// The ICAO identifier of the airport (e.g., YSSY).
    /// </summary>
    public required string AirportIdentifier { get; init; }

    /// <summary>
    /// Flights that are pending insertion into the sequence.
    /// </summary>
    public required FlightMessage[] PendingFlights { get; init; }

    /// <summary>
    /// Flights that have been temporarily removed from the sequence.
    /// </summary>
    public required FlightMessage[] DeSequencedFlights { get; init; }

    /// <summary>
    /// The current sequence state including all active flights and TMA configuration.
    /// </summary>
    public required SequenceMessage Sequence { get; init; }

    /// <summary>
    /// Counter used for generating dummy flight callsigns.
    /// </summary>
    public required int DummyCounter { get; init; }
}
