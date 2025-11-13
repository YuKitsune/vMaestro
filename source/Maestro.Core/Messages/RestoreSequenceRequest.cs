using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Messages;

public record RestoreSessionRequest(string AirportIdentifier, SessionMessage Session) : IRequest;
