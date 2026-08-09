using MediatR;

namespace Maestro.Core.Connectivity.Contracts;

public record CreateConnectionRequest(string AirportIdentifier, Uri ServerUrl, string Environment) : IRequest;
