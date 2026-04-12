using Maestro.Contracts.Connectivity;

namespace Maestro.Server;

public class Connection(string id, string version, string environment, string airportIdentifier, string callsign, Role role)
{
    public string Id { get; } = id;
    public string Version { get; } = version;
    public string Environment { get; } = environment;
    public string AirportIdentifier { get; } = airportIdentifier;
    public string Callsign { get; } = callsign;
    public Role Role { get; } = role;
    public bool IsMaster { get; set; } = false;

    public override string ToString() => $"{Id} ({Callsign})";
}
