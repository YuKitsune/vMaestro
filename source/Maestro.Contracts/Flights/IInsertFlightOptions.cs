using System.Text.Json.Serialization;

namespace Maestro.Contracts.Flights;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "OptionsType")]
[JsonDerivedType(typeof(RelativeInsertionOptions), "Relative")]
[JsonDerivedType(typeof(ExactInsertionOptions), "Exact")]
[JsonDerivedType(typeof(DepartureInsertionOptions), "Departure")]
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
