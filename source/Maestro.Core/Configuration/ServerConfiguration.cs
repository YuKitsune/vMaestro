﻿namespace Maestro.Core.Configuration;

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
    public const string InsertDeparture = nameof(InsertDeparture);
    public const string InsertDummy = nameof(InsertDummy);
    public const string MakePending = nameof(MakePending);
    public const string ChangeRunway = nameof(ChangeRunway);
    public const string ManualDelay = nameof(ManualDelay);
    public const string MakeStable = nameof(MakeStable);
    public const string Recompute = nameof(Recompute);
    public const string Desequence = nameof(Desequence);
    public const string Resequence = nameof(Resequence);
    public const string RemoveFlight = nameof(RemoveFlight);
    public const string Coordination = nameof(Coordination);
}

public static class RoleHelper
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
