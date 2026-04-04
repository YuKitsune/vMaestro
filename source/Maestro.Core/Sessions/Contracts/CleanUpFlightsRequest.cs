using MediatR;

namespace Maestro.Core.Sessions.Contracts;

public record CleanUpFlightsRequest(string AirportIdentifier) : IRequest;
