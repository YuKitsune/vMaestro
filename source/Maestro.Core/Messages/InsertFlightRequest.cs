using System.Text.Json.Serialization;
using Maestro.Core.Connectivity.Contracts;
using MediatR;

namespace Maestro.Core.Messages;

public record InsertFlightRequest(
    string AirportIdentifier,
    string? Callsign,
    string? AircraftType,
    IInsertFlightOptions Options)
    : IRequest, IRelayableRequest;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "OptionsType")]
[JsonDerivedType(typeof(RelativeInsertionOptions), "Relative")]
[JsonDerivedType(typeof(ExactInsertionOptions), "Exact")]
[JsonDerivedType(typeof(DepartureInsertionOptions), "Departure")]
public interface IInsertFlightOptions;

public enum RelativePosition
{
    Before,
    After
}

public record RelativeInsertionOptions(string ReferenceCallsign, RelativePosition Position) : IInsertFlightOptions;

public record ExactInsertionOptions(DateTimeOffset TargetLandingTime, string[] RunwayIdentifiers) : IInsertFlightOptions;

public record DepartureInsertionOptions(string OriginIdentifier, DateTimeOffset TakeoffTime) : IInsertFlightOptions;
