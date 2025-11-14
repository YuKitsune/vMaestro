using MediatR;

namespace Maestro.Core.Hosting.Contracts;

public record CreateMaestroInstanceRequest(string AirportIdentifier) : IRequest;
