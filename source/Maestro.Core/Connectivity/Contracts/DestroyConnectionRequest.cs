using MediatR;

namespace Maestro.Core.Connectivity.Contracts;

public record DestroyConnectionRequest(string AirportIdentifier) : IRequest;
