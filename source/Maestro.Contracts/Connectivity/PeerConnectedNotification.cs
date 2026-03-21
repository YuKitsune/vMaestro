using MediatR;

namespace Maestro.Contracts.Connectivity;

public record PeerConnectedNotification(string AirportIdentifier, string Callsign, Role Role) : INotification;
