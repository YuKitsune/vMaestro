using Maestro.Core.Connectivity.Contracts;

namespace Maestro.Server;

public class Connection(string id, string partition, string airportIdentifier, string callsign, Role role)
{
    public string Id { get; } = id;
    public string Partition { get; } = partition;
    public string AirportIdentifier { get; } = airportIdentifier;
    public string Callsign { get; } = callsign;
    public Role Role { get; } = role;
    public bool IsMaster { get; set; } = false;

    public override string ToString() => $"{Id} ({Callsign})";
}
