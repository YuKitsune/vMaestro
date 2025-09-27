using Maestro.Core.Configuration;

namespace Maestro.Server;

public record ConnectRequest(
    string Partition,
    string AirportIdentifier,
    string Callsign,
    Role Role);
