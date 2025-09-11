namespace Maestro.Server;

public class Connection(string id, GroupKey groupKey)
{
    public string Id { get; } = id;
    public GroupKey GroupKey { get; } = groupKey;
    public bool OwnsSequence { get; set; } = false;
}
