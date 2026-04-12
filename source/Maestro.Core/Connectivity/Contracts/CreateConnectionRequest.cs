using MediatR;

namespace Maestro.Core.Connectivity.Contracts;

public record CreateConnectionRequest(string AirportIdentifier, string Environment) : IRequest;
