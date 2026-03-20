using Maestro.Contracts.Shared;
using MediatR;

namespace Maestro.Contracts.Flights;

public record FlightUpdatedNotification(
    string Callsign,
    string AircraftType,
    AircraftCategory AircraftCategory,
    WakeCategory WakeCategory,
    string Origin,
    string Destination,
    DateTimeOffset EstimatedDepartureTime,
    TimeSpan EstimatedFlightTime,
    FlightPosition? Position,
    FixEstimate[] Estimates)
    : INotification;
