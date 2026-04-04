using MediatR;

namespace Maestro.Core.Sessions.Contracts;

public record DestroyMaestroSessionRequest(string AirportIdentifier) : IRequest;
