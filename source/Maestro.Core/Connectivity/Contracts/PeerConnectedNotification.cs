using MediatR;

namespace Maestro.Core.Connectivity.Contracts;

public record PeerConnectedNotification(string AirportIdentifier, string Callsign, Role Role) : INotification;