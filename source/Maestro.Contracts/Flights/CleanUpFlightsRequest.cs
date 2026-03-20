using MediatR;

namespace Maestro.Contracts.Flights;

public record CleanUpFlightsRequest(string AirportIdentifier) : IRequest;
