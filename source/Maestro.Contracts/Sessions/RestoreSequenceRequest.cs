using MediatR;

namespace Maestro.Contracts.Sessions;

public record RestoreSessionRequest(string AirportIdentifier, SessionDto Session) : IRequest;
