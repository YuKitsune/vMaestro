using MediatR;

namespace Maestro.Core.Connectivity.Contracts;

public record PeerDisconnectedNotification(string AirportIdentifier, string Callsign) : INotification;