using System.Text.Json.Serialization;
using MessagePack;

namespace Maestro.Contracts.Flights;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "optionsType")]
[JsonDerivedType(typeof(RelativeInsertionOptions), "Relative")]
[JsonDerivedType(typeof(ExactInsertionOptions), "Exact")]
[JsonDerivedType(typeof(DepartureInsertionOptions), "Departure")]
[JsonDerivedType(typeof(FdrInsertionOptions), "Fdr")]
[Union(0, typeof(RelativeInsertionOptions))]
[Union(1, typeof(ExactInsertionOptions))]
[Union(2, typeof(DepartureInsertionOptions))]
[Union(3, typeof(FdrInsertionOptions))]
public interface IInsertFlightOptions;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RelativePosition
{
    Before,
    After
}

[MessagePackObject]
public record RelativeInsertionOptions(
    [property: Key(0)] string ReferenceCallsign,
    [property: Key(1)] RelativePosition Position)
    : IInsertFlightOptions;

[MessagePackObject]
public record ExactInsertionOptions(
    [property: Key(0)] DateTimeOffset TargetLandingTime,
    [property: Key(1)] string[] RunwayIdentifiers)
    : IInsertFlightOptions;

[MessagePackObject]
public record DepartureInsertionOptions(
    [property: Key(0)] string OriginIdentifier,
    [property: Key(1)] DateTimeOffset TakeoffTime)
    : IInsertFlightOptions;

/// <summary>
/// Triggers automatic insertion based on FDR data already stored in <c>FlightDataRecords</c>.
/// Used by <c>FlightPlanUpdatedHandler</c> when a new flight is detected.
/// </summary>
[MessagePackObject]
public record FdrInsertionOptions : IInsertFlightOptions;
