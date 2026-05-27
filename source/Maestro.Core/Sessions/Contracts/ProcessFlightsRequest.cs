using MediatR;

namespace Maestro.Core.Sessions.Contracts;

public record ProcessFlightsRequest(string AirportIdentifier) : IRequest;
