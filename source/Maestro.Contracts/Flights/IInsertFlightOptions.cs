using System.Text.Json.Serialization;
using MessagePack;

namespace Maestro.Contracts.Flights;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "optionsType")]
[JsonDerivedType(typeof(RelativeInsertionOptions), "Relative")]
[JsonDerivedType(typeof(ExactInsertionOptions), "Exact")]
[JsonDerivedType(typeof(DepartureInsertionOptions), "Departure")]
[Union(0, typeof(RelativeInsertionOptions))]
[Union(1, typeof(ExactInsertionOptions))]
[Union(2, typeof(DepartureInsertionOptions))]
public interface IInsertFlightOptions;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RelativePosition
{
    Before,
    After
}

public record RelativeInsertionOptions(string ReferenceCallsign, RelativePosition Position) : IInsertFlightOptions;

public record ExactInsertionOptions(DateTimeOffset TargetLandingTime, string[] RunwayIdentifiers) : IInsertFlightOptions;

public record DepartureInsertionOptions(string OriginIdentifier, DateTimeOffset TakeoffTime) : IInsertFlightOptions;
