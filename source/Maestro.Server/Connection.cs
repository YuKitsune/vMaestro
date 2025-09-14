using Maestro.Core.Configuration;

namespace Maestro.Server;

public class Connection(string id, GroupKey groupKey, string callsign, Role role)
{
    public string Id { get; } = id;
    public GroupKey GroupKey { get; } = groupKey;
    public string Callsign { get; } = callsign;
    public Role Role { get; } = role;
    public bool OwnsSequence { get; set; } = false;
}
