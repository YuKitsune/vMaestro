using MediatR;

namespace Maestro.Core.Sessions.Contracts;

public record MaestroSessionDestroyedNotification(string AirportIdentifier) : INotification;
