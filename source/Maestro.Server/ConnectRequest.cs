using Maestro.Core.Connectivity.Contracts;

namespace Maestro.Server;

public record ConnectRequest(
    string Version,
    string Partition,
    string AirportIdentifier,
    string Callsign,
    Role Role);
