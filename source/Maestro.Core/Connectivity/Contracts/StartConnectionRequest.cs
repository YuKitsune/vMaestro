using MediatR;

namespace Maestro.Core.Connectivity.Contracts;

public record StartConnectionRequest(string AirportIdentifier) : IRequest;