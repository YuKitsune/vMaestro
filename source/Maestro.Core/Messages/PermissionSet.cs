using Maestro.Core.Configuration;

namespace Maestro.Core.Messages;

// TODO: Permissions are a mess, need to clean them up

public class PermissionSet(Role role, IReadOnlyDictionary<string, Role[]> permissions)
{
    public Role Role { get; } = role;
    public IReadOnlyDictionary<string, Role[]> Permissions { get; } = permissions;


    public bool CanPerformAction(string actionKey)
    {
        return Permissions.TryGetValue(actionKey, out var allowedRoles) && allowedRoles.Contains(Role);
    }

    public static PermissionSet Default => new(Role.Observer, PermissionHelper.FullAccess());
}
