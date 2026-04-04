using MediatR;

namespace Maestro.Core.Sessions.Contracts;

public record MaestroSessionCreatedNotification(string AirportIdentifier) : INotification;
