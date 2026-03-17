using Maestro.Core.Connectivity.Contracts;
using Maestro.Core.Model;

namespace Maestro.Server.Pages.Helpers;

public static class FormatHelpers
{
    public static string ToDDHHMM(DateTimeOffset time)
    {
        return time.ToString("ddHHmm");
    }

    public static string GetStateBadgeClass(State state) => state switch
    {
        State.Unstable => "bg-secondary",
        State.Stable => "bg-primary",
        State.SuperStable => "bg-info",
        State.Frozen => "bg-warning text-dark",
        State.Landed => "bg-success",
        _ => "bg-secondary"
    };

    public static string GetRoleBadgeClass(Role role) => role switch
    {
        Role.Flow => "bg-primary",
        Role.Enroute => "bg-info",
        Role.Approach => "bg-success",
        Role.Observer => "bg-secondary",
        _ => "bg-secondary"
    };
}
