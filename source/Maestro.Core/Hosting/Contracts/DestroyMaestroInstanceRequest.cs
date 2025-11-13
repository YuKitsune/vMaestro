using MediatR;

namespace Maestro.Core.Hosting.Contracts;

public record DestroyMaestroInstanceRequest(string AirportIdentifier) : IRequest;