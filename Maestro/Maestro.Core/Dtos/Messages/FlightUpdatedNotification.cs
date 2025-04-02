using MediatR;

namespace Maestro.Core.Dtos.Messages;

public record FlightUpdatedNotification(
    string Callsign,
    string AircraftType,
    string OriginIcao,
    string DestinationIcao,
    string? AssignedRunway,
    string? AssignedStar,
    FixDTO[] Estimates)
    : INotification;
