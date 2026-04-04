using MediatR;

namespace Maestro.Core.Sessions.Contracts;

public record CreateMaestroSessionRequest(string AirportIdentifier) : IRequest;
