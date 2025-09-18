using Maestro.Core.Configuration;
using Maestro.Core.Messages.Connectivity;
using MediatR;

namespace Maestro.Core.Messages;

public record CreateSessionRequest(string AirportIdentifier) : IRequest;
public record SessionCreatedNotification(string AirportIdentifier) : INotification;

public record StartSessionRequest(string AirportIdentifier, string Position) : IRequest;
public record SessionStartedNotification(string AirportIdentifier, string Position) : INotification;

public record ConnectSessionRequest(string AirportIdentifier, string Partition) : IRequest;
public record SessionConnectedNotification(string AirportIdentifier, Role Role, IReadOnlyList<PeerInfo> Peers) : INotification;

public record DisconnectSessionRequest(string AirportIdentifier) : IRequest;
public record SessionDisconnectedNotification(string AirportIdentifier) : INotification;

public record StopSessionRequest(string AirportIdentifier) : IRequest;
public record SessionStoppedNotification(string AirportIdentifier) : INotification;

public record DestroySessionRequest(string AirportIdentifier) : IRequest;
public record SessionDestroyedNotification(string AirportIdentifier) : INotification;

public record PeerConnectedNotification(string AirportIdentifier, string Callsign, Role Role) : INotification;
public record PeerDisconnectedNotification(string AirportIdentifier, string Callsign) : INotification;
