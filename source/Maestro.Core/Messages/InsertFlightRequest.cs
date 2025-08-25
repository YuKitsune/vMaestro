using MediatR;

namespace Maestro.Core.Messages;

public enum RelativePosition
{
    Before,
    After
}

public record InsertFlightRequest(
    string AirportIdentifier,
    string? Callsign,
    string? AircraftType,
    IInsertFlightOptions Options)
    : IRequest;

public interface IInsertFlightOptions;

public record RelativeInsertionOptions(
    string ReferenceCallsign,
    RelativePosition Position)
    : IInsertFlightOptions;

public record ExactInsertionOptions(
    DateTimeOffset TargetLandingTime,
    string[] RunwayIdentifiers)
    : IInsertFlightOptions;
