using Maestro.Contracts.Shared;

namespace Maestro.Core.Configuration;

public class AircraftPerformanceConfiguration
{
    /// <summary>
    ///     The ICAO aircraft type code (e.g. "B738", "DH8D").
    /// </summary>
    public required string TypeCode { get; init; }

    /// <summary>
    ///     The descent speed in knots (TAS) used for trajectory time calculations.
    ///     Extracted from the vatSys Performance.xml descent speed at the TMA entry altitude.
    /// </summary>
    public required int DescentSpeedKnots { get; init; }

    /// <summary>
    ///     Whether the aircraft is jet-powered.
    /// </summary>
    public required bool IsJet { get; init; }

    /// <summary>
    ///     The ICAO wake turbulence category.
    /// </summary>
    public required WakeCategory WakeCategory { get; init; }
}
