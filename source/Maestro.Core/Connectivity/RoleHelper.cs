using Maestro.Core.Connectivity.Contracts;

namespace Maestro.Core.Connectivity;

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
