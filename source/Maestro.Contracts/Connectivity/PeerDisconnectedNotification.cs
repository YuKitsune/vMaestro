using MediatR;

namespace Maestro.Contracts.Connectivity;

public record PeerDisconnectedNotification(string AirportIdentifier, string Callsign) : INotification;
