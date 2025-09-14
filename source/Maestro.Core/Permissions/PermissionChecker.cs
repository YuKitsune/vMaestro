using Maestro.Core.Configuration;
using Serilog;

namespace Maestro.Core.Permissions;

public interface IPermissionChecker
{
    bool CanPerformAction(string action, Role role);
}

public class PermissionChecker(ServerConfiguration serverConfiguration, ILogger logger) : IPermissionChecker
{
    public bool CanPerformAction(string action, Role role)
    {
        if (role == Role.Observer)
            return false;

        if (serverConfiguration.Permissions.TryGetValue(action, out var allowedRoles))
        {
            return allowedRoles.Contains(role);
        }

        logger.Warning("No permissions configured for action {Action}", action);
        return false;
    }
}
