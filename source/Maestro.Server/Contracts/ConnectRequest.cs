using Maestro.Contracts.Connectivity;

namespace Maestro.Server.Contracts;

public record ConnectRequest(
    string Version,
    string Partition,
    string AirportIdentifier,
    string Callsign,
    Role Role);
