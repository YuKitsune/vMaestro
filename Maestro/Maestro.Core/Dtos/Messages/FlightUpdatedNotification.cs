using Maestro.Core.Model;
using MediatR;

namespace Maestro.Core.Dtos.Messages;

public record CreateFlightRequest(
    string Callsign,
    string AircraftType,
    string Origin,
    string Destination);

public record ActivateFlightRequest(string Callsign);

public record FlightPlanUpdatedNotification(
    string Callsign,
    string AircraftType,
    string Origin,
    string Destination,
    string? AssignedRunway,
    string? AssignedStar,
    FixDto[] Estimates);

public record FlightUpdatedNotification(
    string Callsign,
    string AircraftType,
    string Origin,
    string Destination,
    string? AssignedRunway,
    string? AssignedStar,
    FixDto[] Estimates)
    : INotification;
