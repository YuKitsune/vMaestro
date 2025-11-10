using MediatR;

namespace Maestro.Core.Connectivity.Contracts;

public record StopConnectionRequest(string AirportIdentifier) : IRequest;