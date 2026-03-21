using Maestro.Contracts.Shared;
using MediatR;
using MessagePack;

namespace Maestro.Contracts.Flights;

[MessagePackObject]
public record FlightUpdatedNotification(
    [property: Key(0)] string Callsign,
    [property: Key(1)] string AircraftType,
    [property: Key(2)] AircraftCategory AircraftCategory,
    [property: Key(3)] WakeCategory WakeCategory,
    [property: Key(4)] string Origin,
    [property: Key(5)] string Destination,
    [property: Key(6)] DateTimeOffset EstimatedDepartureTime,
    [property: Key(7)] TimeSpan EstimatedFlightTime,
    [property: Key(8)] FlightPosition? Position,
    [property: Key(9)] FixEstimate[] Estimates)
    : INotification;
