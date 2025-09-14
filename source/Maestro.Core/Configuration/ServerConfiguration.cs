namespace Maestro.Core.Configuration;

public class ServerConfiguration
{
    public required Uri Uri { get; init; }
    public required string[] Partitions { get; init; } = ["Default"];
    public required int TimeoutSeconds { get; init; } = 30;
    public required Permissions Permissions { get; init; }
}

public enum Role
{
    Flow,
    Enroute,
    Approach,
    Observer
}

public class Permissions
{
    public required Role[] TODO { get; init; } = [];
}

public static class CallsignToRole
{
    public static Role GetRoleFromCallsign(string callsign)
    {
        if (callsign.EndsWith("_FMP"))
            return Role.Flow;

        if (callsign.EndsWith("_CTR") || callsign.EndsWith("_FSS"))
            return Role.Enroute;

        if (callsign.EndsWith("_APP") || callsign.EndsWith("_DEP"))
            return Role.Approach;

        return Role.Observer;
    }
}
