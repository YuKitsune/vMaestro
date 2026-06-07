using Maestro.Contracts.Flights;
using Maestro.Contracts.Runway;
using Maestro.Contracts.Sessions;
using System.Text.Json.Serialization;
using Maestro.Contracts.Slots;
using MessagePack;

namespace Maestro.Contracts.Sessions;

/// <summary>
/// Represents the current state of the arrival sequence.
/// </summary>
[MessagePackObject]
public class SequenceDto
{
    /// <summary>
    /// The currently active runway mode configuration.
    /// </summary>
    [Key(0)]
    public required RunwayModeDto CurrentRunwayMode { get; init; }

    /// <summary>
    /// The next scheduled runway mode, if a mode change is pending.
    /// </summary>
    [Key(1)]
    public required IConfigurationChangeDto? PendingConfigurationChange { get; init; }

    /// <summary>
    /// All flights currently in the sequence, ordered by landing time.
    /// </summary>
    [Key(2)]
    public required FlightDto[] Flights { get; init; }

    /// <summary>
    /// Time slots that restrict runway availability.
    /// </summary>
    [Key(3)]
    public required SlotDto[] Slots { get; init; }

    /// <summary>
    /// Surface wind direction and speed.
    /// </summary>
    [Key(4)]
    public required WindDto SurfaceWind { get; init; }

    /// <summary>
    /// Upper wind direction and speed.
    /// </summary>
    [Key(5)]
    public required WindDto UpperWind { get; init; }

    /// <summary>
    /// Whether the wind values were manually provided by the user or automatically calculated.
    /// </summary>
    [Key(6)]
    public required bool ManualWind { get; init; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "Type")]
[JsonDerivedType(typeof(TerminalConfigurationChangeDto), "TerminalConfigurationChange")]
[JsonDerivedType(typeof(LandingRatesChangeDto), "LandingRatesChange")]
[Union(0, typeof(TerminalConfigurationChangeDto))]
[Union(1, typeof(LandingRatesChangeDto))]
public interface IConfigurationChangeDto;

[MessagePackObject]
public record TerminalConfigurationChangeDto(
    [property: Key(0)] RunwayModeDto NewRunwayMode,
    [property: Key(1)] DateTimeOffset LastLandingTimeInPreviousMode,
    [property: Key(2)] DateTimeOffset FirstLandingTimeInNewMode)
    : IConfigurationChangeDto;

[MessagePackObject]
public record LandingRatesChangeDto(
    [property: Key(0)] IReadOnlyDictionary<string, TimeSpan> NewLandingRates,
    [property: Key(1)] DateTimeOffset ChangeTime)
    : IConfigurationChangeDto;
