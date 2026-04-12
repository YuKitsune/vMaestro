using Maestro.Contracts.Connectivity;

namespace Maestro.Server.Contracts;

public record ConnectRequest(
    string Version,
    string Environment,
    string AirportIdentifier,
    string Callsign,
    Role Role);
