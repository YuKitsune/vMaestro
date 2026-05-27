using MediatR;

namespace Maestro.Core.Sessions.Contracts;

public record CleanUpLandedFlightsRequest(string AirportIdentifier) : IRequest;
