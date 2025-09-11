using MediatR;

namespace Maestro.Core.Messages;

public record CreateSessionRequest(string AirportIdentifier, string Partition) : IRequest;
public record SessionCreatedNotification(string AirportIdentifier) : INotification;

public record StartSessionRequest(string AirportIdentifier, string Position) : IRequest;
public record SessionStartedNotification(string AirportIdentifier, string Position) : INotification;

public record StopSessionRequest(string AirportIdentifier) : IRequest;
public record SessionStoppedNotification(string AirportIdentifier) : INotification;

public record DestroySessionRequest(string AirportIdentifier) : IRequest;
public record SessionDestroyedNotification(string AirportIdentifier) : INotification;
