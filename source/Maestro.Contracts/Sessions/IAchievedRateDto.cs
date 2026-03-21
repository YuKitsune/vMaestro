using System.Text.Json.Serialization;
using MessagePack;

namespace Maestro.Contracts.Sessions;

/// <summary>
/// Represents the achieved landing rate for a runway.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(NoDeviationDto), "NoDeviation")]
[JsonDerivedType(typeof(AchievedRateDto), "AchievedRate")]
[Union(0, typeof(NoDeviationDto))]
[Union(1, typeof(AchievedRateDto))]
public interface IAchievedRateDto;

/// <summary>
/// Indicates no significant deviation from the desired landing rate.
/// </summary>
[MessagePackObject]
public record NoDeviationDto : IAchievedRateDto;

/// <summary>
/// Represents the achieved landing rate with deviation from the desired rate.
/// </summary>
/// <param name="AverageLandingInterval">The average time interval between landings.</param>
/// <param name="LandingIntervalDeviation">The deviation from the desired landing interval. Positive indicates busier than configured, negative indicates quieter.</param>
[MessagePackObject]
public record AchievedRateDto(
    [property: Key(0)] TimeSpan AverageLandingInterval,
    [property: Key(1)] TimeSpan LandingIntervalDeviation)
    : IAchievedRateDto;
