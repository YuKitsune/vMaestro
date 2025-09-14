namespace Maestro.Core.Configuration;

public class ServerConfiguration
{
    public required Uri Uri { get; init; }
    public required string[] Partitions { get; init; } = ["Default"];
    public required int TimeoutSeconds { get; init; } = 30;
    public required Dictionary<string, Role[]> Permissions { get; init; }
}

public enum Role
{
    Flow,
    Enroute,
    Approach,
    Observer
}

public static class ActionKeys
{
    public const string ChangeTerminalConfiguration = nameof(ChangeTerminalConfiguration);
    public const string ChangeLandingRates = nameof(ChangeLandingRates);
    public const string MoveFlight = nameof(MoveFlight);
    public const string ChangeFeederFixEstimate = nameof(ChangeFeederFixEstimate);
    public const string ManageSlots = nameof(ManageSlots);
    public const string InsertOvershoot = nameof(InsertOvershoot);
    public const string InsertPending = nameof(InsertPending);
    public const string InsertDummy = nameof(InsertDummy);
    public const string MakePending = nameof(MakePending);
    public const string ChangeRunway = nameof(ChangeRunway);
    public const string ChangeMaxDelay = nameof(ChangeMaxDelay);
    public const string MakeStable = nameof(MakeStable);
    public const string Recompute = nameof(Recompute);
    public const string Desequence = nameof(Desequence);
    public const string Resequence = nameof(Resequence);
    public const string RemoveFlight = nameof(RemoveFlight);
    public const string Coordination = nameof(Coordination);
}

public static class PermissionHelper
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

    public static IDictionary<string, Role[]> FullAccess()
    {
        return new Dictionary<string, Role[]>
        {
            { ActionKeys.ChangeTerminalConfiguration, [Role.Flow, Role.Enroute, Role.Approach] },
            { ActionKeys.ChangeLandingRates, [Role.Flow, Role.Enroute, Role.Approach] },
            { ActionKeys.MoveFlight, [Role.Flow, Role.Enroute, Role.Approach] },
            { ActionKeys.ChangeFeederFixEstimate, [Role.Flow, Role.Enroute, Role.Approach] },
            { ActionKeys.ManageSlots, [Role.Flow, Role.Enroute, Role.Approach] },
            { ActionKeys.InsertOvershoot, [Role.Flow, Role.Enroute, Role.Approach] },
            { ActionKeys.InsertPending, [Role.Flow, Role.Enroute, Role.Approach] },
            { ActionKeys.InsertDummy, [Role.Flow, Role.Enroute, Role.Approach] },
            { ActionKeys.MakePending, [Role.Flow, Role.Enroute, Role.Approach] },
            { ActionKeys.ChangeRunway, [Role.Flow, Role.Enroute, Role.Approach] },
            { ActionKeys.ChangeMaxDelay, [Role.Flow, Role.Enroute, Role.Approach] },
            { ActionKeys.MakeStable, [Role.Flow, Role.Enroute, Role.Approach] },
            { ActionKeys.Recompute, [Role.Flow, Role.Enroute, Role.Approach] },
            { ActionKeys.Desequence, [Role.Flow, Role.Enroute, Role.Approach] },
            { ActionKeys.Resequence, [Role.Flow, Role.Enroute, Role.Approach] },
            { ActionKeys.RemoveFlight, [Role.Flow, Role.Enroute, Role.Approach] },
            { ActionKeys.Coordination, [Role.Flow, Role.Enroute, Role.Approach] }
        };
    }
}
